namespace StarGen.AtlasView
{
    /// <summary>The zoom continuum's bands (unity-atlas-design.md §zoom):
    /// System joins in K5 when the orbit stage lands.</summary>
    public enum LodBand { Galaxy, Domains, Region, Hex }

    /// <summary>Pure band math + per-band lens styling — what fades per band
    /// is a property of each lens, owned here (the LODController's table),
    /// EditMode-testable without a camera.</summary>
    public static class LodBands
    {
        // Band thresholds as fractions of the galaxy's world extent: an
        // ortho half-height showing most of the disc reads Galaxy; a few
        // domains, Domains; a handful of cells, Region; the lattice, Hex.
        public const double GalaxyFloor = 0.55;
        public const double DomainsFloor = 0.22;
        public const double RegionFloor = 0.07;

        public static LodBand BandFor(double orthoSize, double galaxyExtent)
        {
            double f = galaxyExtent <= 0 ? 1.0 : orthoSize / galaxyExtent;
            if (f >= GalaxyFloor) return LodBand.Galaxy;
            if (f >= DomainsFloor) return LodBand.Domains;
            if (f >= RegionFloor) return LodBand.Region;
            return LodBand.Hex;
        }

        /// <summary>Lane half-width in hex units — highways at a distance,
        /// threads up close.</summary>
        public static float LaneWidth(LodBand band) => band switch
        {
            LodBand.Galaxy => 2.2f,
            LodBand.Domains => 1.4f,
            LodBand.Region => 0.8f,
            _ => 0.35f,
        };

        /// <summary>Port marker radius multiplier per band.</summary>
        public static float PortScale(LodBand band) => band switch
        {
            LodBand.Galaxy => 4.0f,
            LodBand.Domains => 2.5f,
            LodBand.Region => 1.4f,
            _ => 0.9f,
        };
    }
}
