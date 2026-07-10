using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EpochGenesisTests
{
    [Fact]
    public void SeedsOnePolityPerCurrentOrigin_InScheduleOrder()
    {
        var (sk, state) = EpochTestKit.Seeded();
        var current = sk.Origins.Where(o => o.Era == OriginEra.Current)
            .OrderBy(o => o.SpaceflightYear).ThenBy(o => o.Id).ToList();
        Assert.True(current.Count >= 2, "the schedule should carry at least two polities");
        Assert.Equal(current.Count, state.Actors.Count);
        Assert.All(state.Actors, a => Assert.Equal(ActorKind.Polity, a.Kind));
        // actor order == spaceflight-date order; seats are origin homeworlds
        Assert.Equal(current.Select(o => o.Hex), state.Actors.Select(a => a.Seat));
        for (int i = 1; i < state.Actors.Count; i++)
            Assert.True(state.Actors[i].EntryEpoch >= state.Actors[i - 1].EntryEpoch,
                "entry epochs follow the schedule");
        // polity records pair actors with their founding species; each seat's
        // homeworld anchor carries the same species id
        Assert.Equal(state.Actors.Count, state.Polities.Count);
        Assert.Equal(state.Actors.Select(a => a.Id), state.Polities.Select(p => p.ActorId));
        for (int i = 0; i < state.Actors.Count; i++)
        {
            var anchor = sk.CellForHex(state.Actors[i].Seat).Anchors
                .Single(a => a.Type == AnchorType.Homeworld
                             && a.Hex.Equals(state.Actors[i].Seat));
            Assert.Equal(anchor.SpeciesId, state.Polities[i].SpeciesId);
            Assert.Equal(sk.Species[anchor.SpeciesId].Name, state.Actors[i].Name);
        }
    }

    [Fact]
    public void EntryConditions_CarryMaturationAndContactBonus()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.All(state.Polities, p => Assert.InRange(p.EntryGradeBonus, 0.0, 0.15));
        if (state.Actors.Count >= 2)
        {
            // the latest emerger carries at least the contact share of the
            // earliest one's bonus deficit (latecomers are behind, not hopeless)
            var byEntry = state.Actors.OrderBy(a => a.EntryEpoch).ToList();
            if (byEntry[0].EntryEpoch != byEntry[byEntry.Count - 1].EntryEpoch)
                Assert.True(
                    state.Polities[byEntry[byEntry.Count - 1].Id].EntryGradeBonus
                    > state.Polities[byEntry[0].Id].EntryGradeBonus - 0.05,
                    "late emergers should not start strictly worse-equipped");
        }
    }

    [Fact]
    public void DeepTimeChronicle_SeedsTheLog()
    {
        var (sk, state) = EpochTestKit.Seeded();
        Assert.Equal(sk.DeepTimeEvents.Count,
            state.Log.Events.Count(e => e.Stratum is ClockStratum.Cosmic
                                        or ClockStratum.Evolutionary));
        Assert.True(state.Log.Events.Count > 0, "the deep chronicle is the log's floor");
        Assert.Equal(ClockStratum.Cosmic, state.Log.Events[0].Stratum);
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
        // entered on schedule — or retired since (slice H: mergers)
        Assert.All(state.Actors, a => Assert.True(a.Entered || a.Retired));
        foreach (var a in state.Actors)
        {
            // schedule entries only — schism states (slice G) are born of
            // existing ports, not homeworld foundings
            if (!state.Log.Events.Any(e =>
                    e.Type == WorldEventType.PolityEmerged
                    && e.Actors.Contains(a.Id))) continue;
            var home = state.Ports.First(p => p.Hex.Equals(a.Seat));
            // founded at HomeworldPortTier; development may have raised it since
            Assert.InRange(home.Tier, state.Config.Infrastructure.HomeworldPortTier,
                           state.Config.Infrastructure.MaxPortTier);
            Assert.Equal(a.EntryEpoch * state.Config.Sim.YearsPerEpoch, home.FoundedYear);
            // the founding population administers there (diasporas may too)
            Assert.Contains(state.Segments, s => s.PortId == home.Id
                && s.SpeciesId == state.PolityOf(a.Id).SpeciesId);
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
