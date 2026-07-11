using System;
using System.Linq;
using StarGen.Core.Atlas;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The nature lens group — the genesis rasters as base layers
/// (unity-atlas-design.md: the REPL `map` layers survive as the NATURE
/// lens group; same under every eye).</summary>
public class NatureLensTests
{
    [Theory]
    [InlineData(NatureLayer.Density)]
    [InlineData(NatureLayer.Lean)]
    [InlineData(NatureLayer.Gas)]
    [InlineData(NatureLayer.Metal)]
    [InlineData(NatureLayer.Age)]
    [InlineData(NatureLayer.Minerals)]
    [InlineData(NatureLayer.Bio)]
    [InlineData(NatureLayer.Emergence)]
    [InlineData(NatureLayer.Features)]
    public void ShadesRunParallelToTheRaster(NatureLayer layer)
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        var shades = NatureLens.Shades(model, EyeContext.God(state.WorldYear), layer);
        Assert.Equal(model.Cells.Count, shades.Count);
    }

    [Fact]
    public void VoidCellsReadAsTheDarkVoid()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        int voidIndex = -1;
        for (int i = 0; i < model.Cells.Count; i++)
            if (model.Cells[i].IsVoid) { voidIndex = i; break; }
        Assert.True(voidIndex >= 0, "seeded galaxy should contain void cells");
        var shades = NatureLens.Shades(model, EyeContext.God(state.WorldYear),
                                       NatureLayer.Metal);
        Assert.Equal(AtlasPalette.Void, shades[voidIndex]);
    }

    [Fact]
    public void RicherMetallicityShadesBrighter()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        var live = Enumerable.Range(0, model.Cells.Count)
            .Where(i => !model.Cells[i].IsVoid)
            .OrderBy(i => model.Cells[i].Metallicity).ToList();
        int lo = live.First(), hi = live.Last();
        Assert.True(model.Cells[hi].Metallicity > model.Cells[lo].Metallicity,
            "seeded galaxy should vary in metallicity");
        var shades = NatureLens.Shades(model, EyeContext.God(state.WorldYear),
                                       NatureLayer.Metal);
        int Brightness(Rgba c) => c.R + c.G + c.B;
        Assert.True(Brightness(shades[hi]) > Brightness(shades[lo]));
    }

    [Fact]
    public void NatureReadsTheSameUnderEveryEye()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        foreach (NatureLayer layer in Enum.GetValues(typeof(NatureLayer)))
        {
            var god = NatureLens.Shades(model, EyeContext.God(state.WorldYear), layer);
            var actor = NatureLens.Shades(
                model, EyeContext.Controller(0, state.WorldYear), layer);
            Assert.Equal(god, actor);
        }
    }
}
