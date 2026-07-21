using System;
using System.Linq;
using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Frontier graduation (domain-hex-expansion design §4, Stage 3): a
/// mature FRONTIER outpost is promoted in place into a real tier-1 starport —
/// the polity's "infill" expansion move, weighed against a colony expedition's
/// "reach" in the same scoring and treasury. The frontier gate is the
/// anti-clustering guarantee (an interior outpost never graduates); the cost is
/// discounted by existing development; the promotion streams its cost from
/// ExpansionPoints as construction wages (conservation flow #3) — no mint, no
/// burn; and it completes into a Port + Market with segments re-attached.
/// (Distinct from <see cref="GraduationTests"/>, which is FACTION graduation.)</summary>
public class OutpostGraduationTests
{
    // --- a single-owner domain with one frontier outpost far past G ---
    private static (SimState state, Port parent, Outpost outpost,
                    PopulationSegment resident, HexCoordinate hex)
        FrontierDomain(int facilityCount = 2, double residentSize = 1.0,
                       double expansionPoints = 100.0, int margin = 1,
                       double residentWealth = 20.0)
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        a0.Entered = true;
        state.Config.Expansion.GraduationMarginHexes = margin;
        state.WorldYear = 500;

        var parent = new Port(0, a0.Id, a0.Seat, tier: 1, foundedYear: 0);
        state.Ports.Add(parent);
        state.Markets.Add(new Market(parent.Id, state.Config.Economy));
        state.PolityOf(a0.Id).ExpansionPoints = expansionPoints;

        // G is the literal anti-adjacency spacing 1 + margin (default 2); a
        // frontier hex sits well past it, in a real non-void cell. We reuse a
        // domain-scale distance here purely to guarantee "far" — any hex a few
        // hexes out already clears G=2, but a domain-scale reach keeps the
        // fixture unambiguous and on a real committed cell.
        int g = PortDomains.ServiceRadius(state.Config, 1) * 2 + margin;
        var hex = FarValidHex(state, parent, g + 3);

        var outpost = new Outpost(state.Outposts.Count, "Fringe", hex,
                                  parent.Id, 0L);
        state.Outposts.Add(outpost);

        var resident = new PopulationSegment(state.Segments.Count, parent.Id,
            0, 0, residentSize) { Hex = hex, Wealth = residentWealth };
        state.Segments.Add(resident);

