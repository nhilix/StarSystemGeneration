using System;

namespace StarGen.Core.Substrate;

/// <summary>The production formula as pure functions
/// (substrate/infrastructure.md): output = base(type, tier) × terrain ×
/// labor × machineryGrade × automation. Facility state (condition, ownership,
/// damage) is economy-layer concern — nothing here holds state.</summary>
public static class Production
{
    /// <summary>base(type, tier) output scaling — superlinear so tiers matter.</summary>
    public static double TierOutputFactor(int tier) => tier switch
    {
        <= 1 => 1.0,
        2 => 2.5,
        _ => 6.0,
    };

    /// <summary>Build-cost scaling per tier, applied to <see cref="InfraDef.BuildCost"/>.</summary>
    public static double TierCostFactor(int tier) => tier switch
    {
        <= 1 => 1.0,
        2 => 3.0,
        _ => 8.0,
    };

    /// <summary>Workforce term: domain population × embodiment affinity, with
    /// compute-driven automation substituting for people (machine polities run
    /// thin-crewed industry). Clamped at full staffing.</summary>
    public static double LaborFactor(double populationLabor, double embodimentAffinity,
                                     double automationCompute, double required)
    {
        if (required <= 0) return 1.0;
        double staffed = (populationLabor * embodimentAffinity + automationCompute) / required;
        return Math.Max(0.0, Math.Min(1.0, staffed));
    }

    /// <summary>Facility output per world-year. Terrain is the extraction
    /// potential at the facility's hex (1.0 for processing); machinery grade
    /// multiplies productivity through the one grade interface.</summary>
    public static double Output(InfraDef def, int tier, double terrain,
                                double laborFactor, double machineryGrade) =>
        def.BaseOutputPerYear * TierOutputFactor(tier) * terrain * laborFactor
        * Grades.Multiplier(UseCase.IndustryUpkeep, machineryGrade);

    /// <summary>Grade of subsistence-farmed output — crude, always.</summary>
    public const double OrganicBaselineGrade = 0.15;

    /// <summary>Settled populations subsistence-farm and craft locally without
    /// facilities: unserviced systems are poor, not starving-by-definition.
    /// Small enough that facilities always dominate where they exist.</summary>
    public static double OrganicBaseline(double population, double biosphereRichness) =>
        0.4 * Math.Max(0.0, population) * Math.Max(0.0, biosphereRichness);
}
