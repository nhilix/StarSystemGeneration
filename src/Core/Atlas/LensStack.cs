using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>Compositing rule for stacked cell-shading lenses: straight
/// source-over alpha blend onto an opaque base. Lives Core-side so the
/// blend the user eyeballs is the blend the tests pin.</summary>
public static class LensStack
{
    public static IReadOnlyList<Rgba> Composite(IReadOnlyList<Rgba> under,
                                                IReadOnlyList<Rgba> over)
    {
        if (under.Count != over.Count)
            throw new ArgumentException("lens lists must run parallel");
        var outp = new Rgba[under.Count];
        for (int i = 0; i < outp.Length; i++)
        {
            var u = under[i];
            var o = over[i];
            int a = o.A;
            outp[i] = new Rgba(
                (byte)((o.R * a + u.R * (255 - a)) / 255),
                (byte)((o.G * a + u.G * (255 - a)) / 255),
                (byte)((o.B * a + u.B * (255 - a)) / 255),
                u.A);
        }
        return outp;
    }
}

/// <summary>One drawable port: the domain glow's anchor point, brightened
/// past the owner color so the keystone reads above its own glow.
/// ServiceRadiusHexes sizes the glow field — tier-derived, never stored.</summary>
public readonly record struct PortMarker(
    int PortId, HexCoordinate Hex, int Tier, int OwnerActorId, Rgba Color,
    int ServiceRadiusHexes);

public static class PortLens
{
    public static IReadOnlyList<PortMarker> Markers(AtlasReadModel model,
                                                    EyeContext eye)
    {
        var ports = model.State.Ports;
        var markers = new PortMarker[ports.Count];
        for (int i = 0; i < markers.Length; i++)
        {
            var p = ports[i];
            var own = AtlasPalette.OwnerColor(p.OwnerActorId);
            // Solid markers need only a nudge above their own glow — the
            // half-white lift of the soft-dot era washed them cream.
            var bright = new Rgba(
                (byte)(own.R + (255 - own.R) / 4),
                (byte)(own.G + (255 - own.G) / 4),
                (byte)(own.B + (255 - own.B) / 4));
            markers[i] = new PortMarker(p.Id, p.Hex, p.Tier, p.OwnerActorId, bright,
                PortDomains.ServiceRadius(model.State.Config, p.Tier));
        }
        return markers;
    }
}
