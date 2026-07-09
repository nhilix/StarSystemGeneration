using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 4 (economy spec §6): active wars contest their fronts,
/// weariness accrues, termination resolves victory / white peace; capital
/// relocation and extinction absorbed from the stage-1 loop.</summary>
public static class ResolutionPhase
{
    private const double CommitFraction = 0.5;
    private const double AttritionRate = 0.3;
    private const double StockpileBreakFloor = 0.1;

    public static void Run(GalaxySkeleton s, int epoch)
    {
        foreach (var war in s.Wars)
        {
            if (war.Ended) continue;
            var attacker = s.Polities[war.AttackerId];
            var defender = s.Polities[war.DefenderId];
            var aSpecies = s.Species[attacker.SpeciesId];
            var dSpecies = s.Species[defender.SpeciesId];

            // Extinct polities fight no fronts (deferred-tickets spec §4): a war whose
            // side died earlier (lower-id war this epoch, or any prior epoch) goes
            // straight to termination.
            if (!attacker.Extinct && !defender.Extinct)
            {
                double aCommit = CommitFraction * attacker.MilitaryStockpile / Math.Max(1, LiveWarCount(s, attacker.Id));
                double dCommit = CommitFraction * defender.MilitaryStockpile / Math.Max(1, LiveWarCount(s, defender.Id));
                double aStrength = Economy.WarStrength(aCommit, attacker.TechTier, aSpecies.Militancy);
                double dStrength = Economy.WarStrength(dCommit, defender.TechTier, dSpecies.Militancy);

                int aLostThisEpoch = 0, dLostThisEpoch = 0;
                foreach (var coord in FrontInOrder(s, war))
                {
                    var cell = s.CellAt(coord);
                    cell.Contested = true;
                    cell.WarScarred = true;
                    bool attackerHolds = cell.OwnerPolityId == war.AttackerId;
                    double holderStrength = attackerHolds ? aStrength : dStrength;
                    double takerStrength = attackerHolds ? dStrength : aStrength;
                    double pTake = 0.5 * Clamp(
                        0.5 + 0.5 * (takerStrength - holderStrength)
                                   / (takerStrength + holderStrength + 1.0), 0.05, 0.95);
                    var ctx = new RollContext(s.Config.MasterSeed, cell.Coord);
                    if (ctx.NextDouble(RollChannel.SimBattle, epoch, war.Id) < pTake)
                    {
                        int newOwner = attackerHolds ? war.DefenderId : war.AttackerId;
                        int oldOwner = cell.OwnerPolityId;
                        cell.OwnerPolityId = newOwner;
                        if (oldOwner == war.AttackerId) { aLostThisEpoch++; war.AttackerCellsLost++; }
                        else { dLostThisEpoch++; war.DefenderCellsLost++; }
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = newOwner, TargetPolityId = oldOwner,
                            Q = cell.Q, R = cell.R,
                            Magnitude = Math.Abs(takerStrength - holderStrength),
                        });
                        HandleCapitalAndExtinction(s, epoch, s.Polities[oldOwner], s.Polities[newOwner], cell);
                    }
                }

                attacker.MilitaryStockpile = Math.Max(0, attacker.MilitaryStockpile - aCommit * AttritionRate);
                defender.MilitaryStockpile = Math.Max(0, defender.MilitaryStockpile - dCommit * AttritionRate);

                war.AttackerWeariness += Weariness(s, attacker, aLostThisEpoch);
                war.DefenderWeariness += Weariness(s, defender, dLostThisEpoch);
            }

            // Termination (deferred-tickets spec §4): an extinct side loses outright;
            // otherwise the weariness/stockpile break logic decides.
            WarOutcome outcome;
            if (attacker.Extinct && defender.Extinct) outcome = WarOutcome.WhitePeace;
            else if (defender.Extinct) outcome = WarOutcome.AttackerVictory;
            else if (attacker.Extinct) outcome = WarOutcome.DefenderVictory;
            else
            {
                bool aBroke = Broke(war.AttackerWeariness, aSpecies, attacker);
                bool dBroke = Broke(war.DefenderWeariness, dSpecies, defender);
                if (!aBroke && !dBroke) continue;
                outcome = aBroke && dBroke ? WarOutcome.WhitePeace
                    : aBroke ? WarOutcome.DefenderVictory : WarOutcome.AttackerVictory;
            }

