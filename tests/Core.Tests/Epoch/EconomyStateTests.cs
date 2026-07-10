using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D structural tests: the economy state types — markets,
/// cultures, deepened segments, polity ledgers, loans — and the new knob
/// families. Behavior (the market step) is tested per phase; this file pins
/// the shapes and defaults.</summary>
public class EconomyStateTests
{
    private static readonly EpochSimConfig Cfg = new EpochSimConfig();

    [Fact]
    public void Market_CarriesPerGoodStateForAllSeventeenGoods()
    {
        var m = new Market(portId: 3, Cfg.Economy);
        Assert.Equal(3, m.PortId);
        int n = Goods.All.Count;
        Assert.Equal(n, m.Price.Length);
        Assert.Equal(n, m.Inventory.Length);
        Assert.Equal(n, m.InventoryGrade.Length);
        Assert.Equal(n, m.LastCleared.Length);
        Assert.Equal(n, m.BlackBookDemand.Length);
        Assert.Equal(n, m.BlackBookPrice.Length);
    }

    [Fact]
    public void Market_InitialPricesFollowGoodTier()
    {
        var m = new Market(0, Cfg.Economy);
        Assert.Equal(Cfg.Economy.BasePriceRaw, m.Price[(int)GoodId.Ore]);
        Assert.Equal(Cfg.Economy.BasePriceProcessed, m.Price[(int)GoodId.Alloys]);
        Assert.Equal(Cfg.Economy.BasePriceCapital, m.Price[(int)GoodId.Machinery]);
        Assert.True(Cfg.Economy.BasePriceRaw < Cfg.Economy.BasePriceProcessed);
        Assert.True(Cfg.Economy.BasePriceProcessed < Cfg.Economy.BasePriceCapital);
    }

    [Fact]
    public void SimState_CarriesTheNewRegistries()
    {
        var state = EpochTestKit.Seeded().State;
        Assert.NotNull(state.Markets);
        Assert.NotNull(state.Cultures);
        Assert.NotNull(state.Loans);
    }

    [Fact]
    public void Genesis_SeedsOneCulturePerSpecies_IdsAligned()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        Assert.Equal(skeleton.Species.Count, state.Cultures.Count);
        for (int i = 0; i < state.Cultures.Count; i++)
        {
            Assert.Equal(i, state.Cultures[i].Id);
            Assert.Equal(i, state.Cultures[i].SpeciesId);
            Assert.Equal(skeleton.Species[i].Name, state.Cultures[i].Name);
        }
    }

    [Fact]
    public void EveryPort_HasItsMarket_AfterARun()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        Assert.True(state.Ports.Count > 0);
        Assert.Equal(state.Ports.Count, state.Markets.Count);
        for (int i = 0; i < state.Ports.Count; i++)
            Assert.Equal(state.Ports[i].Id, state.Markets[i].PortId);
    }

    [Fact]
    public void Segment_CarriesTheTwoIdentityLayersAndLivingState()
    {
        var seg = new PopulationSegment(id: 0, portId: 1, speciesId: 2,
                                        cultureId: 2, size: 3.0);
        Assert.Equal(2, seg.CultureId);
        Assert.Equal(0.5, seg.SoL);
        Assert.Equal(0.0, seg.Wealth);
        Assert.Equal(4, seg.Ideology.Length);          // the four axes
        foreach (var axis in seg.Ideology)
            Assert.Equal(0.5, axis);                   // neutral until drift
    }

    [Fact]
    public void PolityRecord_CarriesCreditsAndReserves()
    {
        var pr = new PolityRecord(actorId: 0, speciesId: 1);
        Assert.Equal(0.0, pr.Credits);
        Assert.Equal(Goods.All.Count, pr.ReserveQty.Length);
        Assert.Equal(Goods.All.Count, pr.ReserveGrade.Length);
    }

    [Fact]
    public void Loan_RecordsTheDesignTuple()
    {
        var loan = new Loan(id: 0, lenderActorId: 1, borrowerActorId: 2,
                            principal: 100.0, ratePerYear: 0.02,
                            termYears: 50, issuedYear: 250);
        Assert.Equal(1, loan.LenderActorId);
        Assert.Equal(2, loan.BorrowerActorId);
        Assert.Equal(100.0, loan.Principal);
        Assert.Equal(0.02, loan.RatePerYear);
        Assert.Equal(50, loan.TermYears);
        Assert.Equal(250, loan.IssuedYear);
    }

    [Fact]
    public void NewEventTypes_LiveInTheEconomicBlock()
    {
        Assert.Equal(202, (int)WorldEventType.FamineStruck);
        Assert.Equal(203, (int)WorldEventType.FacilityBuilt);
        Assert.Equal(204, (int)WorldEventType.LoanIssued);
        Assert.Equal(205, (int)WorldEventType.LoanDefaulted);
        Assert.Equal(EventFamily.Economic,
                     WorldEventTypes.FamilyOf(WorldEventType.FamineStruck));
        Assert.Equal(EventFamily.Economic,
                     WorldEventTypes.FamilyOf(WorldEventType.LoanDefaulted));
    }

    [Fact]
    public void EconomyKnobs_HaveSaneMarketDefaults()
    {
        var eco = Cfg.Economy;
        Assert.True(eco.PriceDriftMaxPerYear > 0 && eco.PriceDriftMaxPerYear < 1);
        Assert.True(eco.LaborShare > 0 && eco.LaborShare < 1);
        Assert.True(eco.InitialCreditsPerPolity > 0);
        Assert.True(eco.SubsistenceUnitsPerPopPerYear > 0);
        Assert.True(eco.SoLUnitsPerPopPerYear > 0);
        Assert.True(eco.LuxuryUnitsPerPopPerYear > 0);
        Assert.True(eco.ReExportWeight > 0);
        Assert.True(eco.FreightCostPerUnitPerHex > 0);
        Assert.True(eco.FuelPerUnitPerHex > 0);
        Assert.True(eco.LoanRatePerYear > 0);
        Assert.True(eco.LoanTermYears > 0);
        Assert.True(eco.ConditionDecayPerYear > 0);
        Assert.True(eco.ConditionRecoveryPerYear > 0);
        // the era-standard entry tier must allow standard capital recipes
        // (slice G: the config stub retired for per-polity tech)
        Assert.True(Tech.EraStandardTier >= 2);
    }

    [Fact]
    public void PopulationKnobs_HaveSaneDefaults()
    {
        var pop = Cfg.Population;
        Assert.True(pop.MigrationRatePerYear > 0);
        Assert.True(pop.IdeologyDriftPerYear > 0);
        Assert.True(pop.FamineShrinkPerYear > 0);
        Assert.True(pop.SoLDriftPerYear > 0);
    }
}
