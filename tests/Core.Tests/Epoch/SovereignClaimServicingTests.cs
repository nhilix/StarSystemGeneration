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
/// and never forced, and interest never capitalizes.</summary>
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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        // interest due = 1_000_000 × 0.02 = 20_000; budget = 100 × 0.25 = 25
        var (state, pr, bank) = Fixture(credits: 100, claim: 1_000_000);
        double interestDue = 1_000_000 * eco.SovereignClaimInterestRate;
        double budget = 100 * eco.ClaimServicingShare;
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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        // claim 1000 → interest 20; budget = 100 × 0.25 = 25 ≥ 20, so the
        // interest is paid in full and 5 remains for principal
        var (state, pr, bank) = Fixture(credits: 100, claim: 1000);
        double interest = 1000 * eco.SovereignClaimInterestRate;
        double budget = 100 * eco.ClaimServicingShare;
        double principal = budget - interest;
        var cur = state.CurrencyOf(pr.CurrencyId);

        Service(state, pr);

        Assert.Equal(interest, bank.Reserve, 9);           // the reserve's first
        Assert.Equal(100.0 - budget, pr.Credits, 9);       // real-economy inflow
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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        var (state, pr, bank) = Fixture(credits: 100, claim: 1000);
        double interest = 1000 * eco.SovereignClaimInterestRate;
        double principal = 100 * eco.ClaimServicingShare - interest;
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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        var (state, pr, bank) = Fixture(credits: 1000, claim: 2000);
        double budget = 1000 * eco.ClaimServicingShare;          // 250
        double interest = 2000 * eco.SovereignClaimInterestRate; // 40
        double principal = budget - interest;                    // 210 — NOT
        // (1000 − 40) × 0.25 − 40 = 200, which is what a re-read would give

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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        var (state, pr, bank) = Fixture(credits: 10_000, claim: 10);
        double interest = 10 * eco.SovereignClaimInterestRate;   // 0.2

        Service(state, pr);

        Assert.Equal(0.0, bank.ClaimOnState, 9);                 // fully repaid
        Assert.Equal(10.0, bank.CumulativeRetired, 9);
        // budget was 2500; only interest + the 10 of principal was spent
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
        var eco = EpochTestKit.Seeded().State.Config.Economy;
        // budget exactly equals the interest due, so no principal is repaid and
        // no retirement term is needed: claim × rate == credits × share
        // 1000 × 0.02 = 20; credits 80 × 0.25 = 20
        var (state, pr, bank) = Fixture(credits: 80, claim: 1000);
        Assert.Equal(1000 * eco.SovereignClaimInterestRate,
                     80 * eco.ClaimServicingShare, 9);

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

    /// <summary>Principal repayment moves BOTH sides of the §6 identity by p —
    /// but the residual does not subtract CumulativeFiatRetired until task 6,
    /// so a repayment epoch reads as a residual of exactly +p today. This test
    /// pins that gap precisely (rather than hiding it) and is the canary task 6
    /// flips: when the retirement term is wired, this expectation becomes 0.</summary>
    [Fact]
    public void PrincipalRepayment_ResidualIsExactlyTheNotYetWiredRetirement()
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
        // the identity's balance side is short by exactly the retirement the
        // residual does not yet subtract (task 6 wires it and this goes to 0)
        Assert.Equal(-cur.CumulativeFiatRetired, curRow.Residual, 6);
    }
}
