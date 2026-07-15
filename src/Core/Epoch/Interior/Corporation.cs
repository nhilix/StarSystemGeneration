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
/// pair; <c>Credits</c> is a numeraire-denominated read used for cross-actor
/// comparison/ranking (a polity's own single-currency balance, a corporation's
/// numeraire-converted wallet total). Cross-currency money movement must go
/// through Deposit/Withdraw — raw <c>Credits +=/-=</c> is same-currency-only
/// internal bookkeeping and is migrated off corporations by later slice tasks.</summary>
public interface ICreditLedger
{
    double Credits { get; set; }
    /// <summary>This epoch's market receipts (step-transient, cleared by
    /// the Markets phase; corporations pay dividends from it).</summary>
    double Receipts { get; set; }

    /// <summary>Credit this ledger with <paramref name="amount"/> denominated in
    /// <paramref name="fromCurrencyId"/>. A single-currency polity auto-converts
    /// into its own currency; a corporation accumulates the money in that
    /// currency's wallet bucket unconverted.</summary>
    void Deposit(SimState state, double amount, int fromCurrencyId);

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
    // TRANSITIONAL (slice CU-1): the pre-currency single balance. Until the
    // corporation write-sites migrate to Deposit/Withdraw and genesis funds
    // Holdings (later slice tasks), legacy code still does `corp.Credits +=/-=/=`
    // on a concrete Corporation. This field carries that value so behavior is
    // unchanged while Holdings is still empty; it converges to zero as those
    // sites migrate, after which the setter can be removed.
    private double _legacyCredits;

    /// <summary>Numeraire-converted total of the wallet (design: computed from
    /// <see cref="Holdings"/>). Read for cross-actor comparison/ranking. The
    /// setter is the transitional bridge described above — it is NOT the way to
    /// move currency-tagged money; use Deposit/Withdraw for that.</summary>
    public double Credits
    {
        get => _walletNumeraire + _legacyCredits;
        set => _legacyCredits = value - _walletNumeraire;
    }
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
    public void Deposit(SimState state, double amount, int fromCurrencyId)
    {
        if (amount == 0) return;
        _holdings.TryGetValue(fromCurrencyId, out double held);
        _holdings[fromCurrencyId] = held + amount;
        RefreshNumeraire(state);
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
                double valueInTo = state.ConvertCurrency(heldOther, otherId, toCurrencyId);
                if (valueInTo <= remaining + 1e-12)
                {
                    // whole bucket consumed
                    _holdings.Remove(otherId);
                    remaining -= valueInTo;
                }
                else
                {
                    // partial: spend exactly the source amount worth `remaining`
                    double spendOther = state.ConvertCurrency(remaining, toCurrencyId, otherId);
                    double left = heldOther - spendOther;
                    if (left > 1e-12) _holdings[otherId] = left;
                    else _holdings.Remove(otherId);
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
            sum += _holdings[id] * state.CurrencyOf(id).NumeraireRate;
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
