using BossMonitor.Core;
using Xunit;

namespace BossMonitor.Tests;

public class ReaderRegressionTests
{
    [Theory]
    [InlineData("exUsec", "exUsec")]
    [InlineData("exUsecFree", "exUsec")]
    [InlineData("pmcBot", "pmcBot")]
    public void SpecialAiKeepsItsOwnCountZoneAndCoordinates(string role, string canonicalRole)
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", $"Bot activate id:2 role:{role} profileId:specialA pos:(1, 2, 3)");
        f.Append("09:00:01.001", $"Group set boss. groupName:ZoneFixture [Boss] botBoss.Id:2 bossType:{role}");
        f.Append("09:00:01.002", "Bot activate id:3 role:assault profileId:scavA pos:(4, 5, 6)");
        f.Append("09:00:02.000", "bot dispose:2");
        f.Append("09:00:02.001", "BotDied pos:(7, 8, 9)");
        f.Poll();
        var special = Assert.Single(f.Reader.Current.SpecialAis[canonicalRole].LifeInstances);
        Assert.Equal("ZoneFixture", special.Zone);
        Assert.Equal("1, 2, 3", special.Position);
        Assert.Equal("7, 8, 9", special.DeathPosition);
        Assert.Equal("DeadConfirmed", special.LifeStatus);
        Assert.Null(Assert.Single(f.Reader.Current.Scavs.LifeInstances).DiedAt);
    }

    [Theory]
    [InlineData(999, 0, true)]
    [InlineData(1000, 0, true)]
    [InlineData(1001, 0, false)]
    [InlineData(1, 63, true)]
    [InlineData(1, 64, false)]
    public void DeathRequiresTightTimeAndLinePairing(int delayMs, int interveningRows, bool dead)
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:bossKilla profileId:bossA pos:(1, 2, 3)");
        f.Append("09:00:02.000", "bot dispose:2");
        File.AppendAllText(f.Ai, string.Concat(Enumerable.Repeat("unrelated row\n", interveningRows)));
        f.Append(new DateTime(2000, 1, 1, 9, 0, 2).AddMilliseconds(delayMs).ToString("HH:mm:ss.fff"), "BotDied pos:(7, 8, 9)");
        f.Poll();
        var boss = Assert.Single(f.Reader.Current.Bosses["bossKilla"].LifeInstances);
        Assert.Equal(dead, boss.DiedAt != null);
        Assert.Equal(dead ? "7, 8, 9" : "", boss.DeathPosition);
    }

    [Fact]
    public void DeathInAnotherFileCannotConsumePendingDisposal()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:assault profileId:scavA pos:(1, 2, 3)");
        f.Append("09:00:02.000", "bot dispose:2");
        File.WriteAllText(Path.Combine(f.Session, "fixture aiData_001.log"), LogFixture.Line("09:00:02.001", "aiData|BotDied pos:(7, 8, 9)"));
        f.Poll();
        Assert.Null(Assert.Single(f.Reader.Current.Scavs.LifeInstances).DiedAt);
    }

    [Theory]
    [InlineData("impossible", "pmcBEAR", 1, 0, "impossible")]
    [InlineData("IMPOSSIBLE", "pmcBEAR", 1, 0, "impossible")]
    [InlineData("hard", "pmcBEAR", 1, 0, "hard")]
    [InlineData("normal", "pmcBEAR", 1, 0, "normal")]
    [InlineData("easy", "pmcBEAR", 1, 0, "easy")]
    [InlineData("unsupported", "pmcBEAR", 1, 0, "")]
    [InlineData("impossible", "pmcUSEC", 1, 0, "")]
    [InlineData("impossible", "pmcBEAR", 2000, 0, "impossible")]
    [InlineData("impossible", "pmcBEAR", 2001, 0, "")]
    [InlineData("impossible", "pmcBEAR", 1, 63, "impossible")]
    [InlineData("impossible", "pmcBEAR", 1, 64, "")]
    public void DifficultyUsesProfileDataIdRoleAndBoundedPairing(string difficulty, string spawnRole, int delayMs, int interveningRows, string expected)
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Profile for bot typeA taken dataId:50:  profileId:pmcA");
        File.AppendAllText(f.Ai, string.Concat(Enumerable.Repeat("unrelated row\n", interveningRows)));
        f.Append(new DateTime(2000, 1, 1, 9, 0, 1).AddMilliseconds(delayMs).ToString("HH:mm:ss.fff"), $"Activate bots: Side:Savage Type:{spawnRole}  BotDifficulty:{difficulty}  id:999  spawnPoints:1  id:50");
        f.Append("09:00:05.000", "Bot activate id:2 role:pmcBEAR profileId:pmcA pos:(1, 2, 3)");
        f.Poll();
        Assert.Equal(expected, Assert.Single(f.Reader.Current.Pmcs["pmcBEAR"].LifeInstances).PmcDifficulty);
    }

    [Fact]
    public void UnknownAndConflictingDifficultiesStayUnclassified()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Profile for bot typeA taken dataId:50: profileId:pmcA");
        f.Append("09:00:01.001", "Activate bots: Type:pmcBEAR BotDifficulty:impossible id:999 spawnPoints:1 id:50");
        f.Append("09:00:01.002", "Bot activate id:2 role:pmcBEAR profileId:pmcA pos:(1, 2, 3)");
        f.Append("09:00:01.003", "Bot activate id:3 role:pmcBEAR profileId:pmcUnknown pos:(1, 2, 3)");
        f.Poll();
        Assert.Equal("impossible", f.Reader.Current.Pmcs["pmcBEAR"].LifeInstances[0].PmcDifficulty);
        Assert.Equal("", f.Reader.Current.Pmcs["pmcBEAR"].LifeInstances[1].PmcDifficulty);
        f.Append("09:00:02.000", "Profile for bot typeA taken dataId:51: profileId:pmcA");
        f.Append("09:00:02.001", "Activate bots: Type:pmcBEAR BotDifficulty:normal id:999 spawnPoints:1 id:51");
        f.Poll();
        Assert.All(f.Reader.Current.Pmcs["pmcBEAR"].LifeInstances, i => Assert.Equal("", i.PmcDifficulty));
    }

    [Fact]
    public void PartialUtf8RowsAndMirroredOutputDoNotDuplicateScavs()
    {
        using var f = new LogFixture();
        f.Start();
        byte[] row = System.Text.Encoding.UTF8.GetBytes(LogFixture.Line("09:00:01.000", "aiData|Bot activate id:2 role:assault profileId:scavA name:가 pos:(1, 2, 3)"));
        int split = Array.IndexOf(row, (byte)0xEA) + 1;
        File.WriteAllBytes(f.Ai, row[..split]);
        f.Poll();
        Assert.Empty(f.Reader.Current.Scavs.LifeInstances);
        using (var stream = File.Open(f.Ai, FileMode.Append)) stream.Write(row.AsSpan(split));
        File.WriteAllBytes(f.Output, row);
        f.Poll(); f.Poll();
        Assert.Single(f.Reader.Current.Scavs.LifeInstances);
        f.Append("09:00:02.000", "bot dispose:2");
        File.AppendAllText(f.Ai, "2000-01-01 09:00:02.001|fixture|Trace|aiData|BotDied pos:(7, 8,");
        f.Poll();
        Assert.Null(f.Reader.Current.Scavs.LifeInstances[0].DiedAt);
        File.AppendAllText(f.Ai, " 9)\n");
        f.Poll();
        Assert.Equal("7, 8, 9", f.Reader.Current.Scavs.LifeInstances[0].DeathPosition);
    }

    [Fact]
    public void AssaultGroupDeathsCountInBothScavCardAndPlayerKills()
    {
        using var f = new LogFixture();
        f.Start("bigmap");
        f.Append("09:00:01.000", "AIDATA create Player id:1 isAI:False name:FixturePlayer Standing:0");
        f.Append("09:00:02.000", "Bot activate id:2 role:assault profileId:scavA pos:(1, 2, 3)");
        for (int id = 3; id <= 5; id++)
            f.Append("09:00:03.000", $"Bot activate id:{id} role:assaultGroup profileId:groupScav{id} pos:(4, 5, 6)");

        for (int id = 3; id <= 5; id++)
        {
            string time = $"09:00:{id + 1:D2}";
            f.Append($"{time}.000", $"bot get hit from enemy. botId:{id} from:FixturePlayer");
            f.Append($"{time}.001", $"bot dispose:{id}");
            f.Append($"{time}.002", $"BotDied pos:({id}, 8, 9)");
        }
        f.Poll();

        var raid = f.Reader.Current;
        Assert.Equal(3, raid.PlayerKills);
        Assert.Equal(4, raid.Scavs.Count);
        Assert.Equal("scavA", Assert.Single(raid.Scavs.LifeInstances, i => i.DiedAt == null).Profile);
        Assert.All(raid.Scavs.LifeInstances.Where(i => i.DiedAt != null),
            i => Assert.Equal("DeadConfirmed", i.LifeStatus));
    }

    [Fact]
    public void ReusedBotIdKeepsDeathsOnTheirOwnProfiles()
    {
        using var f = new LogFixture();
        f.Start();
        foreach (var (profile, second, position) in new[] { ("scavA", 1, "1, 2, 3"), ("scavB", 4, "4, 5, 6") })
        {
            f.Append($"09:00:{second:D2}.000", $"Bot activate id:2 role:assault profileId:{profile} pos:(0, 0, 0)");
            f.Append($"09:00:{second + 1:D2}.000", "bot dispose:2");
            f.Append($"09:00:{second + 1:D2}.001", $"BotDied pos:({position})");
        }
        f.Poll();
        Assert.Collection(f.Reader.Current.Scavs.LifeInstances,
            i => { Assert.Equal("scavA", i.Profile); Assert.Equal("1, 2, 3", i.DeathPosition); },
            i => { Assert.Equal("scavB", i.Profile); Assert.Equal("4, 5, 6", i.DeathPosition); });
    }

    [Fact]
    public void IdenticalPmcWavePlansAreSeparateCanonicalRows()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Init Boss wave:pmcBEAR ShallSpawn:True BossChance:100");
        f.Append("09:00:01.000", "Init Boss wave:pmcBEAR ShallSpawn:True BossChance:100");
        File.WriteAllText(f.Output, File.ReadAllText(f.Ai));
        f.Poll();
        Assert.Equal(2, f.Reader.Current.Pmcs["pmcBEAR"].InitialPlannedWaves);
    }

    [Fact]
    public void TransitEndsOldRaidAndClearsInfectedAndDifficultyAssociations()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:infectedScreamer profileId:infectedA pos:(1, 2, 3)");
        f.Append("09:00:01.001", "Bot activate id:3 role:infectedScreamer profileId:infectedB pos:(4, 5, 6)");
        f.Append("09:00:02.000", "bot dispose:2");
        f.Append("09:00:02.001", "BotDied pos:(7, 8, 9)");
        f.Append("09:00:03.000", "Profile for bot typeA taken dataId:50: profileId:pmcA");
        f.Append("09:00:03.001", "Activate bots: Type:pmcBEAR BotDifficulty:impossible id:999 spawnPoints:1 id:50");
        File.AppendAllText(f.Application, LogFixture.Line("09:00:04.000", "application|RaidId:fixtureRaid, Count:0, Locations:Lighthouse -> RezervBase -> "));
        f.Append("09:00:05.000", "Bot activate id:4 role:pmcBEAR profileId:pmcA pos:(1, 2, 3)");
        f.Poll();
        Assert.Equal(2, f.Reader.Raids.Count);
        var oldRaid = f.Reader.Raids[0];
        Assert.True(oldRaid.Ended);
        Assert.Equal("DeadConfirmed", oldRaid.InfectedScreamer.LifeInstances[0].LifeStatus);
        Assert.Equal("DeathUnconfirmedAtEnd", oldRaid.InfectedScreamer.LifeInstances[1].LifeStatus);
        Assert.Equal("RezervBase", f.Reader.Current.Map);
        Assert.Empty(f.Reader.Current.InfectedScreamer.LifeInstances);
        Assert.Equal("", Assert.Single(f.Reader.Current.Pmcs["pmcBEAR"].LifeInstances).PmcDifficulty);
    }

    [Fact]
    public void TruncatedFileRebuildsInsteadOfRetainingStaleInstances()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:assault profileId:scavA pos:(1, 2, 3)");
        f.Poll();
        Assert.Single(f.Reader.Current.Scavs.LifeInstances);
        File.WriteAllText(f.Ai, "");
        f.Poll();
        Assert.Empty(f.Reader.Current.Scavs.LifeInstances);
    }

    [Fact]
    public void NewLogSessionDoesNotKeepPreviousRaidInstances()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:pmcBot profileId:raiderA pos:(1, 2, 3)");
        f.Poll();
        Assert.Single(f.Reader.Current.SpecialAis["pmcBot"].LifeInstances);
        string next = Path.Combine(f.Root, "Logs", "log_2000.01.01_10-00-00_fixture");
        Directory.CreateDirectory(next);
        File.WriteAllText(Path.Combine(next, "fixture application_000.log"),
            LogFixture.Line("10:00:00.000", "application|MatchingCompleted:0") +
            LogFixture.Line("10:00:00.001", "application|RaidId:nextRaid, Count:0, Locations:Woods -> "));
        f.Poll();
        Assert.Single(f.Reader.Raids);
        Assert.Equal("Woods", f.Reader.Current.Map);
        Assert.Empty(f.Reader.Current.SpecialAis);
    }

    [Fact]
    public void RaidEndMarksSurvivorsUnconfirmedAndIgnoresLaterActivations()
    {
        using var f = new LogFixture();
        f.Start();
        f.Append("09:00:01.000", "Bot activate id:2 role:assault profileId:scavA pos:(1, 2, 3)");
        f.Append("09:00:01.001", "Bot activate id:3 role:infectedScreamer profileId:infectedA pos:(4, 5, 6)");
        File.WriteAllText(Path.Combine(f.Session, "fixture backend_000.log"),
            LogFixture.Line("09:00:02.000", "backend|---> Request /client/match/local/end"));
        f.Append("09:00:03.000", "Bot activate id:4 role:assault profileId:lateScav pos:(1, 2, 3)");
        f.Poll();
        Assert.True(f.Reader.Current.Ended);
        Assert.Equal("DeathUnconfirmedAtEnd", Assert.Single(f.Reader.Current.Scavs.LifeInstances).LifeStatus);
        Assert.Equal("DeathUnconfirmedAtEnd", Assert.Single(f.Reader.Current.InfectedScreamer.LifeInstances).LifeStatus);
    }
}
