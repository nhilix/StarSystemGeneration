using System;
using System.Collections.Generic;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Genesis;

/// <summary>Discrete cosmic features (genesis/cosmic-genesis.md §Discrete
/// features): sparse, identified, dated objects that interact with the field
/// stack rather than bypassing it. One engine instance per cosmic run —
/// rolls its schedule up front (deterministic per config), applies effects
/// per step, and finalizes the emergent features (nebulae) at the end.
/// Structural constants here are mechanics (data-as-code, TUNING.md
/// §Structural); the dials are GalaxyConfig.Cosmic.</summary>
internal sealed class CosmicFeatureEngine
{
    // -- mergers (structural; Cosmic.MergerCount/MergerScale are the dials) --
    /// <summary>Merger mass as a fraction of the primordial per-cell budget
    /// times the cell count (a few percent of the galaxy).</summary>
    private const double MergerMassFraction = 0.05;
    /// <summary>Share of merger mass arriving as gas; the rest is stars.</summary>
    private const double MergerGasShare = 0.6;
    /// <summary>Metallicity of merger gas — dwarfs are metal-poor; their
    /// streams read as a foreign signature.</summary>
    private const double MergerGasZ = 0.001;
    /// <summary>Locked metallicity of merger stars.</summary>
    private const double MergerStarZ = 0.004;
    /// <summary>Star-formation multiplier of the traveling starburst.</summary>
    private const double StarburstMultiplier = 3.0;
    /// <summary>Potential bump a passing merger leaves at each trail cell.</summary>
    private const double MergerPerturb = 0.35;
    /// <summary>Per-step decay of merger potential perturbations.</summary>
    private const double PerturbDecay = 0.85;
    /// <summary>Mergers arrive inside this window of the formation history.</summary>
    private const double ArrivalWindowStart = 0.15, ArrivalWindowSpan = 0.55;

    // -- globulars (structural; Cosmic.GlobularCount is the dial) --
    /// <summary>Stellar mass of one globular (old cohort, injected whole).</summary>
    private const double GlobularMass = 0.6;
    /// <summary>Locked metallicity of globular stars — ancient and metal-poor.</summary>
    private const double GlobularZ = 0.002;
    /// <summary>Inflow weight multiplier at globular cells (near-zero gas).</summary>
    public const double GlobularInflowFactor = 0.05;

    // -- AGN (structural; Cosmic.AgnActivity is the dial) --
    /// <summary>Core gas above which the nucleus can feed — set against the
    /// simulated core trajectory (peaks ~1.3 at radius 8; the deep well
    /// burns its gas as fast as it collects it).</summary>
    private const double AgnFeedThreshold = 0.8;
    /// <summary>Share of core gas consumed by an outburst (grows the central
    /// remnant mass — conserved, not deleted).</summary>
    private const double AgnConsumeShare = 0.4;
    /// <summary>Minimum steps between outbursts at activity 1.</summary>
    private const int AgnCooldownSteps = 12;
    /// <summary>Trigger chance per fed step at activity 1.</summary>
    private const double AgnTriggerChance = 0.35;
    /// <summary>Metals the wave deposits per swept cell (created, ledgered).</summary>
    private const double AgnWaveEnrichment = 0.002;
    /// <summary>The nucleus is quiescent for this trailing fraction of the
    /// history — present day inherits a quiet core.</summary>
    private const double AgnQuietTailFraction = 0.15;

    // -- nebulae (structural, finalization) --
    /// <summary>Gas threshold over the galaxy mean for nebula membership —
    /// low enough that arm-ridge concentrations connect into regions
    /// instead of reading as isolated single-cell peaks.</summary>
    private const double NebulaGasThreshold = 1.8;
    /// <summary>Largest named nebulae kept per galaxy.</summary>
    private const int NebulaCap = 8;
    /// <summary>Minimum contiguous cells for a named nebula.</summary>
    private const int NebulaMinCells = 2;
    /// <summary>Recent-death threshold over the mean for a supernova remnant.</summary>
    private const double SnRemnantThreshold = 3.0;
    /// <summary>Supernova remnants kept per galaxy.</summary>
    private const int SnRemnantCap = 6;

    private sealed class MergerPlan
    {
        public int ArrivalStep, DurationSteps;
        public double Mass;
        public double EntryX, EntryY, TargetX, TargetY;
        public GalacticFeature? Feature;
    }

