using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kmp.Core;
using Newtonsoft.Json.Linq;

namespace KurumsalMuzikPlayer.Engine;

/// <summary>
/// Şube girişi ve lisans (BRIEF §2.2, §6): /register ile anahtar alınır, /status ile düzenli sorulur.
/// 401 → cihaz panelden silinmiş ya da aynı şube adıyla başka bilgisayardan girilmiş: girişe dönülür.
/// 403 → hizmet süresi dolmuş: müzik durur.
/// </summary>
sealed class Session
{
    // Lisans günde bir sorulur (karar 9 Eki 2026); başarısızsa 15 dk'da bir yeniden denenir. Düşme (401) zaten 15 dk'da
    // bir gelen anons sorgusuyla en geç 15 dk'da fark edilir.
    // KMP_DURUM_SN: sadece deneme için (düşmeyi beklemeden görmek).
    static readonly int CheckEveryMs =
        int.TryParse(Environment.GetEnvironmentVariable("KMP_DURUM_SN"), out var sec) && sec > 0 ? sec * 1000 : 15 * 60_000;
    const string Unreachable = "Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.";

    public enum Screen { Login, Player, Expired }

    readonly object _gate = new();
    readonly SemaphoreSlim _checking = new(1, 1);
    DeviceInfo? _device;
    string? _notice;
    bool _expired;
    bool _online;
    DateTime? _checkedAt;
    DateTime? _lastOk;
    Timer? _timer;

    /// <summary>Ekran (giriş / çalar / süre doldu) değişti.</summary>
    public event Action<Screen>? ScreenChanged;

    public Screen Current
    {
        get
        {
            lock (_gate) return _device == null ? Screen.Login : _expired ? Screen.Expired : Screen.Player;
        }
    }

    /// <summary>Cihaz anahtarı (giriş yoksa null).</summary>
    public string? Token
    {
        get { lock (_gate) return _device?.Token; }
    }

    /// <summary>Başka bir sorgu (paket, anons) cihazın tanınmadığını (401) bildirdi.</summary>
    public void ReportRemoved(string? message)
    {
        lock (_gate)
        {
            if (_device == null) return;
            _device = null;
            _expired = false;
            _notice = message ?? "Bu cihaz panelden kaldırıldı. Yeniden giriş yapın.";
            DeviceStore.Clear();
            Clock.Reset();
        }
        Log.Warn("Sunucu cihazı tanımadı (401): " + message);
        ScreenChanged?.Invoke(Current);
    }

    /// <summary>Hesaptan çıkış (1.0.1): şube kaydı bu bilgisayardan silinir, giriş ekranına dönülür. Sunucudaki cihaz
    /// kaydı durur; aynı şube adıyla yeniden girilirse aynı cihaz devralınır (cihaz hakkı harcanmaz). Arşiv silinmez.</summary>
    public void Logout()
    {
        DeviceInfo? d;
        lock (_gate)
        {
            d = _device;
            if (d == null) return;
            _device = null;
            _expired = false;
            _notice = "Çıkış yapıldı. Yayının yeniden başlaması için giriş yapın.";
            DeviceStore.Clear();
            Clock.Reset();
        }
        Log.Info($"Hesaptan çıkış: {d.AccountName} · {d.BranchName} ({d.Code})");
        ScreenChanged?.Invoke(Current);
    }

    public void Start()
    {
        var d = DeviceStore.Load();
        lock (_gate) _device = d;
        Log.Info(d == null ? "Kayıtlı şube yok; giriş bekleniyor" : $"Şube: {d.AccountName} · {d.BranchName} ({d.Code})");
        _timer = new Timer(_ =>
        {
            bool due;
            lock (_gate) due = !_online || _lastOk == null || DateTime.Now - _lastOk.Value > TimeSpan.FromHours(24);
            if (due) _ = CheckAsync();
        }, null, 0, CheckEveryMs);
    }

