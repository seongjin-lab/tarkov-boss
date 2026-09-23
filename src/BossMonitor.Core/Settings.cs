using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace BossMonitor.Core;

public static class Product
{
    public const string TaskName = "TarkovBossMonitor-LogCleanup";
    public static readonly string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TarkovBossMonitor");
    public static readonly string MachineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TarkovBossMonitor");
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static void WriteJson<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static T? ReadJson<T>(string path) => File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default;
    public static bool GameRunning()
    {
        // Any running Tarkov instance protects every registered installation.
        foreach (var p in Process.GetProcessesByName("EscapeFromTarkov")) { p.Dispose(); return true; }
        return false;
    }
    public static int RunHidden(string exe, params string[] args)
        => RunHiddenDetailed(exe,args).ExitCode;
    public static (int ExitCode,string Output,string Error) RunHiddenDetailed(string exe,params string[] args)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var encoding=System.Text.Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=encoding,StandardErrorEncoding=encoding };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var p = Process.Start(start) ?? throw new IOException(I18n.T("프로세스를 시작하지 못했습니다.", "无法启动进程。"));
        var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(60_000)) { p.Kill(); throw new TimeoutException(I18n.T("작업 시간이 초과되었습니다.", "操作超时。")); }
        return (p.ExitCode,output.GetAwaiter().GetResult().Trim(),error.GetAwaiter().GetResult().Trim());
    }
    public static void SecureMachineDir()
    {
        Directory.CreateDirectory(MachineDir);
        Paths.RejectLinks(MachineDir);
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        acl.SetOwner(admins);
        foreach (var sid in new[] { admins, system })
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(MachineDir).SetAccessControl(acl);
        foreach(string file in Directory.GetFiles(MachineDir))
        {
            Paths.RejectFileLink(file);
            var fileAcl=new FileSecurity();fileAcl.SetAccessRuleProtection(true,false);fileAcl.SetOwner(admins);
            foreach(var sid in new[]{admins,system})fileAcl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
            fileAcl.AddAccessRule(new FileSystemAccessRule(users,FileSystemRights.ReadAndExecute,AccessControlType.Allow));new FileInfo(file).SetAccessControl(fileAcl);
        }
    }
    public static bool TrustedMachineDir()
    {
        try
        {
            if(!Directory.Exists(MachineDir))return false;
            Paths.RejectLinks(MachineDir);
            var acl=new DirectoryInfo(MachineDir).GetAccessControl();
            var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
            var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
            if(acl.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner)return false;
            if(!acl.AreAccessRulesProtected || (!owner.Equals(admins) && !owner.Equals(system)))return false;
            var writes=FileSystemRights.WriteData|FileSystemRights.AppendData|FileSystemRights.WriteExtendedAttributes|FileSystemRights.WriteAttributes|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
            foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                if(rule.AccessControlType==AccessControlType.Allow && !rule.IdentityReference.Equals(admins) && !rule.IdentityReference.Equals(system) && (rule.FileSystemRights&writes)!=0)return false;
            return true;
        }
        catch{return false;}
    }
}

