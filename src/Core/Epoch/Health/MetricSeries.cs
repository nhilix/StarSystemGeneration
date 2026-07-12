using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>The in-memory sim-health series (sim-health spec §1): what the
/// engine's always-on probe accumulates as a state steps. Deliberately
/// NEVER serialized — the artifact carries the world, not its diagnostics,
/// so the metric vocabulary can grow without format churn. A loaded
/// artifact starts empty and accumulates from wherever it steps.</summary>
public sealed class MetricSeries
{
    /// <summary>One full macro row per stepped epoch (after Chronicle).</summary>
    public List<MetricRow> Rows { get; } = new List<MetricRow>();
    /// <summary>Holder-class credit totals after every phase — the
    /// phase-attribution instrument.</summary>
    public List<MoneyRow> MoneyRows { get; } = new List<MoneyRow>();
    /// <summary>Per-entered-polity narrow rows, one set per stepped epoch.</summary>
    public List<PolityRow> PolityRows { get; } = new List<PolityRow>();
}
