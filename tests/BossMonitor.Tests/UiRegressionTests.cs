using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using BossMonitor.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BossMonitor.Tests;

public class UiRegressionTests
{
    [Theory]
    [InlineData("ko-KR")]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    public void ImpossibleCountIsASubsetOfSurvivorsAndShowsZeroOnDeathOrRaidEnd(string language)
    {
        OnStaThread(() =>
        {
            string? previous=Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE",language);
                var (window,panel)=CreateRenderer("factory4_day","all");
                var raid=new RaidState {Map="factory4_day",Key="impossible-threat"};
                var threat=new BossInstance {PmcDifficulty="impossible"};
                var usecThreat=new BossInstance {PmcDifficulty="impossible"};
                var bear=new PmcState
                {
                    Role="pmcBEAR",
                    LifeInstances=[threat,new(),new() {PmcDifficulty="impossible",LifeStatus="DeadConfirmed",DiedAt=new(2000,1,1)}]
                };
                raid.Pmcs["pmcBEAR"]=bear;
                raid.Pmcs["pmcUSEC"]=new()
                {
                    Role="pmcUSEC",
                    LifeInstances=[usecThreat,new(),new() {LifeStatus="DeadConfirmed",DiedAt=new(2000,1,1)}]
                };
                string Risk(int count)=>string.Format(I18n.T("Impossible {0}명","Impossible {0} 人"),count);
                string Subset(int count)=>string.Format(I18n.T("전체 생존 추정 {0}명에 포함","已计入推测存活的 {0} 人"),count);
                IEnumerable<string> PmcLabels()=>Text(Assert.Single(panel.Children.Cast<UIElement>().TakeWhile(child=>child is Border)));
                Render(window,raid);
                Assert.Contains(Risk(2),PmcLabels());
                Assert.Contains(Subset(4),PmcLabels());
                Assert.Contains(PmcLabels(),text=>text.Contains($"{I18n.T("생존 추정","推测存活")} 4") && text.Contains($"{I18n.T("사망 확인","已确认死亡")} 2"));
                Assert.Single(PmcLabels().Where(text=>text.Contains("Impossible")));
                Assert.DoesNotContain(PmcLabels(),text=>text is "BEAR" or "USEC");

                threat.LifeStatus="DeadConfirmed";threat.DiedAt=new(2000,1,1);
                Render(window,raid);
                Assert.Contains(Risk(1),PmcLabels());
                Assert.Contains(Subset(3),PmcLabels());
                usecThreat.LifeStatus="DeadConfirmed";usecThreat.DiedAt=new(2000,1,1);
                Render(window,raid);
                Assert.Equal(Risk(0), Assert.Single(PmcLabels(), text => text.Contains("Impossible")));
                Assert.DoesNotContain(Subset(2), PmcLabels());
                raid.Ended=true;Render(window,raid);
                Assert.Equal(Risk(0), Assert.Single(PmcLabels(), text => text.Contains("Impossible")));
                raid.Ended=false;

                bear.LifeInstances.Add(new() {PmcDifficulty="impossible"});
                Render(window,raid);
                Assert.Contains(Risk(1),PmcLabels());
                Assert.Contains(Subset(3),PmcLabels());
                raid.Ended=true;Render(window,raid);
                Assert.DoesNotContain(Risk(1),PmcLabels());
                Assert.DoesNotContain(Subset(3),PmcLabels());
                Assert.Contains(string.Format(I18n.T("Impossible · 종료 시 사망 미확인 {0}명","Impossible · 结束时死亡未确认 {0} 人"),1),PmcLabels());
            }
            finally {Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE",previous);}
        });
    }

