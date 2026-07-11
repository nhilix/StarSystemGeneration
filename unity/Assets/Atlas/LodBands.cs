using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The zoom continuum's bands (unity-atlas-design.md §zoom):
    /// System joins in K5 when the orbit stage lands.</summary>
    public enum LodBand { Galaxy, Domains, Region, Hex }

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

        public static LodBand BandFor(double cameraDistance, double galaxyExtent)
        {
            double f = galaxyExtent <= 0 ? 10.0 : cameraDistance / galaxyExtent;
            if (f >= GalaxyFloor) return LodBand.Galaxy;
            if (f >= DomainsFloor) return LodBand.Domains;
            if (f >= RegionFloor) return LodBand.Region;
            return LodBand.Hex;
        }

        /// <summary>Lane opacity multiplier: lanes defer to the glows at
        /// altitude and reach full strength as the network resolves.</summary>
        public static float LaneFade(double cameraDistance, double galaxyExtent)
        {
            if (galaxyExtent <= 0) return 1f;
            double f = cameraDistance / galaxyExtent;
            double t = (GalaxyFloor - f) / (GalaxyFloor - DomainsFloor);
            return Mathf.Lerp(0.40f, 1f, Mathf.Clamp01((float)t));
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
            return 0.12f * Mathf.Clamp01((float)t);
        }
    }
}
