using System;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Genesis;

/// <summary>The cosmic clock (genesis/cosmic-genesis.md): a deep-time
/// structure simulation over the region-cell lattice. ~140 steps across the
/// compressed ~14 Gyr; fixed spiral-index order; rolls keyed
/// (step, cell, channel). Structure is the residue of simulated formation —
/// gas gathers, stars form and age, supernovae enrich — never analytic
/// paint. Structural constants below define the mechanics (data-as-code,
/// TUNING.md §Structural); the calibration dials live in
/// GalaxyConfig.Cosmic behind GalaxyKnobRegistry.</summary>
public static class CosmicSim
{
    // -- the deep-time clock (structural) --
    /// <summary>Deep-time steps across the formation history.</summary>
    public const int Steps = 140;
    /// <summary>Compressed span of the cosmic clock in Gyr.</summary>
    public const double SpanGyr = 14.0;
    public const double GyrPerStep = SpanGyr / Steps;

    // -- inflow (structural) --
    /// <summary>Primordial inflow lands during this leading fraction of the
    /// history — after it, the galaxy evolves what it has gathered.</summary>
    private const double InflowEndFraction = 0.35;
    /// <summary>Total primordial gas budget per cell (mass units are
    /// arbitrary: the present-day field is normalized to MeanDensityTarget
    /// at finalization).</summary>
    private const double InflowPerCell = 1.0;
    /// <summary>Potential exponent in the inflow weighting — sharper than
    /// linear so voids stay empty where potential plus clumping never
    /// gathered gas.</summary>
    private const double InflowPotentialExponent = 1.5;
    /// <summary>Noise floor of the inflow clump roll: weight noise spans
    /// [floor, 1] so no cell is force-fed against a cold roll.</summary>
    private const double InflowClumpFloor = 0.15;

    // -- transport (structural) --
    /// <summary>Share of a cell's gas that drifts up the potential gradient
    /// per step (arms and core collect gas). Kept gentle: 140 compounding
    /// steps of aggressive drift funnel the whole disc onto ridge cells and
    /// the map turns to voids (task-4 lesson).</summary>
    private const double DriftRate = 0.04;
    /// <summary>Share of a cell's gas spread evenly to neighbors per step —
    /// the counterweight that keeps the disc broad.</summary>
    private const double DiffusionRate = 0.04;

    // -- star formation (structural base; Cosmic.StarFormationEfficiency scales it) --
    /// <summary>Gas fraction converted per step at unit compression.</summary>
    private const double SfBase = 0.04;
    /// <summary>Potential exponent of the compression term.</summary>
    private const double CompressionExponent = 1.5;
    /// <summary>Exponential decay of the recent-star-formation window per
    /// step (~0.7 Gyr memory).</summary>
    private const double SfRecentDecay = 0.85;

    // -- aging (structural world-time rates, per step) --
    /// <summary>Young → Mid cohort flow (~0.4 Gyr young lifetime).</summary>
    private const double YoungToMid = 0.25;
    /// <summary>Mid → Old cohort flow (~4 Gyr mid lifetime).</summary>
    private const double MidToOld = 0.025;
    /// <summary>Old → Remnants trickle (the graveyard accumulates).</summary>
    private const double OldToRemnants = 0.008;

    // -- death & enrichment (structural; Cosmic.EnrichmentRate scales yield) --
    /// <summary>Share of the young cohort dying fast per step — the massive
    /// fraction.</summary>
    private const double YoungDeathRate = 0.08;
    /// <summary>Share of dying mass returned to gas (the rest is remnants).</summary>
    private const double GasReturnFraction = 0.45;
    /// <summary>New metal mass created per unit of dying young mass.</summary>
    private const double MetalYield = 0.15;
    /// <summary>Share of newly created metals spilled evenly to neighbors —
    /// enrichment travels.</summary>
    private const double EnrichmentSpill = 0.25;

    // -- habitability (structural) --
    /// <summary>Stellar metallicity (StarZ) above which worlds can start
    /// life — the evolutionary clock reads the crossing step. Placed inside
    /// the simulated distribution (median end-of-history Z ≈ 0.017, core max
    /// ≈ 0.06): roughly the upper half of cells cross, staggered from
    /// mid-history to never (rim and early-burned cells stay barren).</summary>
    public const double LifeViableZFloor = 0.012;

