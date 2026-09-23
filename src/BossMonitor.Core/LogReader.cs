using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BossMonitor.Core;

public record Evidence(string File, long Line);
public record LogEvent(DateTime Time, string Kind, string Value, string Extra, string Id, Evidence Evidence, string Key, string BotId = "", int? SpawnChance = null);
public class BossInstance
{
    public string Profile { get; set; } = "";
    public string BotId { get; set; } = "";
    public DateTime SpawnedAt { get; set; }
    public string LifeStatus { get; set; } = "AliveEstimated";
    public DateTime? DiedAt { get; set; }
    public Evidence? SpawnEvidence { get; set; }
    public Evidence? DisposeEvidence { get; set; }
    public Evidence? DeathEvidence { get; set; }
}
public class BossState
{
    public string Role { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public DateTime? At { get; set; }
    public string Position { get; set; } = "";
    public string Zone { get; set; } = "";
    public string Profile { get; set; } = "";
    public int? SpawnChance { get; set; }
    public int Count { get; set; }
    public Evidence? Evidence { get; set; }
    public List<string> Instances { get; set; } = [];
    public List<BossInstance> LifeInstances { get; set; } = [];
}
public class PmcState
{
    public string Role { get; set; } = "";
    public int PlannedWaves { get; set; }
    public int NotPlannedWaves { get; set; }
    public int InitialPlannedWaves { get; set; }
    public int InitialNotPlannedWaves { get; set; }
    public int AdditionalPlannedWaves { get; set; }
    public int AdditionalNotPlannedWaves { get; set; }
    public List<int> SpawnChances { get; set; } = [];
    public DateTime? LastPlanAt { get; set; }
    public DateTime? FirstSpawnedAt { get; set; }
    public string FirstPosition { get; set; } = "";
    public int Count { get; set; }
    public List<BossInstance> LifeInstances { get; set; } = [];
    public int TotalWaves => PlannedWaves + NotPlannedWaves;
    public string Status => Count > 0 ? "Confirmed" : PlannedWaves > 0 ? "Planned" : TotalWaves > 0 ? "NotPlanned" : "Unknown";
}
public class RaidState
{
    public string Key { get; set; } = "";
    public string Map { get; set; } = "";
    public string RaidId { get; set; } = "";
    public DateTime Boundary { get; set; }
    public DateTime? Started { get; set; }
    public bool Ended { get; set; }
    public DateTime? EndedAt { get; set; }
    public Dictionary<string,BossState> Bosses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,PmcState> Pmcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class LogReader
{
    private class Cursor
    {
        public long Offset, Line;
        public DateTime Created;
        public string Tail = "";
        public Decoder Decoder = Encoding.UTF8.GetDecoder();
    }
    private readonly Dictionary<string,Cursor> cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LogEvent> events = [];
    private readonly HashSet<string> seen = [];
    public string Session { get; private set; } = "";
    public string Diagnostic { get; private set; } = I18n.T("로그 대기", "等待日志");
    public bool HasAiData { get; private set; }
    public DateTime? LatestAiAt { get; private set; }
    public IReadOnlyList<RaidState> Raids { get; private set; } = [];
    public RaidState Current => Raids.LastOrDefault() ?? new();
    public HashSet<string> ObservedRoles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Reset()
    {
        cursors.Clear(); events.Clear(); seen.Clear(); Raids=[]; Session=""; HasAiData=false; LatestAiAt=null;
    }
    public static DateTime SessionTime(string path)
    {
        var m = Regex.Match(Path.GetFileName(path), @"^log_(\d{4}\.\d{2}\.\d{2}_\d{1,2}-\d{2}-\d{2})_");
        return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy.MM.dd_H-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : DateTime.MinValue;
    }
    public void Poll(string gameRoot, AppSettings settings)
    {
        string logs = Path.Combine(gameRoot,"Logs");
        if (!Directory.Exists(logs)) { Diagnostic=I18n.T("게임 로그 생성 대기", "等待游戏生成日志"); return; }
        var latest = Directory.EnumerateDirectories(logs).Where(p=>SessionTime(p)!=DateTime.MinValue).OrderByDescending(SessionTime).FirstOrDefault();
        if (latest == null) { Diagnostic=I18n.T("게임 로그 세션 대기", "等待游戏日志会话"); return; }
        if (!latest.Equals(Session,StringComparison.OrdinalIgnoreCase)) { Reset(); Session=latest; }
        var all = Directory.GetFiles(Session,"*.log");
        var files = all.Where(p=>Regex.IsMatch(Path.GetFileName(p), @" (application|aiData|backend)_\d+\.log$")).ToList();
        if (!files.Any(p=>Path.GetFileName(p).Contains(" aiData_")) || !files.Any(p=>Path.GetFileName(p).Contains(" application_")))
            files.AddRange(all.Where(p=>Regex.IsMatch(Path.GetFileName(p), @" output_\d+\.log$")));
        bool replay = files.Any(f=>cursors.TryGetValue(f,out var c) && (new FileInfo(f).Length<c.Offset || File.GetCreationTimeUtc(f)!=c.Created));
        if (replay) { Reset(); Session=latest; }
        bool changed=false;
        // Read a bounded byte budget each tick; initial large sessions do not freeze the UI.
        int budget=8*1024*1024;
        foreach (string file in files.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase))
        {
            if (budget<=0) break;
            if (!cursors.TryGetValue(file,out var cursor)) cursors[file]=cursor=new Cursor { Created=File.GetCreationTimeUtc(file) };
            using var stream = new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            stream.Seek(cursor.Offset,SeekOrigin.Begin);
            byte[] buffer = new byte[65536]; char[] chars=new char[65536];
            int count;
            while (budget>0 && (count=stream.Read(buffer,0,Math.Min(buffer.Length,budget)))>0)
            {
                budget-=count; cursor.Offset+=count;
                int n=cursor.Decoder.GetChars(buffer,0,count,chars,0,false);
                var lines=(cursor.Tail+new string(chars,0,n)).Split('\n');
                cursor.Tail=lines[^1];
                for(int i=0;i<lines.Length-1;i++)
                {
                    cursor.Line++;
                    string line=lines[i].TrimEnd('\r');
                    if (line.Contains("|aiData|") && (line.Contains("|Trace|") || line.Contains("Init Boss wave:") || line.Contains("Bot activate")))
                    {
                        HasAiData=true;
                        if(DateTime.TryParseExact(line.AsSpan(0,Math.Min(23,line.Length)),"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var aiTime) && (LatestAiAt==null || aiTime>LatestAiAt)) LatestAiAt=aiTime;
                    }
                    var ev=Parse(line,new(file,cursor.Line));
                    if(ev!=null && seen.Add(ev.Key)) { events.Add(ev); changed=true; if(ev.Kind is "Active" or "Plan") ObservedRoles.Add(ev.Value); }
                }
                // Ignore unbounded malformed lines, which must never exhaust memory.
                if(cursor.Tail.Length>2_000_000) cursor.Tail="";
            }
        }
        if(changed || Raids.Count==0) Raids=Rebuild(settings);
        long pending=files.Sum(f=>Math.Max(0,new FileInfo(f).Length-(cursors.GetValueOrDefault(f)?.Offset??0)));
        Diagnostic=pending>0 ? I18n.T("로그 복원 중…", "正在恢复日志…") : HasAiData ? I18n.T("로그 연결됨 · 상세 AI 기록 확인", "日志已连接 · 已检测到详细 AI 记录") : I18n.T("AI 상세 로그 대기 · 로컬 PvE 지원", "等待详细 AI 日志 · 支持本地 PvE");
    }
    private static LogEvent? Parse(string line, Evidence evidence)
    {
        if(line.Length<24 || !DateTime.TryParseExact(line[..23],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)) return null;
        string kind="",value="",extra="",id="",botId="";
        int? spawnChance=null;
        Match m;
        if(line.Contains("|application|MatchingCompleted:")) kind="Match";
        else if(line.Contains("|application|") && (m=Regex.Match(line,@"RaidId:([^,]+),.*Locations:([^\r\n]+)")).Success)
        {
            kind="Map"; extra=m.Groups[1].Value.Trim();
            // Transit lists the current location before the first arrow; future destinations are not current maps.
            value=Catalog.CanonicalMapId(m.Groups[2].Value.Split("->",StringSplitOptions.None)[0].Trim().TrimEnd(','));
        }
        else if(line.Contains("|application|GameStarted:")) kind="Start";
        else if(line.Contains("|backend|") && line.Contains("---> Request") && line.Contains("client/match/local/end")) kind="End";
        else if(line.Contains("|aiData|") && (m=Regex.Match(line,@"Init Boss wave:(\w+) ShallSpawn:(True|False)\b(?: BossChance:(\d+))?")).Success)
        {
            kind="Plan";value=m.Groups[1].Value;extra=m.Groups[2].Value;
            if(m.Groups[3].Success && int.TryParse(m.Groups[3].Value,CultureInfo.InvariantCulture,out int chance)) spawnChance=chance;
        }
        else if(line.Contains("|aiData|") && line.Contains("Bot activate") && (m=Regex.Match(line,@"\brole:(\w+)\b")).Success)
        {
            kind="Active";value=m.Groups[1].Value;
            var p=Regex.Match(line,@"\bpos:\(([^)]+)\)"); extra=p.Success?p.Groups[1].Value:"";
            var profile=Regex.Match(line,@"\bprofileId:(\w+)"); var bot=Regex.Match(line,@"\bid:(\w+)"); id=profile.Success?profile.Groups[1].Value:bot.Groups[1].Value;
            botId=bot.Success?bot.Groups[1].Value:"";
        }
        else if(line.Contains("|aiData|") && (m=Regex.Match(line,@"\bbot dispose:(\d+)\b")).Success)
        {kind="Dispose";botId=m.Groups[1].Value;}
        else if(line.Contains("|aiData|") && Regex.IsMatch(line,@"\bBotDied pos:\([^)]+\)")) kind="Died";
        else if(line.Contains("|aiData|Add enemy") && (m=Regex.Match(line,@"groupId:(Zone\w+)\s+\[Boss\]\s+_defWildSpawnType:(\w+)\s+cause:initial\s")).Success)
        { kind="Zone";value=m.Groups[2].Value;extra=m.Groups[1].Value; }
        if(kind=="") return null;
        string key=$"{time:O}|{kind}|{value}|{extra}|{id}|{botId}|{spawnChance}";
        // Multiple PMC waves can have identical roles, chances, decisions, and timestamps.
        // Preserve each canonical log row so their wave counts are not collapsed.
        if(kind=="Plan" && IsPmc(value)) key+=$"|{evidence.File}|{evidence.Line}";
        // Keep death pairs in their original file; output can duplicate aiData records.
        if(kind is "Dispose" or "Died") key+=$"|{evidence.File}|{evidence.Line}";
        return new(time,kind,value,extra,id,evidence,key,botId,spawnChance);
    }
    private static bool IsPmc(string role) => role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase) || role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<BossInstance> LifeInstances(RaidState raid)
        => raid.Bosses.Values.SelectMany(b=>b.LifeInstances).Concat(raid.Pmcs.Values.SelectMany(p=>p.LifeInstances));
    private List<RaidState> Rebuild(AppSettings settings)
    {
        List<RaidState> raids=[];
        RaidState current=new();
        var pendingDeaths=new Dictionary<string,LogEvent>(StringComparer.OrdinalIgnoreCase);
        var activeProfiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var ordered=events.OrderBy(e=>e.Time).ThenBy(e=>e.Kind=="Match"?0:e.Kind=="Map"?1:2).ThenBy(e=>e.Evidence.File,StringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Evidence.Line).ToList();
        foreach(var ev in ordered)
        {
            if(ev.Kind=="Match")
            {
                current.Ended=true;current.EndedAt=ev.Time;
                pendingDeaths.Clear();
                activeProfiles.Clear();
                current=new() { Boundary=ev.Time,Key=$"{Session}|{ev.Time:O}" }; raids.Add(current); continue;
            }
            if(raids.Count==0) { current=new() { Boundary=ev.Time,Key=$"{Session}|{ev.Time:O}" }; raids.Add(current); }
            if(ev.Kind=="Map")
            {
                if(current.RaidId!="" && current.RaidId!=ev.Extra)
                { current.Ended=true;current.EndedAt=ev.Time;pendingDeaths.Clear();activeProfiles.Clear();current=new() { Boundary=ev.Time,Key=$"{Session}|{ev.Extra}" };raids.Add(current); }
                current.Map=ev.Value;current.RaidId=ev.Extra;current.Key=$"{Session}|{ev.Extra}";continue;
            }
            if(ev.Kind=="Start") {current.Started=ev.Time;continue;}
            if(ev.Kind=="End") {current.Ended=true;current.EndedAt=ev.Time;pendingDeaths.Clear();activeProfiles.Clear();continue;}
            if(ev.Kind=="Active" && !current.Ended && ev.BotId!="") activeProfiles[ev.BotId]=ev.Id==""?ev.Time.ToString("O"):ev.Id;
            if(ev.Kind=="Dispose")
            {
                if(!current.Ended) pendingDeaths[ev.Evidence.File]=ev;
                continue;
            }
            if(ev.Kind=="Died")
            {
                if(!current.Ended && pendingDeaths.Remove(ev.Evidence.File,out var disposal) &&
                    ev.Time>=disposal.Time && (ev.Time-disposal.Time).TotalSeconds<=1 &&
                    // Grenade deaths can trigger squad/patrol reassignment messages between
                    // bot dispose and BotDied. Keep the tight time/file pairing, but allow
                    // enough intervening AI log rows for that cleanup sequence.
                    ev.Evidence.Line>disposal.Evidence.Line && ev.Evidence.Line-disposal.Evidence.Line<=64)
                {
                    var matches=LifeInstances(current)
                        .Where(i=>i.BotId==disposal.BotId && i.SpawnedAt<=disposal.Time &&
                            activeProfiles.GetValueOrDefault(i.BotId)==i.Profile)
                        .OrderByDescending(i=>i.SpawnedAt).ToList();
                    if(matches.Count>0 && (matches.Count==1 || matches[0].SpawnedAt>matches[1].SpawnedAt))
                    {
                        var instance=matches[0];
                        if(instance.DiedAt==null)
                        {
                            instance.LifeStatus="DeadConfirmed";instance.DiedAt=ev.Time;
                            instance.DisposeEvidence=disposal.Evidence;instance.DeathEvidence=ev.Evidence;
                        }
                    }
                }
                continue;
            }
            if(IsPmc(ev.Value) && !current.Ended)
            {
                if(!current.Pmcs.TryGetValue(ev.Value,out var pmc)) current.Pmcs[ev.Value]=pmc=new() {Role=ev.Value};
                if(ev.Kind=="Plan")
                {
                    bool planned=ev.Extra=="True";
                    if(planned)pmc.PlannedWaves++;else pmc.NotPlannedWaves++;
                    if(current.Started==null)
                    {
                        if(planned)pmc.InitialPlannedWaves++;else pmc.InitialNotPlannedWaves++;
                    }
                    else
                    {
                        if(planned)pmc.AdditionalPlannedWaves++;else pmc.AdditionalNotPlannedWaves++;
                    }
                    if(ev.SpawnChance is int chance)pmc.SpawnChances.Add(chance);
                    pmc.LastPlanAt=ev.Time;
                }
                else if(ev.Kind=="Active")
                {
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!pmc.LifeInstances.Any(i=>i.Profile==instance))
                    {
                        pmc.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time,SpawnEvidence=ev.Evidence});
                        pmc.Count=pmc.LifeInstances.Count;
                        if(pmc.FirstSpawnedAt==null){pmc.FirstSpawnedAt=ev.Time;pmc.FirstPosition=ev.Extra;}
                    }
                }
                continue;
            }
            if(!Catalog.IsBoss(ev.Value,settings) || current.Ended) continue;
            if(ev.Kind=="Plan")
            {
                // Big Pipe and Birdeye are emitted as Knight's escorts, so the game only
                // writes one plan record for the whole Goons group.
                string[] roles=ev.Value.Equals("bossKnight",StringComparison.OrdinalIgnoreCase)
                    ? ["bossKnight","followerBigPipe","followerBirdEye"]
                    : [ev.Value];
                foreach(string role in roles)
                {
                    if(!current.Bosses.TryGetValue(role,out var planned)) current.Bosses[role]=planned=new() { Role=role };
                    if(planned.Status!="Confirmed")
                    {
                        planned.Status=ev.Extra=="True"?"Planned":"NotPlanned";
                        planned.At=ev.Time;planned.SpawnChance=ev.SpawnChance;planned.Evidence=ev.Evidence;
                    }
                }
                continue;
            }
            if(!current.Bosses.TryGetValue(ev.Value,out var boss)) current.Bosses[ev.Value]=boss=new() { Role=ev.Value };
            if(ev.Kind=="Active")
            {
                if(boss.Status!="Confirmed") {boss.At=ev.Time;boss.Position=ev.Extra;boss.Profile=ev.Id;boss.Evidence=ev.Evidence;}
                boss.Status="Confirmed";
                string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                if(!boss.Instances.Contains(instance)) boss.Instances.Add(instance);
                if(!boss.LifeInstances.Any(i=>i.Profile==instance))
                    boss.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time,SpawnEvidence=ev.Evidence});
                boss.Count=boss.Instances.Count;
            }
        }
        foreach(var raid in raids)
        {
        foreach(var boss in raid.Bosses.Values.Where(b=>b.Status=="Confirmed"))
        {
            if(raid.Ended)
                foreach(var instance in boss.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
            DateTime boundaryEnd=raids.FirstOrDefault(r=>r.Boundary>raid.Boundary)?.Boundary??DateTime.MaxValue;
            var zones=ordered.Where(e=>e.Kind=="Zone" && e.Value.Equals(boss.Role,StringComparison.OrdinalIgnoreCase) && e.Time>=raid.Boundary && e.Time<boundaryEnd && Math.Abs((e.Time-boss.At!.Value).TotalSeconds)<=2).Select(e=>e.Extra).Distinct().ToList();
            boss.Zone=zones.Count==1?zones[0]:"";
        }
        if(raid.Ended)
            foreach(var instance in raid.Pmcs.Values.SelectMany(p=>p.LifeInstances).Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        }
        return raids;
    }
    public void Reevaluate(AppSettings settings) => Raids=Rebuild(settings);
}