    [Theory]
    [InlineData("ko-KR")]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    public void WaitingBossesAndEventsRemainIndividualDetailedCards(string language)
    {
        OnStaThread(() =>
        {
            string? previous=Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE",language);
                var (window,_)=CreateRenderer("Shoreline","selected");
                var settings=Field<AppSettings>(window,"settings");
                settings.Maps["Shoreline"].Roles=["bossSanitar","bossKnight","bossTagilla"];
                var raid=new RaidState {Map="Shoreline",Key="detailed-cards"};
                raid.Bosses["bossSanitar"]=new() {Status="Confirmed",LifeInstances=[new()]};
                raid.Bosses["bossKnight"]=new() {Status="NotPlanned",SpawnChance=23};
                raid.InfectedTagilla.Status="NotPlanned";
                Render(window,raid);
                var bosses=Field<StackPanel>(window,"bosses");
                var events=Field<StackPanel>(window,"eventMobs");
                Assert.Equal(3,bosses.Children.Count);
                Assert.Equal(2,events.Children.Count);
                Assert.DoesNotContain(bosses.Children.OfType<Border>(),card=>card.Child is Expander);
                Assert.DoesNotContain(events.Children.OfType<Border>(),card=>card.Child is Expander);
                Assert.Contains(Catalog.BossName("bossTagilla",settings),Text(bosses));
                Assert.Contains(I18n.T("확인 불가","无法确认"),Text(bosses));
                Assert.Contains(I18n.T("스폰 예정 없음","未计划刷新"),Text(bosses));
                Assert.Contains($"{I18n.T("스폰 확률","刷新概率")} 23%",Text(bosses));
                Assert.Contains(I18n.T("스폰 미확인","未检测到刷新"),Text(events));
                Assert.Contains(I18n.T("스폰 예정 없음","未计划刷新"),Text(events));
                Assert.Equal(1,bosses.Children.OfType<Border>().Single(card=>Text(card).Contains(Catalog.BossName("bossTagilla",settings))).Opacity);
                Assert.Equal(0.65,bosses.Children.OfType<Border>().Single(card=>Text(card).Contains(Catalog.BossName("bossKnight",settings))).Opacity);
                raid.Bosses["bossKnight"].Status="Confirmed";
                raid.Bosses["bossKnight"].LifeInstances.Add(new());
                raid.InfectedTagilla.Status="Confirmed";
                raid.InfectedTagilla.LifeInstances.Add(new());
                Render(window,raid);
                Assert.Equal(3,bosses.Children.Count);
                Assert.Equal(2,events.Children.Count);
                Assert.Contains(Text(events),text=>text.Contains($"{I18n.T("생존 추정","推测存活")} 1"));
            }
            finally {Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE",previous);}
        });
    }
    [Fact]
    public void RecentChangesAnimateWithoutChangingCardLayoutOrHalloweenSkin()
    {
        OnStaThread(() =>
        {
            var (window,panel)=CreateRenderer("Lighthouse","all");
            var raid=new RaidState {Map="Lighthouse",Key="highlight-fixture"};
            raid.InfectedTagilla.Status="NotPlanned";
            Render(window,raid);
            var events=Field<StackPanel>(window,"eventMobs");
            var original=Assert.IsType<Border>(events.Children[0]);
            var skin=original.Background;
            Assert.IsType<StackPanel>(Assert.IsType<Border>(events.Children[1]).Child);
            raid.InfectedScreamer.LifeInstances.Add(new());
            raid.InfectedScreamer.Status="Confirmed";
            raid.Scavs.LifeInstances.Add(new());
            Render(window,raid);
            var infected=Assert.IsType<Border>(events.Children[0]);
            Assert.Equal(skin.ToString(),infected.Background.ToString());
            var surface=Assert.IsType<Grid>(infected.Child);
            var wash=Assert.IsType<Border>(surface.Children[0]);
            Assert.False(wash.IsHitTestVisible);
            Assert.True(wash.HasAnimatedProperties);
            Assert.Equal(original.Margin,infected.Margin);
            Assert.Equal(original.BorderThickness,infected.BorderThickness);
            var scavs=panel.Children.OfType<Border>().Single(card=>Text(card).Contains(I18n.T("스캐브","Scav")));
            Assert.True(Assert.IsType<Border>(Assert.IsType<Grid>(scavs.Child).Children[0]).HasAnimatedProperties);
            // A death briefly restores legibility, with the dimmed value kept as
            // the base so WPF returns to it after the animation completes.
            raid.Scavs.LifeInstances[0].LifeStatus="DeadConfirmed";
            raid.Scavs.LifeInstances[0].DiedAt=DateTime.Now;
            Render(window,raid);
            scavs=panel.Children.OfType<Border>().Single(card=>Text(card).Contains(I18n.T("스캐브","Scav")));
            Assert.True(scavs.HasAnimatedProperties);
            Assert.Equal(0.65,(double)scavs.GetAnimationBaseValue(UIElement.OpacityProperty));
            raid.Ended=true;Render(window,raid);
            Assert.All(events.Children.OfType<Border>(),card=>Assert.False(card.HasAnimatedProperties));
            Assert.IsType<StackPanel>(Assert.IsType<Border>(events.Children[1]).Child);
        });
    }

