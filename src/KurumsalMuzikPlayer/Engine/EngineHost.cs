using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Kmp.Core;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Engine;

/// <summary>
/// Motor: görünmez, tepside. Ana iş parçacığı sadece tepsi simgesini ve Windows iletilerini işler; müzik, pencere
/// borusu, bekçi ve deney kendi iş parçacıklarında (BRIEF §4). Ana döngünün takılması ayrıca ölçülüp günlüğe yazılır.
/// </summary>
sealed class EngineHost : ApplicationContext
{
    const int StallWarnMs = 3000;

    readonly NotifyIcon _tray;
    readonly TestMusicPlayer _music;
    readonly Session _session = new();
    readonly object _libraryGate = new();
    Library? _library;
    LocalMediaServer? _media;
    readonly Control _mainThread = new();
    readonly UiSupervisor _ui;
    readonly ArchiveExperiment? _experiment;
    readonly Settings _settings;
    readonly System.Windows.Forms.Timer _heartbeat;
    readonly System.Threading.Timer _stateTimer;
    readonly System.Threading.Timer _reportTimer;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    long _lastBeatMs;
    long _maxStallMs;
    int _stalls;
    int _shuttingDown;

    public EngineHost(Args args)
    {
        Log.Info("Motor başladı: " + AppInfo.Describe());
        var exe = Application.ExecutablePath;
        var mpvExe = Path.Combine(Path.GetDirectoryName(exe)!, "mpv", "mpv.exe");
        MpvClient.KillOrphans(mpvExe);
        _settings = Settings.Load();
        _ui = new UiSupervisor(exe);
        _mainThread.CreateControl();
        // Çökerse Windows motoru yeniden başlatsın (en az 60 sn çalışmış olmak şartıyla; Windows Hata Bildirimi).
        var hr = RegisterApplicationRestart("--gizli", 0);
        if (hr != 0) Log.Warn($"Çökünce yeniden başlatma kaydı yapılamadı (0x{hr:X})");

        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application,
            Text = AppPaths.ProductName,
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => _ui.Show();

        _music = new TestMusicPlayer(mpvExe, _settings.Volume, PlayItems);

        _ui.CommandReceived += OnUiCommand;
        _ui.ShowRequested += () => _ui.Show();
        _ui.QuitRequested += () => OnMainThread(Shutdown);
        _ui.Start();

        _session.ScreenChanged += OnScreenChanged;
        _session.Start();
        OnScreenChanged(_session.Current);
        // Giriş gerekiyorsa pencere her durumda açık gelir.
        if (!args.Has("--ui-acik")) _ui.Show(args.Has("--gizli") && _session.Current == Session.Screen.Player);

        if (args.Has("--deneme-arsiv"))
        {
            var pattern = args.Value("--desen") == "dosya" ? ArchiveExperiment.Pattern.Files : ArchiveExperiment.Pattern.Container;
            _experiment = new ArchiveExperiment(AppPaths.TestArchiveDir, args.Number("--gb", 6), pattern, args.Number("--mbps", 6));
            _experiment.Start();
        }

        _lastBeatMs = _clock.ElapsedMilliseconds;
        _heartbeat = new System.Windows.Forms.Timer { Interval = 500 };
        _heartbeat.Tick += (_, _) => Interlocked.Exchange(ref _lastBeatMs, _clock.ElapsedMilliseconds);
        _heartbeat.Start();
        new Thread(StallWatch) { IsBackground = true, Name = "motor-bekci" }.Start();

        _stateTimer = new System.Threading.Timer(_ => SendState(), null, 500, 1000);
        _reportTimer = new System.Threading.Timer(_ => Log.Info("Durum: " + Report()), null, 600_000, 600_000);

        SystemEvents.PowerModeChanged += (_, e) => Log.Info($"Güç: {e.Mode}");
        SystemEvents.SessionEnding += (_, e) =>
        {
            Log.Info($"Windows oturumu kapanıyor ({e.Reason})");
            OnMainThread(Shutdown);
        };
    }

    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add("Pencereyi aç", null, (_, _) => _ui.Show());
        m.Items.Add("Deneme müzik klasörünü aç", null, (_, _) => OpenFolder(AppPaths.TestMusicDir));
        m.Items.Add("Günlük klasörünü aç", null, (_, _) => OpenFolder(AppPaths.LogDir));
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Çıkış", null, (_, _) => Shutdown());
        return m;
    }

    static void OpenFolder(string path)
    {
        try { Process.Start("explorer.exe", "\"" + path + "\"")?.Dispose(); }
        catch (Exception e) { Log.Warn($"Klasör açılamadı: {e.Message}"); }
    }

    void OnMainThread(Action a)
    {
        try { _mainThread.BeginInvoke(a); }
        catch (Exception e) { Log.Error("Ana iş parçacığına iletilemedi", e); }
    }

    void OnScreenChanged(Session.Screen screen)
    {
        Log.Info($"Ekran: {screen}");
        switch (screen)
        {
            case Session.Screen.Player:
                StartLibrary();
                _music.Start();
                break;
            case Session.Screen.Login:
                StopLibrary();
                _music.Stop("Giriş bekleniyor");
                _ui.Show();
                break;
            case Session.Screen.Expired:
                StopLibrary();
                _music.Stop("Hizmet süresi doldu");
                _ui.Show();
                break;
        }
        SendState();
    }

    void StartLibrary()
    {
        lock (_libraryGate)
        {
            if (_library != null) return;
            _library = new Library(() => _session.Token, msg => System.Threading.Tasks.Task.Run(() => _session.ReportRemoved(msg)));
            _library.Changed += () => System.Threading.Tasks.Task.Run(_music.Refresh);
            _media = new LocalMediaServer(_library);
            _media.Start();
            _library.Start();
        }
    }

    void StopLibrary()
    {
        Library? lib;
        LocalMediaServer? media;
        lock (_libraryGate)
        {
            lib = _library;
            media = _media;
            _library = null;
            _media = null;
        }
        lib?.Stop();
        media?.Dispose();
    }

    /// <summary>
    /// Çalınacaklar. Faz 2: arşivden inmiş bütün şarkılar (şu anki tarzınkiler önce); arşiv boşsa deneme klasörü.
    /// Faz 3'te akışa (saat dilimlerine) göre çalan gerçek çalar gelir.
    /// </summary>
    List<PlayItem> PlayItems()
    {
        Library? lib;
        LocalMediaServer? media;
        lock (_libraryGate)
        {
            lib = _library;
            media = _media;
        }
        var m = lib?.Current;
        if (lib != null && media != null && m != null)
        {
            var order = m.GenreOrder(lib.IstanbulNow().Hour);
            var albums = m.Albums.ToDictionary(a => a.Id, a => a.Title);
            var genres = m.Genres.ToDictionary(g => g.Slug, g => g.Name);
            var items = lib.ReadyTracks()
                .OrderBy(t => order.IndexOf(t.Genre))
                .Select(t => new PlayItem
                {
                    Url = media.TrackUrl(t),
                    Title = t.Title,
                    Sub = $"{(albums.TryGetValue(t.Album, out var a) ? a : "")} · {(genres.TryGetValue(t.Genre, out var g) ? g : t.Genre)}",
                    FromArchive = true,
                })
                .ToList();
            if (items.Count > 0) return items;
        }
        return TestMusicPlayer.TestFolderItems();
    }

    void OnUiCommand(JObject m)
    {
        switch ((string?)m["t"])
        {
            case "login":
                var user = (string?)m["username"] ?? "";
                var pass = (string?)m["password"] ?? "";
                var branch = (string?)m["branch"] ?? "";
                _ = _session.LoginAsync(user, pass, branch).ContinueWith(t =>
                {
                    var r = t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion
                        ? t.Result
                        : new JObject { ["ok"] = false, ["error"] = "Giriş yapılamadı." };
                    r["t"] = "login-result";
                    _ui.Send(r);
                    SendState();
                });
                break;
            case "volume":
                var v = (int?)m["v"];
                if (v == null) return;
                _music.SetVolume(v.Value);
                _settings.Volume = _music.Volume;
                _settings.Save();
                break;
            case "next":
                _music.Next();
                break;
        }
    }

    void SendState()
    {
        try
        {
            var s = _music.Snapshot();
            s.Merge(_session.Describe());
            Library? lib;
            lock (_libraryGate) lib = _library;
            s["library"] = lib?.Describe();
            s["t"] = "state";
            s["exp"] = _experiment?.Summary();
            _ui.SendState(s);
        }
        catch (Exception e)
        {
            Log.Error("Durum gönderilemedi", e);
        }
    }

    void StallWatch()
    {
        var inStall = false;
        long worst = 0;
        while (true)
        {
            Thread.Sleep(1000);
            var gap = _clock.ElapsedMilliseconds - Interlocked.Read(ref _lastBeatMs);
            if (gap > StallWarnMs)
            {
                if (!inStall) Log.Warn("Motor ana döngüsü yanıt vermiyor");
                inStall = true;
                worst = Math.Max(worst, gap);
            }
            else if (inStall)
            {
                inStall = false;
                _stalls++;
                _maxStallMs = Math.Max(_maxStallMs, worst);
                Log.Warn($"Motor ana döngüsü {worst / 1000.0:0.0} sn takıldı, düzeldi");
                worst = 0;
            }
        }
    }

    string Report() =>
        $"çalışma {_clock.Elapsed:d\\.hh\\:mm}, motor takılması {_stalls} (en uzun {_maxStallMs / 1000.0:0.0} sn), "
        + $"pencere en geç yanıt {_ui.MaxPongMs} ms, pencere yeniden açma {_ui.Restarts}, mpv yeniden başlatma {_music.Restarts}"
        + (_experiment != null ? " · deney: " + _experiment.Summary() : "");

    void Shutdown()
    {
        if (Interlocked.Exchange(ref _shuttingDown, 1) != 0) return;
        Log.Info("Kapanıyor. Son durum: " + Report());
        _heartbeat.Stop();
        _stateTimer.Dispose();
        _reportTimer.Dispose();
        _ui.CloseUi();
        _experiment?.Stop();
        StopLibrary();
        _music.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int RegisterApplicationRestart(string commandLine, int flags);
}
