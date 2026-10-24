using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Kmp.Core;

/// <summary>
/// Anonslar (BRIEF §2.2): liste 15 dakikada bir alınır, MP3'ler yerele iner (şifresiz, karar). Kuşak kararı her dakika
/// web player'la aynı kurallarla (Announce) verilir; çalar çalar. Çalma kayıtları internetsizken birikir, bağlantı
/// gelince sunucuya gider (sunucu 7 güne kadar geriye kabul eder).
/// </summary>
public sealed class Announcer : IDisposable
{
    const int RefreshMs = 15 * 60_000;

    readonly Func<string?> _token;
    readonly Action<string?> _onRemoved;
    readonly object _gate = new();
    List<Announce.Item> _items;
    Dictionary<string, Announce.Bag> _bags;
    List<Play> _pending;
    Timer? _refreshTimer;
    Timer? _tickTimer;
    Timer? _flushTimer;
    string _lastMinuteKey = "";
    volatile bool _stop;
    int _refreshing;

    /// <summary>Müzik çalıyor ve kuşak alabilir mi (çalar söyler).</summary>
    public volatile bool Ready;
    /// <summary>Kuşak sürüyor (çalar bitince BreakEnded çağırır).</summary>
    public volatile bool InBreak;

    /// <summary>Kuşak zamanı geldi: sırayla çalınacak anonslar (dosya yolu ve başlık).</summary>
    public event Action<List<Announce.Item>>? Break;

    // Sunucu küçük harfli alan adları bekler (zod şeması); büyük harfle 400 dönüyordu.
    sealed class Play
    {
        [Newtonsoft.Json.JsonProperty("assignmentId")] public string AssignmentId { get; set; } = "";
        [Newtonsoft.Json.JsonProperty("playedAt")] public long PlayedAt { get; set; }
    }

    sealed class Stored
    {
        public long ServerNow { get; set; }
        public List<Announce.Item> Items { get; set; } = new();
    }

    static string Dir => Path.Combine(AppPaths.DataDir, "anons");
    static string ListFile => Path.Combine(AppPaths.DataDir, "anonslar.json");
    static string BagsFile => Path.Combine(AppPaths.DataDir, "anons-torba.json");
    static string PlaysFile => Path.Combine(AppPaths.DataDir, "anons-gonderilecek.json");

    public Announcer(Func<string?> token, Action<string?> onRemoved)
    {
        _token = token;
        _onRemoved = onRemoved;
        _items = (JsonFile.Read<Stored>(ListFile)?.Items ?? new List<Announce.Item>()).Where(i => Announce.IsKnown(i.Plan)).ToList();
        _bags = JsonFile.Read<Dictionary<string, Announce.Bag>>(BagsFile) ?? new Dictionary<string, Announce.Bag>();
        _pending = JsonFile.Read<List<Play>>(PlaysFile) ?? new List<Play>();
    }

    public void Start()
    {
        _refreshTimer = new Timer(_ => Refresh(), null, 0, RefreshMs);
        _tickTimer = new Timer(_ => Tick(), null, 1000, 1000);
    }

    public void Dispose()
    {
        _stop = true;
        _refreshTimer?.Dispose();
        _tickTimer?.Dispose();
        _flushTimer?.Dispose();
    }

    /// <summary>Anonsun MP3'ü (inmemişse null).</summary>
    public static string? FileOf(Announce.Item it)
    {
        var f = Path.Combine(Dir, Path.GetFileName(it.AnnouncementId) + ".mp3");
        return File.Exists(f) ? f : null;
    }

    /// <summary>Kuşaktaki bir anons gerçekten çaldı.</summary>
    public void Played(Announce.Item it)
    {
        History.Add("anons", it.Title, Announce.IsKnown(it.Plan) ? Announce.PlanLabel(it.Plan) : "");
        lock (_gate)
        {
            _pending.Add(new Play { AssignmentId = it.AssignmentId, PlayedAt = Clock.Now() });
            JsonFile.Write(PlaysFile, _pending);
            // Birkaç saniye sonra gönder (kuşaktaki diğerleri de birlikte gitsin); internet yoksa sırada kalır.
            _flushTimer ??= new Timer(_ =>
            {
                lock (_gate)
                {
                    _flushTimer?.Dispose();
                    _flushTimer = null;
                }
                FlushPlays();
            }, null, 10_000, Timeout.Infinite);
        }
    }

    public void BreakEnded() => InBreak = false;

    // ---------- liste ----------

