using System.Text;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>The `fleet` and `designs` REPL dumps: the fleet registry
/// (composition, posture, supply) and one fleet's full sheet-and-vectors
/// readout — the two-layer stat model made inspectable (P1: every posture
/// is a player job; the design sheet is the ship the player flies).</summary>
public static class FleetView
{
    public static string RenderAll(SimState state)
    {
        if (state.Fleets.Count == 0) return "no fleets yet";
        var sb = new StringBuilder();
        sb.AppendLine("  id  owner            posture     station                hulls  ready");
        foreach (var f in state.Fleets)
        {
            if (f.TotalHulls == 0) continue;              // idle registry slots
            sb.AppendLine(Invariant($"  #{f.Id,-3} {Owner(state, f),-16} ")
                + Invariant($"{f.Posture,-11} {Station(state, f),-22} ")
                + Invariant($"{f.TotalHulls,5}  {f.Readiness:0.00}"));
        }
        int empty = 0;
        foreach (var f in state.Fleets) if (f.TotalHulls == 0) empty++;
        if (empty > 0)
            sb.AppendLine(Invariant($"  (+{empty} hull-less registry entries)"));
        sb.Append("wrecks: ").Append(state.Wreckage.Count)
          .AppendLine(" sites — `fleet <id>` for composition and vectors");
        return sb.ToString();
    }

    public static string RenderOne(SimState state, int fleetId)
    {
        if (fleetId < 0 || fleetId >= state.Fleets.Count)
            return $"no fleet #{fleetId} (0..{state.Fleets.Count - 1})";
        var f = state.Fleets[fleetId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"fleet #{f.Id} — {Owner(state, f)}, ")
            + Invariant($"{f.Posture} at {Station(state, f)}, ")
            + Invariant($"hex ({f.Hex.Q},{f.Hex.R})"));
        sb.AppendLine(Invariant($"  readiness {f.Readiness:0.00} · home port ")
            + (f.HomePortId >= 0 ? Invariant($"#{f.HomePortId}") : "none")
            + " · commander "
            + (f.CommanderId >= 0 ? Invariant($"#{f.CommanderId}") : "(vacant slot)"));
        // forward depot (AC2.7): deployed (Blockade/Expedition) fleets
        // victual at their nearest owned port, not home — FleetOps.
        // SupplyFleets' own criterion (contract-economy spec §4)
        if (f.Posture is FleetPosture.Blockade or FleetPosture.Expedition)
        {
            int depot = FleetOps.NearestOwnedPortId(state, f.OwnerActorId, f.Hex);
            sb.AppendLine("  forward depot: " + (depot >= 0
                ? Invariant($"port #{depot} ")
                  + Invariant($"({HexGrid.Distance(state.Ports[depot].Hex, f.Hex)} hexes)")
                : "none (no owned port)"));
        }

        if (f.Hulls.Count == 0)
        {
            sb.AppendLine("  (no hulls)");
            return sb.ToString();
        }
        sb.AppendLine("  composition:");
        foreach (var g in f.Hulls)
        {
            var d = state.Designs[g.DesignId];
            sb.AppendLine(Invariant($"    {g.Count}x {d.Name} Mk {d.Mark} ")
                + Invariant($"({d.Role}/{d.Size}, design #{d.Id}, ")
                + Invariant($"grade {g.Grade:0.00})"));
        }
        var v = FleetOps.Vectors(state, f);
        sb.AppendLine("  vectors (computed, never stored):");
        sb.AppendLine(Invariant($"    strike {v.Strike:0.0} · sustained {v.Sustained:0.0} ")
            + Invariant($"· screening {v.Screening:0.0} · tracking {v.Tracking:0.0}"));
        sb.AppendLine(Invariant($"    detection {v.Detection:0.0} · stealth {v.Stealth:0.00} ")
            + Invariant($"· capacity {v.Capacity:0.0} · endurance floor ")
            + Invariant($"{v.EnduranceFloor:0.0} ")
            + Invariant($"(~{(int)(v.EnduranceFloor * state.Config.Fleet.EnduranceHexesPerPoint)} hexes off-lane) ")
            + Invariant($"· upkeep {v.Upkeep:0.0}"));
        return sb.ToString();
    }

    public static string RenderDesigns(SimState state, int actorFilter = -1)
    {
        if (state.Designs.Count == 0) return "no designs yet";
        var sb = new StringBuilder();
        sb.AppendLine("  id  owner            class                 cell            grade  tech  year");
        foreach (var d in state.Designs)
        {
            if (actorFilter >= 0 && d.OwnerActorId != actorFilter) continue;
            string owner = state.Actors[d.OwnerActorId].Name;
            sb.AppendLine(Invariant($"  #{d.Id,-3} {owner,-16} ")
                + Invariant($"{d.Name + " Mk " + d.Mark,-21} ")
                + Invariant($"{d.Role + "/" + d.Size,-15} {d.ComponentGrade,5:0.00} ")
                + Invariant($"{d.TechTier,4}  y{d.DesignedYear}"));
        }
        return sb.ToString();
    }

    private static string Owner(SimState state, FleetRecord f) =>
        state.Actors[f.OwnerActorId].Name;

    /// <summary>Where the posture stands: the lane for Posted/Escort, the
    /// port for Patrol/Blockade/Reserve.</summary>
    private static string Station(SimState state, FleetRecord f)
    {
        switch (f.Posture)
        {
            case FleetPosture.Posted:
            case FleetPosture.Escort:
                if (f.TargetId >= 0 && f.TargetId < state.Lanes.Count)
                {
                    var lane = state.Lanes[f.TargetId];
                    return Invariant($"lane #{lane.Id} ({lane.PortAId}<->{lane.PortBId})");
                }
                return "unassigned";
            case FleetPosture.Patrol:
            case FleetPosture.Blockade:
                return f.TargetId >= 0 ? Invariant($"port #{f.TargetId}") : "unassigned";
            case FleetPosture.Expedition:
                return "in transit";
            default:
                return f.HomePortId >= 0
                    ? Invariant($"docked #{f.HomePortId}") : "adrift";
        }
    }
}
