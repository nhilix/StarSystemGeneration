using System;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>
/// Hash-based value noise (spec §4): lattice values from StableHash, bilinear
/// interpolation with smoothstep, fractal octaves, optional domain warp.
/// No external noise library — Core stays dependency-free.
/// </summary>
public static class ValueNoise
{
    public static double Sample(ulong seed, RollChannel channel, double x, double y,
                                int octaves, double frequency)
    {
        double sum = 0, amplitude = 1, totalAmplitude = 0;
        double f = frequency;
        for (int o = 0; o < octaves; o++)
        {
            sum += amplitude * Single(seed, channel, x * f, y * f, o);
            totalAmplitude += amplitude;
            amplitude *= 0.5;
            f *= 2.0;
        }
        return sum / totalAmplitude;
    }

    public static double Warped(ulong seed, RollChannel valueChannel, RollChannel warpChannel,
                                double x, double y, int octaves, double frequency,
                                double warpStrength)
    {
        double wx = Sample(seed, warpChannel, x + 31.7, y, 2, frequency) - 0.5;
        double wy = Sample(seed, warpChannel, x, y + 67.3, 2, frequency) - 0.5;
        return Sample(seed, valueChannel, x + wx * warpStrength, y + wy * warpStrength,
                      octaves, frequency);
    }

    private static double Single(ulong seed, RollChannel channel, double x, double y, int octave)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = SmoothStep(x - x0), ty = SmoothStep(y - y0);
        double v00 = Lattice(seed, channel, x0, y0, octave);
        double v10 = Lattice(seed, channel, x0 + 1, y0, octave);
        double v01 = Lattice(seed, channel, x0, y0 + 1, octave);
        double v11 = Lattice(seed, channel, x0 + 1, y0 + 1, octave);
        double a = v00 + (v10 - v00) * tx;
        double b = v01 + (v11 - v01) * tx;
        return a + (b - a) * ty;
    }

    private static double Lattice(ulong seed, RollChannel channel, int lx, int ly, int octave)
    {
        // Pack lattice coords the same way RollContext packs its coordinate.
        ulong coord = ((ulong)(uint)lx << 32) | (uint)ly;
        ulong h = StableHash.Mix(seed, coord, (ulong)channel, (ulong)(uint)octave);
        return (h >> 11) * (1.0 / (1UL << 53));
    }

    private static double SmoothStep(double t) => t * t * (3 - 2 * t);
}
