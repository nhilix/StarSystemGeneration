using System;
using System.Collections.Generic;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Genesis;

/// <summary>The coarse civ-arc sim (life-and-precursors.md §Precursor
/// waves): each deep-time sapient origin runs a low-fidelity spatial
/// civilization on the raster — capital, terrain-following expansion, a real
/// lane network, a cause-typed ending — so ruins have real geography.
/// Interleaved with the evolution loop so the living residue is *causal*:
/// sterilization scars delay downstream life, biosphere engineering
/// enriches it, transcendence seeds machine descendants. Structural
/// constants are mechanics (TUNING.md §Structural); dials live in
/// GalaxyConfig.Evolution.</summary>
internal sealed class PrecursorArcEngine
{
    // -- class shapes (structural catalog: extent, pace, peak) --
    /// <summary>Grand target extent as a cell-count fraction: base + span×vigor.</summary>
    private const double GrandExtentBase = 0.10, GrandExtentSpan = 0.15;
    private const int MinorExtentBase = 3, MinorExtentSpan = 6;
    private const int PocketExtentBase = 1, PocketExtentSpan = 2;
    /// <summary>Cells claimed per step: base + span×vigor.</summary>
    private const int GrandPaceBase = 1, GrandPaceSpan = 3;
    private const int MinorPaceBase = 1, MinorPaceSpan = 1;
    /// <summary>Peak hold in steps: base + span×vigor.</summary>
    private const int GrandPeakBase = 4, GrandPeakSpan = 8;
    private const int MinorPeakBase = 1, MinorPeakSpan = 2;
    private const int PocketPeakBase = 0, PocketPeakSpan = 1;
    /// <summary>Minor-class share of the non-grand roll span.</summary>
    private const double MinorShare = 0.55;
    /// <summary>Grand waves need at least this budget fraction left.</summary>
    private const double GrandMinBudgetFraction = 0.08;

    // -- end causes (structural catalog): weights per class, the design's
    // conditioning (pocket civs collapse and burn out more than they
    // transcend or fight) --
    private static readonly double[][] EndCauseWeights =
    {
        //             War   Cascade Transcend Plague
        new[] { 0.30, 0.20, 0.30, 0.20 },   // Grand
        new[] { 0.25, 0.35, 0.15, 0.25 },   // Minor
        new[] { 0.10, 0.45, 0.10, 0.35 },   // Pocket
    };

    // -- contact resolution (structural) --
    private const double ContactWarChance = 0.45, ContactAbsorbChance = 0.30;
    /// <summary>Strength = vigor + this × roll (upsets happen).</summary>
    private const double ContactUpsetSpan = 0.35;

    // -- residue (structural) --
    /// <summary>Hospitability multiplier left on sterilized cells.</summary>
    private const double ScarHospitability = 0.25;
    /// <summary>Engineering lifts a living biosphere to at least this.</summary>
    private const double EngineeredRichness = 0.85;
    /// <summary>Transcendence seeds a machine descendant this often.</summary>
    private const double MachineDescendantChance = 0.5;
    /// <summary>Descendant emergence lands in this band of the current era
    /// (fractions of CurrentHorizonGyr).</summary>
    private const double DescendantBandLow = 0.0, DescendantBandHigh = 0.8;

    private enum Phase { Rising, Peak, Ended }

    private sealed class LiveWave
    {
        public PrecursorWave Wave = null!;
        public Phase Phase = Phase.Rising;
        public int TargetExtent;
        public int PeakStepsLeft;
        public int PaceCellsPerStep;
        public readonly HashSet<int> EngineeredCells = new();
    }

    private readonly GalaxySkeleton _skeleton;
    private readonly GalaxyConfig _config;
    private readonly EvoState _evo;
    private readonly List<LiveWave> _live = new();
    /// <summary>Live ownership by cell spiral index (-1 = unowned); ended
    /// waves release their cells (ruins can be overbuilt).</summary>
    private readonly int[] _owner;
    private readonly HashSet<(int, int)> _partitioned = new();
    private int _grandCount;
    private int _budgetCellsLeft;

