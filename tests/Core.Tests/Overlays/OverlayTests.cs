using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Overlays;

public class OverlayTests
{
    private static List<StarSystem> Sample(ulong seed, int hexes)
    {
        var systems = new List<StarSystem>();
        for (int x = 0; x < hexes; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System != null) systems.Add(r.System);
        }
        return systems;
    }

    [Fact]
    public void Overlays_AreRare_ButPresent()
    {
        var systems = Sample(41, 4000);
        int withOverlay = systems.Count(s => s.OverlayId != null);
        Assert.InRange(withOverlay / (double)systems.Count, 0.005, 0.10);
    }

    [Fact]
    public void EligibilityInvariants_Hold()
    {
        foreach (var s in Sample(41, 6000).Where(s => s.OverlayId != null))
        {
            switch (s.OverlayId)
            {
                case "unstable_star":
                    Assert.NotEqual(StarAge.Mature, s.Stars[0].Age);
                    Assert.Contains(s.Tags, t => t.Contains("instability"));
                    break;
                case "derelict_fleet":
                    Assert.Contains(s.Stars.SelectMany(st => st.Slots),
                        sl => sl.Body?.Kind == BodyKind.Wreckage);
                    break;
                case "precursor_ruins":
                    Assert.Contains(s.Stars.SelectMany(st => st.Slots)
                        .Where(sl => sl.Body != null)
                        .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!)),
                        b => b.Tags.Contains("precursor ruins"));
                    break;
                case "anomalous_signal":
                    Assert.Contains("anomalous signal", s.Tags);
                    break;
                default:
                    Assert.Fail($"unknown overlay id {s.OverlayId}");
                    break;
            }
        }
    }

    [Fact]
    public void OverlaySystems_AreAlwaysNamed()
    {
        foreach (var s in Sample(41, 6000).Where(s => s.OverlayId != null))
            Assert.NotNull(s.GivenName);
    }

    [Fact]
    public void OverlayApplication_IsDeterministic()
    {
        for (int x = 0; x < 1500; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            Assert.Equal(Generator.Generate(41, coord).System?.OverlayId,
                         Generator.Generate(41, coord).System?.OverlayId);
        }
    }
}
