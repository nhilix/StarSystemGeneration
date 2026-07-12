using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The war lens — belligerent domains accented, war fleets on
/// station burning (emap war parity: '!' war fleet, letter belligerent,
/// ',' at peace). Stations are blockades and expeditions of owners with
/// an active war; peaceful patrols never read as war.</summary>
public class WarLensTests
{
    private static (AtlasReadModel Model, SimState State) WithPorts()
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
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, state.Actors[1].Id, b!.Value, tier: 2, foundedYear: 0));
        return (new AtlasReadModel(state), state);
    }

    private static void Declare(SimState state, int attacker, int defender) =>
        state.Wars.Add(new War(state.Wars.Count, "The Test War", attacker,
            defender, CasusBelli.BorderIncident, -1, WarDemand.Reparations,
            state.WorldYear));

    [Fact]
    public void ABlockadeInAnActiveWarStandsOnStation()
    {
        var (model, state) = WithPorts();
        int attacker = state.Actors[1].Id;
        Declare(state, attacker, state.Actors[0].Id);
        var fleet = EpochTestKit.BlockadePort(state, attacker, portId: 0);
        var station = Assert.Single(
            WarLens.Stations(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(fleet.Id, station.FleetId);
        Assert.Equal(fleet.Hex, station.Hex);
        Assert.Equal(FleetPosture.Blockade, station.Posture);
        Assert.Equal(attacker, station.OwnerActorId);
    }

    [Fact]
    public void APeacetimeBlockadeIsNotAWarStation()
    {
        var (model, state) = WithPorts();
        EpochTestKit.BlockadePort(state, state.Actors[1].Id, portId: 0);
        Assert.Empty(WarLens.Stations(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void SlotBelligerenceFlagsOnlyTheFighting()
    {
        var (model, state) = WithPorts();
        var eye = EyeContext.God(state.WorldYear);
        var slots = DomainLens.PolitySlots(model, eye);
        Declare(state, state.Actors[0].Id, state.Actors[1].Id);
        var flags = WarLens.SlotBelligerence(model, eye, slots);
        Assert.Equal(slots.Count, flags.Count);
        for (int i = 0; i < slots.Count; i++)
            Assert.Equal(WarOps.AtWar(state, slots[i]), flags[i]);
        Assert.Contains(true, flags);
    }
}