public class MapFilter
{
    public string Mode { get; set; } = "all";
    public List<string> Roles { get; set; } = [];
}
public class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string GamePath { get; set; } = "";
    public bool AlwaysOnTop { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public Dictionary<string, MapFilter> Maps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> CustomRoles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public AppSettings()
    {
        foreach(var entry in Catalog.DefaultBosses)
            Maps[entry.Key] = new() { Mode = "selected", Roles = entry.Value.ToList() };
    }
    public MapFilter Filter(string map)
    {
        string canonical=Catalog.CanonicalMapId(map);
        return Maps.FirstOrDefault(x => x.Key.Equals(canonical, StringComparison.OrdinalIgnoreCase)).Value ?? new MapFilter();
    }
    public static AppSettings Load()
    {
        string path = Path.Combine(Product.UserDir, "settings.json");
        try
        {
            var settings = Product.ReadJson<AppSettings>(path) ?? new();
            if (settings.SchemaVersion > 1) throw new IOException(I18n.T("더 새로운 프로그램 버전의 설정입니다.", "此设置来自更高版本的程序。"));
            settings.Maps ??= new(); settings.CustomRoles ??= new();
            foreach(var filter in settings.Maps.Values){filter.Mode=string.IsNullOrWhiteSpace(filter.Mode)?"all":filter.Mode;filter.Roles??=[];}
            bool added=false;
            added|=settings.Maps.Remove("TarkovStreets");
            added|=settings.Maps.Remove("Sandbox_high");
            added|=settings.CustomRoles.Remove("gifter");
            foreach(var filter in settings.Maps.Values)
                if(filter.Roles.RemoveAll(r=>r.Equals("gifter",StringComparison.OrdinalIgnoreCase))>0)added=true;
            foreach(var entry in Catalog.DefaultBosses)
                if(!settings.Maps.Keys.Any(id=>id.Equals(entry.Key,StringComparison.OrdinalIgnoreCase)))
                {
                    settings.Maps[entry.Key]=new() {Mode="selected",Roles=entry.Value.ToList()};added=true;
                }
            var day=settings.Filter("factory4_day");var night=settings.Filter("factory4_night");
            string factoryMode=day.Mode=="selected"||night.Mode=="selected"?"selected":day.Mode=="all"||night.Mode=="all"?"all":"off";
            var factoryRoles=day.Roles.Concat(night.Roles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(day.Mode!=factoryMode || night.Mode!=factoryMode || !day.Roles.SequenceEqual(factoryRoles,StringComparer.OrdinalIgnoreCase) || !night.Roles.SequenceEqual(factoryRoles,StringComparer.OrdinalIgnoreCase))added=true;
            settings.Maps["factory4_day"]=new(){Mode=factoryMode,Roles=factoryRoles.ToList()};
            settings.Maps["factory4_night"]=new(){Mode=factoryMode,Roles=factoryRoles.ToList()};
            if(added) settings.Save();
            return settings;
        }
        catch
        {
            if (File.Exists(path)) File.Copy(path, path + ".recovery-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true);
            return new();
        }
    }
    public void Save()
    {
        string path = Path.Combine(Product.UserDir, "settings.json");
        if (File.Exists(path)) File.Copy(path, path + ".bak", true);
        Product.WriteJson(path, this);
    }
}

public static class Catalog
{
    // Standard spawns checked against the Official EFT Wiki on 2026-09-15.
    // Seasonal event spawns are excluded; Cultists retain their time/level conditions.
    public static readonly Dictionary<string,string[]> DefaultBosses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Interchange"]=["bossKilla","bossTagilla"],
        ["bigmap"]=["bossBully","bossPartisan","bossKnight","followerBigPipe","followerBirdEye","sectantPriest"],
        ["Woods"]=["bossKojaniy","bossPartisan","bossKnight","followerBigPipe","followerBirdEye","sectantPriest"],
        ["Shoreline"]=["bossSanitar","bossPartisan","bossKnight","followerBigPipe","followerBirdEye","sectantPriest"],
        ["RezervBase"]=["bossGluhar"],
        ["Lighthouse"]=["bossZryachiy","bossPartisan","bossKnight","followerBigPipe","followerBirdEye"],
        ["Sandbox"]=[],
        ["factory4_day"]=["bossTagilla","sectantPriest"],
        ["factory4_night"]=["bossTagilla","sectantPriest"],
        ["laboratory"]=[]
    };
    public static readonly Dictionary<string, string> Bosses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bossKilla"]="킬라", ["bossTagilla"]="타길라", ["bossBully"]="르샬라",
        ["bossKojaniy"]="슈트르만", ["bossGluhar"]="글루하", ["bossSanitar"]="세니타",
        ["bossKnight"]="나이트", ["followerBigPipe"]="빅 파이프", ["followerBirdEye"]="버드아이",
        ["bossZryachiy"]="즈랴치", ["bossBoar"]="카반", ["bossKolontay"]="콜론타이",
        ["bossPartisan"]="파르티잔", ["sectantPriest"]="컬티스트 사제"
    };
    private static readonly Dictionary<string, string> BossesZh = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bossKilla"]="Killa（基拉）", ["bossTagilla"]="Tagilla（塔吉拉）", ["bossBully"]="Reshala（雷沙拉）",
        ["bossKojaniy"]="Shturman（舒特曼）", ["bossGluhar"]="Glukhar（格鲁哈尔）", ["bossSanitar"]="Sanitar（卫生员）",
        ["bossKnight"]="Knight（骑士）", ["followerBigPipe"]="Big Pipe（大管子）", ["followerBirdEye"]="Birdeye（鸟眼）",
        ["bossZryachiy"]="Zryachiy（邪眼）", ["bossBoar"]="Kaban（卡班）", ["bossKolontay"]="Kollontay（科伦泰）",
        ["bossPartisan"]="Partisan（游击队员）", ["sectantPriest"]="邪教祭司"
    };
    private static readonly Dictionary<string, string> BossesEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bossKilla"]="Killa", ["bossTagilla"]="Tagilla", ["bossBully"]="Reshala",
        ["bossKojaniy"]="Shturman", ["bossGluhar"]="Glukhar", ["bossSanitar"]="Sanitar",
        ["bossKnight"]="Knight", ["followerBigPipe"]="Big Pipe", ["followerBirdEye"]="Birdeye",
        ["bossZryachiy"]="Zryachiy", ["bossBoar"]="Kaban", ["bossKolontay"]="Kollontay",
        ["bossPartisan"]="Partisan", ["sectantPriest"]="Cultist Priest"
    };
    public static readonly Dictionary<string, string> Maps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Interchange"]="인터체인지", ["bigmap"]="커스텀", ["Woods"]="우드", ["Shoreline"]="쇼어라인",
        ["RezervBase"]="리저브", ["Lighthouse"]="라이트하우스", ["Sandbox"]="그라운드 제로",
        ["factory4_day"]="팩토리", ["factory4_night"]="팩토리", ["laboratory"]="랩"
    };
    private static readonly Dictionary<string, string> MapsZh = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Interchange"]="立交桥", ["bigmap"]="海关", ["Woods"]="森林", ["Shoreline"]="海岸线",
        ["RezervBase"]="储备站", ["Lighthouse"]="灯塔", ["Sandbox"]="中心区",
        ["factory4_day"]="工厂", ["factory4_night"]="工厂", ["laboratory"]="实验室"
    };
    private static readonly Dictionary<string, string> MapsEn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Interchange"]="Interchange", ["bigmap"]="Customs", ["Woods"]="Woods", ["Shoreline"]="Shoreline",
        ["RezervBase"]="Reserve", ["Lighthouse"]="Lighthouse", ["Sandbox"]="Ground Zero",
        ["factory4_day"]="Factory", ["factory4_night"]="Factory", ["laboratory"]="The Lab"
    };
    private static Dictionary<string,string> LocalizedBosses => I18n.IsChinese ? BossesZh : I18n.IsEnglish ? BossesEn : Bosses;
    private static Dictionary<string,string> LocalizedMaps => I18n.IsChinese ? MapsZh : I18n.IsEnglish ? MapsEn : Maps;
    public static string CanonicalMapId(string map) => map.Equals("Sandbox_high",StringComparison.OrdinalIgnoreCase) ? "Sandbox" : map;
    public static string BossName(string role, AppSettings settings) => settings.CustomRoles.FirstOrDefault(x => x.Key.Equals(role, StringComparison.OrdinalIgnoreCase)).Value ?? LocalizedBosses.GetValueOrDefault(role, role);
    public static string MapName(string map)
    {
        string canonical=CanonicalMapId(map);
        return LocalizedMaps.GetValueOrDefault(canonical, canonical);
    }
    public static bool IsBoss(string role, AppSettings settings) => Bosses.ContainsKey(role) || settings.CustomRoles.Keys.Any(x=>x.Equals(role,StringComparison.OrdinalIgnoreCase)) || role.StartsWith("boss", StringComparison.OrdinalIgnoreCase);
}

