namespace StarGen.Core.Epoch;

/// <summary>Polity-specific sim state beside the common actor substrate:
/// founding species and the investment treasuries Allocation accrues and
/// spends. Registry in SimState.Polities, actor-id order (P6). The treasuries
/// are budget-weighted shares of stub port income until Markets (slice D)
/// replaces the income source.</summary>
public sealed class PolityRecord
{
    public int ActorId { get; }
    public int SpeciesId { get; }
    /// <summary>Accrued expansion budget; colony foundings consume it.</summary>
    public double ExpansionPoints { get; set; }
    /// <summary>Accrued development budget; lanes and port tier raises consume it.</summary>
    public double DevelopmentPoints { get; set; }

    public PolityRecord(int actorId, int speciesId)
    {
        ActorId = actorId;
        SpeciesId = speciesId;
    }
}
