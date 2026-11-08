namespace BossMonitor;

// Presentation state only. Never infer deaths or spawn decisions from silence.
internal sealed class CardChangeTracker
{
    internal enum ChangeKind { Activated, Death, Planned, NotPlanned }
    internal record Change(ChangeKind Kind, DateTimeOffset At);
    private record Snapshot(string Status,int Count,int Dead,int PlannedWaves=0);
    internal static readonly TimeSpan HoldDuration=TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan Duration=TimeSpan.FromSeconds(8);
    private readonly Dictionary<string,Snapshot> previous=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,Change> changes=new(StringComparer.OrdinalIgnoreCase);
    private string? raidKey;

    internal void Observe(RaidState raid,DateTimeOffset now)
    {
        bool baseline=raidKey!=raid.Key;
        if(baseline){raidKey=raid.Key;previous.Clear();changes.Clear();}
        if(raid.Ended || raid.Map=="")changes.Clear();
        foreach(var key in changes.Where(p=>now-p.Value.At>=Duration).Select(p=>p.Key).ToList())changes.Remove(key);

        foreach(var (key,current) in Snapshots(raid))
        {
            var before=previous.GetValueOrDefault(key)??new Snapshot("Unknown",0,0);
            previous[key]=current;
            if(baseline || raid.Ended || raid.Map=="")continue;
            ChangeKind? kind=current.Count>before.Count?ChangeKind.Activated
                :current.Dead>before.Dead?ChangeKind.Death
                :current.PlannedWaves>before.PlannedWaves?ChangeKind.Planned
                :current.Count==0 && current.Status!=before.Status?current.Status switch
                {
                    "Planned"=>ChangeKind.Planned,
                    "NotPlanned"=>ChangeKind.NotPlanned,
                    _=>null
                }:null;
            if(kind is {} value)changes[key]=new(value,now);
        }
    }

    internal Change? Recent(string key,DateTimeOffset now)
        =>changes.TryGetValue(key,out var change) && now-change.At<Duration?change:null;

    private static IEnumerable<(string Key,Snapshot State)> Snapshots(RaidState raid)
    {
        foreach(var (role,state) in raid.Bosses)yield return ($"boss:{role}",Of(state));
        var pmc = PmcCardSummary.From(raid);
        yield return (PmcCardSummary.CardKey, new(pmc.Status, pmc.Count, pmc.Dead, pmc.PlannedWaves));
        foreach(var (role,state) in raid.SpecialAis)yield return ($"special:{role}",Of(state));
        yield return ("scavs",Of(raid.Scavs));
        yield return ("event:infectedScreamer",Of(raid.InfectedScreamer));
        yield return ("event:infectedTagilla",Of(raid.InfectedTagilla));
    }
    private static Snapshot Of(BossState state)=>new(state.Status,state.Count,Dead(state.LifeInstances));
    private static int Dead(List<BossInstance> instances)=>instances.Count(i=>i.LifeStatus=="DeadConfirmed" && i.DiedAt!=null);
}
