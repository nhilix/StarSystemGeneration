using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>The potential prior (cosmic-genesis.md): the analytic shape
/// function as gravitational potential — where matter *wants* to be. Mildly
/// time-varying: the arm pattern is a fixed density wave, the core deepens
/// slowly with formation progress t01 ∈ [0,1].</summary>
public class GalaxyPotentialTests
{
    private static GalaxyConfig Config() =>
        new() { MasterSeed = 42, GalaxyRadiusCells = 8 };

    [Fact]
    public void ZeroBeyondTheRim()
    {
        var c = Config();
        Assert.Equal(0.0, GalaxyPotential.At(c, 1.2, 0.0, 1.0));
        Assert.Equal(0.0, GalaxyPotential.At(c, 0.0, -1.01, 0.5));
    }

    [Fact]
    public void BoundedOverTheDisc()
    {
        // non-negative and finite; no upper clamp (the potential is relative,
        // only its gradients and normalized residue matter)
        var c = Config();
        for (double nx = -0.95; nx <= 0.95; nx += 0.1)
            for (double ny = -0.95; ny <= 0.95; ny += 0.1)
                foreach (var t in new[] { 0.0, 0.5, 1.0 })
                    Assert.InRange(GalaxyPotential.At(c, nx, ny, t), 0.0, 2.0);
    }

    [Fact]
    public void CoreIsTheDeepestWell()
    {
        var c = Config();
        double core = GalaxyPotential.At(c, 0.0, 0.0, 1.0);
        for (double r = 0.2; r < 1.0; r += 0.1)
            Assert.True(core > GalaxyPotential.At(c, r, 0.0, 1.0),
                $"potential at r={r} should sit below the core");
    }

    [Fact]
    public void CoreDeepensWithTime_RimBarelyMoves()
    {
        var c = Config();
        Assert.True(GalaxyPotential.At(c, 0.0, 0.0, 1.0)
                    > GalaxyPotential.At(c, 0.0, 0.0, 0.0),
            "the core should deepen as the galaxy assembles");
        // far out, the core term is negligible — time should barely matter
        double early = GalaxyPotential.At(c, 0.8, 0.0, 0.0);
        double late = GalaxyPotential.At(c, 0.8, 0.0, 1.0);
        Assert.True(Math.Abs(late - early) < 0.05,
            $"rim potential moved {early:F3} -> {late:F3}; the arm pattern is fixed");
    }

    [Fact]
    public void ArmPatternIsAFixedDensityWave()
    {
        var c = Config();
        // the angle of the deepest point on a mid-disc ring must not move with time
        static int ArgMax(GalaxyConfig cfg, double r, double t)
        {
            int best = 0; double bestV = double.MinValue;
            for (int deg = 0; deg < 360; deg++)
            {
                double a = deg * Math.PI / 180;
                double v = GalaxyPotential.At(cfg, r * Math.Cos(a), r * Math.Sin(a), t);
                if (v > bestV) { bestV = v; best = deg; }
            }
            return best;
        }
        Assert.Equal(ArgMax(c, 0.6, 0.0), ArgMax(c, 0.6, 1.0));
    }

    [Fact]
    public void ArmsRaisePotential_ArmlessDiscIsRadiallySymmetric()
    {
        var armed = Config();
        var armless = Config();
        armless.ArmStrength = 0.0;

        var ring = Enumerable.Range(0, 360).Select(deg =>
        {
            double a = deg * Math.PI / 180;
            return (armed: GalaxyPotential.At(armed, 0.6 * Math.Cos(a), 0.6 * Math.Sin(a), 1.0),
                    smooth: GalaxyPotential.At(armless, 0.6 * Math.Cos(a), 0.6 * Math.Sin(a), 1.0));
        }).ToList();

        Assert.True(ring.Max(v => v.armed) - ring.Min(v => v.armed) > 0.05,
            "arm ridges should stand out of the disc");
        Assert.True(ring.Max(v => v.smooth) - ring.Min(v => v.smooth) < 1e-12,
            "with ArmStrength 0 the ring should be perfectly symmetric");
    }

    [Fact]
    public void AtCell_NormalizesLikeTheDensityField()
    {
        var c = Config();
        Assert.Equal(GalaxyPotential.At(c, 0.0, 0.0, 1.0),
            GalaxyPotential.AtCell(c, new Core.Model.HexCoordinate(0, 0), 1.0));
    }
}
