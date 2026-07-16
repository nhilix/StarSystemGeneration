using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 5: bilateral transfer sites — tribute,
/// reparations, a graduation's treasury seed, and a courier's fee escrow —
/// all convert via <see cref="SimState.ConvertCurrency"/>/<c>Deposit</c>/
/// <c>Withdraw</c> at the point money crosses currencies, and leave a
/// same-currency refund alone (no spurious conversion).</summary>
public class BilateralTransferCurrencyTests
{
    private const int G = (int)GoodId.Alloys;

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        return cur;
    }

    /// <summary>Point a live polity's already-minted currency (task 6 mints one
    /// per polity at entry) at a test rate and clear the conversion counters the
    /// history accrued, so a single transfer's booking is measured in isolation.
    /// Rides the real currency rather than shadowing it with a duplicate id.</summary>
    private static void SetRate(SimState state, int polityId, double rate)
    {
        var cur = Cur(state, polityId);
        cur.NumeraireRate = rate;
        cur.CumulativeConvertedIn = 0;
        cur.CumulativeConvertedOut = 0;
    }

    private static Currency Cur(SimState state, int polityId)
        => state.CurrencyOf(state.PolityOf(polityId).CurrencyId);

    // ---- FederationOps.PayTribute ----

    private static (SimState State, int Vassal, int Overlord) VassalFixture(
        double vassalRate, double overlordRate)
    {
        var state = EpochTestKit.Seeded(7, 10).State;
        state.Config.Sim.EpochCount = 24;
        new EpochEngine().Run(state);
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        // genesis (task 6) already minted each polity its own currency at entry;
        // ride those real currencies — set the rates under test and zero the
        // counters the run accrued so the assertions see only this transfer.
        SetRate(state, vassal, vassalRate);
        SetRate(state, overlord, overlordRate);
        // silence any other bound pair a history could carry
        foreach (var p in state.Polities)
            if (p.ActorId != vassal) p.Receipts = 0;
        state.PolityOf(vassal).Receipts = 100;
        return (state, vassal, overlord);
    }

    [Fact]
    public void PayTribute_MatchedRates_LandsOneToOne()
    {
        // two distinct currencies at the SAME numeraire rate: still a
        // cross-currency transfer (booked on the paired counters), just one
        // that happens to land the full amount unchanged
        var (state, vassal, overlord) = VassalFixture(vassalRate: 1.0, overlordRate: 1.0);
        var vr = state.PolityOf(vassal);
        var or = state.PolityOf(overlord);
        double vc = vr.Credits, oc = or.Credits;

        int paid = FederationOps.PayTribute(state);

        Assert.True(paid >= 1);
        double share = state.Config.Relations.VassalTributeShare;
        double tribute = 100 * share;
        Assert.Equal(vc - tribute, vr.Credits, 9);
        Assert.Equal(oc + tribute, or.Credits, 9);
        Assert.Equal(tribute, Cur(state, vassal).CumulativeConvertedOut, 9);
        Assert.Equal(tribute, Cur(state, overlord).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void PayTribute_CrossCurrency_ConvertsIntoOverlordsCurrency()
    {
        var (state, vassal, overlord) = VassalFixture(vassalRate: 1.0, overlordRate: 2.0);
        var vr = state.PolityOf(vassal);
        var or = state.PolityOf(overlord);
        double vc = vr.Credits, oc = or.Credits;

        int paid = FederationOps.PayTribute(state);

        Assert.True(paid >= 1);
        double share = state.Config.Relations.VassalTributeShare;
        double tribute = 100 * share;              // in the VASSAL's own currency
        double landed = tribute * 1.0 / 2.0;        // cur(vassal) -> cur(overlord)
        Assert.Equal(vc - tribute, vr.Credits, 9);  // the vassal debits its own currency whole
        Assert.Equal(oc + landed, or.Credits, 9);   // the overlord banks the converted sum
        Assert.Equal(tribute, Cur(state, vassal).CumulativeConvertedOut, 9);
        Assert.Equal(landed, Cur(state, overlord).CumulativeConvertedIn, 9);
    }

    // ---- WarResolution reparations ----

    private static War Declare(SimState state, int attacker, int defender,
                               WarDemand demand, params WarObjectiveSpec[] specs)
        => WarOps.DeclareWar(state, new DeclareWarAct(attacker, defender,
            (int)CasusBelli.BorderIncident, -1, specs, (int)demand))!;

    [Fact]
    public void Reparations_CrossCurrency_ConvertIntoVictorsCurrency()
    {
        var state = EpochTestKit.Seeded(7, 10).State;
        state.Config.Sim.EpochCount = 24;
        new EpochEngine().Run(state);
        var rel = EpochTestKit.FirstLiveRelation(state);
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.Reparations);
        war.Objectives[0].Status = ObjectiveStatus.Taken;
        var loser = state.PolityOf(rel.PolityBId);
        var winner = state.PolityOf(rel.PolityAId);
        loser.Credits = System.Math.Max(100.0, loser.Credits);
        SetRate(state, loser.ActorId, 1.0);
        SetRate(state, winner.ActorId, 4.0);
        double loserBefore = loser.Credits, winnerBefore = winner.Credits;

        WarResolution.Terminate(state, null);

        Assert.False(war.Active);
        double share = state.Config.War.ReparationsShare;
        double reparations = loserBefore * share;
        double landed = reparations * 1.0 / 4.0;    // cur(loser) -> cur(winner)
        Assert.Equal(loserBefore - reparations, loser.Credits, 6);
        Assert.Equal(winnerBefore + landed, winner.Credits, 6);
        Assert.Equal(reparations,
            Cur(state, loser.ActorId).CumulativeConvertedOut, 6);
        Assert.Equal(landed,
            Cur(state, winner.ActorId).CumulativeConvertedIn, 6);
    }

    // ---- GraduationOps.SeedTreasury ----

    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    [Fact]
    public void SeedTreasury_ConvertsAtTheFoundingRate()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // the parent's currency
        AddCurrency(state, 1, 4.0);   // the child's brand-new currency
        var old = new PolityRecord(0, 0) { CurrencyId = 0, Credits = 1000.0 };
        var young = new PolityRecord(1, 0) { CurrencyId = 1, Credits = 0.0 };

        GraduationOps.SeedTreasury(state, old, young, share: 0.3);

        double transfer = 1000.0 * 0.3;             // 300, in the parent's currency
        double landed = transfer * 1.0 / 4.0;        // cur0 -> cur1 = 75
        Assert.Equal(1000.0 - transfer, old.Credits, 9);
        Assert.Equal(landed, young.Credits, 9);
        Assert.Equal(transfer, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(landed, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void SeedTreasury_InsolventParent_TransfersTheDebtShare_NoLeak()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 1.0);
        // an insolvent parent (existing "goes negative, no cap" convention)
        var old = new PolityRecord(0, 0) { CurrencyId = 0, Credits = -1000.0 };
        var young = new PolityRecord(1, 0) { CurrencyId = 1, Credits = 500.0 };
        double totalBefore = old.Credits + young.Credits;

        GraduationOps.SeedTreasury(state, old, young, share: 0.4);

        // a negative transfer must still move — Withdraw's non-positive
        // guard must never swallow it (that would leave the debt
        // uncharged on the parent while still crediting the child)
        double transfer = -1000.0 * 0.4;   // -400
        Assert.Equal(-1000.0 - transfer, old.Credits, 9);   // -600
        Assert.Equal(500.0 + transfer, young.Credits, 9);   // 100
        Assert.Equal(totalBefore, old.Credits + young.Credits, 9);   // conserved
    }

    [Fact]
    public void SeedTreasury_SameCurrency_ByteIdenticalToRawSplit()
    {
        var state = NewState();
        var old = new PolityRecord(0, 0) { CurrencyId = -1, Credits = 1000.0 };
        var young = new PolityRecord(1, 0) { CurrencyId = -1, Credits = 0.0 };

        GraduationOps.SeedTreasury(state, old, young, share: 0.25);

        Assert.Equal(750.0, old.Credits, 9);
        Assert.Equal(250.0, young.Credits, 9);
    }

    // ---- CourierOps fee escrow ----

    private static (SimState State, Port A, Port B) CourierFixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        AddCurrency(state, 0, 1.0);     // the poster's currency (port owner)
        AddCurrency(state, 1, 2.0);     // the fulfiller's currency
        state.PolityOf(0).CurrencyId = 0;
        state.PolityOf(1).CurrencyId = 1;
        state.WorldYear = 100;
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        return (state, pa, pb);
    }

    [Fact]
    public void CourierFee_DeliveredAcrossCurrencies_SettlesConverted()
    {
        var (state, pa, pb) = CourierFixture();
        pa.DepositStock(G, 40, 0.7);
        var poster = state.PolityOf(0);
        poster.Credits = 100;
        var fulfiller = state.PolityOf(1);
        double fulfillerBefore = fulfiller.Credits;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;

        // the fee escrowed in the poster's own currency (0), no conversion yet
        Assert.Equal(90.0, poster.Credits, 6);

        bool ok = CourierOps.Accept(state, c, fulfillerActorId: 1);
        Assert.True(ok);
        for (int i = 0; i < 5; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));

        Assert.Equal(CourierStatus.Delivered, c.Status);
        double landed = 10.0 * 1.0 / 2.0;   // cur0 -> cur1
        Assert.Equal(fulfillerBefore + landed, fulfiller.Credits, 6);
        Assert.Equal(10.0, state.CurrencyOf(0).CumulativeConvertedOut, 6);
        Assert.Equal(landed, state.CurrencyOf(1).CumulativeConvertedIn, 6);
    }

    [Fact]
    public void CourierFee_Expired_RefundsThePoster_NoSpuriousConversion()
    {
        var (state, pa, _) = CourierFixture();
        pa.DepositStock(G, 40, 0.7);
        var poster = state.PolityOf(0);
        poster.Credits = 100;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;
        state.WorldYear = c.ExpiryYear + 1;

        CourierOps.ExpireOpen(state);

        // the fee returns to the SAME actor in the SAME currency it left —
        // no conversion (it never crossed a currency boundary)
        Assert.Equal(100.0, poster.Credits, 6);
        Assert.Equal(0.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(0.0, state.CurrencyOf(0).CumulativeConvertedIn, 9);
        Assert.Equal(0.0, state.CurrencyOf(1).CumulativeConvertedOut, 9);
        Assert.Equal(0.0, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }
}
