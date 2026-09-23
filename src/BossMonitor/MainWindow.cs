using System.Windows.Threading;

namespace BossMonitor;
public class MainWindow : Window
{
    private static readonly TimeSpan EndedRaidDisplayTime=TimeSpan.FromMinutes(10);
    private AppSettings settings=AppSettings.Load();
    private readonly LogReader reader=new();
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromSeconds(1)};
    private readonly TextBlock map=Theme.Label(I18n.T("게임 경로 설정 대기","等待设置游戏路径"),26),connection=Theme.Label("",13,Theme.Muted),raidLabel=Theme.Label("",13,Theme.Muted);
    private readonly StackPanel bosses=new();
    private readonly StackPanel pmcs=new();
    private readonly Button pause=new() {Content=I18n.T("감시 일시 중지","暂停监控")};
    private readonly Forms.NotifyIcon tray;
    private readonly DockPanel monitor;
    private SettingsView? settingsView;
    private bool subpage;
    private bool busy,paused,closed,configuring,exitRequested;
    private string rendering="",retryRoot="";
    private readonly HashSet<string> notifiedBosses=new(StringComparer.OrdinalIgnoreCase);
    private DateTime hiddenAt=DateTime.MaxValue;
    private DateTime lastConfig=DateTime.MinValue;
    public MainWindow()
    {
        // The installer configures the machine path. Adopt it in the actual desktop user's profile.
        try
        {
            string installed=Maintenance.State().GameRoot;
            if(installed!="" && !installed.Equals(settings.GamePath,StringComparison.OrdinalIgnoreCase))
            {
                settings.GamePath=Paths.ValidateGame(installed);settings.Save();
            }
        }
        catch { }
        Title="Tarkov Boss Monitor";Width=700;Height=660;MinWidth=530;MinHeight=450;Topmost=settings.AlwaysOnTop;Theme.WindowIcon(this);
        var root=new DockPanel {Margin=new Thickness(24)};monitor=root;Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var branding=new StackPanel {Orientation=Orientation.Horizontal};
        branding.Children.Add(Theme.Label("BOSS MONITOR",24,Theme.Green));header.Children.Add(branding);
        raidLabel.Visibility=Visibility.Collapsed;connection.Visibility=Visibility.Collapsed;
        header.Children.Add(map);header.Children.Add(raidLabel);header.Children.Add(connection);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var actions=new WrapPanel();
        pause.Click+=(s,e)=>TogglePause();actions.Children.Add(pause);
        actions.Children.Add(Theme.Action(I18n.T("설정","设置"),(s,e)=>OpenSettings()));
        actions.Children.Add(Theme.Action(I18n.T("트레이로","最小化到托盘"),(s,e)=>HideToTray()));footer.Children.Add(actions);
        var tabs=new TabControl {Background=Theme.Bg,Foreground=Theme.Text,BorderThickness=new Thickness(0),Margin=new Thickness(0,14,0,10)};
        tabs.Items.Add(MonitorTab(I18n.T("보스","首领"),bosses));
        tabs.Items.Add(MonitorTab("AI PMC",pmcs));
        root.Children.Add(tabs);
        var menu=new Forms.ContextMenuStrip();menu.Items.Add(I18n.T("창 열기","打开窗口"),null,(s,e)=>Dispatcher.Invoke(ShowMain));menu.Items.Add(I18n.T("감시 일시 중지 / 재개","暂停/继续监控"),null,(s,e)=>Dispatcher.Invoke(TogglePause));menu.Items.Add(I18n.T("설정","设置"),null,(s,e)=>Dispatcher.Invoke(()=>{ShowMain();OpenSettings();}));menu.Items.Add(I18n.T("종료","退出"),null,(s,e)=>Dispatcher.Invoke(ExitApplication));
        using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/boss-monitor.ico")).Stream;
        tray=new Forms.NotifyIcon {Icon=new System.Drawing.Icon(stream),Text="Tarkov Boss Monitor",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(ShowMain);
        tray.BalloonTipClicked+=(s,e)=>Dispatcher.Invoke(ShowMain);
        timer.Tick+=async(s,e)=>await Tick();
        Loaded+=async(s,e)=>
        {
            timer.Start();await Tick();
        };
        if(string.IsNullOrWhiteSpace(settings.GamePath))OpenSettings(true);
        else Render(DisplayRaid(reader.Current));
        Closing+=(s,e)=>
        {
            if(exitRequested)return;
            if(settings.CloseToTray){e.Cancel=true;HideToTray();}
            else if(settingsView?.IsApplying==true || configuring)
            {e.Cancel=true;MessageBox.Show(this,I18n.T("로그 설정 작업이 완료된 후 종료해 주세요.","请等待日志设置操作完成后再退出。"),"Tarkov Boss Monitor");}
        };
        Application.Current.SessionEnding+=(s,e)=>{exitRequested=true;};
        Closed+=(s,e)=>{closed=true;timer.Stop();tray.Visible=false;tray.Dispose();};
    }
    private static TabItem MonitorTab(string title,StackPanel panel) => new()
    {
        Header=title,Foreground=Theme.Text,Padding=new Thickness(18,9,18,9),
        Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,12,0,0)}
    };
    internal void ShowMain()
    {
        Show();WindowState=WindowState.Normal;Activate();Focus();
        // A launch from Explorer is an explicit request to surface the app. Briefly
        // promoting the window also works around Windows foreground-lock behavior.
        bool configuredTopmost=settings.AlwaysOnTop;
        Topmost=true;Topmost=configuredTopmost;
    }
    private void HideToTray()
    {
        if(!IsVisible)return;
        hiddenAt=DateTime.Now;Hide();
        string message=paused?I18n.T("트레이로 숨겼습니다. 감시는 현재 일시 중지 상태입니다.","已隐藏到托盘，监控当前已暂停。"):settings.GamePath==""?I18n.T("트레이로 숨겼습니다. 창을 다시 열어 게임 연결 설정을 완료해 주세요.","已隐藏到托盘，请重新打开窗口完成游戏连接设置。"):I18n.T("트레이로 숨겼습니다. 보스 감시는 계속됩니다.","已隐藏到托盘，首领监控仍在继续。");
        tray.ShowBalloonTip(4000,"Tarkov Boss Monitor",message+I18n.T(" 알림을 클릭하면 창을 다시 엽니다."," 点击通知可重新打开窗口。"),Forms.ToolTipIcon.Info);
    }
    private void ExitApplication()
    {
        if(settingsView?.IsApplying==true || configuring)
        {
            ShowMain();MessageBox.Show(this,I18n.T("로그 설정 작업이 완료된 후 종료해 주세요.","请等待日志设置操作完成后再退出。"),"Tarkov Boss Monitor");return;
        }
        exitRequested=true;Close();
    }
    private void SetStatus(string text){connection.Text=text;connection.Visibility=text==""?Visibility.Collapsed:Visibility.Visible;}
    private void TogglePause(){paused=!paused;pause.Content=paused?I18n.T("감시 재개","继续监控"):I18n.T("감시 일시 중지","暂停监控");SetStatus(paused?I18n.T("감시 일시 중지","监控已暂停"):"");rendering="";Render(DisplayRaid(reader.Current));}
    private void OpenSettings(bool first=false)
    {
        if(busy || configuring || settingsView!=null)return;
        var view=new SettingsView(settings,reader.ObservedRoles,first);settingsView=view;subpage=true;Topmost=false;Content=view;Title=first?I18n.T("처음 시작하기 · Tarkov Boss Monitor","开始使用 · Tarkov Boss Monitor"):I18n.T("설정 · Tarkov Boss Monitor","设置 · Tarkov Boss Monitor");
        view.Completed+=saved=>
        {
            if(saved){settings=view.Settings;reader.Reset();rendering="";retryRoot="";lastConfig=DateTime.MinValue;}
            settingsView=null;ReturnToMonitor();Render(DisplayRaid(reader.Current));
        };
    }
    private void ReturnToMonitor(){subpage=false;Content=monitor;Title="Tarkov Boss Monitor";Topmost=settings.AlwaysOnTop;}
    private async Task Tick()
    {
        if(busy||paused||closed||(subpage && IsVisible)||settingsView?.IsApplying==true||settings.GamePath=="")return;
        busy=true;
        try
        {
            if(DateTime.Now-lastConfig>TimeSpan.FromSeconds(30))
            {
                lastConfig=DateTime.Now;
                bool mismatch=!Paths.LoggingReady(settings.GamePath);
                try {var machine=Maintenance.State();mismatch|=machine.PendingTrace || !machine.GameRoot.Equals(settings.GamePath,StringComparison.OrdinalIgnoreCase);}catch{mismatch=true;}
                if(mismatch && !Product.GameRunning() && retryRoot!=settings.GamePath)
                {
                    configuring=true;SetStatus(I18n.T("로그 설정 적용 중","正在应用日志设置"));
                    int code=await Helpers.Elevated("configure",settings.GamePath);
                    configuring=false;
                    if(code!=0)retryRoot=settings.GamePath;
                }
            }
            await Task.Run(()=>reader.Poll(settings.GamePath,settings));
            if(closed)return;
            var current=DisplayRaid(reader.Current);
            map.Text=current.Map==""?I18n.T("레이드 감지 대기","等待检测战局"):Catalog.MapName(current.Map);
            raidLabel.Text=current.RaidId==""?"":$"{(current.Ended?I18n.T("종료된 레이드","已结束战局"):I18n.T("현재 레이드","当前战局"))} · {current.RaidId}";
            raidLabel.Visibility=current.RaidId==""?Visibility.Collapsed:Visibility.Visible;
            SetStatus(Paths.LoggingReady(settings.GamePath)?"":I18n.T("로그 설정 확인 필요","需要检查日志设置"));
            Render(current);
            NotifyBosses(current);
        }
        catch(System.ComponentModel.Win32Exception){configuring=false;retryRoot=settings.GamePath;SetStatus(I18n.T("설정 적용 취소됨","已取消应用设置"));}
        catch(Exception ex){configuring=false;SetStatus(I18n.T("감시 오류: ","监控错误：")+ex.Message);}
        finally {busy=false;}
    }
    private static RaidState DisplayRaid(RaidState raid)
        => raid.Ended && (raid.EndedAt==null || DateTime.Now-raid.EndedAt>EndedRaidDisplayTime) ? new() : raid;
    private void Render(RaidState raid)
    {
        var filter=settings.Filter(raid.Map);
        var roles=filter.Mode=="selected"?filter.Roles:raid.Bosses.Keys.ToList();
        if(filter.Mode=="off"||raid.Map=="")roles=[];
        string signature=raid.Key+raid.Ended+paused+filter.Mode+string.Join("|",roles.Select(r=>r+System.Text.Json.JsonSerializer.Serialize(raid.Bosses.GetValueOrDefault(r))))+System.Text.Json.JsonSerializer.Serialize(raid.Pmcs);
        if(signature==rendering)return;rendering=signature;bosses.Children.Clear();pmcs.Children.Clear();
        if(raid.Map==""){RenderWaiting(bosses);RenderWaiting(pmcs);return;}
        RenderPmcs(raid);
        if(filter.Mode=="off"){bosses.Children.Add(Theme.Label(I18n.T("이 맵은 감시하지 않도록 설정되어 있습니다.","此地图已设置为不监控。"),16,Theme.Muted));return;}
        if(roles.Count==0){bosses.Children.Add(Theme.Label(filter.Mode=="selected"?I18n.T("선택된 보스가 없습니다. 설정에서 보스를 선택해 주세요.","未选择首领，请在设置中选择。"):I18n.T("모든 보스 감시 중 · 보스 판정 기록 대기","正在监控所有首领 · 等待判定记录"),16,Theme.Muted));return;}
        foreach(string role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var state=raid.Bosses.GetValueOrDefault(role)??new BossState {Role=role};
            Brush accent=state.Status=="Confirmed"?Theme.Green:state.Status=="Planned"?Theme.Amber:Theme.Muted;
            var content=new StackPanel {Margin=new Thickness(18,12,18,12)};
            content.Children.Add(Theme.Label(Catalog.BossName(role,settings),20));
            content.Children.Add(Theme.Label(Helpers.Status(state.Status)+(raid.Ended?I18n.T(" · 레이드 종료"," · 战局已结束"):""),18,accent));
            if(state.SpawnChance is int chance)content.Children.Add(Theme.Label($"{I18n.T("스폰 확률", "刷新概率")} {chance}%",14,Theme.Muted));
            if(state.At!=null)content.Children.Add(Theme.Label($"{I18n.T("기록", "记录")} {state.At:HH:mm:ss} · {role}",12,Theme.Muted));
            if(state.Status=="Confirmed")
            {
                content.Children.Add(Theme.Label(Helpers.LifeStatus(state,raid.Ended),17,
                    state.LifeInstances.Count>0 && state.LifeInstances.All(i=>i.LifeStatus=="DeadConfirmed")?Theme.Muted:Theme.Green));
                if(state.LifeInstances.Count>1)
                    foreach(var instance in state.LifeInstances.Where(i=>i.DiedAt!=null))
                        content.Children.Add(Theme.Label($"{I18n.T("개체", "个体")} {instance.BotId} · {I18n.T("사망 확인", "已确认死亡")} {instance.DiedAt:HH:mm:ss}",13,Theme.Muted));
                content.Children.Add(Theme.Label($"{I18n.T("최초 구역", "首次区域")}: {(state.Zone==""?I18n.T("확인 불가","无法确认"):state.Zone)}\n{I18n.T("최초 좌표", "首次坐标")}: {(state.Position==""?I18n.T("확인 불가","无法确认"):state.Position)}",13));
                if(state.Count>1)content.Children.Add(Theme.Label($"{I18n.T("활성화 개체", "已激活个体")} {state.Count}",12,Theme.Muted));
            }
            bosses.Children.Add(new Border {Background=Theme.Card,BorderBrush=accent,BorderThickness=new Thickness(3,0,0,0),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,12),Child=content});
        }
    }
    private void RenderPmcs(RaidState raid)
    {
        var states=new[]{"pmcBEAR","pmcUSEC"}.Select(role=>raid.Pmcs.GetValueOrDefault(role)??new PmcState {Role=role}).ToList();
        if(states.All(p=>p.TotalWaves==0 && p.Count==0))
        {
            pmcs.Children.Add(Theme.Label(I18n.T("AI PMC 웨이브 기록 대기","等待 AI PMC 波次记录"),16,Theme.Muted));
            return;
        }
        foreach(var state in states)
        {
            Brush accent=state.Status=="Confirmed"?Theme.Green:state.Status=="Planned"?Theme.Amber:Theme.Muted;
            var content=new StackPanel {Margin=new Thickness(18,12,18,12)};
            content.Children.Add(Theme.Label(state.Role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase)?"BEAR":"USEC",20));
            string initial=state.InitialPlannedWaves>0
                ? state.Count>0 ? I18n.T("초기 스폰","初始刷新") : I18n.T("초기 스폰 예정","初始刷新已计划")
                : I18n.T("초기 스폰 없음","无初始刷新");
            Brush initialColor=state.InitialPlannedWaves==0?Theme.Muted:state.Count>0?Theme.Green:Theme.Amber;
            content.Children.Add(Theme.Label(initial,18,initialColor));
            string additional=raid.Ended
                ? I18n.T("추가 스폰 감시 종료","追加刷新监测已结束")
                : state.AdditionalPlannedWaves>0
                    ? I18n.T("추가 스폰 예정","追加刷新已计划")
                    : I18n.T("추가 스폰 감시 중","正在监测追加刷新");
            content.Children.Add(Theme.Label(additional,14,state.AdditionalPlannedWaves>0?Theme.Amber:Theme.Muted));
            if(state.Count>0)
            {
                content.Children.Add(Theme.Label($"{I18n.T("실제 활성화","实际激活")} {state.Count}{I18n.T("명","个")}",16,Theme.Green));
                int dead=state.LifeInstances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
                int alive=state.Count-dead;
                string life=raid.Ended
                    ? $"{I18n.T("종료 시 사망 미확인","结束时未确认死亡")} {alive}{I18n.T("명","个")} · {I18n.T("사망 확인","已确认死亡")} {dead}{I18n.T("명","个")}"
                    : $"{I18n.T("생존 추정","推测存活")} {alive}{I18n.T("명","个")} · {I18n.T("사망 확인","已确认死亡")} {dead}{I18n.T("명","个")}";
                content.Children.Add(Theme.Label(life,15,alive>0?Theme.Green:Theme.Muted));
                content.Children.Add(Theme.Label($"{I18n.T("첫 활성화","首次激活")} {state.FirstSpawnedAt:HH:mm:ss}\n{I18n.T("최초 좌표","首次坐标")}: {(state.FirstPosition==""?I18n.T("확인 불가","无法确认"):state.FirstPosition)}",13));
            }
            pmcs.Children.Add(new Border {Background=Theme.Card,BorderBrush=accent,BorderThickness=new Thickness(3,0,0,0),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,12),Child=content});
        }
    }
    private void RenderWaiting(StackPanel target)
    {
        var hero=new Grid {Margin=new Thickness(0,0,0,20)};
        hero.ColumnDefinitions.Add(new() {Width=new GridLength(100)});
        hero.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        hero.Children.Add(new Image {Source=Theme.Icon(),Width=72,Height=72,HorizontalAlignment=HorizontalAlignment.Left});
        var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(text,1);hero.Children.Add(text);
        text.Children.Add(Theme.Label(paused?I18n.T("●  감시 일시 중지","●  监控已暂停"):I18n.T("●  감시 중","●  正在监控"),15,paused?Theme.Muted:Theme.Green));
        text.Children.Add(Theme.Label(I18n.T("다음 레이드를 기다리고 있습니다","正在等待下一场战局"),20));
        target.Children.Add(new Border {Background=Theme.Color("#E4EFE9"),CornerRadius=new CornerRadius(12),Padding=new Thickness(22,18,22,0),Child=hero,Margin=new Thickness(0,0,0,20)});
    }
    private void NotifyBosses(RaidState raid)
    {
        if(raid.Ended || raid.Map=="" || raid.RaidId=="")return;
        var filter=settings.Filter(raid.Map);if(filter.Mode=="off")return;
        var newlyConfirmed=new List<BossState>();
        foreach(var boss in raid.Bosses.Values.Where(b=>b.Status=="Confirmed"))
        {
            if(filter.Mode=="selected" && !filter.Roles.Contains(boss.Role,StringComparer.OrdinalIgnoreCase))continue;
            string key=settings.GamePath+"|"+raid.Key+"|"+boss.Role;
            // Mark visible discoveries too; hiding the window must not re-alert already known bosses.
            if(!notifiedBosses.Add(key) || IsVisible || boss.At==null || boss.At.Value<hiddenAt.AddSeconds(-2))continue;
            newlyConfirmed.Add(boss);
        }
        if(newlyConfirmed.Count==0)return;
        string names=string.Join(", ",newlyConfirmed.Select(b=>Catalog.BossName(b.Role,settings)));
        tray.ShowBalloonTip(6000,$"{Catalog.MapName(raid.Map)} · {I18n.T("보스 스폰 확인", "已确认首领刷新")}",names+I18n.T("의 실제 활성화 기록을 확인했습니다. 클릭하면 감시 화면을 엽니다.","的实际激活记录已确认。点击打开监控界面。"),Forms.ToolTipIcon.Info);
    }
}
