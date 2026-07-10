using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice E task 2: the design registry and lineages — entry design
/// sets, mark drift with inherited names (event 400), perception briefs,
/// artifact round-trip of DESIGN records.</summary>
public class ShipDesignRegistryTests
{
    [Fact]
    public void EnteredPolities_CarryTheirFoundingDesigns()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            Assert.NotNull(DesignRegistry.Current(state, a.Id,
                ShipRole.Freight, ShipSize.Medium));
            Assert.NotNull(DesignRegistry.Current(state, a.Id,
                ShipRole.Colony, ShipSize.Medium));
            Assert.NotNull(DesignRegistry.Current(state, a.Id,
                ShipRole.Scout, ShipSize.Light));
            double militancy = state.Skeleton
                .Species[state.PolityOf(a.Id).SpeciesId].Militancy;
            var escort = DesignRegistry.Current(state, a.Id,
                ShipRole.Escort, ShipSize.Light);
            if (militancy > state.Config.Controller.MilitancyReserveGate)
                Assert.NotNull(escort);
            else
                Assert.Null(escort);
        }
        // registry ids are dense and ordered (P6 / loader contract)
        for (int i = 0; i < state.Designs.Count; i++)
            Assert.Equal(i, state.Designs[i].Id);
    }

    [Fact]
    public void MarkDrift_InheritsTheName_AndChroniclesTheLaunch()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Step(state);
        var polity = FirstEntered(state);
        var hauler = DesignRegistry.Current(state, polity,
            ShipRole.Freight, ShipSize.Medium)!;
        double step = state.Config.Fleet.MarkGradeStep;

        // components barely better: no new mark
        var same = DesignRegistry.MaybeAdvanceMark(state, hauler,
            hauler.ComponentGrade + step * 0.5, state.Actors[polity].Seat);
        Assert.Same(hauler, same);

        int staged = state.Staged.Count;
        var next = DesignRegistry.MaybeAdvanceMark(state, hauler,
            hauler.ComponentGrade + step + 0.01, state.Actors[polity].Seat);
        Assert.NotSame(hauler, next);
        Assert.Equal(hauler.Name, next.Name);              // inherited name
        Assert.Equal(hauler.Mark + 1, next.Mark);
        Assert.True(next.ComponentGrade > hauler.ComponentGrade);
        Assert.Equal(staged + 1, state.Staged.Count);
        var e = state.Staged[state.Staged.Count - 1];
        Assert.Equal(WorldEventType.ShipClassLaunched, e.Type);
        Assert.Equal(EventFamily.Military, WorldEventTypes.FamilyOf(e.Type));
        // the registry now answers with the new mark
        Assert.Same(next, DesignRegistry.Current(state, polity,
            ShipRole.Freight, ShipSize.Medium));
    }

    [Fact]
    public void Perception_CarriesCurrentMarkBriefs()
    {
        var state = EpochTestKit.Seeded().State;
        var engine = new EpochEngine();
        engine.Step(state);
        int polity = FirstEntered(state);
        var hauler = DesignRegistry.Current(state, polity,
            ShipRole.Freight, ShipSize.Medium)!;
        DesignRegistry.MaybeAdvanceMark(state, hauler, 0.9,
            state.Actors[polity].Seat);
        engine.Step(state);

        var view = state.Actors[polity].Perception!;
        Assert.NotEmpty(view.OwnDesigns);
        DesignBrief? freight = null;
        foreach (var b in view.OwnDesigns)
            if (b.Role == ShipRole.Freight && b.Size == ShipSize.Medium)
                freight = b;
        Assert.NotNull(freight);
        Assert.Equal(2, freight!.Mark);                    // the current mark only
    }

    [Fact]
    public void Artifact_RoundTripsDesignsAndDeepFleets()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        Assert.NotEmpty(state.Designs);
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("LAYER|fleets|2", text);
        Assert.Contains("DESIGN|0|", text);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Designs.Count, loaded.Designs.Count);
        for (int i = 0; i < state.Designs.Count; i++)
        {
            Assert.Equal(state.Designs[i].Name, loaded.Designs[i].Name);
            Assert.Equal(state.Designs[i].Mark, loaded.Designs[i].Mark);
            Assert.Equal(state.Designs[i].ComponentGrade,
                         loaded.Designs[i].ComponentGrade);
        }
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));   // byte identity
    }

    private static int FirstEntered(SimState state)
    {
        foreach (var a in state.Actors)
            if (a.Entered) return a.Id;
        throw new Xunit.Sdk.XunitException("no polity entered after one epoch");
    }
}
