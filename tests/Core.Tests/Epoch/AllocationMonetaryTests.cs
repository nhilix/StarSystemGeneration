using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice ME task 3 — the monetary-equilibrium core mechanism
/// (2026-07-13-monetary-equilibrium-design.md §1/§2/§3/§5): the allocation
/// base reads receipts (not the stock), the Operations share stays in the
/// treasury as a margin, idle pools recycle into Credits, and a bounded
/// sovereign mint covers the end-of-epoch shortfall without ever touching
/// loan service.</summary>
public class AllocationMonetaryTests
{
    /// <summary>A single entered polity with one port and one segment — the
    /// minimal AllocationPhase fixture, mirroring AllocationEconomyTests.</summary>
    private static SimState Fixture(double credits = 0, double receipts = 0)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, species, species, 3.0));
        var pr = state.PolityOf(actor.Id);
        pr.Credits = credits;
        pr.Receipts = receipts;
        state.WorldYear = 100;
        return state;
    }

    // §1 — the allocation base reads receipts, not the stock: a treasury far
    // richer than its income is no longer swept into pools every epoch.
    [Fact]
    public void Allocation_BudgetsReceipts_NotTheEntireStock()
    {
        var state = Fixture(credits: 10000, receipts: 100);
        var pr = state.PolityOf(0);

        new AllocationPhase().Run(state);

        // the old base (max(Credits, Receipts)) swept ~70% of the 10000 stock
        // into pools; the receipts-only base moves at most the 100 of income,
        // so the historical stock survives nearly intact
        Assert.True(pr.Credits > 9000,
            $"the balance was swept ({pr.Credits:0}); the base should read receipts");
    }

    // §2 — the Operations share is a margin: its slice of allocatable is never
    // subtracted from Credits, so the four subtracted shares sum to exactly
    // (Expansion + Development + Military + Reserves), leaving Operations liquid.
    [Fact]
    public void Operations_StaysInTheTreasury_AsMargin()
    {
        var state = Fixture(credits: 1000, receipts: 1000);
        // isolate the subtraction: no pool decay, no research/appeasement spend,
        // and no steady mint widening the base (Part B has its own tests)
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
        state.Config.Economy.SteadyIssuanceRate = 0.0;
        var pr = state.PolityOf(0);
        var b = PolityPolicies.Default.Budget;

        new AllocationPhase().Run(state);

        // Credits fell by exactly the four moved shares — Operations, Research
        // (no sellers → 0 spent) and Appeasement (no factions → 0) never left
        double movedToPools = 1000.0 * (b.Expansion + b.Development + b.Military + b.Reserves);
        Assert.Equal(1000.0 - movedToPools, pr.Credits, 6);
        // the Operations slice is demonstrably still liquid, not in any pool
        Assert.True(pr.Credits >= 1000.0 * b.Operations - 1e-9);
    }

    // §3 — idle-pool decay: a bounded fraction of leftover Expansion/
    // Development/Military points recirculates into Credits, conserved;
    // ReservePoints is excluded.
    [Fact]
    public void DecayIdlePools_RecirculatesPointsToCredits_Conserved()
    {
        var state = Fixture(credits: 0, receipts: 0);   // no new allocation
        var pr = state.PolityOf(0);
        pr.ExpansionPoints = 100;
        pr.DevelopmentPoints = 100;
        pr.MilitaryPoints = 100;
        pr.ReservePoints = 100;

        new AllocationPhase().Run(state);

        // the three idle pools shrank; reserves are untouched (their own decay
        // dynamic funds physical stockpiles, not idle cash)
        Assert.True(pr.ExpansionPoints < 100);
        Assert.True(pr.DevelopmentPoints < 100);
        Assert.True(pr.MilitaryPoints < 100);
        Assert.Equal(100.0, pr.ReservePoints, 9);
        // conservation: every point that left a pool arrived in Credits
        double remaining = pr.ExpansionPoints + pr.DevelopmentPoints + pr.MilitaryPoints;
        Assert.Equal(300.0 - remaining, pr.Credits, 6);
    }

    // §3 (P7) — decay compounds per world-year: a 25-year step recirculates
    // exactly what twenty-five 1-year steps would.
    [Fact]
    public void DecayIdlePools_IsTickInvariant()
    {
        var coarse = Fixture();
        coarse.PolityOf(0).ExpansionPoints = 100;
        new AllocationPhase().Run(coarse);

        var fine = Fixture();
        fine.Config.Sim.YearsPerEpoch = 1;
        fine.PolityOf(0).ExpansionPoints = 100;
        for (int i = 0; i < 25; i++) new AllocationPhase().Run(fine);

        Assert.Equal(coarse.PolityOf(0).ExpansionPoints,
                     fine.PolityOf(0).ExpansionPoints, 6);
    }

    // §5 — sovereign issuance never fires when the treasury is solvent.
    [Fact]
    public void IssueSovereignCredit_DoesNotFire_WhenCreditsNonNegative()
    {
        var state = Fixture(credits: 500, receipts: 0);
        new AllocationPhase().Run(state);
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);
    }

    // §5 — a negative treasury mints exactly Min(shortfall, rate*Receipts):
    // here the shortfall dwarfs the cap, so issuance equals rate*Receipts, and
    // CumulativeFiatIssued accumulates across epochs.
    [Fact]
    public void IssueSovereignCredit_MintsCapBound_AndAccumulates()
    {
        var state = Fixture(credits: -100000, receipts: 100);
        double rate = state.Config.Economy.SovereignIssuanceRate;
        double cap = rate * 100;

        new AllocationPhase().Run(state);
        Assert.Equal(cap, state.CumulativeFiatIssued, 6);

        // second epoch, still deeply underwater: the running total grows by
        // another capped mint — the level is never reset
        state.PolityOf(0).Receipts = 100;
        new AllocationPhase().Run(state);
        Assert.Equal(2 * cap, state.CumulativeFiatIssued, 6);
    }

    // §5 — issuance is bounded by the cap even when the shortfall is smaller:
    // Min(shortfall, cap) resolves to the shortfall term.
    [Fact]
    public void IssueSovereignCredit_MintsShortfallBound_WhenShortfallUnderCap()
    {
        var state = Fixture(credits: 25, receipts: 100);
        // no decay-back, no research/appeasement spend, no steady mint: the only
        // motion is the four-share subtraction, so the end-of-loop shortfall is exact
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
        state.Config.Economy.SteadyIssuanceRate = 0.0;
        var pr = state.PolityOf(0);
        var b = PolityPolicies.Default.Budget;
        double moved = 100.0 * (b.Expansion + b.Development + b.Military + b.Reserves);
        double shortfall = moved - 25.0;                 // Credits after the sweep
        double cap = state.Config.Economy.SovereignIssuanceRate * 100;
        Assert.True(shortfall < cap, "test premise: shortfall must be under the cap");

        new AllocationPhase().Run(state);

        Assert.Equal(shortfall, state.CumulativeFiatIssued, 6);
        Assert.Equal(0.0, pr.Credits, 6);                // topped up exactly to zero
    }

    // loan-financing fix (Part A) — principal borrowed at the top of an epoch
    // flows through the SAME epoch's budget split: a polity that ended last
    // epoch negative borrows before its budget is set, and the fresh principal
    // reaches the Expansion/Development/etc. pools (not just the non-
    // discretionary bills). Without this the loan could never fund the
    // investment that raises future receipts, and the deficit compounded into a
    // debt spiral. RED before the BorrowedThisEpoch base term existed: pools saw
    // Receipts alone, so ExpansionPoints was 100*Expansion, not 700*Expansion.
    [Fact]
    public void BorrowedPrincipal_FlowsIntoTheSameEpochsBudgetSplit()
    {
        // carried a -500 balance into this epoch; a qualifying lender is present
        var state = Fixture(credits: -500, receipts: 100);
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;   // read the raw split
        state.Config.Economy.SteadyIssuanceRate = 0.0;     // Part B has its own tests
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;
        var pr = state.PolityOf(0);
        var b = PolityPolicies.Default.Budget;

        new AllocationPhase().Run(state);

        // Borrow ran at the top against the carried -500: principal = 1.2*500 =
        // 600, so the base is Receipts(100) + BorrowedThisEpoch(600) = 700.
        // ExpansionPoints only accrues in Allocation (foundings spend it in
        // Resolution), so it reads the split exactly.
        Assert.Single(state.Loans);
        Assert.Equal(600.0, state.Loans[0].Principal, 6);
        Assert.Equal(700.0 * b.Expansion, pr.ExpansionPoints, 6);
        // strictly more than Receipts alone (100*Expansion) would have funded —
        // the proof the borrowed principal reached the same epoch's pools
        Assert.True(pr.ExpansionPoints > 100.0 * b.Expansion,
            $"borrowed principal must reach the budget split ({pr.ExpansionPoints:0.###})");
    }

    // Part A boundary — Borrow runs at the TOP of the epoch against the carried
    // balance, before the budget is set; sovereign issuance still runs LAST and
    // only backstops the capped residual the loan didn't clear. The loan
    // principal reads the carried -100 (1.2x = 120), NOT the deeper hole the
    // budget split later digs — that is the proof Borrow precedes the loop.
    [Fact]
    public void Borrow_RunsAtTopOfEpoch_IssuanceBackstopsOnlyResidual()
    {
        var state = Fixture(credits: -100, receipts: 100);
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
        // a qualifying lender: an entered, portless polity flush enough to front
        // the principal twice over (Borrow's 2x-collateral gate)
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;

        new AllocationPhase().Run(state);

        // the carried deficit sought financing at the top, before issuance
        Assert.Single(state.Loans);
        Assert.Equal(0, state.Loans[0].BorrowerActorId);
        Assert.Equal(1, state.Loans[0].LenderActorId);
        Assert.Equal(120.0, state.Loans[0].Principal, 6);   // 1.2 x the carried -100
        // the budget split re-spent the borrowed principal into the pools, so the
        // treasury ends negative; issuance — running after Borrow — mints only the
        // capped residual (rate*Receipts), never the whole hole
        double cap = state.Config.Economy.SovereignIssuanceRate * 100;
        Assert.Equal(cap, state.CumulativeFiatIssued, 6);
    }

    // §5 — the boundary: issuance never covers loan service. ServiceLoans runs
    // before the per-polity loop against last epoch's balance, so a hopeless
    // borrower still defaults and loses collateral exactly as before — this
    // task changes nothing in ServiceLoans.
    [Fact]
    public void IssueSovereignCredit_NeverRescuesLoanService()
    {
        var state = Fixture(credits: -10000, receipts: 100);
        state.Actors[1].Entered = true;
        var port = state.Ports[0];
        var mine = new Facility(0, (int)InfraTypeId.Mine, 1, port.Hex, 0,
                                builtYear: 50);
        state.Facilities.Add(mine);
        // an outstanding loan the borrower (actor 0) cannot service
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 1000, ratePerYear: 0.02, termYears: 50, issuedYear: 0));

        new AllocationPhase().Run(state);

        // the bounded end-of-epoch mint (positive Receipts) did NOT run before
        // loan service and did NOT keep the borrower solvent through it
        Assert.True(state.Loans[0].Closed, "the unpayable loan still defaults");
        Assert.Equal(1, mine.OwnerActorId);              // collateral seized
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.LoanDefaulted) staged = true;
        Assert.True(staged, "default still stages, unchanged by issuance");
    }

    // P7 tick honesty (pre-existing ServiceLoans bug, fixed this slice): the
    // interest/amort formula now compounds per world-year with the
    // DecayIdlePools shape instead of scaling linearly by rate*years. A loan a
    // solvent borrower services in one coarse 25-year step must finish with the
    // same principal as one serviced in twenty-five fine 1-year steps — the
    // full-payment amort is multiplicative decay of the current principal, so
    // 25 fine ticks compose into the single coarse (1 - 1/Term)^25 factor. The
    // old linear amort drew ~half the whole principal due in one epoch and
    // capitalized the missed interest, driving exponential principal blowup.
    [Fact]
    public void ServiceLoans_PrincipalDecay_IsTickHonest()
    {
        double PrincipalAfter(int years, int steps)
        {
            var state = Fixture(credits: 1e9, receipts: 0);
            state.Actors[1].Entered = true;
            state.PolityOf(1).Credits = 1e9;              // a solvent lender
            state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
                principal: 1000.0, ratePerYear: 0.02, termYears: 50,
                issuedYear: 0));
            state.Config.Sim.YearsPerEpoch = years;
            var phase = new AllocationPhase();
            for (int i = 0; i < steps; i++) phase.Run(state);
            return state.Loans[0].Principal;
        }

        double coarse = PrincipalAfter(years: 25, steps: 1);
        double fine = PrincipalAfter(years: 1, steps: 25);
        // linear rate*years diverged here by ~100 credits (coarse 500 vs fine
        // ~603); compounding lands both on 1000*0.98^25 to floating tolerance
        Assert.Equal(fine, coarse, 4);
    }

    // Borrow is private and runs mid-phase after ServiceLoans (which would
    // default any open loan a negative borrower carries) — reflection invokes it
    // in isolation so the borrower-side debt-to-income gate can be exercised
    // against a state that still holds an open loan.
    private static int InvokeBorrow(SimState state)
    {
        var m = typeof(AllocationPhase).GetMethod("Borrow",
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static)!;
        return (int)m.Invoke(null, new object[] { state })!;
    }

    // Part A — the borrower-side credit-score gate: a polity already carrying
    // open-loan principal above MaxDebtToIncomeRatio × one-epoch-income is refused
    // NEW credit even though a flush lender is standing right there. RED before
    // the gate existed: the old Borrow gated only the lender, so this state issued
    // a second loan; the gate now correctly refuses.
    [Fact]
    public void Borrow_DeniesNewLoan_WhenExistingDebtExceedsIncomeRatio()
    {
        var state = Fixture(credits: -100, receipts: 0);
        var pr = state.PolityOf(0);
        pr.LastIncomePerYear = 4.0;   // ceiling = 3.0 × 4 × 25 years = 300
        // a qualifying lender: entered, flush enough to front 1.2×100 twice over
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;
        // existing open debt of 500 already exceeds the 300 ceiling
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 500, ratePerYear: 0.02, termYears: 125, issuedYear: 0));

        int issued = InvokeBorrow(state);

        Assert.Equal(0, issued);              // locked out of new credit
        Assert.Single(state.Loans);           // no second loan piled on
        Assert.Equal(0.0, pr.BorrowedThisEpoch, 9);
    }

    // Part A — the same polity, same lender, but existing debt UNDER the ceiling:
    // the gate opens and a new loan issues. This is the discriminator proving the
    // refusal above is the debt ratio, not a missing lender.
    [Fact]
    public void Borrow_IssuesNewLoan_WhenExistingDebtIsUnderIncomeRatio()
    {
        var state = Fixture(credits: -100, receipts: 0);
        var pr = state.PolityOf(0);
        pr.LastIncomePerYear = 4.0;   // ceiling = 300
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;
        // existing open debt of 100 stays under the 300 ceiling
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 100, ratePerYear: 0.02, termYears: 125, issuedYear: 0));

        int issued = InvokeBorrow(state);

        Assert.Equal(1, issued);
        Assert.Equal(2, state.Loans.Count);   // the fresh loan joined
        Assert.Equal(120.0, state.Loans[1].Principal, 6);   // 1.2 × the -100
    }

    // Part A — closed/defaulted loans do not count toward the debt load: a polity
    // whose old debt already defaulted is a clean slate for new credit.
    [Fact]
    public void Borrow_IgnoresClosedLoans_WhenSummingDebtLoad()
    {
        var state = Fixture(credits: -100, receipts: 0);
        var pr = state.PolityOf(0);
        pr.LastIncomePerYear = 4.0;   // ceiling = 300
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;
        // a huge but CLOSED loan — its principal is history, not a live burden
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 100000, ratePerYear: 0.02, termYears: 125, issuedYear: 0)
            { Closed = true });

        int issued = InvokeBorrow(state);

        Assert.Equal(1, issued);              // the closed loan does not gate
        Assert.Equal(2, state.Loans.Count);
    }

    // Part B — the always-on steady issuance channel fires for a SOLVENT polity,
    // distinguishing it from the reactive backstop: a polity that ends the epoch
    // in the black still mints SteadyIssuanceRate × its own receipts, while the
    // reactive IssueSovereignCredit stays silent (Credits never went negative).
    // The running total is a separate stock from CumulativeFiatIssued and grows
    // by the SAME amount each epoch — recomputed fresh from Receipts, never
    // compounding on itself.
    [Fact]
    public void SteadyIssuance_FiresForSolventPolity_DistinctFromReactiveBackstop()
    {
        // a fat cushion so the budget split can't drive the treasury negative
        var state = Fixture(credits: 5000, receipts: 1000);
        double rate = state.Config.Economy.SteadyIssuanceRate;
        var pr = state.PolityOf(0);

        new AllocationPhase().Run(state);

        Assert.True(pr.Credits > 0, "the polity stayed solvent all epoch");
        Assert.Equal(rate * 1000, state.CumulativeSteadyIssuance, 6);   // steady fired
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);              // backstop silent

        // second epoch, same receipts: the level grows by the SAME mint again —
        // proof it reads Receipts fresh, never accumulating a growing balance
        pr.Receipts = 1000;
        new AllocationPhase().Run(state);
        Assert.Equal(2 * rate * 1000, state.CumulativeSteadyIssuance, 6);
    }

    // Part B — steady issuance flows through allocatable (like BorrowedThisEpoch),
    // not into idle Credits: the budget pools read Receipts + steady, so the mint
    // funds real investment via the normal split.
    [Fact]
    public void SteadyIssuance_FlowsIntoTheBudgetSplit_NotIdleCash()
    {
        var state = Fixture(credits: 5000, receipts: 1000);
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;   // read the raw split
        double rate = state.Config.Economy.SteadyIssuanceRate;
        var pr = state.PolityOf(0);
        var b = PolityPolicies.Default.Budget;

        new AllocationPhase().Run(state);

        // base = Receipts(1000) + steady(rate*1000); ExpansionPoints only accrues
        // in Allocation, so it reads the split exactly
        double allocatable = 1000.0 + rate * 1000.0;
        Assert.Equal(allocatable * b.Expansion, pr.ExpansionPoints, 6);
        Assert.True(pr.ExpansionPoints > 1000.0 * b.Expansion,
            "the steady mint must reach the budget split, above receipts alone");
    }

    // Part B — the no-op guard: zero receipts mint nothing (a nonnegative fraction
    // of nonnegative receipts), and the running total never moves.
    [Fact]
    public void SteadyIssuance_MintsNothing_OnZeroReceipts()
    {
        var state = Fixture(credits: 100, receipts: 0);
        new AllocationPhase().Run(state);
        Assert.Equal(0.0, state.CumulativeSteadyIssuance, 9);
    }

    // ServiceLoans is private and normally reached through AllocationPhase.Run;
    // reflection invokes it in isolation so the capitalization-ceiling default
    // trigger can be exercised on a loan whose principal is set past the ceiling
    // directly, without the surrounding phase mutating it first.
    private static int InvokeServiceLoans(SimState state)
    {
        var m = typeof(AllocationPhase).GetMethod("ServiceLoans",
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static)!;
        return (int)m.Invoke(null, new object[] { state })!;
    }

    // The capitalization-ceiling default — the second, distinct default trigger:
    // a loan whose live Principal has capitalized past LoanCapitalizationCeiling ×
    // OriginalPrincipal is force-defaulted even though the borrower still holds
    // SOME positive Credits (so it is NOT the Credits<=0 trigger). The loan closes,
    // collateral is seized for the lender, and LoanDefaulted stages — the ceiling
    // path behaves identically to the zero-Credits path.
    [Fact]
    public void ServiceLoans_ForcesDefault_WhenPrincipalCapitalizesPastCeiling()
    {
        var state = Fixture(credits: 0, receipts: 0);
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 0;                 // the lender's ledger
        var mine = new Facility(0, (int)InfraTypeId.Mine, 1,
            state.Ports[0].Hex, 0, builtYear: 50);
        state.Facilities.Add(mine);                    // owned by borrower (actor 0)
        // issued at 100; capitalized to 250 over prior epochs — already past the
        // 2.0 × 100 = 200 ceiling before this epoch's service runs
        var loan = new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 100, ratePerYear: 0.02, termYears: 125, issuedYear: 0);
        loan.Principal = 250;
        state.Loans.Add(loan);
        // the borrower is nominally solvent: SOME Credits, but nowhere near the
        // ~205-credit payment, so it lands in the partial-payment/capitalize path
        state.PolityOf(0).Credits = 50;

        int defaults = InvokeServiceLoans(state);

        Assert.Equal(1, defaults);
        Assert.True(loan.Closed, "the over-ceiling loan is written off");
        Assert.Equal(1, mine.OwnerActorId);            // collateral seized by lender
        Assert.Equal(200.0,
            state.Config.Economy.LoanCapitalizationCeiling * loan.OriginalPrincipal, 9);
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.LoanDefaulted) staged = true;
        Assert.True(staged, "the ceiling default stages like any other");
    }

    // The discriminator: a loan that capitalizes but stays UNDER the ceiling is
    // NOT force-defaulted — it stays open and its principal keeps growing normally.
    // Proves the trigger above is the ceiling, not merely the partial-payment path.
    [Fact]
    public void ServiceLoans_LeavesLoanOpen_WhenCapitalizationStaysUnderCeiling()
    {
        var state = Fixture(credits: 0, receipts: 0);
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 0;
        var mine = new Facility(0, (int)InfraTypeId.Mine, 1,
            state.Ports[0].Hex, 0, builtYear: 50);
        state.Facilities.Add(mine);
        // issued at 100, currently 110 — post-capitalization it lands near ~180,
        // still under the 200 ceiling
        var loan = new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 100, ratePerYear: 0.02, termYears: 125, issuedYear: 0);
        loan.Principal = 110;
        state.Loans.Add(loan);
        state.PolityOf(0).Credits = 1;                 // partial: capitalizes

        int defaults = InvokeServiceLoans(state);

        Assert.Equal(0, defaults);
        Assert.False(loan.Closed, "an under-ceiling loan stays open");
        Assert.True(loan.Principal > 110.0, "and keeps capitalizing normally");
        Assert.True(loan.Principal
            < state.Config.Economy.LoanCapitalizationCeiling * loan.OriginalPrincipal,
            "still below the ceiling");
        Assert.Equal(0, mine.OwnerActorId);            // collateral not seized
    }
}
