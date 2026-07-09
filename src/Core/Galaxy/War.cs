using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

public enum WarGoal { Ore, Exotics, Chokepoint, Punitive }
public enum WarOutcome { Ongoing, AttackerVictory, DefenderVictory, WhitePeace }

/// <summary>Persistent war registry entry (economy spec §4/§6). Ended wars are
/// retained forever, mirroring extinct-polity retention.</summary>
public sealed class War
{
    public int Id { get; set; }
    public int AttackerId { get; set; }
    public int DefenderId { get; set; }
    public int StartEpoch { get; set; }
    public WarGoal Goal { get; set; }
    /// <summary>Initial target cluster (≤3 cells) the victor annexes.</summary>
    public List<HexCoordinate> GoalCells { get; } = new();
    /// <summary>The front: equals GoalCells for the war's entire life (cells may flip
    /// back and forth between belligerents within it, but the set never grows);
    /// contested while live.</summary>
    public List<HexCoordinate> FrontCells { get; } = new();
    public double AttackerWeariness { get; set; }
    public double DefenderWeariness { get; set; }
    public int AttackerCellsLost { get; set; }
    public int DefenderCellsLost { get; set; }
    public bool Ended { get; set; }
    public WarOutcome Outcome { get; set; }
}
