using System;
using System.Collections.Generic;
using System.Linq;

namespace Kmp.Core;

/// <summary>
/// Anons çalma kuralları — web projesindeki src/lib/announce.ts'in birebir çevirisi (web BRIEF §8.2). Değişirse ikisi
/// birlikte değişir; testler (AnnounceTests) web'in announce.test.ts örnekleriyle aynı.
///  - Tetik: gün içi dakika % aralık == 0 (İstanbul saati, sunucu saatinden).
///  - Karışık plan: torba usulü — her anons bir kez çalmadan tekrar yok; tur sınırında da arka arkaya aynısı yok.
///  - Sıralı plan: floor(gün içi dakika / aralık) % anons sayısı.
///  - Saatli plan (X, Y): her gün atamanın kendi saat(ler)inde; o dakikaya düşen bütün anonsları çalar.
///  - Aynı dakikada tetiklenenler tek kuşak: önce saatli (X, Y), sonra tarihli (kampanya), sonra plan sırası.
/// </summary>
public static class Announce
{
    public static readonly string[] Plans = { "PX", "PY", "P1", "P2", "P3", "P4", "P5", "P6", "PA", "PB", "PC" };

    public sealed class Info
    {
        public string Code = "";
        public int Interval;
        public string Mode = ""; // mixed | seq | timed
    }

    public static readonly Dictionary<string, Info> PlanInfo = new()
    {
        ["PX"] = new Info { Code = "X", Interval = 0, Mode = "timed" },
        ["PY"] = new Info { Code = "Y", Interval = 0, Mode = "timed" },
        ["P1"] = new Info { Code = "1", Interval = 2, Mode = "mixed" },
        ["P2"] = new Info { Code = "2", Interval = 5, Mode = "mixed" },
        ["P3"] = new Info { Code = "3", Interval = 8, Mode = "mixed" },
        ["P4"] = new Info { Code = "4", Interval = 10, Mode = "mixed" },
        ["P5"] = new Info { Code = "5", Interval = 15, Mode = "mixed" },
        ["P6"] = new Info { Code = "6", Interval = 30, Mode = "mixed" },
        ["PA"] = new Info { Code = "A", Interval = 5, Mode = "seq" },
        ["PB"] = new Info { Code = "B", Interval = 10, Mode = "seq" },
        ["PC"] = new Info { Code = "C", Interval = 30, Mode = "seq" },
    };

    public static bool IsKnown(string plan) => PlanInfo.ContainsKey(plan);
    public static bool IsTimed(string plan) => PlanInfo.TryGetValue(plan, out var i) && i.Mode == "timed";

    public static string PlanLabel(string plan)
    {
        var i = PlanInfo[plan];
        if (i.Mode == "timed") return $"Plan {i.Code} · her gün saatinde";
        return $"Plan {i.Code} · {i.Interval} dk · {(i.Mode == "mixed" ? "karışık" : "sıralı")}";
    }

    /// <summary>Plandaki bir anons ataması (sunucunun /api/offline/announcements biçimi).</summary>
    public sealed class Item
    {
        public string Plan { get; set; } = "";
        public string AssignmentId { get; set; } = "";
        public string AnnouncementId { get; set; } = "";
        public string Title { get; set; } = "";
        public bool Dated { get; set; }
        public long StartsAt { get; set; }
        public long? EndsAt { get; set; }
        /// <summary>Saatli planda gün içi dakikalar (09:00 → 540).</summary>
        public List<int>? Times { get; set; }
        public string Url { get; set; } = "";
    }

    public static bool IsActive(Item it, long nowMs) => it.StartsAt <= nowMs && (it.EndsAt == null || nowMs < it.EndsAt);

    public static string MinuteLabel(int m) => $"{m / 60:00}:{m % 60:00}";
    public static string FormatTimes(IEnumerable<int>? times) => string.Join(", ", (times ?? Enumerable.Empty<int>()).Select(MinuteLabel));

    /// <summary>Plan bu dakikada çalar mı? Saatli planda o dakikaya saat girilmiş aktif bir atama gerekir.</summary>
    public static bool Triggers(string plan, int dayMinute, IEnumerable<Item>? items = null) =>
        IsTimed(plan)
            ? (items ?? Enumerable.Empty<Item>()).Any(it => it.Times?.Contains(dayMinute) == true)
            : dayMinute % PlanInfo[plan].Interval == 0;

