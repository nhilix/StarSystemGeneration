using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Polity-specific sim state beside the common actor substrate:
/// founding species, the credit ledger, strategic reserves, and the
/// investment treasuries Allocation accrues and spends. Registry in
/// SimState.Polities, actor-id order (P6). Slice D: real market income
/// (transaction tax + tariffs + state facility revenue) replaces the slice-B
/// stub as the treasuries' source.</summary>
public sealed class PolityRecord
{
    public int ActorId { get; }
    public int SpeciesId { get; }
    /// <summary>The credit ledger — conserved (P4): endowed once at entry,
    /// then moved only by transactions, taxes, and loans.</summary>
    public double Credits { get; set; }
    /// <summary>This epoch's market receipts (taxes, payouts, tariffs) —
    /// written by the Markets phase, consumed by the same epoch's Allocation
    /// as the budget base (development is deficit-financed when the balance
    /// runs negative). Step-transient: never serialized.</summary>
    public double Receipts { get; set; }
    /// <summary>Accrued expansion budget; colony foundings consume it.</summary>
    public double ExpansionPoints { get; set; }
    /// <summary>Accrued development budget; lanes, port tier raises, and
    /// facility construction consume it.</summary>
    public double DevelopmentPoints { get; set; }
    /// <summary>Strategic reserve stock per good — held against policy
    /// targets, buffering sieges and famines (economy/markets.md
    /// §Stockpiles). Polity-aggregate in slice D; wars make it spatial (H).</summary>
    public double[] ReserveQty { get; } = new double[Goods.All.Count];
    /// <summary>Mean grade of the reserve stock per good (0 when empty).</summary>
    public double[] ReserveGrade { get; } = new double[Goods.All.Count];

    public PolityRecord(int actorId, int speciesId)
    {
        ActorId = actorId;
        SpeciesId = speciesId;
    }
}
