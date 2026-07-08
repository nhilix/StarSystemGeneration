using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Pure element→color mapping for the orbit diagram (orbit-diagram
    /// spec §5), sibling of LayerPalette. Settled outline matches the data
    /// panel's accent.</summary>
    public static class OrbitPalette
    {
        public static readonly Color32 Fallback = new(0xFF, 0xFF, 0xFF, 255);
        public static readonly Color32 Moon = new(0xB9, 0xBF, 0xD0, 255);
        public static readonly Color32 Ring = new(0x26, 0x2C, 0x3F, 255);
        public static readonly Color32 HabBand = new(0x3F, 0xBF, 0x7F, 26);   // 0.10 alpha
        public static readonly Color32 SettledOutline = new(0xFF, 0xBF, 0x4F, 255);

        public static Color32 StarColor(string typeId) => typeId switch
        {
            "ember_dwarf" => new Color32(0xFF, 0x8A, 0x5C, 255),
            "amber_dwarf" => new Color32(0xFF, 0xB3, 0x47, 255),
            "gold_main" => new Color32(0xFF, 0xD0, 0x66, 255),
            "white_blaze" => new Color32(0xEA, 0xF2, 0xFF, 255),
            "blue_titan" => new Color32(0x7F, 0xB8, 0xFF, 255),
            "ashen_remnant" => new Color32(0x9A, 0xA0, 0xAE, 255),
            "collapsed_core" => new Color32(0xB4, 0x8A, 0xFF, 255),
            _ => Fallback,
        };

        public static Color32 BodyColor(BodyKind kind) => kind switch
        {
            BodyKind.RockyWorld => new Color32(0xC9, 0xA0, 0x6A, 255),
            BodyKind.IceWorld => new Color32(0xA8, 0xD8, 0xE8, 255),
            BodyKind.GasGiant => new Color32(0xE0, 0x88, 0x40, 255),
            BodyKind.PlanetoidBelt => new Color32(0x9A, 0x8F, 0x7A, 255),
            BodyKind.Wreckage => new Color32(0x8A, 0x5C, 0x5C, 255),
            _ => Fallback,
        };
    }
}