    /// <summary>Run the full formation history. Writes the feature registry
    /// and deep-time chronicle onto the skeleton (replacing any previous
    /// run's). The observer (if any) sees each completed step; observation
    /// never changes the run.</summary>
    public static CosmicState Run(GalaxySkeleton skeleton,
                                  Action<CosmicFrame>? observer = null)
    {
        skeleton.Features.Clear();
        skeleton.DeepTimeEvents.Clear();
        var s = new CosmicState(skeleton);
        var config = skeleton.Config;
        var features = new CosmicFeatureEngine(skeleton);
        int inflowSteps = (int)(Steps * InflowEndFraction);

        for (int step = 0; step < Steps; step++)
        {
            double t01 = step / (double)(Steps - 1);
            RefreshPotential(s, t01);
            features.PerturbPotential(s);
            if (step == 0) features.PlaceGlobulars(s);
            if (step < inflowSteps) Inflow(s, config, step, inflowSteps);
            Transport(s);
            features.MergerStep(s, step);
            StarFormation(s, config, step);
            AgeCohorts(s);
            DeathAndEnrichment(s, config);
            features.AgnStep(s, step);
            TrackHabitability(s, step);
            observer?.Invoke(new CosmicFrame(step, Steps,
                -SpanGyr + (step + 1) * GyrPerStep, s));
        }
        features.FinalizeEmergent(s);
        return s;
    }

    private static void RefreshPotential(CosmicState s, double t01)
    {
        var cells = s.Skeleton.Cells;
        for (int i = 0; i < cells.Count; i++)
            s.Potential[i] = GalaxyPotential.AtCell(s.Skeleton.Config, cells[i].Coord, t01);
    }

