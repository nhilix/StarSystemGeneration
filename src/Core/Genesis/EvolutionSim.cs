using System;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Genesis;

/// <summary>One watched step of the evolutionary clock.</summary>
public readonly struct EvoFrame
{
    public int Step { get; }
    public int StepCount { get; }
    /// <summary>Deep-time world-years relative to present day (negative).</summary>
    public double WorldGyr { get; }
    public EvoState State { get; }

    public EvoFrame(int step, int stepCount, double worldGyr, EvoState state)
    {
        Step = step; StepCount = stepCount; WorldGyr = worldGyr; State = state;
    }
}

/// <summary>The evolutionary clock's working state, indexed by cell spiral
/// index. Read-only for observers (the watched run must equal the unwatched
/// one).</summary>
public sealed class EvoState
{
    public GalaxySkeleton Skeleton { get; }
    public bool[] Alive { get; }
    /// <summary>Evolutionary step of local abiogenesis; -1 dead/never.</summary>
    public int[] AbioStep { get; }
    public double[] Richness { get; }
    /// <summary>Catastrophes endured since abiogenesis.</summary>
    public int[] Setbacks { get; }
    /// <summary>Last catastrophe/sterilization step; -1000 = never.</summary>
    public int[] LastCatastropheStep { get; }
    /// <summary>Cell already registered a sapient origin.</summary>
    public bool[] OriginRegistered { get; }
    /// <summary>Hospitability multiplier from precursor sterilization scars
    /// (1 = unscarred): wars and collapses delay or erase downstream life —
    /// the emergence map carries their shadows.</summary>
    public double[] ScarPenalty { get; }
    public int[][] Neighbors { get; }

    public EvoState(GalaxySkeleton skeleton, int[][] neighbors)
    {
        Skeleton = skeleton;
        int n = skeleton.Cells.Count;
        Alive = new bool[n]; AbioStep = new int[n]; Richness = new double[n];
        Setbacks = new int[n]; LastCatastropheStep = new int[n];
        OriginRegistered = new bool[n];
        ScarPenalty = new double[n];
        Neighbors = neighbors;
        for (int i = 0; i < n; i++)
        { AbioStep[i] = -1; LastCatastropheStep[i] = -1000; ScarPenalty[i] = 1.0; }
    }

    /// <summary>Kill the biosphere at a cell (sterilization event).</summary>
    public void Sterilize(int i, int step)
    {
        Alive[i] = false;
        Richness[i] = 0.0;
        AbioStep[i] = -1;
        LastCatastropheStep[i] = step;
    }
}

/// <summary>The evolutionary clock (genesis/life-and-precursors.md): life
/// across the galaxy at ~35 Myr steps, running on the physical galaxy's
/// habitability history. Produces the biosphere residue and — the headline
/// output — the emergence schedule: sapient origins with causal spaceflight
/// dates. Precursor arcs consume the deep-time origins next.</summary>
public static class EvolutionSim
{
    /// <summary>Evolutionary steps across the same compressed span the
    /// cosmic clock covers (~35 Myr each).</summary>
    public const int Steps = 400;
    public const double GyrPerStep = CosmicSim.SpanGyr / Steps;

    // -- hospitability by terrain (structural): bright stable cells early,
    // rim and disturbed cells late or never --
    private const double GraveyardHospitability = 0.25;
    private const double YoungBrightHospitability = 0.7;   // supernova-washed
    private const double OldDimHospitability = 0.9;
    /// <summary>Richness gained per step at hospitability 1 (the sapience
    /// floor is ~3.2 Gyr of calm growth away).</summary>
    private const double RichnessGrowthPerStep = 0.006;
    /// <summary>Richness multiplier surviving a mass extinction.</summary>
    private const double CatastropheRichnessKeep = 0.5;
    /// <summary>Chance a catastrophe resets the cell outright.</summary>
    private const double CatastropheResetChance = 0.15;
    /// <summary>Richness floor for sapience registration.</summary>
    private const double SapienceRichnessFloor = 0.55;
    /// <summary>Steps of calm required before sapience can register.</summary>
    private const int SapienceStabilitySteps = 20;
    /// <summary>Maturation duration shape: scale × (1.6 − 0.8·richness) ×
    /// (1 + 0.25·setbacks) × (0.75 + 0.5·roll).</summary>
    private const double MaturationRichnessBase = 1.6, MaturationRichnessGain = 0.8;
    private const double MaturationSetbackPenalty = 0.25;
    private const double MaturationRollBase = 0.75, MaturationRollSpan = 0.5;

