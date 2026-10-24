using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>
/// Yerel arşiv (BRIEF §2.2, §5): akışın offline paketi.
/// - İndirme kendi iş parçacığında, düşük öncelikte; hız sınırı 6 Mbps; kesilip devam eder (biten parça bir daha inmez).
/// - Öncelik: şu an çalan tarz, sonra sıradaki saatlerinki, sonra diğerleri; tarz içinde paket sırasıyla.
/// - Parçalar Worker'ın /m/ adresinden açık gelir, bellekte bu bilgisayara özel anahtarla şifrelenip albüm kabına eklenir
///   (karar Onur 24 Eki 2026; diskte açık hâli hiç durmaz). Çalarken bellekte çözülür. Anahtar bilgisayarda üretilir,
///   DPAPI ile saklanır: klasör başka bilgisayara ya da kullanıcıya kopyalansa açılmaz.
/// - Pakette olmayan albümler ancak paket tamamen inince, dosya dosya silinir.
/// </summary>
public sealed class Library : IDisposable
{
    const int ManifestEveryMs = 60 * 60_000;
    const int RetryMinMs = 30_000;
    const int RetryMaxMs = 10 * 60_000;

    static readonly int SpeedKbps =
        int.TryParse(Environment.GetEnvironmentVariable("KMP_HIZ_KBPS"), out var k) && k > 0 ? k : 6000;
    static readonly byte[] KeyEntropy = Encoding.UTF8.GetBytes("Kurumsal Müzik Player · arşiv");
    /// <summary>Bilgisayara özel anahtarın sürümü (arsiv\c1\). Değişirse arşiv bir kez yeniden iner.</summary>
    const string LocalKeyId = "c1";

    readonly Func<string?> _token;
    readonly Action<string?> _onRemoved;
    readonly object _gate = new();
    readonly Dictionary<string, AlbumContainer> _containers = new();
    readonly HashSet<string> _skipTracks = new();
    readonly AutoResetEvent _poke = new(false);
    readonly Stopwatch _clock = Stopwatch.StartNew();
    Thread? _thread;
    volatile bool _stop;
    volatile Manifest? _manifest;
    byte[]? _key;
    string? _keyId;
    DeviceState _device = new();
    long _lastManifestMs = long.MinValue / 2;
    string? _cleanedFor;
    bool _coversFor;
    long _coversTriedMs = -600_001;

    // Ekrana giden durum
    string _state = "starting";
    string? _message;
    DateTime? _checkedAt;
    int _total;
    int _done;

    sealed class DeviceState
    {
        public List<string>? Albums { get; set; }
        public long? LastUpdateAt { get; set; }
        public bool Updates { get; set; } = true;
    }

    sealed class StoredKey
    {
        public string Id { get; set; } = "";
        public string Key { get; set; } = "";
    }

    public Library(Func<string?> token, Action<string?> onRemoved)
    {
        _token = token;
        _onRemoved = onRemoved;
    }

    static string ArchiveRoot => Path.Combine(AppPaths.DataDir, "arsiv");
    static string ManifestFile => Path.Combine(AppPaths.DataDir, "paket.json");
    static string DeviceFile => Path.Combine(AppPaths.DataDir, "cihaz-paket.json");
    static string KeyFile => Path.Combine(AppPaths.DataDir, "arsiv-anahtari.bin");
    static string CoverDir => Path.Combine(AppPaths.DataDir, "kapak");

    /// <summary>Paket değişti ya da yeni şarkılar indi (çalar listeyi tazeler).</summary>
    public event Action? Changed;

