namespace StarGen.Core.Atlas;

using StarGen.Core.Epoch;

/// <summary>The 4-way freight purpose (AC2.6). This is the EXACT
/// `efreight` derivation (Repl.RenderFreight), ported ONCE so the atlas
/// map (WorksLens), the ShipmentPanel, and the REPL all read the same
/// rule: if a courier contract rides the shipment (CourierOps.OfShipment
/// — an InTransit contract carrying this shipment's id), its priority
/// decides (War → a war convoy, else an ordinary courier job); with no
/// rider, the shipment's own channel decides (Freight → a trader's own
/// spread run, Requisition → the state hauling its own goods off a
/// courier board). Purpose is DERIVED, never a stored field — do not
/// cache it on Shipment.</summary>
public enum FreightPurpose { WarConvoy, Courier, SpreadRun, StateHaul }

/// <summary>The purpose plus the rider contract id, if any — the map/panel
/// link target back to the courier board (AC2.5's ContractsPanel).</summary>
public readonly record struct FreightPurposeInfo(
    FreightPurpose Purpose, int? RiderContractId);

public static class FreightPurposeQuery
{
    public static FreightPurposeInfo Of(SimState state, Shipment shipment)
    {
        var rider = CourierOps.OfShipment(state, shipment.Id);
        return new FreightPurposeInfo(
            FromParts(shipment.Channel, rider != null,
                      rider?.Priority ?? CourierPriority.Normal),
            rider?.Id);
    }

    /// <summary>The same rule from parts already in hand — the launch
    /// observer's capture path (AC2.F2), where the rider is known
    /// directly at dispatch and the registry lookup above would miss
    /// sub-step couriers entirely (already resolved when asked).</summary>
    public static FreightPurpose FromParts(ShipmentChannel channel,
        bool hasRider, CourierPriority riderPriority) => hasRider
            ? (riderPriority == CourierPriority.War
                ? FreightPurpose.WarConvoy : FreightPurpose.Courier)
            : (channel == ShipmentChannel.Freight
                ? FreightPurpose.SpreadRun : FreightPurpose.StateHaul);
}
