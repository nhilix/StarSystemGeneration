using System;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>The evolutionary clock: biosphere residue and the emergence
/// schedule are deterministic, causal (dates trace to viability + growth +
/// setbacks), and correctly striped into eras. The headline gate: staggered
/// polity entry has causes.</summary>
public class EvolutionSimTests
{
    private static GalaxySkeleton Built(ulong seed = 42, int radius = 8) =>
        SkeletonBuilder.Build(new GalaxyConfig
        { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void Run_IsDeterministic_AndObservationChangesNothing()
    {
        var a = Built();
        int frames = 0;
        var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var b = SkeletonBuilder.Build(config, evoObserver: _ => frames++);
        Assert.Equal(EvolutionSim.Steps, frames);
        Assert.Equal(a.Origins.Count, b.Origins.Count);
        for (int i = 0; i < a.Origins.Count; i++)
        {
            Assert.Equal(a.Origins[i].CellCoord, b.Origins[i].CellCoord);
            Assert.Equal(a.Origins[i].SpaceflightYear, b.Origins[i].SpaceflightYear);
            Assert.Equal(a.Origins[i].Era, b.Origins[i].Era);
        }
        for (int i = 0; i < a.Cells.Count; i++)
            Assert.Equal(a.Cells[i].BiosphereRichness, b.Cells[i].BiosphereRichness);
    }

    [Fact]
    public void Biosphere_LivesOnlyWhereViabilityAllowed()
    {
        var s = Built();
        int living = 0;
        foreach (var cell in s.Cells)
        {
            if (cell.BiosphereRichness <= 0) continue;
            living++;
            Assert.True(cell.LifeViableStep >= 0,
                "life requires the metallicity floor crossing");
            Assert.False(cell.IsVoid, "life needs worlds");
            Assert.True(cell.BiosphereAgeGyr > 0);
            Assert.InRange(cell.BiosphereRichness, 0.0, 1.0);
        }
        Assert.True(living > s.Cells.Count / 10,
            $"a 14 Gyr galaxy should be widely alive ({living}/{s.Cells.Count})");
        Assert.Contains(s.Cells, c => c.LifeViableStep >= 0 && c.BiosphereRichness == 0);
    }

    [Fact]
    public void EmergenceSchedule_DatesAreOrdered_AndErasStriped()
    {
        foreach (ulong seed in new ulong[] { 7, 42, 99 })
        {
            var s = Built(seed);
            Assert.NotEmpty(s.Origins);
            foreach (var o in s.Origins)
            {
                Assert.True(o.AbiogenesisYear <= o.SapienceYear,
                    "life precedes minds");
                Assert.True(o.SapienceYear < o.SpaceflightYear,
                    "minds precede flight");
                Assert.True(o.SapienceYear <= 0, "sapience registered in the run");
                var era = o.SpaceflightYear < (long)(-EvolutionSim.PrecursorGapGyr * 1e9)
                        ? OriginEra.Precursor
                    : o.SpaceflightYear <= (long)(EvolutionSim.CurrentHorizonGyr * 1e9)
                        ? OriginEra.Current
                    : OriginEra.PreSpaceflight;
                Assert.Equal(era, o.Era);
                Assert.Equal(o.CellCoord, HexGrid.CellOf(o.Hex));
            }
            Assert.True(s.Origins.Count(o => o.Era == OriginEra.Current) >= 2,
                $"seed {seed}: at least two current-era polities");
            Assert.NotEmpty(s.Origins.Where(o => o.Era == OriginEra.Precursor));
        }
    }

    [Fact]
    public void Staggering_IsCausal_PrecursorsStartedEarlier()
    {
        var s = Built(42, 12);
        double precursorAbio = s.Origins.Where(o => o.Era == OriginEra.Precursor)
            .Average(o => (double)o.AbiogenesisYear);
        double currentAbio = s.Origins.Where(o => o.Era == OriginEra.Current)
            .Average(o => (double)o.AbiogenesisYear);
        Assert.True(precursorAbio < currentAbio,
            $"precursors abiogenesis ({precursorAbio / 1e9:F2} Gyr) should "
            + $"predate current-era ({currentAbio / 1e9:F2} Gyr)");
    }

    [Fact]
    public void CurrentEraDates_SpreadAcrossTheHorizon()
    {
        var s = Built(42, 12);
        var dates = s.Origins.Where(o => o.Era == OriginEra.Current)
            .Select(o => o.SpaceflightYear).OrderBy(y => y).ToList();
        Assert.True(dates.Count >= 2);
        Assert.True(dates.Last() - dates.First() > 50_000_000,
            "entry staggering needs real spread, not a simultaneous dawn");
    }

    [Fact]
    public void Chronicle_CarriesTheEvolutionaryStratum_InTimeOrder()
    {
        var s = Built();
        var evo = s.DeepTimeEvents
            .Where(e => e.Stratum == ClockStratum.Evolutionary).ToList();
        Assert.Single(evo.Where(e => e.Type == WorldEventType.FirstLife));
        Assert.Equal(s.Origins.Count,
            evo.Count(e => e.Type == WorldEventType.SapienceEmerged));
        Assert.Equal(s.Origins.Count(o => o.Era == OriginEra.Precursor),
            evo.Count(e => e.Type == WorldEventType.SpaceflightReached));

        long previous = long.MinValue;
        for (int i = 0; i < s.DeepTimeEvents.Count; i++)
        {
            var e = s.DeepTimeEvents[i];
            Assert.Equal(i, e.Id);
            Assert.True(e.WorldYear >= previous,
                "the one deep chronicle reads in time order across strata");
            previous = e.WorldYear;
        }
    }

    [Fact]
    public void SapienceRateZero_MeansAnEmptySchedule()
    {
        var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        config.Evolution.SapienceRate = 0.0;
        var s = SkeletonBuilder.Build(config);
        Assert.Empty(s.Origins);
        Assert.Contains(s.Cells, c => c.BiosphereRichness > 0);   // life still happens
    }
}
