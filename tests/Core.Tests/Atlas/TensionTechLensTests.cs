using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The pressure gauge and the tech gap — per-domain scalar
/// accents (emap tension/tech parity): each polity's slot carries its
/// hottest live relation, or its Astrogation tier.</summary>
public class TensionTechLensTests
{
    private static (AtlasReadModel Model, SimState State, int[] Entered) WithPorts()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? a = null, b = null;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            if (a == null) { a = HexGrid.CellCenter(cell.Coord); continue; }
            b = HexGrid.CellCenter(cell.Coord);
            break;
        }
        // Relations only read live (Entered) pairs — genesis stages entry
        // over epochs, so put three polities on stage (BeliefTests idiom).
        var entered = new System.Collections.Generic.List<int>();
        foreach (var actor in state.Actors)
        {
            if (actor.Kind != ActorKind.Polity || entered.Count == 3) continue;
            actor.Entered = true;
            entered.Add(actor.Id);
        }
        state.Ports.Add(new Port(0, entered[0], a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, entered[1], b!.Value, tier: 2, foundedYear: 0));
        return (new AtlasReadModel(state), state, entered.ToArray());
    }

    [Fact]
    public void HeatIsTheHottestLiveRelation()
    {
        var (model, state, entered) = WithPorts();
        var eye = EyeContext.God(state.WorldYear);
        int a = entered[0], b = entered[1];
        Assert.Equal(0.0, TensionLens.HeatOf(model, eye, a));

        state.Relations.Add(new PolityRelation(a, b, 0) { Tension = 0.3 });
        state.Relations.Add(new PolityRelation(a, entered[2], 0)
        { Tension = 0.8 });
        Assert.Equal(0.8, TensionLens.HeatOf(model, eye, a));
        // Parity with emap's TensionGlyph digit: round(heat × 9).
        Assert.Equal(7, (int)System.Math.Round(
            TensionLens.HeatOf(model, eye, a) * 9));
    }

    [Fact]
    public void SlotHeatRunsParallelToTheSlots()
    {
        var (model, state, entered) = WithPorts();
        var eye = EyeContext.God(state.WorldYear);
        var slots = DomainLens.PolitySlots(model, eye);
        state.Relations.Add(new PolityRelation(
            entered[0], entered[1], 0) { Tension = 0.5 });
        var heat = TensionLens.SlotHeat(model, eye, slots);
        Assert.Equal(slots.Count, heat.Count);
        for (int i = 0; i < slots.Count; i++)
            Assert.Equal(TensionLens.HeatOf(model, eye, slots[i]), heat[i]);
    }

    [Fact]
    public void SlotTiersReadTheAstrogationLadder()
    {
        var (model, state, _) = WithPorts();
        var eye = EyeContext.God(state.WorldYear);
        var slots = DomainLens.PolitySlots(model, eye);
        var tiers = TechLens.SlotTiers(model, eye, slots);
        Assert.Equal(slots.Count, tiers.Count);
        for (int i = 0; i < slots.Count; i++)
            Assert.Equal(Tech.Tier(state, slots[i], TechDomain.Astrogation),
                         tiers[i]);
    }

    [Fact]
    public void AccentColorsClimbTheirRamps()
    {
        var cold = TensionLens.HeatColor(0.0);
        var hot = TensionLens.HeatColor(1.0);
        Assert.True(hot.R > cold.R, "heat shades toward ember");

        var low = TechLens.TierColor(0);
        var high = TechLens.TierColor(5);
        Assert.NotEqual(low, high);
    }
}
