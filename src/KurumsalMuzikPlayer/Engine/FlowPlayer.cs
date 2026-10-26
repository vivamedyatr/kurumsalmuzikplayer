using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Engine;

/// <summary>
/// Çalma (BRIEF §2.2; eski BRIEF §6): ne çalacağına burada karar verilir, mpv çalar; pencere sadece gösterir.
/// - Şu an hangi tarz? Akışın dilimleri, İstanbul saati (sunucuya göre düzeltilmiş). 00:00–08:00 son dilim sürer.
/// - O tarz henüz inmediyse sessizlik olmaz: inmiş başka bir tarz çalar, tarz inince ona geçilir.
/// - Tarzın inmiş şarkıları paketin karışık sırasıyla; rastgele bir şarkıdan başlanır, liste bitince yeniden yüklenir.
/// - Anons kuşağı: müzik duraklatılır, anonsları ikinci mpv çalar, sonra müzik tam kaldığı saniyeden sürer.
/// - "Sonraki şarkı": 10 sn'de bir, anons sırasında kapalı.
/// - Duraklat (1.0.1, karar Onur 25 Eki 2026): müzik aynı şarkının aynı saniyesinde bekler, devam edince oradan sürer.
///   Duraklatılmışken anons çalmaz (o dakikalar atlanır), yayın kaydına "durdu" yazılır. Program yeniden açılınca çalar.
/// - 7 gün internetsiz ya da hizmet süresi bitti: müzik durur (Clock.PlayGate).
/// - Ses çıkışı yoksa (hoparlör/ses aygıtı çıkarıldı, oturum kapalı) mpv her şarkıyı anında bitirir: art arda 3 hatada
///   çalma durur, 30 sn'de bir yeniden denenir (1.0.2). "Çaldı" kaydı ancak şarkı 1,5 sn çaldıktan sonra yazılır.
/// </summary>
sealed class FlowPlayer
{
    /// <summary>Tarz en az bu kadar şarkısı inince çalınır (az şarkıyla aynıları dönmesin); tarzın tamamı inmişse hep.</summary>
    const int MinReady = 3;
    const int SkipCooldownMs = 10_000;
    const int BreakGapMs = 400;
    const int ErrorStreakLimit = 3;
    const int RetryAfterErrorsMs = 30_000;
    const int PlayedAfterMs = 1500;

    sealed class Decision
    {
        public string Key = "";
        public string? Genre;
        public string GenreName = "";
        public string? WaitingFor;
        public int SlotHour;
    }

    readonly string _mpvExe;
    readonly Func<Library?> _library;
    readonly Func<LocalMediaServer?> _media;
    readonly object _gate = new();
    MpvClient? _music;
    MpvClient? _ann;
    Timer? _tick;
    Timer? _watchdog;
    Decision? _decision;
    string _loadedKey = "";
    List<Manifest.Track> _list = new();
    Manifest.Track? _current;
    string? _breakTitle;
    long _lastSkip = -SkipCooldownMs;
    int _reloading;
    int _volume;
    double _pos;
    string? _gateReason;
    volatile bool _running;
    volatile bool _paused;
    int _restarts;
    int _errorStreak;
    long _retryAtMs;
    volatile string? _audioIssue;

    public Announcer? Announcer { get; set; }
    public int Restarts => _restarts;

    public FlowPlayer(string mpvExe, int volume, Func<Library?> library, Func<LocalMediaServer?> media)
    {
        _mpvExe = mpvExe;
        _volume = volume;
        _library = library;
        _media = media;
    }

