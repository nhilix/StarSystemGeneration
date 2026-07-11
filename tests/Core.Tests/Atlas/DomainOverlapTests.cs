using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The domains field's polity slots and the overlap shading —
/// where two polities' combined regions intersect, the Venn region reads
/// as the RELATIONSHIP between them (war/tension/warm/neutral). Same
/// polity never overlaps itself: its regions union.</summary>
public class DomainOverlapTests
{
    private static SimState Seeded() => EpochTestKit.Seeded().State;

    [Fact]
    public void PolitySlotsAreDistinctOwnersAscending()
    {
        var state = Seeded();
        int a = state.Actors[0].Id, b = state.Actors[1].Id;
        var hex = HexGrid.CellCenter(state.Skeleton.Cells[0].Coord);
        state.Ports.Add(new Port(0, b, hex, 2, 0));
        state.Ports.Add(new Port(1, a, hex, 1, 0));
        state.Ports.Add(new Port(2, b, hex, 3, 0));
        var model = new AtlasReadModel(state);
        var slots = DomainLens.PolitySlots(model, EyeContext.God(state.WorldYear));
        Assert.Equal(new[] { a, b }, slots);
    }

    [Fact]
    public void OverlapShadeReadsTheRelationship()
    {
        var state = Seeded();
        int a = state.Actors[0].Id, b = state.Actors[1].Id;
        var model = new AtlasReadModel(state);
        var eye = EyeContext.God(state.WorldYear);

        // No relation on record: neutral.
        var neutral = DomainLens.OverlapShade(model, eye, a, b);

        // Loaded tension reads hot.
        var rel = new PolityRelation(a, b, 0) { Tension = 0.8 };
        state.Relations.Add(rel);
        var tense = DomainLens.OverlapShade(model, eye, a, b);
        Assert.NotEqual(neutral, tense);
        Assert.True(tense.R > tense.G && tense.R > tense.B,
            "tension shades toward ember");

        // Warmth reads as kinship.
        rel.Tension = 0.0;
        rel.Warmth = 0.6;
        var warm = DomainLens.OverlapShade(model, eye, a, b);
        Assert.True(warm.G > warm.R, "warmth shades toward green");

        // An active war overrides everything.
        state.Wars.Add(new War(0, "The Border War", a, b,
            CasusBelli.BorderIncident, -1, WarDemand.Reparations,
            state.WorldYear));
        var war = DomainLens.OverlapShade(model, eye, a, b);
        Assert.True(war.R > 180 && war.G < 110, "war shades red");
        // Symmetric — the pair reads the same from both sides.
        Assert.Equal(war, DomainLens.OverlapShade(model, eye, b, a));
    }
}
