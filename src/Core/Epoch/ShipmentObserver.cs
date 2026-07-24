using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>What the launch observer sees (AC2.F2): one dispatched
/// shipment — identity, endpoints, channel, owner, the SAILED route as
/// the ordered chain of port hexes it traverses (origin→…→dest; legs =
/// Count−1; an off-lane crawl is just the endpoint pair — captured as
/// hexes, not lane ids, so a scrubbed keyframe never depends on a lane
/// registry that may have changed later in the run), the courier contract
/// riding it (−1/Normal when none — carried explicitly because a sub-step
/// courier is already resolved before any registry lookup could find it),
/// and a snapshot of the cargo. Reported at every launch, whether the
/// shipment survives the step or delivers/dies inside it — the sub-step
/// blur is exactly what the observer exists to witness.</summary>
public readonly record struct ShipmentLaunch(
    int ShipmentId, int OwnerActorId, ShipmentChannel Channel,
    int OriginPortId, int DestPortId,
    IReadOnlyList<HexCoordinate> RouteHexes, int RiderContractId,
    CourierPriority RiderPriority, IReadOnlyList<double> Qty);

/// <summary>A PASSIVE tap on shipment launches (AC2.F2 recent flows).
/// Null — the default, every serializer load, the whole golden pipeline —
/// means strictly zero side effects: no rolls, no allocations, no
/// iteration-order change; dispatch behaves bit-identically. EpochEngine
/// threads its own tap onto <see cref="SimState.ShipmentObserver"/> for
/// the duration of a step and resets it in a finally, so a tap can never
/// leak past the step it was set for. Never serialized.</summary>
public delegate void ShipmentObserver(ShipmentLaunch launch);
