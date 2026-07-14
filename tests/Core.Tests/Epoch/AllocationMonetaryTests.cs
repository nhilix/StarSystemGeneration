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
        // isolate the subtraction: no pool decay, no research/appeasement spend
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
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
        // no decay-back, no research/appeasement spend: the only motion is the
        // four-share subtraction, so the end-of-loop shortfall is exact
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
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

    // fix wave 1 (finding 1) — Borrow runs BEFORE sovereign issuance: a
    // shortfall a solvent lender can cover becomes a LOAN, and the bounded mint
    // (which now backstops only what stays negative after borrowing) never
    // fires. The pre-fix ordering minted the shortfall away inside the loop, so
    // no loan was ever sought — zero loans in 40 epochs on the reference seed.
    [Fact]
    public void Borrow_RunsBeforeIssuance_ShortfallBecomesLoanNotMint()
    {
        var state = Fixture(credits: 0, receipts: 100);
        // isolate the end-of-loop shortfall: no pool recirculation muddying it
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
        // a qualifying lender: an entered, portless polity flush enough to front
        // the principal twice over (Borrow's 2x-collateral gate)
        state.Actors[1].Entered = true;
        state.PolityOf(1).Credits = 10000;

        new AllocationPhase().Run(state);

        // the shortfall was borrowed, not minted
        Assert.Single(state.Loans);
        Assert.Equal(0, state.Loans[0].BorrowerActorId);
        Assert.Equal(1, state.Loans[0].LenderActorId);
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);
        // the loan cleared the borrower back to solvent, so issuance — running
        // after Borrow — correctly saw nothing left to backstop
        Assert.True(state.PolityOf(0).Credits >= 0,
            $"the loan should have restored solvency ({state.PolityOf(0).Credits:0})");
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
}
