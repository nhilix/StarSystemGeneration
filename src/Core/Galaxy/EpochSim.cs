using System;
using System.Collections.Generic;
using System.Linq;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch-sim orchestrator (economy spec §3): each epoch runs the four
/// global phases in order — income → allocation → action → resolution. Stage 5's
/// news phase will slot in before allocation. Deterministic iteration throughout:
/// cells by SpiralIndex, polities by Id, wars by Id.</summary>
public static class EpochSim
{
    public static void Run(GalaxySkeleton s)
    {
        for (int epoch = 0; epoch < s.Config.EpochCount; epoch++)
        {
            IncomePhase.Run(s, epoch);
            var expansionBudgets = AllocationPhase.Run(s, epoch);
            ActionPhase.Run(s, epoch, expansionBudgets);
            ResolutionPhase.Run(s, epoch);
        }
    }

    internal static List<RegionCell> Owned(GalaxySkeleton s, Polity p) =>
        s.Cells.Where(c => c.OwnerPolityId == p.Id).ToList();

    /// <summary>Species-relative terrain (regional spec §6.1): how comfortable a
    /// cell's expected world mix is for an embodiment. Read by expansion cost and
    /// provisions production.</summary>
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
}
