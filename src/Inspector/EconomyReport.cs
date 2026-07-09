using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;

namespace StarGen.Inspector;

/// <summary>Economy aggregates appended to `stats` when a galaxy is loaded
/// (economy spec §8): production totals, famines, war ledger, tech spread,
/// throughput distribution.</summary>
public static class EconomyReport
{
    public static string Build(GalaxySkeleton s)
    {
        var sb = new StringBuilder();
        var living = s.Polities.Where(p => !p.Extinct).ToList();
        sb.AppendLine("— economy —");
        sb.AppendLine($"polities: {living.Count} living / {s.Polities.Count - living.Count} extinct"
            + $" · mean tech tier {(living.Count == 0 ? 0 : living.Average(p => p.TechTier)):F1}"
            + $" · total stockpile {living.Sum(p => p.MilitaryStockpile):F1}");
        sb.AppendLine($"balances (sum of living): provisions {living.Sum(p => p.ProvisionsBalance):F1}"
            + $" · ore {living.Sum(p => p.OreBalance):F1}"
            + $" · exotics {living.Sum(p => p.ExoticsBalance):F1}");
        int started = s.Events.Count(e => e.Type == GalaxyEventType.WarStarted);
        int ended = s.Events.Count(e => e.Type == GalaxyEventType.WarEnded);
        int white = s.Wars.Count(w => w.Outcome == WarOutcome.WhitePeace);
        sb.AppendLine($"wars: {started} started · {ended} ended ({white} white peace)"
            + $" · {s.Wars.Count(w => !w.Ended)} live"
            + $" · famines {s.Events.Count(e => e.Type == GalaxyEventType.Famine)}"
            + $" · trade blocked {s.Events.Count(e => e.Type == GalaxyEventType.TradeBlocked)}");
        var strainedPolities = living.Where(p => p.BlockadeLoss > 0).ToList();
        sb.AppendLine(strainedPolities.Count == 0
            ? "blockade strain: none"
            : $"blockade strain: {strainedPolities.Count} polities"
              + $" · total {strainedPolities.Sum(p => p.BlockadeLoss):F1}"
              + $" · {strainedPolities.Count(p => p.BlockadeLoss > Economy.TradeBlockedFloor)} above event floor");
        var busy = s.Cells.Where(c => c.RouteThroughput > 0).ToList();
        sb.AppendLine(busy.Count == 0
            ? "trade: no routed flows"
            : $"trade: {busy.Count} transit cells · max throughput {busy.Max(c => c.RouteThroughput):F1}"
              + $" · total {busy.Sum(c => c.RouteThroughput):F1}");
        return sb.ToString();
    }
}
