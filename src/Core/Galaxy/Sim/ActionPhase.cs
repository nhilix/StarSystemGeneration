using System;
using System.Collections.Generic;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 3 (economy spec §3/§6): expansion spends its budget on
/// frontier cells (stage-1 affinity cost model unchanged); militancy-gated war
/// declarations create persistent War objects with deficit-driven goals.</summary>
public static class ActionPhase
{
    private const double MinStockpileToDeclare = 0.5;
    private const int MaxGoalCells = 3;

    public static void Run(GalaxySkeleton s, int epoch, Dictionary<int, double> expansionBudgets)
    {
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) continue;
            Expand(s, polity, epoch,
                expansionBudgets.TryGetValue(polity.Id, out var b) ? b : 0.0);
            DeclareWar(s, polity, epoch);
        }
    }

    private static void Expand(GalaxySkeleton s, Polity polity, int epoch, double budget)
    {
        if (budget <= 0) return;
        var species = s.Species[polity.SpeciesId];
        var owned = EpochSim.Owned(s, polity);
        if (owned.Count == 0) return;

        var seen = new HashSet<int>();
        var frontier = new List<(RegionCell cell, double cost)>();
        foreach (var cell in owned)
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId < 0 && !n.IsVoid
                    && seen.Add(n.SpiralIndex))
                    frontier.Add((n, Cost(species, n)));
        frontier.Sort((x, y) => x.cost != y.cost
            ? x.cost.CompareTo(y.cost)
            : x.cell.SpiralIndex.CompareTo(y.cell.SpiralIndex));

        foreach (var (cell, cost) in frontier)
        {
            if (budget < cost) break;
            budget -= cost;
            cell.OwnerPolityId = polity.Id;
            cell.DevelopmentTier = 1;
            cell.Population = 0.1;
            cell.PopulationSpeciesId = species.Id;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.CellClaimed,
                ActorPolityId = polity.Id, Q = cell.Q, R = cell.R,
            });
        }
    }

    /// <summary>Stage-1 cost model, unchanged (spec §3 architecture note).</summary>
    private static double Cost(SpeciesProfile species, RegionCell cell) =>
        1.0 / (0.05 + cell.MeanDensity * EpochSim.Affinity(species, cell))
        + (cell.IsChokepoint ? 2.0 : 0.0);

    private static void DeclareWar(GalaxySkeleton s, Polity polity, int epoch)
    {
        if (polity.MilitaryStockpile < MinStockpileToDeclare) return;
        var species = s.Species[polity.SpeciesId];
        var ctx = new RollContext(s.Config.MasterSeed, polity.CapitalCoord);
        if (ctx.NextDouble(RollChannel.SimWar, epoch, polity.Id) >= species.Militancy * 0.25)
            return;

        // Border cells of neighbors we are NOT already at war with, by owner.
        var candidates = new List<RegionCell>();
        var seen = new HashSet<int>();
        foreach (var cell in EpochSim.Owned(s, polity))
            foreach (var nc in HexGrid.Neighbors(cell.Coord))
                if (s.TryGetCell(nc, out var n) && n.OwnerPolityId >= 0
                    && n.OwnerPolityId != polity.Id
                    && !s.AtWar(polity.Id, n.OwnerPolityId)
                    && seen.Add(n.SpiralIndex))
                    candidates.Add(n);
        if (candidates.Count == 0) return;

        // Goal type from worst deficit (spec §6); no meaningful deficit →
        // chokepoint seizure if available, else punitive by system value.
        WarGoal goal;
        Func<RegionCell, double> score;
        if (polity.OreBalance < 0 && polity.OreBalance <= polity.ExoticsBalance)
        { goal = WarGoal.Ore; score = Economy.OrePotential; }
        else if (polity.ExoticsBalance < 0)
        { goal = WarGoal.Exotics; score = Economy.ExoticsPotential; }
        else if (candidates.Exists(c => c.IsChokepoint))
        { goal = WarGoal.Chokepoint; score = c => c.IsChokepoint ? 1.0 : 0.0; }
        else
        { goal = WarGoal.Punitive; score = c => Economy.SystemValue(species, c); }

        RegionCell? best = null;
        double bestScore = double.MinValue;
        foreach (var c in candidates)
        {
            double v = score(c);
            if (v > bestScore || (v == bestScore && best != null && c.SpiralIndex < best.SpiralIndex))
            { best = c; bestScore = v; }
        }
        if (best == null) return;
        int defenderId = best.OwnerPolityId;
        var defender = s.Polities[defenderId];

        var war = new War
        {
            Id = s.Wars.Count, AttackerId = polity.Id, DefenderId = defenderId,
            StartEpoch = epoch, Goal = goal,
        };
        war.GoalCells.Add(best.Coord);
        foreach (var nc in HexGrid.Neighbors(best.Coord))
        {
            if (war.GoalCells.Count >= MaxGoalCells) break;
            if (s.TryGetCell(nc, out var n) && n.OwnerPolityId == defenderId)
                war.GoalCells.Add(n.Coord);
        }
        foreach (var gc in war.GoalCells)
        {
            war.FrontCells.Add(gc);
            s.CellAt(gc).Contested = true;
        }
        s.Wars.Add(war);
        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.WarStarted,
            ActorPolityId = polity.Id, TargetPolityId = defenderId,
            Q = best.Q, R = best.R, Detail = (int)goal,
            Magnitude = polity.MilitaryStockpile + defender.MilitaryStockpile,
        });
    }
}
