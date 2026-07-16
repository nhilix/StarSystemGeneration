using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 6: genesis &amp; lifecycle — the task that turns the
/// currency system on. Every polity is founded with its own brand-new
/// <see cref="Currency"/> (entry, graduation splits, federation fusion); the
/// merge plumbing force-converts an absorbed balance and its reissued loans into
/// the survivor's currency; and every polity-death path retires the dead
/// polity's currency.</summary>
public class GenesisLifecycleCurrencyTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    private static SimState Run(ulong seed = 42, int epochs = 24)
    {
        var state = EpochTestKit.Seeded(seed, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    // ---- genesis: every living polity mints its own currency ----

    [Fact]
    public void Entry_MintsEveryLivingPolityItsOwnCurrency()
    {
        var state = Run();
        int living = 0;
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered
                || state.Actors[pr.ActorId].Retired) continue;
            living++;
            // a real, resolvable currency (never the -1 pre-genesis sentinel)
            Assert.True(pr.CurrencyId >= 0,
                "a living polity must have a real currency after genesis");
            var cur = state.CurrencyOf(pr.CurrencyId);   // resolves, never throws
            // each polity mints its OWN currency at its own founding — a clean
            // 1:1 invariant that holds through splits and fusions alike
            Assert.Equal(pr.ActorId, cur.FoundingPolityId);
            Assert.False(cur.Retired, "a living polity's currency is not retired");
        }
        Assert.True(living > 0, "the history produced no living polities");
        // the registry is at least one currency per living polity (retired
        // parents leave their records behind too, so it can be larger)
        Assert.True(state.Currencies.Count >= living);
    }

    // ---- MergeInto: the absorbed balance force-converts into the survivor ----

    [Fact]
    public void MergeInto_ForceConvertsAbsorbedBalance_IntoSurvivorCurrency()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // the absorbed polity's currency
        AddCurrency(state, 1, 2.0);   // the survivor's currency
        var from = new PolityRecord(0, 0) { CurrencyId = 0, Credits = 1000.0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1, Credits = 500.0 };
        state.Polities.Add(from);
        state.Polities.Add(into);

        FederationOps.MergeInto(state, fromId: 0, intoId: 1);

        // 1000 of cur0 lands as 1000 * 1.0/2.0 = 500 of cur1, gross of the
        // skim — into.Deposit (slice CU-2) skims the spread off the top into
        // Bank(cur1).Reserve before crediting the net.
        double landed = 1000.0 * 1.0 / 2.0;
        double spread = state.Config.Economy.ConversionSpread;
        Assert.Equal(0.0, from.Credits, 9);
        Assert.Equal(500.0 + landed * (1 - spread), into.Credits, 9);
        Assert.Equal(1000.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(landed, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void MergeInto_InsolventAbsorbed_HandsTheDebtOver_Converted()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        // an insolvent absorbed polity (negative balance): the debt must move,
        // never be swallowed by a non-positive guard (Deposit has none)
        var from = new PolityRecord(0, 0) { CurrencyId = 0, Credits = -400.0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1, Credits = 1000.0 };
        state.Polities.Add(from);
        state.Polities.Add(into);

        FederationOps.MergeInto(state, 0, 1);

        // -200 of cur1, gross — into.Deposit (slice CU-2) still skims the
        // spread off the top into Bank(cur1).Reserve, signed here since the
        // absorbed balance itself is negative.
        double landed = -400.0 * 1.0 / 2.0;   // -200 of cur1
        double spread = state.Config.Economy.ConversionSpread;
        Assert.Equal(0.0, from.Credits, 9);
        Assert.Equal(1000.0 + landed * (1 - spread), into.Credits, 9);
    }

    // ---- MergeInto: reissued loans reprice only when the LENDER changed ----

    [Fact]
    public void MergeInto_ReissuedLoan_RepricesPrincipal_WhenAbsorbedWasLender()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // absorbed lender's currency
        AddCurrency(state, 1, 2.0);   // survivor's currency
        AddCurrency(state, 2, 1.0);   // an unrelated borrower's currency
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        var third = new PolityRecord(2, 0) { CurrencyId = 2 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        state.Polities.Add(third);
        // absorbed polity 0 lends 800 (in cur0) to the third polity
        state.Loans.Add(new Loan(0, lenderActorId: 0, borrowerActorId: 2,
            principal: 800.0, ratePerYear: 0.05, termYears: 10, issuedYear: 0));

        FederationOps.MergeInto(state, 0, 1);

        Assert.True(state.Loans[0].Closed, "the original loan retires");
        var reissued = state.Loans[state.Loans.Count - 1];
        Assert.False(reissued.Closed);
        Assert.Equal(1, reissued.LenderActorId);     // survivor is the new lender
        Assert.Equal(2, reissued.BorrowerActorId);
        // the loan now denominates in the survivor's currency: 800 of cur0 →
        // 800 * 1.0/2.0 = 400 of cur1 (a cross-currency loan is lender-denominated)
        Assert.Equal(800.0 * 1.0 / 2.0, reissued.Principal, 9);
    }

    [Fact]
    public void MergeInto_ReissuedLoan_KeepsPrincipal_WhenOnlyBorrowerChanged()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // absorbed borrower's currency
        AddCurrency(state, 1, 2.0);   // survivor's currency
        AddCurrency(state, 2, 4.0);   // the unchanged lender's currency
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        var lender = new PolityRecord(2, 0) { CurrencyId = 2 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        state.Polities.Add(lender);
        // polity 2 lends 800 (in cur2) to the absorbed polity 0
        state.Loans.Add(new Loan(0, lenderActorId: 2, borrowerActorId: 0,
            principal: 800.0, ratePerYear: 0.05, termYears: 10, issuedYear: 0));

        FederationOps.MergeInto(state, 0, 1);

        Assert.True(state.Loans[0].Closed);
        var reissued = state.Loans[state.Loans.Count - 1];
        Assert.Equal(2, reissued.LenderActorId);     // lender unchanged
        Assert.Equal(1, reissued.BorrowerActorId);   // survivor is the new borrower
        // the lender did not change, so the loan stays lender-denominated: the
        // principal carries over untouched (FX risk stays with the borrower)
        Assert.Equal(800.0, reissued.Principal, 9);
    }

    // ---- death paths: the dead polity's currency retires ----

    [Fact]
    public void Absorption_RetiresTheVassalsCurrency_AndConvertsItsBalance()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        rel.VassalSinceYear = state.WorldYear
            - state.Config.Relations.VassalAbsorptionEpochs
              * state.Config.Sim.GenerationYears;
        rel.Warmth = 0.9;
        state.PolityOf(overlord).Interior!.Cohesion = 0.7;

        int curV = state.PolityOf(vassal).CurrencyId;
        int curO = state.PolityOf(overlord).CurrencyId;
        Assert.True(curV >= 0 && curO >= 0 && curV != curO);
        // make the forced conversion observable and isolate its booking
        state.CurrencyOf(curV).NumeraireRate = 2.0;
        state.CurrencyOf(curO).NumeraireRate = 1.0;
        state.CurrencyOf(curO).CumulativeConvertedIn = 0;
        double vassalCredits = state.PolityOf(vassal).Credits;
        double overlordBefore = state.PolityOf(overlord).Credits;

        var (absorbed, _) = FederationOps.VassalExits(state);

        Assert.Equal(1, absorbed);
        // the absorbed currency retires; the survivor's does not
        Assert.True(state.CurrencyOf(curV).Retired);
        Assert.False(state.CurrencyOf(curO).Retired);
        // the vassal's balance left whole and landed converted (2.0 → 1.0 = 2×) —
        // only the treasury Credits lands in the survivor's Credits (its pools land
        // in the survivor's pools, resident segment wealth stays segment wealth),
        // so this balance check is exact.
        double landed = vassalCredits * 2.0 / 1.0;
        // the treasury leg routes through into.Deposit inside MergeInto
        // (slice CU-2), which skims the spread off the top into
        // Bank(curO).Reserve before crediting the net; the pool legs below
        // stay raw ConvertCurrency (no Deposit, no skim).
        double spread = state.Config.Economy.ConversionSpread;
        Assert.Equal(0.0, state.PolityOf(vassal).Credits, 6);
        Assert.Equal(overlordBefore + landed * (1 - spread),
            state.PolityOf(overlord).Credits, 6);
        // the balance conversion is booked among the survivor's converted-in
        // total. Task 8 broadened absorption to ALSO force-convert the vassal's
        // investment pools, resident segment wealth, and any resting order/courier
        // escrow into the survivor's currency (currency-and-FX "Data model"), so
        // this aggregate now exceeds the treasury leg alone — the precise per-leg
        // Credits booking is pinned by MergeInto_ForceConvertsAbsorbedBalance.
        Assert.True(state.CurrencyOf(curO).CumulativeConvertedIn >= landed - 1e-6,
            "the balance conversion must be booked among the absorption transfers");
    }

    [Fact]
    public void Federation_RetiresBothParents_IntoOneBrandNewCurrency()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int a = rel.PolityAId, b = rel.PolityBId;
        int curA = state.PolityOf(a).CurrencyId;
        int curB = state.PolityOf(b).CurrencyId;
        Assert.True(curA >= 0 && curB >= 0 && curA != curB);
        int currenciesBefore = state.Currencies.Count;

        int newId = FederationOps.Federate(state, rel);

        int curYoung = state.PolityOf(newId).CurrencyId;
        // the union mints exactly one BRAND-NEW currency, distinct from both
        Assert.True(curYoung >= 0);
        Assert.NotEqual(curA, curYoung);
        Assert.NotEqual(curB, curYoung);
        Assert.Equal(currenciesBefore + 1, state.Currencies.Count);
        Assert.Equal(newId, state.CurrencyOf(curYoung).FoundingPolityId);
        // BOTH parents' currencies retire (two retirements into one new currency)
        Assert.True(state.CurrencyOf(curA).Retired);
        Assert.True(state.CurrencyOf(curB).Retired);
        Assert.False(state.CurrencyOf(curYoung).Retired);
    }

    // ---- graduation: a splinter mints a real child currency; the seed
    //      transfer exercises the REAL cross-currency path end-to-end ----

    [Fact]
    public void Graduation_Splinter_MintsChildCurrency_AndSeedsAcrossIt()
    {
        var state = Run();
        // find a living polity that owns >= 2 ports, one of them populated
        PolityRecord? old = null;
        int seatPort = -1;
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered
                || state.Actors[pr.ActorId].Retired) continue;
            var owned = new List<int>();
            foreach (var port in state.Ports)
                if (port.OwnerActorId == pr.ActorId) owned.Add(port.Id);
            if (owned.Count < 2) continue;
            int populated = -1;
            foreach (int pid in owned)
                foreach (var s in state.Segments)
                    if (s.PortId == pid && s.Size > 0) { populated = pid; break; }
            if (populated >= 0) { old = pr; seatPort = populated; break; }
        }
        Assert.NotNull(old);   // the seeded history must offer a multi-port polity

        int oldCur = old!.CurrencyId;
        old.Credits = 1000.0;                       // a known, solvent treasury
        state.CurrencyOf(oldCur).NumeraireRate = 2.0;
        // isolate the treasury-seed conversion so `transfer` reads ONLY it: task 8
        // also force-converts the parent's pool share AND the seceding port's
        // resident segment wealth and resting escrow into the child's currency
        // (each precisely covered by its own unit test), which would otherwise
        // inflate the parent's converted-out total below. Zero those here.
        old.ExpansionPoints = old.DevelopmentPoints
            = old.MilitaryPoints = old.ReservePoints = 0;
        foreach (var s in state.Segments)
            if (s.PortId == seatPort) s.Wealth = 0;
        foreach (var o in state.Orders)
            if (o.PortId == seatPort) o.EscrowCredits = 0;
        double outBefore = state.CurrencyOf(oldCur).CumulativeConvertedOut;
        int currenciesBefore = state.Currencies.Count;
        var seceding = new HashSet<int> { seatPort };

        var young = GraduationOps.FoundSplinter(state, old, seceding,
            "TestSplinter", militancy: 0.5);

        // the splinter minted a real, distinct, fresh currency of its own
        int youngCur = young.CurrencyId;
        Assert.True(youngCur >= 0);
        Assert.NotEqual(oldCur, youngCur);
        Assert.Equal(currenciesBefore + 1, state.Currencies.Count);
        Assert.Equal(young.ActorId, state.CurrencyOf(youngCur).FoundingPolityId);
        Assert.Equal(1.0, state.CurrencyOf(youngCur).NumeraireRate, 9);  // fresh

        // SeedTreasury ran the REAL cross-currency conversion (not a raw split):
        // the transfer (in the parent's currency) is what left the parent's
        // supply, and it landed in the child converted at 2.0 → 1.0 = 2×.
        double transfer =
            state.CurrencyOf(oldCur).CumulativeConvertedOut - outBefore;
        Assert.True(transfer > 0, "the seed transfer must cross currencies");
        // young.Deposit inside SeedTreasury (slice CU-2) skims the spread off
        // the top into Bank(youngCur).Reserve before crediting the net — the
        // paired counter still books the full gross conversion.
        double landed = transfer * 2.0;
        double spread = state.Config.Economy.ConversionSpread;
        Assert.Equal(landed * (1 - spread), young.Credits, 6);
        Assert.Equal(landed,
            state.CurrencyOf(youngCur).CumulativeConvertedIn, 6);
    }
}