    [Theory]
    [InlineData("ko-KR", "Lighthouse", "exUsec", "all")]
    [InlineData("en-US", "Lighthouse", "exUsec", "all")]
    [InlineData("zh-CN", "Lighthouse", "exUsec", "all")]
    [InlineData("ko-KR", "RezervBase", "pmcBot", "selected")]
    [InlineData("en-US", "RezervBase", "pmcBot", "off")]
    [InlineData("zh-CN", "RezervBase", "pmcBot", "all")]
    [InlineData("ko-KR", "laboratory", "pmcBot", "all")]
    [InlineData("en-US", "laboratory", "pmcBot", "all")]
    [InlineData("zh-CN", "laboratory", "pmcBot", "all")]
    public void MainRenderKeepsSpecialAiWithScavsAndLocalizedPmcDifficulty(string language, string map, string specialRole, string filter)
    {
        OnStaThread(() =>
        {
            string? previous = Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", language);
                var (window, panel) = CreateRenderer(map, filter);
                var raid = new RaidState { Map = map, Key = "fixture" };
                raid.Pmcs["pmcBEAR"] = new()
                {
                    Role = "pmcBEAR",
                    LifeInstances = [new() { PmcDifficulty = "impossible" }, new() { PmcDifficulty = "impossible", LifeStatus = "DeadConfirmed", DiedAt = new(2000, 1, 1) }, new()]
                };
                raid.SpecialAis[specialRole] = new() { Role = specialRole, Status = "Confirmed", LifeInstances = [new() { Position = "1, 2, 3" }] };
                Render(window, raid);
                var labels = Text(panel).ToList();
                Assert.Contains(Catalog.SpecialAiName(specialRole), labels);
                Assert.DoesNotContain(labels, text => text is "BEAR" or "USEC");
                string impossible = Assert.Single(labels.Where(t => t.Contains("Impossible")));
                Assert.EndsWith(language.StartsWith("ko") ? "1명" : language.StartsWith("zh") ? "1 人" : "1", impossible);
                string infected = I18n.IsChinese ? "感染者" : I18n.IsKorean ? "감염자" : "Infected";
                Assert.Equal(map == "laboratory" ? 0 : 2, labels.Count(t => t == (language.StartsWith("ko") ? "스캐브" : "Scav")));
                Assert.DoesNotContain(infected, labels);
                var eventPanel = Field<StackPanel>(window, "eventMobs");
                var events = Field<StackPanel>(window, "eventSection");
                var left = Assert.IsType<StackPanel>(Field<StackPanel>(window, "bosses").Parent);
                Assert.Same(left, events.Parent);
                Assert.True(left.Children.IndexOf(events) > left.Children.IndexOf(Field<StackPanel>(window, "bosses")));
                Assert.Equal(map!="laboratory",Text(eventPanel).Contains(infected));
                Assert.Equal(map == "laboratory" ? Visibility.Collapsed : Visibility.Visible, events.Visibility);
                Assert.DoesNotContain(I18n.T("추가 스폰 감시 중","正在监测追加刷新"),Text(panel).Concat(Text(eventPanel)));
                if (language == "en-US")
                    Assert.DoesNotContain(labels.Concat(Text(eventPanel)), t => System.Text.RegularExpressions.Regex.IsMatch(t, "[가-힣\u4e00-\u9fff]"));

                // Updating an existing raid must invalidate Render's signature.
                raid.Pmcs["pmcBEAR"].LifeInstances[0].LifeStatus = "DeadConfirmed";
                raid.Pmcs["pmcBEAR"].LifeInstances[0].DiedAt = new(2000, 1, 1);
                Render(window, raid);
                string zero = string.Format(I18n.T("Impossible {0}명", "Impossible {0} 人"), 0);
                Assert.Equal(zero, Assert.Single(Text(panel), t => t.Contains("Impossible")));
                Assert.DoesNotContain(I18n.T("추가 스폰 감시 종료","追加刷新监测已结束"),Text(panel).Concat(Text(eventPanel)));
                raid.Ended = true;
                Render(window, raid);
                Assert.Equal(zero, Assert.Single(Text(panel), t => t.Contains("Impossible")));
            }
            finally { Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", previous); }
        });
    }