    // -- era cuts on the spaceflight date (structural) --
    /// <summary>Spaceflight earlier than this before present → a precursor
    /// wave (deep time enough to rise and fall).</summary>
    public const double PrecursorGapGyr = 0.05;
    /// <summary>Spaceflight later than this after present → a
    /// pre-spaceflight native (sapient terrain, no entry). Dates between
    /// project onto the generational window — honest narrative compression
    /// (frame/time.md).</summary>
    public const double CurrentHorizonGyr = 0.35;
    /// <summary>Spaceflight beyond this doesn't register at all — the
    /// species is too far from mattering, and it keeps genuine
    /// pre-spaceflight natives *rare* (the design's word) instead of 7% of
    /// the map.</summary>
    public const double NativeHorizonGyr = 0.7;

    /// <summary>Run the evolutionary history over a cosmic-finalized
    /// skeleton. Writes biosphere residue onto cells, the emergence schedule
    /// onto the skeleton, and the evolutionary chronicle stratum. Replaces
    /// any previous run's origins.</summary>
    public static EvoState Run(GalaxySkeleton skeleton,
                               Action<EvoFrame>? observer = null)
    {
        skeleton.Origins.Clear();
        var s = new EvoState(skeleton, BuildNeighbors(skeleton));
        var config = skeleton.Config;
        var arcs = new PrecursorArcEngine(skeleton, s);
        bool firstLife = false;

        for (int step = 0; step < Steps; step++)
        {
            Abiogenesis(s, config, step, ref firstLife);
            AgeAndEnrich(s, config);
            Catastrophes(s, config, step);
            Spread(s, config, step);
            RegisterSapience(s, config, step);
            arcs.Step(step);   // precursor waves live *inside* the loop:
                               // their scars and gardens shape what follows
            observer?.Invoke(new EvoFrame(step, Steps,
                -CosmicSim.SpanGyr + (step + 1) * GyrPerStep, s));
        }

        // present-day biosphere residue
        for (int i = 0; i < s.Alive.Length; i++)
        {
            var cell = skeleton.Cells[i];
            cell.BiosphereRichness = s.Alive[i] ? s.Richness[i] : 0.0;
            cell.BiosphereAgeGyr = s.Alive[i]
                ? (Steps - s.AbioStep[i]) * GyrPerStep : 0.0;
        }

        // one deep chronicle: the cosmic and evolutionary strata interleave
        // by world-year (stable on append order), ids resequenced
        skeleton.DeepTimeEvents.Sort((a, b) => a.WorldYear != b.WorldYear
            ? a.WorldYear.CompareTo(b.WorldYear) : a.Id.CompareTo(b.Id));
        for (int i = 0; i < skeleton.DeepTimeEvents.Count; i++)
            skeleton.DeepTimeEvents[i] = skeleton.DeepTimeEvents[i] with { Id = i };
        return s;
    }

    /// <summary>Viability is causal: the cosmic clock's metallicity-floor
    /// crossing gates when life *can* start here.</summary>
    private static bool ViableAt(RegionCell cell, int step) =>
        !cell.IsVoid && cell.LifeViableStep >= 0
        && StepGyr(step) >= CosmicStepGyr(cell.LifeViableStep);

    private static double StepGyr(int evoStep) =>
        -CosmicSim.SpanGyr + evoStep * GyrPerStep;
    private static double CosmicStepGyr(int cosmicStep) =>
        -CosmicSim.SpanGyr + cosmicStep * CosmicSim.GyrPerStep;

    private static double Hospitability(RegionCell cell) => cell.Lean switch
    {
        StellarLean.RemnantGraveyard => GraveyardHospitability,
        StellarLean.YoungBright => YoungBrightHospitability,
        StellarLean.OldDim => OldDimHospitability,
        _ => 1.0,
    };

