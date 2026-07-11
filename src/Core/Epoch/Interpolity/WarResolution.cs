using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>How a war ended (the settlement record's outcome).</summary>
public enum WarOutcome
{
    ObjectivesCeded = 0,  // the attacker keeps what it took
    Reparations = 1,      // credits flow with the cessions
    Vassalized = 2,       // the loser kneels
    WhitePeace = 3,       // status quo ante — captures return
    Independence = 4,     // a secession war carried
    Submission = 5,       // civil wars: the loser merges back whole
    Annexed = 6,          // a war of annihilation carried: the loser is no more
}

/// <summary>Termination and settlement (interpolity/war.md §Termination):
/// a polity breaks when its politics break — legitimacy collapse, fleet or
/// stockpile exhaustion, capital loss, extinction — or when its controller
/// concedes; settlement is read from per-objective outcomes, and the
/// residue (claims, veterans, legitimacy, tension relief) is first-class.</summary>
public static class WarResolution
{
    /// <summary>The legitimacy war term (polity/factions-and-government.md):
    /// neutral 0.5 at peace; at war it reads objective progress and
    /// exhaustion — winning steadies a throne, a grinding war saps it.</summary>
    public static double WarScore(SimState state, int polityId)
    {
        double score = 0.5;
        int wars = 0;
        foreach (var war in state.Wars)                       // id order (P6)
        {
            if (!war.Active || !war.Involves(polityId)) continue;
            wars++;
            bool attackerSide = war.OnAttackerSide(polityId);
            int taken = 0;
            foreach (var o in war.Objectives)
                if (o.Status == ObjectiveStatus.Taken) taken++;
            double progress = war.Objectives.Count == 0 ? 0
                : (double)taken / war.Objectives.Count;
            double exhaustion = attackerSide
                ? war.AttackerExhaustion : war.DefenderExhaustion;
            score += (attackerSide ? 0.4 * progress : -0.4 * progress)
                     - 0.5 * exhaustion;
        }
        if (wars == 0) return 0.5;
        return Math.Max(0.0, Math.Min(1.0, score));
    }

    /// <summary>Check every active war for a broken side and settle it.
    /// Concessions are the SettlementResponseActs Resolution collected this
    /// step (a leader suing for peace). Returns settlements.</summary>
    public static int Terminate(SimState state,
                                HashSet<(int WarId, int ActorId)>? concessions)
    {
        var knobs = state.Config.War;
        int settled = 0;
        int wars = state.Wars.Count;
        for (int i = 0; i < wars; i++)
        {
            var war = state.Wars[i];
            if (!war.Active) continue;

            // a leader that left the stage (federated, absorbed, submitted)
            // takes its war with it: the successor was never a party —
            // white peace, never a victory over a ghost (review fix 7)
            if (state.Actors[war.AttackerId].Retired
                || state.Actors[war.DefenderId].Retired)
            {
                Settle(state, war, WarOutcome.WhitePeace, winner: -1);
                settled++;
                continue;
            }

            bool attackerConcedes = concessions != null
                && concessions.Contains((war.Id, war.AttackerId));
            // a war of annihilation accepts no surrender: the defender's
            // concession falls on deaf ears — only broken politics, a
            // broken fleet, or the attacker's own exhaustion end it
            bool defenderConcedes = war.Demand != WarDemand.Annihilation
                && concessions != null
                && concessions.Contains((war.Id, war.DefenderId));
            bool attackerBroke = attackerConcedes || SideBroke(state, war, true);
            bool defenderBroke = defenderConcedes || SideBroke(state, war, false);
            bool objectivesDone = AllObjectivesTaken(war);

            if (!attackerBroke && !defenderBroke && !objectivesDone) continue;

            if ((objectivesDone || defenderBroke) && !attackerBroke)
                SettleVictory(state, war);
            else if (war.Demand == WarDemand.Submission
                     && attackerBroke && !defenderBroke)
                // a failed restoration: the provisional polity submits
                Settle(state, war, WarOutcome.Submission,
                       winner: war.DefenderId);
            else
                Settle(state, war, WarOutcome.WhitePeace, winner: -1);
            settled++;
        }
        return settled;
    }

    /// <summary>Break conditions on truth (war.md §Termination): weariness
    /// at the ceiling, politics collapsed, the navy gone, the capital or
    /// everything lost. Extinct belligerents fight no fronts.</summary>
    public static bool SideBroke(SimState state, War war, bool attacker)
    {
        var knobs = state.Config.War;
        int leader = attacker ? war.AttackerId : war.DefenderId;
        double exhaustion = attacker
            ? war.AttackerExhaustion : war.DefenderExhaustion;
        if (exhaustion >= 1.0) return true;
        var pr = state.PolityOf(leader);
        if (pr.Interior != null
            && pr.Interior.Legitimacy < knobs.LegitimacyCollapseFloor)
            return true;
        double atStart = attacker
            ? war.AttackerStrengthAtStart : war.DefenderStrengthAtStart;
        if (atStart > 0 && WarOps.SideStrength(state, war, attacker)
            < knobs.FleetExhaustionShare * atStart) return true;
        // capital loss / extinction
        var seat = state.Actors[leader].Seat;
        bool holdsCapital = false, holdsAnything = false;
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != leader) continue;
            holdsAnything = true;
            if (port.Hex.Equals(seat)) holdsCapital = true;
        }
        return !holdsCapital || !holdsAnything;
    }

    private static bool AllObjectivesTaken(War war)
    {
        foreach (var o in war.Objectives)
            if (o.Status == ObjectiveStatus.Contested) return false;
        return true;
    }

    private static void SettleVictory(SimState state, War war)
    {
        var outcome = war.Demand switch
        {
            WarDemand.Reparations => WarOutcome.Reparations,
            WarDemand.Vassalize when CanVassalize(state, war)
                => WarOutcome.Vassalized,
            WarDemand.Independence => WarOutcome.Independence,
            WarDemand.Submission => WarOutcome.Submission,
            WarDemand.Annihilation
                when state.Actors[war.DefenderId].Entered
                => WarOutcome.Annexed,
            _ => WarOutcome.ObjectivesCeded,
        };
        Settle(state, war, outcome, winner: war.AttackerId);
    }

    private static bool CanVassalize(SimState state, War war) =>
        state.Actors[war.DefenderId].Entered
        && FederationOps.OverlordOf(state, war.DefenderId) < 0
        && FederationOps.OverlordOf(state, war.AttackerId) < 0
        && !FederationOps.HasVassals(state, war.DefenderId);

    /// <summary>Execute the settlement and its residue: cessions hold or
    /// return, reparations flow conserved, bonds bind or dissolve, claims
    /// raise, veterans embitter, thrones steady or crack, tension unloads.</summary>
    private static void Settle(SimState state, War war, WarOutcome outcome,
                               int winner)
    {
        var knobs = state.Config.War;
        int loser = winner == war.AttackerId ? war.DefenderId
            : winner == war.DefenderId ? war.AttackerId : -1;
        var relation = state.RelationOf(war.AttackerId, war.DefenderId);
        int portsCeded = 0;
        double reparations = 0;

        // fleets stand down FIRST: a submission merge or a retired leader
        // must never strand a war fleet on a blockade station (review fix 1)
        Demobilize(state, war);

        if (outcome == WarOutcome.WhitePeace)
        {
            // status quo ante: captures return with their facilities — to a
            // defender still on the stage (a retired one stays history)
            foreach (var objective in war.Objectives)
            {
                if (objective.Type != WarObjectiveType.CapturePort
                    || objective.Status != ObjectiveStatus.Taken
                    || !state.Actors[war.DefenderId].Entered) continue;
                var port = state.Ports[objective.TargetId];
                if (port.OwnerActorId != war.AttackerId) continue;
                foreach (var facility in state.Facilities)    // id order (P6)
                    if (facility.OwnerActorId == war.AttackerId
                        && MarketEngine.AttachedMarketIndex(state, facility)
                           == port.Id)
                        facility.OwnerActorId = war.DefenderId;
                port.OwnerActorId = war.DefenderId;
            }
        }
        else if (winner == war.AttackerId)
        {
            // the taken stay taken: cessions from per-objective outcomes,
            // each a standing grudge (tomorrow's tension)
            foreach (var objective in war.Objectives)
            {
                if (objective.Type != WarObjectiveType.CapturePort
                    || objective.Status != ObjectiveStatus.Taken
                    || state.Ports[objective.TargetId].OwnerActorId
                       != war.AttackerId) continue;
                portsCeded++;
                if (relation != null && !relation.HasLiveClaim(
                        ClaimType.LostTerritory, war.DefenderId,
                        objective.TargetId))
                {
                    relation.Claims.Add(new RelationClaim(
                        ClaimType.LostTerritory, war.DefenderId,
                        objective.TargetId, state.WorldYear));
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational, WorldEventType.ClaimRaised,
                        new[] { war.DefenderId, war.AttackerId },
                        state.Ports[objective.TargetId].Hex,
                        Magnitude: 1.0, Valence: -0.4,
                        EventVisibility.Regional,
                        new ClaimRaisedPayload(war.DefenderId, war.AttackerId,
                            (int)ClaimType.LostTerritory, objective.TargetId)));
                }
            }
            if (outcome == WarOutcome.Reparations)
            {
                var loserRecord = state.PolityOf(war.DefenderId);
                reparations = Math.Max(0.0, loserRecord.Credits)
                              * knobs.ReparationsShare;
                loserRecord.Credits -= reparations;
                state.PolityOf(war.AttackerId).Credits += reparations;
            }
            if (outcome == WarOutcome.Vassalized && relation != null)
                FederationOps.Bind(state, relation, war.DefenderId);
            if (outcome == WarOutcome.Independence && relation != null
                && relation.VassalPolityId == war.AttackerId)
            {
                relation.VassalPolityId = -1;
                relation.VassalSinceYear = -1;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.VassalSeceded,
                    new[] { war.AttackerId, war.DefenderId },
                    state.Actors[war.AttackerId].Seat,
                    Magnitude: 1.0, Valence: 0.4, EventVisibility.Public,
                    new VassalSecededPayload(war.DefenderId, war.AttackerId,
                        state.Actors[war.DefenderId].Name,
                        state.Actors[war.AttackerId].Name)));
            }
        }

        if ((outcome == WarOutcome.Submission
             || outcome == WarOutcome.Annexed) && winner >= 0 && loser >= 0)
        {
            // the loser merges whole through the same plumbing federations
            // use — for Annexed this is conquest: the winner inherits the
            // segments (and the accommodation strain of ruling them; a
            // conquest empire is NOT a treaty federation)
            FederationOps.DissolveFactionsOf(state, loser);
            FederationOps.MergeInto(state, loser, winner);
            FederationOps.Retire(state, loser);
        }

        // wind-down: the war closes, gauges unload
        war.Active = false;
        war.EndedYear = state.WorldYear;
        if (relation != null)
            relation.Tension *= 1.0 - knobs.SettlementTensionRelief;

        // residue: veterans embitter the sword parties; thrones steady or
        // crack; the settlement is a public record
        BumpVeterans(state, war.AttackerId, knobs.VeteranMilitancyBump);
        BumpVeterans(state, war.DefenderId, knobs.VeteranMilitancyBump);
        if (winner >= 0)
        {
            var winnerInterior = state.PolityOf(winner).Interior;
            if (winnerInterior != null)
                winnerInterior.Legitimacy = Math.Min(1.0,
                    winnerInterior.Legitimacy + knobs.VictoryLegitimacy);
        }
        if (loser >= 0)
        {
            var loserInterior = state.PolityOf(loser).Interior;
            if (loserInterior != null)
                loserInterior.Legitimacy = Math.Max(0.0,
                    loserInterior.Legitimacy - knobs.DefeatLegitimacy);
        }
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PeaceSettled,
            new[] { war.AttackerId, war.DefenderId },
            state.Actors[war.DefenderId].Seat,
            Magnitude: portsCeded, Valence: 0.5, EventVisibility.Public,
            new PeaceSettledPayload(war.Id, war.Name, (int)outcome, winner,
                state.Actors[war.AttackerId].Name,
                state.Actors[war.DefenderId].Name, portsCeded, reparations)));
    }

    /// <summary>The attacker's war fleets stand down to reserve at their
    /// supply bases; escorts re-pool through the next Allocation.</summary>
    private static void Demobilize(SimState state, War war)
    {
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != war.AttackerId
                || fleet.Posture is not (FleetPosture.Blockade
                    or FleetPosture.Expedition)) continue;
            bool warStation = false;
            foreach (var objective in war.Objectives)
            {
                var posture = objective.Type == WarObjectiveType.DestroyFleet
                    ? FleetPosture.Expedition : FleetPosture.Blockade;
                if (fleet.Posture != posture) continue;
                if (objective.Type == WarObjectiveType.CapturePort
                    && fleet.TargetId == objective.TargetId) warStation = true;
                if (objective.Type == WarObjectiveType.BlockadeLane)
                {
                    var lane = state.Lanes[objective.TargetId];
                    if (fleet.TargetId == lane.PortAId
                        || fleet.TargetId == lane.PortBId) warStation = true;
                }
                if (objective.Type == WarObjectiveType.DestroyFleet
                    && fleet.Posture == FleetPosture.Expedition
                    // only THIS war's fleet hunt: the squadron stands at the
                    // defender's capital (or a port that was the defender's)
                    && fleet.TargetId >= 0
                    && fleet.TargetId < state.Ports.Count
                    && (fleet.TargetId == WarConduct.CapitalPortOf(state,
                            war.DefenderId)
                        || state.Ports[fleet.TargetId].OwnerActorId
                           == war.DefenderId))
                    warStation = true;
            }
            if (!warStation) continue;
            fleet.Posture = FleetPosture.Reserve;
            fleet.TargetId = -1;
            if (fleet.HomePortId >= 0 && fleet.HomePortId < state.Ports.Count
                && state.Ports[fleet.HomePortId].OwnerActorId
                   == war.AttackerId)
                fleet.Hex = state.Ports[fleet.HomePortId].Hex;
        }
    }

    /// <summary>Veterans strengthen military factions (war.md §Aftermath):
    /// the sword parties come home harder.</summary>
    private static void BumpVeterans(SimState state, int polityId, double bump)
    {
        foreach (var faction in state.Factions)               // id order (P6)
            if (faction.Active && faction.PolityId == polityId
                && faction.Basis == FactionBasis.Military)
                faction.Militancy = Math.Min(1.0, faction.Militancy + bump);
    }
}
