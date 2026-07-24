using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>AC2.6: the freight-purpose derivation — ported ONCE from
/// `efreight` (Repl.RenderFreight) so the map, ShipmentPanel, and the REPL
/// all read the same rule. A rider courier contract's priority wins (War
/// → war convoy, else courier); with no rider, the shipment's own channel
/// decides (Freight → a trader's own spread run, Requisition → the state
/// hauling its own goods). Purpose is DERIVED, never stored.</summary>
public class FreightPurposeQueryTests
{
    private static SimState WithPorts()
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
        return state;
    }

    private static Shipment Ship(SimState state, int id,
                                 ShipmentChannel channel) =>
        new(id, state.Actors[0].Id, channel, originPortId: 0, destPortId: 1,
            departureYear: (int)state.WorldYear,
            routeLaneIds: System.Array.Empty<int>(), legYears: new[] { 5.0 });

    [Fact]
    public void ARiderWithWarPriorityReadsAsAWarConvoy()
    {
        var state = WithPorts();
        var s = Ship(state, 0, ShipmentChannel.Requisition);
        state.Shipments.Add(s);
        var c = new CourierContract(7, state.Actors[0].Id, 0, 1, 10,
            CourierPriority.War, (int)state.WorldYear,
            (int)state.WorldYear + 5)
        { Status = CourierStatus.InTransit, ShipmentId = s.Id };
        state.Couriers.Add(c);

        var info = FreightPurposeQuery.Of(state, s);
        Assert.Equal(FreightPurpose.WarConvoy, info.Purpose);
        Assert.Equal(7, info.RiderContractId);
    }

    [Fact]
    public void ARiderWithNormalPriorityReadsAsACourier()
    {
        var state = WithPorts();
        var s = Ship(state, 0, ShipmentChannel.Requisition);
        state.Shipments.Add(s);
        var c = new CourierContract(3, state.Actors[0].Id, 0, 1, 10,
            CourierPriority.Normal, (int)state.WorldYear,
            (int)state.WorldYear + 5)
        { Status = CourierStatus.InTransit, ShipmentId = s.Id };
        state.Couriers.Add(c);

        var info = FreightPurposeQuery.Of(state, s);
        Assert.Equal(FreightPurpose.Courier, info.Purpose);
        Assert.Equal(3, info.RiderContractId);
    }

    [Fact]
    public void NoRiderAndFreightChannelReadsAsASpreadRun()
    {
        var state = WithPorts();
        var s = Ship(state, 0, ShipmentChannel.Freight);
        state.Shipments.Add(s);

        var info = FreightPurposeQuery.Of(state, s);
        Assert.Equal(FreightPurpose.SpreadRun, info.Purpose);
        Assert.Null(info.RiderContractId);
    }

    [Fact]
    public void NoRiderAndRequisitionChannelReadsAsAStateHaul()
    {
        var state = WithPorts();
        var s = Ship(state, 0, ShipmentChannel.Requisition);
        state.Shipments.Add(s);

        var info = FreightPurposeQuery.Of(state, s);
        Assert.Equal(FreightPurpose.StateHaul, info.Purpose);
        Assert.Null(info.RiderContractId);
    }

    [Fact]
    public void AnOpenContractOnTheSamePortsIsNotARider()
    {
        // OfShipment matches only an InTransit contract carrying THIS
        // shipment's id — an Open contract (never dispatched) never
        // rides one, so the shipment still reads by its own channel.
        var state = WithPorts();
        var s = Ship(state, 0, ShipmentChannel.Requisition);
        state.Shipments.Add(s);
        var c = new CourierContract(9, state.Actors[0].Id, 0, 1, 10,
            CourierPriority.War, (int)state.WorldYear,
            (int)state.WorldYear + 5);
        state.Couriers.Add(c);   // Status stays Open, ShipmentId stays -1

        var info = FreightPurposeQuery.Of(state, s);
        Assert.Equal(FreightPurpose.StateHaul, info.Purpose);
        Assert.Null(info.RiderContractId);
    }
}
