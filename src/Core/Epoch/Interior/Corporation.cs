using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>What founded a corporation — the niche stamps its character
/// (economy/corporations.md §Founding). Stable ids.</summary>
public enum CorporateNiche
{
    None = 0,
    Extraction = 1,   // mine-rich frontier → extraction conglomerate
    Freight = 2,      // unserved profitable lanes → freight line
    Fabrication = 3,  // industrial gaps → fabricator combine
    Cartel = 4,       // profitable *prohibited* niche — chartered nowhere
    Raiding = 5,      // lawless lanes → pirate band (teeth arrive with H)
    Salvage = 6,      // battlefield/precursor POIs → salvors (slice I)
}

/// <summary>The unified credit surface production and payouts move money
/// through — polities and corporations both keep conserved books (P4).
/// Slice CU-1 adds the currency-aware <see cref="Deposit"/>/<see cref="Withdraw"/>
/// pair; <c>Credits</c> is a numeraire-denominated <b>read-only</b> handle used for
/// cross-actor comparison/ranking (a polity's own single-currency balance, a
/// corporation's numeraire-converted wallet total). All money movement goes through
/// Deposit/Withdraw — there is no setter on the interface: a polity keeps a concrete
/// settable <c>Credits</c> field for same-currency internal bookkeeping, but a
/// corporation's <c>Credits</c> is a pure function of its <see cref="Corporation.Holdings"/>
/// wallet (slice CU-1 task 7 removed the transitional single-balance bridge).</summary>
public interface ICreditLedger
{
    double Credits { get; }
    /// <summary>This epoch's market receipts (step-transient, cleared by
    /// the Markets phase; corporations pay dividends from it).</summary>
    double Receipts { get; set; }

    /// <summary>Credit this ledger with <paramref name="amount"/> denominated in
    /// <paramref name="fromCurrencyId"/>. A single-currency polity auto-converts
    /// into its own currency; a corporation accumulates the money in that
    /// currency's wallet bucket unconverted. Returns the amount actually banked in
    /// the ledger's OWN denomination (the converted own-currency sum for a polity,
    /// the unconverted <paramref name="amount"/> for a corporation) so a caller can
    /// mirror it into <see cref="Receipts"/>.</summary>
    double Deposit(SimState state, double amount, int fromCurrencyId);

    /// <summary>Debit enough of this ledger to provide <paramref name="amount"/>
    /// denominated in <paramref name="toCurrencyId"/>; returns the amount
    /// actually provided in <paramref name="toCurrencyId"/> terms (equal to the
    /// request unless the ledger caps at what it holds — see the concrete
    /// implementers for the per-type shortfall rule).</summary>
    double Withdraw(SimState state, double amount, int toCurrencyId);
}

/// <summary>An emergent trans-polity economic institution
/// (economy/corporations.md): founded by the simulation when a niche
/// persists, dead when the niche or the balance sheet dies, influential in
/// between. Corporations ARE actors (ActorKind.Corporation) — they hold a
/// controller slot; this record is the sim state beside the substrate.
/// Registry in SimState.Corporations, id order (P6).</summary>
public sealed class Corporation : ICreditLedger
{
    public int Id { get; }
    /// <summary>The Actors-registry entry (identity, controller, events).</summary>
    public int ActorId { get; }
    public string Name { get; }
    /// <summary>Chartering polity; −1 for the outlaw cousins (cartels and
    /// pirate bands are chartered nowhere).</summary>
    public int HostPolityId { get; set; }
    public CorporateNiche Niche { get; }
    /// <summary>Headquarters — operations stage from this port's market
    /// (a pirate band's haven port).</summary>
    public int HomePortId { get; set; }
    /// <summary>Niche context: the hunted lane id for pirate bands; −1
    /// otherwise (slice H arms the raiding).</summary>
    public int TargetId { get; set; } = -1;
    public long FoundedYear { get; }
    public bool Active { get; set; } = true;

    /// <summary>The multi-currency wallet (currency-and-FX design): amount held
    /// per <see cref="Currency"/> id. A corporation routinely trades across
    /// polity borders and accumulates whatever currency it earns, converting
    /// only when it must pay (the <see cref="Withdraw"/> draw-down). Read-only
    /// to the outside — money moves only through Deposit/Withdraw.</summary>
    public IReadOnlyDictionary<int, double> Holdings => _holdings;
    private readonly Dictionary<int, double> _holdings = new Dictionary<int, double>();

