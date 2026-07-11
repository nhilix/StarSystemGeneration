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
/// through — polities and corporations both keep conserved books (P4).</summary>
public interface ICreditLedger
{
    double Credits { get; set; }
    /// <summary>This epoch's market receipts (step-transient, cleared by
    /// the Markets phase; corporations pay dividends from it).</summary>
    double Receipts { get; set; }
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
    /// <summary>Conserved books: founded on the merchant faction's war
    /// chest, moved only by market flows, dividends, and seizure.</summary>
    public double Credits { get; set; }
    public double Receipts { get; set; }
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
}

/// <summary>The corporate AI (frame/controller-contract.md §Corporation):
/// long-run profit filtered through founding character — freight lines
/// invest in hulls, conglomerates in facilities, cartels keep thin books
/// and deep margins. Constructed with the config; decides from the view
/// alone (P2).</summary>
public sealed class CorporateController : IController
{
    private static readonly IReadOnlyList<Act> NoActs = new Act[0];

    public ControllerDecision Decide(PerceptionView perceived) =>
        new ControllerDecision(CorporationPolicies.Default, NoActs);
}
