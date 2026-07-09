using System;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Substrate;

/// <summary>The natural-raster field values of one region cell, passed as
/// plain arguments so the substrate stays decoupled from the cell type the
/// state model owns (Slice B may reshape it). Class record, not record
/// struct: the Unity package compiles as C# 9.</summary>
public sealed record CellFields(
    double MeanDensity, StellarLean Lean, double Metallicity,
    bool HasMineralAnchor, bool HasPrecursorAnchor);

/// <summary>Extraction/production potentials over the genesis fields
/// (substrate/market-geography.md, infrastructure.md): pure functions of cell
/// inputs. Output *and grade* root in geography — rich cells yield better,
/// not just more.</summary>
public static class Potentials
{
    private static double Clamp01(double v) => Math.Max(0.0, Math.Min(1.0, v));

    /// <summary>Ore richness: cosmic enrichment (metallicity) plus belts
    /// (mineral anchors).</summary>
    public static double Ore(CellFields f) =>
        Clamp01(0.15 + 0.55 * f.Metallicity + (f.HasMineralAnchor ? 0.35 : 0.0));

    /// <summary>Volatiles richness: gas giants and ice worlds — gas-rich
    /// young regions and icy old ones; remnant graveyards are stripped.</summary>
    public static double Volatiles(CellFields f)
    {
        double leanBonus = f.Lean switch
        {
            StellarLean.YoungBright => 0.25,
            StellarLean.OldDim => 0.20,
            StellarLean.Balanced => 0.15,
            _ => 0.0,
        };
        return Clamp01(0.2 + 0.4 * f.MeanDensity + leanBonus);
    }

    /// <summary>Biosphere richness at the terran-analog baseline; multiply by
    /// <see cref="EmbodimentAffinity"/> for the embodiment-relative value.</summary>
    public static double Biosphere(CellFields f)
    {
        double leanBase = f.Lean switch
        {
            StellarLean.Balanced => 0.60,
            StellarLean.YoungBright => 0.35,
            StellarLean.OldDim => 0.30,
            _ => 0.05,   // remnant graveyard
        };
        return Clamp01(leanBase * (0.5 + 0.5 * f.MeanDensity));
    }

    /// <summary>Exotics richness: precursor sites and anomalies — scarce by
    /// design away from anchors.</summary>
    public static double Exotics(CellFields f) =>
        Clamp01((f.HasPrecursorAnchor ? 0.75 : 0.05)
                + (f.Lean == StellarLean.RemnantGraveyard ? 0.10 : 0.0));

    /// <summary>Grade of raw extraction from a source of this richness: rich
    /// cells yield better, not just more — but never precursor-grade.</summary>
    public static double RawGrade(double richness) =>
        Clamp01(0.15 + 0.7 * Clamp01(richness));

    /// <summary>Species-relative terrain comfort, mirroring the design's
    /// species-terrain table (read by labor and agri siting). Adaptability
    /// blending is a population-layer concern.</summary>
    public static double EmbodimentAffinity(Embodiment embodiment, CellFields f) =>
        embodiment switch
        {
            Embodiment.TerranAnalog => f.Lean switch
            {
                StellarLean.YoungBright => 1.15,
                StellarLean.OldDim => 0.8,
                StellarLean.RemnantGraveyard => 0.4,
                _ => 1.0,
            },
            Embodiment.Aquatic => f.Lean switch
            {
                StellarLean.YoungBright => 1.3,
                StellarLean.OldDim => 0.6,
                StellarLean.RemnantGraveyard => 0.3,
                _ => 1.0,
            },
            Embodiment.Cryophilic => f.Lean switch
            {
                StellarLean.YoungBright => 0.6,
                StellarLean.OldDim => 1.3,
                StellarLean.RemnantGraveyard => 0.9,
                _ => 0.7,
            },
            Embodiment.Lithic => 0.5 + f.Metallicity,
            _ => 1.0,   // Hive, Machine: broad tolerance
        };
}
