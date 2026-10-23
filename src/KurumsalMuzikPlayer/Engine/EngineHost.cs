using System;
using System.Diagnostics;
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

        _tray = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(exe) ?? SystemIcons.Application,
            Text = AppPaths.ProductName,
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => _ui.Show();

        _music = new TestMusicPlayer(mpvExe, _settings.Volume);
        _music.Start();

        _ui.CommandReceived += OnUiCommand;
        _ui.ShowRequested += () => _ui.Show();
        _ui.Start();
        if (!args.Has("--ui-acik")) _ui.Show(args.Has("--gizli"));

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
            Shutdown();
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

    void OnUiCommand(JObject m)
    {
        switch ((string?)m["t"])
        {
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
        _music.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }
}
