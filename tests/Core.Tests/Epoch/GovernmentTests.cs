using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 1: the eight-form catalog, seating in ideology
/// space × species, and the polity interior state (legitimacy, cohesion,
/// enforcement) recomputed each Interior phase.</summary>
public class GovernmentTests
{
    private static SpeciesProfile Species(
        Embodiment embodiment = Embodiment.TerranAnalog,
        double expansionism = 0.5, double cohesion = 0.5, double militancy = 0.5,
        double openness = 0.5, double industry = 0.5, double adaptability = 0.5) =>
        new SpeciesProfile
        {
            Id = 0, Name = "Testkin", Embodiment = embodiment,
            Expansionism = expansionism, Cohesion = cohesion,
            Militancy = militancy, Openness = openness,
            Industry = industry, Adaptability = adaptability,
        };

    [Fact]
    public void Catalog_HasEightFormsWithStableIdsAndSaneRanges()
    {
        Assert.Equal(8, GovernmentForms.All.Count);
        for (int i = 0; i < GovernmentForms.All.Count; i++)
        {
            var def = GovernmentForms.All[i];
            Assert.Equal(i, (int)def.Id);   // catalog in id order, ids dense
            Assert.InRange(def.PolicyInertia, 0.0, 1.0);
            Assert.InRange(def.FactionTolerance, 0.0, 1.0);
            Assert.InRange(def.CohesionFloor, 0.0, 1.0);
            var c = def.Composition;
            Assert.Equal(1.0, c.Species + c.Ideology + c.Ruler + c.Faction, 9);
        }
        Assert.Equal(GovernmentForms.All.Count,
            GovernmentForms.All.Select(d => d.Name).Distinct().Count());
    }

    [Fact]
    public void SpeciesGatedForms_SeatByEmbodiment()
    {
        var neutral = new double[] { 0.5, 0.5, 0.5, 0.5 };
        Assert.Equal(GovernmentFormId.HiveUnity,
            GovernmentForms.SeatFor(Species(Embodiment.Hive), neutral));
        Assert.Equal(GovernmentFormId.MachineConsensus,
            GovernmentForms.SeatFor(Species(Embodiment.Machine), neutral));
        Assert.Equal(GovernmentFormId.StewardDynasty,
            GovernmentForms.SeatFor(Species(Embodiment.Lithic), neutral));
    }

    [Fact]
    public void IdeologySeating_PicksTheNearestSeat()
    {
        // authority + individual: an expansionist, loosely-bound species
        var autocrat = Species(expansionism: 0.9, cohesion: 0.3, militancy: 0.9,
                               openness: 0.3, industry: 0.7);
        var tilt = GovernmentForms.SpeciesIdeologyTilt(autocrat);
        Assert.Equal(GovernmentFormId.Autocracy,
            GovernmentForms.SeatFor(autocrat, tilt));

        // sacral + authority: a closed, unindustrial faith-bound species
        var theocrat = Species(cohesion: 0.7, militancy: 0.6, openness: 0.1,
                               industry: 0.1, expansionism: 0.4);
        var tilt2 = GovernmentForms.SpeciesIdeologyTilt(theocrat);
        Assert.Equal(GovernmentFormId.Theocracy,
            GovernmentForms.SeatFor(theocrat, tilt2));
    }

    [Fact]
    public void Entry_SeatsTheInterior_OfficialMatchesPopular()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var entered = state.Actors.First(a => a.Entered);
        var interior = state.PolityOf(entered.Id).Interior;
        Assert.NotNull(interior);
        // official ideology at birth equals the homeworld segment's
        var seg = state.Segments.First(
            s => state.Ports[s.PortId].OwnerActorId == entered.Id);
        for (int ax = 0; ax < 4; ax++)
            Assert.Equal(seg.Ideology[ax], interior!.OfficialIdeology[ax], 6);
    }

    [Fact]
    public void Recompute_KeepsScalarsInRange_AndCohesionUnderLegitimacy()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        int checkedCount = 0;
        foreach (var pr in state.Polities)
        {
            if (pr.Interior == null) continue;
            checkedCount++;
            Assert.InRange(pr.Interior.Legitimacy, 0.0, 1.0);
            Assert.InRange(pr.Interior.Cohesion, 0.0, 1.0);
            Assert.InRange(pr.Interior.Enforcement, 0.0, 1.0);
            var form = GovernmentForms.Get(pr.Interior.FormId);
            Assert.True(pr.Interior.Cohesion
                        <= System.Math.Max(pr.Interior.Legitimacy,
                                           form.CohesionFloor) + 1e-9,
                "cohesion above legitimacy without a form floor");
        }
        Assert.True(checkedCount > 0, "no seated interiors after a full run");
    }

    [Fact]
    public void Legitimacy_FallsWithProsperityCollapse()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        engine.Step(state);
        var pr = state.Polities.First(p => p.Interior != null);
        // level playing field: recompute from a prosperous baseline...
        foreach (var s in state.Segments)
            if (state.Ports[s.PortId].OwnerActorId == pr.ActorId)
            { s.SoL = 0.8; s.LastSubsistence = 1.0; }
        InteriorOps.Recompute(state);
        double prosperous = pr.Interior!.Legitimacy;
        // ...then the bottom falls out
        foreach (var s in state.Segments)
            if (state.Ports[s.PortId].OwnerActorId == pr.ActorId)
            { s.SoL = 0.1; s.LastSubsistence = 0.4; }
        InteriorOps.Recompute(state);
        Assert.True(pr.Interior.Legitimacy < prosperous,
            $"legitimacy should fall with SoL collapse "
            + $"({prosperous} -> {pr.Interior.Legitimacy})");
    }

    [Fact]
    public void OfficialIdeology_DriftsTowardPopular_AtFormInertia()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var pr = state.Polities.First(p => p.Interior != null);
        // wrench the population away from the official line
        foreach (var s in state.Segments)
            if (state.Ports[s.PortId].OwnerActorId == pr.ActorId)
                s.Ideology[0] = 1.0;
        double before = pr.Interior!.OfficialIdeology[0];
        InteriorOps.Recompute(state);
        double after = pr.Interior.OfficialIdeology[0];
        Assert.True(after > before, "official ideology should chase popular");
        Assert.True(after < 1.0, "inertia keeps the drift bounded");
    }

    [Fact]
    public void Interior_RoundTripsThroughTheArtifact()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        foreach (var pr in state.Polities)
        {
            var lp = loaded.PolityOf(pr.ActorId);
            if (pr.Interior == null) { Assert.Null(lp.Interior); continue; }
            Assert.NotNull(lp.Interior);
            Assert.Equal(pr.Interior.FormId, lp.Interior!.FormId);
            Assert.Equal(pr.Interior.Legitimacy, lp.Interior.Legitimacy);
            Assert.Equal(pr.Interior.Cohesion, lp.Interior.Cohesion);
            Assert.Equal(pr.Interior.Enforcement, lp.Interior.Enforcement);
            Assert.Equal(pr.Interior.LastMeanSoL, lp.Interior.LastMeanSoL);
            Assert.Equal(pr.Interior.RulerCharacterId, lp.Interior.RulerCharacterId);
            Assert.Equal(pr.Interior.FoundingCultureId, lp.Interior.FoundingCultureId);
            for (int ax = 0; ax < 4; ax++)
                Assert.Equal(pr.Interior.OfficialIdeology[ax],
                             lp.Interior.OfficialIdeology[ax]);
        }
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
