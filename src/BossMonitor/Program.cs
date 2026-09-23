namespace BossMonitor;
internal static class Program
{
    private const string SingletonName=@"Local\TarkovBossMonitor-GUI";
    private const string ActivationEventName=@"Local\TarkovBossMonitor-Activate";

    [STAThread] private static void Main()
    {
        try
        {
        using var singleton=new Mutex(true,SingletonName,out bool created);
        if(!created){ActivateExistingInstance();return;}
        using var activationEvent=new EventWaitHandle(false,EventResetMode.AutoReset,ActivationEventName);
        try { File.Delete(Path.Combine(Product.UserDir,"history.json")); } catch { }
        var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose};
        app.DispatcherUnhandledException+=(s,e)=>{e.Handled=true;MessageBox.Show(e.Exception.Message,I18n.T("작업을 완료하지 못했습니다.","无法完成操作。"));};
        Theme.Apply(app);
        var window=new MainWindow();
        var activationRegistration=ThreadPool.RegisterWaitForSingleObject(activationEvent,(_,timedOut)=>
        {
            if(!timedOut && !app.Dispatcher.HasShutdownStarted)
                app.Dispatcher.BeginInvoke(window.ShowMain);
        },null,Timeout.Infinite,false);
        try {app.Run(window);}
        finally {activationRegistration.Unregister(null);}
        }
        catch(Exception ex)
        {
            string log=Path.Combine(Product.UserDir,"startup-error.txt");
            bool recorded=false;
            try{Directory.CreateDirectory(Product.UserDir);File.WriteAllText(log,$"{DateTime.Now:O}\n{ex}");recorded=true;}catch{}
            Forms.MessageBox.Show($"{I18n.T("프로그램을 시작하지 못했습니다.","无法启动程序。")}\n{ex.Message}"+(recorded?$"\n\n{I18n.T("오류 기록", "错误记录")}: {log}":""),I18n.T("Tarkov Boss Monitor 시작 오류","Tarkov Boss Monitor 启动错误"),Forms.MessageBoxButtons.OK,Forms.MessageBoxIcon.Error);
        }
    }

    private static void ActivateExistingInstance()
    {
        // The first process may have acquired the mutex just before creating the event.
        // Retry briefly so a rapid double-click still restores the existing window.
        for(int attempt=0;attempt<20;attempt++)
        {
            try
            {
                using var activationEvent=EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
                return;
            }
            catch(WaitHandleCannotBeOpenedException)
            {
                if(attempt==19)return;
                Thread.Sleep(50);
            }
            catch(UnauthorizedAccessException) {return;}
        }
    }
}

internal static class Theme
{
    public static readonly Brush Bg=Color("#F3F6F4"),Card=Color("#FFFFFF"),Text=Color("#192D23"),Muted=Color("#52665A"),Green=Color("#166747"),Amber=Color("#8A5100");
    public static Brush Color(string hex)=>new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
    public static void Apply(Application app)
    {
        var window=new Style(typeof(Window));
        window.Setters.Add(new Setter(Window.BackgroundProperty,Bg));window.Setters.Add(new Setter(Window.ForegroundProperty,Text));
        window.Setters.Add(new Setter(Window.FontFamilyProperty,new FontFamily(I18n.FontFamily)));window.Setters.Add(new Setter(Window.FontSizeProperty,14d));app.Resources.Add(typeof(Window),window);
        var button=new Style(typeof(Button));button.Setters.Add(new Setter(Control.BackgroundProperty,Green));button.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));
        button.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(14,8,14,8)));button.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4)));button.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));frame.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));frame.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);frame.AppendChild(presenter);
        button.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=frame}));
        var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,Color("#0F5136")));button.Triggers.Add(hover);
        var disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(Control.BackgroundProperty,Color("#DEE6E0")));disabled.Setters.Add(new Setter(Control.ForegroundProperty,Muted));button.Triggers.Add(disabled);
        // Resource registration may seal a style. Complete its setters and triggers first.
        app.Resources.Add(typeof(Button),button);
        foreach(var type in new[]{typeof(TextBox),typeof(ComboBox)})
        {
            var style=new Style(type);style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.White));style.Setters.Add(new Setter(Control.ForegroundProperty,Text));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(9)));style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,38d));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,6,0,8)));app.Resources.Add(type,style);
        }
        var check=new Style(typeof(CheckBox));check.Setters.Add(new Setter(Control.ForegroundProperty,Text));check.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4,4,12,4)));check.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));app.Resources.Add(typeof(CheckBox),check);
    }
    public static TextBlock Label(string text,double size=14,Brush? color=null)=>new() {Text=text,FontSize=Math.Max(size,13),FontWeight=size>=17?FontWeights.SemiBold:FontWeights.Normal,Foreground=color??Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};
    public static Button Action(string text,RoutedEventHandler click) {var b=new Button{Content=text};b.Click+=click;return b;}
    public static BitmapImage Icon()=>new(new Uri("pack://application:,,,/Assets/radar.png"));
    public static void WindowIcon(Window window)
    {
        window.Icon=Icon();window.Background=Bg;window.Foreground=Text;window.FontFamily=new FontFamily(I18n.FontFamily);window.FontSize=14;
    }
}

