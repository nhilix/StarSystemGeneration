using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The frontier gate (domain-hex-expansion design §4, Stage 3): the
/// read-only anti-adjacency predicate that decides which outposts may graduate
/// into starports. Headline anti-goal of the whole slice — <b>starports must
/// never end up founding adjacent to each other</b>. Graduation is
/// <b>densification, not a reach leap</b>: an outpost is frontier /
/// candidacy-eligible iff it sits at distance &gt;= G from EVERY entered port
/// <i>core</i>, where <c>G = ServiceRadius(1) + GraduationMarginHexes</c> — the
/// <b>newcomer's own tier-1 reach</b> plus a configured margin, UNIFORM across
/// every port and INDEPENDENT of the incumbent's tier. It is a pure
/// anti-adjacency spacing (no two port cores within a tier-1 reach), NOT the
/// EncroachedPolities domain-exclusion sum. An outpost within G of any core is
/// interior and never graduates; a fringe outpost of a tier-2+ domain (inside
/// that domain, yet &gt;= G from its core) densifies.</summary>
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

    // a port at an arbitrary hex (for placing a distant foreign core).
    private static Port AddPortAt(SimState state, int tier, HexCoordinate hex,
                                  int ownerActorId)
    {
        var port = new Port(state.Ports.Count, ownerActorId, hex, tier,
                            foundedYear: 0);
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

    // G for an era-standard-tech world (AstroRadiusBonus == 0): the newcomer's
    // OWN tier-1 service radius + margin — INDEPENDENT of any incumbent tier.
    private static int G(SimState state)
        => PortDomains.ServiceRadius(state.Config, 1)
         + state.Config.Expansion.GraduationMarginHexes;

    // ---------------------------------------------------------------------
    // THE anti-adjacency test (headline): an outpost within G of a port core
    // (distance < G) is NEVER frontier, at/beyond G it IS — asserted across
    // several configs so the gate is shown to scale with the MARGIN, never a
    // constant, AND to be INVARIANT to the incumbent's tier (the correction:
    // G is the newcomer's own reach, not the incumbent's domain sum).
    // ---------------------------------------------------------------------

    [Theory]
    // tier-1 incumbent, default margin (G = 4 + 1 = 5)
    [InlineData(1, 1, 4, false)]   // one short of G — interior
    [InlineData(1, 1, 5, true)]    // exactly at G — eligible (>= G)
    [InlineData(1, 1, 6, true)]    // past G — frontier
    // widened margin (G = 4 + 3 = 7): the margin alone moves the boundary —
    // a distance that was frontier at margin 1 is interior at margin 3.
    [InlineData(1, 3, 6, false)]   // frontier at margin 1, interior at margin 3
    [InlineData(1, 3, 7, true)]    // exactly at the widened G — frontier
    // tier-2 incumbent, default margin: G is UNCHANGED at 5 — the newcomer's
    // own reach, NOT the incumbent's bigger domain. Under the OLD (wrong) gate
    // a tier-2 incumbent pushed the boundary to 13; the corrected gate does not
    // move. dist 5 is INSIDE the tier-2 domain (radius 8) yet still eligible —
    // densification.
    [InlineData(2, 1, 4, false)]   // interior below the same G
    [InlineData(2, 1, 5, true)]    // at G — eligible though INSIDE the tier-2 domain
    public void FrontierGate_ScalesWithMargin_InvariantToIncumbentTier(
        int tier, int margin, int dist, bool expectedFrontier)
    {
        var state = World();
        state.Config.Expansion.GraduationMarginHexes = margin;
        var port = AddPort(state, tier);
        // G bookkeeping: it depends ONLY on the tier-1 radius + margin, never
        // on the incumbent's tier.
        Assert.Equal(PortDomains.ServiceRadius(state.Config, 1) + margin,
                     G(state));

        var outpost = OutpostAt(port.Hex, dist);
        Assert.Equal(expectedFrontier, OutpostOps.IsFrontier(state, outpost));
    }

    [Fact]
    public void IncumbentTier_DoesNotMoveTheGate_OnlyTheNewcomersReachCounts()
    {
        // the correction, made concrete: an outpost 5 hexes out is frontier
        // beside a tier-1 port AND still frontier beside a tier-2 port — the
        // incumbent's larger domain does NOT push the boundary out (as the old
        // EncroachedPolities-sum gate wrongly did). Only the newcomer's own
        // tier-1 reach + margin (G = 5) counts, uniform across tiers.
        var s1 = World();
        var p1 = AddPort(s1, tier: 1);
        Assert.True(OutpostOps.IsFrontier(s1, OutpostAt(p1.Hex, 5)));

        var s2 = World();
        var p2 = AddPort(s2, tier: 2);
        Assert.True(OutpostOps.IsFrontier(s2, OutpostAt(p2.Hex, 5)));
    }

    // ---------------------------------------------------------------------
    // Densification works: a FRINGE outpost of a tier-2+ domain — inside the
    // parent's domain, past G from its core — IS eligible. This is the case the
    // OLD (domain-exclusion) gate wrongly rejected: it demanded the outpost sit
    // beyond the parent's whole domain, which an in-domain outpost never can.
    // ---------------------------------------------------------------------

    [Fact]
    public void FringeOutpostOfATier2Domain_IsEligible_InsideTheParentDomain()
    {
        var state = World();
        var parent = AddPort(state, tier: 2);          // ServiceRadius(2) = 8
        int g = G(state);                              // 5 at defaults
        int parentRadius = PortDomains.ServiceRadius(state.Config, 2);
        // dist 6: past G (>= 5) yet still WITHIN the parent's domain (<= 8) —
        // the far reach of a big domain, its densifying second center.
        var fringe = OutpostAt(parent.Hex, 6, parentPortId: parent.Id);
        // dist 6 lies in [G=5, parentRadius=8]: at/past the gate, yet within the
        // parent's service reach — inside the parent's domain, not beyond it.
        Assert.True(g <= 6 && 6 <= parentRadius,
            $"6 must lie in [G={g}, parentRadius={parentRadius}]");
        Assert.True(HexGrid.Distance(parent.Hex, fringe.Hex) <= parentRadius,
            "the fringe outpost must still be within the parent's service reach");
        Assert.True(OutpostOps.IsFrontier(state, fringe));
    }

    // ---------------------------------------------------------------------
    // A frontier outpost IS eligible; interior stays subordinate.
    // ---------------------------------------------------------------------

    [Fact]
    public void FarFromEveryPort_IsFrontierEligible()
    {
        var state = World();
        var port = AddPort(state, tier: 1);
        var outpost = OutpostAt(port.Hex, G(state) + 1);   // past the gate
        Assert.True(OutpostOps.IsFrontier(state, outpost));
    }

    [Fact]
    public void NearAPortCore_IsInterior_SubordinateDensity()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);
        // the outpost sits within the parent's gate distance (dist < G). It must
        // read interior — permanently subordinate, never a candidate.
        var outpost = OutpostAt(parent.Hex, 3, parentPortId: parent.Id);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // The parent port counts — no special-casing.
    // ---------------------------------------------------------------------

    [Fact]
    public void ParentPortCounts_NoExemption_AndNoAutomaticDisqualification()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);
        // near the parent (the only port): interior — the parent is not exempt.
        Assert.False(OutpostOps.IsFrontier(state,
            OutpostAt(parent.Hex, 3, parentPortId: parent.Id)));
        // pushed past G from the parent (still the only port): frontier — the
        // parent gates like any port, not a free pass and not an auto-fail.
        Assert.True(OutpostOps.IsFrontier(state,
            OutpostAt(parent.Hex, G(state) + 1, parentPortId: parent.Id)));
    }

    [Fact]
    public void MustClearEveryPort_ASecondNearbyPortMakesItInterior()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);                 // id 0
        // clear of the parent...
        var outpost = OutpostAt(parent.Hex, G(state) + 2, parentPortId: parent.Id);
        Assert.True(OutpostOps.IsFrontier(state, outpost));

        // ...but a second entered port sits right on the outpost's hex: now it
        // is within THAT core's G, so the gate (every port) fails.
        var neighbour = new Port(state.Ports.Count, 0, outpost.Hex, 1, 0);
        state.Ports.Add(neighbour);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // Foreign-domain graduation is ALLOWED: an outpost >= G from every core,
    // but INSIDE a foreign polity's larger domain, is eligible. The gate
    // excludes no domain (the diplomacy is priced by the tension bump, not
    // forbidden here).
    // ---------------------------------------------------------------------

    [Fact]
    public void ForeignDomainOutpost_IsEligible_WhenClearOfEveryCore()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);                 // actor 0, id 0
        state.Actors[1].Entered = true;
        // a distant, large foreign domain (tier-3, ServiceRadius = 12).
        var foreign = AddPortAt(state, tier: 3,
            new HexCoordinate(parent.Hex.Q + 20, parent.Hex.R), ownerActorId: 1);
        int foreignRadius = PortDomains.ServiceRadius(state.Config, 3);   // 12

        // the outpost sits 6 hexes from the foreign core — INSIDE its big domain
        // (<= 12) yet >= G (5) from it; and 14 hexes from the parent (>= G).
        var outpost = OutpostAt(foreign.Hex, -6, parentPortId: parent.Id);
        Assert.Equal(6, HexGrid.Distance(foreign.Hex, outpost.Hex));
        Assert.True(6 <= foreignRadius,                       // inside foreign domain
            "the outpost must sit inside the foreign domain");
        Assert.True(HexGrid.Distance(parent.Hex, outpost.Hex) >= G(state));

        Assert.True(OutpostOps.IsFrontier(state, outpost));   // allowed, not forbidden
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
        var outpost = OutpostAt(port.Hex, G(state) + 5, graduated: true);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // Only entered ports gate (mirrors EncroachedPolities' Entered guard).
    // ---------------------------------------------------------------------

    [Fact]
    public void UnenteredPort_DoesNotGate_ItProjectsNoCore()
    {
        var state = World();
        // the sole port's owner has not entered → it projects no core.
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
        int g = G(state);                          // 5 at defaults

        var interior = OutpostOps.FrontierStatus(state, OutpostAt(port.Hex, 3));
        Assert.False(interior.IsFrontier);
        Assert.Equal(3, interior.PortDistance);
        Assert.Equal(g, interior.Threshold);
        Assert.Equal(3 - g, interior.Slack);       // negative → interior

        var frontier = OutpostOps.FrontierStatus(state, OutpostAt(port.Hex, g + 2));
        Assert.True(frontier.IsFrontier);
        Assert.Equal(g + 2, frontier.PortDistance);
        Assert.Equal(g, frontier.Threshold);
        Assert.Equal(2, frontier.Slack);           // positive → frontier
    }

    [Fact]
    public void FrontierStatus_BindingPortIsTheNearestCore()
    {
        var state = World();
        var near = AddPort(state, tier: 1);                   // id 0, at seat
        // a second, farther port — the status must bind on the NEAREST core.
        var far = new Port(state.Ports.Count, 0,
            new HexCoordinate(near.Hex.Q + 40, near.Hex.R), 1, 0);
        state.Ports.Add(far);

        var outpost = OutpostAt(near.Hex, 3);                 // 3 from near, 37 from far
        var status = OutpostOps.FrontierStatus(state, outpost);
        Assert.Equal(3, status.PortDistance);                 // the nearest, not 37
        Assert.False(status.IsFrontier);
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
