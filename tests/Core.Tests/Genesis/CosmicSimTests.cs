using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>The cosmic clock's mechanics gates: the field stack is conserved
/// (P4 mass/metals ledger), the run is deterministic, observation never
/// changes it, and structure — arms, core, voids, aged cohorts, enrichment —
/// is residue of the loop, not paint.</summary>
public class CosmicSimTests
{
    private static GalaxySkeleton Skeleton(ulong seed = 42, int radius = 8) =>
        new(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void MassLedger_InflowEqualsTheStack()
    {
        var s = CosmicSim.Run(Skeleton());
        double stack = Enumerable.Range(0, s.CellCount).Sum(s.TotalMass);
        Assert.True(s.InflowTotal > 0, "primordial inflow should land");
        Assert.Equal(s.InflowTotal + s.InjectedTotal, stack, 6);
    }

    [Fact]
    public void MetalsLedger_CreatedEqualsThePools()
    {
        var s = CosmicSim.Run(Skeleton());
        double pools = Enumerable.Range(0, s.CellCount)
            .Sum(i => s.MetalsIsm[i] + s.StarMetals[i] + s.RemnantMetals[i]);
        Assert.True(s.MetalsCreatedTotal > 0, "supernovae should enrich");
        Assert.Equal(s.MetalsCreatedTotal + s.MetalsInjectedTotal, pools, 6);
    }

    [Fact]
    public void Run_IsDeterministic()
    {
        var a = CosmicSim.Run(Skeleton());
        var b = CosmicSim.Run(Skeleton());
        for (int i = 0; i < a.CellCount; i++)
        {
            Assert.Equal(a.Gas[i], b.Gas[i]);
            Assert.Equal(a.StarsYoung[i], b.StarsYoung[i]);
            Assert.Equal(a.StarsMid[i], b.StarsMid[i]);
            Assert.Equal(a.StarsOld[i], b.StarsOld[i]);
            Assert.Equal(a.Remnants[i], b.Remnants[i]);
            Assert.Equal(a.MetalsIsm[i], b.MetalsIsm[i]);
            Assert.Equal(a.StarMetals[i], b.StarMetals[i]);
            Assert.Equal(a.LifeViableStep[i], b.LifeViableStep[i]);
        }
    }

    [Fact]
    public void Observation_NeverChangesTheRun()
    {
        int frames = 0;
        var watched = CosmicSim.Run(Skeleton(), frame => frames++);
        var unwatched = CosmicSim.Run(Skeleton());
        Assert.Equal(CosmicSim.Steps, frames);
        for (int i = 0; i < watched.CellCount; i++)
            Assert.Equal(unwatched.TotalMass(i), watched.TotalMass(i));
    }

    [Fact]
    public void MatterCollectsWhereThePotentialIsDeep()
    {
        var s = CosmicSim.Run(Skeleton());
        var byPotential = Enumerable.Range(0, s.CellCount)
            .OrderByDescending(i => s.Potential[i]).ToList();
        int quarter = s.CellCount / 4;
        double top = byPotential.Take(quarter).Average(s.TotalMass);
        double bottom = byPotential.Skip(3 * quarter).Average(s.TotalMass);
        Assert.True(top > bottom * 3,
            $"deep-potential cells ({top:F3}) should hold several times the "
            + $"mass of shallow ones ({bottom:F3})");
    }

    [Fact]
    public void VoidsEmerge_WherePotentialNeverGatheredGas()
    {
        var s = CosmicSim.Run(Skeleton());
        double meanMass = Enumerable.Range(0, s.CellCount).Average(s.TotalMass);
        int nearEmpty = Enumerable.Range(0, s.CellCount)
            .Count(i => s.TotalMass(i) < meanMass * 0.1);
        Assert.True(nearEmpty > s.CellCount / 20,
            $"some cells should stay near-empty ({nearEmpty} of {s.CellCount})");
    }

    [Fact]
    public void CohortsAge_AndTheGraveyardFills()
    {
        var s = CosmicSim.Run(Skeleton());
        double young = s.StarsYoung.Sum(), mid = s.StarsMid.Sum(),
               old = s.StarsOld.Sum(), remnants = s.Remnants.Sum();
        Assert.True(young > 0, "star formation should still be alive somewhere");
        Assert.True(mid > 0 && old > 0, "cohorts should have aged");
        Assert.True(remnants > 0, "the graveyard should have residents");
        Assert.True(old + mid > young,
            "after 14 Gyr most stellar mass should be past its youth");
    }

    [Fact]
    public void GasBurns_ButNeverCompletely()
    {
        var s = CosmicSim.Run(Skeleton());
        double gas = s.Gas.Sum();
        double total = Enumerable.Range(0, s.CellCount).Sum(s.TotalMass);
        Assert.InRange(gas / total, 0.01, 0.6);
    }

    [Fact]
    public void EnrichmentHistory_MakesLifeViableSomewhere_NotEverywhere()
    {
        var s = CosmicSim.Run(Skeleton());
        int viable = s.LifeViableStep.Count(v => v >= 0);
        Assert.True(viable > 0, "life should become viable somewhere");
        Assert.True(viable < s.CellCount,
            "the rim and voids should include never-viable cells");
        // crossings should be spread over the history, not all at one step
        var steps = s.LifeViableStep.Where(v => v >= 0).Distinct().Count();
        Assert.True(steps > 5, $"viability crossings should stagger ({steps} distinct steps)");
    }

    [Fact]
    public void StarFormationEfficiency_BurnsGasEarlier()
    {
        var eager = Skeleton();
        eager.Config.Cosmic.StarFormationEfficiency = 2.0;
        var lazy = Skeleton();
        lazy.Config.Cosmic.StarFormationEfficiency = 0.5;
        double eagerGas = CosmicSim.Run(eager).Gas.Sum();
        double lazyGas = CosmicSim.Run(lazy).Gas.Sum();
        Assert.True(eagerGas < lazyGas,
            $"higher SF efficiency should leave less present-day gas "
            + $"({eagerGas:F2} vs {lazyGas:F2})");
    }

    [Fact]
    public void EnrichmentRate_WidensTheViableGalaxy()
    {
        var rich = Skeleton();
        rich.Config.Cosmic.EnrichmentRate = 2.0;
        var poor = Skeleton();
        poor.Config.Cosmic.EnrichmentRate = 0.4;
        int richViable = CosmicSim.Run(rich).LifeViableStep.Count(v => v >= 0);
        int poorViable = CosmicSim.Run(poor).LifeViableStep.Count(v => v >= 0);
        Assert.True(richViable > poorViable,
            $"faster enrichment should widen viability ({richViable} vs {poorViable})");
    }
}
