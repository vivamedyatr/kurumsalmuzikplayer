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
/// Faz 0: deneme-muzik klasöründeki dosyaları karışık ve döngüde çalar. mpv çökerse kaldığı şarkının kaldığı
/// saniyesinden yeniden başlar. Faz 3'te yerini akışa göre çalan gerçek çalar alır.
/// </summary>
sealed class TestMusicPlayer
{
    static readonly string[] AudioExtensions = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus", ".ts" };

    readonly string _mpvExe;
    readonly object _gate = new();
    List<string> _files = new();
    MpvClient? _mpv;
    int _offset;
    int _index;
    double _pos;
    double _dur;
    string? _title;
    int _volume;
    string _status = "Ses motoru başlıyor";
    int _restarts;
    DateTime _lastRestart = DateTime.MinValue;
    volatile bool _stopping;
    bool _started;

    public TestMusicPlayer(string mpvExe, int volume)
    {
        _mpvExe = mpvExe;
        _volume = volume;
    }

    public int Restarts => _restarts;

    /// <summary>Çalmayı başlatır (çalıyorsa bir şey yapmaz).</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (!_stopping && _started) return;
            _started = true;
            _stopping = false;
            _status = "Ses motoru başlıyor";
        }
        Task.Run(() => StartCore(_index, 0));
    }

    void StartCore(int startIndex, double startPos)
    {
        if (_stopping) return;
        try
        {
            if (!File.Exists(_mpvExe)) throw new FileNotFoundException("mpv.exe bulunamadı", _mpvExe);
            if (_files.Count == 0)
            {
                var rng = new Random();
                _files = Directory.GetFiles(AppPaths.TestMusicDir)
                    .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(_ => rng.Next()).ToList();
            }
            var mpv = new MpvClient(_mpvExe, "muzik");
            mpv.PropertyChanged += OnProperty;
            mpv.Crashed += OnCrashed;
            mpv.Start(new[] { "--audio-buffer=2", $"--volume={_volume}", "--loop-playlist=inf" });
            mpv.Observe(1, "media-title");
            mpv.Observe(2, "duration");
            mpv.Observe(3, "playlist-pos");
            lock (_gate)
            {
                if (_stopping)
                {
                    mpv.Stop();
                    return;
                }
                _mpv = mpv;
            }
            if (_files.Count == 0)
            {
                SetStatus($"Deneme müzik klasörü boş: {AppPaths.TestMusicDir}");
                return;
            }
            var n = _files.Count;
            _offset = ((startIndex % n) + n) % n;
            if (startPos > 1) mpv.Command("loadfile", _files[_offset], "replace", "start=" + ((int)startPos).ToString());
            else mpv.Command("loadfile", _files[_offset], "replace");
            for (var i = 1; i < n; i++) mpv.Command("loadfile", _files[(_offset + i) % n], "append");
            SetStatus($"Çalıyor · {n} deneme dosyası");
        }
        catch (Exception e)
        {
            Log.Error("Ses motoru başlatılamadı", e);
            SetStatus("Ses motoru başlatılamadı: " + e.Message);
        }
    }

    void OnProperty(string name, JToken? value)
    {
        lock (_gate)
        {
            switch (name)
            {
                case "media-title":
                    _title = value?.Type == JTokenType.String ? (string?)value : null;
                    if (_title != null) Log.Info($"Çalıyor: {_title}");
                    break;
                case "duration":
                    _dur = value?.Type is JTokenType.Float or JTokenType.Integer ? (double)value : 0;
                    break;
                case "playlist-pos":
                    if (value?.Type == JTokenType.Integer && _files.Count > 0) _index = (_offset + (int)value) % _files.Count;
                    break;
            }
        }
    }

    void OnCrashed()
    {
        if (_stopping) return;
        int index;
        double pos;
        lock (_gate)
        {
            _mpv = null;
            index = _index;
            pos = _pos;
        }
        _restarts++;
        // Arka arkaya çökerse bekleme uzar (ses kartı yoksa vb. sonsuz döngü olmasın).
        var wait = DateTime.Now - _lastRestart < TimeSpan.FromMinutes(1) ? 30_000 : 2_000;
        _lastRestart = DateTime.Now;
        SetStatus("Ses motoru yeniden başlatılıyor");
        Task.Delay(wait).ContinueWith(_ => StartCore(index, pos));
    }

    /// <summary>Saniyede bir çağrılır: konumu sorar, ekrana gidecek durumu verir.</summary>
    public JObject Snapshot()
    {
        MpvClient? mpv;
        lock (_gate) mpv = _mpv;
        mpv?.Request(v =>
        {
            if (v?.Type is JTokenType.Float or JTokenType.Integer) lock (_gate) _pos = (double)v;
        }, "get_property", "time-pos");
        lock (_gate)
        {
            var file = _files.Count > 0 ? _files[_index] : null;
            return new JObject
            {
                ["title"] = _title ?? (file != null ? Path.GetFileNameWithoutExtension(file) : "—"),
                ["sub"] = file != null ? Path.GetFileName(file) : AppPaths.TestMusicDir,
                ["pos"] = _pos,
                ["dur"] = _dur,
                ["vol"] = _volume,
                ["status"] = _status,
            };
        }
    }

    public void SetVolume(int volume)
    {
        volume = Math.Max(0, Math.Min(100, volume));
        MpvClient? mpv;
        lock (_gate)
        {
            _volume = volume;
            mpv = _mpv;
        }
        mpv?.Command("set_property", "volume", volume);
    }

    public int Volume { get { lock (_gate) return _volume; } }

    public void Next()
    {
        MpvClient? mpv;
        lock (_gate) mpv = _mpv;
        mpv?.Command("playlist-next");
    }

    /// <summary>Çalmayı durdurur (giriş ekranı, süre doldu, kapanış).</summary>
    public void Stop(string status = "Durdu")
    {
        MpvClient? mpv;
        lock (_gate)
        {
            _stopping = true;
            mpv = _mpv;
            _mpv = null;
            _status = status;
        }
        mpv?.Stop();
    }

    void SetStatus(string s)
    {
        lock (_gate) _status = s;
    }
}
