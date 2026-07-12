using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the hover-hex tooltip's content — what's here: system
/// summary (the hex tier, pure and deterministic, never persisted),
/// domain owners (service radii via DomainLens), port tier, live POI.</summary>
public class HexQueryTests
{
    private static (AtlasReadModel Model, SimState State, HexCoordinate Hex)
        Base()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { hex = HexGrid.CellCenter(cell.Coord); break; }
        return (new AtlasReadModel(state), state, hex);
    }

    [Fact]
    public void APortHexReadsOwnerAndTier()
    {
        var (model, state, hex) = Base();
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex, tier: 3,
                                 foundedYear: 5));
        var info = HexQuery.At(model, EyeContext.God(state.WorldYear), hex);
        Assert.Equal(0, info.PortId);
        Assert.Equal(3, info.PortTier);
        Assert.Equal(state.Actors[0].Name, info.PortOwnerName);
        Assert.Contains(state.Actors[0].Id, info.OwnerActorIds);
    }

    [Fact]
    public void TheWildsReadUnowned()
    {
        var (model, state, hex) = Base();
        var info = HexQuery.At(model, EyeContext.God(state.WorldYear), hex);
        Assert.Empty(info.OwnerActorIds);
        Assert.Equal(-1, info.PortId);
        Assert.Null(info.PortOwnerName);
    }

    [Fact]
    public void OnlyLivePoisSurface()
    {
        var (model, state, hex) = Base();
        state.Pois.Add(new PoiRecord(0, PoiType.Ruins, hex, 2.0, 10));
        state.Pois.Add(new PoiRecord(1, PoiType.Battlefield, hex, 4.0, 20)
        { Depleted = true });
        var info = HexQuery.At(model, EyeContext.God(state.WorldYear), hex);
        var poi = Assert.Single(info.LivePois);
        Assert.Equal(0, poi.Id);
    }

    [Fact]
    public void TheSystemSummaryIsDeterministic_AndHexTier()
    {
        var (model, state, hex) = Base();
        var eye = EyeContext.God(state.WorldYear);
        var once = HexQuery.At(model, eye, hex).SystemSummary;
        var twice = HexQuery.At(model, eye, hex).SystemSummary;
        Assert.Equal(once, twice);
        Assert.False(string.IsNullOrEmpty(once));
    }
}
