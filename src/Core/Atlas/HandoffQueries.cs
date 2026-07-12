using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One Open Threads row: the REPL `threads` line (Kind + Text at
/// HandoffView parity) plus the camera-jump hex for its subject — the
/// atlas's opening screen is a list of reasons to fly somewhere.</summary>
public sealed record ThreadRow(string Kind, string Text, HexCoordinate? JumpHex);

/// <summary>K3: the handoff surface as panel rows. Text and ordering are
/// HandoffView.OpenThreads verbatim (one derivation, zero drift); this
/// query only adds WHERE each thread lives, from the subject ids the
/// threads carry.</summary>
public static class HandoffQueries
{
    /// <summary>Every open thread with its jump target, HandoffView order.
    /// Eye is the K-slice seam: god-equivalent truth for now.</summary>
    public static List<ThreadRow> ThreadRows(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var rows = new List<ThreadRow>();
        foreach (var t in HandoffView.OpenThreads(state))
            rows.Add(new ThreadRow(t.Kind, t.Text, JumpHexOf(state, t)));
        return rows;
    }

    /// <summary>Where the thread's subject lives — the camera target.</summary>
    private static HexCoordinate? JumpHexOf(SimState state, OpenThread t)
    {
        switch (t.Kind)
        {
            case "war":
                return WarFront(state, state.Wars[t.SubjectId]);
            case "tension":
            case "offer":
                return state.Actors[t.SubjectId].Seat;
            case "succession":
                // old throne → the polity itself; claimed throne → the
                // polity whose throne is claimed
                return state.Actors[t.SubjectId2 >= 0
                    ? t.SubjectId2 : t.SubjectId].Seat;
            case "corporation":
                return state.Actors[t.SubjectId2].Seat;   // the host polity
            case "plague":
            {
                var plague = state.Plagues[t.SubjectId];
                if (plague.OriginPortId >= 0
                    && plague.OriginPortId < state.Ports.Count)
                    return state.Ports[plague.OriginPortId].Hex;
                foreach (var portId in plague.InfectedSince.Keys)
                    return state.Ports[portId].Hex;       // port-id order
                return null;
            }
            case "quarantine":
                return state.Ports[state.Lanes[t.SubjectId].PortAId].Hex;
            default:
                return null;
        }
    }

    /// <summary>A war's most watchable place: the first contested port
    /// siege, else the first contested lane cut, else the attacker's seat.</summary>
    private static HexCoordinate WarFront(SimState state, War war)
    {
        foreach (var o in war.Objectives)                 // id order (P6)
            if (o.Type == WarObjectiveType.CapturePort
                && o.Status == ObjectiveStatus.Contested)
                return state.Ports[o.TargetId].Hex;
        foreach (var o in war.Objectives)
            if (o.Type == WarObjectiveType.BlockadeLane
                && o.Status == ObjectiveStatus.Contested)
                return state.Ports[state.Lanes[o.TargetId].PortAId].Hex;
        return state.Actors[war.AttackerId].Seat;
    }
}