            if (outcome == WarOutcome.AttackerVictory)
                foreach (var gc in war.GoalCells)
                {
                    var cell = s.CellAt(gc);
                    if (cell.OwnerPolityId == war.DefenderId)
                    {
                        cell.OwnerPolityId = war.AttackerId;
                        war.DefenderCellsLost++;
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = war.AttackerId, TargetPolityId = war.DefenderId,
                            Q = cell.Q, R = cell.R, Magnitude = 0,
                        });
                        HandleCapitalAndExtinction(s, epoch, defender, attacker, cell);
                    }
                }
            else if (outcome == WarOutcome.DefenderVictory)
                // Settlement (deferred-tickets spec §4): captures return — front cells
                // are by construction originally the defender's. Also cleans zombie
                // cells captured by an attacker that later went extinct.
                foreach (var fc in war.FrontCells)
                {
                    var cell = s.CellAt(fc);
                    if (cell.OwnerPolityId == war.AttackerId)
                    {
                        cell.OwnerPolityId = war.DefenderId;
                        war.AttackerCellsLost++;
                        s.Events.Add(new GalaxyEvent
                        {
                            Epoch = epoch, Type = GalaxyEventType.CellTaken,
                            ActorPolityId = war.DefenderId, TargetPolityId = war.AttackerId,
                            Q = cell.Q, R = cell.R, Magnitude = 0,
                        });
                        HandleCapitalAndExtinction(s, epoch, attacker, defender, cell);
                    }
                }
            // WhitePeace: uti possidetis — you keep what you hold.

            foreach (var fc in war.FrontCells) s.CellAt(fc).Contested = false;
            war.Ended = true;
            war.Outcome = outcome;
            var origin = s.CellAt(war.GoalCells[0]);
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.WarEnded,
                ActorPolityId = war.AttackerId, TargetPolityId = war.DefenderId,
                Q = origin.Q, R = origin.R, Detail = (int)outcome,
                Magnitude = war.AttackerWeariness + war.DefenderWeariness,
            });
        }
    }

    /// <summary>The front equals the goal cluster for the war's entire life (cells
    /// may flip back and forth between belligerents within it, but the set itself
    /// never grows); returned ordered by spiral index for determinism.</summary>
    private static List<HexCoordinate> FrontInOrder(GalaxySkeleton s, War war)
    {
        var cells = new List<RegionCell>();
        foreach (var coord in war.FrontCells) cells.Add(s.CellAt(coord));
        cells.Sort((x, y) => x.SpiralIndex.CompareTo(y.SpiralIndex));
        var result = new List<HexCoordinate>();
        foreach (var c in cells) result.Add(c.Coord);
        return result;
    }

    private static double Weariness(GalaxySkeleton s, Polity p, int cellsLostThisEpoch)
    {
        // Hardship: commodity deficits or blockade strain above the shared floor
        // (deferred-tickets spec §3) — blockading an enemy hastens their breaking.
        bool shortages = p.ProvisionsBalance < 0 || p.OreBalance < 0
            || p.BlockadeLoss > Economy.TradeBlockedFloor;
        return s.Config.WarWearinessRate
            * (shortages ? 1.5 : 1.0) * (1.0 + 0.2 * cellsLostThisEpoch);
    }

    private static bool Broke(double weariness, SpeciesProfile species, Polity p) =>
        weariness >= 0.5 + species.Cohesion || p.MilitaryStockpile < StockpileBreakFloor;

    private static int LiveWarCount(GalaxySkeleton s, int polityId)
    {
        int n = 0;
        foreach (var w in s.Wars)
            if (!w.Ended && (w.AttackerId == polityId || w.DefenderId == polityId)) n++;
        return n;
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>Stage-1 capital-relocation and extinction logic, absorbed (spec §6).</summary>
    private static void HandleCapitalAndExtinction(GalaxySkeleton s, int epoch,
        Polity loser, Polity victor, RegionCell takenCell)
    {
        if (loser.CapitalCoord.Equals(takenCell.Coord))
        {
            RegionCell? best = null;
            foreach (var c in EpochSim.Owned(s, loser))
                if (best == null || c.DevelopmentTier > best.DevelopmentTier
                    || (c.DevelopmentTier == best.DevelopmentTier && c.SpiralIndex < best.SpiralIndex))
                    best = c;
            if (best != null)
            {
                loser.CapitalQ = best.Q;
                loser.CapitalR = best.R;
                s.Events.Add(new GalaxyEvent
                {
                    Epoch = epoch, Type = GalaxyEventType.LostCapital,
                    ActorPolityId = victor.Id, TargetPolityId = loser.Id,
                    Q = takenCell.Q, R = takenCell.R,
                });
            }
        }
        if (EpochSim.Owned(s, loser).Count == 0 && !loser.Extinct)
        {
            loser.Extinct = true;
            s.Events.Add(new GalaxyEvent
            {
                Epoch = epoch, Type = GalaxyEventType.PolityExtinct,
                ActorPolityId = victor.Id, TargetPolityId = loser.Id,
                Q = takenCell.Q, R = takenCell.R,
            });
        }
    }
}
