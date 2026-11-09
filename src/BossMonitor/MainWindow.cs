using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace BossMonitor;
public class MainWindow : Window
{
    private const double DefaultWindowWidth=900,DefaultWindowHeight=700;
    private const string PmcTitle = "AI PMC";
    private const string SupportPageUrl = "https://buymeacoffee.com/wjdtjdwls9p";
    private static readonly TimeSpan EndedRaidDisplayTime=TimeSpan.FromMinutes(10);
    private AppSettings settings=AppSettings.Load();
    private readonly LogReader reader=new();
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromSeconds(1)};
    private readonly TextBlock map=Theme.Label(I18n.T("게임 경로 설정 대기","等待设置游戏路径"),26),connection=Theme.Label("",13,Theme.Muted),raidLabel=Theme.Label("",13,Theme.Muted);
    private readonly TextBlock playerKillsCaption=new() {FontSize=13,Foreground=Theme.Muted,TextAlignment=TextAlignment.Right,Margin=new Thickness(0,0,0,2)};
    private readonly TextBlock playerKills=new() {FontSize=32,FontWeight=FontWeights.SemiBold,Foreground=Theme.Green,TextAlignment=TextAlignment.Right};
    private readonly Border killsBadge=new() {Visibility=Visibility.Collapsed,BorderBrush=Theme.Color("#D5E0D9"),BorderThickness=new Thickness(1,0,0,0),Padding=new Thickness(18,0,0,0),Margin=new Thickness(24,0,0,0),VerticalAlignment=VerticalAlignment.Center};
    private readonly StackPanel bosses=new();
    private readonly StackPanel eventMobs=new();
    private readonly StackPanel eventSection=new() {Margin=new Thickness(0,14,0,0),Visibility=Visibility.Collapsed};
    private readonly StackPanel pmcs=new();
    private readonly StackPanel waiting=new();
    private readonly Grid raidColumns=new();
    private readonly ScrollViewer monitorScroll=new() {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
    private readonly Border updateBanner=new() {Visibility=Visibility.Collapsed,Background=Theme.Color("#E4EFE9"),BorderBrush=Theme.Green,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12,8,8,8),Margin=new Thickness(0,6,0,0)};
    private readonly Button pause=new() {Content=I18n.T("감시 일시 중지","暂停监控")};
    private readonly Forms.NotifyIcon tray;
    private readonly DockPanel monitor;
    private SettingsView? settingsView;
    private WindowState restoreWindowState = WindowState.Normal;
    private bool subpage;
    private bool busy,paused,closed,configuring,exitRequested;
    private string rendering="",retryRoot="";
    private CardChangeTracker? cardChanges;
    private readonly HashSet<string> notifiedBosses=new(StringComparer.OrdinalIgnoreCase);
    private DateTime hiddenAt=DateTime.MaxValue;
    private DateTime lastConfig=DateTime.MinValue;
    private DateTime nextUpdateCheckUtc=DateTime.MinValue;
    private bool checkingForUpdates,updateNotified;
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
        Title="Tarkov Boss Monitor";Width=DefaultWindowWidth;Height=DefaultWindowHeight;MinWidth=620;MinHeight=450;Topmost=settings.AlwaysOnTop;Theme.WindowIcon(this);
        var root=new DockPanel {Margin=new Thickness(24)};monitor=root;Content=root;
        var header=new StackPanel();DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var branding=new Grid();
        branding.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        branding.ColumnDefinitions.Add(new() {Width=GridLength.Auto});
        branding.Children.Add(Theme.Label("BOSS MONITOR",24,Theme.Green));
        var version=Theme.Label($"v{UpdateChecker.CurrentVersion.ToString(3)}",12,Theme.Muted);
        version.HorizontalAlignment=HorizontalAlignment.Right;version.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(version,1);branding.Children.Add(version);
        header.Children.Add(branding);
        raidLabel.Visibility=Visibility.Collapsed;connection.Visibility=Visibility.Collapsed;
        header.Children.Add(CreateRaidHeader());header.Children.Add(connection);header.Children.Add(updateBanner);
        var footer = CreateMonitorFooter();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        BuildRaidColumns();
        var monitorBody=new Grid {Margin=new Thickness(0,8,0,6)};
        monitorBody.Children.Add(raidColumns);monitorBody.Children.Add(waiting);
        monitorScroll.Content=monitorBody;root.Children.Add(monitorScroll);
        var menu=new Forms.ContextMenuStrip();menu.Items.Add(I18n.T("창 열기","打开窗口"),null,(s,e)=>Dispatcher.BeginInvoke(ShowMain));menu.Items.Add(I18n.T("감시 일시 중지 / 재개","暂停/继续监控"),null,(s,e)=>Dispatcher.Invoke(TogglePause));menu.Items.Add(I18n.T("설정","设置"),null,(s,e)=>Dispatcher.BeginInvoke(()=>{ShowMain();OpenSettings();}));menu.Items.Add(I18n.T("종료","退出"),null,(s,e)=>Dispatcher.Invoke(ExitApplication));
        using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/boss-monitor.ico")).Stream;
        tray=new Forms.NotifyIcon {Icon=new System.Drawing.Icon(stream),Text="Tarkov Boss Monitor",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(ShowMain);
        tray.BalloonTipClicked+=(s,e)=>Dispatcher.Invoke(ShowMain);
        StateChanged += (s, e) =>
        {
            if (WindowState != WindowState.Minimized)
                restoreWindowState = WindowState;
        };
        timer.Tick+=async(s,e)=>await Tick();
        Loaded+=async(s,e)=>
        {
            timer.Start();await Tick();FitWindowToContent();
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
    private DockPanel CreateMonitorFooter()
    {
        var footer = new DockPanel();
        var supportLabel = I18n.T("개발자에게 후원하기", "赞助开发者");
        var ink = Theme.Color("#563200");
        var cup = new System.Windows.Shapes.Path
        {
            // The heart above the cup stays readable in the small footer button.
            Data = Geometry.Parse("M 4,10 H 17 V 14 A 5,5 0 0 1 12,19 H 9 A 5,5 0 0 1 4,14 Z M 17,11 H 19 A 2.5,2.5 0 0 1 19,16 H 17 M 3,22 H 19 M 10.5,7 C 4,3 8,0 10.5,3 C 13,0 17,3 10.5,7 Z"),
            Stroke = ink,
            StrokeThickness = 1.7,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
            Width = 21,
            Height = 21,
        };
        var caption = new TextBlock
        {
            Text = supportLabel,
            Foreground = ink,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var reveal = new Border { Child = caption, Width = 0, ClipToBounds = true };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(reveal);
        content.Children.Add(cup);
        var supportStyle = new Style(typeof(Button), Application.Current.TryFindResource(typeof(Button)) as Style);
        supportStyle.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Color("#F6C35E")));
        var supportHover = new Trigger { Property = IsMouseOverProperty, Value = true };
        supportHover.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Color("#EDAA3E")));
        supportStyle.Triggers.Add(supportHover);
        var support = new Button
        {
            Content = content,
            Style = supportStyle,
            Padding = new Thickness(9, 6, 9, 6),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        System.Windows.Automation.AutomationProperties.SetName(support, supportLabel);
        void AnimateCaption(double width)
        {
            var duration = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 160 : 0);
            reveal.BeginAnimation(WidthProperty, new DoubleAnimation(reveal.Width, width, duration));
        }
        support.MouseEnter += (s, e) =>
        {
            caption.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AnimateCaption(caption.DesiredSize.Width);
        };
        support.MouseLeave += (s, e) => AnimateCaption(0);
        support.Click += (s, e) => OpenSupportPage();
        DockPanel.SetDock(support, Dock.Right);
        footer.Children.Add(support);

        var actions = new WrapPanel();
        pause.Click += (s, e) => TogglePause();
        actions.Children.Add(pause);
        actions.Children.Add(Theme.Action(I18n.T("설정", "设置"), (s, e) => OpenSettings()));
        actions.Children.Add(Theme.Action(I18n.T("트레이로", "最小化到托盘"), (s, e) => HideToTray()));
        footer.Children.Add(actions);
        return footer;
    }

    private void OpenSupportPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SupportPageUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(this, I18n.T("후원 페이지를 열 수 없습니다.", "无法打开赞助页面。"), "Tarkov Boss Monitor");
        }
    }

    private Grid CreateRaidHeader()
    {
        var header=new Grid();
        header.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        header.ColumnDefinitions.Add(new() {Width=GridLength.Auto});
        var details=new StackPanel();details.Children.Add(map);details.Children.Add(raidLabel);header.Children.Add(details);
        var kills=new StackPanel {MinWidth=64,HorizontalAlignment=HorizontalAlignment.Right};
        kills.Children.Add(playerKillsCaption);kills.Children.Add(playerKills);
        killsBadge.Child=kills;Grid.SetColumn(killsBadge,1);header.Children.Add(killsBadge);
        return header;
    }
    private void BuildRaidColumns()
    {
        raidColumns.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        raidColumns.ColumnDefinitions.Add(new() {Width=new GridLength(1,GridUnitType.Star)});
        var bossColumn=MonitorColumn(I18n.T("보스","首领"),bosses,new Thickness(0,0,8,0));
        eventSection.Children.Add(CompactLabel(I18n.T("이벤트","活动"),16,Theme.Text));
        eventSection.Children.Add(eventMobs);bossColumn.Children.Add(eventSection);
        var pmcColumn=MonitorColumn(PmcTitle,pmcs,new Thickness(8,0,0,0));Grid.SetColumn(pmcColumn,1);
        raidColumns.Children.Add(bossColumn);raidColumns.Children.Add(pmcColumn);
    }
    private static StackPanel MonitorColumn(string title,StackPanel panel,Thickness margin)
    {
        var column=new StackPanel {Margin=margin};
        column.Children.Add(CompactLabel(title,16,Theme.Text));
        column.Children.Add(panel);
        return column;
    }
    private static TextBlock CompactLabel(string text,double size=13,Brush? color=null)=>new()
    {
        Text=text,FontSize=size,FontWeight=size>=16?FontWeights.SemiBold:FontWeights.Normal,
        Foreground=color??Theme.Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,5)
    };
    private const double InactiveCardOpacity=0.65;
    // Unknown/Planned and death-unconfirmed instances must stay prominent.
    // An actual activation takes precedence over an earlier spawn decision.
    private static bool IsInactiveCard(string status,List<BossInstance> instances)=>instances.Count>0
        ? instances.All(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null)
        : status=="NotPlanned";
    private static Border MonitorCard(UIElement content,Brush accent,bool inactive=false)=>new()
    {
        Background=Theme.Card,BorderBrush=accent,BorderThickness=new Thickness(3,0,0,0),
        CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,8),Child=content,
        Opacity=inactive?InactiveCardOpacity:1
    };
    private Border HighlightCard(Border card,string key)
    {
        var now=DateTimeOffset.UtcNow;
        var change=cardChanges?.Recent(key,now);
        if(change==null)return card;
        var age=now-change.At;
        double strength=1-Math.Clamp((age-CardChangeTracker.HoldDuration).TotalSeconds/(CardChangeTracker.Duration-CardChangeTracker.HoldDuration).TotalSeconds,0,1);
        var remaining=CardChangeTracker.Duration-age;
        var hold=age<CardChangeTracker.HoldDuration?CardChangeTracker.HoldDuration-age:TimeSpan.Zero;
        DoubleAnimationUsingKeyFrames Fade(double highlighted,double normal)
        {
            var animation=new DoubleAnimationUsingKeyFrames {FillBehavior=FillBehavior.Stop};
            double current=normal+(highlighted-normal)*strength;
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(current,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if(hold>TimeSpan.Zero)animation.KeyFrames.Add(new LinearDoubleKeyFrame(highlighted,KeyTime.FromTimeSpan(hold)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(normal,KeyTime.FromTimeSpan(remaining)));
            return animation;
        }
        Color color=(Color)ColorConverter.ConvertFromString(change.Kind switch
        {
            CardChangeTracker.ChangeKind.Activated=>"#2E9B70",
            CardChangeTracker.ChangeKind.Planned=>"#BD8431",
            CardChangeTracker.ChangeKind.Death=>"#9E4038",
            _=>"#71857B"
        });
        // Hold a distinct inset rail and full-width wash before fading. Keep
        // text/layout stable and preserve the infected card's Halloween skin.
        var wash=new Border
        {
            CornerRadius=new CornerRadius(6),IsHitTestVisible=false,
            Background=new LinearGradientBrush(Color.FromArgb(95,color.R,color.G,color.B),Color.FromArgb(25,color.R,color.G,color.B),0),
            BorderBrush=new SolidColorBrush(color),BorderThickness=new Thickness(4,1,1,1),Opacity=0
        };
        var content=card.Child;card.Child=null;
        var surface=new Grid();surface.Children.Add(wash);surface.Children.Add(content);card.Child=surface;
        wash.BeginAnimation(OpacityProperty,Fade(1,0));
        if(card.BorderBrush is SolidColorBrush original)
        {
            var accent=new SolidColorBrush(original.Color);card.BorderBrush=accent;
            Color start=Color.FromRgb(
                (byte)(original.Color.R+(color.R-original.Color.R)*strength),
                (byte)(original.Color.G+(color.G-original.Color.G)*strength),
                (byte)(original.Color.B+(color.B-original.Color.B)*strength));
            var animation=new ColorAnimationUsingKeyFrames {FillBehavior=FillBehavior.Stop};
            animation.KeyFrames.Add(new LinearColorKeyFrame(start,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            if(hold>TimeSpan.Zero)animation.KeyFrames.Add(new LinearColorKeyFrame(color,KeyTime.FromTimeSpan(hold)));
            animation.KeyFrames.Add(new LinearColorKeyFrame(original.Color,KeyTime.FromTimeSpan(remaining)));
            accent.BeginAnimation(SolidColorBrush.ColorProperty,animation);
        }
        if(card.Opacity<1)
            card.BeginAnimation(OpacityProperty,Fade(1,card.Opacity));
        return card;
    }
    private static void ColorDeathText(TextBlock label,int dead,bool halloween=false)
    {
        string text=label.Text;
        int start=text.IndexOf(I18n.T("사망 확인","已确认死亡"),StringComparison.Ordinal);
        if(start<0)return;
        label.Text="";
        if(start>0)label.Inlines.Add(new System.Windows.Documents.Run(text[..start]));
        label.Inlines.Add(new System.Windows.Documents.Run(text[start..])
        {
            Foreground=dead>0?Theme.Color(halloween?"#F3A195":"#9E4038")
                :halloween?Theme.Color("#C4AFA5"):Theme.Muted
        });
    }
    private static void AddLifeSummary(StackPanel content,int alive,int dead,bool ended,bool halloween=false)
    {
        string life=ended
            ? $"{I18n.T("종료 시 사망 미확인","结束时未确认死亡")} {alive}{I18n.T("명","个")} · {I18n.T("사망 확인","已确认死亡")} {dead}{I18n.T("명","个")}"
            : $"{I18n.T("생존 추정","推测存活")} {alive}{I18n.T("명","个")} · {I18n.T("사망 확인","已确认死亡")} {dead}{I18n.T("명","个")}";
        var label=CompactLabel(life,15,halloween?(alive>0?Theme.Color("#FFD09B"):Theme.Color("#C4AFA5")):alive>0?Theme.Green:Theme.Muted);
        label.FontWeight=FontWeights.SemiBold;ColorDeathText(label,dead,halloween);content.Children.Add(label);
    }
    private void FitWindowToContent()
    {
        if(!IsLoaded||!IsVisible||subpage||WindowState!=WindowState.Normal)return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible || subpage || WindowState != WindowState.Normal) return;
            monitorScroll.UpdateLayout();
            double chrome=ActualHeight-monitorScroll.ViewportHeight;
            if(double.IsNaN(chrome)||chrome<=0)return;
            if (!WindowPlacement.TryGetWorkArea(this, out var work)) return;
            double target = Math.Clamp(chrome + monitorScroll.ExtentHeight + 4, MinHeight, Math.Max(MinHeight, work.Height - 24));
            if (Math.Abs(Height - target) <= 1) return;
            ResizeWithinWorkArea(Width, target, work);
        },DispatcherPriority.Loaded);
    }

    private void ResizeWithinWorkArea(double width, double height, Rect workArea)
    {
        var bounds = new Rect(Left, Top, Width, Height);
        var fitted = WindowPlacement.ResizeWithinWorkArea(bounds, new Size(width, height), workArea);
        if (fitted.Left != bounds.Left) Left = fitted.Left;
        if (fitted.Top != bounds.Top) Top = fitted.Top;
        Width = width;
        Height = height;
    }

    internal void ShowMain()
    {
        if (closed) return;
        bool wasHidden = !IsVisible;
        var state = WindowState == WindowState.Minimized ? restoreWindowState : WindowState;
        if (wasHidden) Show();
        if (WindowState != state) WindowState = state;

        var handle = new WindowInteropHelper(this).Handle;
        if (WindowState == WindowState.Normal && WindowPlacement.TryGetWorkArea(this, out var work))
        {
            var bounds = new Rect(Left, Top, Width, Height);
            var recovered = WindowPlacement.RecoverOffscreen(bounds, work);
            if (recovered.Left != bounds.Left) Left = recovered.Left;
            if (recovered.Top != bounds.Top) Top = recovered.Top;
        }

        if (!Activate()) WindowActivation.FlashTaskbar(handle);
        if (wasHidden) FitWindowToContent();
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
        if (WindowState == WindowState.Normal && WindowPlacement.TryGetWorkArea(this, out var work))
        {
            double width = Math.Max(Width, Math.Min(DefaultWindowWidth, Math.Max(MinWidth, work.Width - 24)));
            double height = Math.Max(Height, Math.Min(DefaultWindowHeight, Math.Max(MinHeight, work.Height - 24)));
            ResizeWithinWorkArea(width, height, work);
        }
        view.Completed+=saved=>
        {
            if(saved)
            {
                settings=view.Settings;reader.Reset();rendering="";retryRoot="";lastConfig=DateTime.MinValue;
                if(!settings.CheckForUpdates)updateBanner.Visibility=Visibility.Collapsed;
                else {nextUpdateCheckUtc=DateTime.MinValue;_ = CheckForUpdates();}
            }
            settingsView=null;ReturnToMonitor();Render(DisplayRaid(reader.Current));
        };
    }
    private void ReturnToMonitor(){subpage=false;Content=monitor;Title="Tarkov Boss Monitor";Topmost=settings.AlwaysOnTop;FitWindowToContent();}
    private async Task Tick()
    {
        if(settings.CheckForUpdates && DateTime.UtcNow>=nextUpdateCheckUtc)_ = CheckForUpdates();
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
            SetStatus(Paths.LoggingReady(settings.GamePath)?"":I18n.T("로그 설정 확인 필요","需要检查日志设置"));
            Render(current);
            NotifyBosses(current);
        }
        catch(System.ComponentModel.Win32Exception){configuring=false;retryRoot=settings.GamePath;SetStatus(I18n.T("설정 적용 취소됨","已取消应用设置"));}
        catch(Exception ex){configuring=false;SetStatus(I18n.T("감시 오류: ","监控错误：")+ex.Message);}
        finally {busy=false;}
    }
    private async Task CheckForUpdates()
    {
        if(checkingForUpdates||closed||!settings.CheckForUpdates)return;
        checkingForUpdates=true;nextUpdateCheckUtc=DateTime.UtcNow.AddDays(1);
        try
        {
            var update=await UpdateChecker.CheckAsync();
            if(update==null||closed||!settings.CheckForUpdates)return;
            string message=string.Format(I18n.T("새 버전 {0}을 사용할 수 있습니다.","新版本 {0} 可用。"),update.Tag);
            var content=new DockPanel();
            var open=Theme.Action(I18n.T("릴리스 페이지 열기","打开发布页面"),(s,e)=>OpenReleasePage());DockPanel.SetDock(open,Dock.Right);content.Children.Add(open);
            content.Children.Add(Theme.Label(message,14,Theme.Green));updateBanner.Child=content;updateBanner.Visibility=Visibility.Visible;FitWindowToContent();
            if(!updateNotified)
            {
                updateNotified=true;
                tray.ShowBalloonTip(6000,I18n.T("새 버전 사용 가능","有新版本可用"),message+I18n.T(" 알림을 클릭하면 창을 다시 엽니다."," 点击通知可重新打开窗口。"),Forms.ToolTipIcon.Info);
            }
        }
        catch(Exception){}
        finally{checkingForUpdates=false;}
    }
    private static void OpenReleasePage()
    {
        try{Process.Start(new ProcessStartInfo(UpdateChecker.ReleasePage){UseShellExecute=true});}catch{}
    }
    private static RaidState DisplayRaid(RaidState raid)
        => raid.Ended && (raid.EndedAt==null || DateTime.Now-raid.EndedAt>EndedRaidDisplayTime) ? new() : raid;
    private void RenderRaidHeader(RaidState raid)
    {
        bool hasRaid=raid.Map!="";
        map.Text=hasRaid?Catalog.MapName(raid.Map):I18n.T("레이드 감지 대기","等待检测战局");
        raidLabel.Text=hasRaid?(raid.Ended?I18n.T("종료된 레이드","已结束战局"):I18n.T("현재 레이드","当前战局")):"";
        raidLabel.Visibility=hasRaid?Visibility.Visible:Visibility.Collapsed;
        playerKillsCaption.Text=I18n.T("내 처치","我的击杀");
        playerKills.Text=hasRaid?raid.PlayerKills.ToString():"";
        killsBadge.Visibility=hasRaid?Visibility.Visible:Visibility.Collapsed;
    }
    private void Render(RaidState raid)
    {
        (cardChanges??=new()).Observe(raid,DateTimeOffset.UtcNow);
        // Kill counts can change without changes to any tracked enemy card.
        RenderRaidHeader(raid);
        var filter=settings.Filter(raid.Map);
        var roles=filter.Mode=="selected"?filter.Roles:raid.Bosses.Keys.ToList();
        if(filter.Mode=="off"||raid.Map=="")roles=[];
        roles=roles.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role=>StatusOrder(raid.Bosses.GetValueOrDefault(role)?.Status))
            .ThenBy(role=>Catalog.BossName(role,settings),StringComparer.CurrentCultureIgnoreCase).ToList();
        var btrLocation=BtrTracker.Estimate(raid,DateTime.Now);
        string signature=raid.Key+raid.Ended+paused+filter.Mode+string.Join("|",roles.Select(r=>r+System.Text.Json.JsonSerializer.Serialize(raid.Bosses.GetValueOrDefault(r))))+System.Text.Json.JsonSerializer.Serialize(raid.Followers)+System.Text.Json.JsonSerializer.Serialize(raid.Pmcs)+System.Text.Json.JsonSerializer.Serialize(raid.SpecialAis)+System.Text.Json.JsonSerializer.Serialize(raid.Scavs)+System.Text.Json.JsonSerializer.Serialize(raid.Btr)+System.Text.Json.JsonSerializer.Serialize(raid.BtrReadyAt)+System.Text.Json.JsonSerializer.Serialize(raid.BtrTaxiAt)+btrLocation?.Key;
        // HALLOWEEN_EVENT_BEGIN
        signature+=System.Text.Json.JsonSerializer.Serialize(raid.InfectedScreamer);
        signature+=System.Text.Json.JsonSerializer.Serialize(raid.InfectedTagilla);
        // HALLOWEEN_EVENT_END
        if(signature==rendering)return;rendering=signature;bosses.Children.Clear();pmcs.Children.Clear();eventMobs.Children.Clear();eventSection.Visibility=Visibility.Collapsed;waiting.Children.Clear();
        if(raid.Map=="")
        {
            raidColumns.Visibility=Visibility.Collapsed;waiting.Visibility=Visibility.Visible;RenderWaiting(waiting);FitWindowToContent();return;
        }
        waiting.Visibility=Visibility.Collapsed;raidColumns.Visibility=Visibility.Visible;
        RenderPmcs(raid);
        RenderSpecialAis(raid);
        RenderScavs(raid);
        RenderEventMobs(raid);
        RenderBtr(raid,btrLocation);
        if(filter.Mode=="off"){bosses.Children.Add(Theme.Label(I18n.T("이 맵은 감시하지 않도록 설정되어 있습니다.","此地图已设置为不监控。"),16,Theme.Muted));FitWindowToContent();return;}
        if(roles.Count==0){bosses.Children.Add(Theme.Label(filter.Mode=="selected"?I18n.T("선택된 보스가 없습니다. 설정에서 보스를 선택해 주세요.","未选择首领，请在设置中选择。"):I18n.T("모든 보스 감시 중 · 보스 판정 기록 대기","正在监控所有首领 · 等待判定记录"),16,Theme.Muted));FitWindowToContent();return;}
        foreach(string role in roles)
        {
            var state=raid.Bosses.GetValueOrDefault(role)??new BossState {Role=role};
            bool allDead=state.Status=="Confirmed" && state.LifeInstances.Count>0 && state.LifeInstances.All(i=>i.LifeStatus=="DeadConfirmed");
            Brush accent=allDead?Theme.Muted:state.Status=="Confirmed"?Theme.Green:state.Status=="Planned"?Theme.Amber:Theme.Muted;
            var content=new StackPanel {Margin=new Thickness(14,8,14,7)};
            content.Children.Add(CompactLabel(Catalog.BossName(role,settings),18));
            if(state.Status=="Confirmed")
            {
                var lifeLabel=CompactLabel(Helpers.LifeStatus(state,raid.Ended),15,allDead?Theme.Muted:Theme.Green);
                ColorDeathText(lifeLabel,state.LifeInstances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null));
                lifeLabel.FontWeight=FontWeights.SemiBold;content.Children.Add(lifeLabel);
            }
            else content.Children.Add(CompactLabel(Helpers.Status(state.Status)+(raid.Ended?I18n.T(" · 레이드 종료"," · 战局已结束"):""),15,accent));
            if(state.SpawnChance is int chance)content.Children.Add(CompactLabel($"{I18n.T("스폰 확률", "刷新概率")} {chance}%",13,Theme.Muted));
            if(state.At!=null)content.Children.Add(CompactLabel($"{I18n.T("기록", "记录")} {state.At:HH:mm:ss}",12,Theme.Muted));
            if(state.Status=="Confirmed")
            {
                var followers=raid.Followers.Values.Where(f=>Catalog.FollowerParent(f.Role).Equals(role,StringComparison.OrdinalIgnoreCase)).SelectMany(f=>f.LifeInstances).ToList();
                if(followers.Count>0)
                {
                    int followerDead=followers.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
                    int followerAlive=followers.Count-followerDead;
                    string followerLife=raid.Ended?I18n.T("사망 미확인","死亡未确认"):I18n.T("생존 추정","推测存活");
                    var followerLabel=CompactLabel($"{I18n.T("쫄","随从")} {followers.Count}{I18n.T("명","个")} · {followerLife} {followerAlive}{I18n.T("명","个")} · {I18n.T("사망 확인","已确认死亡")} {followerDead}{I18n.T("명","个")}",13,followerAlive>0?Theme.Green:Theme.Muted);
                    ColorDeathText(followerLabel,followerDead);content.Children.Add(followerLabel);
                }
                if(state.LifeInstances.Count>1)
                    foreach(var instance in state.LifeInstances.Where(i=>i.DiedAt!=null))
                    {
                        var deathLabel=CompactLabel($"{I18n.T("개체", "个体")} {instance.BotId} · {I18n.T("사망 확인", "已确认死亡")} {instance.DiedAt:HH:mm:ss}",12,Theme.Muted);
                        ColorDeathText(deathLabel,1);content.Children.Add(deathLabel);
                    }
                string zoneDisplay=state.Zone;
                if(I18n.IsChinese && state.Position!="")
                {
                    string poi=MapGeo.DescribeLocation(raid.Map,state.Position);
                    if(poi!="")zoneDisplay=poi;
                }
                content.Children.Add(CompactLabel($"{I18n.T("최초 구역", "首次区域")}: {(zoneDisplay==""?I18n.T("확인 불가","无法确认"):zoneDisplay)}\n{I18n.T("최초 좌표", "首次坐标")}: {(state.Position==""?I18n.T("확인 불가","无法确认"):state.Position)}",12));
                foreach(var instance in state.LifeInstances.Where(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null))
                {
                    string identity=state.Count>1?$"{I18n.T("개체", "个体")} {instance.BotId} · ":"";
                    string position=instance.DeathPosition==""?I18n.T("확인 불가","无法确认"):instance.DeathPosition;
                    content.Children.Add(CompactLabel($"{identity}{I18n.T("사망 좌표", "死亡坐标")}: {position}",12));
                }
                if(state.Count>1)content.Children.Add(CompactLabel($"{I18n.T("활성화 개체", "已激活个体")} {state.Count}",12,Theme.Muted));
            }
            bosses.Children.Add(HighlightCard(MonitorCard(content,accent,IsInactiveCard(state.Status,state.LifeInstances)),$"boss:{role}"));
        }
        FitWindowToContent();
    }
    private static int StatusOrder(string? status)=>status switch {"Confirmed"=>0,"Planned"=>1,"NotPlanned"=>2,_=>3};
    private void RenderPmcs(RaidState raid)
    {
        var pmc = PmcCardSummary.From(raid);
        if (pmc.Count == 0 && !pmc.HasWaveRecords)
        {
            pmcs.Children.Add(Theme.Label(I18n.T("AI PMC 웨이브 기록 대기", "等待 AI PMC 波次记录"), 16, Theme.Muted));
            return;
        }

        Brush accent = pmc.Status == "Planned" ? Theme.Amber : pmc.Alive > 0 ? Theme.Green : Theme.Muted;
        var content = new StackPanel { Margin = new Thickness(14, 8, 14, 7) };
        content.Children.Add(CompactLabel(PmcTitle, 18));
        if (pmc.Count > 0)
        {
            AddLifeSummary(content, pmc.Alive, pmc.Dead, raid.Ended);
            // PMC_DIFFICULTY_BEGIN: estimated surviving impossible-difficulty PMCs.
            AddPmcDifficultySummary(content, pmc.Impossible, pmc.Alive, raid.Ended);
            // PMC_DIFFICULTY_END
        }
        else
        {
            string status = pmc.Status == "Unknown" ? I18n.T("스폰 미확인", "未检测到刷新") : Helpers.Status(pmc.Status);
            string ended = raid.Ended ? I18n.T(" · 레이드 종료", " · 战局已结束") : "";
            content.Children.Add(CompactLabel(status + ended, 15, accent));
        }

        bool additionalPlanned = !raid.Ended && pmc.AdditionalSpawnPlanned;
        if (additionalPlanned)
            content.Children.Add(CompactLabel(I18n.T("추가 스폰 예정", "追加刷新已计划"), 13, Theme.Amber));
        if (pmc.FirstSpawnedAt is DateTime firstSpawnedAt)
            content.Children.Add(CompactLabel($"{I18n.T("첫 활성화", "首次激活")} {firstSpawnedAt:HH:mm:ss}", 12));
        bool inactive = !additionalPlanned && (pmc.Status == "NotPlanned" || (pmc.Count > 0 && pmc.Alive == 0));
        pmcs.Children.Add(HighlightCard(MonitorCard(content, accent, inactive), PmcCardSummary.CardKey));
    }
    private static void AddPmcDifficultySummary(StackPanel content,int impossible,int alive,bool ended)
    {
        if(impossible==0)return;
        if(ended)
        {
            string format=I18n.T("Impossible · 종료 시 사망 미확인 {0}명","Impossible · 结束时死亡未确认 {0} 人");
            content.Children.Add(CompactLabel(string.Format(format,impossible),13,Theme.Muted));
            return;
        }

        var warning=new StackPanel();
        var title=CompactLabel(string.Format(I18n.T("Impossible {0}명","Impossible {0} 人"),impossible),15,Theme.Color("#9E4038"));
        title.FontWeight=FontWeights.SemiBold;
        title.Margin=new Thickness(0,0,0,3);
        warning.Children.Add(title);
        var subset=CompactLabel(string.Format(I18n.T("전체 생존 추정 {0}명에 포함","已计入推测存活的 {0} 人"),alive),12,Theme.Muted);
        subset.Margin=new Thickness(0);
        warning.Children.Add(subset);
        content.Children.Add(new Border
        {
            Background=Theme.Color("#FBEEEA"),BorderBrush=Theme.Color("#DFA79E"),
            BorderThickness=new Thickness(3,1,1,1),CornerRadius=new CornerRadius(5),
            Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,5,0,8),Child=warning
        });
    }
    private void RenderScavs(RaidState raid)
    {
        if(Catalog.CanonicalMapId(raid.Map).Equals("laboratory",StringComparison.OrdinalIgnoreCase))return;
        var state=raid.Scavs;
        int dead=state.LifeInstances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
        int alive=state.Count-dead;
        string name=I18n.T("스캐브","Scav");
        var title=CompactLabel(name,16,Theme.Text);title.Margin=new Thickness(0,10,0,5);pmcs.Children.Add(title);
        var content=new StackPanel {Margin=new Thickness(14,8,14,7)};
        content.Children.Add(CompactLabel(name,18));
        if(state.Count==0)content.Children.Add(CompactLabel(I18n.T("스폰 미확인","未检测到刷新"),14,Theme.Muted));
        AddLifeSummary(content,alive,dead,raid.Ended);
        if(state.At!=null)content.Children.Add(CompactLabel($"{I18n.T("첫 활성화","首次激活")} {state.At:HH:mm:ss}",12));
        pmcs.Children.Add(HighlightCard(MonitorCard(content,alive>0?Theme.Green:Theme.Muted,IsInactiveCard(state.Status,state.LifeInstances)),"scavs"));
    }
    private void RenderEventMobs(RaidState raid)
    {
        // All seasonal/event-only enemies belong in the left Events section below
        // bosses. Add future event mobs here, not to RenderPmcs/RenderSpecialAis.
        // Map eligibility belongs here; the shared card only renders life state.
        if(!Catalog.CanonicalMapId(raid.Map).Equals("laboratory",StringComparison.OrdinalIgnoreCase))
            RenderEventMobCard(raid,raid.InfectedScreamer,I18n.T("감염자","感染者"),halloween:true);
        // Show event bosses when a plan or an actual activation is recorded;
        // their presence is evidence-based rather than a fixed map assumption.
        if(raid.InfectedTagilla.Status!="Unknown" || raid.InfectedTagilla.Count>0)
            RenderEventMobCard(raid,raid.InfectedTagilla,I18n.T("감염된 타길라","感染版塔吉拉"));
        eventSection.Visibility=eventMobs.Children.Count>0?Visibility.Visible:Visibility.Collapsed;
    }
    private void RenderEventMobCard(RaidState raid,BossState state,string name,bool halloween=false)
    {
        int dead=state.LifeInstances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
        int alive=state.Count-dead;
        Brush text=halloween?Theme.Color("#F3E4D7"):Theme.Text;
        Brush muted=halloween?Theme.Color("#C4AFA5"):Theme.Muted;
        var content=new StackPanel {Margin=new Thickness(14,8,14,7)};
        if(halloween)
        {
            var heading=new DockPanel {Margin=new Thickness(0,2,0,2)};
            var pumpkin=new Image {Source=HalloweenPumpkin(),Width=30,Height=30,Margin=new Thickness(10,0,0,0),IsHitTestVisible=false};
            DockPanel.SetDock(pumpkin,Dock.Right);heading.Children.Add(pumpkin);
            var title=new StackPanel {Orientation=Orientation.Horizontal};
            title.Children.Add(CompactLabel(name,18,Theme.Color("#FFBA79")));
            title.Children.Add(new Border {Background=Theme.Color("#493023"),CornerRadius=new CornerRadius(3),Padding=new Thickness(6,2,6,2),Margin=new Thickness(9,0,0,3),VerticalAlignment=VerticalAlignment.Center,
                Child=new TextBlock {Text=I18n.T("할로윈","万圣节"),FontSize=11,Foreground=Theme.Color("#F2B177")}});
            heading.Children.Add(title);content.Children.Add(heading);
        }
        else content.Children.Add(CompactLabel(name,18));
        bool hasDecision=state.Status is "Planned" or "NotPlanned";
        if(state.Count==0)content.Children.Add(CompactLabel(hasDecision?Helpers.Status(state.Status):I18n.T("스폰 미확인","未检测到刷新"),14,state.Status=="Planned"?Theme.Amber:muted));
        if(state.Count>0 || !hasDecision)
        {
            AddLifeSummary(content,alive,dead,raid.Ended,halloween);
        }
        if(state.SpawnChance is int chance)content.Children.Add(CompactLabel($"{I18n.T("스폰 확률","刷新概率")} {chance}%",13,muted));
        if(state.At!=null)content.Children.Add(CompactLabel($"{(state.Count>0?I18n.T("첫 활성화","首次激活"):I18n.T("기록","记录"))} {state.At:HH:mm:ss}",12,text));
        bool inactive=IsInactiveCard(state.Status,state.LifeInstances);
        if(!halloween){eventMobs.Children.Add(HighlightCard(MonitorCard(content,alive>0?Theme.Green:state.Status=="Planned"?Theme.Amber:Theme.Muted,inactive),$"event:{state.Role}"));return;}
        // Opt-in skin for infected only: never alter Theme or other event cards.
        var surface=new Grid();
        surface.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M 90,0 L 0,70 M 90,0 L 35,70 M 90,0 L 0,18 M 90,0 L 90,70 M 67,0 Q 67,17 90,19 M 43,0 Q 43,34 90,39 M 18,0 Q 18,52 90,60"),
            Stroke=Theme.Color("#C18B64"),StrokeThickness=1,Opacity=0.13,Width=90,Height=70,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false});
        surface.Children.Add(content);
        eventMobs.Children.Add(HighlightCard(new Border {Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#302027"),(Color)ColorConverter.ConvertFromString("#1B2226"),35),
            BorderBrush=Theme.Color("#9C5834"),BorderThickness=new Thickness(3,1,1,1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,8),Child=surface,
            Opacity=inactive?InactiveCardOpacity:1},$"event:{state.Role}"));
    }
    private static ImageSource HalloweenPumpkin()
    {
        var drawing=new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Theme.Color("#8C9560"),null,Geometry.Parse("M 14,8 L 14,2 L 19,0 L 17,8 Z")));
        drawing.Children.Add(new GeometryDrawing(Theme.Color("#E9893F"),new Pen(Theme.Color("#FFB66D"),0.7),Geometry.Parse("M 16,8 C 3,1 0,15 4,24 C 7,31 24,31 28,24 C 33,14 28,2 16,8 Z")));
        drawing.Children.Add(new GeometryDrawing(null,new Pen(Theme.Color("#AB582E"),0.8),Geometry.Parse("M 12,9 C 8,14 8,24 12,28 M 20,9 C 24,14 24,24 20,28")));
        drawing.Children.Add(new GeometryDrawing(Theme.Color("#302027"),null,Geometry.Parse("M 7,14 L 13,17 L 6,18 Z M 25,14 L 19,17 L 26,18 Z M 15,18 L 17,18 L 16,21 Z M 7,22 L 12,24 L 14,22 L 17,25 L 20,22 L 25,21 L 22,27 L 11,27 Z")));
        drawing.Freeze();return new DrawingImage(drawing);
    }
    private void RenderSpecialAis(RaidState raid)
    {
        var roles=Catalog.SpecialAiRolesForMap(raid.Map)
            .Concat(raid.SpecialAis.Where(s=>s.Value.Status=="Confirmed").Select(s=>s.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(roles.Count==0)return;
        var states=roles
            .Select((role,index)=>(State:raid.SpecialAis.GetValueOrDefault(role)??new BossState {Role=role},Index:index))
            .OrderBy(item=>item.State.Status=="Confirmed"?0:1)
            .ThenBy(item=>item.Index)
            .Select(item=>item.State).ToList();
        var title=CompactLabel(string.Join(" · ",states.Select(s=>Catalog.SpecialAiName(s.Role))),16,Theme.Text);title.Margin=new Thickness(0,10,0,5);pmcs.Children.Add(title);
        foreach(var state in states)
        {
            bool confirmed=state.Status=="Confirmed";
            int dead=state.LifeInstances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
            int alive=state.Count-dead;
            var content=new StackPanel {Margin=new Thickness(14,8,14,7)};
            content.Children.Add(CompactLabel(Catalog.SpecialAiName(state.Role),18));
            if(confirmed)
            {
                AddLifeSummary(content,alive,dead,raid.Ended);
                if(state.At!=null)content.Children.Add(CompactLabel($"{I18n.T("첫 활성화","首次激活")} {state.At:HH:mm:ss}",12));
                var first=state.LifeInstances.OrderBy(i=>i.SpawnedAt).FirstOrDefault();
                if(first!=null)
                {
                    string zone=FirstSpawnZone(raid.Map,first.Zone,first.Position);
                    string position=first.Position==""?I18n.T("확인 불가","无法确认"):first.Position;
                    content.Children.Add(CompactLabel($"{I18n.T("최초 구역","首次区域")}: {zone} · {I18n.T("최초 좌표","首次坐标")}: {position}",12));
                }
            }
            else content.Children.Add(CompactLabel(I18n.T("스폰 미확인","未检测到刷新"),14,Theme.Muted));
            pmcs.Children.Add(HighlightCard(MonitorCard(content,confirmed&&alive>0?Theme.Green:Theme.Muted,IsInactiveCard(state.Status,state.LifeInstances)),$"special:{state.Role}"));
        }
    }
    // MapGeo POI names are Simplified Chinese, so only substitute them for Chinese users.
    private static string FirstSpawnZone(string map,string zone,string position)
    {
        if(I18n.IsChinese && position!="")
        {
            string poi=MapGeo.DescribeLocation(map,position);
            if(poi!="")return poi;
        }
        return zone==""?I18n.T("확인 불가","无法确认"):zone;
    }
    private void RenderBtr(RaidState raid,BtrLocationEstimate? location)
    {
        if(raid.Btr is not BossState state || state.Status!="Confirmed")return;
        var title=CompactLabel("BTR",16,Theme.Text);title.Margin=new Thickness(0,10,0,5);pmcs.Children.Add(title);
        var content=new StackPanel {Margin=new Thickness(14,8,14,7)};
        content.Children.Add(CompactLabel("BTR",18));
        content.Children.Add(CompactLabel(Helpers.Status(state.Status)+(raid.Ended?I18n.T(" · 레이드 종료"," · 战局已结束"):""),15,Theme.Green));
        if(location!=null)
        {
            content.Children.Add(CompactLabel(location.Summary,15,raid.Ended?Theme.Muted:Theme.Amber));
            if(location.Detail!="")content.Children.Add(CompactLabel(location.Detail,12,Theme.Muted));
        }
        if(state.At!=null)content.Children.Add(CompactLabel($"{I18n.T("기록", "记录")} {state.At:HH:mm:ss}",12,Theme.Muted));
        content.Children.Add(CompactLabel($"{I18n.T("최초 구역", "首次区域")}: {FirstSpawnZone(raid.Map,state.Zone,state.Position)}",12));
        content.Children.Add(CompactLabel($"{I18n.T("최초 좌표", "首次坐标")}: {(state.Position==""?I18n.T("확인 불가","无法确认"):state.Position)}",12));
        pmcs.Children.Add(MonitorCard(content,Theme.Green));
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
