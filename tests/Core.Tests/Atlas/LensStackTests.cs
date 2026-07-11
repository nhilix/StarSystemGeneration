using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>Lens compositing and the port markers — the last Core-side
/// derivations the K1 map surface consumes.</summary>
public class LensStackTests
{
    [Fact]
    public void CompositeAlphaBlendsOverlaysOntoTheBase()
    {
        var under = new[] { new Rgba(100, 100, 100, 255) };
        var over = new[] { new Rgba(200, 0, 0, 128) };
        var outp = LensStack.Composite(under, over);
        var c = Assert.Single(outp);
        Assert.Equal(255, c.A);
        Assert.True(c.R > 100 && c.R < 200, $"got {c.R}");
        Assert.True(c.G < 100);
    }

    [Fact]
    public void ATransparentOverlayLeavesTheBaseUntouched()
    {
        var under = new[] { new Rgba(37, 41, 53, 255) };
        var over = new[] { AtlasPalette.Clear };
        var c = Assert.Single(LensStack.Composite(under, over));
        Assert.Equal(under[0], c);
    }

    [Fact]
    public void PortMarkersCarryHexTierOwnerColorAndServiceRadius()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = new HexCoordinate(3, 2);
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex, tier: 3, foundedYear: 0));
        var model = new AtlasReadModel(state);
        var markers = PortLens.Markers(model, EyeContext.God(state.WorldYear));
        var m = Assert.Single(markers);
        Assert.Equal(hex, m.Hex);
        Assert.Equal(3, m.Tier);
        Assert.Equal(state.Actors[0].Id, m.OwnerActorId);
        Assert.Equal(PortDomains.ServiceRadius(state.Config, 3), m.ServiceRadiusHexes);
        var own = AtlasPalette.OwnerColor(m.OwnerActorId);
        Assert.True(m.Color.R >= own.R && m.Color.G >= own.G && m.Color.B >= own.B,
            "marker reads as a brightened owner color");
    }
}
