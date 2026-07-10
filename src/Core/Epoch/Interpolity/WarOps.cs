using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The road to war (interpolity/war.md §Causes): the casus-belli
/// menu computed from real state, the spark mechanism rolling incidents in
/// contested space, and declaration — an Intent act carrying objectives and
/// a settlement demand, resolved here. Conduct and termination live in
/// WarConduct (H6/H7).</summary>
public static class WarOps
{
    /// <summary>Epochs a border incident stays "recent" — the spark's
    /// window as a viable casus belli (structural).</summary>
    public const int IncidentFreshEpochs = 2;

    // ---- war registry helpers ----

    public static War? ActiveWarBetween(SimState state, int a, int b)
    {
        foreach (var war in state.Wars)                       // id order (P6)
            if (war.Active && war.Involves(a) && war.Involves(b)) return war;
        return null;
    }

    public static bool AtWar(SimState state, int polityId)
    {
        foreach (var war in state.Wars)                       // id order (P6)
            if (war.Active && war.Involves(polityId)) return true;
        return false;
    }

    // ---- the casus-belli menu ----

    /// <summary>Every viable declared goal one polity holds against
    /// another, from real state (perfect-info stub: slice I stales the
    /// inputs, never the shape). Returns (cause, subject) pairs in stable
    /// menu order.</summary>
    public static List<(CasusBelli Cause, int SubjectId)> Menu(
        SimState state, int selfId, int otherId)
    {
        var menu = new List<(CasusBelli, int)>();
        var relation = state.RelationOf(selfId, otherId);
        if (relation == null || !RelationsOps.BothLive(state, relation))
            return menu;
        var knobs = state.Config.War;
        var self = state.PolityOf(selfId);
        var other = state.PolityOf(otherId);

        // economic: a price shock at home on a good they produce
        int seizureGood = SeizureGood(state, selfId, otherId);
        if (seizureGood >= 0)
            menu.Add((CasusBelli.ResourceSeizure, seizureGood));
        // economic: they hold a chokepoint port within our reach
        int chokepoint = ChokepointPort(state, selfId, otherId);
        if (chokepoint >= 0)
            menu.Add((CasusBelli.ChokepointControl, chokepoint));
        // economic: they blockade us
        if (BlockadesUs(state, selfId, otherId))
            menu.Add((CasusBelli.PunitiveInterdiction, -1));
        // ideological: doctrine gap under a zealous throne
        if (RelationsOps.IdeologyGap(self, other)
            * RulerZeal(state, self) >= knobs.CrusadeThreshold)
            menu.Add((CasusBelli.Crusade, -1));
        // ideological: our kin (or a suppressed emergence, H8) under their rule
        foreach (var claim in relation.Claims)
            if (!claim.Released && claim.HolderPolityId == selfId
                && claim.Type is ClaimType.CulturalKin or ClaimType.Liberation)
            {
                menu.Add((CasusBelli.Liberation, claim.SubjectId));
                break;
            }
        // ideological: a rising incompatible power out-arming us
        if (other.TechTier[(int)TechDomain.Military]
            > self.TechTier[(int)TechDomain.Military]
            && relation.Warmth < relation.Tension)
            menu.Add((CasusBelli.Containment, -1));
        // political: a live succession claim on their throne
        foreach (var claim in relation.Claims)
            if (!claim.Released && claim.HolderPolityId == selfId
                && claim.Type == ClaimType.Succession)
            {
                menu.Add((CasusBelli.SuccessionClaim, claim.SubjectId));
                break;
            }
        // political: lost territory they hold (secessions, settlements)
        foreach (var claim in relation.Claims)
            if (!claim.Released && claim.HolderPolityId == selfId
                && claim.Type == ClaimType.LostTerritory)
            {
                menu.Add((CasusBelli.GrievanceDischarge, claim.SubjectId));
                break;
            }
        // political: the military faction demands employment
        var discharge = LoudestMilitaryFaction(state, selfId);
        if (discharge != null && discharge.Strength * discharge.Grievance
            >= knobs.GrievanceDischargeFloor
            && !menu.Exists(m => m.Item1 == CasusBelli.GrievanceDischarge))
            menu.Add((CasusBelli.GrievanceDischarge, discharge.Id));
        // political: a vassal's independence is fought for, not asked
        if (FederationOps.OverlordOf(state, selfId) == otherId)
            menu.Add((CasusBelli.VassalSecession, -1));
        // the spark: a recent incident in contested space
        if (relation.LastIncidentEpoch >= 0
            && state.EpochIndex - relation.LastIncidentEpoch
               <= IncidentFreshEpochs)
            menu.Add((CasusBelli.BorderIncident, -1));
        return menu;
    }

