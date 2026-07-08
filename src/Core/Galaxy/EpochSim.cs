using System;
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Stage-1 epoch loop (spec §7.9 stage 1): expansion, development, minimal
/// war, flat budgets. Stockpiles/commodities/diplomacy arrive in later stages.</summary>
public static class EpochSim
{
    public static void Run(GalaxySkeleton s)
    {
        for (int epoch = 0; epoch < s.Config.EpochCount; epoch++)
            foreach (var polity in s.Polities)   // fixed order: by id
            {
                if (polity.Extinct) continue;
                Expand(s, polity, epoch);
                Develop(s, polity, epoch);
                War(s, polity, epoch);
            }
    }

    private static RollContext Ctx(GalaxySkeleton s, Polity p) =>
        new(s.Config.MasterSeed, new HexCoordinate(p.CapitalCx, p.CapitalCy));

    private static List<RegionCell> Owned(GalaxySkeleton s, Polity p) =>
        s.Cells.Where(c => c.OwnerPolityId == p.Id).ToList();

    private static IEnumerable<RegionCell> Adjacent(GalaxySkeleton s, RegionCell cell)
    {
        if (cell.Cx > 0) yield return s.CellAt(cell.Cx - 1, cell.Cy);
        if (cell.Cx < s.Config.CellsX - 1) yield return s.CellAt(cell.Cx + 1, cell.Cy);
        if (cell.Cy > 0) yield return s.CellAt(cell.Cx, cell.Cy - 1);
        if (cell.Cy < s.Config.CellsY - 1) yield return s.CellAt(cell.Cx, cell.Cy + 1);
    }

    internal static double Affinity(SpeciesProfile species, RegionCell cell)
    {
        double baseAffinity = species.Embodiment switch
        {
            Embodiment.TerranAnalog => cell.Lean switch
            {
                StellarLean.YoungBright => 1.15, StellarLean.OldDim => 0.8,
                StellarLean.RemnantGraveyard => 0.4, _ => 1.0,
            },
            Embodiment.Aquatic => cell.Lean switch
            {
                StellarLean.YoungBright => 1.3, StellarLean.OldDim => 0.6,
                StellarLean.RemnantGraveyard => 0.3, _ => 1.0,
            },
            Embodiment.Cryophilic => cell.Lean switch
            {
                StellarLean.YoungBright => 0.6, StellarLean.OldDim => 1.3,
                StellarLean.RemnantGraveyard => 0.9, _ => 0.7,
            },
            Embodiment.Lithic => 0.5 + cell.Metallicity,
            _ => 1.0,   // Hive, Machine: broad tolerance
        };
        return baseAffinity + (1 - baseAffinity) * species.Adaptability * 0.5;
    }

    private static void Expand(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var owned = Owned(s, polity);
        if (owned.Count == 0) return;
        double budget = 2 + Math.Min(3, owned.Sum(c => c.DevelopmentTier) / 10.0);

        var frontier = owned.SelectMany(c => Adjacent(s, c))
            .Where(c => c.OwnerPolityId < 0 && !c.IsVoid)
            .Distinct()
            .Select(c => (cell: c, cost: Cost(species, c)))
            .OrderBy(t => t.cost).ThenBy(t => t.cell.LinearIndex(s.Config))
            .ToList();

        foreach (var (cell, cost) in frontier)
        {
            if (budget < cost) break;
            budget -= cost;
            cell.OwnerPolityId = polity.Id;
            cell.DevelopmentTier = 1;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.CellClaimed,
                ActorPolityId = polity.Id, Cx = cell.Cx, Cy = cell.Cy,
            });
        }
    }

    private static double Cost(SpeciesProfile species, RegionCell cell) =>
        1.0 / (0.05 + cell.MeanDensity * Affinity(species, cell))
        + (cell.IsChokepoint ? 2.0 : 0.0);

    private static void Develop(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var ctx = Ctx(s, polity);
        foreach (var cell in Owned(s, polity))
            if (ctx.NextDouble(RollChannel.SimDevelopment, epoch, cell.LinearIndex(s.Config))
                < species.Industry * 0.5)
                cell.DevelopmentTier = Math.Min(5, cell.DevelopmentTier + 1);
    }

    private static void War(GalaxySkeleton s, Polity polity, int epoch)
    {
        var species = s.Species[polity.SpeciesId];
        var ctx = Ctx(s, polity);
        if (ctx.NextDouble(RollChannel.SimWar, epoch, polity.Id) >= species.Militancy * 0.25)
            return;

        var owned = Owned(s, polity);
        var target = owned.SelectMany(c => Adjacent(s, c))
            .Where(c => c.OwnerPolityId >= 0 && c.OwnerPolityId != polity.Id)
            .OrderBy(c => c.LinearIndex(s.Config))
            .FirstOrDefault();
        if (target == null) return;

        var defender = s.Polities[target.OwnerPolityId];
        var defSpecies = s.Species[defender.SpeciesId];
        double attack = owned.Sum(c => c.DevelopmentTier) * (0.5 + species.Militancy);
        double defense = Owned(s, defender).Sum(c => c.DevelopmentTier) * (0.5 + defSpecies.Militancy);

        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.WarStarted,
            ActorPolityId = polity.Id, TargetPolityId = defender.Id,
            Cx = target.Cx, Cy = target.Cy, Magnitude = attack + defense,
        });
        target.WarScarred = true;
        if (attack <= defense) return;

        target.OwnerPolityId = polity.Id;
        s.Events.Add(new GalaxyEvent
        {
            Epoch = epoch, Type = GalaxyEventType.CellTaken,
            ActorPolityId = polity.Id, TargetPolityId = defender.Id,
            Cx = target.Cx, Cy = target.Cy, Magnitude = attack - defense,
        });

        if (defender.CapitalCx == target.Cx && defender.CapitalCy == target.Cy)
        {
            var remaining = Owned(s, defender)
                .OrderByDescending(c => c.DevelopmentTier)
                .ThenBy(c => c.LinearIndex(s.Config))
                .FirstOrDefault();
            if (remaining != null)
            {
                defender.CapitalCx = remaining.Cx;
                defender.CapitalCy = remaining.Cy;
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.LostCapital,
                    ActorPolityId = polity.Id, TargetPolityId = defender.Id,
                    Cx = target.Cx, Cy = target.Cy,
                });
            }
        }
        if (!Owned(s, defender).Any())
        {
            defender.Extinct = true;   // retained in registry, flagged (spec §7)
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.PolityExtinct,
                ActorPolityId = polity.Id, TargetPolityId = defender.Id,
                Cx = target.Cx, Cy = target.Cy,
            });
        }
    }
}