    public PrecursorArcEngine(GalaxySkeleton skeleton, EvoState evo)
    {
        _skeleton = skeleton;
        _config = skeleton.Config;
        _evo = evo;
        _owner = new int[skeleton.Cells.Count];
        for (int i = 0; i < _owner.Length; i++) _owner[i] = -1;
        _budgetCellsLeft = (int)(skeleton.Cells.Count
            * _config.Evolution.DomainBudgetFraction);
        skeleton.PrecursorWaves.Clear();
    }

    public void Step(int step)
    {
        double nowGyr = -CosmicSim.SpanGyr + step * EvolutionSim.GyrPerStep;
        bool eraOver = nowGyr >= -EvolutionSim.PrecursorGapGyr;

        // when the era closes, sweep with the era cutoff instead of step
        // time: origins dated inside the final step's window still wave
        // (degenerate capital arcs, force-ended below) — every deep-time
        // origin waves, no dead zone between step grid and era cut
        ActivateWaves(step, eraOver ? -EvolutionSim.PrecursorGapGyr : nowGyr);
        foreach (var wave in _live)
        {
            if (wave.Phase == Phase.Ended) continue;
            if (eraOver) { End(wave, step, forced: true); continue; }
            if (wave.Phase == Phase.Rising) Expand(wave, step);
            if (wave.Phase == Phase.Peak) PeakStep(wave, step);
        }
    }

    /// <summary>Origins whose deep-time spaceflight date has arrived plant
    /// their capital and begin the arc. Re-scans from 0 every step: the
    /// origins list grows mid-run (machine descendants append) and the
    /// not-yet-waved check stays authoritative.</summary>
    private void ActivateWaves(int step, double cutoffGyr)
    {
        var origins = _skeleton.Origins;
        for (int i = 0; i < origins.Count; i++)
        {
            var origin = origins[i];
            if (origin.Era != OriginEra.Precursor) continue;
            if (origin.SpaceflightYear > (long)(cutoffGyr * 1e9)) continue;
            if (AlreadyWaved(origin.Id)) continue;
            Activate(origin, step);
        }
    }

    private bool AlreadyWaved(int originId)
    {
        foreach (var w in _skeleton.PrecursorWaves)
            if (w.OriginId == originId) return true;
        return false;
    }

    private void Activate(SapientOrigin origin, int step)
    {
        int id = _skeleton.PrecursorWaves.Count;
        ulong seed = _config.MasterSeed;
        double classRoll = EpochRolls.NextDouble(seed, RollChannel.WaveVigor, 0, id);
        double vigor = EpochRolls.NextDouble(seed, RollChannel.WaveVigor, 0, id, 1);
        int cellCount = _skeleton.Cells.Count;

        VigorClass cls;
        if (classRoll < _config.Evolution.GrandChance
            && _grandCount < (int)Math.Round(_config.Evolution.GrandWaveLimit)
            && _budgetCellsLeft > cellCount * GrandMinBudgetFraction)
        { cls = VigorClass.Grand; _grandCount++; }
        else if (classRoll < _config.Evolution.GrandChance
                 + MinorShare * (1 - _config.Evolution.GrandChance))
            cls = VigorClass.Minor;
        else
            cls = VigorClass.Pocket;

        int target = cls switch
        {
            VigorClass.Grand => (int)(cellCount * (GrandExtentBase + GrandExtentSpan * vigor)),
            VigorClass.Minor => MinorExtentBase + (int)(MinorExtentSpan * vigor),
            _ => PocketExtentBase + (int)(PocketExtentSpan * vigor),
        };
        target = Math.Max(1, Math.Min(target, _budgetCellsLeft));
        _budgetCellsLeft -= target;

        var wave = new PrecursorWave
        {
            Id = id,
            OriginId = origin.Id,
            Name = WaveName(id),
            Class = cls,
            Vigor = vigor,
            CapitalHex = origin.Hex,
            RoseYear = origin.SpaceflightYear,
        };
        var capitalCell = _skeleton.CellForHex(origin.Hex);
        wave.Cells.Add(capitalCell.Coord);
        wave.PortHexes.Add(origin.Hex);
        // a capital rising inside a live wave's territory doesn't steal the
        // cell — it exists contested (ownership bookkeeping stays one-owner)
        if (_owner[capitalCell.SpiralIndex] < 0)
            _owner[capitalCell.SpiralIndex] = id;
        _skeleton.PrecursorWaves.Add(wave);

        _live.Add(new LiveWave
        {
            Wave = wave,
            TargetExtent = target,
            PaceCellsPerStep = cls switch
            {
                VigorClass.Grand => GrandPaceBase + (int)(GrandPaceSpan * vigor),
                VigorClass.Minor => MinorPaceBase + (int)(MinorPaceSpan * vigor),
                _ => 1,
            },
            PeakStepsLeft = cls switch
            {
                VigorClass.Grand => GrandPeakBase + (int)(GrandPeakSpan * vigor),
                VigorClass.Minor => MinorPeakBase + (int)(MinorPeakSpan * vigor),
                _ => PocketPeakBase + (int)(PocketPeakSpan * vigor),
            },
        });

        Chronicle(WorldEventType.PrecursorWaveRose, wave.RoseYear, origin.Hex,
            (int)cls, 0.6, new PrecursorWaveRosePayload(id, wave.Name, (int)cls));
    }

