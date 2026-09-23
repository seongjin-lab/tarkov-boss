namespace BossMonitor;
internal record MapItem(string Id,string Name) {public override string ToString()=>Name+" · "+Id;}

public class SettingsView : UserControl
{
    public AppSettings Settings {get;}
    public event Action<bool>? Completed;
    public bool IsApplying=>applying;
    private readonly ComboBox path=new() {IsEditable=true,MinWidth=200};
    private readonly ComboBox map=new(),mode=new();
    private readonly System.Windows.Controls.Primitives.UniformGrid choices=new() {Columns=2};
    private readonly TextBlock setupStatus=Theme.Label("",13,Theme.Muted),taskStatus=Theme.Label("",12,Theme.Muted),cleanup=Theme.Label("",12,Theme.Muted),storage=Theme.Label("",12,Theme.Muted);
    private readonly CheckBox top=new() {Content=I18n.T("항상 위에 표시","始终置顶")};
    private readonly CheckBox closeToTray=new() {Content=I18n.T("닫으면 트레이로 숨기기","关闭时最小化到托盘")};
    private readonly List<(string Role,CheckBox Box)> checks=[];
    private readonly HashSet<string> observed;
    private string currentMap="";
    private bool loading,applying;
    private readonly Button save=new() {Content=I18n.T("설정 저장 · 감시 시작","保存设置 · 开始监控")};
    public SettingsView(AppSettings source,IEnumerable<string> observedRoles,bool first)
    {
        Settings=System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(source,Product.Json),Product.Json)!;
        observed=new(observedRoles,StringComparer.OrdinalIgnoreCase);
        Background=Theme.Bg;Foreground=Theme.Text;FontFamily=new FontFamily(I18n.FontFamily);FontSize=14;
        var root=new DockPanel {Margin=new Thickness(24)};Content=root;
        var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(Theme.Action(first?I18n.T("나중에 설정","稍后设置"):I18n.T("돌아가기","返回"),(s,e)=>{if(!applying)Completed?.Invoke(false);}));save.Click+=async(s,e)=>await Save();footer.Children.Add(save);
        var heading=new StackPanel();DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        heading.Children.Add(Theme.Label(first?I18n.T("처음 시작하기","开始使用"):I18n.T("설정","设置"),24,Theme.Green));
        heading.Children.Add(Theme.Label(first?I18n.T("게임 폴더를 확인하고 설정을 저장하면 감시를 시작합니다.","确认游戏文件夹并保存设置后即可开始监控。"):I18n.T("게임 연결, 맵별 보스, 로그 보관 설정을 관리합니다.","管理游戏连接、各地图首领和日志保留设置。"),14,Theme.Muted));
        var tabs=new TabControl {Background=Theme.Card,Foreground=Theme.Text,BorderBrush=Theme.Color("#D3DDD6"),Padding=new Thickness(16),Margin=new Thickness(0,8,0,16)};root.Children.Add(tabs);
        StackPanel Section(string title)
        {
            var panel=new StackPanel();tabs.Items.Add(new TabItem {Header=title,Foreground=Theme.Text,Padding=new Thickness(16,9,16,9),Content=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});return panel;
        }
        var body=Section(I18n.T("게임 연결","游戏连接"));
        body.Children.Add(Theme.Label(I18n.T("타르코프 설치 폴더","《逃离塔科夫》安装文件夹"),17));
        var pathRow=new DockPanel();var browse=Theme.Action(I18n.T("폴더 선택","选择文件夹"),(s,e)=>Browse());DockPanel.SetDock(browse,Dock.Right);pathRow.Children.Add(browse);pathRow.Children.Add(path);body.Children.Add(pathRow);
        path.Text=Settings.GamePath;
        body.Children.Add(Theme.Label(I18n.T("EscapeFromTarkov.exe와 Logging.config가 있는 폴더를 선택하세요.\n맵은 자동 감지합니다. 실행 중인 게임은 종료한 뒤 Trace 설정이 적용됩니다.","请选择包含 EscapeFromTarkov.exe 和 Logging.config 的文件夹。\n地图会自动检测。游戏退出后将应用 Trace 设置。"),12,Theme.Muted));
        var traceActions=new WrapPanel();traceActions.Children.Add(Theme.Action(I18n.T("경로 자동 탐색","自动查找路径"),async(s,e)=>await Discover()));traceActions.Children.Add(Theme.Action(I18n.T("로그 설정 자동 적용","自动应用日志设置"),async(s,e)=>await ApplyTrace()));body.Children.Add(traceActions);body.Children.Add(setupStatus);
        top.IsChecked=Settings.AlwaysOnTop;body.Children.Add(top);
        closeToTray.IsChecked=Settings.CloseToTray;body.Children.Add(closeToTray);
        body.Children.Add(Theme.Label(I18n.T("체크하면 닫기(X)로 숨긴 뒤에도 감시합니다. 해제하면 창을 닫을 때 종료합니다.\n트레이로 숨길 때 안내하고, 숨겨진 상태에서 보스 스폰 확인 시 Windows 알림을 표시합니다.","启用后，点击关闭(X)会隐藏到托盘并继续监控；禁用后将退出程序。\n隐藏到托盘时会提示，隐藏期间确认首领刷新时会显示 Windows 通知。"),13,Theme.Muted));
        body=Section(I18n.T("맵별 보스","各地图首领"));
        body.Children.Add(Theme.Label(I18n.T("맵별 감시 보스","按地图选择监控首领"),17));body.Children.Add(Theme.Label(I18n.T("맵은 자동으로 감지합니다. 여기서 각 맵의 감시 대상을 선택하세요.","地图会自动检测。请在此选择每张地图要监控的首领。"),14,Theme.Muted));map.SelectionChanged+=(s,e)=>SelectMap();body.Children.Add(map);
        mode.Items.Add(I18n.T("모든 보스 감시","监控所有首领"));mode.Items.Add(I18n.T("선택한 보스만 감시","仅监控所选首领"));mode.Items.Add(I18n.T("감시 안 함","不监控"));mode.SelectionChanged+=(s,e)=>{choices.IsEnabled=mode.SelectedIndex==1;};body.Children.Add(mode);
        body.Children.Add(new Border {Background=Theme.Card,BorderBrush=Theme.Color("#D3DDD6"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Child=choices});
        body=Section(I18n.T("로그 관리","日志管理"));
        body.Children.Add(Theme.Label(I18n.T("게임 로그 · 최근 7일 보관","游戏日志 · 保留最近 7 天"),17));body.Children.Add(Theme.Label(I18n.T("매일 04:00에 Windows 예약 작업으로 정리합니다. 앱이 꺼져 있어도 실행됩니다.\n게임 실행 중·가장 최근 세션은 보호하며, 최근 7일 로그는 용량을 이유로 삭제하지 않습니다.","Windows 计划任务每天 04:00 清理，即使应用未运行也会执行。\n游戏运行期间和最新会话会受到保护，最近 7 天的日志不会因容量原因被删除。"),12,Theme.Muted));body.Children.Add(taskStatus);body.Children.Add(cleanup);body.Children.Add(storage);
        var maintenance=new WrapPanel();maintenance.Children.Add(Theme.Action(I18n.T("예약 작업 복구","修复计划任务"),async(s,e)=>await Repair()));maintenance.Children.Add(Theme.Action(I18n.T("상태 새로고침","刷新状态"),async(s,e)=>await RefreshStatus()));maintenance.Children.Add(Theme.Action(I18n.T("진단 내보내기","导出诊断"),(s,e)=>ExportDiagnostic()));body.Children.Add(maintenance);
        body.Children.Add(Theme.Label(I18n.T("공식 로컬 PvE 상세 로그를 지원합니다. 업데이트는 새 설치 프로그램으로 진행합니다.","支持官方本地 PvE 详细日志。请使用新的安装程序进行更新。"),11,Theme.Muted));
        RefreshMaps("Interchange");
        Loaded+=async(s,e)=>{await Discover();await RefreshStatus();};
    }
    private void Browse()
    {
        using var picker=new Forms.FolderBrowserDialog {Description=I18n.T("타르코프 게임 설치 폴더 선택","选择《逃离塔科夫》游戏安装文件夹"),UseDescriptionForTitle=true,SelectedPath=Directory.Exists(path.Text)?path.Text:""};
        if(picker.ShowDialog()==Forms.DialogResult.OK){path.Text=picker.SelectedPath;setupStatus.Text=Paths.TraceStatus(path.Text);}
    }
    private async Task Discover()
    {
        string current=path.Text;setupStatus.Text=I18n.T("설치 경로 탐색 중…","正在查找安装路径…");
        var found=await Task.Run(()=>Paths.Discover(current));
        path.ItemsSource=found;
        if(current!="")path.Text=current;else if(found.Count>0)path.Text=found[0];
        setupStatus.Text=found.Count==0?I18n.T("자동 탐색 결과가 없습니다. 폴더를 선택하거나 경로를 입력해 주세요.","未自动找到安装路径。请选择文件夹或输入路径。"): $"{I18n.T("설치 후보", "候选安装路径")} {found.Count} · {(found.Count>1?I18n.T("사용할 설치 경로를 선택하세요.","请选择要使用的安装路径。"):Paths.TraceStatus(path.Text))}";
    }
    private void RefreshMaps(string selected)
    {
        loading=true;
        var ids=Catalog.Maps.Keys.Where(x=>!x.Equals("factory4_night",StringComparison.OrdinalIgnoreCase)).OrderBy(x=>Catalog.MapName(x)).Select(x=>new MapItem(x,Catalog.MapName(x))).ToList();
        map.ItemsSource=ids;map.SelectedItem=ids.FirstOrDefault(x=>x.Id.Equals(selected,StringComparison.OrdinalIgnoreCase))??ids.First();loading=false;currentMap="";SelectMap();
    }
    private void SelectMap()
    {
        if(loading)return;CaptureFilter();currentMap=(map.SelectedItem as MapItem)?.Id??"";
        var filter=Settings.Filter(currentMap);mode.SelectedIndex=filter.Mode=="selected"?1:filter.Mode=="off"?2:0;LoadChoices();
    }
    private void LoadChoices()
    {
        checks.Clear();choices.Children.Clear();var filter=Settings.Filter(currentMap);
        foreach(string role in Catalog.Bosses.Keys.Concat(Settings.CustomRoles.Keys).Concat(observed.Where(r=>Catalog.IsBoss(r,Settings))).Concat(filter.Roles).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x.Equals("bossKilla",StringComparison.OrdinalIgnoreCase)?0:x.Equals("bossTagilla",StringComparison.OrdinalIgnoreCase)?1:filter.Roles.Contains(x,StringComparer.OrdinalIgnoreCase)?2:3).ThenBy(x=>Catalog.BossName(x,Settings)))
        {
            var box=new CheckBox {Content=Catalog.BossName(role,Settings),ToolTip=role,IsChecked=filter.Roles.Contains(role,StringComparer.OrdinalIgnoreCase),Padding=new Thickness(0),MinHeight=34,VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};checks.Add((role,box));choices.Children.Add(box);
        }
        choices.IsEnabled=mode.SelectedIndex==1;
    }
    private void CaptureFilter()
    {
        if(currentMap=="")return;
        var filter=new MapFilter {Mode=mode.SelectedIndex==1?"selected":mode.SelectedIndex==2?"off":"all",Roles=checks.Where(x=>x.Box.IsChecked==true).Select(x=>x.Role).ToList()};
        Settings.Maps[currentMap]=filter;
        if(currentMap.Equals("factory4_day",StringComparison.OrdinalIgnoreCase))
            Settings.Maps["factory4_night"]=new(){Mode=filter.Mode,Roles=filter.Roles.ToList()};
    }
    private async Task<bool> ApplyTrace()
    {
        if(applying)return false;
        try
        {
            string root=Paths.ValidateGame(path.Text);
            if(Product.GameRunning()){setupStatus.Text=I18n.T("게임 실행 중입니다. 설정 저장 후 게임을 종료하면 자동 적용합니다.","游戏正在运行。保存设置并退出游戏后将自动应用。");return true;}
            applying=true;save.IsEnabled=false;setupStatus.Text=I18n.T("로그 설정 자동 적용 및 정리 경로 등록 중…","正在自动应用日志设置并注册清理路径…");
            int code=await Helpers.Elevated("configure",root);
            if(code!=0){setupStatus.Text=I18n.T("설정을 적용하지 못했습니다. 오류 안내를 확인해 주세요.","无法应用设置，请查看错误提示。");return false;}
            path.Text=root;setupStatus.Text=I18n.T("Trace 설정 완료 · 게임을 실행하거나 다시 실행해 주세요.","Trace 设置完成 · 请启动或重新启动游戏。");await RefreshStatus();return true;
        }
        catch(Exception ex){setupStatus.Text=ex is System.ComponentModel.Win32Exception?I18n.T("관리자 권한 요청이 취소되었습니다.","管理员权限请求已取消。"):ex.Message;return false;}
        finally{applying=false;save.IsEnabled=true;}
    }
    private async Task Save()
    {
        if(applying)return;
        try
        {
            string root=Paths.ValidateGame(path.Text);CaptureFilter();
            bool needed=!Paths.LoggingReady(root);
            try{needed|=!Maintenance.State().GameRoot.Equals(root,StringComparison.OrdinalIgnoreCase);}catch{needed=true;}
            if(needed && !await ApplyTrace())return;
            Settings.GamePath=root;Settings.AlwaysOnTop=top.IsChecked==true;Settings.CloseToTray=closeToTray.IsChecked==true;Settings.Save();Completed?.Invoke(true);
        }
        catch(Exception ex){setupStatus.Text=ex.Message;}
    }
    private async Task Repair()
    {
        if(applying)return;applying=true;save.IsEnabled=false;
        try{int code=await Helpers.Elevated("repair");setupStatus.Text=code==0?I18n.T("로그 정리 예약 작업 복구 완료","日志清理计划任务修复完成"):I18n.T("예약 작업 복구 실패","计划任务修复失败");await RefreshStatus();}
        catch(Exception ex){setupStatus.Text=ex.Message;}
        finally{applying=false;save.IsEnabled=true;}
    }
    private async Task RefreshStatus()
    {
        taskStatus.Text=Helpers.TaskStatus();cleanup.Text=Helpers.CleanupStatus();string selected=path.Text;
        storage.Text=I18n.T("게임 로그 용량 확인 중…","正在检查游戏日志大小…");
        try
        {
            var info=await Task.Run(()=>
            {
                string root=Paths.Canonical(selected),logs=Path.Combine(root,"Logs");long bytes=0;
                if(Directory.Exists(logs))
                {
                    Paths.RejectLinks(logs);
                    foreach(string dir in Directory.GetDirectories(logs))
                    {
                        try{Paths.RejectLinks(dir);foreach(string f in Directory.GetFiles(dir,"*.log")){Paths.RejectFileLink(f);bytes+=new FileInfo(f).Length;}}catch{}
                    }
                }
                var disk=new DriveInfo(Path.GetPathRoot(root)!);return $"{I18n.T("게임 로그", "游戏日志")} {bytes/1073741824d:0.00} GB · {I18n.T("드라이브 여유", "磁盘可用空间")} {disk.AvailableFreeSpace/1073741824d:0.0} GB";
            });storage.Text=info;
        }
        catch{storage.Text=I18n.T("게임 경로를 선택하면 로그 용량을 확인할 수 있습니다.","选择游戏路径后即可查看日志大小。");}
    }
    private void ExportDiagnostic()
    {
        var dialog=new Microsoft.Win32.SaveFileDialog {FileName="boss-monitor-diagnostic.json",Filter=I18n.T("JSON 파일|*.json","JSON 文件|*.json")};if(dialog.ShowDialog(Window.GetWindow(this))!=true)return;
        // Export structured status only: no raw logs, installation paths, identities or authentication fields.
        Product.WriteJson(dialog.FileName,new {Version=typeof(MainWindow).Assembly.GetName().Version?.ToString(),AtUtc=DateTime.UtcNow,Trace=Paths.TraceStatus(path.Text),ScheduledTask=Helpers.TaskStatus(),ObservedRoles=observed.OrderBy(x=>x).ToArray(),Filters=Settings.Maps.Select(x=>new {Map=x.Key,x.Value.Mode,x.Value.Roles}).ToArray()});
        setupStatus.Text=I18n.T("개인 경로·닉네임·프로필·원본 로그를 제외한 진단 파일을 저장했습니다.","诊断文件已保存，其中不含个人路径、昵称、配置文件或原始日志。");
    }
}
