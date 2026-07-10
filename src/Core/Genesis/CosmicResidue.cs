using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Genesis;

/// <summary>Compresses the cosmic working state onto the region cells as
/// present-day residue (genesis/cosmic-genesis.md §Provided interface):
/// density from total mass, lean from cohort mix, metallicity from locked
/// stellar metals, plus gas fraction, mineral richness, star-formation
/// activity, and the habitability history scalars. Replaces seeding passes
/// 1–2's analytic paint outright — every value here traces to simulated
/// causes.</summary>
public static class CosmicResidue
{
    // -- lean thresholds (structural). The design's derivation is activity-
    // shaped, not raw cohort mass: young-bright where star formation is
    // *still active*, old-dim where gas burned early, graveyard where it
    // burned early and hard (cosmic-genesis.md §Present-day derivations).
    // Raw young-mass share can never work — young stars are ~0.2% of 14 Gyr
    // of accumulated mass everywhere. --
    /// <summary>Remnant share of stellar+remnant mass above which a cell
    /// reads as graveyard terrain.</summary>
    private const double GraveyardRemnantShare = 0.45;
    /// <summary>Normalized SF activity above which a cell reads young-bright.</summary>
    private const double YoungBrightActivity = 0.5;
    /// <summary>SF activity below which a cell can read old-dim…</summary>
    private const double OldDimActivity = 0.12;
    /// <summary>…provided its gas also burned away.</summary>
    private const double OldDimGasFraction = 0.08;
    /// <summary>Compressive exponent mapping relative cell mass to the [0,1]
    /// density scalar: the simulated mass distribution is heavy-tailed, and
    /// a linear map reads as a spike field over a void sea. Presentation
    /// only — the mass itself stays conserved and raw.</summary>
    private const double DensityExponent = 0.45;
    /// <summary>Normalization percentile for the open-ended fields
    /// (metallicity, mineral richness, SF activity): the p95 cell maps
    /// to 1.0 — deterministic per run, robust to outliers.</summary>
    private const double ScalePercentile = 0.95;

    public static void Compress(CosmicState s)
    {
        var skeleton = s.Skeleton;
        var config = skeleton.Config;
        int n = s.CellCount;

        double massMean = 0;
        for (int i = 0; i < n; i++) massMean += s.TotalMass(i);
        massMean = Math.Max(massMean / n, 1e-12);

        double zScale = Percentile(s, i => s.StarZ(i));
        double mineralScale = Percentile(s, i => s.RemnantMetals[i]);
        double sfScale = Percentile(s, i => s.SfRecent[i]);

        for (int i = 0; i < n; i++)
        {
            var cell = skeleton.Cells[i];
            double mass = s.TotalMass(i);

            cell.MeanDensity = Math.Clamp(
                Math.Pow(mass / massMean, DensityExponent)
                    * config.MeanDensityTarget, 0.0, 1.0);
            cell.IsVoid = cell.MeanDensity < config.TraversabilityThreshold;
            cell.GasFraction = mass > 0 ? s.Gas[i] / mass : 0.0;

            double stellar = s.StarMass(i) + s.Remnants[i];
            if (stellar > 0)
            {
                cell.CohortYoung = s.StarsYoung[i] / stellar;
                cell.CohortMid = s.StarsMid[i] / stellar;
                cell.CohortOld = s.StarsOld[i] / stellar;
                cell.CohortRemnant = s.Remnants[i] / stellar;
            }
            else
            {
                cell.CohortYoung = cell.CohortMid = cell.CohortOld = 0;
                cell.CohortRemnant = 0;
            }

            cell.Metallicity = Math.Clamp(s.StarZ(i) / zScale, 0.0, 1.0);
            cell.MineralRichness = Math.Clamp(
                s.RemnantMetals[i] / mineralScale, 0.0, 1.0);
            cell.SfActivity = Math.Clamp(s.SfRecent[i] / sfScale, 0.0, 1.0);
            cell.Lean = DeriveLean(cell);
            cell.LifeViableStep = s.LifeViableStep[i];
            cell.LastSterilizedStep = s.LastSterilizationStep[i];
        }
    }

    /// <summary>The lean vocabulary survives as a *derivation*: young-bright
    /// where star formation is still active, old-dim where gas burned early,
    /// graveyard where it burned early and hard. Requires Metallicity /
    /// SfActivity / GasFraction to be written first.</summary>
    private static StellarLean DeriveLean(RegionCell cell) =>
        cell.CohortRemnant > GraveyardRemnantShare ? StellarLean.RemnantGraveyard
        : cell.SfActivity > YoungBrightActivity ? StellarLean.YoungBright
        : cell.SfActivity < OldDimActivity && cell.GasFraction < OldDimGasFraction
            ? StellarLean.OldDim
        : StellarLean.Balanced;

    /// <summary>p95 of positive values (spiral-order stable), or 1 when the
    /// field is entirely empty.</summary>
    private static double Percentile(CosmicState s, Func<int, double> field)
    {
        var positive = new List<double>();
        for (int i = 0; i < s.CellCount; i++)
        {
            double v = field(i);
            if (v > 0) positive.Add(v);
        }
        if (positive.Count == 0) return 1.0;
        positive.Sort();
        int at = Math.Min(positive.Count - 1,
            (int)(positive.Count * ScalePercentile));
        return Math.Max(positive[at], 1e-12);
    }
}