    /// <summary>Rise: claim traversable frontier cells (densest first),
    /// planting a port and a lane back into the network per claim.</summary>
    private void Expand(LiveWave live, int step)
    {
        var wave = live.Wave;
        for (int claim = 0; claim < live.PaceCellsPerStep; claim++)
        {
            if (wave.Cells.Count >= live.TargetExtent) { EnterPeak(live); return; }

            // frontier: best unowned traversable neighbor of the extent
            RegionCell? best = null;
            int bestFrom = -1;
            foreach (var (cellCoord, at) in Extent(wave))
            {
                foreach (var neighborCoord in HexGrid.Neighbors(cellCoord))
                {
                    if (!_skeleton.TryGetCell(neighborCoord, out var neighbor)) continue;
                    if (neighbor.IsVoid) continue;
                    int owner = _owner[neighbor.SpiralIndex];
                    if (owner == wave.Id) continue;
                    if (owner >= 0)
                    {
                        if (!Contact(live, owner, neighbor, step)) return;
                        continue;   // partitioned or contested — not claimable
                    }
                    if (best == null || neighbor.MeanDensity > best.MeanDensity
                        || (neighbor.MeanDensity == best.MeanDensity
                            && neighbor.SpiralIndex < best.SpiralIndex))
                    { best = neighbor; bestFrom = at; }
                }
            }
            if (best == null) { EnterPeak(live); return; }

            _owner[best.SpiralIndex] = wave.Id;
            wave.Cells.Add(best.Coord);
            wave.PortHexes.Add(PickPortHex(wave, best, wave.Cells.Count - 1));
            wave.Lanes.Add((bestFrom, wave.Cells.Count - 1));
        }
    }

    private IEnumerable<(HexCoordinate Coord, int Index)> Extent(PrecursorWave wave)
    {
        for (int i = 0; i < wave.Cells.Count; i++) yield return (wave.Cells[i], i);
    }

    /// <summary>Two live waves meet. War fells the weaker immediately;
    /// absorption ends it under the winner's flag; partition freezes the
    /// border. Returns false when the claiming wave itself fell.</summary>
    private bool Contact(LiveWave claiming, int otherId, RegionCell contested, int step)
    {
        var pairKey = (Math.Min(claiming.Wave.Id, otherId),
                       Math.Max(claiming.Wave.Id, otherId));
        if (_partitioned.Contains(pairKey)) return true;

        LiveWave? other = null;
        foreach (var w in _live)
            if (w.Wave.Id == otherId && w.Phase != Phase.Ended) { other = w; break; }
        if (other == null) return true;   // already ended this step

        ulong seed = _config.MasterSeed;
        double resolution = EpochRolls.NextDouble(seed, RollChannel.WaveContact,
            step, pairKey.Item1, pairKey.Item2);
        long year = YearAt(step);
        int outcome = resolution < ContactWarChance ? 0
            : resolution < ContactWarChance + ContactAbsorbChance ? 1 : 2;
        Chronicle(WorldEventType.PrecursorContact, year,
            HexGrid.CellCenter(contested.Coord), outcome,
            outcome == 0 ? -0.7 : -0.2,
            new PrecursorContactPayload(claiming.Wave.Id, otherId, outcome));

        if (outcome == 2)   // partition: an ancient border, no further claims
        {
            _partitioned.Add(pairKey);
            return true;
        }

        double sA = claiming.Wave.Vigor + ContactUpsetSpan
            * EpochRolls.NextDouble(seed, RollChannel.WaveContact, step, pairKey.Item1, 100);
        double sB = other.Wave.Vigor + ContactUpsetSpan
            * EpochRolls.NextDouble(seed, RollChannel.WaveContact, step, pairKey.Item2, 101);
        var (winner, loser) = sA >= sB ? (claiming, other) : (other, claiming);

        // mixed-provenance battlefield at the contested border
        AddSite(loser.Wave, PrecursorSiteType.Battlefield,
            PortHexOf(contested, loser.Wave), winner.Wave.Id, step);

        if (outcome == 0)
            End(loser, step, cause: WaveEndCause.War, victorId: winner.Wave.Id);
        else
            End(loser, step, cause: WaveEndCause.Absorbed, victorId: winner.Wave.Id);
        return !ReferenceEquals(loser, claiming);
    }

