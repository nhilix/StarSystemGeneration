using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 4: the temperament composition — species × official
/// ideology × ruler × faction pressure, weighted by government form; Intent
/// reads the composition, never the fixed species vector.</summary>
public class TemperamentTests
{
    [Fact]
    public void IdeologyMap_IsDirectional()
    {
        // authority + insular reads militant and closed
        var hardline = Temperament.FromIdeology(new[] { 0.0, 0.5, 1.0, 0.5 });
        Assert.True(hardline.Militancy > 0.5);
        Assert.True(hardline.Openness < 0.5);
        // autonomy + open reads the reverse
        var liberal = Temperament.FromIdeology(new[] { 1.0, 0.5, 0.0, 0.5 });
        Assert.True(liberal.Militancy < 0.5);
        Assert.True(liberal.Openness > 0.5);
        // individual + material expands
        Assert.True(Temperament.FromIdeology(new[] { 0.5, 1.0, 0.5, 1.0 })
                        .Expansionism > 0.5);
    }

    [Fact]
    public void Composition_RespondsToTheRuler_ScaledByForm()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var pr = state.Polities.First(p => p.Interior != null);
        var ruler = state.Characters[pr.Interior!.RulerCharacterId];

        ruler.Boldness = 0.0;
        double timid = Temperament.Compose(state, pr).Militancy;
        ruler.Boldness = 1.0;
        double bold = Temperament.Compose(state, pr).Militancy;
        double rulerWeight = GovernmentForms.Get(pr.Interior.FormId)
            .Composition.Ruler;
        if (rulerWeight > 0)
            Assert.True(bold > timid,
                $"a bolder ruler must read more militant ({timid} -> {bold})");
        // the swing is bounded by the form's ruler weight
        Assert.True(bold - timid <= rulerWeight * 0.4 + 1e-9,
            "ruler swing exceeded the form's composition weight");
    }

    [Fact]
    public void Composition_FeelsFactionPressure()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var pr = state.Polities.First(p => p.Interior != null);
        double before = Temperament.Compose(state, pr).Militancy;
        // a powerful military faction leans on Intent
        var junta = new Faction(state.Factions.Count, "Test Guard",
                                pr.ActorId, FactionBasis.Military,
                                state.WorldYear)
        { Strength = 1.0 };
        state.Factions.Add(junta);
        double after = Temperament.Compose(state, pr).Militancy;
        double factionWeight = GovernmentForms.Get(pr.Interior!.FormId)
            .Composition.Faction;
        if (factionWeight > 0)
            Assert.True(after > before,
                $"a military faction must read more militant ({before} -> {after})");
    }

    [Fact]
    public void Intent_ReadsTheComposition_NotTheSpeciesVector()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        engine.Step(state);   // perception now serves the entered polity
        var actor = state.Actors.First(a => a.Entered);
        var view = actor.Perception!;
        var composed = Temperament.Compose(state, state.PolityOf(actor.Id));
        Assert.Equal(composed, view.SelfTemperament);

        // the controller's escort priority follows the composed militancy
        var decision = new GenesisController(state.Config).Decide(view);
        var policies = (PolityPolicies)decision.Policies;
        foreach (var brief in view.OwnDesigns)
            if (brief.Role == ShipRole.Escort)
                Assert.Equal(0.5 * composed.Militancy,
                    policies.ShipbuildingPriorities[brief.DesignId], 9);
    }
}
