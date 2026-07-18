using System.Linq;
using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Wage redirect (domain-hex-expansion §3, conservation flow #2):
/// a sale's PRODUCTION wages split across the port's segments weighted by each
/// segment's aggregate weighted-staffing contribution to the port's producing
/// facilities, so a satellite hex's resident captures the wages its local works
/// throw off while distant port households capture the port facilities'. Moves
/// WHERE credits land, never how many — the split sums to the input wage by
/// construction. Only SettleSale's production wages route here; construction /
/// habitat / refund wages keep the plain size-pro-rata PayWages.</summary>
public class WageRedirectTests
{
    // A producing (Mine) facility at the given hex, active this year.
    private static Facility Mine(SimState state, HexCoordinate hex, int ownerId) =>
        new Facility(state.Facilities.Count, (int)InfraTypeId.Mine, 1, hex,
                     ownerId, 0);

    [Fact]
    public void SatelliteResident_CapturesMoreThanRawSizeShare()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var satHex = new HexCoordinate(portHex.Q + 3, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        // one producing facility, sited at the satellite hex
        state.Facilities.Add(Mine(state, satHex, a0.Id));

        // resident of the satellite hex (crews the working at full weight) and a
        // distant port household (crews it weakly) — EQUAL size, so a raw
        // size-pro-rata split would hand each exactly half.
        var resident = new PopulationSegment(0, port.Id, 0, 0, 1.0) { Hex = satHex };
        var household = new PopulationSegment(1, port.Id, 0, 0, 1.0) { Hex = portHex };
        state.Segments.Add(resident);
        state.Segments.Add(household);

        MarketEngine.PayProductionWages(state, port.Id, 100.0);

        Assert.True(resident.Wealth > 50.0,
            "the resident staffing the satellite working should out-earn a raw size split");
        Assert.True(household.Wealth < 50.0,
            "the distant port household crews the satellite working only weakly");
        Assert.Equal(100.0, resident.Wealth + household.Wealth, 9);   // conserved
    }

    [Fact]
    public void ResidentLessSatelliteWorking_StillPaysCommutingPortHouseholds()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var satHex = new HexCoordinate(portHex.Q + 3, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Facilities.Add(Mine(state, satHex, a0.Id));

        // no resident at the satellite hex — only commuting port households
        var h0 = new PopulationSegment(0, port.Id, 0, 0, 2.0) { Hex = portHex };
        var h1 = new PopulationSegment(1, port.Id, 0, 0, 1.0) { Hex = portHex };
        state.Segments.Add(h0);
        state.Segments.Add(h1);

        MarketEngine.PayProductionWages(state, port.Id, 90.0);

        // the working's wages still reach the commuting households (Stage-1
        // behavior): both share the same port hex, so the split reduces to size
        // pro-rata — nothing is lost, nothing reverts to the owner.
        Assert.True(h0.Wealth > 0 && h1.Wealth > 0);
        Assert.Equal(60.0, h0.Wealth, 9);           // 2/3 of 90 by size
        Assert.Equal(30.0, h1.Wealth, 9);           // 1/3 of 90 by size
        Assert.Equal(90.0, h0.Wealth + h1.Wealth, 9);
    }

    [Fact]
    public void Conservation_PeopledPort_SumsToWage()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var satHex = new HexCoordinate(portHex.Q + 2, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Facilities.Add(Mine(state, satHex, a0.Id));
        state.Facilities.Add(Mine(state, portHex, a0.Id));

        var segs = new[]
        {
            new PopulationSegment(0, port.Id, 0, 0, 1.5) { Hex = satHex },
            new PopulationSegment(1, port.Id, 0, 0, 2.0) { Hex = portHex },
            new PopulationSegment(2, port.Id, 0, 0, 0.5) { Hex = satHex },
        };
        foreach (var s in segs) state.Segments.Add(s);

        MarketEngine.PayProductionWages(state, port.Id, 137.0);

        Assert.Equal(137.0, segs.Sum(s => s.Wealth), 9);
    }

    [Fact]
    public void Conservation_UnpeopledPort_RevertsToOwner()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Facilities.Add(Mine(state, a0.Seat, a0.Id));   // producing, but no residents

        double before = state.PolityOf(a0.Id).Credits;
        MarketEngine.PayProductionWages(state, port.Id, 42.0);

        // no payroll → the sum reverts to the port's owner, never vanishes (P4)
        Assert.Equal(before + 42.0, state.PolityOf(a0.Id).Credits, 9);
    }

    [Fact]
    public void Conservation_ZeroStaffingWeight_FallsBackToSizeProRata()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        // a non-producing support facility only — the port has NO producing
        // facilities, so there is no staffing weight to split by
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Fortress, 1,
            portHex, a0.Id, 0));

        var s0 = new PopulationSegment(0, port.Id, 0, 0, 3.0) { Hex = portHex };
        var s1 = new PopulationSegment(1, port.Id, 0, 0, 1.0) { Hex = portHex };
        state.Segments.Add(s0);
        state.Segments.Add(s1);

        MarketEngine.PayProductionWages(state, port.Id, 80.0);

        // fallback to size pro-rata, conserved
        Assert.Equal(60.0, s0.Wealth, 9);
        Assert.Equal(20.0, s1.Wealth, 9);
        Assert.Equal(80.0, s0.Wealth + s1.Wealth, 9);
    }

    [Fact]
    public void ConstructionWages_StillSplitBySizeProRata()
    {
        // The non-production channel (construction / habitat / refund) must keep
        // its size-pro-rata behavior — PayWages is untouched by the redirect.
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var portHex = a0.Seat;
        var satHex = new HexCoordinate(portHex.Q + 3, portHex.R);
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        // even with a producing satellite facility present, PayWages ignores
        // staffing weight — it is the construction channel.
        state.Facilities.Add(Mine(state, satHex, a0.Id));

        var resident = new PopulationSegment(0, port.Id, 0, 0, 1.0) { Hex = satHex };
        var household = new PopulationSegment(1, port.Id, 0, 0, 1.0) { Hex = portHex };
        state.Segments.Add(resident);
        state.Segments.Add(household);

        MarketEngine.PayWages(state, port.Id, 100.0);

        // equal size → exact 50/50, regardless of where anyone lives
        Assert.Equal(50.0, resident.Wealth, 9);
        Assert.Equal(50.0, household.Wealth, 9);
    }
}
