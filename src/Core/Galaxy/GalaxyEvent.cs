namespace StarGen.Core.Galaxy;

public enum GalaxyEventType
{
    CellClaimed, WarStarted, CellTaken, LostCapital, PolityExtinct,
    WarEnded, TechAdvance, Famine, TradeBlocked,
}

/// <summary>One record of the single global append-only event log (spec §7 State).</summary>
public sealed class GalaxyEvent
{
    public int Epoch { get; set; }
    public GalaxyEventType Type { get; set; }
    public int ActorPolityId { get; set; }
    public int TargetPolityId { get; set; } = -1;
    public int Q { get; set; }
    public int R { get; set; }
    public double Magnitude { get; set; }
    /// <summary>Type-specific payload (economy spec §4): WarStarted → (int)WarGoal,
    /// WarEnded → (int)WarOutcome, TechAdvance → tier reached. 0 otherwise.</summary>
    public int Detail { get; set; }
}
