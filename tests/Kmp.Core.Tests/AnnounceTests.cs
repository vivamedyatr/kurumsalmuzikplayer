using System.Collections.Generic;
using System.Linq;
using Kmp.Core;
using Xunit;

namespace Kmp.Core.Tests;

// Web projesindeki src/lib/announce.test.ts ile aynı örnekler: iki taraf aynı kuralla çalmalı.
public sealed class AnnounceTests
{
    static Announce.Item Item(string id, bool dated = false, long startsAt = 0, long? endsAt = null, string plan = "") =>
        new() { AssignmentId = id, AnnouncementId = "ann-" + id, Title = id, Dated = dated, StartsAt = startsAt, EndsAt = endsAt, Plan = plan };

    static Announce.Item Timed(string id, int[] times, bool dated = false, string plan = "PX")
    {
        var it = Item(id, dated, plan: plan);
        it.Times = times.ToList();
        return it;
    }

    static Dictionary<string, List<Announce.Item>> ByPlan(params (string Plan, Announce.Item[] Items)[] p) =>
        p.ToDictionary(x => x.Plan, x => x.Items.ToList());

    static List<string> Ids(List<Announce.BreakItem> items) => items.Select(x => x.Item.AssignmentId).ToList();

    [Fact]
    public void TriggerIsDayMinuteModInterval()
    {
        Assert.True(Announce.Triggers("P2", 10 * 60 + 5));
        Assert.False(Announce.Triggers("P2", 10 * 60 + 6));
    }

    [Fact]
    public void EightMinutePlanKeepsIntervalAcrossHour()
    {
        var fires = Enumerable.Range(10 * 60 + 50, 21).Where(m => Announce.Triggers("P3", m)).ToList();
        for (var i = 1; i < fires.Count; i++) Assert.Equal(8, fires[i] - fires[i - 1]);
    }

    [Fact]
    public void SequentialIsStateless()
    {
        Assert.Equal(new[] { 0, 1, 2, 0 }, new[] { 600, 605, 610, 615 }.Select(m => Announce.SequentialIndex("PA", m, 3)));
    }

    [Fact]
    public void BagPlaysEachOnceAndNeverRepeatsBackToBack()
    {
        var ids = new[] { "a", "b", "c", "d" };
        Announce.Bag? state = null;
        var played = new List<string>();
        long seed = 7;
        double Rnd() => (seed = (seed * 9301 + 49297) % 233280) / 233280.0;
        for (var i = 0; i < 40; i++)
        {
            var r = Announce.PickFromBag(ids, state, Rnd);
            played.Add(r.Id);
            state = r.State;
        }
        for (var t = 0; t < 10; t++) Assert.Equal(4, played.Skip(t * 4).Take(4).Distinct().Count());
        for (var i = 1; i < played.Count; i++) Assert.NotEqual(played[i - 1], played[i]);
    }

    [Fact]
    public void RemovedAnnouncementLeavesBag()
    {
        var r = Announce.PickFromBag(new[] { "a", "b" }, new Announce.Bag { Remaining = new() { "x", "y" }, Last = "a" }, () => 0.5);
        Assert.Contains(r.Id, new[] { "a", "b" });
    }

    [Fact]
    public void BreakOrderDatedThenPlanOrder()
    {
        var m = 10 * 60 + 30;
        var r = Announce.ComposeBreak(ByPlan(("P2", new[] { Item("p2") }), ("P6", new[] { Item("p6") }), ("PA", new[] { Item("pa") }),
            ("PC", new[] { Item("kampanya", true) })), 1000, m, new Dictionary<string, Announce.Bag>());
        Assert.Equal(new[] { "kampanya", "p2", "p6", "pa" }, Ids(r.Items));
    }

    [Fact]
    public void InactiveAnnouncementsDoNotPlay()
    {
        var r = Announce.ComposeBreak(ByPlan(("P2", new[] { Item("gelecek", true, 5000) }), ("P6", new[] { Item("bitmis", true, 0, 500) })),
            1000, 10 * 60 + 30, new Dictionary<string, Announce.Bag>());
        Assert.Empty(r.Items);
        Assert.Empty(Announce.ComposeBreak(ByPlan(("P6", new[] { Item("x") })), 1000, 10 * 60 + 7, new Dictionary<string, Announce.Bag>()).Items);
    }

    [Fact]
    public void NextTriggerMinute()
    {
        Assert.Equal(10 * 60 + 30, Announce.NextTriggerMinute(new[] { "P6" }, 10 * 60 + 7));
        Assert.Equal(10 * 60 + 10, Announce.NextTriggerMinute(new[] { "P2", "P6" }, 10 * 60 + 7));
        Assert.Equal(0, Announce.NextTriggerMinute(new[] { "PC" }, 23 * 60 + 45));
        Assert.Null(Announce.NextTriggerMinute(new string[0], 600));
    }

    [Fact]
    public void TimedPlansPlayAtTheirMinutes()
    {
        var byPlan = ByPlan(("PX", new[] { Timed("acilis", new[] { 540 }), Timed("hosgeldin", new[] { 540, 720 }), Timed("kapanis", new[] { 1315 }) }));
        Assert.True(Announce.Triggers("PX", 540, byPlan["PX"]));
        Assert.False(Announce.Triggers("PX", 541, byPlan["PX"]));
        var bags = new Dictionary<string, Announce.Bag>();
        Assert.Equal(new[] { "acilis", "hosgeldin" }, Ids(Announce.ComposeBreak(byPlan, 1000, 540, bags).Items));
        Assert.Equal(new[] { "hosgeldin" }, Ids(Announce.ComposeBreak(byPlan, 1000, 720, bags).Items));
        Assert.Empty(Announce.ComposeBreak(byPlan, 1000, 600, bags).Items);
        Assert.Equal("09:00, 21:55", Announce.FormatTimes(new[] { 540, 1315 }));
    }

    [Fact]
    public void TimedFirstThenDatedThenOthers()
    {
        var m = 10 * 60 + 30;
        var r = Announce.ComposeBreak(ByPlan(("P2", new[] { Item("p2") }), ("PC", new[] { Item("kampanya", true) }),
            ("PY", new[] { Timed("y", new[] { m }, plan: "PY") }), ("PX", new[] { Timed("x", new[] { m }) })), 1000, m, new Dictionary<string, Announce.Bag>());
        Assert.Equal(new[] { "x", "y", "kampanya", "p2" }, Ids(r.Items));
        Assert.Equal("Plan X · her gün saatinde", Announce.PlanLabel("PX"));
    }

    [Fact]
    public void TimedOutsideDatesDoesNotPlay()
    {
        var y = Timed("y", new[] { 540 }, plan: "PY");
        y.StartsAt = 5000;
        Assert.Empty(Announce.ComposeBreak(ByPlan(("PY", new[] { y })), 1000, 540, new Dictionary<string, Announce.Bag>()).Items);
    }

    [Fact]
    public void NextTriggerFindsTimedMinute()
    {
        var items = new List<Announce.Item> { Timed("x", new[] { 540, 1315 }) };
        Assert.Equal(540, Announce.NextTriggerMinute(new[] { "PX" }, 500, items));
        Assert.Equal(1315, Announce.NextTriggerMinute(new[] { "PX" }, 540, items));
        Assert.Equal(540, Announce.NextTriggerMinute(new[] { "PX" }, 1316, items));
        Assert.Equal(510, Announce.NextTriggerMinute(new[] { "PX", "P6" }, 500, items));
    }
}
