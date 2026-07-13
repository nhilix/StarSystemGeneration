using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The zoom continuum's bands (unity-atlas-design.md §zoom),
    /// complete since K5: System is the orbit stage.</summary>
    public enum LodBand { Galaxy, Domains, Region, Hex, System }

    /// <summary>Band thresholds and the continuous fade curves — what
    /// resolves is banded; how things scale is continuous in camera
    /// altitude (no jump-cut styling). Pure math, EditMode-testable.</summary>
    public static class LodBands
    {
        // Thresholds as camera distance over galaxy extent. A 50° fov fits
        // the disc at distance ≈ 2.1× extent, so Galaxy starts just under
        // that; Hex is a few cells from the plane.
        public const double GalaxyFloor = 1.10;
        public const double DomainsFloor = 0.45;
        public const double RegionFloor = 0.14;

        // The System band keys on ABSOLUTE distance — one hex is a fixed
        // √3 world units regardless of galaxy size (at 5 the viewport is
        // ~1.3 hexes tall at 50° fov). The extent guard keeps a toy galaxy
        // from having its Hex band swallowed.
        public const double SystemFloorAbs = 5.0;

        public static double SystemFloor(double galaxyExtent) =>
            System.Math.Min(SystemFloorAbs,
                            RegionFloor * galaxyExtent * 0.6);

        public static LodBand BandFor(double cameraDistance, double galaxyExtent)
        {
            double f = galaxyExtent <= 0 ? 10.0 : cameraDistance / galaxyExtent;
            if (f >= GalaxyFloor) return LodBand.Galaxy;
            if (f >= DomainsFloor) return LodBand.Domains;
            if (f >= RegionFloor) return LodBand.Region;
            return cameraDistance < SystemFloor(galaxyExtent)
                ? LodBand.System : LodBand.Hex;
        }

        /// <summary>The hex→orbit crossfade, map side: 1 above the window,
        /// falling to 0 as the camera reaches the System floor — every map
        /// lens multiplies this in, so the whole map dissolves together
        /// while the stage fades up (spec §zoom: "a single hex fills the
        /// viewport and crossfades to the orbit view").</summary>
        public static float MapFade(double cameraDistance, double galaxyExtent)
        {
            double floor = SystemFloor(galaxyExtent);
            if (floor <= 0) return 1f;
            double t = (cameraDistance - floor) / floor;   // window: floor..2×floor
            return Mathf.Clamp01((float)t);
        }

        /// <summary>The crossfade, stage side — the orbit view's master
        /// alpha. The stage is live whenever this is above zero.</summary>
        public static float StageFade(double cameraDistance, double galaxyExtent)
            => 1f - MapFade(cameraDistance, galaxyExtent);

        /// <summary>Lane opacity multiplier: lanes defer to the glows at
        /// altitude and reach full strength as the network resolves.</summary>
        public static float LaneFade(double cameraDistance, double galaxyExtent)
        {
            if (galaxyExtent <= 0) return 1f;
            double f = cameraDistance / galaxyExtent;
            double t = (GalaxyFloor - f) / (GalaxyFloor - DomainsFloor);
            return Mathf.Lerp(0.40f, 1f, Mathf.Clamp01((float)t))
                * MapFade(cameraDistance, galaxyExtent);
        }

        /// <summary>Glyph-mark opacity (fleets, POIs, works, plague, war
        /// stations): ghosted at altitude, resolving toward Region — the
        /// spec's "fleets and POIs resolve" band, as a continuous curve.</summary>
        public static float GlyphFade(double cameraDistance, double galaxyExtent)
        {
            if (galaxyExtent <= 0) return 1f;
            double f = cameraDistance / galaxyExtent;
            const double start = DomainsFloor * 1.4;   // begin fading in
            const double full = DomainsFloor * 0.7;    // fully resolved
            double t = (start - f) / (start - full);
            return Mathf.Lerp(0.0f, 1f, Mathf.Clamp01((float)t))
                * MapFade(cameraDistance, galaxyExtent);
        }

        /// <summary>The lattice's alpha: invisible above Region, fading to
        /// its full (still faint) strength approaching Hex — the artifact
        /// draws the grid at rgba(140,160,200,0.10).</summary>
        public static float LatticeAlpha(double cameraDistance, double galaxyExtent)
        {
            if (galaxyExtent <= 0) return 0f;
            double f = cameraDistance / galaxyExtent;
            const double start = RegionFloor * 1.6;   // begin fading in
            const double full = RegionFloor * 0.6;    // fully in
            double t = (start - f) / (start - full);
            return 0.12f * Mathf.Clamp01((float)t)
                * MapFade(cameraDistance, galaxyExtent);
        }
    }
}
