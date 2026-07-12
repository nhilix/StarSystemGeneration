using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>The tension lens — the pressure gauge (emap tension parity:
/// digit = round(heat × 9)): each domain shaded by its owner's hottest
/// live relation. Where the powder is, before the war lens has anything
/// to say.</summary>
public static class TensionLens
{
    // Cold steel → ember: the gauge's two ends.
    private static readonly Rgba Cold = new(95, 105, 130);
    private static readonly Rgba Ember = new(240, 130, 50);

    /// <summary>The owner's hottest live relation, 0–1 (TensionGlyph's
    /// digit is this × 9, rounded).</summary>
    public static double HeatOf(AtlasReadModel model, EyeContext eye,
                                int actorId)
    {
        double hottest = 0;
        foreach (var rel in model.State.Relations)        // creation order (P6)
            if (rel.Involves(actorId)
                && RelationsOps.BothLive(model.State, rel)
                && rel.Tension > hottest)
                hottest = rel.Tension;
        return hottest;
    }

    /// <summary>Per-slot heat, parallel to DomainLens.PolitySlots.</summary>
    public static IReadOnlyList<double> SlotHeat(AtlasReadModel model,
        EyeContext eye, IReadOnlyList<int> slots)
    {
        var heat = new double[slots.Count];
        for (int i = 0; i < heat.Length; i++)
            heat[i] = HeatOf(model, eye, slots[i]);
        return heat;
    }

    /// <summary>Cold→ember ramp for a heat value — the accent the field
    /// shader tints a domain with under this lens.</summary>
    public static Rgba HeatColor(double heat)
    {
        double t = Math.Clamp(heat, 0.0, 1.0);
        return new Rgba(
            (byte)(Cold.R + (Ember.R - Cold.R) * t),
            (byte)(Cold.G + (Ember.G - Cold.G) * t),
            (byte)(Cold.B + (Ember.B - Cold.B) * t));
    }
}
