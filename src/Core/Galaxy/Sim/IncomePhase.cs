using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 1 (economy spec §3/§5): per-cell production, intra-polity
/// surplus→deficit flows with throughput, cross-polity trade, shortage effects,
/// and population dynamics. Deterministic: polities by id, cells by spiral index.</summary>
public static class IncomePhase
{
    private const double Eps = 1e-9;
    private const double FamineEventFloor = 0.5;
    private const double PopGrowthBase = 0.05;
    private const double FamineShrink = 0.8;
    private const double ScarShrink = 0.95;

    /// <summary>Transit predicate ignoring blockades: used to classify a failed route
    /// as blockade-induced (a target IS reachable unblockaded) vs. plain scarcity
    /// (deferred-tickets spec §3). Only void is impassable.</summary>
    private static readonly Func<RegionCell, bool> Unblockaded = c => !c.IsVoid;

    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var cell in s.Cells) cell.RouteThroughput = 0.0;
        // Strain is a last-epoch snapshot like the balances; extinct and landless
        // polities hold zero (deferred-tickets spec §3).
        foreach (var polity in s.Polities) polity.BlockadeLoss = 0.0;

        // Per-polity, per-good remaining surplus/deficit after internal routing,
        // kept for the cross-polity pass. [polityId][good] → amount (+surplus/−deficit).
        var remaining = new Dictionary<int, double[]>();
        // Cells whose provisions deficit went unfilled (famine candidates).
        var unfed = new Dictionary<int, List<(RegionCell cell, double lack)>>();

        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) continue;
            var species = s.Species[polity.SpeciesId];
            var owned = EpochSim.Owned(s, polity);
            if (owned.Count == 0) continue;
            var passable = Economy.Passable(s, polity.Id);
            double[] totals = new double[3];
            unfed[polity.Id] = new List<(RegionCell, double)>();

            foreach (var good in new[] { Commodity.Provisions, Commodity.Ore })
            {
                var net = new Dictionary<int, double>();
                foreach (var cell in owned)
                    net[cell.SpiralIndex] = Economy.Produced(good, species, cell)
                                          - Economy.Consumed(good, s.Config, species, cell);
                totals[(int)good] = Sum(net);

                // Fill deficits from nearest surplus, cheapest-first by BFS distance.
                foreach (var deficit in owned)
                {
                    double need = -net[deficit.SpiralIndex];
                    Func<RegionCell, bool> isSurplus =
                        c => c.OwnerPolityId == polity.Id
                             && net.TryGetValue(c.SpiralIndex, out var v) && v > Eps;
                    while (need > Eps)
                    {
                        var path = Economy.Route(s, deficit, isSurplus, passable);
                        if (path == null)
                        {
                            // Blockade-induced only if a surplus IS reachable ignoring
                            // blockades; otherwise nothing exists for a blockade to deny.
                            if (Economy.Route(s, deficit, isSurplus, Unblockaded) != null)
                                polity.BlockadeLoss += need;
                            break;
                        }
                        var source = path[path.Count - 1];
                        double amount = Math.Min(need, net[source.SpiralIndex]);
                        net[source.SpiralIndex] -= amount;
                        net[deficit.SpiralIndex] += amount;
                        need -= amount;
                        foreach (var transit in path) transit.RouteThroughput += amount;
                    }
                    if (good == Commodity.Provisions && need > Eps)
                        unfed[polity.Id].Add((deficit, need));
                }
                // remaining surplus for cross-polity trade:
                double surplus = 0;
                foreach (var v in net.Values) if (v > Eps) surplus += v;
                double deficitLeft = 0;
                foreach (var v in net.Values) if (v < -Eps) deficitLeft += -v;
                GetRemaining(remaining, polity.Id)[(int)good] = surplus - deficitLeft;
            }

            // Exotics: produced at cells, consumed at polity level (tech, allocation phase).
            double exotics = 0;
            foreach (var cell in owned) exotics += Economy.Produced(Commodity.Exotics, species, cell);
            totals[(int)Commodity.Exotics] = exotics;
            GetRemaining(remaining, polity.Id)[(int)Commodity.Exotics] = exotics;

            polity.ProvisionsBalance = totals[0];
            polity.OreBalance = totals[1];
            polity.ExoticsBalance = totals[2];
        }

        CrossPolityTrade(s, remaining);
        ApplyPopulationAndEvents(s, epoch, unfed);
    }

    private static double[] GetRemaining(Dictionary<int, double[]> map, int id)
    {
        if (!map.TryGetValue(id, out var arr)) { arr = new double[3]; map[id] = arr; }
        return arr;
    }

    private static double Sum(Dictionary<int, double> net)
    {
        double t = 0; foreach (var v in net.Values) t += v; return t;
    }

    /// <summary>Matched complementary surpluses between graph-adjacent non-belligerents
    /// convert to wealth for both sides (spec §5); throughput rides the capital-capital
    /// path passable for BOTH parties. A blocked trade (path exists unblockaded but not
    /// under blockade rules) strains both partners.</summary>
    private static void CrossPolityTrade(GalaxySkeleton s, Dictionary<int, double[]> remaining)
    {
        for (int a = 0; a < s.Polities.Count; a++)
        {
            var pa = s.Polities[a];
            if (pa.Extinct || !remaining.ContainsKey(a)) continue;
            for (int b = a + 1; b < s.Polities.Count; b++)
            {
                var pb = s.Polities[b];
                if (pb.Extinct || !remaining.ContainsKey(b)) continue;
                if (s.AtWar(a, b)) continue;
                if (!SharesBorder(s, a, b)) continue;

                for (int g = 0; g < 3; g++)
                {
                    double give = Math.Min(Math.Max(0, remaining[a][g]), Math.Max(0, -remaining[b][g]))
                                + Math.Min(Math.Max(0, remaining[b][g]), Math.Max(0, -remaining[a][g]));
                    if (give <= Eps) continue;
                    var capA = s.CellAt(pa.CapitalCoord);
                    var capB = s.CellAt(pb.CapitalCoord);
                    Func<RegionCell, bool> isCapB = c => c.SpiralIndex == capB.SpiralIndex;
                    var path = Economy.Route(s, capA, isCapB,
                        c => Economy.Passable(s, a)(c) && Economy.Passable(s, b)(c));
                    if (path == null)
                    {
                        if (Economy.Route(s, capA, isCapB, Unblockaded) != null)
                        {
                            pa.BlockadeLoss += give;
                            pb.BlockadeLoss += give;
                        }
                        continue;
                    }
                    double wealth = give * s.Config.TradeIncomeWeight;
                    pa.Wealth += wealth;
                    pb.Wealth += wealth;
                    remaining[a][g] -= Math.Sign(remaining[a][g]) * Math.Min(Math.Abs(remaining[a][g]), give);
                    remaining[b][g] -= Math.Sign(remaining[b][g]) * Math.Min(Math.Abs(remaining[b][g]), give);
                    foreach (var transit in path) transit.RouteThroughput += give;
                }
            }
        }
    }

    private static bool SharesBorder(GalaxySkeleton s, int a, int b)
    {
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId != a) continue;
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId == b) return true;
        }
        return false;
    }

    private static void ApplyPopulationAndEvents(GalaxySkeleton s, int epoch,
        Dictionary<int, List<(RegionCell cell, double lack)>> unfed)
    {
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct || !unfed.ContainsKey(polity.Id)) continue;
            var starving = unfed[polity.Id];
            double famineMagnitude = 0, worstLack = 0;
            RegionCell? worst = null;
            foreach (var (cell, lack) in starving)
            {
                cell.Population = Math.Max(0, cell.Population * FamineShrink);
                famineMagnitude += lack;
                if (worst == null || lack > worstLack) { worst = cell; worstLack = lack; }
            }
            if (famineMagnitude > FamineEventFloor && worst != null)
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.Famine,
                    ActorPolityId = polity.Id, Q = worst.Q, R = worst.R,
                    Magnitude = famineMagnitude,
                });

            // Strain above the floor fires TradeBlocked for ANY polity — no live-war
            // gate: a neutral bystander severed by a third-party front qualifies, a
            // warring polity with a plain no-surplus famine does not (spec §3).
            if (polity.BlockadeLoss > Economy.TradeBlockedFloor)
            {
                var cap = s.CellAt(polity.CapitalCoord);
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.TradeBlocked,
                    ActorPolityId = polity.Id, Q = cap.Q, R = cap.R,
                    Magnitude = polity.BlockadeLoss,
                });
            }
        }

        // Growth for fed cells; war-scar shrink for ALL besieged cells — famine and
        // siege are separate pressures and stack (deferred-tickets spec §5). The
        // growth guard is untouched: a starving cell never grows, a fed cell never
        // shrinks from feeding.
        var starvingSet = new HashSet<RegionCell>();
        foreach (var list in unfed.Values) foreach (var (cell, _) in list) starvingSet.Add(cell);
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId < 0) continue;
            if (!starvingSet.Contains(cell))
            {
                double cap = 1.0 + cell.DevelopmentTier;
                if (cell.Population < cap)
                    cell.Population = Math.Min(cap,
                        cell.Population + PopGrowthBase * (1 + cell.DevelopmentTier) * 0.5);
            }
            if (cell.Contested && cell.WarScarred)
                cell.Population = Math.Max(0, cell.Population * ScarShrink);
        }
    }
}
