namespace BossMonitor;

// Combine faction records for display while keeping their underlying raid state separate.
internal sealed record PmcCardSummary(
    string Status, int Count, int Dead, int Impossible, int PlannedWaves, bool HasWaveRecords,
    bool AdditionalSpawnPlanned, DateTime? FirstSpawnedAt)
{
    internal const string CardKey = "pmc";
    internal int Alive => Count - Dead;

    internal static PmcCardSummary From(RaidState raid)
    {
        PmcState[] states =
        [
            raid.Pmcs.GetValueOrDefault("pmcBEAR") ?? new(),
            raid.Pmcs.GetValueOrDefault("pmcUSEC") ?? new(),
        ];
        int count = states.Sum(state => state.Count);
        int dead = 0;
        int impossible = 0;
        foreach (var instance in states.SelectMany(state => state.LifeInstances))
        {
            if (instance.LifeStatus == "DeadConfirmed" && instance.DiedAt != null)
                dead++;
            else if (instance.PmcDifficulty == "impossible")
                impossible++;
        }

        int plannedWaves = states.Sum(state => state.PlannedWaves);
        // One faction's negative decision leaves the other faction unknown.
        string status = count > 0 ? "Confirmed"
            : plannedWaves > 0 ? "Planned"
            : states.All(state => state.Status == "NotPlanned") ? "NotPlanned"
            : "Unknown";

        return new(status, count, dead, impossible, plannedWaves,
            states.Any(state => state.TotalWaves > 0),
            states.Any(state => state.AdditionalPlannedWaves > 0),
            states.Min(state => state.FirstSpawnedAt));
    }
}
