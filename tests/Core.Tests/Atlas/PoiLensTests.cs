using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The POI lens — anchored points of interest at their hexes
/// (chronicle-and-poi.md): live anchors only, typed for the authored glyph
/// set, magnitude carried for sizing.</summary>
public class PoiLensTests
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
    public void ALivePoiMarksItsHexTypedAndSized()
    {
        var (model, state, hex) = Seeded();
        state.Pois.Add(new PoiRecord(0, PoiType.Battlefield, hex,
                                     magnitude: 12, foundedYear: 100));
        var mark = Assert.Single(
            PoiLens.Marks(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0, mark.PoiId);
        Assert.Equal(PoiType.Battlefield, mark.Type);
        Assert.Equal(hex, mark.Hex);
        Assert.Equal(12.0, mark.Magnitude);
    }

    [Fact]
    public void ADepletedPoiNoLongerPinsItsHex()
    {
        var (model, state, hex) = Seeded();
        state.Pois.Add(new PoiRecord(0, PoiType.Ruins, hex,
                                     magnitude: 3, foundedYear: 100)
        { Depleted = true });
        Assert.Empty(PoiLens.Marks(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void TypesCarryDistinctColorsForTheGlyphSet()
    {
        var (model, state, hex) = Seeded();
        state.Pois.Add(new PoiRecord(0, PoiType.Battlefield, hex, 5, 100));
        state.Pois.Add(new PoiRecord(1, PoiType.PrecursorSite, hex, 5, 100)
        { Dormant = true });
        var marks = PoiLens.Marks(model, EyeContext.God(state.WorldYear));
        Assert.Equal(2, marks.Count);
        Assert.NotEqual(marks[0].Color, marks[1].Color);
        Assert.True(marks[1].Dormant);
    }
}
