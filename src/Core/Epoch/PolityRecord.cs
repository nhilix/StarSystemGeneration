using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Polity-specific sim state beside the common actor substrate:
/// founding species, the credit ledger, strategic reserves, and the
/// investment treasuries Allocation accrues and spends. Registry in
/// SimState.Polities, actor-id order (P6). Slice D: real market income
/// (transaction tax + tariffs + state facility revenue) replaces the slice-B
/// stub as the treasuries' source.</summary>
public sealed class PolityRecord : ICreditLedger
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
    /// <summary>Accrued military budget; yard hull production consumes it
    /// (slice E — the Budget.Military share stops idling in Credits).</summary>
    public double MilitaryPoints { get; set; }
    /// <summary>Hulls ever laid down (yards + genesis starter fleets) — the
    /// conservation ledger: Built == active + Wrecked + Scrapped, always (P4).</summary>
    public int HullsBuilt { get; set; }
    /// <summary>Hulls lost to attrition and battle — each one has a
    /// wreckage record at a real hex.</summary>
    public int HullsWrecked { get; set; }
    /// <summary>Hulls deliberately broken up (colony ships become the
    /// colony; partial alloy recovery lands with salvage).</summary>
    public int HullsScrapped { get; set; }
    /// <summary>Strategic reserve stock per good — held against policy
    /// targets, buffering sieges and famines (economy/markets.md
    /// §Stockpiles). Polity-aggregate in slice D; wars make it spatial (H).</summary>
    public double[] ReserveQty { get; } = new double[Goods.All.Count];
    /// <summary>Mean grade of the reserve stock per good (0 when empty).</summary>
    public double[] ReserveGrade { get; } = new double[Goods.All.Count];

    /// <summary>Starting-kit quality from the emergence schedule (slice F):
    /// maturation richness plus the late-emerger contact bonus. Slice G
    /// converts it into starting Astrogation/Industrial tech tiers (its
    /// design intent) — latecomers are behind, not hopeless.</summary>
    public double EntryGradeBonus { get; set; }

    /// <summary>Last step's realized receipts per world-year — the trailing
    /// income rate the capability brief plans against (spec §2, P3:
    /// deliberately hindsight, never clairvoyance).</summary>
    public double LastIncomePerYear { get; set; }
    /// <summary>War-economy readiness 0..1: raised by fed Mobilization
    /// projects, decays at peace (spec §5).</summary>
    public double Mobilization { get; set; }

    /// <summary>Per-domain tech tier, indexed by <see cref="TechDomain"/> —
    /// the qualitative ladder (slice G). Seeded at entry; ceilings and
    /// regions derive via <see cref="Tech"/>.</summary>
    public int[] TechTier { get; } =
        { Tech.EraStandardTier, Tech.EraStandardTier,
          Tech.EraStandardTier, Tech.EraStandardTier };
    /// <summary>Accumulated research toward each domain's next tier.</summary>
    public double[] TechProgress { get; } = new double[4];

    /// <summary>The polity's inside (slice G): form, official ideology,
    /// legitimacy/cohesion/enforcement. Seated at entry, null before.</summary>
    public PolityInterior? Interior { get; set; }

    public PolityRecord(int actorId, int speciesId)
    {
        ActorId = actorId;
        SpeciesId = speciesId;
    }
}
