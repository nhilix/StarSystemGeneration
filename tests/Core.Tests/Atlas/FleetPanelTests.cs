using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Fleet panel — `fleet`/`fleet &lt;id&gt;`/`designs`
/// parity (FleetView): the registry skips hull-less slots, the station
/// derives from posture, the vectors ARE FleetOps.Vectors.</summary>
public class FleetPanelTests
{
    private static (AtlasReadModel Model, SimState State) WithFleet()
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
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, 2, 0));
        state.Ports.Add(new Port(1, state.Actors[1].Id, b!.Value, 2, 0));
        EpochTestKit.AddLane(state, 0, 1);
        EpochTestKit.PostFreight(state, state.Actors[0].Id, laneId: 0,
                                 hulls: 4);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void TheRegistrySkipsHullLessSlots()
    {
        var (model, state) = WithFleet();
        state.Fleets.Add(new FleetRecord(state.Fleets.Count,
            state.Actors[0].Id, state.Ports[0].Hex));   // idle slot
        var rows = FleetPanel.Rows(model, EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows);
        Assert.Equal(0, row.Id);
        Assert.Equal(4, row.TotalHulls);
        Assert.Equal(state.Actors[0].Name, row.OwnerName);
    }

    [Fact]
    public void ThePostedStationIsItsLane()
    {
        var (model, state) = WithFleet();
        var row = Assert.Single(
            FleetPanel.Rows(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(FleetPosture.Posted, row.Posture);
        Assert.Equal(StationKind.Lane, row.Station);
        Assert.Equal(0, row.StationId);
    }

    [Fact]
    public void TheCardsVectorsAreFleetOpsVectors()
    {
        var (model, state) = WithFleet();
        var card = FleetPanel.Card(model,
            EyeContext.God(state.WorldYear), 0);
        Assert.NotNull(card);
        Assert.Equal(FleetOps.Vectors(state, state.Fleets[0]),
                     card!.Vectors);
        var comp = Assert.Single(card.Composition);
        Assert.Equal(4, comp.Count);
        Assert.Equal(state.Fleets[0].Hulls[0].DesignId, comp.DesignId);
    }

    [Fact]
    public void ADeployedFleetNamesItsForwardDepot()
    {
        var (model, state) = WithFleet();
        // the freight fleet (id 0) is Posted, not deployed; blockade the
        // enemy port with actor 0's own squadron — its forward depot is
        // its own port #0, not the port it's blockading
        var fleet = EpochTestKit.BlockadePort(state, state.Actors[0].Id,
                                              portId: 1);
        var card = FleetPanel.Card(model, EyeContext.God(state.WorldYear),
                                   fleet.Id);
        Assert.NotNull(card);
        Assert.Equal(0, card!.ForwardDepotPortId);
        Assert.Equal(HexGrid.Distance(state.Ports[0].Hex, fleet.Hex),
                     card.ForwardDepotDistanceHexes);
    }

    [Fact]
    public void ANonDeployedFleetNamesNoForwardDepot()
    {
        var (model, state) = WithFleet();
        var card = FleetPanel.Card(model, EyeContext.God(state.WorldYear), 0);
        Assert.NotNull(card);
        Assert.Equal(FleetPosture.Posted, card!.Row.Posture);
        Assert.Equal(-1, card.ForwardDepotPortId);
        Assert.Equal(-1, card.ForwardDepotDistanceHexes);
    }

    [Fact]
    public void DesignsFilterByOwner()
    {
        var (model, state) = WithFleet();
        DesignRegistry.Register(state, state.Actors[1].Id,
            ShipRole.Escort, ShipSize.Light, grade: 0.4);
        var all = FleetPanel.Designs(model, EyeContext.God(state.WorldYear));
        Assert.Equal(2, all.Count);
        var mine = FleetPanel.Designs(model,
            EyeContext.God(state.WorldYear), state.Actors[1].Id);
        var d = Assert.Single(mine);
        Assert.Equal(ShipRole.Escort, d.Role);
        Assert.Equal(state.Actors[1].Name, d.OwnerName);
    }
}
