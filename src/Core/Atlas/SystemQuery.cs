using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>An orbit address inside the stage: which star, which slot.
/// None (-1,-1) is the deep-space station orbit — a port or facility with
/// no body to dock at (bodiless system, empty reach).</summary>
public readonly record struct OrbitRef(int StarIndex, int SlotIndex)
{
    public static readonly OrbitRef None = new(-1, -1);
}

/// <summary>One star of the system, stage-ready.</summary>
public sealed record StageStarRow(int Index, string TypeId, string TypeName,
                                  StarAge Age, int? CompanionSlotIndex);

/// <summary>One orbit ring: every slot of every star, occupied or not —
/// option A draws them all; belts are dashed; Habitable rings bound the
/// tinted band annulus.</summary>
public sealed record StageRingRow(int StarIndex, int SlotIndex,
                                  OrbitBand Band, bool IsBelt);

/// <summary>One occupied orbit slot: the body the hex tier put there.</summary>
public sealed record StageOrbitRow(
    int StarIndex, int SlotIndex, OrbitBand Band, BodyKind Kind, int Size,
    string? Name, Atmosphere Atmosphere, Biosphere Biosphere,
    Settlement Settlement, int SatelliteCount);

/// <summary>One standing facility, attached to its affinity orbit.</summary>
public sealed record StageFacilityRow(int Id, string TypeName,
                                      InfraFamily Family, int Tier,
                                      bool Active, double Condition,
                                      string OwnerName, OrbitRef At);

/// <summary>One in-flight construction site (the facility-to-be).</summary>
public sealed record StageSiteRow(int ProjectId, string TypeName,
                                  double Progress, OrbitRef At);

/// <summary>Everything the SystemStage renders for one hex: the hex-tier
/// system (stars, occupied orbits — pure function, computed on demand,
/// never persisted) plus the epoch overlays (port, facilities, sites)
/// attached to orbits by deterministic affinity rules.</summary>
public sealed record SystemInfo(
    HexCoordinate Hex, bool HasSystem, string Designation, string? GivenName,
    StarArrangement Arrangement, string? OverlayId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<StageStarRow> Stars, IReadOnlyList<StageRingRow> Rings,
    IReadOnlyList<StageOrbitRow> Orbits,
    int PortId, int PortTier, string? PortOwnerName, OrbitRef PortAt,
    IReadOnlyList<StageFacilityRow> Facilities,
    IReadOnlyList<StageSiteRow> Sites);

/// <summary>K5: the SystemStage's read model. The hex a player drills
/// into shows the system the generator deterministically put there and
/// the assets the simulation actually built (P1). Facilities are
/// hex-anchored in the registries; the stage docks each at a body by type
/// affinity — extraction goes where its substrate is, everything else
/// rides the port body. View-only: no RollChannel is consumed; layout
/// angles are a pure hash.</summary>
public static class SystemQuery
{
    public static SystemInfo At(AtlasReadModel model, EyeContext eye,
                                HexCoordinate hex)
    {
        var state = model.State;
        var context = new GalaxyContext(model.Skeleton.Config)
        { Skeleton = model.Skeleton };
        var system = Generator.Generate(context, hex).System;

        var stars = new List<StageStarRow>();
        var rings = new List<StageRingRow>();
        var orbits = new List<StageOrbitRow>();
        if (system != null)
        {
            for (int s = 0; s < system.Stars.Count; s++)
            {
                var star = system.Stars[s];
                stars.Add(new StageStarRow(s, star.TypeId, star.TypeName,
                                           star.Age,
                                           star.CompanionSlotIndex));
                foreach (var slot in star.Slots)
                {
                    var body = slot.Body;
                    rings.Add(new StageRingRow(s, slot.Index, slot.Band,
                        body?.Kind == BodyKind.PlanetoidBelt));
                    if (body == null) continue;
                    orbits.Add(new StageOrbitRow(s, slot.Index, slot.Band,
                        body.Kind, body.Size, body.Name, body.Atmosphere,
                        body.Biosphere, body.Settlement,
                        body.Satellites.Count));
                }
            }
        }

        int portId = -1, portTier = 0;
        string? portOwner = null;
        foreach (var port in state.Ports)                 // id order (P6)
            if (port.Hex.Equals(hex))
            {
                portId = port.Id;
                portTier = port.Tier;
                portOwner = state.Actors[port.OwnerActorId].Name;
                break;
            }
        var portAt = system != null ? PortOrbit(system) : OrbitRef.None;

        var facilities = new List<StageFacilityRow>();
        foreach (var f in state.Facilities)               // id order (P6)
        {
            if (!f.Hex.Equals(hex)) continue;
            // a facility row exists at groundbreaking; until commissioned
            // its InFlight project is the mark (sites below) — one thing,
            // one mark
            if (f.CommissionedYear < 0) continue;
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            facilities.Add(new StageFacilityRow(f.Id, def.Name, def.Family,
                f.Tier, MarketEngine.IsActive(state, f), f.Condition,
                state.Actors[f.OwnerActorId].Name,
                system != null
                    ? FacilityOrbit(system, (InfraTypeId)f.TypeId, portAt)
                    : OrbitRef.None));
        }

        var sites = new List<StageSiteRow>();
        foreach (var p in state.Projects)                 // id order (P6)
        {
            if (!p.InFlight || !p.Hex.Equals(hex)) continue;
            string name = p.Kind == ProjectKind.FacilityConstruction
                          && p.TypeId >= 0
                ? Infrastructure.Get((InfraTypeId)p.TypeId).Name
                : p.Kind.ToString();
            var at = system != null
                ? (p.Kind == ProjectKind.FacilityConstruction && p.TypeId >= 0
                    ? FacilityOrbit(system, (InfraTypeId)p.TypeId, portAt)
                    : portAt)
                : OrbitRef.None;
            sites.Add(new StageSiteRow(p.Id, name, p.Progress, at));
        }

        return system == null
            ? new SystemInfo(hex, false, "empty reach", null,
                StarArrangement.Single, null, System.Array.Empty<string>(),
                stars, rings, orbits, portId, portTier, portOwner,
                OrbitRef.None, facilities, sites)
            : new SystemInfo(hex, true, system.Designation, system.GivenName,
                system.Arrangement, system.OverlayId, system.Tags,
                stars, rings, orbits, portId, portTier, portOwner, portAt,
                facilities, sites);
    }