    private readonly List<MergerPlan> _mergers = new();
    private readonly List<int> _globularCells = new();
    private int _lastOutburstStep = -1000;   // not MinValue: step - MinValue overflows
    private int _featureOrdinal;

    private readonly GalaxySkeleton _skeleton;
    private readonly GalaxyConfig _config;

    public CosmicFeatureEngine(GalaxySkeleton skeleton)
    {
        _skeleton = skeleton;
        _config = skeleton.Config;
        ulong seed = _config.MasterSeed;
        var cosmic = _config.Cosmic;

        // merger schedule, sorted by arrival so feature ids read chronologically
        int mergerCount = Math.Max(0, (int)Math.Round(cosmic.MergerCount
            * (0.5 + EpochRolls.NextDouble(seed, RollChannel.CosmicMergerSchedule, 0, 0))));
        double rim = DensityField.WorldRimRadius(_config);
        for (int m = 0; m < mergerCount; m++)
        {
            double Roll(int field) => EpochRolls.NextDouble(seed,
                RollChannel.CosmicMergerSchedule, 0, m + 1, field);
            double bearing = 2 * Math.PI * Roll(0);
            // the trail aims past the core with a perpendicular offset, so
            // streams curve by the center instead of dead-ending on it
            double offset = (Roll(4) - 0.5) * 0.5 * rim;
            _mergers.Add(new MergerPlan
            {
                ArrivalStep = (int)(CosmicSim.Steps
                    * (ArrivalWindowStart + ArrivalWindowSpan * Roll(1))),
                DurationSteps = 8 + (int)(6 * Roll(2)),
                Mass = _skeleton.Cells.Count * MergerMassFraction
                    * cosmic.MergerScale * (0.6 + 0.8 * Roll(3)),
                EntryX = rim * Math.Cos(bearing), EntryY = rim * Math.Sin(bearing),
                TargetX = -Math.Sin(bearing) * offset, TargetY = Math.Cos(bearing) * offset,
            });
        }
        _mergers.Sort((a, b) => a.ArrivalStep != b.ArrivalStep
            ? a.ArrivalStep.CompareTo(b.ArrivalStep)
            : a.Mass.CompareTo(b.Mass));

        int globularCount = Math.Max(0, (int)Math.Round(cosmic.GlobularCount
            * (0.7 + 0.6 * EpochRolls.NextDouble(seed, RollChannel.CosmicGlobularPlace, 0, 0))));
        int n = _skeleton.Cells.Count;
        for (int g = 0; g < globularCount && _globularCells.Count < n - 1; g++)
        {
            int pick = EpochRolls.NextInt(seed, RollChannel.CosmicGlobularPlace,
                0, g + 1, 1, n);   // never the core cell (index 0)
            while (_globularCells.Contains(pick))
                pick = pick % (n - 1) + 1;   // forward probe, still never 0
            _globularCells.Add(pick);
        }
    }

    /// <summary>Step-0 placements: ancient clusters injected whole as old,
    /// metal-poor stars (they formed before the disc).</summary>
    public void PlaceGlobulars(CosmicState s)
    {
        foreach (int i in _globularCells)
        {
            s.StarsOld[i] += GlobularMass;
            s.StarMetals[i] += GlobularMass * GlobularZ;
            s.InjectedTotal += GlobularMass;
            s.MetalsInjectedTotal += GlobularMass * GlobularZ;
            s.IsGlobularCell[i] = true;

            var cell = _skeleton.Cells[i];
            var feature = NewFeature(GalacticFeatureType.GlobularCluster,
                -CosmicSim.SpanGyr);
            feature.Cells.Add(cell.Coord);
            Chronicle(WorldEventType.GlobularFormed, 0, cell.Coord,
                GlobularMass, 0.0,
                new GlobularFormedPayload(feature.Id, feature.Name));
        }
    }

    /// <summary>Decay live perturbations, then add them onto the refreshed
    /// potential prior — called every step after the base refresh.</summary>
    public void PerturbPotential(CosmicState s)
    {
        for (int i = 0; i < s.CellCount; i++)
        {
            s.PotentialPerturb[i] *= PerturbDecay;
            s.Potential[i] += s.PotentialPerturb[i];
            s.StarburstBoost[i] = 1.0;
        }
    }

