using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 8 — natives and late emergence
/// (interpolity/relations.md §Natives): dates project onto the native
/// window; free space births a polity, claimed space resolves through the
/// host's policy — client vassal, member with a cultural minority, or a
/// suppressed emergence handing every rival a liberation casus belli.</summary>
public class NativeEmergenceTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int epochs)
    {
        state.Config.Sim.EpochCount = state.EpochIndex + epochs;
        new EpochEngine().Run(state);
    }

    /// <summary>Plant a synthetic pre-spaceflight origin at a hex and wind
    /// the native window so its date fires on the next step.</summary>
    private static SapientOrigin PlantNative(SimState state, HexCoordinate hex)
    {
        var origin = new SapientOrigin
        {
            Id = state.Skeleton.Origins.Count,
            CellCoord = HexGrid.CellOf(hex),
            Hex = hex,
            AbiogenesisYear = -4_000_000_000,
            SapienceYear = -1_000_000,
            SpaceflightYear = 100_000_000,
            Richness = 0.6,
            Era = OriginEra.PreSpaceflight,
        };
        state.Skeleton.Origins.Add(origin);
        // squeeze the native window right behind the polity window: every
        // native base date lands around epoch 20, well past by now
        state.Config.Genesis.NativeWindowYears =
            state.Config.Genesis.EmergenceWindowYears + 20;
        return origin;
    }

    /// <summary>The hex farthest from every port — free space that stays
    /// free through another epoch of expansion.</summary>
    private static HexCoordinate FreeHex(SimState state)
    {
        HexCoordinate best = default;
        int bestDist = -1;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            var hex = HexGrid.CellCenter(cell.Coord);
            int nearest = int.MaxValue;
            foreach (var p in state.Ports)
                nearest = System.Math.Min(nearest,
                    HexGrid.Distance(p.Hex, hex));
            if (nearest > bestDist) { bestDist = nearest; best = hex; }
        }
        Assert.True(NativeOps.HostOf(state, best) < 0,
            "no free space left in this galaxy");
        return best;
    }

    [Fact]
    public void FreeSpaceEmergence_BirthsAPolity()
    {
        var state = Run();
        var origin = PlantNative(state, FreeHex(state));
        int species = state.Skeleton.Species.Count;
        Continue(state, 1);
        Assert.True(origin.ResolvedEpoch >= 0, "the date must fire");
        Assert.True(state.Skeleton.Species.Count > species,
            "a native people is a new species");
        // the galaxy's real natives may have resolved alongside — find
        // the planted one by its homeworld
        var young = state.Actors.FirstOrDefault(a =>
            a.Kind == ActorKind.Polity && a.Seat.Equals(origin.Hex));
        Assert.NotNull(young);
        Assert.True(young!.Entered);
        Assert.Contains(state.Ports, p => p.Hex.Equals(origin.Hex)
            && p.OwnerActorId == young.Id);
        Assert.Contains(state.Log.Events, e =>
            e.Type == WorldEventType.PolityEmerged
            && e.Actors.Contains(young.Id));
    }

    [Fact]
    public void SuppressedEmergence_HandsRivalsALiberationCasusBelli()
    {
        var state = Run();
        // plant under a host with an exploiter's temperament forced on
        Port? hostPort = null;
        foreach (var rel in state.Relations)
        {
            foreach (var p in state.Ports)
                if (p.OwnerActorId == rel.PolityAId) { hostPort = p; break; }
            if (hostPort != null) break;
        }
        Assert.NotNull(hostPort);
        int host = hostPort!.OwnerActorId;
        var origin = PlantNative(state, hostPort.Hex);
        // force the standing policy: exploitation
        var actor = state.Actors[host];
        var policies = actor.Policies as PolityPolicies ?? PolityPolicies.Default;
        actor.Policies = policies with { NativePolicy = NativePolicy.Exploit };
        actor.Controller = new PinnedPolicyController(
            policies with { NativePolicy = NativePolicy.Exploit });

        Continue(state, 1);
        Assert.True(origin.ResolvedEpoch >= 0);
        Assert.Contains(state.Log.Events, e =>
            e.Type == WorldEventType.EmergenceSuppressed
            && e.Actors.Contains(host));
        // every rival with a relation gained a standing liberation claim
        RelationClaim? liberation = null;
        foreach (var rel in state.Relations)
        {
            if (!rel.Involves(host)) continue;
            foreach (var c in rel.Claims)
                if (!c.Released && c.Type == ClaimType.Liberation
                    && c.HolderPolityId == rel.OtherOf(host))
                    liberation = c;
        }
        Assert.NotNull(liberation);
        // and the natives live under the host as a captive segment of the
        // claimed culture
        Assert.Contains(state.Segments, s =>
            s.CultureId == liberation!.SubjectId
            && state.Ports[s.PortId].OwnerActorId == host);
    }

    [Fact]
    public void Integration_JoinsThemAsMembers()
    {
        var state = Run();
        Port? hostPort = null;
        foreach (var p in state.Ports)
            if (state.Actors[p.OwnerActorId].Entered) { hostPort = p; break; }
        int host = hostPort!.OwnerActorId;
        var origin = PlantNative(state, hostPort.Hex);
        var actor = state.Actors[host];
        var policies = actor.Policies as PolityPolicies ?? PolityPolicies.Default;
        actor.Controller = new PinnedPolicyController(
            policies with { NativePolicy = NativePolicy.Integrate });
        actor.Policies = policies with { NativePolicy = NativePolicy.Integrate };

        Continue(state, 1);
        Assert.True(origin.ResolvedEpoch >= 0);
        Assert.Contains(state.Log.Events, e =>
            e.Type == WorldEventType.NativesIntegrated
            && e.Actors.Contains(host));
        Assert.DoesNotContain(state.Staged, e =>
            e.Type == WorldEventType.EmergenceSuppressed);
    }

    [Fact]
    public void NativeStatus_RoundTrips()
    {
        var state = Run();
        var origin = PlantNative(state, FreeHex(state));
        Continue(state, 1);
        Assert.True(origin.ResolvedEpoch >= 0);
        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));
        Assert.Equal(origin.ResolvedEpoch,
            loaded.Skeleton.Origins[origin.Id].ResolvedEpoch);
        Assert.Equal(state.Skeleton.Species.Count,
            loaded.Skeleton.Species.Count);
        Assert.Equal(ArtifactSerializer.ToText(state),
                     ArtifactSerializer.ToText(loaded));
    }

    /// <summary>A controller that answers with fixed policies and no acts —
    /// pins a standing policy against the genesis AI's rewrites.</summary>
    private sealed class PinnedPolicyController : IController
    {
        private readonly PolityPolicies _policies;
        public PinnedPolicyController(PolityPolicies policies)
        { _policies = policies; }
        public ControllerDecision Decide(PerceptionView perceived) =>
            new ControllerDecision(_policies, new Act[0]);
    }
}
