using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class BodyGeneratorTests
{
    public static List<Body> SampleBodies(ulong seed, int hexes)
    {
        var bodies = new List<Body>();
        for (int x = 0; x < hexes; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            bodies.AddRange(r.System.Stars
                .SelectMany(st => st.Slots)
                .Where(sl => sl.Body != null)
                .Select(sl => sl.Body!));
        }
        return bodies;
    }

    [Fact]
    public void Bodies_AreGenerated_AndSomeSlotsAreEmpty()
    {
        var bodies = SampleBodies(9, 600);
        Assert.NotEmpty(bodies);
        int slots = 0, filled = 0;
        for (int x = 0; x < 600; x++)
        {
            var r = Generator.Generate(9, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            foreach (var sl in r.System.Stars.SelectMany(st => st.Slots))
            {
                slots++;
                if (sl.Body != null) filled++;
            }
        }
        Assert.True(filled < slots, "some slots must stay empty (derelict-fleet overlay needs them)");
    }

    [Fact]
    public void Wreckage_NeverGeneratedInBaseline() =>
        Assert.DoesNotContain(SampleBodies(9, 600), b => b.Kind == BodyKind.Wreckage);

    [Fact]
    public void GasGiants_AreDenseAtmosphere_And_Belts_AreBarrenSizeZero()
    {
        foreach (var b in SampleBodies(9, 600))
        {
            if (b.Kind == BodyKind.GasGiant) Assert.Equal(Atmosphere.Dense, b.Atmosphere);
            if (b.Kind == BodyKind.PlanetoidBelt)
            {
                Assert.Equal(0, b.Size);
                Assert.Equal(Biosphere.Barren, b.Biosphere);
            }
        }
    }

    [Fact]
    public void RichBiospheres_ClusterInHabitableBand()
    {
        int habFlourish = 0, otherFlourish = 0;
        for (int x = 0; x < 3000; x++)
        {
            var r = Generator.Generate(9, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            foreach (var sl in r.System.Stars.SelectMany(st => st.Slots))
            {
                if (sl.Body == null || sl.Body.Biosphere < Biosphere.Flourishing) continue;
                if (sl.Band == OrbitBand.Habitable) habFlourish++; else otherFlourish++;
            }
        }
        Assert.True(habFlourish > otherFlourish,
            $"flourishing+ biospheres should cluster in habitable band (hab {habFlourish} vs other {otherFlourish})");
    }

    [Fact]
    public void SettlementWithoutBiosphere_Occurs()
    {
        // "colony on a dead rock" must be possible (spec §5)
        Assert.Contains(SampleBodies(9, 3000),
            b => b.Settlement != Settlement.None && b.Biosphere == Biosphere.Barren);
    }
}
