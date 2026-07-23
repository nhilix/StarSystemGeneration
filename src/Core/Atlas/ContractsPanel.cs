using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One courier contract row (AC2.5, `econtracts` parity): route
/// (ports + owner names), cargo, fee, priority (WAR distinctly flagged),
/// poster, and fulfiller once accepted. Reuses <see cref="CargoLine"/> —
/// the same qty/grade-per-good shape a Shipment's cargo already carries
/// (a courier's escrowed basket IS the same Qty/Grade pair).</summary>
public sealed record ContractRow(
    int Id, CourierPriority Priority, CourierStatus Status,
    int OriginPortId, string OriginPortOwnerName,
    int DestPortId, string DestPortOwnerName,
    IReadOnlyList<CargoLine> Cargo, double FeeEscrow,
    int PosterActorId, string PosterName,
    int FulfillerActorId, string? FulfillerName);

/// <summary>K3-pattern panel query (AC2.5): the courier job board, Core
/// side — everything `econtracts` (Repl.RenderContracts) prints, typed.
/// SimState.Couriers holds ONLY Open/InTransit contracts (CourierOps
/// retires Delivered/Lost/Expired the moment they resolve — Resolve and
/// ExpireOpen both Remove), so every row here is live by construction;
/// no status filter needed. Registry order (P6, creation order) — the
/// SAME order the REPL walks.</summary>
public static class ContractsPanel
{
    /// <summary>Every open/in-transit contract, optionally narrowed to one
    /// poster — `econtracts [actorId]` parity (posterFilter &lt; 0 = all).</summary>
    public static List<ContractRow> Rows(AtlasReadModel model, EyeContext eye,
                                         int posterFilter = -1)
    {
        var state = model.State;
        var rows = new List<ContractRow>(state.Couriers.Count);
        foreach (var c in state.Couriers)                 // registry order (P6)
        {
            if (posterFilter >= 0 && c.PosterActorId != posterFilter) continue;
            rows.Add(RowOf(state, c));
        }
        return rows;
    }

    private static ContractRow RowOf(SimState state, CourierContract c)
    {
        var cargo = new List<CargoLine>();
        for (int g = 0; g < c.Qty.Length; g++)
            if (c.Qty[g] > 0)
                cargo.Add(new CargoLine((GoodId)g, Goods.Get((GoodId)g).Name,
                                        c.Qty[g], c.Grade[g]));
        // Open never has a fulfiller yet; InTransit always does (Accept
        // sets FulfillerActorId before the status flips) — the ternary
        // mirrors the REPL's own Open/else split.
        bool assigned = c.Status != CourierStatus.Open;
        return new ContractRow(c.Id, c.Priority, c.Status,
            c.OriginPortId, PortOwnerName(state, c.OriginPortId),
            c.DestPortId, PortOwnerName(state, c.DestPortId),
            cargo, c.FeeEscrow,
            c.PosterActorId, OwnerName(state, c.PosterActorId),
            assigned ? c.FulfillerActorId : -1,
            assigned ? OwnerName(state, c.FulfillerActorId) : null);
    }

    /// <summary>A port's identity — no Port.Name field exists, so (as
    /// elsewhere in Core.Atlas, e.g. HexQuery/SystemQuery's PortOwnerName)
    /// a port reads by its owner's name.</summary>
    private static string PortOwnerName(SimState state, int portId) =>
        portId >= 0 && portId < state.Ports.Count
            ? OwnerName(state, state.Ports[portId].OwnerActorId) : "—";

    private static string OwnerName(SimState state, int actorId) =>
        actorId >= 0 && actorId < state.Actors.Count
            ? state.Actors[actorId].Name : "—";
}
