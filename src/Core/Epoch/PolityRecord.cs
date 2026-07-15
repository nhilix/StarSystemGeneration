using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Polity-specific sim state beside the common actor substrate:
/// founding species, the credit ledger, and the
/// investment treasuries Allocation accrues and spends. Registry in
/// SimState.Polities, actor-id order (P6). Slice D: real market income
/// (transaction tax + tariffs + state facility revenue) replaces the slice-B
/// stub as the treasuries' source.</summary>
public sealed class PolityRecord : ICreditLedger
{
    public int ActorId { get; }
    public int SpeciesId { get; }
    /// <summary>The one currency this polity mints (slice CU-1) — a polity is
    /// always single-currency. −1 until genesis wiring assigns it (a later
    /// slice task); <see cref="Segment"/>/<see cref="Faction"/> wealth resolves
    /// to this at point of use rather than carrying its own id.</summary>
    public int CurrencyId { get; set; } = -1;
    /// <summary>The credit ledger — conserved (P4): endowed once at entry,
    /// then moved only by transactions, taxes, and loans. Denominated in this
    /// polity's own <see cref="CurrencyId"/>.</summary>
    public double Credits { get; set; }
    /// <summary>This epoch's market receipts (taxes, payouts, tariffs) —
    /// written by the Markets phase, consumed by the same epoch's Allocation
    /// as the budget base (development is deficit-financed when the balance
    /// runs negative). Step-transient: never serialized.</summary>
    public double Receipts { get; set; }
    /// <summary>Principal borrowed THIS epoch specifically — Borrow issues at
    /// the top of Allocation, and this term joins the same epoch's allocation
    /// base so a fresh loan can fund the investment pools (raising future
    /// Receipts) instead of only the non-discretionary bills. Marking just this
    /// epoch's borrowing keeps the accumulated Credits stock out of the base,
    /// which is exactly what the receipts-only base was designed to exclude.
    /// Reset to zero each epoch; step-transient, never serialized.</summary>
    public double BorrowedThisEpoch { get; set; }
    /// <summary>Accrued expansion budget; colony foundings consume it.</summary>
    public double ExpansionPoints { get; set; }
    /// <summary>Accrued development budget; lanes, port tier raises, and
    /// facility construction consume it.</summary>
    public double DevelopmentPoints { get; set; }
    /// <summary>Accrued military budget; yard hull production consumes it
    /// (slice E — the Budget.Military share stops idling in Credits).</summary>
    public double MilitaryPoints { get; set; }
    /// <summary>Accrued reserve budget (stage 2 — Budget.Reserves finally
    /// funds something): polity procurement buys located stock with it, so
    /// the quartermaster no longer competes with a drained credit balance.</summary>
    public double ReservePoints { get; set; }
    /// <summary>Hulls ever laid down (yards + genesis starter fleets) — the
    /// conservation ledger: Built == active + Wrecked + Scrapped, always (P4).</summary>
    public int HullsBuilt { get; set; }
    /// <summary>Hulls lost to attrition and battle — each one has a
    /// wreckage record at a real hex.</summary>
    public int HullsWrecked { get; set; }
    /// <summary>Hulls deliberately broken up (colony ships become the
    /// colony; partial alloy recovery lands with salvage).</summary>
    public int HullsScrapped { get; set; }
    // strategic reserves are LOCATED state now — Port.StockQty/StockGrade,
    // per port, per good (time-and-logistics spec §4b); the polity-aggregate
    // pool died with stage 2

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

    /// <summary>Credit the treasury with <paramref name="amount"/> denominated
    /// in <paramref name="fromCurrencyId"/>, auto-converting into this polity's
    /// own currency on receipt (a polity is single-currency). Returns the
    /// own-currency sum added to <see cref="Credits"/>. A cross-currency receipt
    /// records the transfer: <paramref name="fromCurrencyId"/> leaves circulation,
    /// this polity's currency gains the converted sum.</summary>
    public double Deposit(SimState state, double amount, int fromCurrencyId)
    {
        if (fromCurrencyId == CurrencyId) { Credits += amount; return amount; }
        double own = state.ConvertCurrency(amount, fromCurrencyId, CurrencyId);
        Credits += own;
        state.RecordConversion(fromCurrencyId, amount, CurrencyId, own);
        return own;
    }

    /// <summary>Debit the treasury to provide <paramref name="amount"/>
    /// denominated in <paramref name="toCurrencyId"/>. The polity converts the
    /// needed sum of its own currency out, deducting it from
    /// <see cref="Credits"/> — which may go negative, the existing insolvency
    /// path (<c>Borrow</c> answers a negative balance); a polity does not cap.
    /// Returns the full <paramref name="toCurrencyId"/> amount provided. A
    /// cross-currency payout records the transfer: this polity's own currency
    /// leaves circulation, <paramref name="toCurrencyId"/> gains the amount.</summary>
    public double Withdraw(SimState state, double amount, int toCurrencyId)
    {
        if (amount <= 0) return 0;
        if (toCurrencyId == CurrencyId) { Credits -= amount; return amount; }
        double ownCost = state.ConvertCurrency(amount, toCurrencyId, CurrencyId);
        Credits -= ownCost;
        state.RecordConversion(CurrencyId, ownCost, toCurrencyId, amount);
        return amount;
    }
}
