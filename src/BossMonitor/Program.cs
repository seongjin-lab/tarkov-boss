using System.IO.MemoryMappedFiles;

namespace BossMonitor;
internal static class Program
{
    private const string SingletonName=@"Local\TarkovBossMonitor-GUI";
    private const string ActivationEventName=@"Local\TarkovBossMonitor-Activate";
    private const string ActivationOwnerName=@"Local\TarkovBossMonitor-ActivationOwner";

    [STAThread] private static void Main()
    {
        try
        {
        using var singleton=new Mutex(true,SingletonName,out bool created);
        if(!created){ActivateExistingInstance();return;}
        // Publish the PID before the event becomes available to subsequent launches.
        using var activationOwner = MemoryMappedFile.CreateOrOpen(ActivationOwnerName, sizeof(int));
        using (var owner = activationOwner.CreateViewAccessor(0, sizeof(int)))
        {
            owner.Write(0, Environment.ProcessId);
        }
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
                // Explorer may grant foreground access to the new process only.
                GrantForegroundToExistingInstance();
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

    private static void GrantForegroundToExistingInstance()
    {
        try
        {
            using var activationOwner = MemoryMappedFile.OpenExisting(ActivationOwnerName, MemoryMappedFileRights.Read);
            using var owner = activationOwner.CreateViewAccessor(0, sizeof(int), MemoryMappedFileAccess.Read);
            int processId = owner.ReadInt32(0);
            if (processId > 0)
                WindowActivation.AllowSetForegroundWindow(processId);
        }
        catch (FileNotFoundException)
        {
            // Older instances can still receive the activation event.
        }
        catch (UnauthorizedAccessException)
        {
            // Signal the event even if we cannot read the foreground grant target.
        }
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
}