    private static void Abiogenesis(EvoState s, GalaxyConfig config, int step,
                                    ref bool firstLife)
    {
        var cells = s.Skeleton.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            if (s.Alive[i] || !ViableAt(cells[i], step)) continue;
            double chance = config.Evolution.AbiogenesisRate
                * Hospitability(cells[i]) * s.ScarPenalty[i];
            if (EpochRolls.NextDouble(config.MasterSeed, RollChannel.EvoAbiogenesis,
                    step, i) >= chance) continue;
            s.Alive[i] = true;
            s.AbioStep[i] = step;
            s.Richness[i] = 0.02;
            if (!firstLife)
            {
                firstLife = true;
                Chronicle(s.Skeleton, WorldEventType.FirstLife, step,
                    cells[i].Coord, 0.0, 0.5, new FirstLifePayload());
            }
        }
    }

    private static void AgeAndEnrich(EvoState s, GalaxyConfig config)
    {
        var cells = s.Skeleton.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            if (!s.Alive[i]) continue;
            s.Richness[i] = Math.Min(1.0, s.Richness[i]
                + RichnessGrowthPerStep * Hospitability(cells[i]) * s.ScarPenalty[i]);
        }
    }

    /// <summary>0a sterilization events (AGN waves land at their cosmic
    /// step) plus rolled mass extinctions; precursor scars join with the
    /// arc sim.</summary>
    private static void Catastrophes(EvoState s, GalaxyConfig config, int step)
    {
        var cells = s.Skeleton.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            if (!s.Alive[i]) continue;
            var cell = cells[i];

            // AGN wave sweeping this cell during this evolutionary step
            if (cell.LastSterilizedStep >= 0)
            {
                double waveGyr = CosmicStepGyr(cell.LastSterilizedStep);
                if (waveGyr > StepGyr(step - 1) && waveGyr <= StepGyr(step))
                {
                    s.Alive[i] = false;
                    s.Richness[i] = 0.0;
                    s.AbioStep[i] = -1;
                    s.LastCatastropheStep[i] = step;
                    continue;
                }
            }

            if (EpochRolls.NextDouble(config.MasterSeed, RollChannel.EvoCatastrophe,
                    step, i) >= config.Evolution.CatastropheFrequency) continue;
            s.LastCatastropheStep[i] = step;
            s.Setbacks[i]++;
            if (EpochRolls.NextDouble(config.MasterSeed, RollChannel.EvoCatastrophe,
                    step, i, 1) < CatastropheResetChance)
            {
                s.Alive[i] = false;
                s.Richness[i] = 0.0;
                s.AbioStep[i] = -1;
            }
            else
                s.Richness[i] *= CatastropheRichnessKeep;
        }
    }

    /// <summary>Panspermia: life propagates to viable neighbors along
    /// habitability gradients — slow.</summary>
    private static void Spread(EvoState s, GalaxyConfig config, int step)
    {
        var cells = s.Skeleton.Cells;
        // seeded flags applied after the sweep so order can't matter
        var seeded = new bool[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            if (s.Alive[i] || !ViableAt(cells[i], step)) continue;
            var neighbors = s.Neighbors[i];
            for (int k = 0; k < neighbors.Length; k++)
            {
                int source = neighbors[k];
                if (!s.Alive[source]) continue;
                double chance = config.Evolution.SpreadRate * s.Richness[source]
                    * Hospitability(cells[i]) * s.ScarPenalty[i];
                if (EpochRolls.NextDouble(config.MasterSeed, RollChannel.EvoSpread,
                        step, i, k) < chance)
                { seeded[i] = true; break; }
            }
        }
        for (int i = 0; i < cells.Count; i++)
        {
            if (!seeded[i]) continue;
            s.Alive[i] = true;
            s.AbioStep[i] = step;
            s.Richness[i] = 0.05;   // arrives with a toehold ecology
        }
    }

    private static void RegisterSapience(EvoState s, GalaxyConfig config, int step)
    {
        var cells = s.Skeleton.Cells;
        for (int i = 0; i < cells.Count; i++)
        {
            if (!s.Alive[i] || s.OriginRegistered[i]) continue;
            if (s.Richness[i] < SapienceRichnessFloor) continue;
            if (step - s.LastCatastropheStep[i] < SapienceStabilitySteps) continue;
            double chance = config.Evolution.SapienceRate * s.Richness[i];
            if (EpochRolls.NextDouble(config.MasterSeed, RollChannel.EvoSapience,
                    step, i) >= chance) continue;

            s.OriginRegistered[i] = true;
            var cell = cells[i];
            int id = s.Skeleton.Origins.Count;

            long abioYear = GyrToYears(StepGyr(s.AbioStep[i]));
            long sapienceYear = GyrToYears(StepGyr(step));
            double maturationGyr = config.Evolution.MaturationScaleGyr
                * (MaturationRichnessBase - MaturationRichnessGain * s.Richness[i])
                * (1 + MaturationSetbackPenalty * s.Setbacks[i])
                * (MaturationRollBase + MaturationRollSpan
                    * EpochRolls.NextDouble(config.MasterSeed,
                        RollChannel.EvoMaturation, 0, id));
            // spaceflight = abiogenesis + maturation, never before sapience
            long spaceflightYear = Math.Max(sapienceYear + 1_000_000,
                abioYear + GyrToYears(maturationGyr));
            if (spaceflightYear > GyrToYears(NativeHorizonGyr))
            { s.OriginRegistered[i] = false; continue; }   // may re-roll richer later

            var origin = new SapientOrigin
            {
                Id = id,
                CellCoord = cell.Coord,
                Hex = PickOriginHex(s.Skeleton, cell, step),
                AbiogenesisYear = abioYear,
                SapienceYear = sapienceYear,
                SpaceflightYear = spaceflightYear,
                Richness = s.Richness[i],
                Setbacks = s.Setbacks[i],
                Era = spaceflightYear < GyrToYears(-PrecursorGapGyr)
                        ? OriginEra.Precursor
                    : spaceflightYear <= GyrToYears(CurrentHorizonGyr)
                        ? OriginEra.Current
                    : OriginEra.PreSpaceflight,
            };
            s.Skeleton.Origins.Add(origin);

            Chronicle(s.Skeleton, WorldEventType.SapienceEmerged, step,
                cell.Coord, s.Richness[i], 0.7, new SapienceEmergedPayload(id));
            if (origin.Era == OriginEra.Precursor)
                s.Skeleton.DeepTimeEvents.Add(new WorldEvent(
                    s.Skeleton.DeepTimeEvents.Count, spaceflightYear,
                    ClockStratum.Evolutionary, WorldEventType.SpaceflightReached,
                    Array.Empty<int>(), origin.Hex, 0.0, 0.7,
                    EventVisibility.Public, new SpaceflightReachedPayload(id)));
        }
    }

    /// <summary>Deterministic homeworld hex inside the origin cell.</summary>
    private static HexCoordinate PickOriginHex(GalaxySkeleton skeleton,
                                               RegionCell cell, int step)
    {
        var center = HexGrid.CellCenter(cell.Coord);
        int count = 0;
        foreach (var _ in HexGrid.Spiral(center, HexGrid.CellRadius)) count++;
        int pick = EpochRolls.NextInt(skeleton.Config.MasterSeed,
            RollChannel.EvoSapience, step, cell.SpiralIndex, 0, count, 1);
        int at = 0;
        foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
            if (at++ == pick) return hex;
        return center;
    }

    private static long GyrToYears(double gyr) => (long)Math.Round(gyr * 1e9);

    private static void Chronicle(GalaxySkeleton skeleton, WorldEventType type,
        int step, HexCoordinate cellCoord, double magnitude, double valence,
        EventPayload payload)
    {
        skeleton.DeepTimeEvents.Add(new WorldEvent(
            skeleton.DeepTimeEvents.Count,
            GyrToYears(-CosmicSim.SpanGyr + step * GyrPerStep),
            ClockStratum.Evolutionary, type, Array.Empty<int>(),
            HexGrid.CellCenter(cellCoord), magnitude, valence,
            EventVisibility.Public, payload));
    }

    private static int[][] BuildNeighbors(GalaxySkeleton skeleton)
    {
        int n = skeleton.Cells.Count;
        var neighbors = new int[n][];
        for (int i = 0; i < n; i++)
        {
            var list = new System.Collections.Generic.List<int>(6);
            foreach (var coord in HexGrid.Neighbors(skeleton.Cells[i].Coord))
                if (skeleton.TryGetCell(coord, out var neighbor))
                    list.Add(neighbor.SpiralIndex);
            neighbors[i] = list.ToArray();
        }
        return neighbors;
    }
}
