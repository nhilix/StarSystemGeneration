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
        // extraction is body-native now (body-resource-stock design): a Mine/
        // Skimmer/Agri/Excavation with no substrate-appropriate body has
        // nothing to draw from, so it resolves None (and groundbreaking rejects
        // it) rather than riding the port body. Only non-extraction support/
        // processing assets ride the port.
        if (IsExtraction(type)) return BodyRef.None;
        return taken.Contains(portBody) ? BodyRef.None : portBody;
    }

    /// <summary>The four extraction types, whose output roots in a specific
    /// body — a depletable stock for Mine/Excavation, a renewable yield for
    /// Skimmer/Agri — never in the port body.</summary>
    public static bool IsExtraction(InfraTypeId type) =>
        type == InfraTypeId.Mine || type == InfraTypeId.Skimmer
        || type == InfraTypeId.AgriComplex
        || type == InfraTypeId.ExcavationSite;

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

    /// <summary>Renewable extraction yield in [0,1] from the claimed body's own
    /// real attributes (body-resource-stock design) — a gas giant's mass for a
    /// Skimmer, a world's biosphere and water for an AgriComplex. No depletion:
    /// the giant and the living soil replenish at any facility's scale. 0 for a
    /// missing/None body, a null system, or a non-renewable type. Pure,
    /// deterministic, no rolls.</summary>
    public static double RenewableYield(StarSystem? system, BodyRef body,
                                        InfraTypeId type)
    {
        var b = BodyAt(system, body);
        if (b == null) return 0.0;
        if (type == InfraTypeId.Skimmer)
        {
            // GasGiantSize table spans 10-14; a fat giant out-yields a lean one
            // across a [0.5, 1.0] band (never zero — any giant has mass).
            double norm = System.Math.Max(0.0,
                System.Math.Min(1.0, (b.Size - 10.0) / 4.0));
            return 0.5 + 0.5 * norm;
        }
        if (type == InfraTypeId.AgriComplex)
        {
            // living, watered worlds farm best; a barren dry rock still
            // subsistence-farms a little. Biosphere 0-3, Hydrographics 0-100.
            double bio = System.Math.Max(0.0,
                System.Math.Min(1.0, (int)b.Biosphere / 3.0));
            double water = System.Math.Max(0.0,
                System.Math.Min(1.0, b.Hydrographics / 100.0));
            return System.Math.Max(0.0,
                System.Math.Min(1.0, 0.3 + 0.5 * bio + 0.2 * water));
        }
        return 0.0;
    }

    /// <summary>Grade of renewable extraction — richer body, better grade,
    /// through the existing Potentials.RawGrade shape (no new grade math).</summary>
    public static double RenewableGrade(StarSystem? system, BodyRef body,
                                        InfraTypeId type) =>
        Potentials.RawGrade(RenewableYield(system, body, type));

    private static Body? BodyAt(StarSystem? system, BodyRef body)
    {
        if (system == null || body.IsNone) return null;
        if (body.StarIndex < 0 || body.StarIndex >= system.Stars.Count)
            return null;
        foreach (var slot in system.Stars[body.StarIndex].Slots)
            if (slot.Index == body.SlotIndex) return slot.Body;
        return null;
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
