using System.Linq;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>Slice AC: the domain-interior read-model query — the selection /
/// derivation lifted out of the Inspector's <c>DomainView</c> so the REPL and a
/// future Unity domain layer read ONE derivation (K3 parity). Satellite
/// workings (grouped, sorted), outposts with candidacy + residents, and the
/// settle/graduation event selection. Mirrors the MarketPanel fixture pattern —
/// genesis seeds actors/species only, so ports/facilities/segments/outposts are
/// built by hand.</summary>
public class DomainInteriorQueryTests
{
    private static EyeContext God(SimState s) => EyeContext.God(s.WorldYear);

    [Fact]
    public void OutOfRangePortReturnsNull()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.Null(DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 99));
        Assert.Null(DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), -1));
    }

    [Fact]
    public void HeaderCarriesPortIdentity()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.Equal(0, card.PortId);
        Assert.Equal(2, card.Tier);
        Assert.Equal(portHex, card.Hex);
        Assert.Equal(port.OwnerActorId, card.OwnerActorId);
        Assert.Equal(state.Actors[0].Name, card.OwnerName);
        Assert.Equal((int)state.WorldYear, card.FoundedYear);
    }

    [Fact]
    public void SatelliteWorkingsGroupedByHexSortedByQThenRFacilitiesById()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        // two satellite hexes; hexA has higher Q than hexB so sort must reorder.
        var hexA = new HexCoordinate(portHex.Q + 2, portHex.R);
        var hexB = new HexCoordinate(portHex.Q + 1, portHex.R);
        // facilities added out of id order across hexes; within hexB ids 2,1.
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, 1, hexA,
            port.OwnerActorId, (int)state.WorldYear));
        state.Facilities.Add(new Facility(2, (int)InfraTypeId.Refinery, 1, hexB,
            port.OwnerActorId, (int)state.WorldYear));
        state.Facilities.Add(new Facility(1, (int)InfraTypeId.Mine, 2, hexB,
            port.OwnerActorId, (int)state.WorldYear));
        // a facility ON the port hex must NOT appear as a satellite working.
        state.Facilities.Add(new Facility(3, (int)InfraTypeId.Depot, 1, portHex,
            port.OwnerActorId, (int)state.WorldYear));

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.Equal(2, card.SatelliteHexes.Count);
        // hexB (Q+1) sorts before hexA (Q+2)
        Assert.Equal(hexB, card.SatelliteHexes[0].Hex);
        Assert.Equal(hexA, card.SatelliteHexes[1].Hex);
        // within hexB, facilities by id: 1 then 2
        Assert.Equal(new[] { 1, 2 },
            card.SatelliteHexes[0].Facilities.Select(f => f.Id).ToArray());
        var row = card.SatelliteHexes[1].Facilities.Single();
        Assert.Equal(0, row.Id);
        Assert.Equal(Infrastructure.Get(InfraTypeId.Mine).Name, row.TypeName);
        Assert.Equal(1, row.Tier);
        Assert.True(row.Active);            // CommissionedYear >= 0
        Assert.Equal(1.0, row.Condition);
        Assert.True(row.Body.IsNone);
    }

    [Fact]
    public void UnderConstructionFacilityReadsInactive()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 1, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        var satHex = HexGrid.Neighbor(portHex, 0);
        var f = new Facility(0, (int)InfraTypeId.Mine, 1, satHex,
            port.OwnerActorId, (int)state.WorldYear)
        { CommissionedYear = -1 };          // still under construction
        state.Facilities.Add(f);

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.False(card.SatelliteHexes.Single().Facilities.Single().Active);
    }

    [Fact]
    public void OutpostsCarrySettledAndUnpeopledResidents()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        var settledHex = new HexCoordinate(portHex.Q + 1, portHex.R);
        var emptyHex = new HexCoordinate(portHex.Q - 1, portHex.R);
        state.Outposts.Add(new Outpost(0, "Peopled", settledHex, 0, state.WorldYear));
        state.Outposts.Add(new Outpost(1, "Empty", emptyHex, 0, state.WorldYear));
        // an outpost of a DIFFERENT port must be excluded.
        state.Outposts.Add(new Outpost(2, "Foreign", settledHex, 99, state.WorldYear));
        // resident at the peopled hex; a below-threshold segment is filtered out.
        state.Segments.Add(new PopulationSegment(0, 0, speciesId: 0, cultureId: 0,
            size: 5.0) { Hex = settledHex, SoL = 0.6 });
        state.Segments.Add(new PopulationSegment(1, 0, speciesId: 0, cultureId: 0,
            size: 0.0005) { Hex = settledHex, SoL = 0.6 });

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.Equal(new[] { 0, 1 }, card.Outposts.Select(o => o.Id).ToArray());
        var peopled = card.Outposts[0];
        var res = Assert.Single(peopled.Residents);
        Assert.Equal(0, res.SegmentId);
        Assert.Equal(0, res.SpeciesId);
        Assert.Equal(5.0, res.Size);
        Assert.Equal(0.6, res.SoL);
        Assert.Empty(card.Outposts[1].Residents);   // unpeopled
    }

    [Fact]
    public void CandidacyReadsInteriorFrontierAndGraduated()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int g = 1 + state.Config.Expansion.GraduationMarginHexes;   // G = 2

        // interior: dist 1 < G
        state.Outposts.Add(new Outpost(0, "Inner",
            new HexCoordinate(portHex.Q + 1, portHex.R), 0, state.WorldYear));
        // frontier: dist == G
        state.Outposts.Add(new Outpost(1, "Fringe",
            new HexCoordinate(portHex.Q + g, portHex.R), 0, state.WorldYear));
        // graduated: promoted to a real port at its hex
        var gradHex = new HexCoordinate(portHex.Q - g - 1, portHex.R);
        state.Outposts.Add(new Outpost(2, "Born", gradHex, 0, state.WorldYear)
        { Graduated = true });
        state.Ports.Add(new Port(1, state.Actors[0].Id, gradHex, 1,
            (int)state.WorldYear + 5));
        state.Markets.Add(new Market(1, state.Config.Economy));

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        var inner = card.Outposts[0].Candidacy;
        Assert.Equal(DomainCandidacyKind.Interior, inner.Kind);
        Assert.Equal(1, inner.Standing.PortDistance);
        Assert.Equal(g, inner.Standing.Threshold);
        Assert.Equal(1 - g, inner.Standing.Slack);

        var fringe = card.Outposts[1].Candidacy;
        Assert.Equal(DomainCandidacyKind.Frontier, fringe.Kind);
        Assert.Equal(g, fringe.Standing.PortDistance);

        var born = card.Outposts[2].Candidacy;
        Assert.Equal(DomainCandidacyKind.Graduated, born.Kind);
        Assert.Equal(1, born.GraduatedPortId);
    }

    [Fact]
    public void CandidacyIsVacuouslyFrontierWithNoEnteredPort()
    {
        var (_, state) = EpochTestKit.Seeded();
        // no actor entered → no port core to clash with
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        state.Ports.Add(new Port(0, state.Actors[0].Id, portHex, 1,
            (int)state.WorldYear));
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Outposts.Add(new Outpost(0, "Lonely",
            new HexCoordinate(portHex.Q + 1, portHex.R), 0, state.WorldYear));

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.Equal(DomainCandidacyKind.FrontierNoPort,
            card.Outposts[0].Candidacy.Kind);
    }

    [Fact]
    public void EventSelectionPicksDomainSettlesAndGraduations()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var portHex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        var port = new Port(0, state.Actors[0].Id, portHex, 2, (int)state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));

        var settleHex = new HexCoordinate(portHex.Q + 1, portHex.R);
        state.Outposts.Add(new Outpost(0, "Inner", settleHex, 0, state.WorldYear));
        var gradHex = new HexCoordinate(portHex.Q - 3, portHex.R);
        state.Outposts.Add(new Outpost(1, "Born", gradHex, 0, state.WorldYear)
        { Graduated = true });
        state.Ports.Add(new Port(1, state.Actors[0].Id, gradHex, 1,
            (int)state.WorldYear + 5));
        state.Markets.Add(new Market(1, state.Config.Economy));
        // an outpost NOT in this domain — its settle must be excluded.
        state.Outposts.Add(new Outpost(2, "Alien", settleHex, 99, state.WorldYear));

        int owner = port.OwnerActorId;
        state.Log.Append(state.WorldYear, ClockStratum.Generational,
            WorldEventType.OutpostFounded, new[] { owner }, settleHex, 1.0, 1.0,
            EventVisibility.Regional,
            new OutpostFoundedPayload(state.Actors[owner].Name, 0));   // in domain
        state.Log.Append(state.WorldYear, ClockStratum.Generational,
            WorldEventType.OutpostFounded, new[] { owner }, settleHex, 1.0, 1.0,
            EventVisibility.Regional,
            new OutpostFoundedPayload(state.Actors[owner].Name, 2));   // foreign
        state.Log.Append(state.WorldYear + 5, ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { owner }, gradHex, 1.0, 1.0,
            EventVisibility.Public,
            new PortEstablishedPayload(state.Actors[owner].Name, 1));  // graduation

        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            God(state), 0)!;
        Assert.Equal(2, card.Events.Count);
        Assert.Equal(WorldEventType.OutpostFounded, card.Events[0].Type);
        Assert.Equal(0, ((OutpostFoundedPayload)card.Events[0].Payload!).OutpostId);
        Assert.Equal(WorldEventType.PortEstablished, card.Events[1].Type);
    }
}
