using StarGen.Core.Epoch;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The authored glyph vocabulary (K2): row-major cells of
    /// Resources/AtlasGlyphs.png — 16 white runtime-tinted icons
    /// (game-icons.net CC BY 3.0; GLYPH-CREDITS.md is the ledger).
    /// Order here IS the atlas layout; append, never reorder.</summary>
    public enum AtlasGlyph
    {
        FleetPosted = 0,
        FleetEscort = 1,
        FleetPatrol = 2,
        FleetBlockade = 3,
        FleetExpedition = 4,
        FleetReserve = 5,
        PoiBattlefield = 6,
        PoiRuins = 7,
        PoiRuinedCapital = 8,
        PoiMemorial = 9,
        PoiPrecursor = 10,
        WorkSite = 11,
        WorkFreight = 12,
        WorkConvoy = 13,
        PlagueInfected = 14,
        PlagueImmune = 15,
    }

    /// <summary>Atlas plumbing: the texture (Resources-loaded once) and
    /// each glyph's UV rect. Placement is data, shapes are authored,
    /// tint is runtime — the K1-amended asset boundary.</summary>
    public static class AtlasGlyphs
    {
        public const int Columns = 4;
        public const int Rows = 4;

        private static Texture2D _atlas;

        public static Texture2D Atlas
        {
            get
            {
                if (_atlas == null)
                    _atlas = Resources.Load<Texture2D>("AtlasGlyphs");
                return _atlas;
            }
        }

        /// <summary>UV rect (xMin, yMin, xMax, yMax). The PNG's first row
        /// is the image top; Unity's v axis starts at the bottom.</summary>
        public static Vector4 UvRect(AtlasGlyph glyph)
        {
            int i = (int)glyph;
            int col = i % Columns;
            int row = i / Columns;
            float u0 = (float)col / Columns;
            float v1 = 1f - (float)row / Rows;
            return new Vector4(u0, v1 - 1f / Rows, u0 + 1f / Columns, v1);
        }

        public static AtlasGlyph Of(FleetPosture posture) => posture switch
        {
            FleetPosture.Posted => AtlasGlyph.FleetPosted,
            FleetPosture.Escort => AtlasGlyph.FleetEscort,
            FleetPosture.Patrol => AtlasGlyph.FleetPatrol,
            FleetPosture.Blockade => AtlasGlyph.FleetBlockade,
            FleetPosture.Expedition => AtlasGlyph.FleetExpedition,
            _ => AtlasGlyph.FleetReserve,
        };

        public static AtlasGlyph Of(PoiType type) => type switch
        {
            PoiType.Battlefield => AtlasGlyph.PoiBattlefield,
            PoiType.Ruins => AtlasGlyph.PoiRuins,
            PoiType.RuinedCapital => AtlasGlyph.PoiRuinedCapital,
            PoiType.Memorial => AtlasGlyph.PoiMemorial,
            _ => AtlasGlyph.PoiPrecursor,
        };
    }
}
