using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The one-time body assignment that used to be SystemQuery's
/// per-render guess (locality slice §4). Type affinity — mine → belt else
/// rock, skimmer → gas giant, agri → richest biosphere, excavation →
/// wreckage else rock — evaluated against the frozen body list, skipping
/// bodies already claimed so two same-type facilities don't collapse onto
/// one body. Deterministic first-match in star/slot order.</summary>
public static class BodySiting
{
    public static BodyRef Assign(StarSystem? system, InfraTypeId type,
                                 BodyRef portBody, IEnumerable<BodyRef> claimed)
    {
        if (system == null) return BodyRef.None;
        var taken = new HashSet<BodyRef>(claimed);
        switch (type)
        {
            case InfraTypeId.Mine:
                return FirstFree(system, BodyKind.PlanetoidBelt, taken)
                    ?? FirstFree(system, BodyKind.RockyWorld, taken) ?? portBody;
            case InfraTypeId.Skimmer:
                return FirstFree(system, BodyKind.GasGiant, taken) ?? portBody;
            case InfraTypeId.AgriComplex:
                return RichestBiosphere(system, taken) ?? portBody;
            case InfraTypeId.ExcavationSite:
                return FirstFree(system, BodyKind.Wreckage, taken)
                    ?? FirstFree(system, BodyKind.RockyWorld, taken) ?? portBody;
            default:
                return portBody;
        }
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
