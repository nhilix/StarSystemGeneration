using System;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The nature field sampler — cell values become smooth nebular
/// clouds: Gaussian-blended across neighboring cells, presence-scaled
/// alpha (rich areas glow, poor areas fade to starfield), void feathers
/// to transparency, deterministic noise breakup.</summary>
public class NatureFieldSamplerTests
{
    private static (AtlasReadModel Model, NatureFieldSampler Sampler) Metal()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        var sampler = new NatureFieldSampler(
            model, EyeContext.God(state.WorldYear), NatureLayer.Metal);
        return (model, sampler);
    }

    [Fact]
    public void SamplingIsDeterministic()
    {
        var (model, a) = Metal();
        var b = new NatureFieldSampler(
            model, EyeContext.God(model.State.WorldYear), NatureLayer.Metal);
        for (int i = 0; i < 20; i++)
        {
            double x = i * 7.3 - 60, y = i * 3.1 - 25;
            Assert.Equal(a.Sample(x, y), b.Sample(x, y));
        }
    }

    [Fact]
    public void TheFieldIsSmoothAcrossCellBoundaries()
    {
        var (model, sampler) = Metal();
        // Walk a line across the disc; adjacent samples 1 world unit apart
        // must never jump by more than a modest step — no hex cliffs.
        var (cx, cy) = HexGrid.HexToWorld(
            HexGrid.CellCenter(model.Cells[0].Coord));
        for (double t = 0; t < 60; t += 1.0)
        {
            var s0 = sampler.Sample(cx + t, cy);
            var s1 = sampler.Sample(cx + t + 1.0, cy);
            Assert.True(Math.Abs(s0.R - s1.R) <= 24
                     && Math.Abs(s0.A - s1.A) <= 30,
                $"cliff at t={t}: {s0} -> {s1}");
        }
    }

    [Fact]
    public void DeepVoidFadesToTransparent()
    {
        var (model, sampler) = Metal();
        // Far outside the disc: nothing to blend, fully transparent.
        var far = sampler.Sample(10000, 10000);
        Assert.Equal(0, far.A);
    }

    [Fact]
    public void TheWholeDiskStaysVisible()
    {
        // The disc must read as a blended whole with the arms as bright
        // features — the poorest LIVE cell never fades to nothing (the
        // REPL map's read; only true void goes dark).
        var (model, sampler) = Metal();
        int poor = -1;
        for (int i = 0; i < model.Cells.Count; i++)
        {
            var c = model.Cells[i];
            if (c.IsVoid) continue;
            if (poor < 0 || c.Metallicity < model.Cells[poor].Metallicity)
                poor = i;
        }
        var (px, py) = HexGrid.HexToWorld(
            HexGrid.CellCenter(model.Cells[poor].Coord));
        Assert.True(sampler.Sample(px, py).A >= 60,
            $"poorest live cell reads {sampler.Sample(px, py).A} — disk lost");
    }

    [Fact]
    public void RicherCellsGlowStronger()
    {
        var (model, sampler) = Metal();
        int rich = -1, poor = -1;
        for (int i = 0; i < model.Cells.Count; i++)
        {
            var c = model.Cells[i];
            if (c.IsVoid) continue;
            if (rich < 0 || c.Metallicity > model.Cells[rich].Metallicity) rich = i;
            if (poor < 0 || c.Metallicity < model.Cells[poor].Metallicity) poor = i;
        }
        var (rx, ry) = HexGrid.HexToWorld(
            HexGrid.CellCenter(model.Cells[rich].Coord));
        var (px, py) = HexGrid.HexToWorld(
            HexGrid.CellCenter(model.Cells[poor].Coord));
        Assert.True(sampler.Sample(rx, ry).A > sampler.Sample(px, py).A,
            "presence should scale alpha with the field value");
    }
}
