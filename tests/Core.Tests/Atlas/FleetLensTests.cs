using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The fleets lens — posture-differentiated marks at fleet hexes
/// (K2: the first authored-glyph vocabulary; the lens hands the
/// presentation posture + owner tint, the sprite atlas does the rest).</summary>
public class FleetLensTests
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
        EpochTestKit.AddLane(state, 0, 1);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void APostedFleetMarksItsHexWithPostureAndOwner()
    {
        var (model, state) = WithLane();
        var fleet = EpochTestKit.PostFreight(
            state, state.Actors[0].Id, laneId: 0, hulls: 3);
        var mark = Assert.Single(
            FleetLens.Markers(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(fleet.Id, mark.FleetId);
        Assert.Equal(fleet.Hex, mark.Hex);
        Assert.Equal(FleetPosture.Posted, mark.Posture);
        Assert.Equal(state.Actors[0].Id, mark.OwnerActorId);
        Assert.Equal(3, mark.Hulls);
    }

    [Fact]
    public void AHulklessFleetLeavesNoMark()
    {
        var (model, state) = WithLane();
        state.Fleets.Add(new FleetRecord(0, state.Actors[0].Id,
                                         state.Ports[0].Hex));
        Assert.Empty(FleetLens.Markers(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void PosturesReadDistinctly()
    {
        var (model, state) = WithLane();
        EpochTestKit.PostFreight(state, state.Actors[0].Id, laneId: 0, hulls: 2);
        EpochTestKit.BlockadePort(state, state.Actors[1].Id, portId: 1);
        var marks = FleetLens.Markers(model, EyeContext.God(state.WorldYear));
        Assert.Equal(2, marks.Count);
        Assert.Equal(FleetPosture.Posted, marks[0].Posture);
        Assert.Equal(FleetPosture.Blockade, marks[1].Posture);
        // Owner tint differs — two polities never share a mark color.
        Assert.NotEqual(marks[0].Color, marks[1].Color);
    }
}