public static class Paths
{
    public static string Canonical(string path) => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))).TrimEnd(Path.DirectorySeparatorChar);
    public static void RejectLinks(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current != null)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(I18n.T("정션·심볼릭 링크 경로는 지원하지 않습니다.", "不支持联接或符号链接路径。"));
            current = current.Parent;
        }
    }
    public static string ValidateGame(string path)
    {
        string root = Canonical(path);
        if (root.StartsWith(@"\\") || new DriveInfo(Path.GetPathRoot(root)!).DriveType != DriveType.Fixed) throw new IOException(I18n.T("로컬 고정 드라이브의 게임 폴더를 선택해 주세요.", "请选择本地固定磁盘上的游戏文件夹。"));
        RejectLinks(root);
        if (!File.Exists(Path.Combine(root, "EscapeFromTarkov.exe"))) throw new IOException(I18n.T("EscapeFromTarkov.exe가 있는 게임 폴더를 선택해 주세요.", "请选择包含 EscapeFromTarkov.exe 的游戏文件夹。"));
        string config = Path.Combine(root, "Logging.config");
        RejectFileLink(config);
        if (!File.Exists(config)) throw new IOException(I18n.T("Logging.config를 찾을 수 없습니다. 게임 설치를 확인해 주세요.", "找不到 Logging.config，请检查游戏安装。"));
        _ = JsonNode.Parse(File.ReadAllText(config)) ?? throw new IOException(I18n.T("로그 설정을 읽을 수 없습니다.", "无法读取日志配置。"));
        RejectLinks(Path.Combine(root, "Logs"));
        return root;
    }
    public static void RejectFileLink(string path)
    {
        RejectLinks(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException(I18n.T("링크 파일은 처리하지 않습니다.", "不会处理链接文件。"));
    }
    public static List<string> Discover(string saved = "")
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try { string p = Canonical(path); if (File.Exists(p)) p = Path.GetDirectoryName(p)!; candidates.Add(p); } catch { }
        }
        Add(saved);
        foreach (var baseDir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
        {
            string launcher = Path.Combine(baseDir, "Battlestate Games", "BsgLauncher");
            foreach (string filename in new[] { "settings", "settings.json" })
            {
                string file = Path.Combine(launcher, filename);
                try
                {
                    if (!File.Exists(file) || new FileInfo(file).Length > 2_000_000) continue;
                    var node = JsonNode.Parse(File.ReadAllText(file));
                    // Only local path fields are inspected. Authentication fields are never retained or exported.
                    void Walk(JsonNode? item)
                    {
                        if (item is JsonObject obj)
                            foreach (var kv in obj)
                            {
                                if (kv.Key.Contains("path", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains("dir", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains("folder", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (kv.Value is JsonValue v && v.TryGetValue<string>(out var value)) { Add(value); try { Add(Path.Combine(value, "Escape from Tarkov")); } catch { } }
                                }
                                if (kv.Value is JsonObject or JsonArray) Walk(kv.Value);
                            }
                        else if (item is JsonArray arr) foreach (var child in arr) Walk(child);
                    }
                    Walk(node);
                }
                catch { }
            }
        }
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key == null) continue;
                foreach (string sub in key.GetSubKeyNames())
                {
                    using var item = key.OpenSubKey(sub);
                    string name = item?.GetValue("DisplayName") as string ?? "";
                    if (!name.Contains("Tarkov", StringComparison.OrdinalIgnoreCase) && !name.Contains("Battlestate", StringComparison.OrdinalIgnoreCase)) continue;
                    Add(item?.GetValue("InstallLocation") as string);
                }
            }
            catch { }
        }
        foreach (var drive in DriveInfo.GetDrives().Where(d=>d.DriveType == DriveType.Fixed && d.IsReady))
        foreach (string relative in new[] { @"Battlestate Games\Escape from Tarkov", @"Games\Escape from Tarkov", @"Escape from Tarkov", @"Program Files\Escape from Tarkov", @"Program Files (x86)\Escape from Tarkov" }) Add(Path.Combine(drive.RootDirectory.FullName, relative));
        return candidates.Where(p=>File.Exists(Path.Combine(p, "EscapeFromTarkov.exe"))).OrderBy(p=>p.Equals(saved,StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(p=>p).ToList();
    }
    public static string TraceStatus(string root)
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(Path.Combine(root,"Logging.config")));
            var rules = node?["rules"]?.AsArray();
            bool ready=rules!=null && new[]{"aiData"}.All(name=>
            {
                var matches=rules.Where(r=>r?["fileName"]?.GetValue<string>()==name).ToList();
                return matches.Count==1 && matches[0]?["minLevel"]?.GetValue<string>()=="Trace";
            });
            return ready ? I18n.T("Trace 설정됨", "Trace 已设置") : I18n.T("Trace 설정 필요", "需要设置 Trace");
        }
        catch { return I18n.T("로그 설정 읽기 실패", "读取日志配置失败"); }
    }
    public static bool LoggingReady(string root)
    {
        try
        {
            var rules=JsonNode.Parse(File.ReadAllText(Path.Combine(root,"Logging.config")))?["rules"]?.AsArray();
            if(rules==null)return false;
            string[] levels=["Trace","Debug","Information","Warning","Error","Critical"];
            foreach(var desired in new Dictionary<string,int> { ["aiData"]=0,["application"]=1,["backend"]=2 })
            {
                var matching=rules.Where(r=>r?["fileName"]?.GetValue<string>()==desired.Key).ToList();
                if(matching.Count!=1)return false;
                int actual=Array.IndexOf(levels,matching[0]?["minLevel"]?.GetValue<string>()??"");
                if(actual<0 || actual>desired.Value)return false;
            }
            return true;
        }
        catch{return false;}
    }
}
