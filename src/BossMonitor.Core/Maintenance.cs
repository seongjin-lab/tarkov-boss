using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace BossMonitor.Core;

public class ChangeRecord
{
    public string Root { get; set; }="";
    public string Backup { get; set; }="";
    public string AppliedHash { get; set; }="";
    public DateTime At { get; set; }
    public Dictionary<string,string> Previous { get; set; }=[];
    public Dictionary<string,string> Applied { get; set; }=[];
    public bool Restored { get; set; }
}
public class MachineState
{
    public string GameRoot { get; set; }="";
    public bool PendingTrace { get; set; }
    public List<ChangeRecord> Changes { get; set; }=[];
}
public class CleanupResult
{
    public DateTime AtUtc { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public string Status { get; set; }="";
    public int Deleted { get; set; }
    public int Failed { get; set; }
    public long Bytes { get; set; }
    public long RemainingBytes { get; set; }
}

// Directory handles without FILE_SHARE_DELETE pin all ancestors during privileged operations.
// OPEN_REPARSE_POINT avoids following a link even if it is swapped before opening.
public sealed class PinnedPath : IDisposable
{
    private readonly List<SafeFileHandle> handles=[];
    [StructLayout(LayoutKind.Sequential)] private struct AttributeTag { public uint Attributes; public uint Tag; }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int info,out AttributeTag data,uint size);
    public static SafeFileHandle OpenForDeletion(string path)
    {
        var h=CreateFileW(path,0xC0010000,0,IntPtr.Zero,3,0x00200000,IntPtr.Zero);
        if(h.IsInvalid){h.Dispose();throw new IOException(I18n.T("로그 파일을 독점적으로 열 수 없습니다.","无法独占打开日志文件。"));}
        if(!GetFileInformationByHandleEx(h,9,out var info,8) || (info.Attributes&0x400)!=0){h.Dispose();throw new IOException(I18n.T("링크 파일은 삭제하지 않습니다.","不会删除链接文件。"));}
        return h;
    }
    public PinnedPath(string path)
    {
        try
        {
            var stack=new Stack<string>();
            var dir=new DirectoryInfo(Path.GetFullPath(path));
            while(dir!=null) {stack.Push(dir.FullName);dir=dir.Parent;}
            while(stack.Count>0)
            {
                string current=stack.Pop();
                var h=CreateFileW(current,0x80,3,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
                if(h.IsInvalid) {h.Dispose();throw new IOException(I18n.T("대상 경로를 안전하게 잠글 수 없습니다.","无法安全锁定目标路径。"));}
                handles.Add(h);
                if(!GetFileInformationByHandleEx(h,9,out var info,8) || (info.Attributes&0x400)!=0) throw new IOException(I18n.T("링크 또는 확인할 수 없는 경로는 처리하지 않습니다.","不会处理链接或无法验证的路径。"));
            }
        }
        catch {Dispose();throw;}
    }
    public void Dispose() {for(int i=handles.Count-1;i>=0;i--) handles[i].Dispose();handles.Clear();}
}

public static class Maintenance
{
    private static string StateFile=>Path.Combine(Product.MachineDir,"machine.json");
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static MachineState State()
    {
        Paths.RejectFileLink(StateFile);
        return Product.ReadJson<MachineState>(StateFile)??new();
    }
    public static bool IsAdmin()=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static void Install()
    {
        if(!IsAdmin()) throw new UnauthorizedAccessException(I18n.T("설치 관리자 권한이 필요합니다.","安装需要管理员权限。"));
        bool trusted=Product.TrustedMachineDir();
        Product.SecureMachineDir();
        if(!trusted && File.Exists(StateFile))File.Move(StateFile,Path.Combine(Product.MachineDir,"untrusted-state-"+Guid.NewGuid().ToString("N")+".json"));
        if(!File.Exists(StateFile)) Product.WriteJson(StateFile,new MachineState());
        RegisterTask();
    }
    public static void RegisterTask()
    {
        string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Tarkov Boss Monitor");
        if(!Paths.Canonical(AppContext.BaseDirectory).Equals(Paths.Canonical(expected),StringComparison.OrdinalIgnoreCase))
            throw new IOException(I18n.T("독립 로그 정리 작업은 Program Files의 정식 설치 폴더에서만 등록할 수 있습니다.","独立日志清理任务只能从 Program Files 中的正式安装目录注册。"));
        string exe=Path.Combine(AppContext.BaseDirectory,"BossMonitor.Cleanup.exe");
        Paths.RejectFileLink(exe);
        if(!File.Exists(exe)) throw new IOException(I18n.T("로그 정리 실행 파일을 찾을 수 없습니다.","找不到日志清理程序。"));
        string escaped=System.Security.SecurityElement.Escape(exe)!;
        string date=DateTime.Today.ToString("yyyy-MM-dd")+"T04:00:00";
        string xml=$"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo><Description>Tarkov Boss Monitor: keep 7 days, clean old logs while the game is closed</Description></RegistrationInfo>
          <Triggers><CalendarTrigger><StartBoundary>{date}</StartBoundary><Enabled>true</Enabled><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger></Triggers>
          <Principals><Principal id="Author"><UserId>S-1-5-18</UserId><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
          <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>true</Hidden><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT10M</ExecutionTimeLimit><Priority>7</Priority></Settings>
          <Actions Context="Author"><Exec><Command>{escaped}</Command><WorkingDirectory>{System.Security.SecurityElement.Escape(AppContext.BaseDirectory)}</WorkingDirectory></Exec></Actions>
        </Task>
        """;
        string taskFile=Path.Combine(Product.MachineDir,"cleanup-task.xml");
        Paths.RejectFileLink(taskFile);File.WriteAllText(taskFile,xml,Encoding.Unicode);
        // ServiceAccount is a COM enum, not a permitted XML LogonType value.
        // schtasks supports /RU SYSTEM together with /XML and supplies service-account logon semantics.
        var result=Product.RunHiddenDetailed(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/Create","/TN",Product.TaskName,"/XML",taskFile,"/RU","SYSTEM","/F");
        string diagnostic=Path.Combine(Product.MachineDir,"task-registration-result.json");
        Paths.RejectFileLink(diagnostic);
        Product.WriteJson(diagnostic,new { AtUtc=DateTime.UtcNow,result.ExitCode,result.Output,result.Error });
        if(result.ExitCode!=0)
        {
            string detail=string.IsNullOrWhiteSpace(result.Error)?result.Output:result.Error;
            throw new IOException($"{I18n.T("Windows 로그 정리 예약 작업을 등록하지 못했습니다.","无法注册 Windows 日志清理计划任务。")}\n{I18n.T("종료 코드", "退出代码")}: {result.ExitCode}\n{detail}\n{I18n.T("진단 파일", "诊断文件")}: {diagnostic}");
        }
    }
    public static void PrepareUpdate()
    {
        Product.RunHidden(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/Change","/TN",Product.TaskName,"/DISABLE");
        Product.RunHidden(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/End","/TN",Product.TaskName);
        using var mutex=new Mutex(false,@"Global\TarkovBossMonitor-Cleanup");bool held=false;
        try
        {
            try{held=mutex.WaitOne(TimeSpan.FromSeconds(30));}catch(AbandonedMutexException){held=true;}
            if(!held)throw new IOException(I18n.T("로그 정리 프로세스 종료 후 업데이트를 다시 시도해 주세요.","请在日志清理进程退出后重试更新。"));
        }
        finally{if(held)mutex.ReleaseMutex();}
    }
    public static bool ConfigureForInstall(string selected)
    {
        if(!IsAdmin()) throw new UnauthorizedAccessException(I18n.T("로그 설정 변경에 관리자 권한이 필요합니다.","更改日志设置需要管理员权限。"));
        string root=Paths.ValidateGame(selected);
        try
        {
            if(!Product.GameRunning()) {Configure(root);return false;}
        }
        catch(IOException) when(Product.GameRunning()) { }
        Product.SecureMachineDir();
        using var pin=new PinnedPath(root);
        using var machinePin=new PinnedPath(Product.MachineDir);
        var state=State();
        state.GameRoot=root;
        state.PendingTrace=!Paths.LoggingReady(root);
        Product.WriteJson(StateFile,state);
        return true;
    }
    public static string Configure(string selected)
    {
        if(!IsAdmin()) throw new UnauthorizedAccessException(I18n.T("로그 설정 변경에 관리자 권한이 필요합니다.","更改日志设置需要管理员权限。"));
        if(Product.GameRunning()) throw new IOException(I18n.T("게임 종료 후 로그 설정을 적용해 주세요.","请退出游戏后再应用日志设置。"));
        Product.SecureMachineDir();
        string root=Paths.ValidateGame(selected);
        using var pinned=new PinnedPath(root);
        using var machinePin=new PinnedPath(Product.MachineDir);
        string config=Path.Combine(root,"Logging.config");
        Paths.RejectFileLink(config);
        string original=File.ReadAllText(config);
        var document=JsonNode.Parse(original) ?? throw new IOException(I18n.T("로그 설정 JSON이 올바르지 않습니다.","日志设置 JSON 无效。"));
        var rules=document["rules"]?.AsArray() ?? throw new IOException(I18n.T("로그 설정에 rules가 없습니다.","日志设置中没有 rules。"));
        var record=new ChangeRecord {Root=root,At=DateTime.UtcNow};
        string[] levels=["Trace","Debug","Information","Warning","Error","Critical"];
        foreach(var desired in new Dictionary<string,string> { ["aiData"]="Trace",["application"]="Debug",["backend"]="Information" })
        {
            var matches=rules.Where(r=>r?["fileName"]?.GetValue<string>()==desired.Key).ToList();
            if(matches.Count!=1) throw new IOException($"{desired.Key} {I18n.T("로그 규칙이 없거나 중복되었습니다. 게임 설정을 복구해 주세요.","日志规则缺失或重复，请修复游戏设置。")}");
            string previous=matches[0]?["minLevel"]?.GetValue<string>()??"";
            int index=Array.IndexOf(levels,previous);
            if(index<0) throw new IOException(I18n.T("알 수 없는 로그 수준입니다. 자동 변경을 중단했습니다.","日志级别未知，已停止自动修改。"));
            if(index<=Array.IndexOf(levels,desired.Value)) continue;
            record.Previous[desired.Key]=previous;record.Applied[desired.Key]=desired.Value;
            matches[0]!["minLevel"]=desired.Value;
        }
        var state=State();
        if(record.Previous.Count>0)
        {
            record.Backup=Path.Combine(Product.MachineDir,"Logging-"+Guid.NewGuid().ToString("N")+".bak");
            File.WriteAllText(record.Backup,original);
            string updated=document.ToJsonString(Product.Json);_ = JsonNode.Parse(updated);
            record.AppliedHash=Hash(updated);
            // Record recovery information before modifying the game file.
            state.Changes.Add(record);Product.WriteJson(StateFile,state);
            ReplaceConfig(config,original,updated);
            if(Hash(File.ReadAllText(config))!=record.AppliedHash) throw new IOException(I18n.T("Trace 설정 저장 결과가 일치하지 않습니다.","保存后的 Trace 设置不一致。"));
        }
        RegisterTask();
        state.GameRoot=root;state.PendingTrace=false;Product.WriteJson(StateFile,state);
        return record.Previous.Count>0?I18n.T("로그 설정 적용 완료 · 게임을 다시 실행해 주세요.","日志设置已应用 · 请重新启动游戏。"):I18n.T("Trace 설정 확인 완료 · 로그 정리 경로 등록 완료.","Trace 设置检查完成 · 日志清理路径已注册。");
    }
    private static void ReplaceConfig(string path,string before,string after)
    {
        string temp=path+".boss-monitor-"+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            if(Product.GameRunning()) throw new IOException(I18n.T("게임이 실행되었습니다. 종료 후 다시 적용해 주세요.","游戏已启动，请退出后重新应用。"));
            Paths.RejectFileLink(path);
            File.WriteAllText(temp,after,new UTF8Encoding(false));
            if(Hash(File.ReadAllText(path))!=Hash(before)) throw new IOException(I18n.T("다른 프로그램이 로그 설정을 변경했습니다. 다시 적용해 주세요.","其他程序修改了日志设置，请重新应用。"));
            File.Replace(temp,path,null);
        }
        finally {if(File.Exists(temp)) File.Delete(temp);}
    }
    public static string Uninstall()
    {
        Product.RunHidden(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/Change","/TN",Product.TaskName,"/DISABLE");
        Product.RunHidden(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/End","/TN",Product.TaskName);
        using var mutex=new Mutex(false,@"Global\TarkovBossMonitor-Cleanup");
        bool held=false;
        try
        {
            try {held=mutex.WaitOne(TimeSpan.FromSeconds(30));} catch(AbandonedMutexException) {held=true;}
            if(!held) throw new IOException(I18n.T("로그 정리 작업 종료를 기다리지 못했습니다. 제거를 다시 시도해 주세요.","等待日志清理任务退出失败，请重试卸载。"));
            Product.RunHidden(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/Delete","/TN",Product.TaskName,"/F");
            int skipped=0;var state=State();
            foreach(var record in state.Changes.AsEnumerable().Reverse().Where(c=>!c.Restored))
            {
                try
                {
                    if(Product.GameRunning()) {skipped++;continue;}
                    string root=Paths.ValidateGame(record.Root);using var pin=new PinnedPath(root);
                    string path=Path.Combine(root,"Logging.config");string before=File.ReadAllText(path);
                    if(Hash(before)!=record.AppliedHash) {skipped++;continue;}
                    var node=JsonNode.Parse(before)!;var rules=node["rules"]!.AsArray();
                    foreach(var kv in record.Previous)
                    {
                        var matching=rules.Where(r=>r?["fileName"]?.GetValue<string>()==kv.Key).ToList();
                        if(matching.Count!=1 || matching[0]?["minLevel"]?.GetValue<string>()!=record.Applied[kv.Key]) throw new IOException(I18n.T("변경된 설정은 자동 복원하지 않습니다.","不会自动恢复已被修改的设置。"));
                        matching[0]!["minLevel"]=kv.Value;
                    }
                    // Restore exact previous bytes only when the complete config is unchanged.
                    Paths.RejectFileLink(record.Backup);
                    if(!Path.GetDirectoryName(record.Backup)!.Equals(Product.MachineDir,StringComparison.OrdinalIgnoreCase)) throw new IOException(I18n.T("백업 경로가 올바르지 않습니다.","备份路径无效。"));
                    ReplaceConfig(path,before,File.ReadAllText(record.Backup));record.Restored=true;
                }
                catch {skipped++;}
            }
            state.GameRoot="";state.PendingTrace=false;Product.WriteJson(StateFile,state);
            string message=skipped>0 ? $"{I18n.T("예약 작업 제거 완료.","计划任务已删除。")} {skipped}{I18n.T("건의 로그 설정은 게임 실행·설정 변경 등으로 자동 복원하지 않았습니다. Logging.config의 aiData minLevel을 백업의 이전 값으로 수동 복원할 수 있습니다. 백업 위치:"," 项日志设置因游戏运行或设置变更而未自动恢复。可将 Logging.config 中 aiData 的 minLevel 手动恢复为备份中的原值。备份位置：")} {Product.MachineDir}" : I18n.T("예약 작업 제거 및 제품이 변경한 로그 설정 복원 완료.","计划任务已删除，并已恢复本产品修改的日志设置。");
            File.WriteAllText(Path.Combine(Product.MachineDir,"uninstall-result.txt"),message);return message;
        }
        finally {if(held)mutex.ReleaseMutex();}
    }

    [StructLayout(LayoutKind.Sequential)] private struct Disposition { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetFileInformationByHandle(SafeFileHandle file,int info,ref Disposition data,uint size);
    public static void Cleanup()
    {
        using var mutex=new Mutex(false,@"Global\TarkovBossMonitor-Cleanup");bool held=false;
        try
        {
            try {held=mutex.WaitOne(0);} catch(AbandonedMutexException){held=true;}
            if(!held)return;
            if(!Product.TrustedMachineDir())throw new UnauthorizedAccessException(I18n.T("정리 명세 저장소 권한이 올바르지 않습니다.","清理配置存储的权限无效。"));
            Paths.RejectLinks(Product.MachineDir);
            var previous=Product.ReadJson<CleanupResult>(Path.Combine(Product.MachineDir,"cleanup-result.json"));
            var result=new CleanupResult {AtUtc=DateTime.UtcNow,LastSuccessUtc=previous?.LastSuccessUtc};
            try
            {
                var state=State();
                if(state.GameRoot=="") {result.Status=I18n.T("게임 경로 미등록","未注册游戏路径");SaveResult(result);return;}
                if(Product.GameRunning()) {result.Status=I18n.T("게임 실행 중 · 정리 건너뜀","游戏运行中 · 跳过清理");SaveResult(result);return;}
                if(state.PendingTrace) Configure(state.GameRoot);
                string root=Paths.ValidateGame(state.GameRoot);using var rootPin=new PinnedPath(root);
                string logs=Path.Combine(root,"Logs");
                if(!Directory.Exists(logs)){result.Status=I18n.T("게임 로그 생성 대기","等待游戏生成日志");SaveResult(result);return;}
                using var logsPin=new PinnedPath(logs);
                var sessions=Directory.GetDirectories(logs).Where(p=>LogReader.SessionTime(p)!=DateTime.MinValue).OrderByDescending(LogReader.SessionTime).ToList();
                DateTime cutoff=DateTime.UtcNow.AddHours(-168);
                foreach(string session in sessions.Skip(1))
                {
                    if(Product.GameRunning()) {result.Status=I18n.T("게임 실행 감지 · 정리 중단","检测到游戏运行 · 停止清理");break;}
                    try
                    {
                        using(var sessionPin=new PinnedPath(session))
                        foreach(string file in Directory.GetFiles(session,"*.log",SearchOption.TopDirectoryOnly))
                        {
                            if(!Regex.IsMatch(Path.GetFileName(file),@"^\d{4}\.\d{2}\.\d{2}_\d{1,2}-\d{2}-\d{2}_[\w.]+ [A-Za-z][A-Za-z0-9_]*_\d+\.log$")) continue;
                            try
                            {
                                if(Product.GameRunning()) break;
                                Paths.RejectFileLink(file);
                                if(File.GetLastWriteTimeUtc(file)>=cutoff)continue;
                                using var handle=PinnedPath.OpenForDeletion(file);
                                using var locked=new FileStream(handle,FileAccess.ReadWrite);
                                if(File.GetLastWriteTimeUtc(file)>=cutoff)continue;
                                long bytes=locked.Length;var disposition=new Disposition {Delete=true};
                                if(!SetFileInformationByHandle(locked.SafeFileHandle,4,ref disposition,4))throw new IOException(I18n.T("로그 파일을 삭제하지 못했습니다.","无法删除日志文件。"));
                                result.Deleted++;result.Bytes+=bytes;
                            }
                            catch {result.Failed++;}
                        }
                        Paths.RejectLinks(session);
                        if(!Directory.EnumerateFileSystemEntries(session).Any())Directory.Delete(session,false);
                    }
                    catch {result.Failed++;}
                }
                foreach(string session in sessions)
                {
                    if(!Directory.Exists(session))continue;
                    try {using var pin=new PinnedPath(session);foreach(string f in Directory.GetFiles(session,"*.log")){Paths.RejectFileLink(f);result.RemainingBytes+=new FileInfo(f).Length;}}catch{}
                }
                if(result.Status==""){result.Status=result.Failed==0?I18n.T("정리 완료","清理完成"):I18n.T("정리 완료 · 일부 파일 건너뜀","清理完成 · 已跳过部分文件");result.LastSuccessUtc=DateTime.UtcNow;}
            }
            catch {result.Status=I18n.T("경로·권한·게임 상태 확인 실패 · 삭제 중단","路径、权限或游戏状态检查失败 · 已停止删除");result.Failed++;}
            SaveResult(result);
        }
        finally {if(held)mutex.ReleaseMutex();}
    }
    private static void SaveResult(CleanupResult result)
    {
        string path=Path.Combine(Product.MachineDir,"cleanup-result.json");Paths.RejectFileLink(path);Product.WriteJson(path,result);
        string history=Path.Combine(Product.MachineDir,"cleanup-history.json");Paths.RejectFileLink(history);
        var list=Product.ReadJson<List<CleanupResult>>(history)??[];list.Add(result);
        Product.WriteJson(history,list.OrderByDescending(r=>r.AtUtc).Take(90).ToList());
    }
}
