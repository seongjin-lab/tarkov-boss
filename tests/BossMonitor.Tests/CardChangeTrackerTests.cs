using BossMonitor.Core;
using Xunit;

namespace BossMonitor.Tests;

public class CardChangeTrackerTests
{
    private static readonly DateTimeOffset Now=new(2026,1,1,0,0,0,TimeSpan.Zero);
    private static RaidState Raid()=>new() {Key="raid-a",Map="factory4_day"};

    [Fact]
    public void InitialHistoryAndDifferentRaidsDoNotHighlight()
    {
        var tracker=new CardChangeTracker();
        var raid=Raid();
        raid.Bosses["bossTagilla"]=new() {Status="Confirmed",LifeInstances=[new()]};
        tracker.Observe(raid,Now);
        Assert.Null(tracker.Recent("boss:bossTagilla",Now));
        raid.Bosses["bossTagilla"].LifeInstances.Add(new());
        tracker.Observe(raid,Now.AddSeconds(1));
        Assert.Equal(CardChangeTracker.ChangeKind.Activated,tracker.Recent("boss:bossTagilla",Now.AddSeconds(1))!.Kind);
        raid.Key="raid-b";
        tracker.Observe(raid,Now.AddSeconds(2));
        Assert.Null(tracker.Recent("boss:bossTagilla",Now.AddSeconds(2)));
    }

    [Fact]
    public void NewCardsAndEventDecisionsHighlightWithoutTreatingProbabilityAsActivation()
    {
        var tracker=new CardChangeTracker();var raid=Raid();tracker.Observe(raid,Now);
        raid.InfectedTagilla.SpawnChance=100;
        tracker.Observe(raid,Now.AddSeconds(1));
        Assert.Null(tracker.Recent("event:infectedTagilla",Now.AddSeconds(1)));
        raid.InfectedTagilla.Status="NotPlanned";
        tracker.Observe(raid,Now.AddSeconds(2));
        Assert.Equal(CardChangeTracker.ChangeKind.NotPlanned,tracker.Recent("event:infectedTagilla",Now.AddSeconds(2))!.Kind);
        raid.Bosses["bossTagilla"]=new() {Status="Planned"};
        tracker.Observe(raid,Now.AddSeconds(3));
        Assert.Equal(CardChangeTracker.ChangeKind.Planned,tracker.Recent("boss:bossTagilla",Now.AddSeconds(3))!.Kind);
        raid.InfectedTagilla.Status="Confirmed";raid.InfectedTagilla.LifeInstances.Add(new());
        tracker.Observe(raid,Now.AddSeconds(4));
        Assert.Equal(CardChangeTracker.ChangeKind.Activated,tracker.Recent("event:infectedTagilla",Now.AddSeconds(4))!.Kind);
    }

    [Fact]
    public void UnrelatedUpdatesDoNotRestartHighlightAndExpiryNeedsNoPoll()
    {
        var tracker=new CardChangeTracker();var raid=Raid();tracker.Observe(raid,Now);
        raid.Scavs.LifeInstances.Add(new());tracker.Observe(raid,Now.AddSeconds(1));
        raid.Scavs.Position="different";raid.PlayerKills=1;
        tracker.Observe(raid,Now.AddSeconds(4));
        Assert.Equal(Now.AddSeconds(1),tracker.Recent("scavs",Now.AddSeconds(4))!.At);
        Assert.NotNull(tracker.Recent("scavs",Now.AddSeconds(8)));
        Assert.Null(tracker.Recent("scavs",Now.AddSeconds(9)));
    }

    [Fact]
    public void DeathRequiresEvidenceAndRaidEndClearsHighlights()
    {
        var tracker=new CardChangeTracker();var raid=Raid();
        raid.SpecialAis["exUsec"]=new() {Status="Confirmed",LifeInstances=[new()]};
        tracker.Observe(raid,Now);
        var instance=raid.SpecialAis["exUsec"].LifeInstances[0];
        instance.LifeStatus="DeadConfirmed";tracker.Observe(raid,Now.AddSeconds(1));
        Assert.Null(tracker.Recent("special:exUsec",Now.AddSeconds(1)));
        instance.DiedAt=Now.DateTime;tracker.Observe(raid,Now.AddSeconds(2));
        Assert.Equal(CardChangeTracker.ChangeKind.Death,tracker.Recent("special:exUsec",Now.AddSeconds(2))!.Kind);
        raid.Ended=true;tracker.Observe(raid,Now.AddSeconds(3));
        Assert.Null(tracker.Recent("special:exUsec",Now.AddSeconds(3)));
    }

    [Fact]
    public void BothPmcFactionsHighlightTheSameCard()
    {
        var tracker=new CardChangeTracker();var raid=Raid();
        raid.Pmcs["pmcBEAR"]=new() {Role="pmcBEAR",PlannedWaves=1,LifeInstances=[new()]};
        raid.Pmcs["pmcUSEC"]=new() {Role="pmcUSEC"};
        tracker.Observe(raid,Now);
        raid.Pmcs["pmcUSEC"].PlannedWaves++;
        raid.Pmcs["pmcUSEC"].AdditionalPlannedWaves++;
        tracker.Observe(raid,Now.AddSeconds(1));
        Assert.Equal(CardChangeTracker.ChangeKind.Planned,tracker.Recent("pmc",Now.AddSeconds(1))!.Kind);
        var bear = raid.Pmcs["pmcBEAR"].LifeInstances[0];
        bear.LifeStatus = "DeadConfirmed";
        bear.DiedAt = Now.DateTime;
        tracker.Observe(raid,Now.AddSeconds(2));
        Assert.Equal(CardChangeTracker.ChangeKind.Death,tracker.Recent("pmc",Now.AddSeconds(2))!.Kind);
    }
}
