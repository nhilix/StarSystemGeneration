using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using StarGen.Inspector;
using Xunit;

namespace StarGen.Core.Tests.Inspector;

/// <summary>The `domain &lt;port&gt;` REPL surface (domain-hex-expansion
/// design §6): satellite hexes with their facilities, and outposts with
/// their resident segments. Mirrors the MarketPanel/MarketView fixture
/// pattern — genesis seeds actors/species only, so ports/facilities/
/// segments/outposts are built by hand.</summary>
public class DomainViewTests
{
    [Fact]
    public void RendersSatelliteFacilitiesAndOutpostResidents()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        // a satellite hex one hop from the port hex — a facility here is a
        // satellite working (Stage 1), not the port's own industry.
        var satelliteHex = HexGrid.Neighbor(portHex, 0);
        var facility = new Facility(0, (int)InfraTypeId.Mine, 1, satelliteHex,
            port.OwnerActorId, (int)state.WorldYear);
        state.Facilities.Add(facility);

        // the settle election founds an outpost at that same worked hex
        // (Stage 2) and a resident segment relocates there.
        var outpost = new Outpost(0, "Testhaven", satelliteHex, port.Id,
            state.WorldYear);
        state.Outposts.Add(outpost);
        var seg = new PopulationSegment(0, port.Id, speciesId: 0, cultureId: 0,
            size: 5.0)
        { Hex = satelliteHex };
        state.Segments.Add(seg);

        var rendered = DomainView.Render(state, port.Id);

        Assert.Contains("Testhaven", rendered);          // the outpost
        Assert.Contains("#0", rendered);                  // segment/facility id
        Assert.Contains($"({satelliteHex.Q},{satelliteHex.R})", rendered);
        Assert.Contains("Mine", rendered);                // the satellite facility
        Assert.Contains("candidacy: (stage 3)", rendered);  // the labeled Stage-3 slot
    }

    [Fact]
    public void HandlesAnEmptyDomainWithoutThrowing()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 1, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        var rendered = DomainView.Render(state, port.Id);

        Assert.Contains("no satellite workings", rendered);
        Assert.Contains("no outposts yet", rendered);
    }

    [Fact]
    public void UnknownPortDoesNotThrow()
    {
        var (_, state) = EpochTestKit.Seeded();
        var rendered = DomainView.Render(state, 99);
        Assert.Contains("no port #99", rendered);
    }
}
