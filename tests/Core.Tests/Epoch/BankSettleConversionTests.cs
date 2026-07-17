using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 3: the <see cref="SimState.SettleConversion"/>
/// primitive (the spread-skimming sibling of the exempt <see cref="SimState.
/// RecordConversion"/>), plus the reserve-aware per-currency residual in
/// <see cref="MetricsOps"/>. A conversion's spread is money SEQUESTERED out of
/// the destination currency's circulating <see cref="Currency.Supply"/> into its
/// <see cref="Bank.Reserve"/>; because <c>SupplyOps</c> stays circulating-only,
/// the residual must add the live reserve back to the balance side or a nonzero
/// reserve reads as a false leak. These are the focused unit checks; no exchange
/// site is wired yet (task 4), so real seed-42 runs keep a zero reserve and stay
/// byte-identical.</summary>
public class BankSettleConversionTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    // an Actor + PolityRecord pair so FoundCurrency (which reads Actors[id].Name
    // and founds the parallel Bank) has something to name the currency after.
    private static PolityRecord AddPolity(SimState state, int id)
    {
        state.Actors.Add(new Actor(id, ActorKind.Polity, $"P{id}",
            new HexCoordinate(id, id), entryYear: 0,
            new GenesisController(state.Config)) { Entered = true });
        var pr = new PolityRecord(id, 0);
        state.Polities.Add(pr);
        return pr;
    }

    private static Currency FoundCurrency(SimState state, int polityId, double rate)
    {
        var cur = state.FoundCurrency(polityId);
        cur.NumeraireRate = rate;
        return cur;
    }

    // ---- (a) cross-currency settle: skims spread to the target bank ----

    [Fact]
    public void SettleConversion_AcrossCurrencies_SkimsSpreadToTargetBank_ReturnsNet_RecordsFull()
    {
        var state = NewState();
        AddPolity(state, 0);
        AddPolity(state, 1);
        var from = FoundCurrency(state, 0, 1.0);
        var to = FoundCurrency(state, 1, 2.0);

        double outAmount = 100.0;
        double inAmount = state.ConvertCurrency(outAmount, from.Id, to.Id); // 50
        double spread = inAmount * state.Config.Economy.ConversionSpread;   // 50 * 0.005

        double net = state.SettleConversion(from.Id, outAmount, to.Id, inAmount);

        // net = inAmount - spread landed in a wallet; spread sits in the reserve
        Assert.Equal(inAmount - spread, net, 12);
        Assert.Equal(spread, state.BankOf(to.Id).Reserve, 12);
        Assert.Equal(spread, state.BankOf(to.Id).CumulativeSpreadIntake, 12);
        Assert.Equal(0.0, state.BankOf(from.Id).Reserve, 12);
        // counters record the FULL amounts (same as RecordConversion) — the
        // spread is netted back on the balance side by the reserve, not by a
        // shrunken ConvertedIn
        Assert.Equal(outAmount, from.CumulativeConvertedOut, 12);
        Assert.Equal(inAmount, to.CumulativeConvertedIn, 12);
    }

    // ---- (b) same-currency / pre-genesis: full return, no skim ----

    [Fact]
    public void SettleConversion_SameCurrency_ReturnsFull_NoSkim_NoReserveChange()
    {
        var state = NewState();
        AddPolity(state, 0);
        var cur = FoundCurrency(state, 0, 1.0);

        double net = state.SettleConversion(cur.Id, 100.0, cur.Id, 100.0);

        Assert.Equal(100.0, net, 12);
        Assert.Equal(0.0, state.BankOf(cur.Id).Reserve, 12);
        Assert.Equal(0.0, state.BankOf(cur.Id).CumulativeSpreadIntake, 12);
        // same-currency is a no-op for the counters too (mirrors RecordConversion)
        Assert.Equal(0.0, cur.CumulativeConvertedIn, 12);
        Assert.Equal(0.0, cur.CumulativeConvertedOut, 12);
    }

    [Fact]
    public void SettleConversion_PreGenesisId_ReturnsFull_NoSkim()
    {
        var state = NewState();
        AddPolity(state, 0);
        var cur = FoundCurrency(state, 0, 1.0);

        // toId is the real currency, fromId is the pre-genesis sentinel: no rate
        // exists, so the transfer is dormant 1:1 and nothing is skimmed
        double net = state.SettleConversion(-1, 100.0, cur.Id, 100.0);

        Assert.Equal(100.0, net, 12);
        Assert.Equal(0.0, state.BankOf(cur.Id).Reserve, 12);
        Assert.Equal(0.0, state.BankOf(cur.Id).CumulativeSpreadIntake, 12);
        Assert.Equal(0.0, cur.CumulativeConvertedIn, 12);
    }

    // ---- (c) reserve-aware residual: nonzero reserve nets to zero ----

    [Fact]
    public void Residual_WithNonzeroReserve_NetsToZeroUnderNewFormula_AndWouldLeakUnderOld()
    {
        var state = NewState();
        AddPolity(state, 0);
        var cur = FoundCurrency(state, 0, 1.0);
        var bank = state.BankOf(cur.Id);

        // baseline epoch: 1000 circulating, empty reserve, no mints/conversions
        cur.Supply = 1000.0;
        bank.Reserve = 0.0;
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        // next epoch: 100 units sequestered out of circulation into the reserve
        // (exactly what a spread skim does). No mint, no net conversion — the
        // total conserved money is unchanged, only its split between circulating
        // Supply and the sequestered Reserve moved.
        cur.Supply = 900.0;
        bank.Reserve = 100.0;
        var row = MetricsOps.Snapshot(state);

        var curRow = row.Currencies[0];
        var baseRow = state.Health.Rows[0].Currencies[0];

        // the baseline row captured the reserve so the delta is correct
        Assert.Equal(0.0, baseRow.Reserve, 12);
        Assert.Equal(100.0, curRow.Reserve, 12);

        // NEW formula (Supply + Reserve balance side): nets to zero — no leak
        Assert.Equal(0.0, curRow.Residual, 9);

        // OLD formula (Supply-only balance side) would have read a false leak of
        // -reserve. Recover the old residual by removing the reserve delta the
        // new formula added; assert it equals -100 so the fix is genuinely
        // covered in both directions.
        double oldResidual = curRow.Residual - (curRow.Reserve - baseRow.Reserve);
        Assert.Equal(-100.0, oldResidual, 9);
    }
}
