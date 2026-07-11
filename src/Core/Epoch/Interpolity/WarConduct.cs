using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>How the outcome of one engagement fell (war.md §Conduct 2).</summary>
public enum BattleOutcome
{
    DecisiveAttacker = 0,
    Attrition = 1,
    Stalemate = 2,
    DecisiveDefender = 3,
}

/// <summary>The theater/objective model (interpolity/war.md §Conduct), one
/// epoch at a time: each war leader's Intent-set doctrine assigns fleets to
/// objectives, engagements resolve on fleet vectors modified by
/// fortification, supply reach, and commander competence, sieges grind on
/// defender reserves and fortress tiers, and captures transfer ports with
/// their segments intact. Hull losses conserve into wreckage at the hex
/// (P4). Blockade fleets sever lanes through FleetOps.SeveredLaneIds — the
/// real interdiction that retires the REPL's debug hook.</summary>
public static class WarConduct
{
    /// <summary>Mobilization share of pooled warships per doctrine posture
    /// (structural: the doctrine IS the dial).</summary>
    private static double MobilizationShare(DoctrinePosture posture) =>
        posture switch
        {
            DoctrinePosture.Aggressive => 0.8,
            DoctrinePosture.Defensive => 0.4,
            _ => 0.6,
        };

    /// <summary>Fight every active war one epoch forward. Returns battles
    /// fought (the phase note).</summary>
    public static int FightWars(SimState state)
    {
        int battles = 0;
        int wars = state.Wars.Count;   // settlements never append mid-fight
        for (int i = 0; i < wars; i++)
        {
            var war = state.Wars[i];
            if (!war.Active) continue;
            // a war whose leader left the stage fights no ghosts — the
            // same phase's Terminate settles it white (review fix 7)
            if (state.Actors[war.AttackerId].Retired
                || state.Actors[war.DefenderId].Retired) continue;
            Reconcile(state, war);
            Mobilize(state, war);
            battles += Engage(state, war);
        }
        return battles;
    }

    /// <summary>Objectives whose targets already changed hands are held,
    /// not contested — nobody besieges their own port.</summary>
    private static void Reconcile(SimState state, War war)
    {
        foreach (var objective in war.Objectives)
        {
            if (objective.Status != ObjectiveStatus.Contested) continue;
            if (objective.Type == WarObjectiveType.CapturePort
                && state.Ports[objective.TargetId].OwnerActorId
                   != war.DefenderId)
                objective.Status = ObjectiveStatus.Taken;
            if (objective.Type == WarObjectiveType.BlockadeLane)
            {
                var lane = state.Lanes[objective.TargetId];
                if (state.Ports[lane.PortAId].OwnerActorId != war.DefenderId
                    && state.Ports[lane.PortBId].OwnerActorId != war.DefenderId)
                    objective.Status = ObjectiveStatus.Taken;
            }
        }
    }

    // ---- assignment ----

