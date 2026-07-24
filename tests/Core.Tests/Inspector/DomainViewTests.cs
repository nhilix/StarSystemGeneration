using System.Text;
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
        state.Actors[0].Entered = true;
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
        // the outpost sits one hex from its own parent's core (dist 1 < G 2)
        // — interior, permanently subordinate.
        Assert.Contains("interior", rendered);
        Assert.Contains("subordinate", rendered);
    }

    [Fact]
    public void RendersCandidacyForInteriorFrontierAndGraduatedOutposts()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int g = 1 + state.Config.Expansion.GraduationMarginHexes;   // G = 2 (defaults)

        // interior: dist 1 < G — stacked on the parent core, never graduates.
        var interiorHex = new HexCoordinate(portHex.Q + 1, portHex.R);
        var interior = new Outpost(0, "Innerhold", interiorHex, port.Id,
            state.WorldYear);
        state.Outposts.Add(interior);

        // frontier: dist == G — candidacy-eligible (a genuine second centre).
        var frontierHex = new HexCoordinate(portHex.Q + g, portHex.R);
        var frontier = new Outpost(1, "Fringehold", frontierHex, port.Id,
            state.WorldYear);
        state.Outposts.Add(frontier);

        // graduated: already promoted into a real starport at its own hex.
        var gradHex = new HexCoordinate(portHex.Q - g - 1, portHex.R);
        var graduated = new Outpost(2, "Bornstead", gradHex, port.Id,
            state.WorldYear)
        { Graduated = true };
        state.Outposts.Add(graduated);
        var newPort = new Port(1, state.Actors[0].Id, gradHex, 1,
            (int)state.WorldYear + 5);
        state.Ports.Add(newPort);
        state.Markets.Add(new Market(1, state.Config.Economy));

        // events: a settle (OutpostFounded) for the interior outpost, and the
        // graduation's own PortEstablished at the new port's (== outpost's) hex.
        state.Log.Append(state.WorldYear, ClockStratum.Generational,
            WorldEventType.OutpostFounded, new[] { port.OwnerActorId }, interiorHex,
            1.0, 1.0, EventVisibility.Regional,
            new OutpostFoundedPayload(state.Actors[port.OwnerActorId].Name,
                interior.Id));
        state.Log.Append(state.WorldYear + 5, ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { newPort.OwnerActorId }, gradHex,
            1.0, 1.0, EventVisibility.Public,
            new PortEstablishedPayload(state.Actors[newPort.OwnerActorId].Name,
                newPort.Id));

        var rendered = DomainView.Render(state, port.Id);

        Assert.Contains($"interior — subordinate (dist 1 < G {g}", rendered);
        Assert.Contains($"frontier — eligible (dist {g} ≥ G {g}", rendered);
        Assert.Contains("graduated → port #1", rendered);
        Assert.Contains("outpost takes root (#0)", rendered);   // the settle event
        Assert.Contains("establishes a port (#1)", rendered);   // the graduation event
    }

    /// <summary>Slice AC parity guard: after the derivation moved into
    /// <c>DomainInteriorQuery</c>, <c>DomainView.Render</c> must still produce
    /// BYTE-IDENTICAL output. This pins the whole format (header, satellite
    /// block, outpost + candidacy + resident lines, events footer) against a
    /// deterministic hand-built domain, so any format drift on either side of
    /// the query boundary fails here.</summary>
    [Fact]
    public void RenderIsByteIdenticalToTheAuthoredFormat()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        var satHex = HexGrid.Neighbor(portHex, 0);
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, 1, satHex,
            port.OwnerActorId, (int)state.WorldYear));
        state.Outposts.Add(new Outpost(0, "Testhaven", satHex, 0, state.WorldYear));
        state.Segments.Add(new PopulationSegment(0, 0, speciesId: 0, cultureId: 0,
            size: 5.0) { Hex = satHex, SoL = 0.5 });

        int g = 1 + state.Config.Expansion.GraduationMarginHexes;
        string owner = state.Actors[0].Name;
        string mine = Infrastructure.Get(InfraTypeId.Mine).Name;
        string species = state.Skeleton.Species[0].Name;

        var e = new StringBuilder();
        e.AppendLine($"domain #0 — tier 2 port at ({portHex.Q},{portHex.R}), "
            + $"{owner}'s domain, founded y{(int)state.WorldYear}");
        e.AppendLine("satellite hexes:");
        e.AppendLine($"  ({satHex.Q},{satHex.R}):");
        e.AppendLine($"    #0 {mine} t1 — condition 1.00, body —");
        e.AppendLine("outposts:");
        e.AppendLine($"  #0 Testhaven at ({satHex.Q},{satHex.R}) — "
            + $"founded y{state.WorldYear}");
        e.AppendLine($"    candidacy: interior — subordinate (dist 1 < G {g}, "
            + $"slack {1 - g})");
        e.AppendLine($"    #0 {species} — size 5.00, SoL 0.50");
        e.AppendLine("events:");
        e.AppendLine("  (no settle or graduation events yet)");

        Assert.Equal(e.ToString(), DomainView.Render(state, 0));
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
        Assert.Contains("no settle or graduation events yet", rendered);
    }

    [Fact]
    public void UnknownPortDoesNotThrow()
    {
        var (_, state) = EpochTestKit.Seeded();
        var rendered = DomainView.Render(state, 99);
        Assert.Contains("no port #99", rendered);
    }
}
