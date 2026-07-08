using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Diagram address of a drawable element: Slot -1 = the star itself,
    /// Moon -1 = the slot body. Slot is the position in Star.Slots (coincides with
    /// OrbitSlot.Index today, but the list position is the contract).</summary>
    public readonly struct BodyRef : IEquatable<BodyRef>
    {
        public readonly int Star;
        public readonly int Slot;
        public readonly int Moon;

        public BodyRef(int star, int slot, int moon)
        {
            Star = star; Slot = slot; Moon = moon;
        }

        public bool Equals(BodyRef other) =>
            Star == other.Star && Slot == other.Slot && Moon == other.Moon;
        public override bool Equals(object? obj) => obj is BodyRef other && Equals(other);
        public override int GetHashCode() => (Star * 397 + Slot) * 397 + Moon;
        public override string ToString() => $"BodyRef({Star},{Slot},{Moon})";
    }

    public readonly struct RingSpec
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly bool IsBelt;
        /// <summary>The slot this ring belongs to (Moon = -1).</summary>
        public readonly BodyRef Ref;

        public RingSpec(Vector2 center, float radius, bool isBelt, BodyRef slotRef)
        {
            Center = center; Radius = radius; IsBelt = isBelt; Ref = slotRef;
        }
    }

    public readonly struct BandSpec
    {
        public readonly Vector2 Center;
        public readonly float Inner;
        public readonly float Outer;

        public BandSpec(Vector2 center, float inner, float outer)
        {
            Center = center; Inner = inner; Outer = outer;
        }
    }

    public readonly struct StarSpec
    {
        public readonly Vector2 Pos;
        public readonly float Radius;
        public readonly int StarIndex;
        public readonly string TypeId;

        public StarSpec(Vector2 pos, float radius, int starIndex, string typeId)
        {
            Pos = pos; Radius = radius; StarIndex = starIndex; TypeId = typeId;
        }
    }

    public readonly struct BodySpec
    {
        public readonly Vector2 Pos;
        public readonly float Radius;
        public readonly BodyRef Ref;
        public readonly BodyKind Kind;
        public readonly bool Settled;
        public readonly int Hydrographics;    // 0-100 surface coverage %
        /// <summary>Deterministic unit hash for visual detail placement
        /// (ocean blobs, gas-band count) — same system, same picture.</summary>
        public readonly float DetailHash;

        public BodySpec(Vector2 pos, float radius, BodyRef bodyRef, BodyKind kind,
            bool settled, int hydrographics, float detailHash)
        {
            Pos = pos; Radius = radius; Ref = bodyRef; Kind = kind; Settled = settled;
            Hydrographics = hydrographics; DetailHash = detailHash;
        }
    }

    public readonly struct PickTarget
    {
        public readonly Vector2 Pos;
        public readonly float PickRadius;
        public readonly BodyRef Ref;

        public PickTarget(Vector2 pos, float pickRadius, BodyRef bodyRef)
        {
            Pos = pos; PickRadius = pickRadius; Ref = bodyRef;
        }
    }

    public sealed class OrbitLayoutResult
    {
        /// <summary>Primary slots first in slot order, then each companion's
        /// sub-rings grouped in Stars order.</summary>
        public List<RingSpec> Rings { get; } = new();
        public List<BandSpec> HabBands { get; } = new();
        public List<StarSpec> Stars { get; } = new();
        public List<BodySpec> Bodies { get; } = new();
        public List<PickTarget> Picks { get; } = new();
        public Rect Bounds { get; internal set; }
    }

    /// <summary>Pure nested-concentric geometry (orbit-diagram spec §4): no render
    /// types, edit-mode testable. All angles are decorative and derive from
    /// StableHash of the designation — a system always draws the same picture.</summary>
    public static class OrbitLayout
    {
        public const float R0 = 1.0f;               // innermost gap
        public const float DR = 0.5f;               // default ring gap
        public const float PrimaryDisc = 0.28f;
        public const float CompanionDisc = 0.16f;
        // Body discs cap at 0.15 (size 10): distinctly below the companion disc
        // and roughly half the primary, so star > planet > moon stays readable.
        public const float BodyDiscBase = 0.05f;
        public const float BodyDiscPerSize = 0.010f;
        public const float MoonDisc = 0.035f;
        public const float MoonOrbitPad = 0.09f;
        public const float RingStroke = 0.02f;
        public const float SubDrMin = 0.11f;        // minimum companion sub-ring spacing
        public const float MinPickRadius = 0.12f;
        public const float BeltPickRadius = 0.3f;
        public const float HabHalfWidthFactor = 0.45f;

        private const uint OrbitChannel = 0xA1;
        private const uint MoonChannel = 0xA2;
        private const uint DetailChannel = 0xA3;

        /// <summary>Widened gap around a companion slot: at least a doubled swath
        /// (its gravitational influence clears the primary's disc), more when the
        /// companion needs room for many sub-rings (spec §4).</summary>
        public static float CompanionClearance(int subSlotCount) =>
            Math.Max(2f * DR, (CompanionDisc + (subSlotCount + 1) * SubDrMin) / 0.9f);

        public static OrbitLayoutResult Compute(StarSystem system)
        {
            var result = new OrbitLayoutResult();
            if (system.Stars.Count == 0)
            {
                result.Bounds = new Rect(-1f, -1f, 2f, 2f);
                return result;
            }

            int primaryIndex = 0;
            for (int i = 0; i < system.Stars.Count; i++)
                if (system.Stars[i].CompanionSlotIndex == null) { primaryIndex = i; break; }
            var primary = system.Stars[primaryIndex];

            // Companion gap clearances, keyed by the primary slot each occupies.
            var clearance = new Dictionary<int, float>();
            for (int i = 0; i < system.Stars.Count; i++)
            {
                if (i == primaryIndex || system.Stars[i].CompanionSlotIndex is not { } rawSlot)
                    continue;
                if (primary.Slots.Count == 0) break;   // degenerate, never generated
                int slot = Math.Clamp(rawSlot, 0, primary.Slots.Count - 1);
                float widened = CompanionClearance(system.Stars[i].Slots.Count);
                clearance[slot] = clearance.TryGetValue(slot, out var existing)
                    ? Math.Max(existing, widened) : widened;
            }

            // Cumulative primary ring radii: gap i defaults to DR (R0 innermost),
            // widened on both sides of a companion slot.
            var radii = new float[primary.Slots.Count];
            float r = 0f;
            for (int i = 0; i < primary.Slots.Count; i++)
            {
                float gap = i == 0 ? R0 : DR;
                if (clearance.TryGetValue(i, out var into)) gap = Math.Max(gap, into);
                if (i > 0 && clearance.TryGetValue(i - 1, out var outOf)) gap = Math.Max(gap, outOf);
                r += gap;
                radii[i] = r;
            }

            result.Stars.Add(new StarSpec(Vector2.zero, PrimaryDisc, primaryIndex, primary.TypeId));
            result.Picks.Add(new PickTarget(Vector2.zero, PickRadiusFor(PrimaryDisc),
                new BodyRef(primaryIndex, -1, -1)));
            LayoutStar(result, system.Designation, primaryIndex, primary, Vector2.zero,
                radii, HabHalfWidthFactor * DR, bodyScale: 1f);

            for (int i = 0; i < system.Stars.Count; i++)
            {
                if (i == primaryIndex || system.Stars[i].CompanionSlotIndex is not { } rawSlot)
                    continue;
                if (primary.Slots.Count == 0) break;
                var companion = system.Stars[i];
                int slot = Math.Clamp(rawSlot, 0, primary.Slots.Count - 1);
                float angle = 2f * Mathf.PI * UnitHash(system.Designation, OrbitChannel, i, slot);
                var center = radii[slot] * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int subSlots = companion.Slots.Count;
                float subDr = subSlots > 0
                    ? (0.9f * clearance[slot] - CompanionDisc) / (subSlots + 1) : 0f;
                var subRadii = new float[subSlots];
                for (int j = 0; j < subSlots; j++)
                    subRadii[j] = CompanionDisc + (j + 1) * subDr;

                result.Stars.Add(new StarSpec(center, CompanionDisc, i, companion.TypeId));
                result.Picks.Add(new PickTarget(center, PickRadiusFor(CompanionDisc),
                    new BodyRef(i, -1, -1)));
                // Companion orbits are compressed by subDr/DR; its bodies and
                // moons scale by the same ratio so they fit their rings. Clamped
                // at 1 so a shared-slot clearance can never inflate bodies past
                // the size hierarchy (star > planet > moon).
                LayoutStar(result, system.Designation, i, companion, center,
                    subRadii, HabHalfWidthFactor * subDr,
                    bodyScale: Mathf.Min(1f, subDr / DR));
            }

            result.Bounds = ComputeBounds(result);
            return result;
        }

        /// <summary>Nearest pick target containing the world point; null when none.</summary>
        public static BodyRef? PickAt(OrbitLayoutResult layout, Vector2 world)
        {
            BodyRef? best = null;
            float bestDistance = float.MaxValue;
            foreach (var target in layout.Picks)
            {
                float distance = Vector2.Distance(world, target.Pos);
                if (distance <= target.PickRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target.Ref;
                }
            }
            return best;
        }

        private static void LayoutStar(OrbitLayoutResult result, string designation,
            int starIndex, Star star, Vector2 center, float[] radii, float habHalf,
            float bodyScale)
        {
            int firstHab = -1, lastHab = -1;
            for (int i = 0; i < star.Slots.Count; i++)
                if (star.Slots[i].Band == OrbitBand.Habitable)
                {
                    if (firstHab < 0) firstHab = i;
                    lastHab = i;
                }
            if (firstHab >= 0)
                result.HabBands.Add(new BandSpec(center,
                    Math.Max(0f, radii[firstHab] - habHalf), radii[lastHab] + habHalf));

            for (int i = 0; i < star.Slots.Count; i++)
            {
                var slot = star.Slots[i];
                var body = slot.Body;
                bool isBelt = body != null && body.Kind == BodyKind.PlanetoidBelt;
                var slotRef = new BodyRef(starIndex, i, -1);
                result.Rings.Add(new RingSpec(center, radii[i], isBelt, slotRef));

                if (body == null) continue;
                float angle = 2f * Mathf.PI * UnitHash(designation, OrbitChannel, starIndex, i);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (isBelt)
                {
                    // Belts draw as their dashed ring; the pick target is a point on
                    // the ring at the slot angle with an enlarged radius (spec §4),
                    // compressed like everything else inside a companion so it
                    // cannot eclipse a sub-system's empty space.
                    result.Picks.Add(new PickTarget(center + radii[i] * direction,
                        Math.Max(bodyScale * BeltPickRadius, MinPickRadius), slotRef));
                    continue;
                }

                float discRadius = bodyScale * (BodyDiscBase + BodyDiscPerSize * body.Size);
                var pos = center + radii[i] * direction;
                result.Bodies.Add(new BodySpec(pos, discRadius, slotRef, body.Kind,
                    body.Settlement != Settlement.None, body.Hydrographics,
                    UnitHash(designation, DetailChannel, starIndex, i)));
                result.Picks.Add(new PickTarget(pos, PickRadiusFor(discRadius), slotRef));

                int moonCount = body.Satellites.Count;
                for (int m = 0; m < moonCount; m++)
                {
                    float start = UnitHash(designation, MoonChannel, starIndex, i);
                    float moonAngle = 2f * Mathf.PI * (start + (float)m / moonCount);
                    var moonPos = pos + (discRadius + bodyScale * MoonOrbitPad)
                        * new Vector2(Mathf.Cos(moonAngle), Mathf.Sin(moonAngle));
                    var moon = body.Satellites[m];
                    var moonRef = new BodyRef(starIndex, i, m);
                    result.Bodies.Add(new BodySpec(moonPos, bodyScale * MoonDisc, moonRef,
                        moon.Kind, moon.Settlement != Settlement.None, moon.Hydrographics,
                        UnitHash(designation, DetailChannel, starIndex, i)));
                    result.Picks.Add(new PickTarget(moonPos,
                        PickRadiusFor(bodyScale * MoonDisc), moonRef));
                }
            }
        }

        private static float PickRadiusFor(float discRadius) =>
            Math.Max(discRadius * 1.6f, MinPickRadius);

        private static float UnitHash(string designation, uint channel, int star, int slot)
        {
            ulong h = StableHash.Mix(Fnv1a(designation), channel,
                (ulong)(uint)star, (ulong)(uint)slot);
            return (float)((h >> 11) * (1.0 / (1UL << 53)));
        }

        private static ulong Fnv1a(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        private static Rect ComputeBounds(OrbitLayoutResult result)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            void Include(Vector2 p, float extent)
            {
                minX = Math.Min(minX, p.x - extent);
                maxX = Math.Max(maxX, p.x + extent);
                minY = Math.Min(minY, p.y - extent);
                maxY = Math.Max(maxY, p.y + extent);
            }

            foreach (var ring in result.Rings) Include(ring.Center, ring.Radius + RingStroke);
            foreach (var band in result.HabBands) Include(band.Center, band.Outer);
            foreach (var star in result.Stars) Include(star.Pos, star.Radius);
            foreach (var body in result.Bodies) Include(body.Pos, body.Radius);
            if (minX > maxX) return new Rect(-1f, -1f, 2f, 2f);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