    /// <summary>The attacker's Intent posts fleets to objectives per
    /// doctrine and commander boldness: warships pool from the leader's
    /// Reserve and Patrol stations into one war fleet per contested
    /// objective (Blockade at ports under siege or interdiction — real
    /// severed lanes — Expedition for fleet hunts).</summary>
    private static void Mobilize(SimState state, War war)
    {
        var leader = state.PolityOf(war.AttackerId);
        if (!state.Actors[war.AttackerId].Entered) return;
        var policies = state.Actors[war.AttackerId].Policies as PolityPolicies
                       ?? PolityPolicies.Default;
        double share = MobilizationShare(policies.Doctrine.Posture);
        // the marshal's nerve stretches or shrinks the commitment
        var marshal = MarshalOf(state, war.AttackerId);
        if (marshal != null) share += 0.2 * (marshal.Boldness - 0.5);
        share = Math.Max(0.2, Math.Min(0.9, share));

        var contested = new List<WarObjective>();
        foreach (var o in war.Objectives)
            if (o.Status == ObjectiveStatus.Contested) contested.Add(o);
        if (contested.Count == 0) return;   // nothing left to fight over

        // pool mobilizable warships (reserve + patrol; standing war fleets
        // keep what they hold)
        var pool = new List<HullGroup>();
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != war.AttackerId
                || fleet.Posture is not (FleetPosture.Reserve
                    or FleetPosture.Patrol)) continue;
            for (int i = fleet.Hulls.Count - 1; i >= 0; i--)
            {
                var g = fleet.Hulls[i];
                if (!ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                    continue;
                int take = (int)Math.Floor(g.Count * share);
                if (take <= 0) continue;
                Blend(pool, g.DesignId, take, g.Grade);
                fleet.RemoveHulls(g.DesignId, take);
            }
        }
        pool.Sort((x, y) => x.DesignId.CompareTo(y.DesignId));
        int totalHulls = 0;
        foreach (var g in pool) totalHulls += g.Count;
        int perObjective = totalHulls / contested.Count;
        foreach (var objective in contested)                  // id order
        {
            var fleet = WarFleet(state, war, objective);
            DealHulls(pool, fleet, perObjective);
            var hex = ObjectiveHex(state, objective);
            fleet.Hex = hex;
            fleet.HomePortId = NearestOwnPort(state, war.AttackerId, hex);
        }
        // remainder reinforces the first front
        DealHulls(pool, WarFleet(state, war, contested[0]), int.MaxValue);
    }

    /// <summary>The attacker's standing fleet at one objective: Blockade at
    /// ports and lanes (interdiction is one hex address), Expedition for
    /// the fleet hunt.</summary>
    private static FleetRecord WarFleet(SimState state, War war,
                                        WarObjective objective)
    {
        var posture = objective.Type == WarObjectiveType.DestroyFleet
            ? FleetPosture.Expedition : FleetPosture.Blockade;
        int target = objective.Type switch
        {
            WarObjectiveType.CapturePort => objective.TargetId,
            WarObjectiveType.BlockadeLane => BlockadedPort(state,
                objective.TargetId, war.DefenderId),
            _ => CapitalPort(state, war.DefenderId),
        };
        return FleetOps.PostureFleet(state, war.AttackerId, posture, target);
    }

    /// <summary>The lane's defender-owned endpoint — where the blockade
    /// squadron stations (its approaches sever every lane it touches).</summary>
    private static int BlockadedPort(SimState state, int laneId, int defenderId)
    {
        var lane = state.Lanes[laneId];
        return state.Ports[lane.PortAId].OwnerActorId == defenderId
            ? lane.PortAId : lane.PortBId;
    }

    /// <summary>The polity's capital port (its seat hex), else its lowest
    /// port id, else −1 — the fleet hunt's station address.</summary>
    public static int CapitalPortOf(SimState state, int polityId) =>
        CapitalPort(state, polityId);

    private static int CapitalPort(SimState state, int polityId)
    {
        var seat = state.Actors[polityId].Seat;
        int fallback = -1;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != polityId) continue;
            if (port.Hex.Equals(seat)) return port.Id;
            if (fallback < 0) fallback = port.Id;
        }
        return fallback;
    }

    private static HexCoordinate ObjectiveHex(SimState state,
                                              WarObjective objective)
    {
        switch (objective.Type)
        {
            case WarObjectiveType.CapturePort:
                return state.Ports[objective.TargetId].Hex;
            case WarObjectiveType.BlockadeLane:
                var lane = state.Lanes[objective.TargetId];
                return HexGrid.Round(
                    (state.Ports[lane.PortAId].Hex.Q
                     + state.Ports[lane.PortBId].Hex.Q) * 0.5,
                    (state.Ports[lane.PortAId].Hex.R
                     + state.Ports[lane.PortBId].Hex.R) * 0.5);
            default:
                int capital = CapitalPort(state, objective.TargetId);
                return capital >= 0 ? state.Ports[capital].Hex
                    : state.Actors[objective.TargetId].Seat;
        }
    }

    private static int NearestOwnPort(SimState state, int actorId,
                                      HexCoordinate hex)
    {
        int best = -1, bestDist = int.MaxValue;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != actorId) continue;
            int d = HexGrid.Distance(port.Hex, hex);
            if (d < bestDist) { bestDist = d; best = port.Id; }
        }
        return best;
    }

    // ---- engagement resolution ----

    private static int Engage(SimState state, War war)
    {
        var knobs = state.Config.War;
        int battles = 0;
        double attackerHullsLost = 0, defenderHullsLost = 0;
        double attackerHullsAtRisk = Math.Max(1, SideWarshipHulls(state, war, true));
        double defenderHullsAtRisk = Math.Max(1, SideWarshipHulls(state, war, false));
        foreach (var objective in war.Objectives)             // id order
        {
            if (objective.Status != ObjectiveStatus.Contested) continue;
            var warFleet = WarFleet(state, war, objective);
            if (warFleet.TotalHulls == 0)
            {
                objective.SiegeEpochs = 0;   // no besieger, no siege
                continue;
            }
            var hex = ObjectiveHex(state, objective);

            // attacker power: vectors × readiness × supply reach ×
            // commander competence, plus the coalition's distant support
            var av = FleetOps.Vectors(state, warFleet);
            double supply = SupplyFactor(state, warFleet, hex, knobs);
            double attPower = (av.Strike + av.Sustained) * warFleet.Readiness
                              * supply
                              * CompetenceFactor(state, warFleet.CommanderId)
                              + knobs.AllySupportFactor
                                * AllyStrength(state, war, attacker: true);
            // defender power: everything stationed at the objective, a
            // mobile-response share of the rest, allied support, and the
            // fortress guns
            double local = LocalDefense(state, war, hex, out int defCommander);
            double defenderTotal = FleetOps.WarStrength(state, war.DefenderId);
            double defPower = (local
                               + knobs.MobileResponseShare
                                 * Math.Max(0, defenderTotal - local)
                               + knobs.AllySupportFactor
                                 * AllyStrength(state, war, attacker: false))
                              * CompetenceFactor(state, defCommander)
                              * (1.0 + FortressBonus(state, war.DefenderId, hex,
                                                     knobs));
            // screens blunt strike weight: rock-paper-scissors texture
            double ratio = attPower + defPower <= 0 ? 0.5
                : attPower / (attPower + defPower);
            double u = EpochRolls.NextDouble(state.Config.MasterSeed,
                RollChannel.Battle, state.EpochIndex, war.Id, objective.Id);
            double margin = ratio - u;
            var outcome = margin > 0.3 ? BattleOutcome.DecisiveAttacker
                : margin < -0.3 ? BattleOutcome.DecisiveDefender
                : Math.Abs(margin) <= 0.1 ? BattleOutcome.Stalemate
                : BattleOutcome.Attrition;

            // losses conserve into wreckage at the objective's hex
            double attShare = outcome switch
            {
                BattleOutcome.DecisiveAttacker => knobs.LossDecisiveWinner,
                BattleOutcome.DecisiveDefender => knobs.LossDecisiveLoser,
                BattleOutcome.Attrition => knobs.LossAttrition,
                _ => knobs.LossStalemate,
            };
            double defShare = outcome switch
            {
                BattleOutcome.DecisiveAttacker => knobs.LossDecisiveLoser,
                BattleOutcome.DecisiveDefender => knobs.LossDecisiveWinner,
                BattleOutcome.Attrition => knobs.LossAttrition,
                _ => knobs.LossStalemate,
            };
            int attLost = WreckAt(state, warFleet, hex,
                (int)Math.Round(warFleet.TotalHulls * attShare));
            int defLost = WreckDefendersAt(state, war, hex, defShare);
            attackerHullsLost += attLost;
            defenderHullsLost += defLost;
            battles++;

            // decisive days scar the ground they were fought over
            if (outcome == BattleOutcome.DecisiveAttacker)
                DamageFacilities(state, war.DefenderId, hex,
                                 knobs.BattleFacilityDamage);

            CommanderFates(state, war, warFleet.CommanderId, defCommander,
                           outcome, hex);
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.BattleFought,
                new[] { war.AttackerId, war.DefenderId }, hex,
                Magnitude: attLost + defLost, Valence: -0.7,
                EventVisibility.Public,
                new BattleFoughtPayload(war.Id, war.Name, (int)objective.Type,
                    objective.TargetId, war.AttackerId, war.DefenderId,
                    (int)outcome, attLost, defLost,
                    warFleet.CommanderId, CommanderName(state, warFleet.CommanderId),
                    defCommander, CommanderName(state, defCommander))));

            Progress(state, war, objective, warFleet, outcome, attPower,
                     defPower, hex);
        }

        // weariness: time under arms plus this epoch's blood (H7 reads it)
        int years = state.Config.Sim.YearsPerEpoch;
        double baseWear = state.Config.Economy.WarWearinessPerYear * years;
        war.AttackerExhaustion = Math.Min(1.0, war.AttackerExhaustion + baseWear
            + knobs.ExhaustionPerLoss * attackerHullsLost / attackerHullsAtRisk);
        war.DefenderExhaustion = Math.Min(1.0, war.DefenderExhaustion + baseWear
            + knobs.ExhaustionPerLoss * defenderHullsLost / defenderHullsAtRisk);
        return battles;
    }

    /// <summary>Objective progression: sieges tick under superiority and
    /// break on relief; blockades count held epochs; the fleet hunt ends
    /// when the enemy navy is broken.</summary>
    private static void Progress(SimState state, War war,
        WarObjective objective, FleetRecord warFleet, BattleOutcome outcome,
        double attPower, double defPower, HexCoordinate hex)
    {
        var knobs = state.Config.War;
        switch (objective.Type)
        {
            case WarObjectiveType.CapturePort:
            {
                if (outcome == BattleOutcome.DecisiveDefender)
                {
                    objective.SiegeEpochs = 0;   // the relief carried
                    break;
                }
                if (attPower <= defPower) break;   // no superiority, no siege
                objective.SiegeEpochs++;
                var port = state.Ports[objective.TargetId];
                if (objective.SiegeEpochs == 1)
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational, WorldEventType.SiegeBegun,
                        new[] { war.AttackerId, war.DefenderId }, port.Hex,
                        Magnitude: 1.0, Valence: -0.6, EventVisibility.Public,
                        new SiegeBegunPayload(war.Id, war.Name, port.Id,
                            state.Actors[war.AttackerId].Name,
                            state.Actors[war.DefenderId].Name)));
                if (objective.SiegeEpochs >= SiegeThreshold(state, war, port))
                    Capture(state, war, objective, port);
                break;
            }
            case WarObjectiveType.BlockadeLane:
                if (outcome != BattleOutcome.DecisiveDefender
                    && attPower > defPower)
                {
                    objective.SiegeEpochs++;   // epochs the lane stayed cut
                    if (objective.SiegeEpochs >= knobs.BlockadeHoldEpochs)
                        objective.Status = ObjectiveStatus.Taken;
                }
                else objective.SiegeEpochs = 0;
                break;
            case WarObjectiveType.DestroyFleet:
                if (war.DefenderStrengthAtStart > 0
                    && WarOps.SideStrength(state, war, attacker: false)
                       <= knobs.FleetDestroyedShare
                          * war.DefenderStrengthAtStart)
                    objective.Status = ObjectiveStatus.Taken;
                break;
        }
    }

    /// <summary>How long the port can hold: a floor, plus what its larders
    /// carry (local market provisions plus the polity reserve's pro-rata
    /// share against its population's hunger), plus the fortress tiers.</summary>
    public static int SiegeThreshold(SimState state, War war, Port port)
    {
        var knobs = state.Config.War;
        var defender = state.PolityOf(war.DefenderId);
        int ownPorts = 0;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == war.DefenderId) ownPorts++;
        double provisions =
            state.Markets[port.Id].Inventory[(int)GoodId.Provisions]
            + defender.ReserveQty[(int)GoodId.Provisions] / Math.Max(1, ownPorts);
        double population = 0;
        foreach (var s in state.Segments)
            if (s.PortId == port.Id) population += s.Size;
        double hungerPerEpoch = Math.Max(0.1,
            population * state.Config.Economy.SubsistenceUnitsPerPopPerYear
            * state.Config.Sim.YearsPerEpoch);
        int larder = (int)Math.Min(knobs.SiegeProvisionEpochsCap,
                                   provisions / hungerPerEpoch);
        return knobs.SiegeBaseEpochs + larder
               + FortressTiers(state, war.DefenderId, port.Hex);
    }

    /// <summary>A fallen port transfers its domain whole: sovereignty,
    /// facilities, and the population segments intact — conquest
    /// composition is automatic (war.md §Conduct 3).</summary>
    private static void Capture(SimState state, War war,
                                WarObjective objective, Port port)
    {
        foreach (var facility in state.Facilities)            // id order (P6)
            if (facility.OwnerActorId == war.DefenderId
                && MarketEngine.AttachedMarketIndex(state, facility) == port.Id)
                facility.OwnerActorId = war.AttackerId;
        // defender fleets stationed there scatter home (the capital)
        int fallback = CapitalPort(state, war.DefenderId);
        foreach (var fleet in state.Fleets)                   // id order (P6)
            if (fleet.OwnerActorId == war.DefenderId
                && fleet.HomePortId == port.Id && fallback >= 0
                && fallback != port.Id)
            {
                fleet.HomePortId = fallback;
                fleet.Hex = state.Ports[fallback].Hex;
            }
        port.OwnerActorId = war.AttackerId;
        objective.Status = ObjectiveStatus.Taken;
        objective.SiegeEpochs = 0;
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PortCaptured,
            new[] { war.AttackerId, war.DefenderId }, port.Hex,
            Magnitude: 1.0, Valence: -0.8, EventVisibility.Public,
            new PortCapturedPayload(war.Id, war.Name, port.Id,
                state.Actors[war.AttackerId].Name,
                state.Actors[war.DefenderId].Name)));
    }

    // ---- the modifiers ----

    /// <summary>Extended lines degrade: power falls with distance from the
    /// supply base, floored — an army at the end of its tether still fights.</summary>
    private static double SupplyFactor(SimState state, FleetRecord fleet,
                                       HexCoordinate hex, WarKnobs knobs)
    {
        if (fleet.HomePortId < 0 || fleet.HomePortId >= state.Ports.Count)
            return 0.7;
        int dist = HexGrid.Distance(state.Ports[fleet.HomePortId].Hex, hex);
        return Math.Max(0.5, 1.0 - knobs.SupplyPenaltyPerHex * dist);
    }

    private static double CompetenceFactor(SimState state, int commanderId)
    {
        if (commanderId < 0 || commanderId >= state.Characters.Count)
            return 1.0;
        var commander = state.Characters[commanderId];
        return commander.Alive ? 0.8 + 0.4 * commander.Competence : 1.0;
    }

    private static double AllyStrength(SimState state, War war, bool attacker)
    {
        double strength = 0;
        foreach (var ally in attacker ? war.AttackerAllies : war.DefenderAllies)
            strength += FleetOps.WarStrength(state, ally);
        return strength;
    }

    /// <summary>Defender combat weight physically at the objective (fleets
    /// homed or standing there), and its senior commander.</summary>
    private static double LocalDefense(SimState state, War war,
                                       HexCoordinate hex, out int commanderId)
    {
        double local = 0;
        commanderId = -1;
        double bestRenown = -1;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != war.DefenderId
                || fleet.TotalHulls == 0) continue;
            bool here = fleet.Hex.Equals(hex)
                || (fleet.HomePortId >= 0 && fleet.HomePortId < state.Ports.Count
                    && state.Ports[fleet.HomePortId].Hex.Equals(hex));
            if (!here) continue;
            var v = FleetOps.Vectors(state, fleet);
            local += (v.Strike + v.Sustained + 0.5 * v.Screening)
                     * fleet.Readiness;
            if (fleet.CommanderId >= 0)
            {
                double renown = state.Characters[fleet.CommanderId].Renown;
                if (renown > bestRenown)
                { bestRenown = renown; commanderId = fleet.CommanderId; }
            }
        }
        return local;
    }

    private static int FortressTiers(SimState state, int ownerId,
                                     HexCoordinate hex)
    {
        int tiers = 0;
        foreach (var f in state.Facilities)                   // id order (P6)
            if (f.OwnerActorId == ownerId
                && f.TypeId == (int)InfraTypeId.Fortress
                && f.Hex.Equals(hex) && f.Condition > 0.2)
                tiers += f.Tier;
        return tiers;
    }

    private static double FortressBonus(SimState state, int ownerId,
                                        HexCoordinate hex, WarKnobs knobs) =>
        knobs.FortressDefensePerTier * FortressTiers(state, ownerId, hex);

    private static void DamageFacilities(SimState state, int ownerId,
                                         HexCoordinate hex, double damage)
    {
        foreach (var f in state.Facilities)                   // id order (P6)
            if (f.OwnerActorId == ownerId && f.Hex.Equals(hex))
                f.Condition = Math.Max(0.05, f.Condition - damage);
    }

    // ---- losses ----

    /// <summary>Wreck hulls out of one fleet at a battle hex, quietly (the
    /// battle event carries the loss; no attrition event).</summary>
    private static int WreckAt(SimState state, FleetRecord fleet,
                               HexCoordinate hex, int count)
    {
        fleet.Hex = hex;   // the war fleet stands at its objective
        return FleetOps.Wreck(state, fleet, count, quiet: true);
    }

    /// <summary>Defender losses come off the warships that fought: fleets
    /// at the hex first, then the mobile response drawn from home stations.</summary>
    private static int WreckDefendersAt(SimState state, War war,
                                        HexCoordinate hex, double share)
    {
        int lost = 0;
        foreach (bool localPass in new[] { true, false })
            foreach (var fleet in state.Fleets)               // id order (P6)
            {
                if (fleet.OwnerActorId != war.DefenderId
                    || fleet.TotalHulls == 0) continue;
                bool here = fleet.Hex.Equals(hex)
                    || (fleet.HomePortId >= 0
                        && fleet.HomePortId < state.Ports.Count
                        && state.Ports[fleet.HomePortId].Hex.Equals(hex));
                if (here != localPass) continue;
                int warships = 0;
                foreach (var g in fleet.Hulls)
                    if (ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                        warships += g.Count;
                double effectiveShare = localPass ? share
                    : share * state.Config.War.MobileResponseShare;
                int toLose = (int)Math.Round(warships * effectiveShare);
                if (toLose <= 0) continue;
                lost += WreckWarships(state, fleet, hex, toLose);
            }
        return lost;
    }

    /// <summary>Wreck only warship hulls (the freight marine survives the
    /// day), at the battle hex.</summary>
    private static int WreckWarships(SimState state, FleetRecord fleet,
                                     HexCoordinate hex, int count)
    {
        var corp = state.CorporationOf(fleet.OwnerActorId);
        var pr = corp == null ? state.PolityOf(fleet.OwnerActorId) : null;
        int wrecked = 0;
        // backwards: RemoveHulls drops emptied groups without skipping any
        for (int i = fleet.Hulls.Count - 1; i >= 0 && count > 0; i--)
        {
            var g = fleet.Hulls[i];
            if (!ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                continue;
            int loss = Math.Min(g.Count, count);
            fleet.RemoveHulls(g.DesignId, loss);
            state.Wreckage.Add(new WreckageRecord(state.Wreckage.Count,
                hex, g.DesignId, loss, state.WorldYear));
            if (corp != null) corp.HullsWrecked += loss;
            else pr!.HullsWrecked += loss;
            wrecked += loss;
            count -= loss;
        }
        return wrecked;
    }

    // ---- commanders ----

    /// <summary>The polity's living marshal, or null — the nerve behind the
    /// mobilization share.</summary>
    private static Character? MarshalOf(SimState state, int polityId)
    {
        foreach (var c in state.Characters)                   // id order (P6)
            if (c.Alive && c.PolityId == polityId
                && c.Role == CharacterRole.Marshal) return c;
        return null;
    }

    private static string CommanderName(SimState state, int commanderId) =>
        commanderId >= 0 && commanderId < state.Characters.Count
            ? state.Characters[commanderId].Name : "";

    /// <summary>War is a hazard for commanders (characters.md): a rout can
    /// kill the beaten admiral; a decisive day makes the victor's renown —
    /// and, past the cap's judgment, a war hero.</summary>
    private static void CommanderFates(SimState state, War war,
        int attackerCommander, int defenderCommander, BattleOutcome outcome,
        HexCoordinate hex)
    {
        var knobs = state.Config.War;
        int victor = -1, beaten = -1, beatenPolity = -1, victorPolity = -1;
        if (outcome == BattleOutcome.DecisiveAttacker)
        {
            victor = attackerCommander;
            victorPolity = war.AttackerId;
            beaten = defenderCommander;
            beatenPolity = war.DefenderId;
        }
        else if (outcome == BattleOutcome.DecisiveDefender)
        {
            victor = defenderCommander;
            victorPolity = war.DefenderId;
            beaten = attackerCommander;
            beatenPolity = war.AttackerId;
        }
        if (beaten >= 0 && state.Characters[beaten].Alive
            && EpochRolls.NextDouble(state.Config.MasterSeed,
                RollChannel.CommanderFate, state.EpochIndex, beaten)
               < knobs.CommanderDeathOnRout)
        {
            var fallen = state.Characters[beaten];
            fallen.Alive = false;
            fallen.DeathYear = state.WorldYear;
            // the flag passes: no fleet keeps a dead commander's name
            if (fallen.InstitutionId >= 0
                && fallen.InstitutionId < state.Fleets.Count
                && state.Fleets[fallen.InstitutionId].CommanderId == fallen.Id)
                state.Fleets[fallen.InstitutionId].CommanderId = -1;
            fallen.InstitutionId = -1;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.CharacterDied,
                new[] { beatenPolity }, hex, Magnitude: 1.0, Valence: -0.8,
                EventVisibility.Public,
                new CharacterDiedPayload(fallen.Id, fallen.Name,
                    (int)CharacterRole.Commander,
                    state.WorldYear - fallen.BirthYear)));
        }
        if (victor >= 0 && state.Characters[victor].Alive)
        {
            var hero = state.Characters[victor];
            hero.Renown += knobs.RenownPerVictory;
            if (hero.Notable == NotableType.None
                && hero.Renown >= knobs.WarHeroRenown
                && CharacterOps.NotableCount(state, victorPolity)
                   < state.Config.Character.MaxNotablesPerPolity)
            {
                hero.Notable = NotableType.WarHero;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.NotableEmerged,
                    new[] { victorPolity }, hex, Magnitude: 1.0, Valence: 0.6,
                    EventVisibility.Public,
                    new NotableEmergedPayload(hero.Id, hero.Name,
                        (int)NotableType.WarHero)));
            }
        }
    }

    // ---- shared hull bookkeeping ----

    private static void Blend(List<HullGroup> pool, int designId, int count,
                              double grade)
    {
        foreach (var g in pool)
            if (g.DesignId == designId)
            {
                g.Grade = (g.Count * g.Grade + count * grade) / (g.Count + count);
                g.Count += count;
                return;
            }
        pool.Add(new HullGroup(designId, count, grade));
    }

    private static void DealHulls(List<HullGroup> pool, FleetRecord fleet,
                                  int count)
    {
        foreach (var g in pool)
        {
            if (count <= 0) return;
            int take = Math.Min(g.Count, count);
            if (take <= 0) continue;
            fleet.AddHulls(g.DesignId, take, g.Grade);
            g.Count -= take;
            count -= take;
        }
    }

    /// <summary>A side's warship hull count — the exhaustion denominator.</summary>
    private static int SideWarshipHulls(SimState state, War war, bool attacker)
    {
        int leader = attacker ? war.AttackerId : war.DefenderId;
        int hulls = 0;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != leader) continue;
            foreach (var g in fleet.Hulls)
                if (ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                    hulls += g.Count;
        }
        return hulls;
    }
}
