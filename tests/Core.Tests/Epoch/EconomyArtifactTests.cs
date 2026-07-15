using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 7 — artifact v2: the economy is state, so the
/// artifact carries it. Config v2 (all knob families), actors v2 (standing
/// policies + credits), segments v2 (identity layers), and the appended
/// markets layer (markets, cultures, reserves, loans).</summary>
public class EconomyArtifactTests
{
    private static SimState Run(int epochs = 12)
    {
        var state = EpochTestKit.Seeded(42, 10).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static SimState Reload(SimState state) =>
        ArtifactSerializer.Load(new StringReader(ArtifactSerializer.ToText(state)));

    [Fact]
    public void MarketState_RoundTrips()
    {
        var built = Run();
        Assert.True(built.Markets.Count > 0);
        var loaded = Reload(built);
        Assert.Equal(built.Markets.Count, loaded.Markets.Count);
        for (int m = 0; m < built.Markets.Count; m++)
            for (int g = 0; g < Goods.All.Count; g++)
            {
                Assert.Equal(built.Markets[m].Price[g], loaded.Markets[m].Price[g]);
                Assert.Equal(built.Markets[m].LastCleared[g],
                             loaded.Markets[m].LastCleared[g]);
                Assert.Equal(built.Markets[m].BlackBookDemand[g],
                             loaded.Markets[m].BlackBookDemand[g]);
            }
        // the shelf lives on the orders layer now: the book round-trips
        Assert.Equal(built.Orders.Count, loaded.Orders.Count);
        for (int i = 0; i < built.Orders.Count; i++)
        {
            Assert.Equal(built.Orders[i].QtyRemaining,
                         loaded.Orders[i].QtyRemaining);
            Assert.Equal(built.Orders[i].LimitPrice,
                         loaded.Orders[i].LimitPrice);
        }
    }

    [Fact]
    public void SegmentIdentityLayers_RoundTrip()
    {
        var built = Run();
        var loaded = Reload(built);
        Assert.Equal(built.Segments.Count, loaded.Segments.Count);
        for (int i = 0; i < built.Segments.Count; i++)
        {
            var b = built.Segments[i];
            var l = loaded.Segments[i];
            Assert.Equal(b.CultureId, l.CultureId);
            Assert.Equal(b.SoL, l.SoL);
            Assert.Equal(b.Wealth, l.Wealth);
            Assert.Equal(b.Ideology, l.Ideology);
        }
        Assert.Equal(built.Cultures.Count, loaded.Cultures.Count);
    }

    [Fact]
    public void PoliciesAndCredits_RoundTrip()
    {
        var built = Run();
        var loaded = Reload(built);
        for (int i = 0; i < built.Polities.Count; i++)
            Assert.Equal(built.Polities[i].Credits, loaded.Polities[i].Credits);
        foreach (var actor in built.Actors)
        {
            if (actor.Policies is not PolityPolicies p) continue;
            var l = (PolityPolicies)loaded.Actors[actor.Id].Policies!;
            Assert.Equal(p.TaxRate, l.TaxRate);
            Assert.Equal(p.Budget, l.Budget);
            Assert.Equal(p.LawCode.Count, l.LawCode.Count);
            foreach (var kv in p.LawCode)
                Assert.Equal(kv.Value, l.LawCode[kv.Key]);
            foreach (var kv in p.StockpileTargets)
                Assert.Equal(kv.Value, l.StockpileTargets[kv.Key], 12);
        }
    }

    [Fact]
    public void StockpilesAndLoans_RoundTrip()
    {
        var built = Run();
        // force some located stock whatever the run produced (markets v2:
        // STOCK lines replace the polity-aggregate RESERVE pool, spec §4b)
        built.Ports[0].StockQty[(int)GoodId.Provisions] = 12.5;
        built.Ports[0].StockGrade[(int)GoodId.Provisions] = 0.44;
        built.Loans.Add(new Loan(built.Loans.Count, 0, 1, 77.0, 0.02, 50, 250)
        { Closed = false });

        var loaded = Reload(built);

        Assert.Equal(12.5, loaded.Ports[0].StockQty[(int)GoodId.Provisions]);
        Assert.Equal(0.44, loaded.Ports[0].StockGrade[(int)GoodId.Provisions]);
        var loan = loaded.Loans[loaded.Loans.Count - 1];
        Assert.Equal(77.0, loan.Principal);
        Assert.Equal(0.02, loan.RatePerYear);
        Assert.Equal(50, loan.TermYears);
        Assert.False(loan.Closed);
    }

    [Fact]
    public void Loan_OriginalPrincipal_SurvivesRoundTripAfterCapitalization()
    {
        var built = Run();
        // simulate capitalized interest growing Principal past OriginalPrincipal
        // (markets v4: OriginalPrincipal rides the LOAN line as its own field so
        // the capitalization ceiling's fixed reference point isn't reset by a
        // reload — without it, an already-over-ceiling loan gets fresh runway
        // every time the artifact round-trips)
        var loan = new Loan(built.Loans.Count, 0, 1, 77.0, 0.02, 50, 250)
        { Closed = false };
        loan.Principal = 140.0;
        built.Loans.Add(loan);

        var loaded = Reload(built);

        var reloadedLoan = loaded.Loans[loaded.Loans.Count - 1];
        Assert.Equal(140.0, reloadedLoan.Principal);
        Assert.Equal(77.0, reloadedLoan.OriginalPrincipal);
        Assert.NotEqual(reloadedLoan.Principal, reloadedLoan.OriginalPrincipal);
    }

    [Fact]
    public void CumulativeFiatIssued_RoundTrips()
    {
        var built = Run();
        // whatever the history minted survives as-is (clock v2 carries it)...
        Assert.Equal(built.CumulativeFiatIssued,
                     Reload(built).CumulativeFiatIssued);
        // ...and an explicit level survives too: the running total has no event
        // log to recompute from, so the CLOCK line must persist it directly
        built.CumulativeFiatIssued = 4242.5;
        Assert.Equal(4242.5, Reload(built).CumulativeFiatIssued);
    }

    [Fact]
    public void CumulativeSteadyIssuance_RoundTrips()
    {
        var built = Run();
        // whatever the history minted via the steady channel survives (clock v3)...
        Assert.Equal(built.CumulativeSteadyIssuance,
                     Reload(built).CumulativeSteadyIssuance);
        // ...and an explicit level persists on the CLOCK line, no event log to
        // recompute the third mint's running total from
        built.CumulativeSteadyIssuance = 1337.25;
        Assert.Equal(1337.25, Reload(built).CumulativeSteadyIssuance);
    }

    [Fact]
    public void NonDefaultEconomyConfig_RoundTrips()
    {
        var state = EpochTestKit.Seeded(42, 8).State;
        state.Config.Economy.LaborShare = 0.33;
        state.Config.Economy.PriceDriftMaxPerYear = 0.07;
        state.Config.Tech.BaseThreshold = 40;   // slice G: real tech knobs
        state.Config.Population.MigrationRatePerYear = 0.009;
        state.Config.Infrastructure.FacilitiesPerPortTier = 5;
        state.Config.Sim.EpochCount = 4;
        new EpochEngine().Run(state);

        var loaded = Reload(state);

        Assert.Equal(0.33, loaded.Config.Economy.LaborShare);
        Assert.Equal(0.07, loaded.Config.Economy.PriceDriftMaxPerYear);
        Assert.Equal(40, loaded.Config.Tech.BaseThreshold);
        Assert.Equal(0.009, loaded.Config.Population.MigrationRatePerYear);
        Assert.Equal(5, loaded.Config.Infrastructure.FacilitiesPerPortTier);
    }

    [Fact]
    public void LoadThenContinue_EqualsTheStraightRun()
    {
        // the strongest equivalence gate: step 8, save, load, step 4 more —
        // byte-identical to 12 straight
        var straight = Run(12);

        var half = Run(8);
        var resumed = Reload(half);
        resumed.Config.Sim.EpochCount = 12;
        new EpochEngine().Run(resumed);

        Assert.Equal(ArtifactSerializer.ToText(straight),
                     ArtifactSerializer.ToText(resumed));
    }
}