    private HexCoordinate PortHexOf(RegionCell cell, PrecursorWave wave)
    {
        for (int i = 0; i < wave.Cells.Count; i++)
            if (wave.Cells[i].Equals(cell.Coord)) return wave.PortHexes[i];
        return HexGrid.CellCenter(cell.Coord);
    }

    private void EnterPeak(LiveWave live) => live.Phase = Phase.Peak;

    /// <summary>Peak: optional biosphere engineering in territory, then the
    /// hold expires into the cause-typed ending.</summary>
    private void PeakStep(LiveWave live, int step)
    {
        var wave = live.Wave;
        for (int k = 0; k < wave.Cells.Count; k++)
        {
            var cell = _skeleton.CellAt(wave.Cells[k]);
            int i = cell.SpiralIndex;
            if (!_evo.Alive[i] || live.EngineeredCells.Contains(i)) continue;
            if (EpochRolls.NextDouble(_config.MasterSeed, RollChannel.WaveEngineer,
                    step, i, wave.Id) >= _config.Evolution.BioEngineeringRate) continue;
            live.EngineeredCells.Add(i);
            _evo.Richness[i] = Math.Max(_evo.Richness[i], EngineeredRichness);
            AddSite(wave, PrecursorSiteType.EngineeredBiosphere,
                wave.PortHexes[k], -1, step);
        }

        if (live.PeakStepsLeft-- <= 0) End(live, step, forced: false);
    }

    /// <summary>The ending: draw the cause (unless contact decided it),
    /// write the residue — sites, sterilization scars, dormant survivors,
    /// a possible machine descendant — and release the territory.</summary>
    private void End(LiveWave live, int step, bool forced = false,
                     WaveEndCause? cause = null, int victorId = -1)
    {
        var wave = live.Wave;
        if (live.Phase == Phase.Ended) return;
        live.Phase = Phase.Ended;

        if (cause == null)
        {
            double roll = EpochRolls.NextDouble(_config.MasterSeed,
                RollChannel.WaveEnd, step, wave.Id);
            var weights = EndCauseWeights[(int)wave.Class];
            double sum = 0;
            int pick = weights.Length - 1;
            for (int c = 0; c < weights.Length; c++)
            {
                sum += weights[c];
                if (roll < sum) { pick = c; break; }
            }
            cause = (WaveEndCause)pick;
        }
        wave.EndCause = cause.Value;
        wave.FellYear = YearAt(step);

        bool sterilizes = cause is WaveEndCause.War or WaveEndCause.Plague;
        for (int k = 0; k < wave.Cells.Count; k++)
        {
            var cell = _skeleton.CellAt(wave.Cells[k]);
            int i = cell.SpiralIndex;
            if (_owner[i] == wave.Id) _owner[i] = -1;   // never cross-release

            var siteType = k == 0 ? PrecursorSiteType.Capital
                : cause == WaveEndCause.Transcendence ? PrecursorSiteType.Megastructure
                : PrecursorSiteType.Ruins;
            AddSite(wave, siteType, wave.PortHexes[k],
                cause == WaveEndCause.Absorbed ? victorId : -1, step);

            if (sterilizes)
            {
                bool wasAlive = _evo.Alive[i];
                _evo.Sterilize(i, step);
                _evo.ScarPenalty[i] = Math.Min(_evo.ScarPenalty[i], ScarHospitability);
                if (wasAlive)
                    AddSite(wave, PrecursorSiteType.SterilizationScar,
                        wave.PortHexes[k], victorId, step);
            }
        }

        if (cause == WaveEndCause.Transcendence && !forced
            && EpochRolls.NextDouble(_config.MasterSeed, RollChannel.WaveDescendant,
                0, wave.Id) < MachineDescendantChance)
            SeedMachineDescendant(wave, step);

        Chronicle(WorldEventType.PrecursorWaveFell, wave.FellYear, wave.CapitalHex,
            wave.Cells.Count, cause == WaveEndCause.Transcendence ? 0.3 : -0.6,
            new PrecursorWaveFellPayload(wave.Id, wave.Name, (int)cause.Value,
                wave.Cells.Count));
    }

