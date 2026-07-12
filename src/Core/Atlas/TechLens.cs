using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>The tech lens — the tech gap made visible (emap tech parity):
/// each domain shows its owner's Astrogation tier; leaders' ports reach
/// farther, and the map says so.</summary>
public static class TechLens
{
    // Dim bronze → arc-light: the ladder's rungs brighten toward blue-white.
    private static readonly Rgba Low = new(120, 95, 70);
    private static readonly Rgba High = new(170, 215, 255);
    /// <summary>Tier where the ramp saturates — parity with emap's digit
    /// cap is 9, but real seed-scale tiers live well under this.</summary>
    private const int RampCap = 6;

    /// <summary>Per-slot Astrogation tiers, parallel to
    /// DomainLens.PolitySlots.</summary>
    public static IReadOnlyList<int> SlotTiers(AtlasReadModel model,
        EyeContext eye, IReadOnlyList<int> slots)
    {
        var tiers = new int[slots.Count];
        for (int i = 0; i < tiers.Length; i++)
            tiers[i] = Tech.Tier(model.State, slots[i], TechDomain.Astrogation);
        return tiers;
    }

    /// <summary>Bronze→arc-light ramp for a tier — the accent the field
    /// shader tints a domain with under this lens.</summary>
    public static Rgba TierColor(int tier)
    {
        double t = Math.Clamp(tier / (double)RampCap, 0.0, 1.0);
        return new Rgba(
            (byte)(Low.R + (High.R - Low.R) * t),
            (byte)(Low.G + (High.G - Low.G) * t),
            (byte)(Low.B + (High.B - Low.B) * t));
    }
}