    public async Task CheckAsync()
    {
        DeviceInfo? d;
        lock (_gate) d = _device;
        if (d == null || !await _checking.WaitAsync(0).ConfigureAwait(false)) return;
        var before = Current;
        try
        {
            var r = await ServerApi.SendAsync(HttpMethod.Get, "/api/offline/status", d.Token).ConfigureAwait(false);
            lock (_gate)
            {
                _checkedAt = DateTime.Now;
                if (r.Status == 401)
                {
                    _device = null;
                    _expired = false;
                    _notice = r.Error ?? "Bu cihaz panelden kaldırıldı. Yeniden giriş yapın.";
                    DeviceStore.Clear();
                    Clock.Reset();
                    Log.Warn("Sunucu cihazı tanımadı (401): " + _notice);
                }
                else if (r.Status is 200 or 403)
                {
                    _online = true;
                    _expired = r.Status == 403 && (bool?)r.Body["expired"] == true;
                    d.ServiceEndsAt = (long?)r.Body["account"]?["serviceEndsAt"] ?? d.ServiceEndsAt;
                    d.LastOkAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _lastOk = DateTime.Now;
                    Clock.ServerSeen((long?)r.Body["serverNow"], d.ServiceEndsAt);
                    if (_expired) _notice = r.Error;
                    DeviceStore.Save(d);
                }
                else
                {
                    _online = false;
                    Log.Warn($"Durum sorgusu: sunucu {r.Status} döndü");
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            lock (_gate) _online = false;
            Log.Warn("Durum sorgusu: sunucuya ulaşılamadı (" + (e.InnerException?.Message ?? e.Message) + ")");
        }
        catch (Exception e)
        {
            Log.Error("Durum sorgusu", e);
        }
        finally
        {
            _checking.Release();
        }
        var after = Current;
        if (after != before) ScreenChanged?.Invoke(after);
    }

    public async Task<JObject> LoginAsync(string username, string password, string branch)
    {
        try
        {
            var r = await ServerApi.SendAsync(HttpMethod.Post, "/api/offline/register", body: new { username, password, branch })
                .ConfigureAwait(false);
            if (r.Status != 200)
            {
                Log.Warn($"Giriş reddedildi ({r.Status}): {r.Error}");
                return new JObject
                {
                    ["ok"] = false,
                    ["error"] = r.Error ?? "Giriş yapılamadı.",
                    ["wall"] = (bool?)r.Body["wall"] == true,
                };
            }
            var d = new DeviceInfo
            {
                Token = (string?)r.Body["token"] ?? "",
                AccountName = (string?)r.Body["account"]?["name"] ?? "",
                ServiceEndsAt = (long?)r.Body["account"]?["serviceEndsAt"] ?? 0,
                BranchName = (string?)r.Body["device"]?["branchName"] ?? branch,
                Code = (string?)r.Body["device"]?["code"] ?? "",
                LastOkAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            DeviceStore.Save(d);
            Clock.ServerSeen(null, d.ServiceEndsAt);
            var takenOver = (bool?)r.Body["takenOver"] == true;
            lock (_gate)
            {
                _device = d;
                _expired = false;
                _online = true;
                _checkedAt = DateTime.Now;
                _lastOk = DateTime.Now;
                _notice = takenOver ? "Bu şube adıyla açık olan diğer bilgisayardaki yayın durdu; yayın artık bu bilgisayarda." : null;
            }
            Log.Info($"Giriş yapıldı: {d.AccountName} · {d.BranchName} ({d.Code}){(takenOver ? ", şube devralındı" : "")}");
            ScreenChanged?.Invoke(Current);
            return new JObject { ["ok"] = true };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Log.Warn("Giriş: sunucuya ulaşılamadı (" + (e.InnerException?.Message ?? e.Message) + ")");
            return new JObject { ["ok"] = false, ["error"] = Unreachable };
        }
        catch (Exception e)
        {
            Log.Error("Giriş", e);
            return new JObject { ["ok"] = false, ["error"] = "Giriş yapılamadı: " + e.Message };
        }
    }

    /// <summary>Pencereye giden oturum bilgisi.</summary>
    public JObject Describe()
    {
        lock (_gate)
        {
            return new JObject
            {
                ["screen"] = Current.ToString().ToLowerInvariant(),
                ["notice"] = _notice,
                ["account"] = _device?.AccountName,
                ["branch"] = _device?.BranchName,
                ["code"] = _device?.Code,
                ["serviceEnds"] = _device is { ServiceEndsAt: > 0 } d ? Clock.Istanbul(d.ServiceEndsAt - 1).ToString("dd.MM.yyyy") : null,
                ["online"] = _online,
                ["checkedAt"] = _checkedAt?.ToString("HH:mm"),
                ["server"] = ServerApi.BaseUrl,
            };
        }
    }
}