    // ---------- başlat / durdur ----------

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
        }
        Task.Run(() =>
        {
            StartMpv(music: true);
            StartMpv(music: false);
            _tick = new Timer(_ => Tick(), null, 0, 2000);
            _watchdog = new Timer(_ => _ = Watchdog(), null, 10_000, 10_000);
        });
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
        }
        _tick?.Dispose();
        _watchdog?.Dispose();
        _music?.Stop();
        _ann?.Stop();
        _music = _ann = null;
        _paused = false;
        _loadedKey = "";
        _current = null;
        _breakTitle = null;
        if (Announcer != null) Announcer.Ready = false;
        PlayLog.State(false);
    }

    void StartMpv(bool music)
    {
        if (!_running) return;
        try
        {
            if (!File.Exists(_mpvExe)) throw new FileNotFoundException("mpv.exe bulunamadı", _mpvExe);
            var name = music ? "muzik" : "anons";
            // Anons motorunda boşluksuz geçiş kapalı: açıkken "dosya bitti" olayı ~2 sn erken geliyor, müzik anonsun
            // son saniyelerinin üstüne giriyordu (2.x'te ölçüldü, 15 Eki 2026).
            var args = new List<string> { "--audio-buffer=2", $"--volume={_volume}", music ? "--gapless-audio=weak" : "--gapless-audio=no" };
            // KMP_SES_AYGITI: sadece deneme için (örn. olmayan bir aygıtla "ses çıkışı yok" durumunu görmek).
            var device = Environment.GetEnvironmentVariable("KMP_SES_AYGITI");
            if (!string.IsNullOrEmpty(device)) args.Add("--audio-device=" + device);
            var mpv = new MpvClient(_mpvExe, name);
            mpv.Crashed += () => OnCrashed(music);
            if (music)
            {
                mpv.PropertyChanged += (prop, value) =>
                {
                    if (prop == "playlist-pos" && value?.Type == JTokenType.Integer) OnTrack((int)value);
                };
                mpv.Event += (ev, msg) =>
                {
                    if (ev == "idle") _ = OnIdle();
                    else if (ev == "end-file" && (string?)msg["reason"] == "error") OnPlayError((string?)msg["file_error"]);
                };
            }
            mpv.Start(args);
            if (music) mpv.Observe(1, "playlist-pos");
            lock (_gate)
            {
                if (music) _music = mpv;
                else _ann = mpv;
            }
            if (music) _loadedKey = "";
        }
        catch (Exception e)
        {
            Log.Error($"Ses motoru başlatılamadı ({(music ? "müzik" : "anons")})", e);
        }
    }

    void OnCrashed(bool music)
    {
        if (!_running) return;
        Interlocked.Increment(ref _restarts);
        lock (_gate)
        {
            if (music) _music = null;
            else _ann = null;
        }
        // Kaldığı şarkı ve saniye (anons sırasında çökerse kuşak biter, müzik sürer).
        var track = _current;
        var pos = _pos;
        Task.Delay(2000).ContinueWith(_ =>
        {
            StartMpv(music);
            if (music && track != null && _decision?.Genre == track.Genre) Load(_decision!, false, track, pos);
        });
    }

    // ---------- karar ----------

    Decision? Decide()
    {
        var lib = _library();
        var m = lib?.Current;
        if (lib == null || m == null || m.Slots.Count == 0) return null;
        var hour = Clock.Istanbul().Hour;
        var slots = m.Slots.OrderBy(s => s.Hour).ToList();
        var slot = slots.LastOrDefault(s => s.Hour <= hour) ?? slots.Last();
        string Name(string slug) => m.Genres.FirstOrDefault(g => g.Slug == slug)?.Name ?? slug;
        bool Playable(string slug)
        {
            var ready = lib.ReadyTracks(slug).Count;
            var total = m.Tracks.Count(t => t.Genre == slug);
            return ready > 0 && (ready >= MinReady || ready == total);
        }
        var d = new Decision { SlotHour = slot.Hour, Genre = slot.Genre, GenreName = Name(slot.Genre) };
        if (!Playable(slot.Genre))
        {
            d.WaitingFor = Name(slot.Genre);
            var alt = m.GenreOrder(hour).FirstOrDefault(Playable);
            if (alt == null)
            {
                d.Genre = null;
                d.Key = $"none|{slot.Hour}";
                return d;
            }
            d.Genre = alt;
            d.GenreName = Name(alt);
        }
        d.Key = $"{slot.Hour}|{d.Genre}";
        return d;
    }

    void Tick()
    {
        try
        {
            if (!_running) return;
            if (!Clock.PlayGate(out var reason))
            {
                _gateReason = reason;
                _decision = null;
                StopMusic();
                return;
            }
            _gateReason = null;
            var d = Decide();
            _decision = d;
            if (d?.Genre == null)
            {
                StopMusic();
                return;
            }
            // Duraklatılmışken dilim değişse de bekler; devam edilince yeni tarza geçilir. Ses çıkışı hatasından sonra 30 sn bekler.
            if (d.Key != _loadedKey && _breakTitle == null && !_paused && NowMs() >= _retryAtMs) Load(d, false);
        }
        catch (Exception e)
        {
            Log.Error("Çalar kararı", e);
        }
    }

    void StopMusic()
    {
        if (_loadedKey == "") return;
        _loadedKey = "";
        _current = null;
        _music?.Command("stop");
        if (Announcer != null) Announcer.Ready = false;
        PlayLog.State(false);
    }

    /// <summary>Tarzın inmiş şarkılarını yükler. fromTop: liste bitti, baştan; değilse rastgele (ya da verilen) şarkıdan.</summary>
    void Load(Decision d, bool fromTop, Manifest.Track? resume = null, double resumePos = 0)
    {
        var lib = _library();
        var media = _media();
        var mpv = _music;
        if (lib == null || media == null || mpv == null || d.Genre == null || Interlocked.Exchange(ref _reloading, 1) == 1) return;
        try
        {
            var ready = lib.ReadyTracks(d.Genre);
            if (ready.Count == 0) return;
            var start = resume != null ? Math.Max(0, ready.FindIndex(t => t.Key == resume.Key)) : fromTop ? 0 : new Random().Next(ready.Count);
            var list = ready.Skip(start).Concat(ready.Take(start)).ToList();
            lock (_gate) _list = list;
            _loadedKey = d.Key;
            if (resume != null && resumePos > 1) mpv.Command("loadfile", media.TrackUrl(list[0]), "replace", "start=" + (int)resumePos);
            else mpv.Command("loadfile", media.TrackUrl(list[0]), "replace");
            foreach (var t in list.Skip(1)) mpv.Command("loadfile", media.TrackUrl(t), "append");
            mpv.Set("pause", _paused);
            if (_audioIssue == null) Log.Info($"Çalınıyor: {d.GenreName}, {list.Count} şarkı{(d.WaitingFor != null ? $" ({d.WaitingFor} iniyor)" : "")}");
        }
        catch (Exception e)
        {
            Log.Error("Liste yüklenemedi", e);
            _loadedKey = "";
        }
        finally
        {
            Interlocked.Exchange(ref _reloading, 0);
        }
    }

    void OnTrack(int pos)
    {
        Manifest.Track? t;
        lock (_gate) t = pos >= 0 && pos < _list.Count ? _list[pos] : null;
        if (t == null) return;
        // Hatalar yüzünden çalma durdurulduysa mpv'den geç gelen şarkı geçişi sayılmaz (30 sn sonra yeniden denenir).
        if (NowMs() < _retryAtMs)
        {
            _current = null;
            return;
        }
        _current = t;
        _pos = 0;
        var genre = _decision?.GenreName ?? "";
        // Açılamayan şarkı (ses çıkışı yok) milisaniyeler içinde biter: kayıt ancak gerçekten çalmaya devam ediyorsa yazılır.
        Task.Delay(PlayedAfterMs).ContinueWith(_ =>
        {
            if (!ReferenceEquals(_current, t) || !_running || NowMs() < _retryAtMs) return;
            _errorStreak = 0;
            if (_audioIssue != null)
            {
                _audioIssue = null;
                Log.Info("Ses çıkışı yeniden çalışıyor");
            }
            var album = _library()?.Current?.Albums.FirstOrDefault(a => a.Id == t.Album)?.Title ?? "";
            Log.Info($"Çalıyor: {t.Title} · {album}");
            History.Add("music", t.Title, $"{album} · {genre}", t.Album);
            PlayLog.State(!_paused);
            PlayLog.Song(t.Title, album, genre);
            if (Announcer != null) Announcer.Ready = !_paused && _breakTitle == null;
        });
    }

    /// <summary>Şarkı açılamadı. Art arda birkaç kez olursa (çoğunlukla ses çıkışı yok) çalma durur, 30 sn sonra yeniden denenir.</summary>
    void OnPlayError(string? error)
    {
        if (!_running) return;
        var streak = Interlocked.Increment(ref _errorStreak);
        if (streak == 1) Log.Warn($"Şarkı çalınamadı: {error ?? "bilinmeyen hata"}");
        if (streak < ErrorStreakLimit) return;
        _errorStreak = 0;
        _retryAtMs = NowMs() + RetryAfterErrorsMs;
        var issue = error != null && error.IndexOf("audio output", StringComparison.OrdinalIgnoreCase) >= 0
            ? "Ses çıkışı açılamadı: hoparlör ya da ses aygıtı bulunamadı. 30 sn'de bir yeniden deneniyor."
            : "Şarkılar çalınamıyor; 30 sn'de bir yeniden deneniyor.";
        if (_audioIssue == null) Log.Warn($"Art arda {ErrorStreakLimit} şarkı çalınamadı ({error}); çalma durdu, 30 sn'de bir yeniden denenecek");
        _audioIssue = issue;
        StopMusic();
    }

    /// <summary>Liste bitti: yeniden yükle (bu arada inen şarkılar da girer), baştan çal.</summary>
    async Task OnIdle()
    {
        if (_breakTitle != null || _reloading == 1 || _decision?.Genre == null || _paused || NowMs() < _retryAtMs || _loadedKey == "") return;
        await Task.Delay(300).ConfigureAwait(false);
        var mpv = _music;
        if (mpv == null) return;
        var idle = await mpv.GetAsync("idle-active").ConfigureAwait(false);
        if (idle?.Type == JTokenType.Boolean && (bool)idle && _decision?.Genre != null) Load(_decision, true);
    }

    double _watchPos = -1;
    int _stuckChecks;

    /// <summary>
    /// Bekçi (10 sn'de bir): motor boşta kaldıysa ya da kendiliğinden duraklatıldıysa yeniden çal; şarkı 30 sn ilerlemediyse
    /// (örn. okunamayan parça) sonrakine geç (1.0.2, karar Onur: bozuk şarkı yeniden indirilmez, atlanır).
    /// </summary>
    async Task Watchdog()
    {
        var mpv = _music;
        if (!_running || _paused || _breakTitle != null || _reloading == 1 || _decision?.Genre == null || mpv == null || _loadedKey == "")
        {
            _stuckChecks = 0;
            _watchPos = -1;
            return;
        }
        var idle = await mpv.GetAsync("idle-active").ConfigureAwait(false);
        if (idle?.Type == JTokenType.Boolean && (bool)idle)
        {
            Log.Warn("Müzik boşta kalmış; yeniden başlatılıyor");
            _stuckChecks = 0;
            Load(_decision!, false);
            return;
        }
        var paused = await mpv.GetAsync("pause").ConfigureAwait(false);
        if (paused?.Type == JTokenType.Boolean && (bool)paused)
        {
            mpv.Set("pause", false);
            return;
        }
        var t = await mpv.GetAsync("time-pos").ConfigureAwait(false);
        var pos = t?.Type is JTokenType.Float or JTokenType.Integer ? (double)t : -1;
        var stuck = pos < 0 || Math.Abs(pos - _watchPos) < 0.5;
        _watchPos = pos;
        if (!stuck || _breakTitle != null || _paused)
        {
            _stuckChecks = 0;
            return;
        }
        if (++_stuckChecks < 3) return;
        _stuckChecks = 0;
        Log.Warn($"Şarkı 30 sn ilerlemedi ({_current?.Title ?? "?"}); sonrakine geçiliyor");
        mpv.Command("playlist-next", "force");
    }

    // ---------- düğmeler ----------

    static readonly System.Diagnostics.Stopwatch Sw = System.Diagnostics.Stopwatch.StartNew();
    static long NowMs() => Sw.ElapsedMilliseconds;

    public int SkipWaitMs() => (int)Math.Max(0, SkipCooldownMs - (NowMs() - _lastSkip));

    public bool Skip()
    {
        if (_breakTitle != null || _paused || SkipWaitMs() > 0 || _current == null) return false;
        _lastSkip = NowMs();
        _music?.Command("playlist-next", "force");
        return true;
    }

    public int Volume => _volume;

    /// <summary>Duraklat / devam. Anons sırasında ve yayın yokken kabul edilmez.</summary>
    public bool SetPaused(bool paused)
    {
        if (paused == _paused) return true;
        if (paused && (_breakTitle != null || _current == null)) return false;
        _paused = paused;
        if (paused)
        {
            if (Announcer != null) Announcer.Ready = false;
            _music?.Set("pause", true);
            PlayLog.State(false);
            Log.Info("Duraklatıldı");
        }
        else
        {
            _music?.Set("pause", false);
            if (_current != null)
            {
                PlayLog.State(true);
                if (Announcer != null) Announcer.Ready = true;
            }
            Log.Info("Devam ediyor");
            // Duraklatılmışken dilim değiştiyse yeni tarz hemen yüklensin.
            Task.Run(Tick);
        }
        return true;
    }

    public void SetVolume(int v)
    {
        _volume = Math.Max(0, Math.Min(100, v));
        _music?.Set("volume", _volume);
        _ann?.Set("volume", _volume);
    }

    // ---------- anons kuşağı ----------

    /// <summary>Kuşaktaki anonsları sırayla çalar; müzik duraklatılır, sonra kaldığı saniyeden sürer.</summary>
    public async Task PlayBreak(List<Announce.Item> items)
    {
        var music = _music;
        var ann = _ann;
        var announcer = Announcer;
        if (music == null || ann == null || _current == null || _breakTitle != null)
        {
            announcer?.BreakEnded();
            return;
        }
        try
        {
            if (announcer != null) announcer.Ready = false;
            _breakTitle = items[0].Title;
            music.Set("pause", true);
            foreach (var it in items)
            {
                var file = Announcer.FileOf(it);
                if (file == null) continue;
                _breakTitle = it.Title;
                if (await PlayAnnouncement(ann, file).ConfigureAwait(false)) announcer?.Played(it);
                else Log.Warn($"Anons çalınamadı: {it.Title}");
            }
            // Anons ile müzik arasında kısa sessizlik (birbirine girmesin).
            await Task.Delay(BreakGapMs).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Anons kuşağı", e);
        }
        finally
        {
            _breakTitle = null;
            _music?.Set("pause", _paused);
            if (announcer != null) announcer.Ready = _current != null && !_paused;
            announcer?.BreakEnded();
        }
    }

    /// <summary>Anons bitene kadar bekler; çalmaya başladıysa true (çalma kaydı için).</summary>
    static async Task<bool> PlayAnnouncement(MpvClient ann, string file)
    {
        var started = false;
        var done = new TaskCompletionSource<bool>();
        void OnEvent(string ev, JObject e)
        {
            if (ev == "playback-restart") started = true;
            else if (ev == "end-file") done.TrySetResult(started && (string?)e["reason"] != "error");
        }
        ann.Event += OnEvent;
        try
        {
            ann.Command("loadfile", file, "replace");
            var finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromMinutes(10))).ConfigureAwait(false);
            return finished == done.Task && done.Task.Result;
        }
        finally
        {
            ann.Event -= OnEvent;
        }
    }

    // ---------- pencere için ----------

    public JObject Snapshot()
    {
        var mpv = _music;
        mpv?.Request(v =>
        {
            if (v?.Type is JTokenType.Float or JTokenType.Integer) _pos = (double)v;
        }, "get_property", "time-pos");
        var lib = _library();
        var m = lib?.Current;
        var d = _decision;
        var t = _current;
        var genre = d?.Genre != null ? m?.Genres.FirstOrDefault(g => g.Slug == d.Genre) : null;
        var album = t != null ? m?.Albums.FirstOrDefault(a => a.Id == t.Album) : null;
        string status;
        if (_gateReason != null) status = _gateReason;
        else if (d == null) status = "Paket bekleniyor";
        else if (d.Genre == null) status = $"{d.WaitingFor} iniyor; ilk şarkılar inince yayın başlar";
        else if (_paused) status = "Duraklatıldı";
        else if (_audioIssue != null && _current == null) status = _audioIssue;
        else if (d.WaitingFor != null) status = $"{d.WaitingFor} iniyor; o inene kadar {d.GenreName} çalıyor";
        else status = "Çalıyor";
        return new JObject
        {
            ["genre"] = d?.GenreName,
            ["genreColors"] = genre?.Cover is { } c ? new JObject { ["from"] = c.From, ["to"] = c.To, ["ink"] = c.Ink } : null,
            ["slotHour"] = d?.SlotHour,
            ["title"] = t?.Title ?? (d?.Genre == null ? "Yayın hazırlanıyor" : "—"),
            ["sub"] = album?.Title ?? "",
            ["cover"] = t != null ? lib?.CoverFile(t.Album) : null,
            ["pos"] = _pos,
            ["dur"] = t != null ? t.DurationMs / 1000.0 : 0,
            ["vol"] = _volume,
            ["status"] = status,
            ["gate"] = _gateReason,
            ["breakTitle"] = _breakTitle,
            ["paused"] = _paused,
            ["audioIssue"] = _current == null ? _audioIssue : null,
            ["skipWaitMs"] = SkipWaitMs(),
            ["offlineDaysLeft"] = Clock.OfflineDaysLeft(),
            ["slots"] = m == null ? null : new JArray(m.Slots.OrderBy(s => s.Hour).Select(s =>
            {
                var g = m.Genres.FirstOrDefault(x => x.Slug == s.Genre);
                return new JObject { ["hour"] = s.Hour, ["name"] = g?.Name ?? s.Genre, ["from"] = g?.Cover?.From, ["to"] = g?.Cover?.To, ["ink"] = g?.Cover?.Ink };
            })),
            ["minute"] = Clock.DayMinute(Clock.Istanbul()),
        };
    }
}