    public Manifest? Current => _manifest;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "arsiv", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _poke.Set();
        if (Thread.CurrentThread != _thread) _thread?.Join(5000);
        lock (_gate)
        {
            foreach (var c in _containers.Values) c.Dispose();
            _containers.Clear();
        }
    }

    public void Dispose() => Stop();

    /// <summary>Paketi hemen yeniden sor (akış değişti, oturum açıldı vb.).</summary>
    /// <summary>
    /// "Akışı düzenle"ye basıldı: şube web sitesinde akışı değiştirecek. Saatlik sorguyu beklemeden 2, 5, 10 ve 20 dk
    /// sonra paket yeniden sorulur; yeni tarz hemen inmeye başlar (eski tarzlar yenisi inene kadar çalar).
    /// </summary>
    public void RefreshAfterFlowEdit()
    {
        foreach (var min in new[] { 2, 5, 10, 20 })
            System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(min)).ContinueWith(_ => { if (!_stop) RefreshSoon(); });
    }

    /// <summary>"Aylık yeni müzik" anahtarı (karar 10 Eki 2026). Açılınca 30 gün dolmuşsa sunucu hemen yeniler.</summary>
    public void SetUpdates(bool on)
    {
        lock (_gate)
        {
            if (_device.Updates == on) return;
            _device.Updates = on;
        }
        WriteJson(DeviceFile, _device);
        Log.Info("Aylık yeni müzik: " + (on ? "açık" : "kapalı"));
        RefreshSoon();
    }

    public void RefreshSoon()
    {
        _lastManifestMs = long.MinValue / 2;
        _poke.Set();
    }

    // ---------- çalar için ----------

    /// <summary>Bu tarzın bütün parçaları inmiş şarkıları, paket sırasıyla.</summary>
    public List<Manifest.Track> ReadyTracks(string? genre = null)
    {
        var m = _manifest;
        if (m == null || _key == null) return new List<Manifest.Track>();
        return m.Tracks.Where(t => (genre == null || t.Genre == genre) && IsComplete(t)).ToList();
    }

    /// <summary>Parçayı okur ve bellekte çözer; açık hâli diske yazılmaz. Yoksa ya da bozuksa null.</summary>
    public byte[]? ReadSegment(Manifest.Track track, int i)
    {
        var key = _key;
        var c = Container(track.Album, false);
        var data = c?.TryRead(track.Id, i);
        if (data == null || key == null) return null;
        return ArchiveCrypto.Crypt(key, track.SegmentKey(i), data);
    }

    public string? CoverFile(string albumId)
    {
        try { return Directory.GetFiles(CoverDir, albumId + ".*").FirstOrDefault(f => !f.EndsWith(".part")); }
        catch { return null; }
    }

    /// <summary>İstanbul saati (sunucu saatine göre düzeltilmiş).</summary>
    public DateTime IstanbulNow() => Clock.Istanbul().DateTime;

    public JObject Describe()
    {
        lock (_gate)
        {
            return new JObject
            {
                ["state"] = _state,
                ["message"] = _message,
                ["total"] = _total,
                ["done"] = _done,
                ["checkedAt"] = _checkedAt?.ToString("HH:mm"),
                ["speedKbps"] = SpeedKbps,
                ["updates"] = _device.Updates,
                // Sıradaki aylık değişim (son değişim + 30 gün); sunucu karar verir, bu sadece bilgi.
                ["nextUpdate"] = _device.Updates && _device.LastUpdateAt is long last
                    ? Clock.Istanbul(last + 30L * 86_400_000).ToString("dd.MM.yyyy") : null,
            };
        }
    }

    // ---------- ana döngü ----------

    void Run()
    {
        try
        {
            LoadLocal();
            RemoveOtherKeyFolders();
            var backoff = RetryMinMs;
            var tokenFails = 0;
            while (!_stop)
            {
                if (_clock.ElapsedMilliseconds - _lastManifestMs >= ManifestEveryMs || TokenExpiring()) FetchManifest();
                if (_stop) break;
                var m = _manifest;
                if (m == null)
                {
                    SetState("offline", "Paket bilgisi alınamadı; internet gelince tekrar denenecek.");
                    Nap(backoff);
                    backoff = Math.Min(backoff * 2, RetryMaxMs);
                    continue;
                }
                if (_key == null)
                {
                    SetState("offline", "Arşiv anahtarı alınamadı; tekrar denenecek.");
                    Nap(backoff);
                    backoff = Math.Min(backoff * 2, RetryMaxMs);
                    continue;
                }
                // Kapaklar küçük: paket gelir gelmez iner (ekranda hemen görünsün).
                if (!_coversFor && _clock.ElapsedMilliseconds - _coversTriedMs > 600_000)
                {
                    _coversTriedMs = _clock.ElapsedMilliseconds;
                    _coversFor = SyncCovers(m);
                }
                var job = Next(m);
                if (job == null)
                {
                    Recount();
                    SetState("ready", null);
                    if (_cleanedFor != m.Version)
                    {
                        _cleanedFor = m.Version;
                        Cleanup(m);
                    }
                    Nap(ManifestEveryMs);
                    continue;
                }
                if (TokenExpiring())
                {
                    Nap(backoff);
                    backoff = Math.Min(backoff * 2, RetryMaxMs);
                    continue;
                }
                SetState("downloading", null);
                var (track, i) = job.Value;
                try
                {
                    var sw = Stopwatch.StartNew();
                    var bytes = Download(m, track, i);
                    Container(track.Album, true)!.Append(track.Id, i, bytes, bytes.Length);
                    lock (_gate) _done++;
                    if (i == track.Segments.Count - 1) Changed?.Invoke();
                    backoff = RetryMinMs;
                    tokenFails = 0;
                    var wait = bytes.Length * 8.0 / SpeedKbps - sw.ElapsedMilliseconds;
                    if (wait > 0) Nap((int)wait);
                }
                catch (MediaDenied)
                {
                    // İlk ret: izin süresi dolmuş olabilir, paket (ve yeni izin) hemen istenir. Tekrar reddedilirse
                    // sunucu meşgul edilmez: artan aralıklarla denenir.
                    FetchManifest();
                    if (++tokenFails > 1)
                    {
                        Log.Warn($"Medya sunucusu indirmeye izin vermedi ({backoff / 1000} sn sonra tekrar)");
                        SetState("offline", "Sunucu indirmeye izin vermedi; tekrar denenecek.");
                        Nap(backoff);
                        backoff = Math.Min(backoff * 2, RetryMaxMs);
                    }
                }
                catch (MediaMissing)
                {
                    lock (_gate) _skipTracks.Add(track.Key);
                    Log.Warn($"Parça sunucuda yok, şarkı atlandı: {track.Key}/{i:0000}");
                }
                catch (IOException e) when ((e.HResult & 0xFFFF) is 0x70 or 0x27)
                {
                    SetState("disk-full", "Diskte yer kalmadı. Yer açılınca indirme kendiliğinden devam eder.");
                    Log.Warn("İndirme durdu: disk dolu");
                    Nap(backoff);
                    backoff = Math.Min(backoff * 2, RetryMaxMs);
                }
                catch (Exception e)
                {
                    SetState("offline", Explain(e));
                    Log.Warn($"İndirme durdu: {Explain(e)} ({backoff / 1000} sn sonra tekrar)");
                    Nap(backoff);
                    backoff = Math.Min(backoff * 2, RetryMaxMs);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Arşiv durdu", e);
            SetState("offline", "Arşiv hatası: " + e.Message);
        }
    }

    void Nap(int ms) => _poke.WaitOne(Math.Max(0, ms));

    bool TokenExpiring()
    {
        var m = _manifest;
        return m != null && NowMs() > m.Media.ExpiresAt - 60_000;
    }

    static long NowMs() => Clock.Now();

    (Manifest.Track, int)? Next(Manifest m)
    {
        var hour = IstanbulNow().Hour;
        foreach (var g in m.GenreOrder(hour))
            foreach (var t in m.Tracks)
            {
                if (t.Genre != g) continue;
                lock (_gate)
                    if (_skipTracks.Contains(t.Key)) continue;
                var c = Container(t.Album, false);
                for (var i = 0; i < t.Segments.Count; i++)
                    if (c == null || !c.Contains(t.Id, i)) return (t, i);
            }
        return null;
    }

    bool IsComplete(Manifest.Track t)
    {
        var c = Container(t.Album, false);
        if (c == null || t.Segments.Count == 0) return false;
        for (var i = 0; i < t.Segments.Count; i++)
            if (!c.Contains(t.Id, i)) return false;
        return true;
    }

    /// <summary>Albümün kabı. create=false iken diskte yoksa null.</summary>
    AlbumContainer? Container(string albumId, bool create)
    {
        if (_keyId == null || !IsSafeName(albumId)) return null;
        lock (_gate)
        {
            if (_containers.TryGetValue(albumId, out var c)) return c;
            var path = Path.Combine(ArchiveRoot, _keyId, albumId + ".kmp");
            if (!create && !File.Exists(path)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            c = AlbumContainer.Open(path);
            _containers[albumId] = c;
            return c;
        }
    }

    static bool IsSafeName(string s) => s.Length is > 0 and < 80 && s.All(ch => char.IsLetterOrDigit(ch) || ch == '-');

    // ---------- indirme ----------

    sealed class MediaDenied : Exception { }
    sealed class MediaMissing : Exception { }

    static string EncodeKey(string key) => string.Join("/", key.Split('/').Select(Uri.EscapeDataString));

    byte[] Download(Manifest m, Manifest.Track t, int i)
    {
        var url = $"{m.Media.Base}/m/{EncodeKey(t.Key)}/{i:0000}.ts?t={Uri.EscapeDataString(m.Media.Token)}";
        using var res = ServerApi.Media.GetAsync(url, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
        var status = (int)res.StatusCode;
        if (status is 401 or 403) throw new MediaDenied();
        if (status == 404) throw new MediaMissing();
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"medya sunucusu {status}");
        var bytes = res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        // Yarım ya da bozuk parça kaydedilmez: MPEG-TS paketleri 0x47 ile başlar.
        if (bytes.Length < 188 || bytes[0] != 0x47) throw new InvalidDataException("bozuk parça");
        return ArchiveCrypto.Crypt(_key!, t.SegmentKey(i), bytes);
    }

    static string Explain(Exception e)
    {
        var inner = e is HttpRequestException { InnerException: { } x } ? x : e;
        var m = inner.Message;
        if (e is System.Threading.Tasks.TaskCanceledException)
            return $"İnternet yavaş ya da bağlantı koptu ({m}).";
        if (inner is System.Net.WebException we)
            return we.Status switch
            {
                System.Net.WebExceptionStatus.NameResolutionFailure => $"Sunucu adı çözülemedi ({m}). İnternet ya da DNS ayarını kontrol edin.",
                System.Net.WebExceptionStatus.TrustFailure or System.Net.WebExceptionStatus.SecureChannelFailure =>
                    $"Güvenli bağlantı kurulamadı ({m}). Bilgisayarın tarih/saatini kontrol edin.",
                System.Net.WebExceptionStatus.ProxyNameResolutionFailure => $"Proxy hatası ({m}).",
                _ => $"İnternet yok ya da bağlantı koptu ({m}).",
            };
        return "İndirme hatası: " + m;
    }

    // ---------- paket (saatte bir) ----------

    void FetchManifest()
    {
        _lastManifestMs = _clock.ElapsedMilliseconds;
        var token = _token();
        if (token == null) return;
        ServerApi.Result r;
        try
        {
            var body = new { local = _device.Albums, lastUpdateAt = _device.LastUpdateAt, updates = _device.Updates };
            r = ServerApi.SendAsync(HttpMethod.Post, "/api/offline/manifest", token, body).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Warn("Paket bilgisi alınamadı: " + (e.InnerException?.Message ?? e.Message));
            return; // internet yok: kayıtlı paketle devam
        }
        lock (_gate) _checkedAt = DateTime.Now;
        if (r.Status == 401)
        {
            Log.Warn("Paket: cihaz anahtarı geçersiz (401) " + r.Error);
            _stop = true;
            _onRemoved(r.Error);
            return;
        }
        if (r.Status != 200)
        {
            Log.Warn($"Paket bilgisi hatası: {r.Status} {r.Error}");
            SetState(_state, r.Error ?? "Paket bilgisi alınamadı; tekrar denenecek.");
            return;
        }
        var m = r.Body.ToObject<Manifest>()!;
        Clock.ServerSeen(m.ServerNow);

        m.Media.Archive = null; // sunucunun eski ortak anahtarı kullanılmıyor (karar 24 Eki 2026); paket.json'a yazılmaz

        if (m.Device != null)
        {
            _device.Albums = m.Device.Albums;
            _device.LastUpdateAt = m.Device.LastUpdateAt;
            WriteJson(DeviceFile, _device);
        }
        var changed = m.Version != _manifest?.Version;
        _manifest = m;
        lock (_gate) _skipTracks.Clear();
        WriteJson(ManifestFile, m);
        Recount();
        if (changed)
        {
            Log.Info($"Yeni paket: {m.Version}, {m.Albums.Count} albüm, {m.Tracks.Count} şarkı");
            _coversFor = false;
            _coversTriedMs = -600_001;
            Changed?.Invoke();
        }
    }

    void UseKey(string id, byte[] key, bool save)
    {
        if (!IsSafeName(id) || key.Length != 32) return;
        if (_keyId == id && _key != null && _key.SequenceEqual(key)) return;
        if (_keyId != null && _keyId != id) Log.Warn($"Arşiv anahtarı değişti ({_keyId} → {id}); arşiv yeniden inecek");
        lock (_gate)
        {
            foreach (var c in _containers.Values) c.Dispose();
            _containers.Clear();
        }
        _key = key;
        _keyId = id;
        if (save)
        {
            var json = JsonConvert.SerializeObject(new StoredKey { Id = id, Key = Convert.ToBase64String(key) });
            File.WriteAllBytes(KeyFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(json), KeyEntropy, DataProtectionScope.CurrentUser));
        }
    }

    void LoadLocal()
    {
        try
        {
            if (File.Exists(DeviceFile)) _device = JsonConvert.DeserializeObject<DeviceState>(File.ReadAllText(DeviceFile)) ?? new DeviceState();
        }
        catch (Exception e) { Log.Warn("cihaz-paket.json okunamadı: " + e.Message); }
        try
        {
            if (File.Exists(KeyFile))
            {
                var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(KeyFile), KeyEntropy, DataProtectionScope.CurrentUser));
                var k = JsonConvert.DeserializeObject<StoredKey>(json);
                if (k?.Id == LocalKeyId) UseKey(k.Id, Convert.FromBase64String(k.Key), save: false);
            }
        }
        catch (Exception e) { Log.Warn($"Arşiv anahtarı açılamadı ({e.GetType().Name})"); }
        if (_key == null)
        {
            var key = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
            UseKey(LocalKeyId, key, save: true);
            Log.Info("Bu bilgisayara özel arşiv anahtarı üretildi; arşiv (varsa eskisi silinerek) baştan iner");
        }
        try
        {
            if (File.Exists(ManifestFile)) _manifest = JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(ManifestFile, Encoding.UTF8));
        }
        catch (Exception e) { Log.Warn("paket.json okunamadı: " + e.Message); }
        Recount();
        if (_manifest != null)
            Log.Info($"Kayıtlı paket: {_manifest.Version}, {_manifest.Tracks.Count} şarkı, %{Percent()} inmiş");
    }

    static void WriteJson(string file, object data)
    {
        try
        {
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(data), new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(tmp, file, null);
            else File.Move(tmp, file);
        }
        catch (Exception e) { Log.Warn($"{Path.GetFileName(file)} yazılamadı: {e.Message}"); }
    }

    // ---------- durum ----------

    void Recount()
    {
        var m = _manifest;
        int total = 0, done = 0;
        if (m != null)
            foreach (var t in m.Tracks)
            {
                var c = Container(t.Album, false);
                for (var i = 0; i < t.Segments.Count; i++)
                {
                    total++;
                    if (c != null && c.Contains(t.Id, i)) done++;
                }
            }
        lock (_gate)
        {
            _total = total;
            _done = done;
        }
    }

    int Percent()
    {
        lock (_gate) return _total == 0 ? 0 : (int)(100L * _done / _total);
    }

    void SetState(string state, string? message)
    {
        lock (_gate)
        {
            if (_state != state) Log.Info($"Arşiv: {state}{(message != null ? " · " + message : "")}");
            _state = state;
            _message = message;
        }
    }

    // ---------- temizlik ve kapaklar ----------

    /// <summary>Pakette olmayan albümlerin kapları ve eski anahtarın klasörü, dosya dosya silinir.</summary>
    void Cleanup(Manifest m)
    {
        try
        {
            if (!Directory.Exists(ArchiveRoot)) return;
            var keep = new HashSet<string>(m.Albums.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var dir in Directory.GetDirectories(ArchiveRoot))
            {
                var current = string.Equals(Path.GetFileName(dir), _keyId, StringComparison.OrdinalIgnoreCase);
                foreach (var f in Directory.GetFiles(dir, "*.kmp"))
                {
                    if (_stop) return;
                    var album = Path.GetFileNameWithoutExtension(f);
                    if (current && keep.Contains(album)) continue;
                    lock (_gate)
                    {
                        if (current && _containers.TryGetValue(album, out var c))
                        {
                            c.Dispose();
                            _containers.Remove(album);
                        }
                    }
                    File.Delete(f);
                    Log.Info($"Arşivden silindi: {Path.GetFileName(dir)}/{album}");
                    Nap(500);
                }
                if (!current && Directory.GetFiles(dir).Length == 0) Directory.Delete(dir);
            }
        }
        catch (Exception e) { Log.Warn("Arşiv temizliği: " + e.Message); }
    }

    /// <summary>Başka anahtarla yazılmış eski arşiv klasörleri hiçbir işe yaramaz: açılışta dosya dosya silinir.</summary>
    void RemoveOtherKeyFolders()
    {
        try
        {
            if (!Directory.Exists(ArchiveRoot)) return;
            foreach (var dir in Directory.GetDirectories(ArchiveRoot))
            {
                if (string.Equals(Path.GetFileName(dir), _keyId, StringComparison.OrdinalIgnoreCase)) continue;
                var files = Directory.GetFiles(dir);
                foreach (var f in files)
                {
                    if (_stop) return;
                    File.Delete(f);
                    Nap(200);
                }
                Directory.Delete(dir);
                Log.Info($"Eski anahtarlı arşiv silindi: {Path.GetFileName(dir)} ({files.Length} dosya)");
            }
        }
        catch (Exception e) { Log.Warn("Eski arşiv silinemedi: " + e.Message); }
    }

    /// <summary>Albüm kapakları (küçük, şifresiz dosyalar). Hepsi tamamsa true.</summary>
    bool SyncCovers(Manifest m)
    {
        var ok = true;
        try
        {
            Directory.CreateDirectory(CoverDir);
            foreach (var a in m.Albums)
            {
                if (_stop || a.CoverKey == null || !IsSafeName(a.Id) || CoverFile(a.Id) != null) continue;
                var ext = Path.GetExtension(a.CoverKey).ToLowerInvariant() is ".png" or ".webp" ? Path.GetExtension(a.CoverKey).ToLowerInvariant() : ".jpg";
                var file = Path.Combine(CoverDir, a.Id + ext);
                try
                {
                    var url = $"{m.Media.Base}/m/{EncodeKey(a.CoverKey)}?t={Uri.EscapeDataString(m.Media.Token)}";
                    var data = ServerApi.Media.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    File.WriteAllBytes(file + ".part", data);
                    File.Move(file + ".part", file);
                }
                catch (Exception e)
                {
                    ok = false;
                    Log.Warn($"Kapak inmedi ({a.Title}): {e.InnerException?.Message ?? e.Message}");
                }
            }
            var wanted = new HashSet<string>(m.Albums.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.GetFiles(CoverDir))
            {
                var id = Path.GetFileName(f).Split('.')[0];
                if (!wanted.Contains(id) || f.EndsWith(".part")) File.Delete(f);
            }
        }
        catch (Exception e)
        {
            ok = false;
            Log.Warn("Kapaklar: " + e.Message);
        }
        return ok;
    }

}