    [Fact]
    public void MainRenderShowsNewInfectedCountsWithoutReplacingSpecialAi()
    {
        OnStaThread(() =>
        {
            var (window, panel) = CreateRenderer("Lighthouse", "all");
            var raid = new RaidState { Map = "Lighthouse", Key = "fixture" };
            Render(window, raid);
            var events = Field<StackPanel>(window, "eventMobs");
            string before = string.Join("\n", Text(events));
            raid.InfectedScreamer.Status = "Confirmed";
            raid.InfectedScreamer.LifeInstances.Add(new());
            Render(window, raid);
            Assert.NotEqual(before, string.Join("\n", Text(events)));
            Assert.DoesNotContain(I18n.T("감염자", "感染者"), Text(panel));
            Assert.Contains(Catalog.SpecialAiName("exUsec"), Text(panel));
        });
    }

    [Theory]
    [InlineData("ko-KR", false)]
    [InlineData("en-US", false)]
    [InlineData("zh-CN", false)]
    [InlineData("ko-KR", true)]
    [InlineData("en-US", true)]
    [InlineData("zh-CN", true)]
    public void RaidHeaderShowsKillsWithoutOpaqueRaidIdsAndUpdatesWithoutCardChanges(string language, bool ended)
    {
        OnStaThread(() =>
        {
            string? previous = Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", language);
                var (window, _) = CreateRenderer("Lighthouse", "all");
                var raid = new RaidState { Map = "Lighthouse", RaidId = "opaqueInternalRaidId", PlayerKills = 7, Ended = ended };
                Render(window, raid);
                var kills = Field<TextBlock>(window, "playerKills");
                var status = Field<TextBlock>(window, "raidLabel");
                Assert.Equal("7", kills.Text);
                Assert.Equal(I18n.T("내 처치", "我的击杀"), Field<TextBlock>(window, "playerKillsCaption").Text);
                Assert.Equal(Visibility.Visible, Field<Border>(window, "killsBadge").Visibility);
                Assert.Equal(1, Grid.GetColumn(Field<Border>(window, "killsBadge")));
                Assert.Equal(ended ? I18n.T("종료된 레이드", "已结束战局") : I18n.T("현재 레이드", "当前战局"), status.Text);
                Assert.DoesNotContain(raid.RaidId, status.Text + kills.Text);
                raid.PlayerKills = 8; raid.RaidId = "";
                Render(window, raid);
                Assert.EndsWith("8", kills.Text);
                Assert.Equal(Visibility.Visible, Field<Border>(window, "killsBadge").Visibility);
                typeof(MainWindow).GetMethod("RenderRaidHeader", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [new RaidState()]);
                Assert.Equal("", kills.Text);
                Assert.Equal(Visibility.Collapsed, Field<Border>(window, "killsBadge").Visibility);
                Assert.Equal(Visibility.Collapsed, status.Visibility);
            }
            finally { Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", previous); }
        });
    }

