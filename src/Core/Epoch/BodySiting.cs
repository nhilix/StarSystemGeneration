using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The one-time body assignment that used to be SystemQuery's
/// per-render guess (locality slice §4). Type affinity — mine → belt else
/// rock, skimmer → gas giant, agri → richest biosphere, excavation →
/// wreckage else rock — evaluated against the frozen body list, skipping
/// bodies already claimed. The terminal fallback to the port body is
/// claim-checked too, so two same-type facilities can never collapse onto
/// one body — once every candidate (including the port) is already taken,
/// assignment yields <see cref="BodyRef.None"/> rather than a collision.
/// Deterministic first-match in star/slot order.</summary>
public static class BodySiting
{
    public static BodyRef Assign(StarSystem? system, InfraTypeId type,
                                 BodyRef portBody, IEnumerable<BodyRef> claimed)
    {
        if (system == null) return BodyRef.None;
        var taken = new HashSet<BodyRef>(claimed);
        BodyRef? preferred = type switch
        {
            InfraTypeId.Mine => FirstFree(system, BodyKind.PlanetoidBelt, taken)
                ?? FirstFree(system, BodyKind.RockyWorld, taken),
            InfraTypeId.Skimmer => FirstFree(system, BodyKind.GasGiant, taken),
            InfraTypeId.AgriComplex => RichestBiosphere(system, taken),
            InfraTypeId.ExcavationSite => FirstFree(system, BodyKind.Wreckage, taken)
                ?? FirstFree(system, BodyKind.RockyWorld, taken),
            _ => null
        };
        if (preferred.HasValue) return preferred.Value;
        return taken.Contains(portBody) ? BodyRef.None : portBody;
    }

    private static BodyRef? FirstFree(StarSystem system, BodyKind kind,
                                      HashSet<BodyRef> taken)
    {
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                if (slot.Body?.Kind != kind) continue;
                var r = new BodyRef(s, slot.Index);
                if (!taken.Contains(r)) return r;
            }
        return null;
    }

    private static BodyRef? RichestBiosphere(StarSystem system,
                                             HashSet<BodyRef> taken)
    {
        BodyRef? best = null;
        var bestLife = Biosphere.Barren;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null || body.Biosphere <= bestLife) continue;
                var r = new BodyRef(s, slot.Index);
                if (taken.Contains(r)) continue;
                bestLife = body.Biosphere;
                best = r;
            }
        return best;
    }

    /// <summary>A per-body extraction multiplier in [0.5, 1.5] from the
    /// SPECIFIC claimed body (locality slice §4 throughline: body-level
    /// richness variance finally reaches the price signal). Size drives the
    /// extractor bodies; biosphere drives agri. 1.0 (neutral) for a None
    /// body, a null system, or a missing body — so legacy/unsettled
    /// facilities are unchanged. Pure, deterministic, no rolls.</summary>
    public static double RichnessModifier(StarSystem? system, BodyRef body,
                                          InfraTypeId type)
    {
        if (system == null || body.IsNone) return 1.0;
        if (body.StarIndex < 0 || body.StarIndex >= system.Stars.Count)
            return 1.0;
        Body? b = null;
        foreach (var slot in system.Stars[body.StarIndex].Slots)
            if (slot.Index == body.SlotIndex) { b = slot.Body; break; }
        if (b == null) return 1.0;
        // Body.Size is a small integer scale; map it onto [0.5, 1.5] around
        // a neutral mid so a rich belt out-yields a poor one, an airless
        // agri world under-yields a lush one — bounded, never a mint.
        double signal = type switch
        {
            InfraTypeId.AgriComplex => (int)b.Biosphere,
            _ => b.Size,
        };
        double norm = System.Math.Max(0.0, System.Math.Min(1.0, signal / 6.0));
        return 0.5 + norm;               // [0.5, 1.5]
    }

    /// <summary>Where the port docks: most-settled body (ties by size, then
    /// star/slot order), else first habitable-band body, else first body,
    /// else None — the Epoch twin of SystemQuery.PortOrbit.</summary>
    public static BodyRef PortBody(StarSystem? system)
    {
        if (system == null) return BodyRef.None;
        var best = BodyRef.None;
        int bestRank = -1;
        var firstHabitable = BodyRef.None;
        var firstBody = BodyRef.None;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null) continue;
                var at = new BodyRef(s, slot.Index);
                if (firstBody.IsNone) firstBody = at;
                if (firstHabitable.IsNone && slot.Band == OrbitBand.Habitable)
                    firstHabitable = at;
                if (body.Settlement == Settlement.None) continue;
                int rank = (int)body.Settlement * 1000 + body.Size;
                if (rank > bestRank) { bestRank = rank; best = at; }
            }
        if (!best.IsNone) return best;
        return !firstHabitable.IsNone ? firstHabitable : firstBody;
    }
}
