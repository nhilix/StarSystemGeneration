using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EpochGenesisTests
{
    [Fact]
    public void SeedsOnePolityPerHomeworldAnchor_AtAnchorHexes()
    {
        var (sk, state) = EpochTestKit.Seeded();
        var anchors = sk.Cells.SelectMany(c => c.Anchors)
                              .Where(a => a.Type == AnchorType.Homeworld).ToList();
        Assert.True(anchors.Count >= 2, "seeding pass should place at least two homeworlds");
        Assert.Equal(anchors.Count, state.Actors.Count);
        Assert.All(state.Actors, a => Assert.Equal(ActorKind.Polity, a.Kind));
        Assert.Equal(anchors.Select(a => a.Hex), state.Actors.Select(a => a.Seat));
        // polity records pair actors with their founding species, actor-id order
        Assert.Equal(state.Actors.Count, state.Polities.Count);
        Assert.Equal(anchors.Select(a => a.SpeciesId), state.Polities.Select(p => p.SpeciesId));
        Assert.Equal(state.Actors.Select(a => a.Id), state.Polities.Select(p => p.ActorId));
        // named for their species
        Assert.Equal(anchors.Select(a => sk.Species[a.SpeciesId].Name),
                     state.Actors.Select(a => a.Name));
    }

    [Fact]
    public void EntryEpochs_Staggered_Deterministic()
    {
        var (_, s1) = EpochTestKit.Seeded();
        var (_, s2) = EpochTestKit.Seeded();
        Assert.Equal(s1.Actors.Select(a => a.EntryEpoch), s2.Actors.Select(a => a.EntryEpoch));
        int window = s1.Config.Genesis.EmergenceWindowYears / s1.Config.Sim.YearsPerEpoch;
        Assert.All(s1.Actors, a => Assert.InRange(a.EntryEpoch, 0, window));
    }

    [Fact]
    public void Entry_FoundsHomeworldPortAndSegment()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        Assert.All(state.Actors, a => Assert.True(a.Entered));
        foreach (var a in state.Actors)
        {
            var home = state.Ports.First(p => p.OwnerActorId == a.Id);
            Assert.Equal(a.Seat, home.Hex);
            Assert.Equal(state.Config.Infrastructure.HomeworldPortTier, home.Tier);
            Assert.Equal(a.EntryEpoch * state.Config.Sim.YearsPerEpoch, home.FoundedYear);
            var seg = Assert.Single(state.Segments, s => s.PortId == home.Id);
            Assert.Equal(state.PolityOf(a.Id).SpeciesId, seg.SpeciesId);
        }
    }

    [Fact]
    public void StateCarriesTheNaturalRaster()
    {
        var (sk, state) = EpochTestKit.Seeded();
        Assert.Same(sk, state.Skeleton);
        Assert.Empty(state.Ports);        // nothing founded before the first step
        Assert.Empty(state.Facilities);
        Assert.Empty(state.Fleets);
        Assert.Empty(state.Lanes);
    }
}
