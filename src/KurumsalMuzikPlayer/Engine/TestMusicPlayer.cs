using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Engine;

/// <summary>Çalınacak bir şey: arşivden bir şarkı (yerel medya adresi) ya da deneme klasöründen bir dosya.</summary>
sealed class PlayItem
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Sub { get; set; } = "";
    public bool FromArchive { get; set; }
}

/// <summary>
/// Faz 0–2: verilen listeyi döngüde çalar (Faz 2'den beri arşivden inen şarkılar; arşiv boşsa deneme-muzik klasörü).
/// mpv çökerse kaldığı şarkının kaldığı saniyesinden yeniden başlar. Faz 3'te yerini akışa göre çalan gerçek çalar alır.
/// </summary>
sealed class TestMusicPlayer
{
    readonly string _mpvExe;
    readonly Func<List<PlayItem>> _source;
    readonly object _gate = new();
    List<PlayItem> _items = new();
    MpvClient? _mpv;
    int _offset;
    int _index;
    double _pos;
    double _dur;
    int _volume;
    string _status = "Ses motoru başlıyor";
    int _restarts;
    DateTime _lastRestart = DateTime.MinValue;
    volatile bool _stopping;
    bool _started;

    public TestMusicPlayer(string mpvExe, int volume, Func<List<PlayItem>> source)
    {
        _mpvExe = mpvExe;
        _volume = volume;
        _source = source;
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
            var mpv = new MpvClient(_mpvExe, "muzik");
            mpv.PropertyChanged += OnProperty;
            mpv.Crashed += OnCrashed;
            mpv.Start(new[] { "--audio-buffer=2", $"--volume={_volume}", "--loop-playlist=inf" });
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
            Load(_items.Count > 0 ? _items : _source(), startIndex, startPos);
        }
        catch (Exception e)
        {
            Log.Error("Ses motoru başlatılamadı", e);
            SetStatus("Ses motoru başlatılamadı: " + e.Message);
        }
    }

    void Load(List<PlayItem> items, int startIndex, double startPos)
    {
        MpvClient? mpv;
        lock (_gate)
        {
            mpv = _mpv;
            _items = items;
        }
        if (mpv == null) return;
        if (items.Count == 0)
        {
            SetStatus($"Çalınacak şarkı yok: arşiv iniyor ya da deneme müzik klasörü boş ({AppPaths.TestMusicDir})");
            return;
        }
        var n = items.Count;
        lock (_gate)
        {
            _offset = ((startIndex % n) + n) % n;
            _index = _offset;
        }
        if (startPos > 1) mpv.Command("loadfile", items[_offset].Url, "replace", "start=" + ((int)startPos).ToString());
        else mpv.Command("loadfile", items[_offset].Url, "replace");
        for (var i = 1; i < n; i++) mpv.Command("loadfile", items[(_offset + i) % n].Url, "append");
        SetStatus(items[0].FromArchive ? $"Çalıyor · arşivden {n} şarkı" : $"Çalıyor · {n} deneme dosyası");
    }

    /// <summary>
    /// Liste değişti (arşive yeni şarkılar indi): yeni şarkılar sona eklenir. Deneme dosyalarından arşive geçişte liste
    /// baştan kurulur.
    /// </summary>
    public void Refresh()
    {
        if (_stopping) return;
        var fresh = _source();
        MpvClient? mpv;
        List<PlayItem> current;
        lock (_gate)
        {
            mpv = _mpv;
            current = _items;
        }
        if (mpv == null || fresh.Count == 0) return;
        var switching = current.Count == 0 || current[0].FromArchive != fresh[0].FromArchive;
        if (switching && (fresh[0].FromArchive ? fresh.Count >= 3 : current.Count == 0))
        {
            Log.Info(fresh[0].FromArchive ? "Arşivden çalmaya geçiliyor" : "Deneme dosyaları çalıyor");
            Load(fresh, 0, 0);
            return;
        }
        if (switching) return;
        var known = new HashSet<string>(current.Select(i => i.Url));
        var added = fresh.Where(i => !known.Contains(i.Url)).ToList();
        if (added.Count == 0) return;
        foreach (var i in added) mpv.Command("loadfile", i.Url, "append");
        lock (_gate)
        {
            _items = current.Concat(added).ToList();
            _status = $"Çalıyor · arşivden {_items.Count} şarkı";
        }
    }

    void OnProperty(string name, JToken? value)
    {
        lock (_gate)
        {
            switch (name)
            {
                case "duration":
                    _dur = value?.Type is JTokenType.Float or JTokenType.Integer ? (double)value : 0;
                    break;
                case "playlist-pos":
                    if (value?.Type == JTokenType.Integer && _items.Count > 0)
                    {
                        _index = (_offset + (int)value) % _items.Count;
                        Log.Info($"Çalıyor: {_items[_index].Title}");
                    }
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
            var item = _items.Count > 0 ? _items[_index] : null;
            return new JObject
            {
                ["title"] = item?.Title ?? "—",
                ["sub"] = item?.Sub ?? "",
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
            _items = new List<PlayItem>();
            _status = status;
        }
        mpv?.Stop();
    }

    void SetStatus(string s)
    {
        lock (_gate) _status = s;
    }

    /// <summary>Deneme klasöründeki ses dosyaları, karışık.</summary>
    public static List<PlayItem> TestFolderItems()
    {
        string[] audio = { ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus", ".ts" };
        var rng = new Random();
        return Directory.GetFiles(AppPaths.TestMusicDir)
            .Where(f => audio.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(_ => rng.Next())
            .Select(f => new PlayItem { Url = f, Title = Path.GetFileNameWithoutExtension(f), Sub = Path.GetFileName(f) })
            .ToList();
    }
}