    /// <summary>A good shocked at home (price ≥ multiple × founding at any
    /// own market) that the other side produces — lowest good id, or −1.</summary>
    private static int SeizureGood(SimState state, int selfId, int otherId)
    {
        var eco = state.Config.Economy;
        double multiple = state.Config.War.PriceShockMultiple;
        Span<bool> shocked = stackalloc bool[Goods.All.Count];
        bool any = false;
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != selfId) continue;
            var market = state.Markets[port.Id];
            for (int g = 0; g < Goods.All.Count; g++)
                if (market.Price[g]
                    >= multiple * Market.InitialPrice(eco, (GoodId)g))
                { shocked[g] = true; any = true; }
        }
        if (!any) return -1;
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.OwnerActorId != otherId) continue;
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            foreach (var g in def.Produces)
                if (shocked[(int)g]) return (int)g;
        }
        return -1;
    }

    /// <summary>The other side's chokepoint port nearest our reach, or −1.</summary>
    private static int ChokepointPort(SimState state, int selfId, int otherId)
    {
        int reach = state.Config.Expansion.ColonizationReachHexes;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != otherId) continue;
            if (!state.Skeleton.TryGetCell(HexGrid.CellOf(port.Hex), out var cell)
                || !cell.IsChokepoint) continue;
            foreach (var own in state.Ports)
                if (own.OwnerActorId == selfId
                    && HexGrid.Distance(own.Hex, port.Hex) <= reach)
                    return port.Id;
        }
        return -1;
    }

    private static bool BlockadesUs(SimState state, int selfId, int otherId)
    {
        foreach (var fleet in state.Fleets)                   // id order (P6)
            if (fleet.Posture == FleetPosture.Blockade
                && fleet.OwnerActorId == otherId && fleet.TotalHulls > 0
                && fleet.TargetId >= 0 && fleet.TargetId < state.Ports.Count
                && state.Ports[fleet.TargetId].OwnerActorId == selfId)
                return true;
        return false;
    }

    private static double RulerZeal(SimState state, PolityRecord pr)
    {
        int id = pr.Interior?.RulerCharacterId ?? -1;
        return id >= 0 && id < state.Characters.Count
            ? state.Characters[id].Zeal : 0.5;
    }

    private static Faction? LoudestMilitaryFaction(SimState state, int polityId)
    {
        Faction? loudest = null;
        foreach (var faction in state.Factions)               // id order (P6)
            if (faction.Active && faction.PolityId == polityId
                && faction.Basis == FactionBasis.Military
                && (loudest == null || faction.Strength * faction.Grievance
                    > loudest.Strength * loudest.Grievance))
                loudest = faction;
        return loudest;
    }

    // ---- the spark mechanism ----

    /// <summary>Incidents roll continuously in high-friction space (war.md):
    /// patrol clashes and enforcement killings in the contested-overlap
    /// zones. Low-tension incidents fizzle into demands and apologies —
    /// also events; loaded ones prime the BorderIncident casus belli.
    /// Returns incidents rolled.</summary>
    public static int Incidents(SimState state,
        Dictionary<(int A, int B), RelationsOps.PairGeometry> geometry)
    {
        var knobs = state.Config.War;
        var relKnobs = state.Config.Relations;
        int incidents = 0;
        foreach (var relation in state.Relations)             // creation order (P6)
        {
            if (!RelationsOps.BothLive(state, relation)) continue;
            if (ActiveWarBetween(state, relation.PolityAId,
                                 relation.PolityBId) != null) continue;
            if (!geometry.TryGetValue((relation.PolityAId, relation.PolityBId),
                                      out var g) || g.OverlapPairs <= 0)
                continue;
            double p = knobs.IncidentRatePerEpoch
                       * Math.Min(1.0, g.OverlapPairs / relKnobs.OverlapSaturation);
            // the non-aggression rung's spark de-escalation
            if (relation.Rung >= TreatyRung.NonAggression)
                p *= 1.0 - relKnobs.NonAggressionDamping;
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    RollChannel.WarSpark, state.EpochIndex,
                    relation.PolityAId, relation.PolityBId) >= p) continue;
            relation.LastIncidentEpoch = state.EpochIndex;
            relation.Tension = Math.Min(1.0,
                relation.Tension + knobs.IncidentTensionBump);
            incidents++;
            bool loaded = relation.Tension >= knobs.WarTensionFloor;
            var flashpoint = HexGrid.Round(
                (g.ClosestA.Q + g.ClosestB.Q) * 0.5,
                (g.ClosestA.R + g.ClosestB.R) * 0.5);
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.BorderIncident,
                new[] { relation.PolityAId, relation.PolityBId }, flashpoint,
                Magnitude: relation.Tension, Valence: -0.4,
                EventVisibility.Regional,
                new BorderIncidentPayload(relation.PolityAId,
                    relation.PolityBId,
                    state.Actors[relation.PolityAId].Name,
                    state.Actors[relation.PolityBId].Name, loaded)));
        }
        return incidents;
    }

    // ---- declaration ----

    /// <summary>Resolve a declaration (war.md: an Intent act with an
    /// objective set and a settlement demand). Validated against truth;
    /// the defender's defense-alliance partners and both leaders' vassals
    /// join as supporting belligerents. Returns the war, or null.</summary>
    public static War? DeclareWar(SimState state, DeclareWarAct act)
    {
        int attacker = act.ActorId, defender = act.TargetPolityId;
        if (attacker == defender || defender >= state.Actors.Count
            || state.Actors[attacker].Kind != ActorKind.Polity
            || state.Actors[defender].Kind != ActorKind.Polity
            || !state.Actors[attacker].Entered
            || !state.Actors[defender].Entered) return null;
        var relation = state.RelationOf(attacker, defender);
        if (relation == null) return null;
        if (ActiveWarBetween(state, attacker, defender) != null) return null;
        var cause = (CasusBelli)act.CasusBelli;
        // the vassal lock: a vassal fights only for its own independence
        int overlord = FederationOps.OverlordOf(state, attacker);
        if (overlord >= 0 && (overlord != defender
            || cause != CasusBelli.VassalSecession)) return null;

        // ground the objective set: defender-owned ports, defender lanes,
        // the defender's navy; an empty spec defaults to the navy
        var objectives = new List<WarObjective>();
        foreach (var spec in act.Objectives)
        {
            if (objectives.Count >= 4) break;
            switch (spec.Type)
            {
                case WarObjectiveType.CapturePort:
                    if (spec.TargetId >= 0 && spec.TargetId < state.Ports.Count
                        && state.Ports[spec.TargetId].OwnerActorId == defender)
                        objectives.Add(new WarObjective(objectives.Count,
                            spec.Type, spec.TargetId));
                    break;
                case WarObjectiveType.BlockadeLane:
                    if (spec.TargetId >= 0 && spec.TargetId < state.Lanes.Count)
                    {
                        var lane = state.Lanes[spec.TargetId];
                        if (state.Ports[lane.PortAId].OwnerActorId == defender
                            || state.Ports[lane.PortBId].OwnerActorId == defender)
                            objectives.Add(new WarObjective(objectives.Count,
                                spec.Type, spec.TargetId));
                    }
                    break;
                case WarObjectiveType.DestroyFleet:
                    objectives.Add(new WarObjective(objectives.Count,
                        spec.Type, defender));
                    break;
            }
        }
        if (objectives.Count == 0)
            objectives.Add(new WarObjective(0, WarObjectiveType.DestroyFleet,
                                            defender));

        var war = new War(state.Wars.Count, WarName(state, cause,
                act.SubjectId, defender), attacker, defender, cause,
            act.SubjectId, (WarDemand)act.Demand, state.WorldYear);
        war.Objectives.AddRange(objectives);
        state.Wars.Add(war);

        // supporting belligerents: the defender's defense allies (real
        // deterrence), and both leaders' vassals under obligation
        foreach (var rel in state.Relations)                  // creation order (P6)
        {
            if (!RelationsOps.BothLive(state, rel)) continue;
            if (rel.Involves(defender) && rel.Rung == TreatyRung.DefenseAlliance)
            {
                int ally = rel.OtherOf(defender);
                if (ally != attacker && !war.Involves(ally))
                    war.DefenderAllies.Add(ally);
            }
            if (rel.VassalPolityId >= 0)
            {
                // vassals march under their overlord's banner, both ways
                if (rel.OtherOf(rel.VassalPolityId) == attacker
                    && rel.VassalPolityId != defender
                    && !war.Involves(rel.VassalPolityId))
                    war.AttackerAllies.Add(rel.VassalPolityId);
                if (rel.OtherOf(rel.VassalPolityId) == defender
                    && rel.VassalPolityId != attacker
                    && !war.Involves(rel.VassalPolityId))
                    war.DefenderAllies.Add(rel.VassalPolityId);
                // and the protection bought with tribute is DELIVERED: an
                // attacked vassal's overlord answers (review fix 5) —
                // unless the overlord is the attacker (a secession war)
                if (rel.VassalPolityId == defender
                    && rel.OtherOf(defender) != attacker
                    && !war.Involves(rel.OtherOf(defender)))
                    war.DefenderAllies.Add(rel.OtherOf(defender));
            }
        }
        war.AttackerStrengthAtStart = SideStrength(state, war, attacker: true);
        war.DefenderStrengthAtStart = SideStrength(state, war, attacker: false);

        // declaring on a treaty partner IS breaking the treaty
        if (relation.Rung != TreatyRung.None)
        {
            var brokenRung = relation.Rung;
            relation.Rung = TreatyRung.None;
            relation.RungEpoch = -1;
            relation.Warmth = Math.Max(0.0, relation.Warmth
                - state.Config.Relations.BreakWarmthPenalty);
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.TreatyBroken,
                new[] { attacker, defender }, state.Actors[attacker].Seat,
                Magnitude: (int)brokenRung, Valence: -0.7,
                EventVisibility.Public,
                new TreatyBrokenPayload(attacker, defender,
                    state.Actors[attacker].Name, state.Actors[defender].Name,
                    (int)brokenRung)));
        }
        relation.OfferedRung = TreatyRung.None;
        relation.OfferedById = -1;
        relation.OfferEpoch = -1;

        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.WarDeclared,
            new[] { attacker, defender }, state.Actors[defender].Seat,
            Magnitude: objectives.Count, Valence: -0.8, EventVisibility.Public,
            new WarDeclaredPayload(war.Id, war.Name, attacker, defender,
                state.Actors[attacker].Name, state.Actors[defender].Name,
                (int)cause, (int)war.Demand)));
        return war;
    }

    /// <summary>A side's mustered strength: the leader plus its supporters.</summary>
    public static double SideStrength(SimState state, War war, bool attacker)
    {
        double strength = FleetOps.WarStrength(state,
            attacker ? war.AttackerId : war.DefenderId);
        foreach (var ally in attacker ? war.AttackerAllies : war.DefenderAllies)
            strength += FleetOps.WarStrength(state, ally);
        return strength;
    }

    /// <summary>"the <X> War" — deterministic from the cause (P8: the name
    /// tells you what it was about).</summary>
    public static string WarName(SimState state, CasusBelli cause,
                                 int subjectId, int defenderId)
    {
        string defender = state.Actors[defenderId].Name;
        switch (cause)
        {
            case CasusBelli.ResourceSeizure when subjectId >= 0
                && subjectId < Goods.All.Count:
                return "the " + Goods.All[subjectId].Name + " War";
            case CasusBelli.SuccessionClaim when subjectId >= 0
                && subjectId < state.Dynasties.Count:
                return "the " + state.Dynasties[subjectId].Name + " Succession";
            case CasusBelli.Liberation when subjectId >= 0
                && subjectId < state.Cultures.Count:
                return "the " + state.Cultures[subjectId].Name + " Liberation";
            case CasusBelli.Crusade:
                return "the " + defender + " Crusade";
            case CasusBelli.VassalSecession:
                return "the " + state.Actors[defenderId].Name
                       + " Secession War";   // named for the overlord fought
            case CasusBelli.CivilWar:
                return "the " + defender + " Civil War";
            default:
                return "the " + defender + " War";
        }
    }
}
