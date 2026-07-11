using System;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Substrate;

/// <summary>Everything siting reads about a candidate cell: the natural
/// raster fields plus domain context the caller supplies (connectivity, port
/// presence, development — B/D own those registries).</summary>
public sealed record CellSite(
    CellFields Fields, double Connectivity, bool IsPortHeart,
    int PortTier, int DevelopmentTier, bool IsChokepoint);

/// <summary>Siting rules as pure scores in [0,1] per facility type
/// (substrate/infrastructure.md catalog table). Scores rank candidate cells;
/// siting *execution* is state-model work (Slice B/D).</summary>
public static class Siting
{
    private static double Clamp01(double v) => Math.Max(0.0, Math.Min(1.0, v));

    /// <summary>How strongly the cell reads as a port heart: 0 without a
    /// port, rising with port tier (outpost → starport → nexus).</summary>
    private static double Portness(CellSite s) =>
        s.IsPortHeart ? Clamp01(0.5 + 0.15 * s.PortTier) : 0.0;

    private static double Dev(CellSite s) =>
        Clamp01(s.DevelopmentTier / 3.0);

    public static double Score(InfraTypeId type, CellSite s, Embodiment workforce)
    {
        var f = s.Fields;
        double portness = Portness(s);
        double score = type switch
        {
            // the domain heart: best-connected system in the region
            InfraTypeId.Port => 0.45 * s.Connectivity + 0.25 * f.MeanDensity
                              + 0.15 * Dev(s) + 0.15 * (s.IsChokepoint ? 1.0 : 0.0),

            // extraction reads the genesis fields
            InfraTypeId.Mine => Potentials.Ore(f),
            InfraTypeId.Skimmer => Potentials.Volatiles(f),
            InfraTypeId.AgriComplex =>
                Potentials.Biosphere(f) * Potentials.EmbodimentAffinity(workforce, f),
            InfraTypeId.ExcavationSite => Potentials.Exotics(f),

            // processing: near inputs or at ports / population centers
            InfraTypeId.Refinery =>
                Math.Max(0.7 * Math.Max(Potentials.Ore(f), Potentials.Volatiles(f)), portness),
            InfraTypeId.Chemworks =>
                0.8 * Math.Max(Potentials.Volatiles(f), Potentials.Biosphere(f))
                + 0.2 * portness,
            InfraTypeId.Fabricator => 0.7 * portness + 0.3 * Dev(s),
            InfraTypeId.ExoticsLab =>
                Math.Max(Potentials.Exotics(f), s.IsPortHeart && s.PortTier >= 2 ? 0.6 : 0.0),

            // heavy: developed domains, alloy supply, port proximity
            InfraTypeId.Foundry => 0.6 * Dev(s) + 0.4 * Potentials.Ore(f),
            InfraTypeId.Shipyard => 0.7 * portness + 0.3 * Dev(s),
            InfraTypeId.Arsenal => 0.8 * Dev(s) + 0.2 * portness,
            InfraTypeId.ComputeCore => 0.5 * Potentials.Exotics(f) + 0.5 * Dev(s),

            // support: junction ports, approaches, chokepoint lanes
            InfraTypeId.Depot => 0.6 * portness + 0.4 * (s.IsChokepoint ? 1.0 : 0.0),
            InfraTypeId.Fortress => 0.5 * (s.IsChokepoint ? 1.0 : 0.0) + 0.5 * portness,
            // gates are placed by the lane builder, not the siting scorer
            InfraTypeId.Gate => portness,

            _ => 0.0,
        };
        return Clamp01(score);
    }
}
