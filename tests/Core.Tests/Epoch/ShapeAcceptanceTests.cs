using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 9 — shape acceptance over full 40-epoch histories
/// across seeds: prices bounded (no runaway spirals, no NaN), populations
/// bounded by their port caps, and the credit ledger conserved to the mint
/// (P4: every flow is a transfer; money enters only at entry).</summary>
public class ShapeAcceptanceTests
{
    private static SimState Run(ulong seed)
    {
        var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 10 };
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                      new EpochSimConfig { MasterSeed = seed });
        new EpochEngine().Run(state);
        return state;
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    [InlineData(1234ul)]
    public void FortyEpochs_PricesStayBounded(ulong seed)
    {
        var state = Run(seed);
        var eco = state.Config.Economy;
        Assert.True(state.Markets.Count > 0);
        foreach (var m in state.Markets)
            for (int g = 0; g < m.Price.Length; g++)
            {
                Assert.True(double.IsFinite(m.Price[g]),
                    $"price NaN/Inf at market {m.PortId} good {g}");
                Assert.InRange(m.Price[g], 0.001,
                    Market.InitialPrice(eco, (GoodId)g) * 100.0 + 1e-9);
                double asks = BookOps.AskQty(state, m.PortId, g);
                Assert.True(double.IsFinite(asks) && asks >= 0);
                double grade = BookOps.AskGrade(state, m.PortId, g);
                Assert.True(double.IsFinite(grade));
                Assert.InRange(grade, 0.0, 1.0);
            }
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void FortyEpochs_PopulationsStayBounded(ulong seed)
    {
        var state = Run(seed);
        var cap = state.Config.Expansion.SegmentCapPerTier;
        foreach (var port in state.Ports)
        {
            double total = 0;
            foreach (var s in state.Segments)
            {
                Assert.True(double.IsFinite(s.Size) && s.Size >= 0);
                Assert.InRange(s.SoL, 0.0, 1.0);
                Assert.True(double.IsFinite(s.Wealth));
                if (s.PortId == port.Id) total += s.Size;
            }
            Assert.True(total <= port.Tier * cap + 1e-6,
                $"port {port.Id} holds {total} over its tier cap");
        }
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    [InlineData(1234ul)]
    public void FortyEpochs_CreditsConserveToTheMint(ulong seed)
    {
        // Per-currency conservation (currency-and-FX design, "Conservation &
        // determinism"). With real FX rates live, the old single lump sum of NATIVE
        // credits across currencies is no longer a conserved quantity — a recorded
        // conversion legitimately changes the native sum (X of A ↔ X·rate of B), so
        // MetricsOps.Money.Supply is now an informative display figure, not the
        // invariant. The invariant is N per-currency residuals, each zero on a real
        // history: every Currency grows only through its own declared mints
        // (sovereign + steady issuance INTO it) and every cross-currency move is a
        // transfer that nets across the CumulativeConvertedIn/Out pair. This is the
        // same bar the dedicated ConservationTests hold on seed 42 — checked here
        // across the full acceptance sweep (seeds 42/7/1234, radius 10).
        var state = Run(seed);
        Assert.True(state.Health.Rows.Count >= 10, "history too short");
        for (int i = 1; i < state.Health.Rows.Count; i++)
        {
            var row = state.Health.Rows[i];
            foreach (var cur in row.Currencies)
            {
                double scale = System.Math.Max(1.0, System.Math.Abs(cur.Supply));
                Assert.True(
                    System.Math.Abs(cur.Residual) <= 1.3e-9 * scale,
                    $"seed {seed} epoch {row.Epoch} currency {cur.CurrencyId}: "
                    + $"residual {cur.Residual:G6} on supply {cur.Supply:G6} — "
                    + "an unknown mint or leak in this currency");
            }
        }
    }
}
