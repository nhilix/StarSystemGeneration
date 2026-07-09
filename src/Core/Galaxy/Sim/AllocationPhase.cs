using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

/// <summary>Epoch phase 2 (economy spec §3/§5/§6): wealth income, four-way
/// temperament-weighted budget split (war-overridden), stockpile grow/decay,
/// development spending under the tech ceiling, exotics → tech-tier ladder.</summary>
public static class AllocationPhase
{
    private const double DevIncomePerTier = 0.35;
    // 5.5 (not the plan's 1.5): budgets don't carry across epochs, so the base must
    // let a dev budget afford a tier raise at reference temperaments; tuned in shape-band task.
    private const double DevIncomeBase = 5.5;
    private const double UpkeepPerWar = 0.5;

    /// <summary>Returns the expansion budget per polity id for ActionPhase.</summary>
    public static Dictionary<int, double> Run(GalaxySkeleton s, int epoch)
    {
        var expansionBudgets = new Dictionary<int, double>();
        foreach (var polity in s.Polities)
        {
            if (polity.Extinct) { expansionBudgets[polity.Id] = 0; continue; }
            var species = s.Species[polity.SpeciesId];
            var owned = EpochSim.Owned(s, polity);

            int devSum = 0;
            if (owned.Count > 0)
            {
                foreach (var c in owned) devSum += c.DevelopmentTier;
                polity.Wealth += DevIncomeBase
                    + DevIncomePerTier * devSum * (1.0 + 0.1 * polity.TechTier);
            }

            int liveWars = CountLiveWars(s, polity.Id);
            bool atWar = liveWars > 0;
            double upkeep = UpkeepPerWar * liveWars;
            double paid = Math.Min(polity.Wealth, upkeep);
            polity.Wealth -= paid;
            bool upkeepUnpaid = paid < upkeep - 1e-9;

            // Stockpile: decays always; steeper when upkeep unpaid or ore-starved.
            double decay = s.Config.StockpileDecayRate
                * ((upkeepUnpaid || polity.OreBalance < 0) ? 2.0 : 1.0);

            if (owned.Count == 0)
            {
                polity.MilitaryStockpile = Math.Max(0, polity.MilitaryStockpile * (1.0 - decay));
                expansionBudgets[polity.Id] = 0;
                continue;
            }

            double wExp = species.Expansionism;
            double wDev = species.Industry;
            double wMil = species.Militancy * (atWar ? 2.0 : 1.0);
            double wSum = wExp + wDev + wMil;
            double pool = polity.Wealth;
            double expBudget = pool * wExp / wSum;
            double devBudget = pool * wDev / wSum;
            double milBudget = pool * wMil / wSum;
            polity.Wealth = 0;

            polity.MilitaryStockpile =
                Math.Max(0, polity.MilitaryStockpile * (1.0 - decay)) + milBudget;

            // Development: cheapest-first (tier, then spiral), stalled by ore deficit.
            if (polity.OreBalance >= 0)
            {
                int ceiling = Economy.DevCeiling(polity.TechTier);
                while (true)
                {
                    RegionCell? cheapest = null;
                    foreach (var c in owned)
                        if (c.DevelopmentTier < ceiling
                            && (cheapest == null
                                || c.DevelopmentTier < cheapest.DevelopmentTier
                                || (c.DevelopmentTier == cheapest.DevelopmentTier
                                    && c.SpiralIndex < cheapest.SpiralIndex)))
                            cheapest = c;
                    if (cheapest == null) break;
                    double cost = 1.0 + cheapest.DevelopmentTier;
                    if (devBudget < cost) break;
                    devBudget -= cost;
                    cheapest.DevelopmentTier++;
                }
            }

            // Tech: exotics surplus invests, Industry-scaled; cumulative geometric ladder.
            if (polity.ExoticsBalance > 0)
            {
                polity.ExoticsInvested += polity.ExoticsBalance * (0.5 + species.Industry);
                while (polity.ExoticsInvested >= Economy.TechThreshold(s.Config, polity.TechTier))
                {
                    polity.TechTier++;
                    var cap = s.CellAt(polity.CapitalCoord);
                    s.Events.Add(new GalaxyEvent
                    {
                        Epoch = epoch, Type = GalaxyEventType.TechAdvance,
                        ActorPolityId = polity.Id, Q = cap.Q, R = cap.R,
                        Detail = polity.TechTier,
                    });
                }
            }

            expansionBudgets[polity.Id] = expBudget;
        }
        return expansionBudgets;
    }

    private static int CountLiveWars(GalaxySkeleton s, int polityId)
    {
        int n = 0;
        foreach (var w in s.Wars)
            if (!w.Ended && (w.AttackerId == polityId || w.DefenderId == polityId)) n++;
        return n;
    }
}
