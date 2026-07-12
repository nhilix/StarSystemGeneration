using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The plague lens — contagion made visible (emap plague parity):
/// infected ports burn, immune ports carry the scar, healthy ports leave
/// no mark. Quarantined approaches ride the lane lens's status, which the
/// plague presentation re-emphasizes.</summary>
public class PlagueLensTests
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
        state.Ports.Add(new Port(1, state.Actors[0].Id, b!.Value, tier: 2, foundedYear: 0));
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void AnInfectedPortBurns()
    {
        var (model, state) = WithPorts();
        var plague = new Plague(0, "Test Rot", originPortId: 0, startYear: state.WorldYear);
        plague.InfectedSince[0] = state.WorldYear;
        state.Plagues.Add(plague);
        var mark = Assert.Single(
            PlagueLens.Marks(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0, mark.PortId);
        Assert.Equal(PortPlagueStatus.Infected, mark.Status);
        Assert.Equal(state.Ports[0].Hex, mark.Hex);
    }

    [Fact]
    public void ARecoveredPortCarriesTheScarUntilImmunityLapses()
    {
        var (model, state) = WithPorts();
        var plague = new Plague(0, "Test Rot", originPortId: 0, startYear: state.WorldYear);
        plague.ImmuneUntil[1] = state.WorldYear + 10;
        state.Plagues.Add(plague);
        var mark = Assert.Single(
            PlagueLens.Marks(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(1, mark.PortId);
        Assert.Equal(PortPlagueStatus.Immune, mark.Status);

        // The scar reads the STATE's clock — lapse ends the mark.
        state.WorldYear += 11;
        Assert.Empty(PlagueLens.Marks(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void HealthyPortsLeaveNoMark()
    {
        var (model, state) = WithPorts();
        Assert.Empty(PlagueLens.Marks(model, EyeContext.God(state.WorldYear)));
    }
}
