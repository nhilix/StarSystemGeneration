using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Civil war (polity/factions-and-government.md §Graduation:
/// "contested → civil war (the war machinery, against a provisional
/// polity)"): when a coup is contested, the domains farthest from the
/// palace rally to the deposed ruler as a provisional polity — founded
/// through the same splinter flow schisms use — and the throne is fought
/// for through the ordinary war machinery. Submission settlements merge
/// the loser back whole.</summary>
public static class CivilWarOps
{
    /// <summary>Split the realm and declare the war. The usurper holds the
    /// capital half; the loyalists take the outer half under the deposed
    /// ruler and fight to take the palace back. No split viable (a one-port
    /// realm) → the coup stands uncontested after all.</summary>
    public static War? Erupt(SimState state, PolityRecord usurperState,
        Character deposedRuler, double[] preLurchIdeology,
        GovernmentFormId preCoupForm, double preCoupLegitimacy)
    {
        // the outer half rallies to the old flag (the palace holds the core)
        var seat = state.Actors[usurperState.ActorId].Seat;
        var owned = new List<(int Dist, int Id)>();
        foreach (var port in state.Ports)                     // id order (P6)
            if (port.OwnerActorId == usurperState.ActorId)
                owned.Add((HexGrid.Distance(seat, port.Hex), port.Id));
        if (owned.Count < 2) return null;   // nothing to rally
        owned.Sort((a, b) => a.Dist != b.Dist
            ? b.Dist.CompareTo(a.Dist) : a.Id.CompareTo(b.Id));
        var loyalist = new HashSet<int>();
        for (int i = 0; i < owned.Count / 2; i++) loyalist.Add(owned[i].Id);
        if (loyalist.Count == 0) return null;

        var species = state.Skeleton.Species[usurperState.SpeciesId];
        var young = GraduationOps.FoundSplinter(state, usurperState, loyalist,
            deposedRuler.Name + " Loyalists", species.Militancy);

        // the provisional polity IS the old order: pre-coup form, pre-lurch
        // line, the deposed ruler back on a seat
        var interior = new PolityInterior
        {
            FormId = preCoupForm,
            FoundingCultureId =
                usurperState.Interior?.FoundingCultureId
                ?? usurperState.SpeciesId,
            Legitimacy = preCoupLegitimacy,
            RulerCharacterId = deposedRuler.Id,
        };
        for (int ax = 0; ax < 4; ax++)
            interior.OfficialIdeology[ax] = preLurchIdeology[ax];
        young.Interior = interior;
        deposedRuler.PolityId = young.ActorId;
        deposedRuler.Role = CharacterRole.Ruler;
        deposedRuler.InstitutionId = young.ActorId;

        // the two halves know each other intimately — a relation at open
        // hostility, then the war itself, objectives: the palace back
        int a = Math.Min(young.ActorId, usurperState.ActorId);
        int b = Math.Max(young.ActorId, usurperState.ActorId);
        var relation = new PolityRelation(a, b, state.EpochIndex)
        {
            Warmth = 0.05,
            Tension = 0.9,
        };
        state.Relations.Add(relation);

        var war = new War(state.Wars.Count,
            WarOps.WarName(state, CasusBelli.CivilWar, -1,
                           usurperState.ActorId),
            young.ActorId, usurperState.ActorId, CasusBelli.CivilWar, -1,
            WarDemand.Submission, state.WorldYear);
        int capital = -1;
        foreach (var port in state.Ports)                     // id order (P6)
            if (port.OwnerActorId == usurperState.ActorId
                && port.Hex.Equals(seat)) { capital = port.Id; break; }
        if (capital >= 0)
            war.Objectives.Add(new WarObjective(war.Objectives.Count,
                WarObjectiveType.CapturePort, capital));
        war.Objectives.Add(new WarObjective(war.Objectives.Count,
            WarObjectiveType.DestroyFleet, usurperState.ActorId));
        state.Wars.Add(war);
        // brothers' wars stay in the family: no allies answer either side
        war.AttackerStrengthAtStart = WarOps.SideStrength(state, war, true);
        war.DefenderStrengthAtStart = WarOps.SideStrength(state, war, false);

        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.WarDeclared,
            new[] { young.ActorId, usurperState.ActorId }, seat,
            Magnitude: loyalist.Count, Valence: -0.9, EventVisibility.Public,
            new WarDeclaredPayload(war.Id, war.Name, young.ActorId,
                usurperState.ActorId, state.Actors[young.ActorId].Name,
                state.Actors[usurperState.ActorId].Name,
                (int)CasusBelli.CivilWar, (int)WarDemand.Submission)));
        return war;
    }
}
