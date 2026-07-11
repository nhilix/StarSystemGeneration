using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>What a standing claim asserts (interpolity/relations.md
/// §Relations state per pair). Stable ids.</summary>
public enum ClaimType
{
    CulturalKin = 0,   // subject = culture id living under the other's rule
    LostTerritory = 1, // subject = port id ceded or captured
    Succession = 2,    // subject = dynasty id with a cross-throne claim (H4)
    Liberation = 3,    // subject = origin id of a suppressed emergence (H8)
}

/// <summary>A standing claim on a relation — a tension source that persists
/// until its cause resolves. Released claims stay as history (their year
/// range is the grudge's biography).</summary>
public sealed class RelationClaim
{
    public ClaimType Type { get; }
    /// <summary>The polity holding the grievance (one of the pair).</summary>
    public int HolderPolityId { get; }
    /// <summary>Type-dependent registry id: culture, port, dynasty, origin.</summary>
    public int SubjectId { get; }
    public long RaisedYear { get; }
    public bool Released { get; set; }
    public long ReleasedYear { get; set; } = -1;

    public RelationClaim(ClaimType type, int holderPolityId, int subjectId,
                         long raisedYear)
    {
        Type = type;
        HolderPolityId = holderPolityId;
        SubjectId = subjectId;
        RaisedYear = raisedYear;
    }
}

/// <summary>The treaty ladder's discrete rungs (interpolity/relations.md
/// §Relations state per pair). Stable ids; federation is not a rung — it
/// fuses a NEW polity. Vassalage rides the relation as an asymmetric bond
/// (VassalPolityId), not this enum.</summary>
public enum TreatyRung
{
    None = 0,
    TradePact = 1,      // tariff cuts, lane priority
    NonAggression = 2,  // spark de-escalation, tension damping
    DefenseAlliance = 3,// join defensive wars — attackers price allied fleets
    /// <summary>Never stored on a relation: an offer at this rung, accepted,
    /// executes the fusion (FederationOps.Federate) and retires both.</summary>
    Federation = 4,
}

/// <summary>Relations state per pair of polities (interpolity/relations.md):
/// the pressure gauge war reads and the ladder peace climbs. One record per
/// pair that has met, keyed (A &lt; B); registry in SimState.Relations in
/// creation order (contact scans pairs ascending — P6). Warmth and tension
/// drift toward targets recomputed from live sources each Interior phase;
/// tension decays only as its sources resolve, because the target holds
/// while they stand.</summary>
public sealed class PolityRelation
{
    public int PolityAId { get; }
    public int PolityBId { get; }
    /// <summary>World-year the pair first met (P7: relation clocks are
    /// calendar years, never step counts — slice J).</summary>
    public int MetYear { get; }
    /// <summary>[0,1] accumulated positive interdependence: trade volume,
    /// dynastic ties, honored treaties; cooled by strain and ideology gap.</summary>
    public double Warmth { get; set; }
    /// <summary>[0,1] accumulated friction: overlap zones, standing claims,
    /// interdiction strain, agitation, ideology gap × zeal.</summary>
    public double Tension { get; set; }
    public TreatyRung Rung { get; set; } = TreatyRung.None;
    /// <summary>World-year the current rung was signed (−1 unsigned) —
    /// sustained alliance is the federation gate's clock.</summary>
    public int RungYear { get; set; } = -1;
    /// <summary>Pending treaty offer on the table (None = no offer).</summary>
    public TreatyRung OfferedRung { get; set; } = TreatyRung.None;
    /// <summary>Who made the standing offer; −1 none.</summary>
    public int OfferedById { get; set; } = -1;
    public int OfferYear { get; set; } = -1;
    /// <summary>Live dynastic instruments (marriages/wardships) between the
    /// pair — warmth now, succession claims later (H4).</summary>
    public int DynasticTies { get; set; }
    /// <summary>World-year of the newest instrument — the lapse clock: a
    /// tie's generation dies out after DynasticTieLapseYears, converting
    /// into the succession claim it always carried (−1 none).</summary>
    public long LastTieYear { get; set; } = -1;
    /// <summary>World-year of the last border incident between the pair —
    /// the spark's freshness window as a casus belli (−1 never).</summary>
    public int LastIncidentYear { get; set; } = -1;
    /// <summary>The vassal of the pair when the bond is vassalage; −1 for
    /// peers (H3).</summary>
    public int VassalPolityId { get; set; } = -1;
    /// <summary>World-year the vassal bond was struck (−1 unbound) — long
    /// stable vassalage is the absorption exit's clock.</summary>
    public int VassalSinceYear { get; set; } = -1;
    public List<RelationClaim> Claims { get; } = new List<RelationClaim>();

    /// <summary>Last recompute's tension source terms, for the REPL's
    /// legibility panel (overlap, claims, interdiction, ideology, agitation,
    /// militancy). Transient — never serialized.</summary>
    public double[] LastTensionTerms { get; } = new double[6];
    /// <summary>Last recompute's warmth source terms (baseline−strangeness,
    /// trade, treaty, dynastic, −ideology cooling, reputation). Transient.</summary>
    public double[] LastWarmthTerms { get; } = new double[6];

    public PolityRelation(int polityAId, int polityBId, int metYear)
    {
        PolityAId = polityAId;
        PolityBId = polityBId;
        MetYear = metYear;
    }

    public bool Involves(int polityId) =>
        PolityAId == polityId || PolityBId == polityId;

    public int OtherOf(int polityId) =>
        PolityAId == polityId ? PolityBId : PolityAId;

    /// <summary>Live (unreleased) claims of one type held by one side.</summary>
    public bool HasLiveClaim(ClaimType type, int holderPolityId, int subjectId)
    {
        foreach (var c in Claims)
            if (!c.Released && c.Type == type
                && c.HolderPolityId == holderPolityId
                && c.SubjectId == subjectId)
                return true;
        return false;
    }
}
