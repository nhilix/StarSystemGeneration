using System;

namespace StarGen.Core.Atlas;

/// <summary>Engine-free color primitive — the read model speaks colors so
/// every palette decision is xUnit-coverable; the Unity side casts to
/// Color32 without deriving anything.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public Rgba(byte r, byte g, byte b) : this(r, g, b, 255) { }
}

/// <summary>Palette discipline (PoC lesson): pure value→color mapping in
/// one place — dark-map conventions, golden-ratio actor hues, ramps from a
/// common floor so stacked lenses read against the same darkness.</summary>
public static class AtlasPalette
{
    /// <summary>Void cells and the map background — the wilds visibly dark.</summary>
    public static readonly Rgba Void = new(10, 10, 14);
    /// <summary>Non-void cells a lens has nothing to say about.</summary>
    public static readonly Rgba Floor = new(24, 26, 32);
    /// <summary>Fully transparent — overlay lenses return this where the
    /// layers beneath should show through.</summary>
    public static readonly Rgba Clear = new(0, 0, 0, 0);

    /// <summary>Floor→base ramp by v∈[0,1] — every scalar raster reads on
    /// the same darkness scale.</summary>
    public static Rgba Ramp(Rgba baseColor, double v)
    {
        double t = Math.Clamp(v, 0.0, 1.0);
        return new Rgba(
            (byte)(Floor.R + (baseColor.R - Floor.R) * t),
            (byte)(Floor.G + (baseColor.G - Floor.G) * t),
            (byte)(Floor.B + (baseColor.B - Floor.B) * t));
    }

    /// <summary>Golden-ratio hue per actor id (PoC convention) — stable,
    /// collision-resistant polity colors without a stored palette.</summary>
    public static Rgba OwnerColor(int actorId)
    {
        double hue = actorId * 0.6180339887 % 1.0;
        return Hsv(hue, 0.72, 0.78);
    }

    public static Rgba Hsv(double h, double s, double v)
    {
        double r = v, g = v, b = v;
        if (s > 0)
        {
            double sector = h * 6.0 % 6.0;
            int i = (int)sector;
            double f = sector - i;
            double p = v * (1.0 - s);
            double q = v * (1.0 - s * f);
            double t = v * (1.0 - s * (1.0 - f));
            (r, g, b) = i switch
            {
                0 => (v, t, p),
                1 => (q, v, p),
                2 => (p, v, t),
                3 => (p, q, v),
                4 => (t, p, v),
                _ => (v, p, q),
            };
        }
        return new Rgba((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
