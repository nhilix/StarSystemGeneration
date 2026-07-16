using System.Linq;
using System.Reflection;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 4c: the three remaining direct cross-currency sites.
/// Foreign fleet upkeep is a PAYMENT (gross up the payer, keep the pool
/// non-negative with (1+spread) headroom, local Bank keeps the skim); migration
/// and corporate-dissolution residue both ARRIVE into the recipients' own port
/// currency (reduce-recipient — the segment / home-port recipients bank NET, the
/// destination Bank keeps the skim).</summary>
public class BankDirectSiteSkimTests
{
    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    // polity 0 (currency 0, rate 1) and polity 1 (currency 1, rate 2), both entered
    private static SimState Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        state.Actors[0].Entered = true;
        state.Actors[1].Entered = true;
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        state.PolityOf(0).CurrencyId = 0;
        state.PolityOf(1).CurrencyId = 1;
        state.WorldYear = 100;
        return state;
    }

    private static double Spread(SimState state) =>
        state.Config.Economy.ConversionSpread;

    // ---- (1) foreign fleet upkeep: gross up the payer, pool stays non-negative ----

    [Fact]
    public void FleetUpkeep_ForeignPort_GrossesUpPolity_ReserveKeepsSkim_PoolNonNegative()
    {
        var state = Fixture();
        var pr = state.PolityOf(0);                     // payer, currency 0
        pr.MilitaryPoints = 100_000.0;
        // a FOREIGN victualling port (owned by polity 1, currency 1) with a market
        // and a local Fuel ask the upkeep lift can cross
        state.Ports.Add(new Port(0, ownerActorId: 1, state.Actors[1].Seat,
            tier: 2, foundedYear: 0));
        var market = new Market(0, state.Config.Economy);
        state.Markets.Add(market);
        OrderOps.PostSell(state, 1, 0, (int)GoodId.Fuel, qty: 100.0, grade: 0.5,
            ask: 3.0, expiryYear: 10000);
        double before = pr.MilitaryPoints;

        double need = 5.0;
        var drawUpkeep = typeof(FleetOps).GetMethod("DrawUpkeep",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        drawUpkeep.Invoke(null, new object[] { state, pr, market,
            (int)GoodId.Fuel, need });

        double cost = need * 3.0;                        // 15, currency 1
        double skim = cost * Spread(state);
        double ownCostGross = (cost + skim) * 2.0;       // C1 -> C0 at 2/1

        // the polity bore the grossed cost; the pool never went negative (headroom)
        Assert.Equal(before - ownCostGross, pr.MilitaryPoints, 4);
        Assert.True(pr.MilitaryPoints > 0);
        // the foreign (local) Bank keeps the skim; the payer's own does not
        Assert.Equal(skim, state.BankOf(1).Reserve, 6);
        Assert.Equal(0.0, state.BankOf(0).Reserve, 6);
        Assert.Equal(ownCostGross, state.CurrencyOf(0).CumulativeConvertedOut, 4);
        Assert.Equal(cost + skim, state.CurrencyOf(1).CumulativeConvertedIn, 4);
    }

    // ---- (2) migration: the destination segment banks NET ----

    [Fact]
    public void Migration_CrossCurrency_DestinationSegmentBanksNet_ReserveKeepsSkim()
    {
        var state = Fixture();
        // port 0 (polity 0, currency 0) — the source; port 1 (polity 1, currency 1)
        // — an empty, more-attractive (open-land = 1.0) destination on a lane
        state.Ports.Add(new Port(0, ownerActorId: 0, state.Actors[0].Seat,
            tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, ownerActorId: 1, state.Actors[1].Seat,
            tier: 2, foundedYear: 0));
        EpochTestKit.AddLane(state, 0, 1);
        var src = new PopulationSegment(0, 0, state.PolityOf(0).SpeciesId,
            state.PolityOf(0).SpeciesId, 100.0)
        { Wealth = 1000.0, LastSubsistence = 0.5, SoL = 0.5 };
        state.Segments.Add(src);

        var migrate = typeof(InteriorPhase).GetMethod("Migrate",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        migrate.Invoke(null, new object[] { state, 1 });   // preexisting = 1

        double wealthShare = 1000.0 - src.Wealth;          // what actually left C0
        Assert.True(wealthShare > 0, "no migration occurred");
        double moved = wealthShare * 1.0 / 2.0;            // C0 -> C1 at 1/2
        double skim = moved * Spread(state);

        var home = state.Segments.First(s => s.PortId == 1);
        // the destination segment banks the NET; the destination Bank keeps the skim
        Assert.Equal(moved - skim, home.Wealth, 6);
        Assert.Equal(skim, state.BankOf(1).Reserve, 6);
        Assert.Equal(0.0, state.BankOf(0).Reserve, 6);
        Assert.Equal(wealthShare, state.CurrencyOf(0).CumulativeConvertedOut, 6);
        Assert.Equal(moved, state.CurrencyOf(1).CumulativeConvertedIn, 6);
    }

    // ---- (3) dissolution residue: DrainWalletTo settles recipients NET ----

    [Fact]
    public void DissolutionResidue_DrainWalletTo_SettlesNet_ReserveKeepsSkim()
    {
        var state = Fixture();
        // home port (currency 0) so the residue settles in C0
        var corp = new Corporation(0, actorId: 100, "Deadco", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);
        corp.Deposit(state, 100.0, 0);   // 100 C0 (home currency)
        corp.Deposit(state, 50.0, 1);    //  50 C1, worth 100 C0

        var drain = typeof(CorporationOps).GetMethod("DrainWalletTo",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        double total = (double)drain.Invoke(null, new object[] { state, corp, 0 })!;

        // the C0 bucket drains at par (same currency, no skim); the C1 bucket
        // converts to 100 C0 and skims into the home Bank, so the residue total is
        // the NET the recipients actually receive
        double skim = 100.0 * Spread(state);
        Assert.Equal(100.0 + (100.0 - skim), total, 6);   // 199.5
        Assert.Equal(skim, state.BankOf(0).Reserve, 6);
        Assert.Empty(corp.Holdings);
        Assert.Equal(50.0, state.CurrencyOf(1).CumulativeConvertedOut, 6);
        Assert.Equal(100.0, state.CurrencyOf(0).CumulativeConvertedIn, 6);
    }
}
