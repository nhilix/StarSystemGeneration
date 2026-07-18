using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The four ideology axes — the fast identity layer
/// (polity/population-and-identity.md). Each axis is a scalar in [0,1]:
/// 0 = the first-named pole, 1 = the second, 0.5 neutral.</summary>
public enum IdeologyAxis
{
    AuthorityAutonomy = 0,
    CommunalIndividual = 1,
    OpenInsular = 2,
    SacralMaterial = 3,
}

/// <summary>Species- and culture-tagged population quantity, administered per
/// port domain (frame/actors.md: population is substrate, never an actor — it
/// responds statistically and holds no controller). The design tuple:
/// (species, culture, size, standard of living, ideology distribution) —
/// slice D carries ideology as a mean point per axis; income and wealth close
/// the wages→consumption loop (economy/markets.md §Household income).</summary>
public sealed class PopulationSegment
{
    public int Id { get; }
    /// <summary>The administering port (its domain is where this population lives).</summary>
    public int PortId { get; }
    public int SpeciesId { get; }
    /// <summary>The slow identity layer — travels with migrants, never blends
    /// away (conquest and migration add segments). Settable since slice G:
    /// a regional schism splits the culture (the registry's split mechanic).</summary>
    public int CultureId { get; set; }
    public double Size { get; set; }
    /// <summary>Standard of living in [0,1] — derives from what the segment's
    /// income actually clears at local prices; feeds growth, legitimacy, and
    /// migration pull.</summary>
    public double SoL { get; set; } = 0.5;
    /// <summary>Unspent income accumulates — conserved credits (P4).</summary>
    public double Wealth { get; set; }
    /// <summary>Mean position per <see cref="IdeologyAxis"/>, drifting with
    /// lived conditions (the fast layer).</summary>
    public double[] Ideology { get; } = { 0.5, 0.5, 0.5, 0.5 };
    /// <summary>Last market step's subsistence satisfaction [0,1] — written
    /// by clearing, read by Interior demographics the same step AND by the
    /// next step's reserve release, so it is real cross-step state and the
    /// artifact carries it (segments layer v2).</summary>
    public double LastSubsistence { get; set; } = 1.0;
    /// <summary>The body within the port's domain this segment settled at —
    /// assigned at creation (locality slice §3, follow-on plan). None until
    /// then; the port id remains the administering domain.</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
    /// <summary>The segment's settled hex within its administering port's domain
    /// (domain-hex-expansion design §3) — <b>defaults to the administering
    /// port's hex</b>, so a segment that has not relocated reads its port hex
    /// (Task 2.4 staffing depends on this). Serialized (segments layer v4).
    /// Stage 2's settle election moves it to a satellite hex; the
    /// <see cref="PortId"/> stays the administering domain either way.</summary>
    public HexCoordinate Hex { get; set; }

    public PopulationSegment(int id, int portId, int speciesId, int cultureId,
                             double size)
    {
        Id = id;
        PortId = portId;
        SpeciesId = speciesId;
        CultureId = cultureId;
        Size = size;
    }
}
