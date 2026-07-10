using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Genesis;

/// <summary>The cosmic clock's working state: the conserved field stack per
/// region cell (genesis/cosmic-genesis.md §The field stack), indexed by cell
/// spiral index. Sim-internal — present-day quantities are compressed onto
/// RegionCell at finalization and this working state is never persisted.
/// Observers receive it read-only; mutating it from a watcher would fork
/// history (the watched run must stay byte-identical to the unwatched one).</summary>
public sealed class CosmicState
{
    public GalaxySkeleton Skeleton { get; }

    // -- the conserved mass stack (P4: inflow + injections == sum) --
    public double[] Gas { get; }
    public double[] StarsYoung { get; }
    public double[] StarsMid { get; }
    public double[] StarsOld { get; }
    public double[] Remnants { get; }

    // -- the metals ledger (created == ism + locked-in-stars + in-remnants) --
    /// <summary>Metal mass riding the gas — what new stars inherit.</summary>
    public double[] MetalsIsm { get; }
    /// <summary>Metal mass locked in living stars at their formation —
    /// derives present-day world metallicity (when a region formed its stars
    /// determines how metal-rich its worlds are).</summary>
    public double[] StarMetals { get; }
    /// <summary>Metal mass carried into stellar corpses — the
    /// mineral-richness signal (metals × remnant processing).</summary>
    public double[] RemnantMetals { get; }

    /// <summary>Star mass formed in the recent window (exponentially decayed)
    /// — derives present-day star-formation activity.</summary>
    public double[] SfRecent { get; }
    /// <summary>Current-step potential per cell (refreshed each step;
    /// includes merger perturbations once features land).</summary>
    public double[] Potential { get; }

    // -- habitability history (compressed to scalars at finalization) --
    /// <summary>First step the cell's stellar metallicity crossed the
    /// life-viable floor; -1 = never.</summary>
    public int[] LifeViableStep { get; }
    /// <summary>Last step a sterilization event (AGN wave, starburst) swept
    /// the cell; -1 = never.</summary>
    public int[] LastSterilizationStep { get; }

    /// <summary>In-galaxy neighbor spiral indices per cell, precomputed in
    /// spiral order.</summary>
    public int[][] Neighbors { get; }

    // -- conservation ledger totals --
    public double InflowTotal { get; internal set; }
    /// <summary>Mass injected by discrete features (mergers).</summary>
    public double InjectedTotal { get; internal set; }
    public double MetalsCreatedTotal { get; internal set; }
    /// <summary>Metals injected by discrete features (merger stars carry a
    /// foreign signature).</summary>
    public double MetalsInjectedTotal { get; internal set; }

    public int CellCount => Skeleton.Cells.Count;

    public CosmicState(GalaxySkeleton skeleton)
    {
        Skeleton = skeleton;
        int n = skeleton.Cells.Count;
        Gas = new double[n]; StarsYoung = new double[n]; StarsMid = new double[n];
        StarsOld = new double[n]; Remnants = new double[n];
        MetalsIsm = new double[n]; StarMetals = new double[n]; RemnantMetals = new double[n];
        SfRecent = new double[n]; Potential = new double[n];
        LifeViableStep = new int[n]; LastSterilizationStep = new int[n];
        for (int i = 0; i < n; i++)
        {
            LifeViableStep[i] = -1;
            LastSterilizationStep[i] = -1;
        }

        Neighbors = new int[n][];
        for (int i = 0; i < n; i++)
        {
            var list = new List<int>(6);
            foreach (var coord in HexGrid.Neighbors(skeleton.Cells[i].Coord))
                if (skeleton.TryGetCell(coord, out var neighbor))
                    list.Add(neighbor.SpiralIndex);
            Neighbors[i] = list.ToArray();
        }
    }

    /// <summary>Total baryon mass in a cell across the whole stack.</summary>
    public double TotalMass(int i) =>
        Gas[i] + StarsYoung[i] + StarsMid[i] + StarsOld[i] + Remnants[i];

    /// <summary>Total stellar mass (living cohorts) in a cell.</summary>
    public double StarMass(int i) => StarsYoung[i] + StarsMid[i] + StarsOld[i];

    /// <summary>Metallicity of the living stellar population — the world
    /// metallicity signal, 0 where no stars formed.</summary>
    public double StarZ(int i)
    {
        double stars = StarMass(i);
        return stars > 0 ? StarMetals[i] / stars : 0.0;
    }
}

/// <summary>One watched step of the cosmic clock, handed to observers after
/// the step completes. Read-only by contract: observation must not change
/// the run.</summary>
public readonly struct CosmicFrame
{
    public int Step { get; }
    public int StepCount { get; }
    /// <summary>Deep-time world-years relative to present day (negative:
    /// "-6.2 Gyr" reads as 6.2 billion years ago).</summary>
    public double WorldGyr { get; }
    public CosmicState State { get; }

    public CosmicFrame(int step, int stepCount, double worldGyr, CosmicState state)
    {
        Step = step; StepCount = stepCount; WorldGyr = worldGyr; State = state;
    }
}