    /// <summary>A transcendence ending can seed a current-era
    /// machine-intelligence origin at the precursor capital — a present-day
    /// player with a real backstory.</summary>
    private void SeedMachineDescendant(PrecursorWave wave, int step)
    {
        double band = EpochRolls.NextDouble(_config.MasterSeed,
            RollChannel.WaveDescendant, 0, wave.Id, 1);
        long entry = (long)(1e9 * EvolutionSim.CurrentHorizonGyr
            * (DescendantBandLow + (DescendantBandHigh - DescendantBandLow) * band));
        var source = _skeleton.Origins[wave.OriginId];
        var descendant = new SapientOrigin
        {
            Id = _skeleton.Origins.Count,
            CellCoord = wave.Cells[0],
            Hex = wave.CapitalHex,
            AbiogenesisYear = source.AbiogenesisYear,
            SapienceYear = wave.FellYear,
            SpaceflightYear = entry,
            Richness = 0.9,
            Setbacks = 0,
            Era = OriginEra.Current,
            DescendantOfWaveId = wave.Id,
        };
        _skeleton.Origins.Add(descendant);
        wave.DescendantOriginId = descendant.Id;
        // the awakening is a sapience event like any other — machine
        // descendants are ordinary entries on the one schedule
        Chronicle(WorldEventType.SapienceEmerged, wave.FellYear, wave.CapitalHex,
            descendant.Richness, 0.7, new SapienceEmergedPayload(descendant.Id));
    }

    private void AddSite(PrecursorWave wave, PrecursorSiteType type,
                         HexCoordinate hex, int otherWaveId, int step)
    {
        var site = new PrecursorSite
        {
            Id = wave.Sites.Count,
            WaveId = wave.Id,
            Type = type,
            Hex = hex,
            OtherWaveId = otherWaveId,
            Dormant = type != PrecursorSiteType.SterilizationScar
                && type != PrecursorSiteType.EngineeredBiosphere
                && EpochRolls.NextDouble(_config.MasterSeed, RollChannel.WaveDormant,
                    0, wave.Id, wave.Sites.Count)
                   < _config.Evolution.DormantSurvivalRate,
        };
        wave.Sites.Add(site);
    }

    private HexCoordinate PickPortHex(PrecursorWave wave, RegionCell cell, int ordinal)
    {
        var center = HexGrid.CellCenter(cell.Coord);
        int count = 0;
        foreach (var _ in HexGrid.Spiral(center, HexGrid.CellRadius)) count++;
        int pick = EpochRolls.NextInt(_config.MasterSeed, RollChannel.WaveExpand,
            0, wave.Id, 0, count, ordinal);
        int at = 0;
        foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
            if (at++ == pick) return hex;
        return center;
    }

    private string WaveName(int id)
    {
        int syllables = 2 + (EpochRolls.NextDouble(_config.MasterSeed,
            RollChannel.WaveName, 0, id, 99) < 0.35 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(EpochRolls.NextDouble(
                _config.MasterSeed, RollChannel.WaveName, 0, id, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }

    private static long YearAt(int step) =>
        (long)Math.Round((-CosmicSim.SpanGyr + step * EvolutionSim.GyrPerStep) * 1e9);

    private void Chronicle(WorldEventType type, long year, HexCoordinate location,
        double magnitude, double valence, EventPayload payload)
    {
        _skeleton.DeepTimeEvents.Add(new WorldEvent(
            _skeleton.DeepTimeEvents.Count, year, ClockStratum.Evolutionary, type,
            Array.Empty<int>(), location, magnitude, valence,
            EventVisibility.Public, payload));
    }
}