    public static int SequentialIndex(string plan, int dayMinute, int count) => dayMinute / PlanInfo[plan].Interval % count;

    /// <summary>Karışık plan torbası (durum dışarıda saklanır: anons-torba.json).</summary>
    public sealed class Bag
    {
        public List<string> Remaining { get; set; } = new();
        public string? Last { get; set; }
    }

    public static (string Id, Bag State) PickFromBag(IList<string> ids, Bag? state, Func<double> random)
    {
        var valid = new HashSet<string>(ids);
        var remaining = (state?.Remaining ?? new List<string>()).Where(valid.Contains).ToList();
        var last = state?.Last;
        if (remaining.Count == 0)
        {
            // Yeni tur: hepsi torbaya; turun ilki bir önceki turun sonuncusu olmasın.
            remaining = ids.ToList();
            if (last != null && remaining.Count > 1) remaining = remaining.Where(x => x != last).ToList();
            var first = remaining[(int)Math.Floor(random() * remaining.Count)];
            return (first, new Bag { Remaining = ids.Where(x => x != first).ToList(), Last = first });
        }
        var id = remaining[(int)Math.Floor(random() * remaining.Count)];
        return (id, new Bag { Remaining = remaining.Where(x => x != id).ToList(), Last = id });
    }

    public sealed class BreakItem
    {
        public string Plan = "";
        public Item Item = new();
    }

    static int Rank(BreakItem b) => IsTimed(b.Plan) ? 0 : b.Item.Dated ? 1 : 2;

    /// <summary>
    /// Bu dakikada çalacak anons kuşağı. byPlan: her plandaki atamalar; bags: karışık planların torbaları (güncellenmiş
    /// hâli döner). Aralıklı plan tetiklenince o plandan tek anons; saatli planda o dakikadaki hepsi.
    /// </summary>
    public static (List<BreakItem> Items, Dictionary<string, Bag> Bags) ComposeBreak(
        IDictionary<string, List<Item>> byPlan, long nowMs, int dayMinute, IDictionary<string, Bag> bags, Func<double>? random = null)
    {
        random ??= DefaultRandom;
        var nextBags = new Dictionary<string, Bag>(bags);
        var items = new List<BreakItem>();
        foreach (var plan in Plans)
        {
            if (!byPlan.TryGetValue(plan, out var all)) continue;
            var active = all.Where(it => IsActive(it, nowMs))
                .OrderBy(a => a.StartsAt).ThenBy(a => a.AssignmentId, StringComparer.Ordinal).ToList();
            if (active.Count == 0 || !Triggers(plan, dayMinute, active)) continue;
            var mode = PlanInfo[plan].Mode;
            if (mode == "timed")
            {
                foreach (var it in active)
                    if (it.Times?.Contains(dayMinute) == true) items.Add(new BreakItem { Plan = plan, Item = it });
            }
            else if (mode == "seq")
            {
                items.Add(new BreakItem { Plan = plan, Item = active[SequentialIndex(plan, dayMinute, active.Count)] });
            }
            else
            {
                nextBags.TryGetValue(plan, out var bag);
                var r = PickFromBag(active.Select(a => a.AssignmentId).ToList(), bag, random);
                nextBags[plan] = r.State;
                items.Add(new BreakItem { Plan = plan, Item = active.First(a => a.AssignmentId == r.Id) });
            }
        }
        // OrderBy kararlıdır: aynı sıradakilerde plan sırası korunur.
        return (items.OrderBy(Rank).ToList(), nextBags);
    }

    /// <summary>fromMinute'tan sonraki ilk tetik dakikası (gün içi, 0–1439); aktif plan yoksa null.</summary>
    public static int? NextTriggerMinute(IList<string> plans, int fromMinute, IList<Item>? items = null)
    {
        if (plans.Count == 0) return null;
        items ??= new List<Item>();
        for (var m = fromMinute + 1; m < fromMinute + 1 + 1440; m++)
        {
            var dm = m % 1440;
            if (plans.Any(p => Triggers(p, dm, items.Where(it => it.Plan == p)))) return dm;
        }
        return null;
    }

    static readonly Random Rng = new();
    static double DefaultRandom()
    {
        lock (Rng) return Rng.NextDouble();
    }
}
