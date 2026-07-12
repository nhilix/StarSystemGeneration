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
        var state = Run(seed);
        var eco = state.Config.Economy;
        // the mint fires once per schedule emergence — schism states (slice
        // G) split existing ledgers instead of minting
        double minted = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == StarGen.Core.Epoch.WorldEventType.PolityEmerged)
                minted += eco.InitialCreditsPerPolity
                          + state.Config.Expansion.HomeworldSegmentSize
                            * eco.InitialWealthPerPop;
        double held = 0;
        foreach (var p in state.Polities)
            held += p.Credits + p.ExpansionPoints + p.DevelopmentPoints
                    + p.MilitaryPoints + p.ReservePoints;
        foreach (var s in state.Segments)
            held += s.Wealth;
        foreach (var f in state.Factions)
            held += f.Wealth;   // appeasement is a flow, not a sink (slice G)
        foreach (var c in state.Corporations)
            held += c.Credits;  // corporate books are conserved too (slice G)
        // a colony expedition in flight carries the settlers' stake between
        // treasuries: ExpansionPoints was charged at dispatch, the colony
        // segment's Wealth is minted only on arrival (Task 9 — founding runs
        // in world-time), so the in-transit ColonyCost is held by the voyage
        foreach (var p in state.Projects)
            if (p.InFlight && p.Kind == StarGen.Core.Epoch.ProjectKind.ColonyExpedition)
                held += state.Config.Expansion.ColonyCost;
        Assert.True(minted > 0);
        Assert.Equal(minted, held, minted * 1e-9);
    }
}
