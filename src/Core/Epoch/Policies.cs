using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>Polity budget weights, consumed by Allocation. Normalized shares.</summary>
public sealed record BudgetWeights(
    double Development, double Military, double Research,
    double Expansion, double Appeasement, double Reserves);

/// <summary>The research budget's standing split across the four tech
/// domains (economy/technology.md §Advancement) — the contract's
/// "research (per tech domain)" weights. Normalized shares.</summary>
public sealed record ResearchSplit(
    double Industrial, double Military, double Astrogation, double Life)
{
    public static ResearchSplit Default { get; } =
        new ResearchSplit(0.35, 0.15, 0.30, 0.20);
}

public enum LegalityLevel { Legal, Restricted, Prohibited }

public enum DiplomaticPosture { Hostile, Wary, Neutral, Cordial, Friendly }

public enum NativePolicy { Protectorate, Integrate, Exploit, Uplift }

public enum DoctrinePosture { Defensive, Balanced, Aggressive }

/// <summary>Posture and engagement biases, consumed by Resolution.</summary>
public sealed record MilitaryDoctrine(DoctrinePosture Posture, double EngagementBias)
{
    public static MilitaryDoctrine Default { get; } =
        new MilitaryDoctrine(DoctrinePosture.Balanced, 0.5);
}

/// <summary>Polity standing policies per frame/controller-contract.md.
/// Dictionary keys are registry ids of catalogs later slices land (goods:
/// Slice C, ship designs: Slice E, polities: actor ids) — empty by default.
/// The contract's tariff schedule is per polity/good; the per-polity
/// dimension lands with Markets (Slice D). Research is one weight until tech
/// domains arrive (Slice G extends; additions are frame-safe).</summary>
public sealed record PolityPolicies(
    BudgetWeights Budget,
    double TaxRate,
    IReadOnlyDictionary<int, double> TariffSchedule,
    IReadOnlyDictionary<int, LegalityLevel> LawCode,
    double CharterOpenness,
    MilitaryDoctrine Doctrine,
    IReadOnlyDictionary<int, double> ShipbuildingPriorities,
    IReadOnlyDictionary<int, double> StockpileTargets,
    IReadOnlyDictionary<int, DiplomaticPosture> DiplomaticPostures,
    NativePolicy NativePolicy,
    ResearchSplit Research) : PolicySet
{
    private static readonly IReadOnlyDictionary<int, double> NoWeights =
        new Dictionary<int, double>();
    private static readonly IReadOnlyDictionary<int, LegalityLevel> NoLaws =
        new Dictionary<int, LegalityLevel>();
    private static readonly IReadOnlyDictionary<int, DiplomaticPosture> NoPostures =
        new Dictionary<int, DiplomaticPosture>();

    public static PolityPolicies Default { get; } = new PolityPolicies(
        Budget: new BudgetWeights(Development: 0.30, Military: 0.20, Research: 0.15,
                                  Expansion: 0.20, Appeasement: 0.05, Reserves: 0.10),
        TaxRate: 0.10,
        TariffSchedule: NoWeights,
        LawCode: NoLaws,
        CharterOpenness: 0.5,
        Doctrine: MilitaryDoctrine.Default,
        ShipbuildingPriorities: NoWeights,
        StockpileTargets: NoWeights,
        DiplomaticPostures: NoPostures,
        NativePolicy: NativePolicy.Protectorate,
        Research: ResearchSplit.Default);
}

/// <summary>Corporate investment split across asset classes, consumed by Allocation.</summary>
public sealed record InvestmentAllocation(double Facilities, double Fleet, double Depots);

/// <summary>Corporation standing policies per frame/controller-contract.md.
/// Route-bid and lobby keys are registry ids landing in Slices D/G.
/// RiskAppetite is the legality margin — how far into black books it
/// operates.</summary>
public sealed record CorporationPolicies(
    InvestmentAllocation Investment,
    IReadOnlyDictionary<int, double> RouteBids,
    double DividendRate,
    IReadOnlyList<int> LobbyTargets,
    double RiskAppetite) : PolicySet
{
    public static CorporationPolicies Default { get; } = new CorporationPolicies(
        Investment: new InvestmentAllocation(Facilities: 0.5, Fleet: 0.4, Depots: 0.1),
        RouteBids: new Dictionary<int, double>(),
        DividendRate: 0.2,
        LobbyTargets: new int[0],
        RiskAppetite: 0.1);
}