    [Theory]
    [InlineData("ko-KR")]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    public void InfectedTagillaAppearsOnlyInEventsAndShowsProbabilityDecisionAndLifeChanges(string language)
    {
        OnStaThread(() =>
        {
            string? previous = Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", language);
                var (window, panel) = CreateRenderer("factory4_day", "all");
                var raid = new RaidState { Map = "factory4_day", Key = "eventBoss" };
                var events = Field<StackPanel>(window, "eventMobs");
                string name = I18n.T("감염된 타길라", "感染版塔吉拉");
                Render(window, raid);
                Assert.DoesNotContain(name, Text(events));
                raid.InfectedTagilla.Status = "NotPlanned"; raid.InfectedTagilla.SpawnChance = 6;
                Render(window, raid);
                Assert.Contains(name, Text(events));
                Assert.Contains(I18n.T("스폰 예정 없음", "未计划刷新"),Text(events));
                Assert.Contains($"{I18n.T("스폰 확률", "刷新概率")} 6%", Text(events));
                Assert.DoesNotContain(name, Text(panel));
                Assert.DoesNotContain(name, Text(Field<StackPanel>(window, "bosses")));
                raid.InfectedTagilla.Status = "Planned";
                Render(window, raid);
                Assert.Contains(I18n.T("스폰 예정", "计划刷新"), Text(events));
                raid.InfectedTagilla.Status = "Confirmed";
                raid.InfectedTagilla.LifeInstances.Add(new());
                Render(window, raid);
                var card = events.Children.OfType<Border>().Single(card=>card.Child is not Expander && Text(card).Contains(name));
                Assert.Contains(Text(card), t => t.Contains($"{I18n.T("생존 추정", "推测存活")} 1"));
                raid.InfectedTagilla.LifeInstances[0].LifeStatus = "DeadConfirmed";
                raid.InfectedTagilla.LifeInstances[0].DiedAt = new(2000, 1, 1);
                Render(window, raid);
                Assert.Contains(Text(events.Children.OfType<Border>().Single(card=>card.Child is not Expander && Text(card).Contains(name))), t => t.Contains($"{I18n.T("사망 확인", "已确认死亡")} 1"));
            }
            finally { Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", previous); }
        });
    }

