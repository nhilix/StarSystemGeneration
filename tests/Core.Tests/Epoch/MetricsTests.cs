using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The sim-health probe (slice SH): pure read-only aggregation of
/// the money holder classes and macro state. The probe must never perturb —
/// the trace render is the witness.</summary>
public class MetricsTests
{
    [Fact]
    public void MoneyRowTracksEveryHolderClass()
    {
        var (_, state) = EpochTestKit.Seeded();
        var before = MetricsOps.Money(state, "test");

        var pr = state.Polities[0];
        pr.Credits += 100.0;
        pr.ExpansionPoints += 5.0;
        pr.DevelopmentPoints += 6.0;
        pr.MilitaryPoints += 7.0;
        pr.ReservePoints += 8.0;
        state.Segments.Add(new PopulationSegment(state.Segments.Count,
            portId: 0, speciesId: 0, cultureId: 0, size: 3.0)
        { Wealth = 25.0 });
        var corp = new Corporation(0, pr.ActorId, "Probe Test Combine",
            pr.ActorId, CorporateNiche.Freight, 0, state.WorldYear);
        state.Corporations.Add(corp);
        corp.Deposit(state, 40.0, 0);   // wallet is the corp's whole balance now
        var faction = new Faction(0, "Probe Merchants", pr.ActorId,
            FactionBasis.Corporate, state.WorldYear)
        { Wealth = 15.0 };
        state.Factions.Add(faction);
        state.Orders.Add(new MarketOrder(state.NextOrderId++, OrderSide.Buy,
            pr.ActorId, portId: 0, good: 0, limitPrice: 1.0,
            qtyRemaining: 9.0, grade: 0.0, escrowCredits: 9.0,
            postedYear: state.WorldYear, expiryYear: state.WorldYear + 100));
        state.Loans.Add(new Loan(0, pr.ActorId, pr.ActorId, 250.0,
            0.02, 50, state.WorldYear));
        state.Couriers.Add(new CourierContract(state.NextCourierId++,
            pr.ActorId, 0, 0, feeEscrow: 12.0, CourierPriority.Normal,
            state.WorldYear, state.WorldYear + 100));
        state.Projects.Add(new Project(state.Projects.Count,
            ProjectKind.ColonyExpedition, pr.ActorId, pr.ActorId, 0,
            new StarGen.Core.Model.HexCoordinate(0, 0), yearsRequired: 10,
            state.WorldYear));

        var after = MetricsOps.Money(state, "test");
        Assert.Equal(100.0, after.PolityCredits - before.PolityCredits, 9);
        Assert.Equal(26.0, after.PolityPools - before.PolityPools, 9);
        Assert.Equal(40.0, after.CorpCredits - before.CorpCredits, 9);
        Assert.Equal(25.0, after.SegmentWealth - before.SegmentWealth, 9);
        Assert.Equal(15.0, after.FactionWealth - before.FactionWealth, 9);
        Assert.Equal(9.0, after.OrderEscrow - before.OrderEscrow, 9);
        Assert.Equal(12.0, after.CourierEscrow - before.CourierEscrow, 9);
        Assert.Equal(state.Config.Expansion.ColonyCost,
            after.ExpeditionPurses - before.ExpeditionPurses, 9);
        Assert.Equal(250.0, after.LoanPrincipal - before.LoanPrincipal, 9);
        Assert.Equal(100.0 + 26.0 + 40.0 + 25.0 + 15.0 + 9.0 + 12.0
            + state.Config.Expansion.ColonyCost,
            after.Supply - before.Supply, 9);
        Assert.Equal("test", after.Phase);
        Assert.Equal(state.EpochIndex, after.Epoch);
    }

    [Fact]
    public void MoneyRowSkipsClosedLoans()
    {
        var (_, state) = EpochTestKit.Seeded();
        var before = MetricsOps.Money(state, "test");
        state.Loans.Add(new Loan(0, 0, 1, 250.0, 0.02, 50, state.WorldYear)
        { Closed = true });
        var after = MetricsOps.Money(state, "test");
        Assert.Equal(0.0, after.LoanPrincipal - before.LoanPrincipal, 9);
    }

    [Fact]
    public void SnapshotCountsNegativeTreasuriesAmongEnteredOnly()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.True(state.Polities.Count >= 3,
            "seed 42 radius 8 should seed several polities");
        state.Actors[state.Polities[0].ActorId].Entered = true;
        state.Polities[0].Credits = -50.0;
        state.Actors[state.Polities[1].ActorId].Entered = true;
        state.Polities[1].Credits = 10.0;
        // negative but NOT entered — must not count
        state.Actors[state.Polities[2].ActorId].Entered = false;
        state.Polities[2].Credits = -99.0;

        var row = MetricsOps.Snapshot(state);
        Assert.Equal(1, row.NegativeTreasuries);
        Assert.Equal(2, row.LivePolities);
        Assert.Equal(-50.0, row.MinPolityCredits, 9);
        Assert.Equal(10.0, row.MaxPolityCredits, 9);
        Assert.Equal(-20.0, row.MedianPolityCredits, 9);
    }

    [Fact]
    public void SnapshotAggregatesPopulationAndSoL()
    {
        var (_, state) = EpochTestKit.Seeded();
        double pop = 0, sol = 0;
        foreach (var s in state.Segments) { pop += s.Size; sol += s.SoL * s.Size; }
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(pop, row.Population, 9);
        Assert.Equal(pop <= 0 ? 0.0 : sol / pop, row.MeanSoL, 9);
    }

    [Fact]
    public void PolityRowsCoverEnteredPolities()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[0];
        state.Actors[pr.ActorId].Entered = true;
        pr.Credits = 77.0;
        var rows = MetricsOps.PolityRows(state);
        Assert.Contains(rows, r => r.ActorId == pr.ActorId
            && r.Credits == 77.0);
        foreach (var r in rows)
            Assert.True(state.Actors[r.ActorId].Entered);
    }

    [Fact]
    public void SnapshotCountsLivingOutpostsOnly()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex0 = new StarGen.Core.Model.HexCoordinate(1, 1);
        var hex1 = new StarGen.Core.Model.HexCoordinate(2, 2);
        state.Outposts.Add(new Outpost(0, "Firsthold", hex0, 0, state.WorldYear));
        state.Outposts.Add(new Outpost(1, "Graduated Reach", hex1, 0,
            state.WorldYear)
        { Graduated = true });

        var row = MetricsOps.Snapshot(state);

        Assert.Equal(1, row.Outposts);   // the graduated one no longer counts
        Assert.Equal(1.0,
            MetricRegistry.Find("Settlement.Outposts")!.Get(row), 9);
        Assert.Equal(1, row.GraduatedPorts);   // the graduated one counts here
        Assert.Equal(1.0,
            MetricRegistry.Find("Settlement.GraduatedPorts")!.Get(row), 9);
    }

    [Fact]
    public void ProbeNeverPerturbsTheState()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        // the artifact pins the ENTIRE state byte-for-byte — a probe
        // mutation anywhere shows, not just in the trace list
        string before = ArtifactSerializer.ToText(state);
        MetricsOps.Money(state, "probe");
        MetricsOps.Snapshot(state);
        MetricsOps.PolityRows(state);
        Assert.Equal(before, ArtifactSerializer.ToText(state));
    }
}
