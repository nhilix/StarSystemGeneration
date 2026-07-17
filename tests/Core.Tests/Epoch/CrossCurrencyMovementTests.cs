using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 8 — the full cross-currency movement audit. Every
/// place money moves between two actors whose currencies can differ must route
/// through <see cref="SimState.ConvertCurrency"/> + <see cref="SimState.
/// RecordConversion"/>, not a raw 1:1 add/subtract — otherwise the per-currency
/// conservation residual leaks (the leak class the ConservationTests, un-skipped
/// by this task, catch across the full history). These are the focused unit
/// checks for the individual sites the audit converted; the holistic acceptance
/// bar is <see cref="ConservationTests"/> across the committed sweep.</summary>
public class CrossCurrencyMovementTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static void AddCurrency(SimState state, int id, double rate)
    {
        state.Currencies.Add(new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate });
        state.Banks.Add(new Bank(id));
    }

    // an Actor + PolityRecord pair (some ops — TransferPort's CapitalPort —
    // resolve a polity's seat through the Actors registry)
    private static PolityRecord AddPolity(SimState state, int id, int currencyId)
    {
        state.Actors.Add(new Actor(id, ActorKind.Polity, $"P{id}",
            new HexCoordinate(id, id), entryYear: 0,
            new GenesisController(state.Config)) { Entered = true });
        var pr = new PolityRecord(id, 0) { CurrencyId = currencyId };
        state.Polities.Add(pr);
        return pr;
    }

    private static Port AddPort(SimState state, int ownerActorId)
    {
        var port = new Port(state.Ports.Count, ownerActorId,
            new HexCoordinate(0, 0), tier: 1, foundedYear: 0);
        state.Ports.Add(port);
        return port;
    }

    private static PopulationSegment AddSegment(SimState state, int portId, double wealth)
    {
        var seg = new PopulationSegment(state.Segments.Count, portId, 0, 0, 10.0)
        { Wealth = wealth };
        state.Segments.Add(seg);
        return seg;
    }

    // ---- port-ownership change: every port-resolved holder converts ----

    [Fact]
    public void MergeInto_ConvertsResidentSegmentWealthAndOrderEscrow_IntoSurvivor()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // absorbed
        AddCurrency(state, 1, 2.0);   // survivor (weaker → 1 cur0 = 0.5 cur1)
        state.Polities.Add(new PolityRecord(0, 0) { CurrencyId = 0 });
        state.Polities.Add(new PolityRecord(1, 0) { CurrencyId = 1 });
        var port = AddPort(state, ownerActorId: 0);
        var seg = AddSegment(state, port.Id, wealth: 100.0);
        // a resting buy order escrowed in the port's (cur0) market
        state.Orders.Add(new MarketOrder(state.NextOrderId++, OrderSide.Buy,
            ownerActorId: 0, port.Id, good: 0, limitPrice: 5.0, qtyRemaining: 8,
            grade: 0, escrowCredits: 40.0, postedYear: 0, expiryYear: 1000));

        FederationOps.MergeInto(state, fromId: 0, intoId: 1);

        Assert.Equal(1, port.OwnerActorId);
        // both port-resolved holders re-denominate at 1.0/2.0 = 0.5×, not 1:1
        Assert.Equal(100.0 * 0.5, seg.Wealth, 9);
        Assert.Equal(40.0 * 0.5, state.Orders[0].EscrowCredits, 9);
        // the transfers are booked so the per-currency residual nets out
        Assert.Equal(100.0 + 40.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(50.0 + 20.0, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void MergeInto_ConvertsInvestmentPools_IntoSurvivorCurrency()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        state.Polities.Add(new PolityRecord(0, 0)
        {
            CurrencyId = 0,
            DevelopmentPoints = 100.0,
            MilitaryPoints = 40.0,
        });
        state.Polities.Add(new PolityRecord(1, 0) { CurrencyId = 1 });
        AddPort(state, ownerActorId: 0);   // a port so the merge has domain

        FederationOps.MergeInto(state, 0, 1);

        var into = state.PolityOf(1);
        Assert.Equal(0.0, state.PolityOf(0).DevelopmentPoints, 9);
        Assert.Equal(0.0, state.PolityOf(0).MilitaryPoints, 9);
        // pools are money in the absorbed currency too — they convert, not carry 1:1
        Assert.Equal(100.0 * 0.5, into.DevelopmentPoints, 9);
        Assert.Equal(40.0 * 0.5, into.MilitaryPoints, 9);
        Assert.Equal(140.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(70.0, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void TransferPort_ConvertsCapturedPortResidentWealth_AtTheCaptureSeam()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // defender (old owner)
        AddCurrency(state, 1, 4.0);   // conqueror (much weaker → 0.25×)
        AddPolity(state, 0, currencyId: 0);
        AddPolity(state, 1, currencyId: 1);
        var port = AddPort(state, ownerActorId: 0);
        var seg = AddSegment(state, port.Id, wealth: 200.0);

        WarConduct.TransferPort(state, port.Id, newOwnerActorId: 1);

        Assert.Equal(1, port.OwnerActorId);
        Assert.Equal(200.0 * 1.0 / 4.0, seg.Wealth, 9);   // 50, not a raw 200
        Assert.Equal(200.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(50.0, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    // Construction wages that cross the funder→build-port currency boundary
    // (a foreign-owned build port, or a GatePair end in another polity) and
    // cross-polity migration are integration paths exercised end-to-end by the
    // per-currency ConservationTests across the committed acceptance sweep — the
    // two confirmed leaks this task closed. They have no clean seam to isolate
    // here (private Feed/Migrate over full market/demographic state), so the
    // full-history residual is their guard.

    // ---- a cancelled colony expedition's purse recycles, never vanishes ----

    [Fact]
    public void CancelColonyExpedition_RefundsThePurse_ToTheFunderPool()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        var funder = new PolityRecord(0, 0) { CurrencyId = 0, ExpansionPoints = 0.0 };
        state.Polities.Add(funder);
        var port = AddPort(state, ownerActorId: 0);
        var expedition = new Project(0, ProjectKind.ColonyExpedition,
            ownerActorId: 0, funderActorId: 0, portId: port.Id,
            hex: new HexCoordinate(0, 0), yearsRequired: 5, startedYear: 0);
        state.Projects.Add(expedition);
        Assert.True(expedition.InFlight);

        ProjectOps.Cancel(state, expedition);

        Assert.False(expedition.InFlight);   // cancelled
        // the in-flight purse (ColonyCost, counted by SupplyOps in the funder's
        // currency) returns to the funder's pool instead of leaking a real sink
        Assert.Equal(state.Config.Expansion.ColonyCost, funder.ExpansionPoints, 9);
    }
}