    void Refresh()
    {
        var token = _token();
        if (_stop || token == null || Interlocked.Exchange(ref _refreshing, 1) == 1) return;
        try
        {
            var r = ServerApi.SendAsync(HttpMethod.Get, "/api/offline/announcements", token).GetAwaiter().GetResult();
            if (r.Status == 401)
            {
                _onRemoved(r.Error);
                return;
            }
            if (r.Status != 200) return;
            var stored = r.Body.ToObject<Stored>() ?? new Stored();
            Clock.ServerSeen(stored.ServerNow);
            var items = stored.Items.Where(i => Announce.IsKnown(i.Plan)).ToList();
            lock (_gate) _items = items;
            JsonFile.Write(ListFile, new Stored { ServerNow = stored.ServerNow, Items = items });
            SyncFiles(items);
            FlushPlays();
        }
        catch (Exception e)
        {
            // İnternet yok: kayıtlı liste ve inmiş MP3'lerle devam.
            Log.Warn("Anons listesi alınamadı: " + (e.InnerException?.Message ?? e.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    /// <summary>Eksik MP3'leri indirir, listeden çıkanları siler.</summary>
    void SyncFiles(List<Announce.Item> items)
    {
        Directory.CreateDirectory(Dir);
        var wanted = items.GroupBy(i => Path.GetFileName(i.AnnouncementId)).ToDictionary(g => g.Key, g => g.First().Url);
        foreach (var kv in wanted)
        {
            if (_stop) return;
            var file = Path.Combine(Dir, kv.Key + ".mp3");
            if (File.Exists(file)) continue;
            try
            {
                var data = ServerApi.Media.GetByteArrayAsync(kv.Value).GetAwaiter().GetResult();
                File.WriteAllBytes(file + ".part", data);
                File.Move(file + ".part", file);
                Log.Info($"Anons indi: {items.First(i => Path.GetFileName(i.AnnouncementId) == kv.Key).Title}");
            }
            catch (Exception e) { Log.Warn($"Anons inmedi: {e.InnerException?.Message ?? e.Message}"); }
        }
        foreach (var f in Directory.GetFiles(Dir))
        {
            var id = Path.GetFileName(f).Split('.')[0];
            if (!wanted.ContainsKey(id) || f.EndsWith(".part"))
                try { File.Delete(f); } catch { }
        }
    }

    void FlushPlays()
    {
        var token = _token();
        List<Play> batch;
        lock (_gate) batch = _pending.Take(2000).ToList();
        if (token == null || batch.Count == 0) return;
        try
        {
            var r = ServerApi.SendAsync(HttpMethod.Post, "/api/offline/announcements/played", token, new { plays = batch })
                .GetAwaiter().GetResult();
            if (r.Status == 400) Log.Warn("Anons çalma kayıtları reddedildi (400): " + r.Error);
            if (r.Status is 200 or 400)
                lock (_gate)
                {
                    _pending.RemoveRange(0, Math.Min(batch.Count, _pending.Count));
                    JsonFile.Write(PlaysFile, _pending);
                }
        }
        catch { /* internet yok: sırada kalır */ }
    }

    // ---------- kuşak (her saniye bakılır, dakikada bir karar) ----------

    void Tick()
    {
        if (_stop || !Ready || InBreak || !Clock.PlayGate(out _)) return;
        var now = Clock.Now();
        var t = Clock.Istanbul(now);
        var key = t.ToString("yyyy-MM-dd HH:mm");
        if (key == _lastMinuteKey) return;
        _lastMinuteKey = key;
        // Dakikanın ilk yarısında değilsek (program geç açıldı / bilgisayar uyudu) bu dakika atlanır (web ile aynı).
        if (t.Second > 30) return;
        Dictionary<string, List<Announce.Item>> byPlan;
        lock (_gate)
            byPlan = _items.Where(i => FileOf(i) != null).GroupBy(i => i.Plan).ToDictionary(g => g.Key, g => g.ToList());
        var r = Announce.ComposeBreak(byPlan, now, Clock.DayMinute(t), _bags);
        _bags = r.Bags;
        JsonFile.Write(BagsFile, _bags);
        if (r.Items.Count == 0) return;
        InBreak = true;
        Log.Info($"Anons kuşağı: {string.Join(", ", r.Items.Select(b => b.Item.Title))}");
        Break?.Invoke(r.Items.Select(b => b.Item).ToList());
    }

    /// <summary>Penceredeki "anons akışı" (salt okunur): sıradaki kuşağın saati, yayındaki planlar, başlayacaklar.</summary>
    public JObject Schedule()
    {
        var now = Clock.Now();
        var t = Clock.Istanbul(now);
        List<Announce.Item> items;
        lock (_gate) items = _items.ToList();
        var active = items.Where(i => Announce.IsActive(i, now)).ToList();
        var plans = Announce.Plans.Where(p => active.Any(i => i.Plan == p)).ToList();
        var nm = Announce.NextTriggerMinute(plans, Clock.DayMinute(t), active);
        string? next = nm == null ? null : Announce.MinuteLabel(nm.Value);
        var nextPlans = nm == null ? new List<string>() : plans.Where(p => Announce.Triggers(p, nm.Value, active.Where(i => i.Plan == p))).ToList();
        return new JObject
        {
            ["next"] = next,
            ["nextWhat"] = string.Join(" · ", nextPlans.Select(p => $"Plan {Announce.PlanInfo[p].Code}")),
            ["plans"] = new JArray(plans.Select(p => new JObject
            {
                ["label"] = Announce.PlanLabel(p),
                ["items"] = new JArray(active.Where(i => i.Plan == p).OrderBy(i => i.StartsAt).ThenBy(i => i.Title)
                    .Select(i => new JObject
                    {
                        ["title"] = Announce.IsTimed(p) ? $"{i.Title} · {Announce.FormatTimes(i.Times)}" : i.Title,
                        ["ends"] = i.EndsAt is long e ? Clock.Istanbul(e).AddDays(-1).ToString("dd.MM.yyyy") : null,
                        ["ready"] = FileOf(i) != null,
                    })),
            })),
            ["upcoming"] = new JArray(items.Where(i => i.StartsAt > now).OrderBy(i => i.StartsAt)
                .Select(i => new JObject { ["title"] = i.Title, ["plan"] = Announce.PlanLabel(i.Plan), ["starts"] = Clock.Istanbul(i.StartsAt).ToString("dd.MM.yyyy") })),
        };
    }
}
