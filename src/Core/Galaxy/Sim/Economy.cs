using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

public enum Commodity { Provisions, Ore, Exotics }

/// <summary>Pure economy math (economy spec §5): production, consumption, value,
/// tech ladder, war strength, and BFS flow routing. Never mutates the skeleton —
/// phases apply the results.</summary>
public static class Economy
{
    /// <summary>Neutral species for owner-less display reads (inspector layers).</summary>
    public static readonly SpeciesProfile DisplayBaseline = new()
    {
        Id = -1, Name = "baseline", Embodiment = Embodiment.TerranAnalog,
        Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
        Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
    };

    /// <summary>Blockade-strain floor (deferred-tickets spec §3): BlockadeLoss above
    /// this fires TradeBlocked and counts as war-weariness hardship.</summary>
    public const double TradeBlockedFloor = 2.0;

    public static bool HasAnchor(RegionCell cell, AnchorType type)
    {
        foreach (var a in cell.Anchors) if (a.Type == type) return true;
        return false;
    }

    /// <summary>Provisions fertility through the owner's embodiment (spec §5):
    /// aquatics farm bright-star cells, cryophiles the dim reaches.</summary>
    public static double ProvisionsPotential(SpeciesProfile species, RegionCell cell) =>
        EpochSim.Affinity(species, cell) * cell.MeanDensity;

    public static double OrePotential(RegionCell cell) =>
        cell.Metallicity + (HasAnchor(cell, AnchorType.MineralRich) ? 1.5 : 0.0);

    public static double ExoticsPotential(RegionCell cell) =>
        (HasAnchor(cell, AnchorType.PrecursorSite) ? 1.0 : 0.0)
        + (cell.Lean == StellarLean.RemnantGraveyard ? 0.05 : 0.02);

    public static double Produced(Commodity good, SpeciesProfile owner, RegionCell cell) => good switch
    {
        Commodity.Provisions => ProvisionsPotential(owner, cell) * cell.DevelopmentTier
                                * (0.5 + 0.5 * Math.Min(1.0, cell.Population)),
        Commodity.Ore => OrePotential(cell) * cell.DevelopmentTier * 0.5,
        _ => ExoticsPotential(cell) * cell.DevelopmentTier * 0.5,
    };

    /// <summary>Embodiment diet discount (spec §5: lithics/machines barely need provisions).</summary>
    public static double DietFactor(Embodiment e) => e switch
    {
        Embodiment.Lithic => 0.2,
        Embodiment.Machine => 0.1,
        _ => 1.0,
    };

    public static double Consumed(Commodity good, GalaxyConfig config, SpeciesProfile owner, RegionCell cell) => good switch
    {
        Commodity.Provisions => cell.Population * config.ProvisionsPerPop * DietFactor(owner.Embodiment),
        Commodity.Ore => 0.2 * cell.DevelopmentTier,
        _ => 0.0,   // exotics are consumed at polity level by tech investment
    };

    /// <summary>System value (spec §5): production potential + throughput + strategic position.
    /// War-goal selection maximizes this within its goal type.</summary>
    public static double SystemValue(SpeciesProfile owner, RegionCell cell) =>
        ProvisionsPotential(owner, cell) + OrePotential(cell) + ExoticsPotential(cell)
        + 0.5 * cell.RouteThroughput + (cell.IsChokepoint ? 2.0 : 0.0);

    /// <summary>Cumulative exotics investment required to reach tier+1 (geometric, ×3).</summary>
    public static double TechThreshold(GalaxyConfig config, int tier) =>
        config.TechThresholdBase * Math.Pow(3.0, tier);

    /// <summary>Development-tier ceiling: stage-1 flat cap 5 at tech 0, +1 per tier, max 9.</summary>
    public static int DevCeiling(int techTier) => Math.Min(9, 5 + techTier);

    public static double WarStrength(double committedStockpile, int techTier, double militancy) =>
        committedStockpile * (1.0 + 0.5 * techTier) * (0.5 + militancy);

    /// <summary>Transit predicate for polity flows (spec §5): blockaded = contested or
    /// owned by a belligerent of the flow's owner. Unclaimed non-void space is open.</summary>
    public static Func<RegionCell, bool> Passable(GalaxySkeleton s, int polityId) =>
        c => !c.IsVoid && !c.Contested
             && (c.OwnerPolityId < 0 || c.OwnerPolityId == polityId
                 || !s.AtWar(c.OwnerPolityId, polityId));

    /// <summary>Deterministic BFS from a cell to the nearest cell satisfying
    /// <paramref name="isTarget"/>, transiting only <paramref name="passable"/> cells
    /// (endpoints exempt). Returns the full path including both endpoints, or null.
    /// Neighbor order = HexGrid.Neighbors order; ties resolve by discovery order.</summary>
    public static List<RegionCell>? Route(GalaxySkeleton s, RegionCell from,
        Func<RegionCell, bool> isTarget, Func<RegionCell, bool> passable)
    {
        var parent = new Dictionary<int, int>();
        var seen = new HashSet<int> { from.SpiralIndex };
        var queue = new Queue<RegionCell>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var nc in HexGrid.Neighbors(cur.Coord))
            {
                if (!s.TryGetCell(nc, out var n) || seen.Contains(n.SpiralIndex)) continue;
                seen.Add(n.SpiralIndex);
                parent[n.SpiralIndex] = cur.SpiralIndex;
                if (isTarget(n)) return BuildPath(s, from, n, parent);
                if (passable(n)) queue.Enqueue(n);
            }
        }
        return null;
    }

    private static List<RegionCell> BuildPath(GalaxySkeleton s, RegionCell from,
        RegionCell target, Dictionary<int, int> parent)
    {
        var path = new List<RegionCell> { target };
        int cur = target.SpiralIndex;
        while (cur != from.SpiralIndex)
        {
            cur = parent[cur];
            path.Add(s.Cells[cur]);
        }
        path.Reverse();
        return path;
    }
}
