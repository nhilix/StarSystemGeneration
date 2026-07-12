using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The news lens — god sees all pulses in transit
/// (perception-and-news.md): word born at an event's hex, aging toward
/// its expiry; the controller inbox is the reserved eye seam. Liveness
/// parity with BeliefOps.DeliverPulses (0 ≤ age ≤ PulseMaxYears).</summary>
public class NewsLensTests
{
    private static (AtlasReadModel Model, SimState State, HexCoordinate Hex) Seeded()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            hex = HexGrid.CellCenter(cell.Coord);
            break;
        }
        return (new AtlasReadModel(state), state, hex);
    }

    [Fact]
    public void ALivePulseMarksItsOriginAged()
    {
        var (model, state, hex) = Seeded();
        state.Pulses.Add(new NewsPulse(0, eventId: 7, hex,
            emitYear: state.WorldYear - 20, magnitude: 0.8));
        var mark = Assert.Single(
            NewsLens.Pulses(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0, mark.PulseId);
        Assert.Equal(hex, mark.Origin);
        Assert.Equal(20.0, mark.AgeYears, precision: 9);
        Assert.Equal(0.8, mark.Magnitude, precision: 9);
    }

    [Fact]
    public void AnExpiredPulseIsRumorNotNews()
    {
        var (model, state, hex) = Seeded();
        state.Pulses.Add(new NewsPulse(0, 7, hex,
            state.WorldYear - (long)state.Config.News.PulseMaxYears - 1, 0.8));
        Assert.Empty(NewsLens.Pulses(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void FreshWordShoutsAndOldWordFades()
    {
        var (model, state, hex) = Seeded();
        state.Pulses.Add(new NewsPulse(0, 7, hex, state.WorldYear - 2, 0.8));
        state.Pulses.Add(new NewsPulse(1, 8, hex, state.WorldYear - 120, 0.8));
        var marks = NewsLens.Pulses(model, EyeContext.God(state.WorldYear));
        Assert.Equal(2, marks.Count);
        Assert.True(marks[0].Color.A > marks[1].Color.A,
            "the younger pulse reads louder");
    }
}