    // Numeraire value of _holdings, cached because the interface's parameterless
    // Credits getter cannot reach the per-epoch rate table without a state
    // back-reference (which the codebase's "pass state explicitly" convention
    // forbids). Refreshed by Deposit/Withdraw and by RefreshNumeraire (called by
    // the per-epoch FX pass once rates move — a later slice task).
    private double _walletNumeraire;

    /// <summary>Numeraire-converted total of the wallet (design: computed from
    /// <see cref="Holdings"/>, cached in <c>_walletNumeraire</c> and refreshed by
    /// every Deposit/Withdraw and by the per-epoch FX pass). Read-only and read for
    /// cross-actor comparison/ranking — it is NOT the way to move currency-tagged
    /// money; use Deposit/Withdraw for that. The transitional single-balance bridge
    /// (slice CU-1 tasks 1–6b) is gone: every corp write-site now routes through the
    /// wallet, so <c>Credits</c> is purely a function of <see cref="Holdings"/>.</summary>
    public double Credits => _walletNumeraire;
    public double Receipts { get; set; }
    /// <summary>Last step's receipts per world-year (spec §2).</summary>
    public double LastIncomePerYear { get; set; }
    /// <summary>The executive suite — a character id (characters.md roles).</summary>
    public int ExecutiveCharacterId { get; set; } = -1;
    /// <summary>Hull conservation ledger, like the polity's (P4).</summary>
    public int HullsBuilt { get; set; }
    public int HullsWrecked { get; set; }
    public int HullsScrapped { get; set; }
    /// <summary>Consecutive world-years of near-zero revenue — the
    /// niche-death clock (the deposit exhausts, the lane closes, margins
    /// evaporate). P7: years, not steps.</summary>
    public int LeanYears { get; set; }

    public Corporation(int id, int actorId, string name, int hostPolityId,
                       CorporateNiche niche, int homePortId, long foundedYear)
    {
        Id = id;
        ActorId = actorId;
        Name = name;
        HostPolityId = hostPolityId;
        Niche = niche;
        HomePortId = homePortId;
        FoundedYear = foundedYear;
    }

    /// <summary>Accumulate <paramref name="amount"/> of
    /// <paramref name="fromCurrencyId"/> in the matching wallet bucket
    /// unconverted — a corporation banks whatever currency it earns.</summary>
    public double Deposit(SimState state, double amount, int fromCurrencyId)
    {
        if (amount == 0) return 0;
        _holdings.TryGetValue(fromCurrencyId, out double held);
        _holdings[fromCurrencyId] = held + amount;
        RefreshNumeraire(state);
        return amount;   // a corporation banks the currency it was paid, unconverted
    }

