using System.Linq;
using System.Reflection;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice BF task 5 (bank-flow design §4) — the tail of the lending
/// relationship task 4 opened: the polity services its bank's claim out of
/// surplus, and principal repayment DESTROYS money (the sim's first monetary
/// sink). These tests pin the two hard rules the design calls load-bearing
/// against slice SH's structural debt-spiral finding: servicing is surplus-only
/// and never forced, and interest never capitalizes.
///
/// Task 5b (design §4a) added the clock contract: both rates are per-WORLD-YEAR
/// and scaled to the epoch's length (P7 / "time, not ticks"). The share
/// compounds, the interest is linear — see the three tests at the foot of this
/// class, which pin each half exactly and the whole to a band.</summary>
public class SovereignClaimServicingTests
{
    /// <summary>An entered polity with its own currency, its parallel
    /// <see cref="Bank"/>, and a claim already on the books (as if task 4's
    /// <see cref="Bank.LendToState"/> had run in an earlier epoch). Mirrors
    /// SovereignLendingTests.Fixture.</summary>
    private static (SimState State, PolityRecord Pr, Bank Bank) Fixture(
        double credits, double claim, double reserve = 0)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        state.FoundCurrency(actor.Id);
        var bank = state.BankOf(pr.CurrencyId);
        bank.Reserve = reserve;
        bank.ClaimOnState = claim;
        bank.CumulativeLentToState = claim;
        pr.Credits = credits;
        state.WorldYear = 100;
        return (state, pr, bank);
    }

    /// <summary>The design's §4a year-scaling, mirrored here so every
    /// expectation below states the PROPERTY rather than a magic number: the
    /// servicing share compounds per world-year (a stock the same operation
    /// depletes, exactly like DecayIdlePools), while the interest rate is
    /// LINEAR in years — rule 2 forbids compounding, and the claim cannot grow
    /// between world-years, so each year accrues on the same principal.</summary>
    private static (double Share, double InterestRate) Scaled(SimState state)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        return (1.0 - System.Math.Pow(1.0 - eco.ClaimServicingSharePerYear, years),
                eco.SovereignClaimInterestRatePerYear * years);
    }

    private static void Service(SimState state, PolityRecord pr)
    {
        var m = typeof(AllocationPhase).GetMethod("ServiceSovereignClaim",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        m.Invoke(null, new object[] { state, pr });
    }

    // ---- rule 1: surplus-only, never forced ----

    [Fact]
    public void InsolventPolity_ServicesNothing()
    {
        var (state, pr, bank) = Fixture(credits: -500, claim: 1000);

        Service(state, pr);

        // it borrows more; it does not pay. Nothing moves at all.
        Assert.Equal(-500.0, pr.Credits, 9);
        Assert.Equal(1000.0, bank.ClaimOnState, 9);
        Assert.Equal(0.0, bank.Reserve, 9);
        Assert.Equal(0.0, bank.CumulativeRetired, 9);
        Assert.Equal(0.0, state.CurrencyOf(pr.CurrencyId).CumulativeFiatRetired, 9);
    }

    [Fact]
    public void ZeroBalancePolity_ServicesNothing()
    {
        var (state, pr, bank) = Fixture(credits: 0, claim: 1000);

        Service(state, pr);

        Assert.Equal(0.0, pr.Credits, 9);
        Assert.Equal(1000.0, bank.ClaimOnState, 9);
    }

    [Fact]
    public void NoClaim_ServicesNothing()
    {
        var (state, pr, bank) = Fixture(credits: 1000, claim: 0);

        Service(state, pr);

        Assert.Equal(1000.0, pr.Credits, 9);
        Assert.Equal(0.0, bank.Reserve, 9);
        Assert.Equal(0.0, bank.CumulativeRetired, 9);
    }

    /// <summary>Rule 1 is structural, not a clamp: every term is bounded by a
    /// share of a POSITIVE balance, so a treasury can never be driven negative
    /// — checked here across a long history of servicing epochs against a huge
    /// claim book that always exceeds the budget.</summary>
    [Fact]
    public void Servicing_NeverDrivesCreditsNegative_AcrossAFullHistory()
    {
        var (state, pr, bank) = Fixture(credits: 1000, claim: 1_000_000);

        for (int epoch = 0; epoch < 200; epoch++)
        {
            Service(state, pr);
            Assert.True(pr.Credits > 0.0,
                $"epoch {epoch}: servicing drove the treasury to {pr.Credits}");
            Assert.True(bank.ClaimOnState >= 0.0,
                $"epoch {epoch}: the claim book went negative ({bank.ClaimOnState})");
        }
    }

    [Fact]
    public void PreGenesisPolity_HasNoBank_ServicesNothing_AndDoesNotThrow()
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        pr.Credits = 1000;
        Assert.True(pr.CurrencyId < 0, "fixture precondition: no currency founded");

        Service(state, pr);                        // must not throw

        Assert.Equal(1000.0, pr.Credits, 9);
    }

    // ---- rule 2: interest NEVER capitalizes ----

    /// <summary>The rule-2 test. A polity whose whole servicing budget falls
    /// short of the interest due pays what it can and the REMAINDER IS
    /// DISCARDED — it is never added to the claim. The claim book is unchanged:
    /// no principal was affordable, and unpaid interest does not accrue. This
    /// is what makes the mechanism spiral-proof: there is no compounding term.</summary>
    [Fact]
    public void CannotAffordFullInterest_ClaimIsUnchanged_NoCapitalization()
    {
        // at the default 25y epoch: interest due = 1_000_000 × 0.025 = 25_000;
        // budget = 100 × 0.222 ≈ 22.2
        var (state, pr, bank) = Fixture(credits: 100, claim: 1_000_000);
        var (share, rate) = Scaled(state);
        double interestDue = 1_000_000 * rate;
        double budget = 100 * share;
        Assert.True(budget < interestDue, "fixture precondition: interest unaffordable");

        Service(state, pr);

        // the claim did NOT grow by the unpaid interest — it did not move at all
        Assert.Equal(1_000_000.0, bank.ClaimOnState, 6);
        // the polity paid its whole budget as interest, and nothing more
        Assert.Equal(100.0 - budget, pr.Credits, 9);
        Assert.Equal(budget, bank.Reserve, 9);
        // no principal was affordable, so nothing was retired
        Assert.Equal(0.0, bank.CumulativeRetired, 9);
        Assert.Equal(0.0, state.CurrencyOf(pr.CurrencyId).CumulativeFiatRetired, 9);
    }

    /// <summary>A permanently broke polity never pays and its claim never
    /// grows — the claim book is flat across a whole history.</summary>
    [Fact]
    public void PermanentlyBrokePolity_ClaimNeverGrows()
    {
        var (state, pr, bank) = Fixture(credits: -100, claim: 5000);

        for (int epoch = 0; epoch < 200; epoch++)
        {
            Service(state, pr);
            Assert.Equal(5000.0, bank.ClaimOnState, 9);
        }
    }

    // ---- the mechanics: interest, then principal, out of ONE budget ----

    /// <summary>Interest is an internal Credits→Reserve move: one currency,
    /// no counter motion, no money created or destroyed.</summary>
    [Fact]
    public void Interest_MovesCreditsToReserve_WithNoCounterMotion()
    {
        // at the default 25y epoch: claim 1000 → interest 25; budget =
        // 100 × 0.222 ≈ 22.2 — so pick credits that afford both comfortably
        var (state, pr, bank) = Fixture(credits: 1000, claim: 1000);
        var (share, rate) = Scaled(state);
        double interest = 1000 * rate;
        double budget = 1000 * share;
        double principal = budget - interest;
        Assert.True(interest < budget, "fixture precondition: interest affordable");
        var cur = state.CurrencyOf(pr.CurrencyId);

        Service(state, pr);

        Assert.Equal(interest, bank.Reserve, 9);           // the reserve's first
        Assert.Equal(1000.0 - budget, pr.Credits, 9);      // real-economy inflow
        // the interest moved no counter — only the principal is a mint/burn
        Assert.Equal(principal, cur.CumulativeFiatRetired, 9);
        Assert.Equal(0.0, cur.CumulativeFiatIssued, 9);
        Assert.Equal(0.0, cur.CumulativeConvertedIn, 9);
        Assert.Equal(0.0, cur.CumulativeConvertedOut, 9);
    }

    /// <summary>Principal repayment destroys money: the claim and the retired
    /// counter move by exactly the same amount, and Credits falls by it.</summary>
    [Fact]
    public void PrincipalRepayment_MovesClaimAndRetiredByTheSameAmount()
    {
        var (state, pr, bank) = Fixture(credits: 1000, claim: 1000);
        var (share, rate) = Scaled(state);
        double principal = 1000 * share - 1000 * rate;
        var cur = state.CurrencyOf(pr.CurrencyId);

        Service(state, pr);

        Assert.Equal(1000.0 - principal, bank.ClaimOnState, 9);
        Assert.Equal(principal, cur.CumulativeFiatRetired, 9);
        Assert.Equal(principal, bank.CumulativeRetired, 9);
        // CumulativeLentToState is a LEVEL — repayment never draws it down
        Assert.Equal(1000.0, bank.CumulativeLentToState, 9);
    }

    /// <summary>The compound-assignment trap (design §4, and a real bug already
    /// documented at Phases.cs:444): the budget is computed ONCE, before Credits
    /// is mutated. Re-reading `Credits × share` after the interest debit would
    /// silently shrink the principal budget. Pinned numerically: with the budget
    /// computed once, principal = budget − interest exactly.</summary>
    [Fact]
    public void ServicingBudget_IsComputedOnce_BeforeAnyCreditsMutation()
    {
        var (state, pr, bank) = Fixture(credits: 1000, claim: 2000);
        var (share, rate) = Scaled(state);
        double budget = 1000 * share;            // ≈ 222.2 at the default clock
        double interest = 2000 * rate;           // = 50
        double principal = budget - interest;    // ≈ 172.2 — NOT
        // (1000 − 50) × share − 50, which is what a re-read would give
        Assert.True(interest < budget, "fixture precondition: interest affordable");

        Service(state, pr);

        Assert.Equal(principal, bank.CumulativeRetired, 9);
        Assert.Equal(1000.0 - budget, pr.Credits, 9);
    }

    /// <summary>A claim smaller than the principal budget is repaid in FULL and
    /// stops at zero — the claim never goes negative, and the polity keeps the
    /// unspent remainder of its budget.</summary>
    [Fact]
    public void SmallClaim_IsRepaidInFull_AndStopsAtZero()
    {
        var (state, pr, bank) = Fixture(credits: 10_000, claim: 10);
        var (_, rate) = Scaled(state);
        double interest = 10 * rate;             // = 0.25 at the default clock

        Service(state, pr);

        Assert.Equal(0.0, bank.ClaimOnState, 9);                 // fully repaid
        Assert.Equal(10.0, bank.CumulativeRetired, 9);
        // the budget was ~2222; only interest + the 10 of principal was spent
        Assert.Equal(10_000.0 - interest - 10.0, pr.Credits, 9);
    }

    // ---- conservation (design §6) ----

    /// <summary>Interest alone is conservation-neutral even under TODAY's
    /// residual (which does not yet subtract CumulativeFiatRetired — that is
    /// task 6): Supply falls by i and Reserve rises by i, so `Supply + Reserve`
    /// is unchanged and no counter moves. This is the half of §6 that is
    /// verifiable before task 6 lands.</summary>
    [Fact]
    public void InterestOnly_KeepsPerCurrencyResidualZero()
    {
        // budget exactly equals the interest due, so no principal is repaid and
        // no retirement term is needed: claim × rate == credits × share. The
        // balance is SOLVED for from the year-scaled knobs rather than written
        // as a literal, so it stays exact at any epoch length (§4a).
        var (state, pr, bank) = Fixture(credits: 1, claim: 1000);
        var (share, rate) = Scaled(state);
        pr.Credits = 1000 * rate / share;
        Assert.Equal(1000 * rate, pr.Credits * share, 9);

        SupplyOps.Recompute(state);
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        Service(state, pr);

        SupplyOps.Recompute(state);
        var row = MetricsOps.Snapshot(state);
        var curRow = row.Currencies.First(r => r.CurrencyId == pr.CurrencyId);

        Assert.Equal(0.0, curRow.Residual, 6);
        Assert.Equal(0.0, bank.CumulativeRetired, 9);           // interest only
        Assert.Equal(1000.0, bank.ClaimOnState, 9);
    }

    /// <summary>Principal repayment moves BOTH sides of the §6 identity by p:
    /// Supply falls by p and CumulativeFiatRetired rises by p. Task 6 wired the
    /// retirement term into the residual's delta form (and its baseline carry),
    /// so a repayment epoch now nets to 0 — (−p) + (p) = 0. This is the flipped
    /// canary: it asserted the not-yet-wired gap before task 6; it now proves the
    /// term is wired.</summary>
    [Fact]
    public void PrincipalRepayment_ResidualIsZero_RetirementWired()
    {
        var (state, pr, bank) = Fixture(credits: 1000, claim: 2000);

        SupplyOps.Recompute(state);
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        Service(state, pr);

        SupplyOps.Recompute(state);
        var row = MetricsOps.Snapshot(state);
        var curRow = row.Currencies.First(r => r.CurrencyId == pr.CurrencyId);
        var cur = state.CurrencyOf(pr.CurrencyId);

        Assert.True(cur.CumulativeFiatRetired > 0.0, "principal was repaid");
        // the residual now subtracts the retirement it destroyed, so the
        // repayment epoch is conserved (same FP tolerance the sibling tests use)
        Assert.Equal(0.0, curRow.Residual, 6);
    }

    // ---- §4a: time, not ticks ----

    /// <summary>A polity with a claim and a STEADY REAL INCOME of 100 credits
    /// per world-year, serviced for `steps` epochs of `ype` years each. The
    /// income is stated per world-year and scaled to the epoch, so the only
    /// thing under test is whether servicing itself tracks the clock. Deliberately
    /// NOT the full engine: at engine level the claim book is fed by
    /// IssueSovereignCredit, whose cap is applied once per EPOCH rather than per
    /// world-year, so a fine clock gets 25x the borrowing opportunities and the
    /// claim book diverges ~3x between clocks BEFORE this pass ever runs. That
    /// is a real, separate, pre-existing clock-dependence (measured with
    /// servicing disabled: 6259 issued at 25y/epoch vs 18104 at 1y/epoch); a
    /// test that swept it in would be measuring someone else's defect.</summary>
    private static (double Credits, double Claim, double Retired, double Reserve)
        ServiceWithSteadyIncome(int steps, int ype, double claim,
                                double credits = 1000, double incomePerYear = 100)
    {
        var state = EpochTestKit.Seeded().State;
        state.Config.Sim.YearsPerEpoch = ype;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        state.FoundCurrency(actor.Id);
        var bank = state.BankOf(pr.CurrencyId);
        bank.ClaimOnState = claim;
        pr.Credits = credits;
        for (int i = 0; i < steps; i++)
        {
            pr.Credits += incomePerYear * ype;
            Service(state, pr);
        }
        return (pr.Credits, bank.ClaimOnState,
                state.CurrencyOf(pr.CurrencyId).CumulativeFiatRetired,
                bank.Reserve);
    }

    /// <summary>§4a, half 1: the servicing SHARE compounds per world-year, so a
    /// single 25-year step services EXACTLY what twenty-five 1-year steps would
    /// — asserted exactly, not within a band. With no income the budget is the
    /// pure statement of the compounding identity `1 − (1 − s)^25 == 1 − 0.99^25`.
    /// This is the DecayIdlePools invariant (Phases.cs) applied to the claim
    /// book. The pre-amendment code charged a flat per-epoch share and fails
    /// this outright.</summary>
    [Fact]
    public void ServicingShare_CompoundsPerWorldYear_TwentyFiveOneYearStepsEqualOne25YearStep()
    {
        // a claim far larger than any budget, so the whole budget is spent every
        // step and the claim never moves — isolating the SHARE from everything else
        var coarse = ServiceWithSteadyIncome(steps: 1, ype: 25, claim: 1_000_000,
                                             incomePerYear: 0);
        var fine = ServiceWithSteadyIncome(steps: 25, ype: 1, claim: 1_000_000,
                                           incomePerYear: 0);

        Assert.Equal(coarse.Credits, fine.Credits, 6);
        // the whole budget was spent in both, and identically
        Assert.Equal(coarse.Retired + coarse.Reserve,
                     fine.Retired + fine.Reserve, 6);
        Assert.Equal(1000.0 * (1.0 - System.Math.Pow(0.99, 25)), coarse.Reserve, 6);
    }

    /// <summary>§4a, half 2: interest is LINEAR in years, never compounded. A
    /// 25-year epoch charges exactly 25x what a 1-year epoch charges on the same
    /// starting claim — asserted exactly, on a single step from an identical
    /// state, with the budget deliberately non-binding so the Math.Min does not
    /// mask the rate. A compound form (`claim × ((1+r)^25 − 1)`) is ~1.2% higher
    /// and fails this; that form is FORBIDDEN by rule 2, which is what makes the
    /// mechanism structurally spiral-proof.</summary>
    [Fact]
    public void Interest_IsLinearInYears_NeverCompounded()
    {
        // credits are huge so the budget never binds; claim 1000 → interest is
        // 1000 × 0.001 × years exactly
        var coarse = ServiceWithSteadyIncome(steps: 1, ype: 25, claim: 1000,
                                             credits: 100_000, incomePerYear: 0);
        var fine = ServiceWithSteadyIncome(steps: 1, ype: 1, claim: 1000,
                                           credits: 100_000, incomePerYear: 0);

        // the reserve receives interest and nothing else
        Assert.Equal(25.0, coarse.Reserve, 9);            // 1000 × 0.001 × 25
        Assert.Equal(1.0, fine.Reserve, 9);               // 1000 × 0.001 × 1
        Assert.Equal(25.0 * fine.Reserve, coarse.Reserve, 9);   // LINEAR
    }

    /// <summary>The regression guard for the amendment (design §4a, P7 "time,
    /// not ticks"), at the level of a whole serviced history. The original §4
    /// charged a fraction of a treasury STOCK once per EPOCH, so servicing
    /// intensity scaled as 1/YearsPerEpoch — under this harness it settles to a
    /// treasury of ~10000 at 25y/epoch against ~400 at 1y/epoch, a ~25x
    /// clock-dependence. Year-scaled, the two clocks agree to ~12%.
    ///
    /// The residual ~12% is NOT a defect and cannot be tuned away: it is the
    /// irreducible discretization gap of any per-year-compounded share against
    /// income arriving between draws (steady state solves to 8750 at 25y vs 9900
    /// at 1y — the observed figures exactly). DecayIdlePools, the precedent this
    /// follows, has the identical characteristic. The band is set at 20%: wide
    /// enough for that gap, ~2 orders tighter than the regression it catches.</summary>
    [Fact]
    public void Servicing_IsClockInvariant_AcrossEpochLengths()
    {
        // 200 world-years of the same history on two clocks, claim large enough
        // that it is never fully repaid (so servicing is live throughout)
        var coarse = ServiceWithSteadyIncome(steps: 8, ype: 25, claim: 20_000);
        var fine = ServiceWithSteadyIncome(steps: 200, ype: 1, claim: 20_000);

        void AssertAgrees(string what, double a, double b)
        {
            double scale = System.Math.Max(System.Math.Abs(a), System.Math.Abs(b));
            if (scale < 1e-9) return;
            double rel = System.Math.Abs(a - b) / scale;
            Assert.True(rel < 0.20,
                $"{what} is clock-dependent: {a} at 25y/epoch vs {b} at 1y/epoch " +
                $"({rel:P1} apart) — servicing must not scale with the clock (§4a)");
        }

        AssertAgrees("treasury", coarse.Credits, fine.Credits);
        AssertAgrees("claim book", coarse.Claim, fine.Claim);
        AssertAgrees("principal retired", coarse.Retired, fine.Retired);
        AssertAgrees("interest taken", coarse.Reserve, fine.Reserve);

        // not vacuous: both clocks actually serviced, and neither repaid it all
        Assert.True(coarse.Retired > 0.0 && fine.Retired > 0.0);
        Assert.True(coarse.Claim > 0.0 && fine.Claim > 0.0);
    }
}