    /// <summary>Active mergers: gas + star injection along the trail cell, a
    /// traveling starburst, a decaying potential perturbation — and the
    /// chronicle entry on arrival.</summary>
    public void MergerStep(CosmicState s, int step)
    {
        foreach (var plan in _mergers)
        {
            int k = step - plan.ArrivalStep;
            if (k < 0 || k >= plan.DurationSteps) continue;

            double f = (k + 1) / (double)plan.DurationSteps;
            double x = plan.EntryX + (plan.TargetX - plan.EntryX) * f;
            double y = plan.EntryY + (plan.TargetY - plan.EntryY) * f;
            var cellCoord = HexGrid.CellOf(HexGrid.WorldToHex(x, y));
            if (!_skeleton.TryGetCell(cellCoord, out var cell)) continue;
            int i = cell.SpiralIndex;

            if (plan.Feature == null)
            {
                plan.Feature = NewFeature(GalacticFeatureType.MergerStream,
                    -CosmicSim.SpanGyr + plan.ArrivalStep * CosmicSim.GyrPerStep);
                Chronicle(WorldEventType.DwarfGalaxyMerged, step, cellCoord,
                    plan.Mass, 0.0, new DwarfGalaxyMergedPayload(
                        plan.Feature.Id, plan.Feature.Name, plan.Mass));
            }
            if (plan.Feature.Cells.Count == 0
                || !plan.Feature.Cells[plan.Feature.Cells.Count - 1].Equals(cellCoord))
                plan.Feature.Cells.Add(cellCoord);

            double gasIn = plan.Mass * MergerGasShare / plan.DurationSteps;
            double starsIn = plan.Mass * (1 - MergerGasShare) / plan.DurationSteps;
            s.Gas[i] += gasIn;
            s.MetalsIsm[i] += gasIn * MergerGasZ;
            s.StarsYoung[i] += starsIn;
            s.StarMetals[i] += starsIn * MergerStarZ;
            s.InjectedTotal += gasIn + starsIn;
            s.MetalsInjectedTotal += gasIn * MergerGasZ + starsIn * MergerStarZ;

            s.StarburstBoost[i] = StarburstMultiplier;
            foreach (int neighbor in s.Neighbors[i])
                s.StarburstBoost[neighbor] = StarburstMultiplier;
            s.PotentialPerturb[i] += MergerPerturb;
        }
    }

    /// <summary>Accretion epochs trigger on merger/gas feeding; each
    /// outburst emits a sterilization/enrichment wave over an inner radius.
    /// Quiescent at present day.</summary>
    public void AgnStep(CosmicState s, int step)
    {
        double activity = _config.Cosmic.AgnActivity;
        if (activity <= 0) return;
        if (step >= CosmicSim.Steps * (1 - AgnQuietTailFraction)) return;
        if (step - _lastOutburstStep < AgnCooldownSteps / Math.Max(0.25, activity)) return;

        bool mergerFeeding = false;
        foreach (var plan in _mergers)
        {
            int k = step - plan.ArrivalStep;
            if (k < 0 || k >= plan.DurationSteps || plan.Feature == null) continue;
            var last = plan.Feature.Cells[plan.Feature.Cells.Count - 1];
            if (HexGrid.Distance(last, new HexCoordinate(0, 0)) <= 2)
            { mergerFeeding = true; break; }
        }
        if (!mergerFeeding && s.Gas[0] < AgnFeedThreshold) return;
        if (EpochRolls.NextDouble(_config.MasterSeed, RollChannel.CosmicAgnTrigger,
                step, 0) >= AgnTriggerChance * activity) return;

        _lastOutburstStep = step;
        int radius = 2 + (int)Math.Round(1.5 * activity);
        var feature = NewFeature(GalacticFeatureType.AgnOutburst,
            -CosmicSim.SpanGyr + step * CosmicSim.GyrPerStep);

        var core = new HexCoordinate(0, 0);
        foreach (var cell in _skeleton.Cells)
        {
            if (HexGrid.Distance(cell.Coord, core) > radius) continue;
            int i = cell.SpiralIndex;
            s.LastSterilizationStep[i] = step;
            // the wave passes through globulars — their thin ISM holds
            // nothing, and their ancient stars must stay metal-poor
            if (!s.IsGlobularCell[i])
            {
                s.MetalsIsm[i] += AgnWaveEnrichment;
                s.MetalsCreatedTotal += AgnWaveEnrichment;
            }
            feature.Cells.Add(cell.Coord);
        }

        // accretion feeds the central mass — conserved into remnants
        double consumed = s.Gas[0] * AgnConsumeShare;
        double metalsAlong = s.Gas[0] > 0 ? s.MetalsIsm[0] * AgnConsumeShare : 0;
        s.Gas[0] -= consumed;
        s.Remnants[0] += consumed;
        s.MetalsIsm[0] -= metalsAlong;
        s.RemnantMetals[0] += metalsAlong;

        Chronicle(WorldEventType.AgnIgnited, step, core, radius, -0.8,
            new AgnIgnitedPayload(feature.Id, radius));
    }

