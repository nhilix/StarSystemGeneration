namespace StarGen.Core.Epoch;

/// <summary>A polity's inside (polity/factions-and-government.md): its
/// government form, the official ideology line (drifting toward the popular
/// line at the form's inertia), and the legitimacy/cohesion/enforcement
/// scalars that decide what its factions can get away with. Seated at entry
/// by the Interior phase, recomputed every epoch, serialized on the interior
/// layer. Null on a PolityRecord until the polity enters.</summary>
public sealed class PolityInterior
{
    public GovernmentFormId FormId { get; set; }
    /// <summary>The official line per <see cref="IdeologyAxis"/> — matches
    /// the popular line at birth, then chases it at (1 − PolicyInertia).</summary>
    public double[] OfficialIdeology { get; } = new double[4];
    /// <summary>[0,1] f(SoL trend, official-vs-popular gap, war outcomes,
    /// ruler prestige, cultural accommodation), form-weighted.</summary>
    public double Legitimacy { get; set; } = 0.6;
    /// <summary>[0,1] legitimacy discounted by structural strain (size,
    /// culture count, capital distance); floored by form (hive unity).</summary>
    public double Cohesion { get; set; } = 0.6;
    /// <summary>[0,1] the state's physical grip — garrison hulls per port.
    /// Graduation tests strength × grievance against legitimacy × this.</summary>
    public double Enforcement { get; set; } = 0.5;
    /// <summary>Previous epoch's size-weighted mean SoL — the trend input.</summary>
    public double LastMeanSoL { get; set; } = 0.5;
    /// <summary>Occupant of the seat — a character id once slice G task 2
    /// mints them; −1 vacant.</summary>
    public int RulerCharacterId { get; set; } = -1;
    /// <summary>The culture whose accommodation is free — minorities strain.</summary>
    public int FoundingCultureId { get; set; }
}
