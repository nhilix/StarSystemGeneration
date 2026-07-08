using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Pure layer->color mapping (atlas spec §4). Carries the spike's
    /// conventions: golden-ratio polity hues, brightness by development,
    /// grayscale density, white capitals.</summary>
    public static class LayerPalette
    {
        private static readonly Color32 Void = new(10, 10, 14, 255);
        private static readonly Color32 Unclaimed = new(40, 40, 40, 255);

        public static Color32 CellColor(GalaxySkeleton s, RegionCell c, AtlasLayer layer)
        {
            switch (layer)
            {
                case AtlasLayer.Polity:
                    if (c.IsVoid) return Void;
                    foreach (var p in s.Polities)
                        if (!p.Extinct && p.CapitalCoord.Equals(c.Coord))
                            return new Color32(255, 255, 255, 255);
                    if (c.OwnerPolityId < 0) return Unclaimed;
                    float hue = (c.OwnerPolityId * 0.6180339887f) % 1f;
                    float value = 0.55f + 0.09f * Mathf.Min(5, c.DevelopmentTier);
                    return (Color32)Color.HSVToRGB(hue, 0.75f, value);
                case AtlasLayer.Zone:
                    if (c.IsVoid) return Void;
                    if (c.WarScarred) return new Color32(200, 60, 50, 255);
                    if (c.IsChokepoint) return new Color32(70, 160, 180, 255);
                    return new Color32(55, 55, 60, 255);
                case AtlasLayer.Dev:
                    if (c.IsVoid) return Void;
                    if (c.OwnerPolityId < 0) return Unclaimed;
                    byte d = (byte)(70 + 37 * Mathf.Min(5, c.DevelopmentTier));
                    return new Color32(d, d, d, 255);
                case AtlasLayer.Lean:
                    if (c.IsVoid) return Void;
                    return c.Lean switch
                    {
                        StellarLean.YoungBright => new Color32(120, 170, 255, 255),
                        StellarLean.OldDim => new Color32(200, 120, 80, 255),
                        StellarLean.RemnantGraveyard => new Color32(150, 60, 150, 255),
                        _ => new Color32(110, 110, 110, 255),
                    };
                default:   // Density
                    byte g = (byte)(255 * Mathf.Clamp01((float)c.MeanDensity));
                    return new Color32(g, g, g, 255);
            }
        }

        public static Color32 HexColor(HexState state) => state switch
        {
            HexState.Void => Void,
            HexState.Empty => new Color32(28, 28, 34, 255),
            HexState.System => new Color32(190, 190, 200, 255),
            HexState.Settled => new Color32(255, 190, 80, 255),
            HexState.Anchored => new Color32(120, 220, 160, 255),
            _ => Void,
        };

        public static Color32 Highlight(Color32 c) => new(
            (byte)Mathf.Min(255, c.r + 60), (byte)Mathf.Min(255, c.g + 60),
            (byte)Mathf.Min(255, c.b + 60), 255);
    }
}
