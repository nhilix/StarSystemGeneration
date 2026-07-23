using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One war-registry row (`wars` parity).</summary>
public sealed record WarRow(int Id, string Name, int AttackerId,
    string AttackerName, int DefenderId, string DefenderName, bool Active,
    long EndedYear, CasusBelli Cause, int ObjectivesTaken,
    int ObjectivesTotal);

/// <summary>One side of a war card: leader, allies, exhaustion, and the
/// current strength as a fraction of what was mustered at declaration
/// (null when nothing was mustered — InterpolityView hides the line).</summary>
public sealed record WarSide(int LeaderId, string LeaderName,
    IReadOnlyList<int> AllyIds, double Exhaustion,
    double? StrengthOfMustered);

/// <summary>One front (`war &lt;id&gt;` parity): the falls-at siege
/// threshold rides contested port sieges only.</summary>
public sealed record FrontRow(WarObjectiveType Type, int TargetId,
    ObjectiveStatus Status, int SiegeYears, int? FallsAtYears);

/// <summary>A war fleet on station under the attacker's banner.
/// DepotPortId/-DistanceHexes name its forward depot (AC2.7,
/// FleetOps.NearestOwnedPortId) — every station fleet here is already
/// Blockade/Expedition (deployed), so DepotPortId is only -1 when the
/// attacker holds no port at all.</summary>
public sealed record WarFleetRow(int FleetId, int Hulls, HexCoordinate Hex,
    string? CommanderName, double Readiness, int DepotPortId,
    int DepotDistanceHexes);

/// <summary>The war card — one campaign readable like a story.</summary>
public sealed record WarCard(int Id, string Name, bool Active,
    long EndedYear, long DeclaredYear, CasusBelli Cause, WarDemand Demand,
    WarSide Attacker, WarSide Defender, IReadOnlyList<FrontRow> Objectives,
    IReadOnlyList<WarFleetRow> FleetsOnStation,
    IReadOnlyList<string> Chronicle);

/// <summary>K3: the front click / war-lens target — InterpolityView
/// RenderWars/RenderWar parity (side strength via WarOps.SideStrength,
/// falls-at via WarConduct.SiegeThreshold, the four war-chronicle payload
/// types described by SimTraceView).</summary>
public static class WarPanel
{
    public static List<WarRow> Rows(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var rows = new List<WarRow>();
        foreach (var war in state.Wars)                   // id order (P6)
        {
            int taken = 0;
            foreach (var o in war.Objectives)
                if (o.Status == ObjectiveStatus.Taken) taken++;
            rows.Add(new WarRow(war.Id, war.Name, war.AttackerId,
                state.Actors[war.AttackerId].Name, war.DefenderId,
                state.Actors[war.DefenderId].Name, war.Active,
                war.EndedYear, war.Cause, taken, war.Objectives.Count));
        }
        return rows;
    }

    public static WarCard? Card(AtlasReadModel model, EyeContext eye,
                                int warId)
    {
        var state = model.State;
        if (warId < 0 || warId >= state.Wars.Count) return null;
        var war = state.Wars[warId];

        var objectives = new List<FrontRow>();
        foreach (var o in war.Objectives)
        {
            int? fallsAt = o.Type == WarObjectiveType.CapturePort
                && o.Status == ObjectiveStatus.Contested
                ? WarConduct.SiegeThreshold(state, war, state.Ports[o.TargetId])
                : null;
            objectives.Add(new FrontRow(o.Type, o.TargetId, o.Status,
                                        o.SiegeYears, fallsAt));
        }

        // war fleets on station under the attacker (InterpolityView parity)
        var fleets = new List<WarFleetRow>();
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != war.AttackerId || fleet.TotalHulls == 0
                || fleet.Posture is not (FleetPosture.Blockade
                    or FleetPosture.Expedition)) continue;
            int depotPortId = FleetOps.NearestOwnedPortId(state,
                fleet.OwnerActorId, fleet.Hex);
            int depotDistance = depotPortId >= 0
                ? HexGrid.Distance(state.Ports[depotPortId].Hex, fleet.Hex)
                : -1;
            fleets.Add(new WarFleetRow(fleet.Id, fleet.TotalHulls, fleet.Hex,
                fleet.CommanderId >= 0
                    ? state.Characters[fleet.CommanderId].Name : null,
                fleet.Readiness, depotPortId, depotDistance));
        }

        var chronicle = new List<string>();
        foreach (var e in state.Log.Events)
            if ((e.Payload is BattleFoughtPayload b && b.WarId == war.Id)
                || (e.Payload is SiegeBegunPayload sg && sg.WarId == war.Id)
                || (e.Payload is PortCapturedPayload pc && pc.WarId == war.Id)
                || (e.Payload is PeaceSettledPayload ps && ps.WarId == war.Id))
                chronicle.Add(SimTraceView.Describe(e));

        return new WarCard(war.Id, war.Name, war.Active, war.EndedYear,
            war.DeclaredYear, war.Cause, war.Demand,
            SideOf(state, war, attacker: true),
            SideOf(state, war, attacker: false), objectives, fleets,
            chronicle);
    }

    private static WarSide SideOf(SimState state, War war, bool attacker)
    {
        int leader = attacker ? war.AttackerId : war.DefenderId;
        double atStart = attacker
            ? war.AttackerStrengthAtStart : war.DefenderStrengthAtStart;
        return new WarSide(leader, state.Actors[leader].Name,
            attacker ? war.AttackerAllies : war.DefenderAllies,
            attacker ? war.AttackerExhaustion : war.DefenderExhaustion,
            atStart > 0
                ? WarOps.SideStrength(state, war, attacker) / atStart
                : null);
    }
}