    /// <summary>Provide <paramref name="amount"/> of
    /// <paramref name="toCurrencyId"/> via the deterministic draw-down rule:
    /// spend the matching bucket first; on a shortfall walk the other buckets in
    /// ascending currency-id order, converting each into
    /// <paramref name="toCurrencyId"/> until covered; fully drained buckets are
    /// removed. Caps at what the wallet holds (a corporation has no overdraft —
    /// unlike a polity, whose balance goes negative into insolvency). Returns the
    /// amount actually provided, in <paramref name="toCurrencyId"/> terms.</summary>
    public double Withdraw(SimState state, double amount, int toCurrencyId)
    {
        if (amount <= 0) return 0;
        double remaining = amount;

        // 1. the matching bucket (no conversion), whether it covers the whole
        //    request or only part of it
        if (_holdings.TryGetValue(toCurrencyId, out double matched) && matched > 0)
        {
            double take = matched < remaining ? matched : remaining;
            double left = matched - take;
            if (left > 1e-12) _holdings[toCurrencyId] = left;
            else _holdings.Remove(toCurrencyId);
            remaining -= take;
        }

        // 2. the other buckets in ascending currency-id order, converting each
        if (remaining > 1e-12)
        {
            var ids = new List<int>(_holdings.Keys);
            ids.Sort();
            foreach (int otherId in ids)
            {
                if (remaining <= 1e-12) break;
                if (otherId == toCurrencyId) continue;   // already handled
                double heldOther = _holdings[otherId];
                if (heldOther <= 0) continue;
                // slice CU-2 task 8 fix 3: guard the spread on both ids being
                // real (matching the sibling sites in FleetOps / MarketEngine /
                // PolityRecord). In the unowned-port degrade path (toCurrencyId
                // == -1) SkimToReserve / RecordConversion no-op, so a nonzero
                // spread would still shrink the payee contribution and silently
                // vanish the skim. Zero it: no destination Bank means no skim.
                double spread = (otherId >= 0 && toCurrencyId >= 0)
                    ? state.Config.Economy.ConversionSpread : 0.0;
                double valueInTo = state.ConvertCurrency(heldOther, otherId, toCurrencyId);
                // slice CU-2 gross-up incidence: to PROVIDE `p` of toCurrencyId to
                // the payee the corp must source p*(1+spread) of it — `p` for the
                // payee, `p*spread` skimmed onto the destination reserve. So this
                // bucket's whole converted value valueInTo can finish the request
                // only if its payee contribution valueInTo/(1+spread) still exceeds
                // what remains.
                if (valueInTo <= remaining * (1.0 + spread) + 1e-12)
                {
                    // whole bucket consumed: its valueInTo splits into the payee
                    // contribution (valueInTo/(1+spread)) and the spread skimmed ON
                    // TOP into the reserve; the loop continues to cover any genuine
                    // remainder from the next bucket. Books the FULL heldOther out /
                    // valueInTo in (exact bucket drain), reserve keeps the skim.
                    double pcontrib = valueInTo / (1.0 + spread);
                    double skim = valueInTo - pcontrib;
                    _holdings.Remove(otherId);
                    state.SkimToReserve(toCurrencyId, skim);
                    state.RecordConversion(otherId, heldOther, toCurrencyId, valueInTo);
                    remaining -= pcontrib;
                }
                else
                {
                    // partial: source exactly the payee's `remaining` PLUS its skim
                    // from this bucket — the payee gets the full remaining, the
                    // reserve gets the skim, the bucket (which more than covers it)
                    // keeps the rest. Grossing the payer means the corp bears the
                    // spread, so the payee is never short-changed.
                    double skim = remaining * spread;
                    double grossTo = remaining + skim;
                    double spendOther = state.ConvertCurrency(grossTo, toCurrencyId, otherId);
                    double left = heldOther - spendOther;
                    if (left > 1e-12) _holdings[otherId] = left;
                    else _holdings.Remove(otherId);
                    state.SkimToReserve(toCurrencyId, skim);
                    state.RecordConversion(otherId, spendOther, toCurrencyId, grossTo);
                    remaining = 0;
                }
            }
        }

        RefreshNumeraire(state);
        double provided = amount - remaining;
        return provided < 0 ? 0 : provided;
    }

    /// <summary>Recompute the cached numeraire value of the wallet from the
    /// current rate table — called after every wallet mutation, and to be
    /// called by the per-epoch FX pass after it moves rates (later slice task).
    /// Iterates buckets in ascending currency-id order so the running sum is
    /// byte-identical across runs (determinism, P6).</summary>
    public void RefreshNumeraire(SimState state)
    {
        var ids = new List<int>(_holdings.Keys);
        ids.Sort();
        double sum = 0;
        foreach (int id in ids)
            sum += _holdings[id] * state.NumeraireRateOf(id);
        _walletNumeraire = sum;
    }
}

/// <summary>The corporate AI (frame/controller-contract.md §Corporation):
/// long-run profit filtered through founding character — freight lines
/// invest in hulls, conglomerates in facilities, cartels keep thin books
/// and deep margins. Constructed with the config; decides from the view
/// alone (P2).</summary>
public sealed class CorporateController : IController
{
    private static readonly IReadOnlyList<Act> NoActs = new Act[0];
    private readonly EpochSimConfig _config;

    public CorporateController(EpochSimConfig config) { _config = config; }

    /// <summary>The corp's standing plan (contract-economy spec §3, C11):
    /// its perceived investment candidates packed against income + savings
    /// — the same scheduler discipline polities run, at portfolio scope.
    /// Operate executes the due entries mechanically (Move 1).</summary>
    public ControllerDecision Decide(PerceptionView perceived) =>
        new ControllerDecision(CorporationPolicies.Default with
        { Plan = Planner.BuildCorpPlan(perceived, _config) }, NoActs);
}