    /// <summary>Emergent features at finalization: contiguous high-gas
    /// regions become named emission nebulae (star formation active) or dark
    /// clouds; recent massive-cohort deaths become supernova remnants.</summary>
    public void FinalizeEmergent(CosmicState s)
    {
        int n = s.CellCount;
        double meanGas = 0, meanSf = 0, meanDeaths = 0;
        for (int i = 0; i < n; i++)
        { meanGas += s.Gas[i]; meanSf += s.SfRecent[i]; meanDeaths += s.RecentDeaths[i]; }
        meanGas /= n; meanSf /= n; meanDeaths /= n;

        // flood-fill contiguous high-gas regions in spiral order
        double gasFloor = meanGas * NebulaGasThreshold;
        var assigned = new bool[n];
        var regions = new List<List<int>>();
        for (int i = 0; i < n; i++)
        {
            if (assigned[i] || s.Gas[i] < gasFloor) continue;
            var region = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(i); assigned[i] = true;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                region.Add(c);
                foreach (int neighbor in s.Neighbors[c])
                    if (!assigned[neighbor] && s.Gas[neighbor] >= gasFloor)
                    { assigned[neighbor] = true; queue.Enqueue(neighbor); }
            }
            if (region.Count >= NebulaMinCells) regions.Add(region);
        }
        regions.Sort((a, b) =>
        {
            double ga = 0, gb = 0;
            foreach (int i in a) ga += s.Gas[i];
            foreach (int i in b) gb += s.Gas[i];
            int byGas = gb.CompareTo(ga);
            return byGas != 0 ? byGas : a[0].CompareTo(b[0]);
        });
        for (int r = 0; r < regions.Count && r < NebulaCap; r++)
        {
            var region = regions[r];
            double sf = 0;
            foreach (int i in region) sf += s.SfRecent[i];
            var feature = NewFeature(sf / region.Count >= meanSf
                ? GalacticFeatureType.EmissionNebula
                : GalacticFeatureType.DarkCloud, 0.0);
            foreach (int i in region)
                feature.Cells.Add(_skeleton.Cells[i].Coord);
        }

        // supernova remnants: the loudest recent graveyards
        var candidates = new List<int>();
        for (int i = 0; i < n; i++)
            if (s.RecentDeaths[i] >= meanDeaths * SnRemnantThreshold)
                candidates.Add(i);
        candidates.Sort((a, b) =>
        {
            int byDeaths = s.RecentDeaths[b].CompareTo(s.RecentDeaths[a]);
            return byDeaths != 0 ? byDeaths : a.CompareTo(b);
        });
        for (int c = 0; c < candidates.Count && c < SnRemnantCap; c++)
        {
            var feature = NewFeature(GalacticFeatureType.SupernovaRemnant, 0.0);
            feature.Cells.Add(_skeleton.Cells[candidates[c]].Coord);
        }
    }

    private GalacticFeature NewFeature(GalacticFeatureType type, double dateGyr)
    {
        var feature = new GalacticFeature
        {
            Id = _skeleton.Features.Count,
            Type = type,
            Name = FeatureName(_featureOrdinal++),
            DateGyr = dateGyr,
        };
        _skeleton.Features.Add(feature);
        return feature;
    }

    private string FeatureName(int ordinal)
    {
        int syllables = 2 + (EpochRolls.NextDouble(_config.MasterSeed,
            RollChannel.CosmicFeatureName, 0, ordinal, 99) < 0.35 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(EpochRolls.NextDouble(
                _config.MasterSeed, RollChannel.CosmicFeatureName, 0, ordinal, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }

    private void Chronicle(WorldEventType type, int step, HexCoordinate cellCoord,
                           double magnitude, double valence, EventPayload payload)
    {
        long year = (long)Math.Round((-CosmicSim.SpanGyr
            + step * CosmicSim.GyrPerStep) * 1e9);
        _skeleton.DeepTimeEvents.Add(new WorldEvent(
            _skeleton.DeepTimeEvents.Count, year, ClockStratum.Cosmic, type,
            Array.Empty<int>(), HexGrid.CellCenter(cellCoord), magnitude, valence,
            EventVisibility.Public, payload));
    }
}