    /// <summary>Primordial gas lands potential-weighted, noise-clumped —
    /// step 1 of the loop.</summary>
    private static void Inflow(CosmicState s, GalaxyConfig config,
                               int step, int inflowSteps)
    {
        int n = s.CellCount;
        double budget = InflowPerCell * n / inflowSteps;
        var weights = new double[n];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double clump = InflowClumpFloor + (1 - InflowClumpFloor)
                * EpochRolls.NextDouble(config.MasterSeed,
                    RollChannel.CosmicInflowClump, step, i);
            weights[i] = Math.Pow(s.Potential[i], InflowPotentialExponent) * clump;
            if (s.IsGlobularCell[i])
                weights[i] *= CosmicFeatureEngine.GlobularInflowFactor;
            sum += weights[i];
        }
        if (sum <= 0) return;
        for (int i = 0; i < n; i++)
        {
            double landed = budget * weights[i] / sum;
            s.Gas[i] += landed;
            s.InflowTotal += landed;
        }
    }

    /// <summary>Gas drifts along the potential gradient plus slight
    /// diffusion. Flows are computed from a snapshot and applied as deltas,
    /// so the result is order-independent and mass-exact.</summary>
    private static void Transport(CosmicState s)
    {
        int n = s.CellCount;
        var deltaGas = new double[n];
        var deltaMetals = new double[n];

        for (int i = 0; i < n; i++)
        {
            double gas = s.Gas[i];
            if (gas <= 0) continue;
            var neighbors = s.Neighbors[i];
            if (neighbors.Length == 0) continue;

            // up-gradient drift, split proportional to the potential rise;
            // gas flows around globular cells (compact halo objects don't
            // accrete the disc), keeping them gas-starved and metal-poor
            double riseSum = 0;
            for (int k = 0; k < neighbors.Length; k++)
                if (!s.IsGlobularCell[neighbors[k]])
                    riseSum += Math.Max(0, s.Potential[neighbors[k]] - s.Potential[i]);

            double metalPerGas = s.MetalsIsm[i] / gas;
            if (riseSum > 0)
            {
                double drifted = gas * DriftRate;
                for (int k = 0; k < neighbors.Length; k++)
                {
                    if (s.IsGlobularCell[neighbors[k]]) continue;
                    double rise = Math.Max(0, s.Potential[neighbors[k]] - s.Potential[i]);
                    if (rise <= 0) continue;
                    double moved = drifted * rise / riseSum;
                    deltaGas[i] -= moved; deltaGas[neighbors[k]] += moved;
                    double metals = moved * metalPerGas;
                    deltaMetals[i] -= metals; deltaMetals[neighbors[k]] += metals;
                }
            }

            // slight diffusion, evenly split over non-globular neighbors
            double diffused = gas * DiffusionRate / neighbors.Length;
            for (int k = 0; k < neighbors.Length; k++)
            {
                if (s.IsGlobularCell[neighbors[k]]) continue;
                deltaGas[i] -= diffused; deltaGas[neighbors[k]] += diffused;
                double metals = diffused * metalPerGas;
                deltaMetals[i] -= metals; deltaMetals[neighbors[k]] += metals;
            }
        }

        for (int i = 0; i < n; i++)
        {
            s.Gas[i] += deltaGas[i];
            s.MetalsIsm[i] += deltaMetals[i];
        }
    }

    /// <summary>Rate ∝ gas × potential compression × trigger noise;
    /// Gas → StarsYoung. New stars lock in the cell's current gas
    /// metallicity — when a region formed its stars determines how
    /// metal-rich its worlds are.</summary>
    private static void StarFormation(CosmicState s, GalaxyConfig config, int step)
    {
        int n = s.CellCount;
        double efficiency = SfBase * config.Cosmic.StarFormationEfficiency;
        for (int i = 0; i < n; i++)
        {
            double gas = s.Gas[i];
            if (gas <= 0) { s.SfRecent[i] *= SfRecentDecay; continue; }
            double trigger = 0.5 + EpochRolls.NextDouble(config.MasterSeed,
                RollChannel.CosmicSfTrigger, step, i);
            double rate = efficiency
                * Math.Pow(s.Potential[i], CompressionExponent) * trigger
                * s.StarburstBoost[i];
            double formed = Math.Min(gas, gas * rate);

            double metalsLocked = formed * (s.MetalsIsm[i] / gas);
            s.Gas[i] -= formed;
            s.MetalsIsm[i] -= metalsLocked;
            s.StarsYoung[i] += formed;
            s.StarMetals[i] += metalsLocked;
            s.SfRecent[i] = s.SfRecent[i] * SfRecentDecay + formed;
        }
    }

    /// <summary>Young → Mid → Old → Remnants at fixed world-time rates.</summary>
    private static void AgeCohorts(CosmicState s)
    {
        int n = s.CellCount;
        for (int i = 0; i < n; i++)
        {
            double toRemnants = s.StarsOld[i] * OldToRemnants;
            double toOld = s.StarsMid[i] * MidToOld;
            double toMid = s.StarsYoung[i] * YoungToMid;

            // aging carries the aggregate stellar metals along implicitly
            // (StarMetals tracks the living population as one pool); only the
            // Old → Remnants flow moves locked metals out, proportionally.
            double starMass = s.StarMass(i);
            if (toRemnants > 0 && starMass > 0)
            {
                double lockedOut = s.StarMetals[i] * toRemnants / starMass;
                s.StarMetals[i] -= lockedOut;
                s.RemnantMetals[i] += lockedOut;
            }

            s.StarsOld[i] += toOld - toRemnants;
            s.StarsMid[i] += toMid - toOld;
            s.StarsYoung[i] -= toMid;
            s.Remnants[i] += toRemnants;
        }
    }

    /// <summary>The massive fraction of young cohorts dies fast — returning
    /// gas, adding metals, spilling enrichment to neighboring cells.</summary>
    private static void DeathAndEnrichment(CosmicState s, GalaxyConfig config)
    {
        int n = s.CellCount;
        double yield = MetalYield * config.Cosmic.EnrichmentRate;
        var spill = new double[n];

        for (int i = 0; i < n; i++)
        {
            double dying = s.StarsYoung[i] * YoungDeathRate;
            s.RecentDeaths[i] = s.RecentDeaths[i] * SfRecentDecay + dying;
            if (dying <= 0) continue;

            double starMass = s.StarMass(i);
            double lockedShare = starMass > 0
                ? s.StarMetals[i] * dying / starMass : 0.0;
            double gasBack = dying * GasReturnFraction;

            s.StarsYoung[i] -= dying;
            s.Gas[i] += gasBack;
            s.Remnants[i] += dying - gasBack;
            s.StarMetals[i] -= lockedShare;
            s.MetalsIsm[i] += lockedShare * GasReturnFraction;
            s.RemnantMetals[i] += lockedShare * (1 - GasReturnFraction);

            double created = dying * yield;
            s.MetalsCreatedTotal += created;
            double kept = created * (1 - EnrichmentSpill);
            s.MetalsIsm[i] += kept;
            // spill skips globular cells: ejecta passes through the compact
            // ancient cluster — its stars must stay metal-poor forever
            var neighbors = s.Neighbors[i];
            int openNeighbors = 0;
            for (int k = 0; k < neighbors.Length; k++)
                if (!s.IsGlobularCell[neighbors[k]]) openNeighbors++;
            if (openNeighbors > 0)
            {
                double each = created * EnrichmentSpill / openNeighbors;
                for (int k = 0; k < neighbors.Length; k++)
                    if (!s.IsGlobularCell[neighbors[k]])
                        spill[neighbors[k]] += each;
            }
            else
                s.MetalsIsm[i] += created * EnrichmentSpill;
        }

        for (int i = 0; i < n; i++) s.MetalsIsm[i] += spill[i];
    }

    private static void TrackHabitability(CosmicState s, int step)
    {
        int n = s.CellCount;
        for (int i = 0; i < n; i++)
            if (s.LifeViableStep[i] < 0 && s.StarZ(i) >= LifeViableZFloor)
                s.LifeViableStep[i] = step;
    }
}