    /// <summary>Where the port docks: the most settled body (ties broken
    /// by size, then first in star/slot order), else the first
    /// habitable-band body, else the first body at all, else deep orbit.</summary>
    public static OrbitRef PortOrbit(StarSystem system)
    {
        var best = OrbitRef.None;
        int bestRank = -1;
        var firstHabitable = OrbitRef.None;
        var firstBody = OrbitRef.None;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null) continue;
                var at = new OrbitRef(s, slot.Index);
                if (firstBody == OrbitRef.None) firstBody = at;
                if (firstHabitable == OrbitRef.None
                    && slot.Band == OrbitBand.Habitable)
                    firstHabitable = at;
                if (body.Settlement == Settlement.None) continue;
                int rank = (int)body.Settlement * 1000 + body.Size;
                if (rank > bestRank)
                {
                    bestRank = rank;
                    best = at;
                }
            }
        if (best != OrbitRef.None) return best;
        return firstHabitable != OrbitRef.None ? firstHabitable : firstBody;
    }

    /// <summary>Where a facility type docks: extraction goes to its
    /// substrate (mine → belt else rock, skimmer → gas giant, agri → the
    /// richest biosphere, excavation → wreckage else rock); processing,
    /// heavy, support, and anything without its substrate ride the port
    /// body. Deterministic: first match in star/slot order.</summary>
    public static OrbitRef FacilityOrbit(StarSystem system, InfraTypeId type,
                                         OrbitRef portAt) =>
        type switch
        {
            InfraTypeId.Mine =>
                First(system, BodyKind.PlanetoidBelt)
                ?? First(system, BodyKind.RockyWorld) ?? portAt,
            InfraTypeId.Skimmer => First(system, BodyKind.GasGiant) ?? portAt,
            InfraTypeId.AgriComplex => RichestBiosphere(system) ?? portAt,
            InfraTypeId.ExcavationSite =>
                First(system, BodyKind.Wreckage)
                ?? First(system, BodyKind.RockyWorld) ?? portAt,
            _ => portAt,
        };

    private static OrbitRef? First(StarSystem system, BodyKind kind)
    {
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
                if (slot.Body?.Kind == kind)
                    return new OrbitRef(s, slot.Index);
        return null;
    }

    private static OrbitRef? RichestBiosphere(StarSystem system)
    {
        OrbitRef? best = null;
        var bestLife = Biosphere.Barren;
        for (int s = 0; s < system.Stars.Count; s++)
            foreach (var slot in system.Stars[s].Slots)
            {
                var body = slot.Body;
                if (body == null || body.Biosphere <= bestLife) continue;
                bestLife = body.Biosphere;
                best = new OrbitRef(s, slot.Index);
            }
        return best;
    }

    /// <summary>The body's start angle on its ring — a pure hash of
    /// (hex, star, slot) in [0, 2π): stable across rebuilds, spread so
    /// bodies don't line up. View-only; no RollChannel is consumed.</summary>
    public static double OrbitAngle(HexCoordinate hex, int starIndex,
                                    int slotIndex)
    {
        unchecked
        {
            ulong h = 1469598103934665603UL;               // fnv64
            void Mix(ref ulong acc, long v)
            {
                for (int i = 0; i < 8; i++)
                {
                    acc ^= (ulong)(v >> (i * 8)) & 0xFF;
                    acc *= 1099511628211UL;
                }
            }
            Mix(ref h, hex.Q);
            Mix(ref h, hex.R);
            Mix(ref h, starIndex);
            Mix(ref h, slotIndex);
            return h % 62832UL / 10000.0;                  // [0, 2π)
        }
    }
}
