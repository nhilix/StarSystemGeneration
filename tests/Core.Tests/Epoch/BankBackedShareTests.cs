using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-4 task 2 (monetary-federation design §2) — <see
/// cref="Bank.BackedShare"/>, the bounded-share sibling of BF's unbacked FX
/// signal: <c>Reserve / (Reserve + ClaimOnState)</c>, guarded 0/0 → 0.0. A
/// pure computed property, no state, no allocation — these tests exercise it
/// directly on a bare <see cref="Bank"/>, with no other production code
/// reading it yet.</summary>
public class BankBackedShareTests
{
    [Fact]
    public void Saver_DeepReserve_NoClaim_IsFullyBacked()
    {
        var bank = new Bank(0) { Reserve = 100.0, ClaimOnState = 0.0 };

        Assert.Equal(1.0, bank.BackedShare);
    }

    [Fact]
    public void Debtor_NoReserve_PositiveClaim_IsUnbacked()
    {
        var bank = new Bank(0) { Reserve = 0.0, ClaimOnState = 100.0 };

        Assert.Equal(0.0, bank.BackedShare);
    }

    [Fact]
    public void Balanced_ReserveExactlyMatchesClaim_IsHalfBacked()
    {
        var bank = new Bank(0) { Reserve = 50.0, ClaimOnState = 50.0 };

        Assert.Equal(0.5, bank.BackedShare);
    }

    [Fact]
    public void Fresh_ZeroReserveZeroClaim_GuardsTo_Zero()
    {
        var bank = new Bank(0) { Reserve = 0.0, ClaimOnState = 0.0 };

        Assert.Equal(0.0, bank.BackedShare);
    }
}
