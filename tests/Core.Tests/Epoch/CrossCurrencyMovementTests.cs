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

    // ---- outpost graduation re-attaches residents across a currency seam ----

    [Fact]
    public void OutpostGraduation_ConvertsResidentWealth_WhenParentPortChangedCurrency()
    {
        // the graduation seam (domain-hex-expansion §4): CompleteGraduation
        // re-attaches an outpost's residents from the parent port to the newly
        // born tier-1 port. Usually same-currency (the graduating polity's own
        // ports), so a no-op. But if the parent port changed hands during the
        // promotion's multi-year duration (conquest/federation/secession), the
        // residents' Wealth crosses a currency boundary at re-attach: SupplyOps
        // buckets segment wealth by its port-owner currency, so a BARE PortId
        // swap silently re-denominates it 1:1 and leaks the whole segment's
        // wealth per-currency (the DX-slice sweep leak). It must convert-and-
        // record, the ConvertPortHoldings ownership-seam discipline.
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // the graduating polity (born port)
        AddCurrency(state, 1, 4.0);   // the parent's NEW owner (seized it)
        AddPolity(state, 0, currencyId: 0);   // graduator / born-port owner
        AddPolity(state, 1, currencyId: 1);   // took the parent mid-promotion
        state.Actors[1].Entered = false;      // keep the encroachment bump inert

        // the parent port now belongs to polity 1 (currency 1) — it changed
        // hands after the outpost was founded; its administering currency is 1.
        var parent = AddPort(state, ownerActorId: 1);
        var hex = new HexCoordinate(3, 0);
        var outpost = new Outpost(state.Outposts.Count, "Fringe", hex,
                                  parent.Id, 0L);
        state.Outposts.Add(outpost);
        // a resident administered by the (now foreign) parent, at the outpost hex
        var seg = new PopulationSegment(state.Segments.Count, parent.Id, 0, 0, 10.0)
        { Hex = hex, Wealth = 200.0 };
        state.Segments.Add(seg);

        // the promotion project the graduating polity (0) funded before the
        // parent was seized — its born port is owned by polity 0 (currency 0).
        var proj = new Project(state.Projects.Count, ProjectKind.OutpostGraduation,
            ownerActorId: 0, funderActorId: 0, portId: parent.Id, hex: hex,
            yearsRequired: 5, startedYear: 0)
        { TargetId = outpost.Id };
        state.Projects.Add(proj);

        double outBefore = state.CurrencyOf(1).CumulativeConvertedOut;
        double inBefore = state.CurrencyOf(0).CumulativeConvertedIn;

        ProjectOps.Complete(state, proj, completionYear: 100);

        var born = state.Ports[^1];
        Assert.Equal(0, born.OwnerActorId);            // born under the graduator
        Assert.Equal(born.Id, seg.PortId);             // resident re-attached
        Assert.True(state.Outposts[outpost.Id].Graduated);
        // 200 of cur1 → cur0 at the frozen rate (200 * 4/1 = 800), NOT a raw 200
        double converted = state.ConvertCurrency(200.0, 1, 0);
        Assert.Equal(converted, seg.Wealth, 9);
        // the transfer is booked so the per-currency residual nets out: cur1
        // sheds the 200 it lost, cur0 books the converted sum it gained.
        Assert.Equal(outBefore + 200.0,
                     state.CurrencyOf(1).CumulativeConvertedOut, 9);
        Assert.Equal(inBefore + converted,
                     state.CurrencyOf(0).CumulativeConvertedIn, 9);
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
