using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The frontier gate (domain-hex-expansion design §4, Stage 3): the
/// read-only anti-clustering predicate that decides which outposts may graduate
/// into starports. Headline anti-goal of the whole slice — <b>starports must
/// never end up founding adjacent to each other</b>. An outpost is frontier /
/// candidacy-eligible iff it sits at distance &gt; G from EVERY entered port,
/// where G = ServiceRadius(1) + ServiceRadius(port.Tier) + AstroRadiusBonus +
/// GraduationMarginHexes — the EncroachedPolities no-overlap geometry plus a
/// configured margin, so the gate scales with config, never an absolute
/// constant. Interior outposts (inside a domain) never graduate.</summary>
public class OutpostOpsTests
{
    // --- a clean single-actor world with no ports until the test adds them ---
    private static SimState World()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        return state;
    }

    private static Port AddPort(SimState state, int tier, int ownerActorId = 0)
    {
        var port = new Port(state.Ports.Count, ownerActorId,
            state.Actors[ownerActorId].Seat, tier, foundedYear: 0);
        state.Ports.Add(port);
        return port;
    }

    // an outpost `dist` hexes along the Q axis from `origin` (axial distance
    // with equal R is exactly |dq|), parented to `parentPortId`.
    private static Outpost OutpostAt(HexCoordinate origin, int dist,
                                     int parentPortId = 0, bool graduated = false)
        => new Outpost(0, "Testholm",
            new HexCoordinate(origin.Q + dist, origin.R), parentPortId, 0L)
        { Graduated = graduated };

    // G for a single-owner, era-standard-tech world (AstroRadiusBonus == 0):
    // ServiceRadius(1) + ServiceRadius(tier) + margin, all integer hexes.
    private static int G(SimState state, int tier)
        => PortDomains.ServiceRadius(state.Config, 1)
         + PortDomains.ServiceRadius(state.Config, tier)
         + state.Config.Expansion.GraduationMarginHexes;

    // ---------------------------------------------------------------------
    // THE anti-clustering test (headline): an outpost inside a port's domain
    // (distance < G) is NEVER frontier — asserted across several configs so the
    // gate is shown to scale with the service radii + margin, never a constant.
    // ---------------------------------------------------------------------

    [Theory]
    // tier-1 incumbent, default margin (G = 4 + 4 + 1 = 9)
    [InlineData(1, 1, 3, false)]   // deep interior
    [InlineData(1, 1, 9, false)]   // exactly at G — not > G, still interior
    [InlineData(1, 1, 10, true)]   // one past G — frontier
    // tier-2 incumbent, default margin (G = 4 + 8 + 1 = 13): its bigger domain
    // pushes the frontier out — a distance that was frontier at tier 1 is now
    // interior. The gate SCALES with config.
    [InlineData(2, 1, 9, false)]   // frontier under tier-1, interior under tier-2
    [InlineData(2, 1, 13, false)]  // exactly at G
    [InlineData(2, 1, 14, true)]   // one past G — frontier
    // tier-1 incumbent, widened margin (G = 4 + 4 + 3 = 11): the margin alone
    // moves the boundary — a distance that was frontier at margin 1 is interior.
    [InlineData(1, 3, 10, false)]  // frontier at margin 1, interior at margin 3
    [InlineData(1, 3, 12, true)]   // one past the widened G — frontier
    public void FrontierGate_ScalesWithConfig_NeverAnAbsoluteConstant(
        int tier, int margin, int dist, bool expectedFrontier)
    {
        var state = World();
        state.Config.Expansion.GraduationMarginHexes = margin;
        var port = AddPort(state, tier);
        Assert.Equal(G(state, tier),
            state.Config.Expansion.GraduationMarginHexes
            + PortDomains.ServiceRadius(state.Config, 1)
            + PortDomains.ServiceRadius(state.Config, tier));   // G bookkeeping

        var outpost = OutpostAt(port.Hex, dist);

        Assert.Equal(expectedFrontier, OutpostOps.IsFrontier(state, outpost));
    }

    [Fact]
    public void SameHex_FlipsFrontierToInterior_WhenTheIncumbentDomainGrows()
    {
        // one concrete demonstration that the verdict is not a fixed radius:
        // an outpost 11 hexes out is frontier beside a tier-1 port (G = 9),
        // interior beside a tier-2 one (G = 13) — nothing about the outpost
        // changed, only the incumbent's domain grew.
        var s1 = World();
        var p1 = AddPort(s1, tier: 1);
        Assert.True(OutpostOps.IsFrontier(s1, OutpostAt(p1.Hex, 11)));

        var s2 = World();
        var p2 = AddPort(s2, tier: 2);
        Assert.False(OutpostOps.IsFrontier(s2, OutpostAt(p2.Hex, 11)));
    }

    // ---------------------------------------------------------------------
    // A frontier outpost IS eligible.
    // ---------------------------------------------------------------------

    [Fact]
    public void FarFromEveryPort_IsFrontierEligible()
    {
        var state = World();
        var port = AddPort(state, tier: 1);
        var outpost = OutpostAt(port.Hex, G(state, 1) + 1);   // just past the gate
        Assert.True(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // The parent port counts — no special-casing. An outpost near its own
    // parent is interior; the parent gates it exactly like any other port.
    // ---------------------------------------------------------------------

    [Fact]
    public void NearItsOwnParent_IsInterior_ParentGetsNoExemption()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);
        // the ONLY port is the parent; the outpost sits within the parent's
        // gate distance. It must read interior — the parent is not exempted.
        var outpost = OutpostAt(parent.Hex, 3, parentPortId: parent.Id);
        Assert.False(OutpostOps.IsFrontier(state, outpost));

        // pushed past the parent's own gate (parent still the only port), the
        // same outpost becomes frontier — the parent is treated as any port,
        // not a free pass and not an automatic disqualifier.
        var farther = OutpostAt(parent.Hex, G(state, 1) + 1, parentPortId: parent.Id);
        Assert.True(OutpostOps.IsFrontier(state, farther));
    }

    [Fact]
    public void MustClearEveryPort_ASecondNearbyPortMakesItInterior()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);                 // id 0
        // clear of the parent...
        var outpost = OutpostAt(parent.Hex, G(state, 1) + 2, parentPortId: parent.Id);
        Assert.True(OutpostOps.IsFrontier(state, outpost));

        // ...but a second entered port sits right on the outpost's hex: now it
        // is inside THAT port's domain, so the gate (every port) fails.
        var neighbour = new Port(state.Ports.Count, 0, outpost.Hex, 1, 0);
        state.Ports.Add(neighbour);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // A graduated outpost is never a candidate.
    // ---------------------------------------------------------------------

    [Fact]
    public void GraduatedOutpost_IsNeverFrontier_EvenAtFrontierGeometry()
    {
        var state = World();
        var port = AddPort(state, tier: 1);
        // frontier geometry (far past the gate) but already graduated → false.
        var outpost = OutpostAt(port.Hex, G(state, 1) + 5, graduated: true);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // Only entered ports gate (mirrors EncroachedPolities' Entered guard).
    // ---------------------------------------------------------------------

    [Fact]
    public void UnenteredPort_DoesNotGate_ItProjectsNoDomain()
    {
        var state = World();
        // the sole port's owner has not entered → it projects no domain.
        state.Actors[0].Entered = false;
        var ghost = new Port(state.Ports.Count, 0, state.Actors[0].Seat, 3, 0);
        state.Ports.Add(ghost);
        var outpost = OutpostAt(ghost.Hex, 1);   // atop the un-entered port

        // no entered port anywhere → vacuously frontier.
        Assert.True(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // The companion status (drives the T3.3 REPL "dist vs G" display).
    // ---------------------------------------------------------------------

    [Fact]
    public void FrontierStatus_ReportsBindingPortDistance_Threshold_AndSlack()
    {
        var state = World();
        var port = AddPort(state, tier: 1);
        int g = G(state, 1);                       // 9 at defaults

        var interior = OutpostOps.FrontierStatus(state, OutpostAt(port.Hex, 5));
        Assert.False(interior.IsFrontier);
        Assert.Equal(5, interior.PortDistance);
        Assert.Equal(g, interior.Threshold);
        Assert.Equal(5 - g, interior.Slack);       // negative → interior

        var frontier = OutpostOps.FrontierStatus(state, OutpostAt(port.Hex, g + 2));
        Assert.True(frontier.IsFrontier);
        Assert.Equal(g + 2, frontier.PortDistance);
        Assert.Equal(g, frontier.Threshold);
        Assert.Equal(2, frontier.Slack);           // positive → frontier
    }

    [Fact]
    public void FrontierStatus_NoEnteredPort_IsVacuouslyFrontier()
    {
        var state = World();                       // no ports added
        var status = OutpostOps.FrontierStatus(state, OutpostAt(state.Actors[0].Seat, 3));
        Assert.True(status.IsFrontier);
        Assert.Equal(int.MaxValue, status.Slack);
    }
}
