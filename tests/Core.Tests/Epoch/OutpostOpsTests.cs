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
/// <i>core</i>, where <c>G = 1 + GraduationMarginHexes</c> (default margin 1 →
/// <b>G = 2</b>) — the <b>literal anti-adjacency</b> spacing, "not in a touching
/// hex" ⇔ <c>dist ≥ 2</c>, NOT any radius-derived / domain-scale distance
/// (decision #2 FINAL, 2026-07-20 — two earlier domain-scale gates blocked
/// graduation entirely because outposts form only 1–3 hexes from their parent).
/// A stacked-on-a-port outpost (dist 1) is interior and never graduates; a
/// second-centre outpost that has cleared every port by G hexes densifies —
/// even inside a larger tier-2+ domain. G is uniform across every port and
/// INDEPENDENT of the incumbent's tier.</summary>
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

    // G = the literal anti-adjacency spacing 1 + margin — INDEPENDENT of any
    // port's tier / service radius.
    private static int G(SimState state)
        => 1 + state.Config.Expansion.GraduationMarginHexes;

    // ---------------------------------------------------------------------
    // THE anti-adjacency test (headline): an outpost within G of a port core
    // (distance < G) is NEVER frontier, at/beyond G it IS — asserted across
    // several margins so the gate is shown to move with the MARGIN alone, AND
    // to be INVARIANT to the incumbent's tier (G is the literal spacing 1 +
    // margin, not the incumbent's domain size). At the defaults (margin 1 →
    // G = 2) a dist-1 "stacked on a port" outpost is interior and a dist-2
    // "genuine second centre" outpost is eligible — the crux of decision #2.
    // ---------------------------------------------------------------------

    [Theory]
    // tier-1 incumbent, default margin (G = 1 + 1 = 2)
    [InlineData(1, 1, 1, false)]   // dist 1 — touching a port, interior
    [InlineData(1, 1, 2, true)]    // dist 2 — exactly at G, eligible (>= G)
    [InlineData(1, 1, 3, true)]    // dist 3 — past G, frontier
    // widened margin (G = 1 + 2 = 3): the margin alone moves the boundary —
    // a distance that was frontier at margin 1 is interior at margin 2.
    [InlineData(1, 2, 2, false)]   // frontier at margin 1, interior at margin 2
    [InlineData(1, 2, 3, true)]    // exactly at the widened G — frontier
    // tier-2 incumbent, default margin: G is UNCHANGED at 2 — the literal
    // spacing, NOT the incumbent's bigger domain. dist 2 is INSIDE the tier-2
    // domain (radius 8) yet still eligible — densification, not a reach leap.
    [InlineData(2, 1, 1, false)]   // interior below the same G
    [InlineData(2, 1, 2, true)]    // at G — eligible though INSIDE the tier-2 domain
    public void FrontierGate_ScalesWithMargin_InvariantToIncumbentTier(
        int tier, int margin, int dist, bool expectedFrontier)
    {
        var state = World();
        state.Config.Expansion.GraduationMarginHexes = margin;
        var port = AddPort(state, tier);
        // G bookkeeping: it depends ONLY on the margin (1 + margin), never on
        // the incumbent's tier or service radius.
        Assert.Equal(1 + margin, G(state));

        var outpost = OutpostAt(port.Hex, dist);
        Assert.Equal(expectedFrontier, OutpostOps.IsFrontier(state, outpost));
    }

    [Fact]
    public void IncumbentTier_DoesNotMoveTheGate_OnlyLiteralAdjacencyCounts()
    {
        // the correction, made concrete: an outpost 2 hexes out (a genuine
        // second centre) is frontier beside a tier-1 port AND still frontier
        // beside a tier-2 port — the incumbent's larger domain does NOT push
        // the boundary out (as the old domain-scale gates wrongly did). Only
        // the literal spacing G = 2 counts, uniform across tiers.
        var s1 = World();
        var p1 = AddPort(s1, tier: 1);
        Assert.True(OutpostOps.IsFrontier(s1, OutpostAt(p1.Hex, 2)));

        var s2 = World();
        var p2 = AddPort(s2, tier: 2);
        Assert.True(OutpostOps.IsFrontier(s2, OutpostAt(p2.Hex, 2)));
    }

    // ---------------------------------------------------------------------
    // Densification works: a second-centre outpost inside a tier-2+ domain —
    // cleared its parent by G but still within the parent's service radius —
    // IS eligible. This is the case the OLD (domain-scale) gates wrongly
    // rejected: they demanded the outpost sit beyond the parent's whole domain,
    // which an in-domain outpost never can.
    // ---------------------------------------------------------------------

    [Fact]
    public void SecondCentreInsideATier2Domain_IsEligible()
    {
        var state = World();
        var parent = AddPort(state, tier: 2);          // ServiceRadius(2) = 8
        int g = G(state);                              // 2 at defaults
        int parentRadius = PortDomains.ServiceRadius(state.Config, 2);
        // dist 4: past G (>= 2) yet still WELL WITHIN the parent's domain
        // (<= 8) — a densifying second centre, not a reach leap outside.
        var fringe = OutpostAt(parent.Hex, 4, parentPortId: parent.Id);
        Assert.True(g <= 4 && 4 <= parentRadius,
            $"4 must lie in [G={g}, parentRadius={parentRadius}]");
        Assert.True(HexGrid.Distance(parent.Hex, fringe.Hex) <= parentRadius,
            "the second-centre outpost must still be within the parent's reach");
        Assert.True(OutpostOps.IsFrontier(state, fringe));
    }

    // ---------------------------------------------------------------------
    // A frontier outpost IS eligible; a stacked-on-a-port one stays subordinate.
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
    public void StackedOnAPortCore_IsInterior_SubordinateDensity()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);
        // the outpost sits one hex from the parent (dist 1 < G = 2) — stacked
        // on the port, the majority case. It must read interior — permanently
        // subordinate, never a candidate.
        var outpost = OutpostAt(parent.Hex, 1, parentPortId: parent.Id);
        Assert.False(OutpostOps.IsFrontier(state, outpost));
    }

    // ---------------------------------------------------------------------
    // The parent port counts — no special-casing. An outpost 1 hex from its
    // OWN parent is interior even if far from every other port.
    // ---------------------------------------------------------------------

    [Fact]
    public void ParentPortCounts_ADistOneOutpostIsInterior_EvenIfFarFromAllOthers()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);                 // id 0, at seat
        // a second, VERY distant port — irrelevant to this outpost's verdict.
        var far = new Port(state.Ports.Count, 0,
            new HexCoordinate(parent.Hex.Q + 40, parent.Hex.R), 1, 0);
        state.Ports.Add(far);

        // 1 hex from its own parent (far from `far`): the parent gates like any
        // port — no free pass — so the outpost is interior.
        var stacked = OutpostAt(parent.Hex, 1, parentPortId: parent.Id);
        Assert.False(OutpostOps.IsFrontier(state, stacked));

        // pushed to dist 2 from the parent (still far from `far`): now frontier.
        var second = OutpostAt(parent.Hex, 2, parentPortId: parent.Id);
        Assert.True(OutpostOps.IsFrontier(state, second));
    }

    [Fact]
    public void MustClearEveryPort_ASecondAdjacentPortMakesItInterior()
    {
        var state = World();
        var parent = AddPort(state, tier: 1);                 // id 0
        // clear of the parent (dist 2 = G)...
        var outpost = OutpostAt(parent.Hex, 3, parentPortId: parent.Id);
        Assert.True(OutpostOps.IsFrontier(state, outpost));

        // ...but a second entered port sits one hex from the outpost: now it is
        // within THAT core's G (dist 1 < 2), so the gate (every port) fails.
        var neighbour = new Port(state.Ports.Count, 0,
            new HexCoordinate(outpost.Hex.Q + 1, outpost.Hex.R), 1, 0);
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

        // the outpost sits 4 hexes from the foreign core — INSIDE its big domain
        // (<= 12) yet >= G (2) from it; and 16 hexes from the parent (>= G).
        var outpost = OutpostAt(foreign.Hex, -4, parentPortId: parent.Id);
        Assert.Equal(4, HexGrid.Distance(foreign.Hex, outpost.Hex));
        Assert.True(4 <= foreignRadius,                       // inside foreign domain
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
        int g = G(state);                          // 2 at defaults

        var interior = OutpostOps.FrontierStatus(state, OutpostAt(port.Hex, 1));
        Assert.False(interior.IsFrontier);
        Assert.Equal(1, interior.PortDistance);
        Assert.Equal(g, interior.Threshold);
        Assert.Equal(1 - g, interior.Slack);       // negative → interior

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

        var outpost = OutpostAt(near.Hex, 1);                 // 1 from near, 39 from far
        var status = OutpostOps.FrontierStatus(state, outpost);
        Assert.Equal(1, status.PortDistance);                 // the nearest, not 39
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
