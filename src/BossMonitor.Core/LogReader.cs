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
    // PMC_DIFFICULTY_BEGIN: PMC difficulty classification
    public string PmcDifficulty { get; set; } = "";
    // PMC_DIFFICULTY_END
    public string Position { get; set; } = "";
    public string Zone { get; set; } = "";
    public DateTime SpawnedAt { get; set; }
    public string LifeStatus { get; set; } = "AliveEstimated";
    public DateTime? DiedAt { get; set; }
    public string DeathPosition { get; set; } = "";
}
public class BossState
{
    public string Role { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    public DateTime? At { get; set; }
    public string Position { get; set; } = "";
    public string Zone { get; set; } = "";
    public int? SpawnChance { get; set; }
    public List<BossInstance> LifeInstances { get; set; } = [];
    public int Count => LifeInstances.Count;
}
public class PmcState
{
    public string Role { get; set; } = "";
    public int PlannedWaves { get; set; }
    public int NotPlannedWaves { get; set; }
    public int InitialPlannedWaves { get; set; }
    public int AdditionalPlannedWaves { get; set; }
    public DateTime? FirstSpawnedAt { get; set; }
    public List<BossInstance> LifeInstances { get; set; } = [];
    public int Count => LifeInstances.Count;
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
    public DateTime? GameDateTime { get; set; }
    public double GameTimeFactor { get; set; } = 7;
    public TimeSpan? RaidDuration { get; set; }
    public int PlayerKills { get; set; }
    public Dictionary<string,BossState> Bosses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,BossState> Followers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,PmcState> Pmcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,BossState> SpecialAis { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public BossState Scavs { get; set; } = new() { Role="assault" };
    // HALLOWEEN_EVENT_BEGIN
    public BossState InfectedScreamer { get; set; } = new() { Role="infectedScreamer" };
    public BossState InfectedTagilla { get; set; } = new() { Role="infectedTagilla" };
    // HALLOWEEN_EVENT_END
    public BossState? Btr { get; set; }
    public List<DateTime> BtrReadyAt { get; set; } = [];
    public List<DateTime> BtrTaxiAt { get; set; } = [];
}

public class LogReader
{
    private class Cursor
    {
        public long Offset, Line;
        public DateTime Created;
        public DateTime? LastTimestamp;
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
    public RaidState Current => Raids.Count==0 ? new() : Raids[^1];
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
        bool hasCanonicalAiAndApplication=files.Any(p=>Path.GetFileName(p).Contains(" aiData_")) && files.Any(p=>Path.GetFileName(p).Contains(" application_"));
        // GameDateTime and the raid timer are only written to output.log. Always read it,
        // but when canonical logs exist only accept its time records to avoid duplicating
        // the AI events mirrored there.
        files.AddRange(all.Where(p=>Regex.IsMatch(Path.GetFileName(p), @" output_\d+\.log$")));
        files=files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
                    if(line.Length>=23 && DateTime.TryParseExact(line.AsSpan(0,23),"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var lineTime))
                        cursor.LastTimestamp=lineTime;
                    if (line.Contains("|aiData|") && (line.Contains("|Trace|") || line.Contains("Init Boss wave:") || line.Contains("Bot activate")))
                    {
                        HasAiData=true;
                        if(DateTime.TryParseExact(line.AsSpan(0,Math.Min(23,line.Length)),"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var aiTime) && (LatestAiAt==null || aiTime>LatestAiAt)) LatestAiAt=aiTime;
                    }
                    bool timeOnly=hasCanonicalAiAndApplication && Regex.IsMatch(Path.GetFileName(file), @" output_\d+\.log$");
                    var ev=Parse(line,new(file,cursor.Line),timeOnly,cursor.LastTimestamp);
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
    private static LogEvent? Parse(string line, Evidence evidence, bool timeOnly=false,DateTime? inheritedTime=null)
    {
        bool btrReady=line.Contains("Audio.Vehicles.BTR.BtrSoundController:OnReadyToDeparture(BtrReadyToDepartureEvent)",StringComparison.Ordinal);
        bool btrTaxi=line.Contains("Service PlayerTaxi has already been purchased!",StringComparison.Ordinal);
        DateTime time;
        if(line.Length>=24 && DateTime.TryParseExact(line[..23],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsedTime)) time=parsedTime;
        else if(btrReady && inheritedTime is DateTime previousTime) time=previousTime;
        else return null;
        string kind="",value="",extra="",id="",botId="";
        int? spawnChance=null;
        Match m;
        if(btrReady) kind="BtrReady";
        else if(btrTaxi) kind="BtrTaxi";
        else if((m=Regex.Match(line,@"RealDateTime:\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}\s+GameDateTime:(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2})\s+factor:([0-9.]+)")).Success &&
            DateTime.TryParseExact(m.Groups[1].Value,"MM/dd/yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var gameTime))
        {
            kind="GameTime";value=gameTime.ToString("O",CultureInfo.InvariantCulture);extra=m.Groups[2].Value;
        }
        else if((m=Regex.Match(line,@"GameTimer utcNow:\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2} start:(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}) escape:(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2})")).Success &&
            DateTime.TryParseExact(m.Groups[1].Value,"MM/dd/yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var timerStart) &&
            DateTime.TryParseExact(m.Groups[2].Value,"MM/dd/yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var timerEnd))
        {
            kind="RaidTimer";value=Math.Max(0,(timerEnd-timerStart).TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        else if(timeOnly) return null;
        else if(line.Contains("|application|MatchingCompleted:")) kind="Match";
        else if(line.Contains("|application|") && (m=Regex.Match(line,@"RaidId:([^,]+),.*Locations:([^\r\n]+)")).Success)
        {
            kind="Map"; extra=m.Groups[1].Value.Trim();
            // Transit appends visited locations to the route. The last non-empty
            // entry is the map currently being loaded; the first entry is stale
            // after moving to another map.
            string location=m.Groups[2].Value.Split("->",StringSplitOptions.None)
                .Select(x=>x.Trim().TrimEnd(',')).LastOrDefault(x=>x!="")??"";
            value=Catalog.CanonicalMapId(location);
        }
        else if(line.Contains("|application|GameStarted:")) kind="Start";
        else if(line.Contains("|backend|") && line.Contains("---> Request") && line.Contains("client/match/local/end")) kind="End";
        // PMC_DIFFICULTY_BEGIN: link spawn-data IDs to PMC profiles, not runtime bot IDs.
        else if(line.Contains("|aiData|Profile for bot typeA taken") &&
            (m=Regex.Match(line,@"\bdataId:(\d+):\s+profileId:(\w+)\b")).Success)
        {kind="PmcProfileTaken";value=m.Groups[1].Value;id=m.Groups[2].Value;}
        else if(line.Contains("|aiData|Activate bots:") &&
            (m=Regex.Match(line,@"\bType:(pmcBEAR|pmcUSEC)\s+BotDifficulty:(\w+)\s+id:\d+\s+spawnPoints:1\s+id:(\d+)\b",RegexOptions.IgnoreCase)).Success)
        {kind="PmcDifficultySpawn";value=m.Groups[1].Value;extra=m.Groups[2].Value;id=m.Groups[3].Value;}
        // PMC_DIFFICULTY_END
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
        else if(line.Contains("|aiData|AIDATA create") &&
            (m=Regex.Match(line,@"\bPlayer id:1\s+isAI:False\b.*?\bname:(.*?)\s+Standing:")).Success)
        {kind="LocalPlayer";value=m.Groups[1].Value.Trim();}
        else if(line.Contains("|aiData|bot get hit from enemy.") &&
            (m=Regex.Match(line,@"\bbotId:(\d+)\b.*?\sfrom:(.+?)\s*$")).Success)
        {kind="Hit";botId=m.Groups[1].Value;value=m.Groups[2].Value.Trim();}
        else if(line.Contains("|aiData|") && (m=Regex.Match(line,@"\bbot dispose:(\d+)\b")).Success)
        {kind="Dispose";botId=m.Groups[1].Value;}
        else if(line.Contains("|aiData|") && (m=Regex.Match(line,@"\bBotDied pos:\(([^)]+)\)")).Success)
        {kind="Died";extra=m.Groups[1].Value;}
        else if(line.Contains("|aiData|Group set boss.") &&
            (m=Regex.Match(line,@"\bgroupName:(\S+)\s+\[Boss\]\s+botBoss\.Id:(\d+)\s+bossType:(\w+)\b")).Success)
        {
            // EFT 1.2 stopped emitting the later "Add enemy ... cause:initial"
            // row for bosses. This row identifies the selected zone and bot directly.
            kind="Zone";extra=m.Groups[1].Value;botId=m.Groups[2].Value;value=m.Groups[3].Value;
        }
        else if(line.Contains("|aiData|Add enemy") && (m=Regex.Match(line,@"groupId:(Zone\w+)\s+\[Boss\]\s+_defWildSpawnType:(\w+)\s+cause:initial\s")).Success)
        { kind="Zone";value=m.Groups[2].Value;extra=m.Groups[1].Value; }
        if(kind=="") return null;
        string key=$"{time:O}|{kind}|{value}|{extra}|{id}|{botId}|{spawnChance}";
        // Multiple PMC waves can have identical roles, chances, decisions, and timestamps.
        // Preserve each canonical log row so their wave counts are not collapsed.
        if(kind=="Plan" && IsPmc(value)) key+=$"|{evidence.File}|{evidence.Line}";
        // Keep death pairs in their original file; output can duplicate aiData records.
        if(kind is "Dispose" or "Died") key+=$"|{evidence.File}|{evidence.Line}";
        // PMC_DIFFICULTY_BEGIN: preserve separate profile and spawn rows for safe pairing.
        if(kind is "PmcProfileTaken" or "PmcDifficultySpawn") key+=$"|{evidence.File}|{evidence.Line}";
        // PMC_DIFFICULTY_END
        return new(time,kind,value,extra,id,evidence,key,botId,spawnChance);
    }
    private static bool IsPmc(string role) => role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase) || role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<BossInstance> LifeInstances(RaidState raid)
        => raid.Bosses.Values.SelectMany(b=>b.LifeInstances).Concat(raid.Followers.Values.SelectMany(f=>f.LifeInstances)).Concat(raid.Pmcs.Values.SelectMany(p=>p.LifeInstances))
            .Concat(raid.SpecialAis.Values.SelectMany(s=>s.LifeInstances))
            .Concat(raid.Scavs.LifeInstances)
            // HALLOWEEN_EVENT_BEGIN
            .Concat(raid.InfectedScreamer.LifeInstances)
            .Concat(raid.InfectedTagilla.LifeInstances)
            // HALLOWEEN_EVENT_END
            .Concat(raid.Btr?.LifeInstances??[]);
    private List<RaidState> Rebuild(AppSettings settings)
    {
        List<RaidState> raids=[];
        RaidState current=new();
        var pendingDeaths=new Dictionary<string,LogEvent>(StringComparer.OrdinalIgnoreCase);
        var activeProfiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var lastHitByBot=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        // PMC_DIFFICULTY_BEGIN: temporary per-raid profile/difficulty associations.
        RaidState? difficultyRaid=null;
        var difficultyProfiles=new Dictionary<(string File,string DataId),LogEvent>();
        var profileDifficulties=new Dictionary<string,(string Role,string Difficulty)>(StringComparer.OrdinalIgnoreCase);
        // PMC_DIFFICULTY_END
        string localPlayer="";
        var ordered=events.OrderBy(e=>e.Time).ThenBy(e=>e.Kind=="Match"?0:e.Kind=="Map"?1:2).ThenBy(e=>e.Evidence.File,StringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Evidence.Line).ToList();
        foreach(var ev in ordered)
        {
            if(ev.Kind=="Match")
            {
                current.Ended=true;current.EndedAt=ev.Time;
                pendingDeaths.Clear();
                activeProfiles.Clear();
                lastHitByBot.Clear();
                localPlayer="";
                current=new() { Boundary=ev.Time,Key=$"{Session}|{ev.Time:O}" }; raids.Add(current); continue;
            }
            if(raids.Count==0) { current=new() { Boundary=ev.Time,Key=$"{Session}|{ev.Time:O}" }; raids.Add(current); }
            // PMC_DIFFICULTY_BEGIN: discard associations at raid/map boundaries.
            if(!ReferenceEquals(difficultyRaid,current))
            {difficultyRaid=current;difficultyProfiles.Clear();profileDifficulties.Clear();}
            if(ev.Kind=="PmcProfileTaken")
            {
                if(!current.Ended) difficultyProfiles[(ev.Evidence.File,ev.Value)]=ev;
                continue;
            }
            if(ev.Kind=="PmcDifficultySpawn")
            {
                if(!current.Ended && difficultyProfiles.Remove((ev.Evidence.File,ev.Id),out var profile) &&
                    ev.Time>=profile.Time && (ev.Time-profile.Time).TotalSeconds<=2 &&
                    ev.Evidence.Line>profile.Evidence.Line && ev.Evidence.Line-profile.Evidence.Line<=64)
                {
                    var association=(Role:ev.Value,Difficulty:ev.Extra.ToLowerInvariant());
                    if(profileDifficulties.TryGetValue(profile.Id,out var previous) &&
                        (!previous.Role.Equals(association.Role,StringComparison.OrdinalIgnoreCase) || previous.Difficulty!=association.Difficulty))
                    {
                        profileDifficulties[profile.Id]=(Role:"",Difficulty:"");
                        foreach(var instance in current.Pmcs.Values.SelectMany(p=>p.LifeInstances).Where(i=>i.Profile.Equals(profile.Id,StringComparison.OrdinalIgnoreCase)))
                            instance.PmcDifficulty="";
                    }
                    else profileDifficulties[profile.Id]=association;
                }
                continue;
            }
            // PMC_DIFFICULTY_END
            if(ev.Kind=="GameTime")
            {
                // A second GameDateTime is logged in the lobby after extraction. Keep the
                // first value belonging to the active raid so it cannot overwrite raid time.
                if(!current.Ended && current.GameDateTime==null && DateTime.TryParse(ev.Value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var gameTime))
                {
                    current.GameDateTime=gameTime;
                    if(double.TryParse(ev.Extra,NumberStyles.Float,CultureInfo.InvariantCulture,out var factor) && factor>0) current.GameTimeFactor=factor;
                }
                continue;
            }
            if(ev.Kind=="RaidTimer")
            {
                if(!current.Ended && current.RaidDuration==null && double.TryParse(ev.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)) current.RaidDuration=TimeSpan.FromSeconds(seconds);
                continue;
            }
            if(ev.Kind=="Map")
            {
                bool raidChanged=current.RaidId!="" && !current.RaidId.Equals(ev.Extra,StringComparison.OrdinalIgnoreCase);
                bool mapChanged=current.Map!="" && ev.Value!="" && !current.Map.Equals(ev.Value,StringComparison.OrdinalIgnoreCase);
                if(raidChanged || mapChanged)
                {
                    current.Ended=true;current.EndedAt=ev.Time;pendingDeaths.Clear();activeProfiles.Clear();lastHitByBot.Clear();localPlayer="";
                    string key=mapChanged && !raidChanged
                        ? $"{Session}|{ev.Extra}|{ev.Value}|{ev.Time:O}"
                        : $"{Session}|{ev.Extra}";
                    current=new() {Boundary=ev.Time,Key=key};raids.Add(current);
                }
                else if(current.RaidId=="") current.Key=$"{Session}|{ev.Extra}";
                current.Map=ev.Value;current.RaidId=ev.Extra;continue;
            }
            if(ev.Kind=="Start") {current.Started=ev.Time;continue;}
            if(ev.Kind=="End") {current.Ended=true;current.EndedAt=ev.Time;pendingDeaths.Clear();activeProfiles.Clear();lastHitByBot.Clear();localPlayer="";continue;}
            if(ev.Kind=="BtrReady")
            {
                if(!current.Ended && (current.BtrReadyAt.Count==0 || current.BtrReadyAt[^1]!=ev.Time)) current.BtrReadyAt.Add(ev.Time);
                continue;
            }
            if(ev.Kind=="BtrTaxi")
            {
                if(!current.Ended && (current.BtrTaxiAt.Count==0 || current.BtrTaxiAt[^1]!=ev.Time)) current.BtrTaxiAt.Add(ev.Time);
                continue;
            }
            if(ev.Kind=="LocalPlayer" && !current.Ended) {localPlayer=ev.Value;continue;}
            if(ev.Kind=="Hit" && !current.Ended && ev.BotId!="") {lastHitByBot[ev.BotId]=ev.Value;continue;}
            if(ev.Kind=="Active" && !current.Ended && ev.BotId!="")
            {
                activeProfiles[ev.BotId]=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                lastHitByBot.Remove(ev.BotId);
            }
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
                    if(localPlayer!="" && string.Equals(lastHitByBot.GetValueOrDefault(disposal.BotId),localPlayer,StringComparison.OrdinalIgnoreCase))
                        current.PlayerKills++;
                    lastHitByBot.Remove(disposal.BotId);
                    var matches=LifeInstances(current)
                        .Where(i=>i.BotId==disposal.BotId && i.SpawnedAt<=disposal.Time &&
                            activeProfiles.GetValueOrDefault(i.BotId)==i.Profile)
                        .OrderByDescending(i=>i.SpawnedAt).ToList();
                    if(matches.Count>0 && (matches.Count==1 || matches[0].SpawnedAt>matches[1].SpawnedAt))
                    {
                        var instance=matches[0];
                        if(instance.DiedAt==null)
                        {
                            instance.LifeStatus="DeadConfirmed";instance.DiedAt=ev.Time;instance.DeathPosition=ev.Extra;
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
                        if(planned)pmc.InitialPlannedWaves++;
                    }
                    else
                    {
                        if(planned)pmc.AdditionalPlannedWaves++;
                    }
                }
                else if(ev.Kind=="Active")
                {
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!pmc.LifeInstances.Any(i=>i.Profile==instance))
                    {
                        pmc.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time});
                        // PMC_DIFFICULTY_BEGIN: unknown/unsupported difficulties remain unclassified.
                        if(profileDifficulties.TryGetValue(instance,out var difficulty) &&
                            difficulty.Role.Equals(ev.Value,StringComparison.OrdinalIgnoreCase) &&
                            difficulty.Difficulty is "easy" or "normal" or "hard" or "impossible")
                            pmc.LifeInstances[^1].PmcDifficulty=difficulty.Difficulty;
                        // PMC_DIFFICULTY_END
                        if(pmc.FirstSpawnedAt==null)pmc.FirstSpawnedAt=ev.Time;
                    }
                }
                continue;
            }
            // Grouped Scavs use a separate role but share the same life counts.
            if((ev.Value.Equals("assault",StringComparison.OrdinalIgnoreCase) || ev.Value.Equals("assaultGroup",StringComparison.OrdinalIgnoreCase)) && !current.Ended)
            {
                if(ev.Kind=="Active")
                {
                    var scav=current.Scavs;
                    if(scav.Status!="Confirmed") {scav.At=ev.Time;scav.Position=ev.Extra;}
                    scav.Status="Confirmed";
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!scav.LifeInstances.Any(i=>i.Profile==instance))
                        scav.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,Position=ev.Extra,SpawnedAt=ev.Time});
                }
                continue;
            }
            // HALLOWEEN_EVENT_BEGIN
            if((ev.Value.Equals("infectedScreamer",StringComparison.OrdinalIgnoreCase) || ev.Value.Equals("infectedTagilla",StringComparison.OrdinalIgnoreCase)) && !current.Ended)
            {
                bool tagilla=ev.Value.Equals("infectedTagilla",StringComparison.OrdinalIgnoreCase);
                var infected=tagilla?current.InfectedTagilla:current.InfectedScreamer;
                // The boss has a single spawn decision. Screamer wave plans are
                // not individual activations and must not inflate its count.
                if(ev.Kind=="Plan" && tagilla)
                {
                    infected.SpawnChance=ev.SpawnChance;
                    if(infected.Status!="Confirmed")
                    {
                        infected.Status=ev.Extra=="True"?"Planned":"NotPlanned";
                        infected.At=ev.Time;
                    }
                }
                else if(ev.Kind=="Active")
                {
                    if(infected.Status!="Confirmed") {infected.At=ev.Time;infected.Position=ev.Extra;}
                    infected.Status="Confirmed";
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!infected.LifeInstances.Any(i=>i.Profile==instance))
                        infected.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,Position=ev.Extra,SpawnedAt=ev.Time});
                }
                continue;
            }
            // HALLOWEEN_EVENT_END
            if(ev.Value.Equals("shooterBTR",StringComparison.OrdinalIgnoreCase) && !current.Ended)
            {
                if(ev.Kind=="Active")
                {
                    var btr=current.Btr??=new() {Role=ev.Value};
                    if(btr.Status!="Confirmed") {btr.At=ev.Time;btr.Position=ev.Extra;}
                    btr.Status="Confirmed";
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!btr.LifeInstances.Any(i=>i.Profile==instance))
                        btr.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time});
                }
                continue;
            }
            if(Catalog.IsSpecialAi(ev.Value) && !current.Ended)
            {
                if(ev.Kind=="Active")
                {
                    string specialRole=Catalog.CanonicalSpecialAiRole(ev.Value);
                    if(!current.SpecialAis.TryGetValue(specialRole,out var special)) current.SpecialAis[specialRole]=special=new() {Role=specialRole};
                    if(special.Status!="Confirmed") {special.At=ev.Time;special.Position=ev.Extra;}
                    special.Status="Confirmed";
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!special.LifeInstances.Any(i=>i.Profile==instance))
                        special.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,Position=ev.Extra,SpawnedAt=ev.Time});
                }
                continue;
            }
            if(Catalog.FollowerParent(ev.Value)!="" && !current.Ended)
            {
                if(ev.Kind=="Active")
                {
                    if(!current.Followers.TryGetValue(ev.Value,out var follower)) current.Followers[ev.Value]=follower=new() {Role=ev.Value};
                    if(follower.Status!="Confirmed") {follower.At=ev.Time;follower.Position=ev.Extra;}
                    follower.Status="Confirmed";
                    string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                    if(!follower.LifeInstances.Any(i=>i.Profile==instance))
                        follower.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time});
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
                        planned.At=ev.Time;planned.SpawnChance=ev.SpawnChance;
                    }
                }
                continue;
            }
            if(!current.Bosses.TryGetValue(ev.Value,out var boss)) current.Bosses[ev.Value]=boss=new() { Role=ev.Value };
            if(ev.Kind=="Active")
            {
                if(boss.Status!="Confirmed") {boss.At=ev.Time;boss.Position=ev.Extra;}
                boss.Status="Confirmed";
                string instance=ev.Id==""?ev.Time.ToString("O"):ev.Id;
                if(!boss.LifeInstances.Any(i=>i.Profile==instance))
                    boss.LifeInstances.Add(new() {Profile=instance,BotId=ev.BotId,SpawnedAt=ev.Time});
            }
        }
        foreach(var raid in raids)
        {
        if(raid.GameDateTime is DateTime gameTime && raid.Bosses.TryGetValue("sectantPriest",out var cultist) &&
            cultist.Status=="Planned" && !CultistWindowOverlapsRaid(gameTime,raid.GameTimeFactor,raid.RaidDuration))
            cultist.Status="NotPlanned";
        foreach(var boss in raid.Bosses.Values.Where(b=>b.Status=="Confirmed"))
        {
            if(raid.Ended)
                foreach(var instance in boss.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
            DateTime boundaryEnd=raids.FirstOrDefault(r=>r.Boundary>raid.Boundary)?.Boundary??DateTime.MaxValue;
            var zones=ordered.Where(e=>e.Kind=="Zone" && e.Value.Equals(boss.Role,StringComparison.OrdinalIgnoreCase) && e.Time>=raid.Boundary && e.Time<boundaryEnd &&
                (e.BotId!="" ? boss.LifeInstances.Any(i=>i.BotId==e.BotId) : Math.Abs((e.Time-boss.At!.Value).TotalSeconds)<=2))
                .Select(e=>e.Extra).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            boss.Zone=zones.Count==1?zones[0]:"";
        }
        if(raid.Btr is BossState btr)
        {
            if(raid.Ended)
                foreach(var instance in btr.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
            DateTime boundaryEnd=raids.FirstOrDefault(r=>r.Boundary>raid.Boundary)?.Boundary??DateTime.MaxValue;
            var zones=ordered.Where(e=>e.Kind=="Zone" && e.Value.Equals("shooterBTR",StringComparison.OrdinalIgnoreCase) &&
                e.Time>=raid.Boundary && e.Time<boundaryEnd && e.BotId!="" && btr.LifeInstances.Any(i=>i.BotId==e.BotId))
                .Select(e=>e.Extra).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            btr.Zone=zones.Count==1?zones[0]:"";
        }
        foreach(var special in raid.SpecialAis.Values)
        {
            DateTime boundaryEnd=raids.FirstOrDefault(r=>r.Boundary>raid.Boundary)?.Boundary??DateTime.MaxValue;
            foreach(var instance in special.LifeInstances)
            {
                var zones=ordered.Where(e=>e.Kind=="Zone" && Catalog.CanonicalSpecialAiRole(e.Value).Equals(special.Role,StringComparison.OrdinalIgnoreCase) &&
                    e.Time>=raid.Boundary && e.Time<boundaryEnd && e.BotId!="" && e.BotId==instance.BotId)
                    .Select(e=>e.Extra).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                instance.Zone=zones.Count==1?zones[0]:"";
            }
        }
        if(raid.Ended)
            foreach(var instance in raid.Followers.Values.SelectMany(f=>f.LifeInstances).Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        if(raid.Ended)
            foreach(var instance in raid.Pmcs.Values.SelectMany(p=>p.LifeInstances).Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        if(raid.Ended)
            foreach(var instance in raid.SpecialAis.Values.SelectMany(s=>s.LifeInstances).Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        if(raid.Ended)
            foreach(var instance in raid.Scavs.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        // HALLOWEEN_EVENT_BEGIN
        if(raid.Ended)
            foreach(var instance in raid.InfectedScreamer.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        if(raid.Ended)
            foreach(var instance in raid.InfectedTagilla.LifeInstances.Where(i=>i.DiedAt==null)) instance.LifeStatus="DeathUnconfirmedAtEnd";
        // HALLOWEEN_EVENT_END
        }
        return raids;
    }
    private static bool CultistWindowOverlapsRaid(DateTime gameTime,double factor,TimeSpan? raidDuration)
    {
        var time=gameTime.TimeOfDay;
        if(time>=TimeSpan.FromHours(22) || time<TimeSpan.FromHours(7)) return true;
        if(raidDuration==null || factor<=0) return false;
        var gameSpan=TimeSpan.FromTicks((long)Math.Min(TimeSpan.MaxValue.Ticks,raidDuration.Value.Ticks*factor));
        return gameSpan>=TimeSpan.FromHours(22)-time;
    }
    public void Reevaluate(AppSettings settings) => Raids=Rebuild(settings);
}