    [Theory]
    [InlineData("ko-KR")]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    public void MergedPmcCardShowsSpawnDecisionUntilActivationWithoutInitialSpawnRow(string language)
    {
        OnStaThread(() =>
        {
            string? previous = Environment.GetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE");
            try
            {
                Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", language);
                var (window, panel) = CreateRenderer("factory4_day", "all");
                var raid = new RaidState { Map = "factory4_day", Key = "pendingPmc" };
                var bear = new PmcState { Role = "pmcBEAR", PlannedWaves = 1, InitialPlannedWaves = 1 };
                IEnumerable<string> PmcLabels() => Text(Assert.IsType<Border>(panel.Children[0]));
                raid.Pmcs["pmcBEAR"] = bear;
                raid.Pmcs["pmcUSEC"] = new() { Role = "pmcUSEC", NotPlannedWaves = 1 };
                var pmcColumn = Assert.IsType<StackPanel>(panel.Parent);
                var pmcHeading = Assert.IsType<TextBlock>(pmcColumn.Children[0]);
                Render(window, raid);
                Assert.Equal("AI PMC", pmcHeading.Text);
                Assert.Equal("AI PMC", PmcLabels().First());
                Assert.Contains(I18n.T("스폰 예정", "计划刷新"), PmcLabels());
                Assert.DoesNotContain(PmcLabels(), t => t.Contains("Impossible"));
                Assert.DoesNotContain(I18n.T("스폰 예정 없음", "未计划刷新"), Text(panel));
                Assert.DoesNotContain(I18n.T("초기 스폰 예정", "初始刷新已计划"), Text(panel));

                bear.PlannedWaves = 0; bear.InitialPlannedWaves = 0;
                Render(window, raid);
                Assert.Contains(I18n.T("스폰 미확인", "未检测到刷新"), PmcLabels());
                bear.NotPlannedWaves = 1;
                Render(window, raid);
                Assert.Contains(I18n.T("스폰 예정 없음", "未计划刷新"), PmcLabels());
                var bearPmc = new BossInstance();
                bear.LifeInstances.Add(bearPmc);
                bear.FirstSpawnedAt = new(2000, 1, 1, 11, 12, 52);
                var usec = raid.Pmcs["pmcUSEC"];
                usec.FirstSpawnedAt = new(2000, 1, 1, 11, 12, 51);
                usec.LifeInstances.Add(new() { PmcDifficulty = "impossible" });
                usec.LifeInstances.Add(new() { LifeStatus = "DeadConfirmed", DiedAt = new(2000, 1, 1) });
                raid.Scavs.LifeInstances.Add(new());
                Render(window, raid);
                Assert.Equal("AI PMC", pmcHeading.Text);
                Assert.DoesNotContain(I18n.T("스폰 미확인", "未检测到刷新"), PmcLabels());
                Assert.DoesNotContain(I18n.T("스폰 예정", "计划刷新"), Text(panel));
                Assert.DoesNotContain(I18n.T("초기 스폰", "初始刷新"), Text(panel));
                string aliveLabel = I18n.T("생존 추정", "推测存活");
                string deadLabel = I18n.T("사망 확인", "已确认死亡");
                Assert.Contains(PmcLabels(), t => t.Contains($"{aliveLabel} 2") && t.Contains($"{deadLabel} 1"));
                Assert.Equal($"{I18n.T("첫 활성화", "首次激活")} 11:12:51", Assert.Single(PmcLabels().Where(t => t.Contains(I18n.T("첫 활성화", "首次激活")))));
                bearPmc.LifeStatus = "DeadConfirmed";
                bearPmc.DiedAt = new(2000, 1, 1);
                Render(window, raid);
                Assert.Contains(PmcLabels(), t => t.Contains($"{aliveLabel} 1") && t.Contains($"{deadLabel} 2"));
                bear.AdditionalPlannedWaves=1;
                Render(window,raid);
                Assert.Contains(I18n.T("추가 스폰 예정","追加刷新已计划"),PmcLabels());
                raid.Ended=true;
                Render(window,raid);
                Assert.Equal("AI PMC", pmcHeading.Text);
                Assert.Equal("AI PMC", PmcLabels().First());
                string unconfirmedLabel = I18n.T("종료 시 사망 미확인", "结束时未确认死亡");
                Assert.Contains(PmcLabels(), t => t.Contains($"{unconfirmedLabel} 1") && t.Contains($"{deadLabel} 2"));
                Assert.DoesNotContain(I18n.T("추가 스폰 예정","追加刷新已计划"),PmcLabels());
                Assert.DoesNotContain(I18n.T("추가 스폰 감시 종료","追加刷新监测已结束"),Text(panel));
            }
            finally { Environment.SetEnvironmentVariable("TARKOV_BOSS_MONITOR_LANGUAGE", previous); }
        });
    }

    private static (MainWindow Window, StackPanel Panel) CreateRenderer(string map, string filter)
    {
        // Bypass only application startup (tray icon, settings writes, timers).
        // Execute the actual top-level Render method, including section ordering
        // and render-signature invalidation, against real WPF controls.
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var panel = new StackPanel();
        var settings = new AppSettings();
        settings.Maps[map] = new() { Mode = filter };
        Set("settings", settings); Set("pmcs", panel); Set("bosses", new StackPanel());
        Set("waiting", new StackPanel()); Set("raidColumns", new Grid());
        Set("eventMobs", new StackPanel()); Set("eventSection", new StackPanel());
        Set("map", new TextBlock()); Set("raidLabel", new TextBlock());
        Set("playerKillsCaption", new TextBlock()); Set("playerKills", new TextBlock()); Set("killsBadge", new Border());
        typeof(MainWindow).GetMethod("BuildRaidColumns", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, []);
        typeof(MainWindow).GetMethod("CreateRaidHeader", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, []);
        return (window, panel);
        void Set(string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
    }

    private static T Field<T>(MainWindow window, string name)
        => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static void Render(MainWindow window, RaidState raid)
        => typeof(MainWindow).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(window, [raid]);

    private static IEnumerable<string> Text(UIElement element)
    {
        if (element is TextBlock text) yield return new System.Windows.Documents.TextRange(text.ContentStart,text.ContentEnd).Text;
        if (element is Panel panel)
            foreach (UIElement child in panel.Children) foreach (string value in Text(child)) yield return value;
        if (element is Decorator decorator && decorator.Child != null)
            foreach (string value in Text(decorator.Child)) yield return value;
    }

    private static void OnStaThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
