namespace StarGen.Core.Galaxy;

public enum GalaxyEventType { CellClaimed, WarStarted, CellTaken, LostCapital, PolityExtinct }

/// <summary>One record of the single global append-only event log (spec §7 State).</summary>
public sealed class GalaxyEvent
{
    public int Epoch { get; set; }
    public GalaxyEventType Type { get; set; }
    public int ActorPolityId { get; set; }
    public int TargetPolityId { get; set; } = -1;
    public int Cx { get; set; }
    public int Cy { get; set; }
    public double Magnitude { get; set; }
}