internal static class Helpers
{
    public static async Task<int> Elevated(string command,string? root=null)
    {
        var info=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"BossMonitor.SetupHelper.exe")) {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        info.ArgumentList.Add(command);if(root!=null)info.ArgumentList.Add(root);
        using var p=Process.Start(info)??throw new IOException(I18n.T("설정 프로그램을 실행하지 못했습니다.","无法启动设置程序。"));
        await p.WaitForExitAsync();return p.ExitCode;
    }
    public static string TaskStatus()
    {
        try
        {
            var type=Type.GetTypeFromProgID("Schedule.Service");if(type==null)return I18n.T("작업 스케줄러 확인 불가","无法检查任务计划程序");
            dynamic service=Activator.CreateInstance(type)!;service.Connect();dynamic folder=service.GetFolder("\\");dynamic task=folder.GetTask(Product.TaskName);
            bool enabled=task.Enabled;DateTime next=task.NextRunTime;int result=task.LastTaskResult;
            return enabled?$"{I18n.T("예약됨 · 매일 04:00 · 다음", "已计划 · 每天 04:00 · 下次")} {next:MM-dd HH:mm} · {I18n.T("최근 결과", "最近结果")} {result}":I18n.T("로그 정리 작업 비활성화 · 복구 필요","日志清理任务已禁用 · 需要修复");
        }
        catch{return I18n.T("로그 정리 작업 없음 · 복구 필요","没有日志清理任务 · 需要修复");}
    }
    public static string CleanupStatus()
    {
        try
        {
            var r=Product.ReadJson<CleanupResult>(Path.Combine(Product.MachineDir,"cleanup-result.json"));
            return r==null?I18n.T("아직 정리 실행 기록이 없습니다.","尚无清理运行记录。"):$"{r.AtUtc.ToLocalTime():MM-dd HH:mm} · {r.Status}\n{I18n.T("삭제", "已删除")} {r.Deleted} / {r.Bytes/1048576d:0.0} MB · {I18n.T("건너뜀", "已跳过")} {r.Failed}";
        }
        catch{return I18n.T("정리 결과를 읽지 못했습니다.","无法读取清理结果。");}
    }
    public static string Status(string code)=>code switch {"Confirmed"=>I18n.T("스폰 확인","已确认刷新"), "Planned"=>I18n.T("스폰 예정","计划刷新"), "NotPlanned"=>I18n.T("스폰 예정 없음","未计划刷新"),_=>I18n.T("확인 불가","无法确认")};
    public static string LifeStatus(BossState boss,bool ended)
    {
        var instances=boss.LifeInstances??[];
        if(instances.Count==0)return I18n.T("생존 기록 없음","无生存记录");
        int dead=instances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
        string alive=ended?I18n.T("종료 시 사망 미확인","结束时未确认死亡"):I18n.T("생존 추정","推测存活");
        if(dead==instances.Count)
            return $"{I18n.T("사망 확인", "已确认死亡")} · {instances.Max(i=>i.DiedAt):HH:mm:ss}"+(instances.Count>1?$" · {dead}{I18n.T("명", "个")}":"");
        if(instances.Count==1)return alive;
        return $"{alive} {instances.Count-dead}{I18n.T("명", "个")} · {I18n.T("사망 확인", "已确认死亡")} {dead}{I18n.T("명", "个")}";
    }
}
