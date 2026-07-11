using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The lanes lens — built highways with their live status: open,
/// quarantined (plague), severed (blockade interdiction). Endpoints are
/// port hexes; the wilds stay off-network.</summary>
public class LaneLensTests
{
    private static (AtlasReadModel Model, SimState State) WithLane()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? a = null, b = null;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            if (a == null) { a = HexGrid.CellCenter(cell.Coord); continue; }
            b = HexGrid.CellCenter(cell.Coord);
            break;
        }
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, state.Actors[0].Id, b!.Value, tier: 2, foundedYear: 0));
        state.Lanes.Add(new Lane(0, 0, 1, builtYear: 0));
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void SegmentsCarryPortEndpointsAndReadOpen()
    {
        var (model, state) = WithLane();
        var segments = LaneLens.Segments(model, EyeContext.God(state.WorldYear));
        var seg = Assert.Single(segments);
        Assert.Equal(state.Ports[0].Hex, seg.A);
        Assert.Equal(state.Ports[1].Hex, seg.B);
        Assert.Equal(LaneStatus.Open, seg.Status);
    }

    [Fact]
    public void AQuarantinedLaneReadsQuarantinedUntilItLapses()
    {
        var (model, state) = WithLane();
        state.Lanes[0].QuarantinedUntil = state.WorldYear + 10;
        var seg = Assert.Single(
            LaneLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(LaneStatus.Quarantined, seg.Status);

        // The lapse reads the STATE's clock — the eye never time travels.
        state.WorldYear += 11;
        var later = Assert.Single(
            LaneLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(LaneStatus.Open, later.Status);
    }

    [Fact]
    public void ABlockadedPortSeversItsLanes()
    {
        var (model, state) = WithLane();
        EpochTestKit.BlockadePort(state, state.Actors[1].Id, portId: 0);
        var seg = Assert.Single(
            LaneLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(LaneStatus.Severed, seg.Status);
    }
}