        for (int i = 0; i < facilityCount; i++)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)InfraTypeId.Mine, 1, hex, a0.Id, builtYear: 0)
            { CommissionedYear = 0 });

        return (state, parent, outpost, resident, hex);
    }

    // the center of the first non-void cell at least `minDist` hexes from the
    // port — a real target hex whose system can be committed.
    private static HexCoordinate FarValidHex(SimState state, Port port,
                                             int minDist)
    {
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            var c = HexGrid.CellCenter(cell.Coord);
            if (HexGrid.Distance(port.Hex, c) >= minDist) return c;
        }
        throw new InvalidOperationException("no far valid cell in this galaxy");
    }

    // spawn the promotion project exactly as ResolutionPhase.TryGraduate does,
    // so ProjectOps drives the conserved wage stream + in-place completion.
    private static Project SpawnGraduation(SimState state, Port parent,
                                           Outpost outpost)
    {
        double cost = ColonyValuation.GraduationCost(state, outpost);
        double years = Math.Max(1.0, state.Config.Expansion.GraduationYears);
        var proj = ProjectOps.SpawnAt(state, ProjectKind.OutpostGraduation,
            parent.OwnerActorId, parent.OwnerActorId, parent.Id, outpost.Hex,
            years, ProjectPriority.Growth, planOrder: 0,
            startedYear: state.WorldYear);
        proj.WagesPerYear = cost / years;
        proj.TargetId = outpost.Id;
        return proj;
    }

    private static void RunToCompletion(SimState state, Project proj)
    {
        for (int i = 0; i < 100 && proj.InFlight; i++)
        {
            ProjectOps.AdvanceAll(state);
            state.WorldYear += Math.Max(1, state.Config.Sim.YearsPerEpoch);
        }
    }

    private static double TotalMoney(SimState state)
    {
        double m = 0;
        foreach (var pr in state.Polities)
            m += pr.Credits + pr.ExpansionPoints + pr.DevelopmentPoints
               + pr.MilitaryPoints + pr.ReservePoints;
        foreach (var s in state.Segments) m += s.Wealth;
        foreach (var c in state.Corporations) m += c.Credits;
        return m;
    }

    // ---------------------------------------------------------------------
    // The frontier gate, through the whole candidate build (not just the T3.1
    // predicate): an INTERIOR outpost never becomes a graduation candidate — at
    // more than one config. The anti-clustering guarantee holds end to end.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void InteriorOutpost_IsNeverAGraduationCandidate(int margin)
    {
        var (state, parent, _, _, _) = FrontierDomain(margin: margin);
        // stack the sole outpost on the parent port (dist 1 < G): interior under
        // the literal anti-adjacency gate (G = 1 + margin ≥ 2), the majority
        // "suburb" case that must never graduate.
        var interiorHex = new HexCoordinate(parent.Hex.Q + 1, parent.Hex.R);
        state.Outposts[0] = state.Outposts[0] with { Hex = interiorHex };
        state.Segments[0].Hex = interiorHex;

        Assert.False(OutpostOps.IsFrontier(state, state.Outposts[0]));
        var candidates = ColonyValuation.CandidatesFor(state, parent.OwnerActorId);
        Assert.DoesNotContain(candidates,
            c => c.Kind == ColonyCandidateKind.Graduation);
    }

    [Fact]
    public void FrontierOutpost_EntersTheOneRankedList_AsAGraduationCandidate()
    {
        var (state, parent, outpost, _, hex) = FrontierDomain();
        Assert.True(OutpostOps.IsFrontier(state, outpost));

        var candidates = ColonyValuation.CandidatesFor(state, parent.OwnerActorId);
        var grad = Assert.Single(candidates,
            c => c.Kind == ColonyCandidateKind.Graduation);
        Assert.Equal(outpost.Id, grad.OutpostId);
        Assert.Equal(hex, grad.Target);
        Assert.Equal(ColonyValuation.GraduationCost(state, outpost), grad.Cost, 9);
    }

    // ---------------------------------------------------------------------
    // The cost discount: a facility-rich / populous outpost costs strictly LESS
    // than a bare one, floored at MinCostFraction × ColonyCost.
    // ---------------------------------------------------------------------

    [Fact]
    public void FacilityRichOutpost_CostsStrictlyLess_ThanABareOne()
    {
        var (rich, _, richO, _, _) = FrontierDomain(facilityCount: 4,
                                                    residentSize: 3.0);
        var (bare, _, bareO, _, _) = FrontierDomain(facilityCount: 1,
                                                    residentSize: 0.5);
        double richCost = ColonyValuation.GraduationCost(rich, richO);
        double bareCost = ColonyValuation.GraduationCost(bare, bareO);
        Assert.True(richCost < bareCost,
            $"rich {richCost} should be < bare {bareCost}");
        double full = bare.Config.Expansion.ColonyCost;
        Assert.True(bareCost < full);
        Assert.True(richCost
            >= full * rich.Config.Expansion.GraduationMinCostFraction - 1e-9);
    }

    [Fact]
    public void HeavilyDevelopedOutpost_IsFlooredAtMinCostFraction()
    {
        var (state, _, outpost, _, _) = FrontierDomain(facilityCount: 40,
                                                       residentSize: 40.0);
        double cost = ColonyValuation.GraduationCost(state, outpost);
        double floor = state.Config.Expansion.ColonyCost
                     * state.Config.Expansion.GraduationMinCostFraction;
        Assert.Equal(floor, cost, 9);
    }

    // ---------------------------------------------------------------------
    // The controller: infill needs NO convoy; reach still does. One decision
    // site emits the right act for the top candidate's kind.
    // ---------------------------------------------------------------------

    [Fact]
    public void Controller_EmitsGraduateAct_ForATopGraduationCandidate_NoConvoy()
    {
        var cfg = new EpochSimConfig();
        var controller = new GenesisController(cfg);
        var grad = new ColonyCandidate(new HexCoordinate(9, -4), 1.5,
            ColonyCandidateKind.Graduation, OutpostId: 7, Cost: 8.0);
        var view = new PerceptionView(3, worldYear: 100,
            knownPolityIds: new[] { 0, 1 }, expansionPoints: 8.0,
            colonyCandidates: new[] { grad },
            colonyHullsAvailable: 0);   // NO convoy — infill needs none

        var act = Assert.IsType<GraduateOutpostAct>(
            Assert.Single(controller.Decide(view).Acts));
        Assert.Equal(3, act.ActorId);
        Assert.Equal(7, act.OutpostId);

        // one credit short of the discounted cost → no act.
        var broke = new PerceptionView(3, 100, new[] { 0, 1 },
            expansionPoints: 7.99, colonyCandidates: new[] { grad },
            colonyHullsAvailable: 0);
        Assert.Empty(controller.Decide(broke).Acts);
    }

    [Fact]
    public void Controller_StillGatesExpeditions_OnAConvoy()
    {
        var cfg = new EpochSimConfig();
        var controller = new GenesisController(cfg);
        var reach = new ColonyCandidate(new HexCoordinate(9, -4), 1.5);
        var noHull = new PerceptionView(3, 100, new[] { 0, 1 },
            expansionPoints: cfg.Expansion.ColonyCost,
            colonyCandidates: new[] { reach }, colonyHullsAvailable: 0);
        Assert.Empty(controller.Decide(noHull).Acts);

        var withHull = new PerceptionView(3, 100, new[] { 0, 1 },
            expansionPoints: cfg.Expansion.ColonyCost,
            colonyCandidates: new[] { reach }, colonyHullsAvailable: 1);
        Assert.IsType<FoundColonyAct>(Assert.Single(
            controller.Decide(withHull).Acts));
    }

    // ---------------------------------------------------------------------
    // Promotion integrity: a completed graduation births a tier-1 Port + Market
    // at the hex, re-attaches the residents, re-resolves the facilities to the
    // new market, marks the outpost graduated, and fires the tension bump.
    // ---------------------------------------------------------------------

    [Fact]
    public void Completion_BirthsAPortAndMarket_AtTheOutpostHex()
    {
        var (state, parent, outpost, _, hex) = FrontierDomain();
        int portsBefore = state.Ports.Count, marketsBefore = state.Markets.Count;

        var proj = SpawnGraduation(state, parent, outpost);
        ProjectOps.Complete(state, proj, completionYear: 600);

        Assert.Equal(portsBefore + 1, state.Ports.Count);
        Assert.Equal(marketsBefore + 1, state.Markets.Count);
        var born = state.Ports[^1];
        Assert.Equal(hex, born.Hex);
        Assert.Equal(1, born.Tier);
        Assert.Equal(parent.OwnerActorId, born.OwnerActorId);
        Assert.True(state.Outposts[outpost.Id].Graduated);
        var ev = Assert.Single(state.Staged,
            e => e.Type == WorldEventType.PortEstablished);
        Assert.Equal(born.Id, ((PortEstablishedPayload)ev.Payload!).PortId);
    }

    [Fact]
    public void Completion_ReattachesResidents_AndReresolvesFacilities()
    {
        var (state, parent, outpost, resident, hex) = FrontierDomain();
        Assert.Equal(parent.Id, resident.PortId);   // administered by parent

        var proj = SpawnGraduation(state, parent, outpost);
        ProjectOps.Complete(state, proj, completionYear: 600);
        var born = state.Ports[^1];

        Assert.Equal(born.Id, resident.PortId);
        Assert.Equal(hex, resident.Hex);
        foreach (var f in state.Facilities)
            if (f.Hex.Equals(hex))
                Assert.Equal(born.Id, MarketEngine.AttachedMarketIndex(state, f));
    }

    [Fact]
    public void Completion_FiresTheEncroachmentTensionBump_AgainstANeighbor()
    {
        var (state, parent, outpost, _, hex) = FrontierDomain();
        var a1 = state.Actors[1];
        a1.Entered = true;
        var neighborPort = new Port(state.Ports.Count, a1.Id,
            new HexCoordinate(hex.Q + 1, hex.R), tier: 1, foundedYear: 0);
        state.Ports.Add(neighborPort);
        state.Markets.Add(new Market(neighborPort.Id, state.Config.Economy));
        var rel = new PolityRelation(parent.OwnerActorId, a1.Id, 0)
        { Tension = 0.1 };
        state.Relations.Add(rel);
        double before = rel.Tension;

        var proj = SpawnGraduation(state, parent, outpost);
        ProjectOps.Complete(state, proj, completionYear: 600);

        Assert.True(rel.Tension > before,
            $"tension {rel.Tension} should exceed {before}");
    }

    // ---------------------------------------------------------------------
    // Conservation (flow #3): the ExpansionPoints spent equals the wages that
    // land — total money is conserved across the charge+completion to FP eps.
    // ---------------------------------------------------------------------

    [Fact]
    public void Promotion_ConservesTotalMoney_CostRecyclesIntoWages()
    {
        var (state, parent, outpost, _, _) = FrontierDomain(
            expansionPoints: 100.0);
        double before = TotalMoney(state);
        double cost = ColonyValuation.GraduationCost(state, outpost);
        double expBefore = state.PolityOf(parent.OwnerActorId).ExpansionPoints;

        var proj = SpawnGraduation(state, parent, outpost);
        RunToCompletion(state, proj);

        Assert.True(proj.Completed);
        double after = TotalMoney(state);
        Assert.Equal(before, after, 6);
        double expAfter = state.PolityOf(parent.OwnerActorId).ExpansionPoints;
        Assert.Equal(expBefore - cost, expAfter, 6);
        double segWealth = state.Segments.Sum(s => s.Wealth);
        Assert.True(segWealth > 0);
    }

    [Fact]
    public void Deterministic_SameConfig_SameGraduationCost()
    {
        var (s1, _, o1, _, _) = FrontierDomain();
        var (s2, _, o2, _, _) = FrontierDomain();
        Assert.Equal(ColonyValuation.GraduationCost(s1, o1),
                     ColonyValuation.GraduationCost(s2, o2), 12);
    }
}
