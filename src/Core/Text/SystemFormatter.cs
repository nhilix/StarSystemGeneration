using System.Text;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Core.Text;

/// <summary>Human-readable dump (spec §10) — also the determinism-test snapshot format.</summary>
public static class SystemFormatter
{
    public static string Format(HexResult result)
    {
        if (result.System == null)
            return $"[{result.Coordinate.X:D4}-{result.Coordinate.Y:D4}] — empty";

        var s = result.System;
        var sb = new StringBuilder();
        sb.Append($"[{result.Coordinate.X:D4}-{result.Coordinate.Y:D4}] {s.Designation}");
        if (s.GivenName != null) sb.Append($" \"{s.GivenName}\"");
        sb.Append($" · {s.Arrangement.ToString().ToLowerInvariant()}");
        if (s.OverlayId != null) sb.Append($" · overlay: {s.OverlayId}");
        sb.AppendLine();
        foreach (var tag in s.Tags) sb.AppendLine($"  ! {tag}");

        for (int i = 0; i < s.Stars.Count; i++)
        {
            var star = s.Stars[i];
            char label = (char)('A' + i);
            sb.Append($"  Star {label} — {star.TypeName}, {star.Age.ToString().ToLowerInvariant()}");
            if (star.CompanionSlotIndex is int cs) sb.Append($" (slot {cs})");
            sb.AppendLine();
            foreach (var slot in star.Slots) AppendSlot(sb, slot);
        }
        return sb.ToString();
    }

    private static void AppendSlot(StringBuilder sb, OrbitSlot slot)
    {
        string band = slot.Band switch
        {
            OrbitBand.Inner => "inner",
            OrbitBand.Habitable => "hab  ",
            _ => "outer",
        };
        if (slot.Body == null)
        {
            sb.AppendLine($"    {slot.Index} [{band}] —");
            return;
        }
        AppendBody(sb, slot.Body, $"    {slot.Index} [{band}] ", "              ");
        for (int i = 0; i < slot.Body.Satellites.Count; i++)
            AppendBody(sb, slot.Body.Satellites[i], $"        moon {(char)('a' + i)}: ", "              ");
    }

    private static void AppendBody(StringBuilder sb, Body body, string prefix, string indent)
    {
        var parts = new StringBuilder(prefix);
        parts.Append(Describe(body.Kind));
        if (body.Name != null) parts.Append($" \"{body.Name}\"");
        if (body.Size > 0) parts.Append($" · size {body.Size}");
        if (body.Kind == BodyKind.RockyWorld || body.Kind == BodyKind.IceWorld)
        {
            parts.Append($" · {Describe(body.Atmosphere)}");
            if (body.Hydrographics > 0) parts.Append($" · oceans {body.Hydrographics}%");
            if (body.Biosphere != Biosphere.Barren)
                parts.Append($" · {body.Biosphere.ToString().ToLowerInvariant()}");
        }
        sb.AppendLine(parts.ToString());

        if (body.Society is Society soc)
            sb.AppendLine($"{indent}{body.Settlement.ToString().ToLowerInvariant()} · pop tier {soc.PopulationTier}"
                + $" · {soc.Government} · {soc.Order.ToString().ToLowerInvariant()}"
                + $" · {soc.Port.ToString().ToLowerInvariant()} port");
        foreach (var tag in body.Tags) sb.AppendLine($"{indent}POI: {tag}");
    }

    private static string Describe(BodyKind kind) => kind switch
    {
        BodyKind.RockyWorld => "rocky world",
        BodyKind.IceWorld => "ice world",
        BodyKind.GasGiant => "gas giant",
        BodyKind.PlanetoidBelt => "planetoid belt",
        _ => "wreckage field",
    };

    private static string Describe(Atmosphere atmo) => atmo switch
    {
        Atmosphere.None => "no atmosphere",
        Atmosphere.Trace => "trace atmosphere",
        Atmosphere.Thin => "thin atmosphere",
        Atmosphere.Breathable => "breathable",
        Atmosphere.Dense => "dense atmosphere",
        Atmosphere.Toxic => "toxic atmosphere",
        _ => "corrosive atmosphere",
    };
}
