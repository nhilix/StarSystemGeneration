using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ExpansionTests
{
    [Fact]
    public void FullRun_EstablishesColonyPortsBeyondHomeworlds()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        int homeworlds = state.Actors.Count;
        Assert.True(state.Ports.Count > homeworlds,
            $"expected colonies beyond {homeworlds} homeworld ports, got {state.Ports.Count}");
        Assert.Contains(state.Log.Events, e => e.Type == WorldEventType.PortEstablished);
    }

    [Fact]
    public void ColonyPorts_WithinReach_OnePortPerHex_SegmentSeeded()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        var cfg = state.Config;
        // one port per hex, ever
        Assert.Equal(state.Ports.Count, state.Ports.Select(p => p.Hex).Distinct().Count());
        foreach (var e in state.Log.Events.Where(e => e.Type == WorldEventType.PortEstablished))
        {
            var payload = Assert.IsType<PortEstablishedPayload>(e.Payload);
            var port = state.Ports[payload.PortId];
            Assert.Equal(e.Location, port.Hex);
            // schisms (slice G) and mergers (slice H: federation, vassal
            // absorption) may since have transferred sovereignty
            if (!state.Actors[e.Actors[0]].Retired
                && !state.Log.Events.Any(ev =>
                    ev.Type == WorldEventType.SchismDeclared
                    && ev.Actors.Contains(e.Actors[0])))
                Assert.Equal(e.Actors[0], port.OwnerActorId);
            // founded at tier 1 (may be raised later; founding year pins the record)
            Assert.Equal(e.WorldYear, port.FoundedYear);
            // reachable from some port that existed by then (the founder's
            // at the time — sovereignty may have moved since)
            Assert.Contains(state.Ports, o => o.Id != port.Id
                && o.FoundedYear <= port.FoundedYear
                && HexGrid.Distance(o.Hex, port.Hex) <= cfg.Expansion.ColonizationReachHexes);
            // a colony population segment administers under the new port
            Assert.Contains(state.Segments, s => s.PortId == port.Id);
            // never founded on a void cell
            Assert.False(state.Skeleton.CellForHex(port.Hex).IsVoid);
        }
    }

    [Fact]
    public void Candidates_DeterministicAndReachBound()
    {
        var (_, s1) = EpochTestKit.Seeded();
        var (_, s2) = EpochTestKit.Seeded();
        var e1 = new EpochEngine(); var e2 = new EpochEngine();
        for (int i = 0; i < 3; i++) { e1.Step(s1); e2.Step(s2); }
        foreach (var a in s1.Actors.Where(a => a.Entered))
        {
            var c1 = ColonyValuation.CandidatesFor(s1, a.Id);
            var c2 = ColonyValuation.CandidatesFor(s2, a.Id);
            Assert.Equal(c1.Select(c => (c.Target, c.Score)),
                         c2.Select(c => (c.Target, c.Score)));
            var ports = s1.Ports.Where(p => p.OwnerActorId == a.Id).ToList();
            foreach (var c in c1)
            {
                Assert.True(ports.Any(p => HexGrid.Distance(p.Hex, c.Target)
                    <= s1.Config.Expansion.ColonizationReachHexes), "candidate beyond reach");
                Assert.False(s1.Skeleton.CellForHex(c.Target).IsVoid);
            }
        }
    }

    [Fact]
    public void Registries_ByteIdenticalAcrossRuns()
    {
        var (_, s1) = EpochTestKit.Seeded();
        var (_, s2) = EpochTestKit.Seeded();
        new EpochEngine().Run(s1);
        new EpochEngine().Run(s2);
        Assert.Equal(s1.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)),
                     s2.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)));
        Assert.Equal(s1.Segments.Select(s => (s.Id, s.PortId, s.SpeciesId, s.Size)),
                     s2.Segments.Select(s => (s.Id, s.PortId, s.SpeciesId, s.Size)));
    }
}
