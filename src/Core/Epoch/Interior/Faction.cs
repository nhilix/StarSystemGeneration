namespace StarGen.Core.Epoch;

/// <summary>The six faction bases (polity/factions-and-government.md
/// §Faction formation). Stable ids.</summary>
public enum FactionBasis
{
    Ideological = 0, // a popular cluster far from official ideology
    Cultural = 1,    // a minority culture segment
    Regional = 2,    // frontier domains far from the capital's care
    Corporate = 3,   // dividend-fed elites (armed in slice G task 7)
    Military = 4,    // veteran and commander networks
    Sacral = 5,      // faith movements
}

/// <summary>An interest bloc inside a polity (frame/actors.md §Internal
/// faction): agenda and strength drawn from population segments and patrons;
/// pressure mechanically, no controller slot until graduation. At most one
/// active faction per (polity, basis). Registry in SimState.Factions,
/// id order (P6).</summary>
public sealed class Faction
{
    public int Id { get; }
    public string Name { get; }
    public int PolityId { get; }
    public FactionBasis Basis { get; }
    public long FormedYear { get; }
    /// <summary>Basis context: culture id (cultural); −1 otherwise.</summary>
    public int ContextId { get; set; } = -1;
    public int LeaderCharacterId { get; set; } = -1;
    /// <summary>False once dissolved or graduated — dead factions stay in
    /// the registry as history (their id is their chronicle key).</summary>
    public bool Active { get; set; } = true;
    /// <summary>[0,1] population share + wealth + patron renown, recomputed
    /// each Interior phase from real state.</summary>
    public double Strength { get; set; }
    /// <summary>[0,1] willingness to force the issue — leader boldness ×
    /// species militancy × grievance.</summary>
    public double Militancy { get; set; }
    /// <summary>Accrued unappeased pressure; graduation tests
    /// strength × grievance against legitimacy × enforcement.</summary>
    public double Grievance { get; set; }
    /// <summary>Conserved credits: appeasement (and later dividends) flow
    /// in; dissolution returns them to the polity's segments.</summary>
    public double Wealth { get; set; }
    /// <summary>Appeasement received this epoch (Allocation writes, the
    /// Interior grievance update consumes and zeroes) — never serialized;
    /// always 0 at epoch boundaries.</summary>
    public double PaidThisEpoch { get; set; }
    /// <summary>What full appeasement would have cost this epoch (same
    /// lifecycle as PaidThisEpoch) — grievance accrues on the gap.</summary>
    public double DemandThisEpoch { get; set; }
    /// <summary>The agenda's budget emphasis (6 normalized weights in
    /// BudgetWeights order) — null when the agenda is ideological.</summary>
    public double[]? BudgetTarget { get; set; }
    /// <summary>The agenda's ideology pull per IdeologyAxis — null when the
    /// agenda is fiscal.</summary>
    public double[]? IdeologyTarget { get; set; }
    /// <summary>Corporate basis only: the profit niche this merchant
    /// faction watches (CorporateNiche id; 0 none).</summary>
    public int NicheType { get; set; }
    /// <summary>Consecutive world-years the niche has persisted — the
    /// charter graduation's clock (economy/corporations.md §Founding).
    /// P7: years, not steps.</summary>
    public int NichePersistenceYears { get; set; }

    public Faction(int id, string name, int polityId, FactionBasis basis,
                   long formedYear)
    {
        Id = id;
        Name = name;
        PolityId = polityId;
        Basis = basis;
        FormedYear = formedYear;
    }
}
