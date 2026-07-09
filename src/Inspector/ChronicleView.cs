using System.Text;
using StarGen.Core.Galaxy;

namespace StarGen.Inspector;

/// <summary>Event-log browser (economy spec §8): renders all event types, optional
/// polity filter, most recent last. `Describe` is the single formatting authority.</summary>
public static class ChronicleView
{
    public static string Build(GalaxySkeleton s, int polityFilter = -1, int tail = 60)
    {
        var sb = new StringBuilder();
        int shown = 0, matched = 0;
        foreach (var e in s.Events)
            if (polityFilter < 0 || e.ActorPolityId == polityFilter || e.TargetPolityId == polityFilter)
                matched++;
        int skip = matched - tail;
        foreach (var e in s.Events)
        {
            if (polityFilter >= 0 && e.ActorPolityId != polityFilter && e.TargetPolityId != polityFilter)
                continue;
            if (skip-- > 0) continue;
            sb.AppendLine(Describe(s, e));
            shown++;
        }
        if (shown == 0) sb.AppendLine("no matching events");
        else if (matched > shown) sb.AppendLine($"({matched - shown} earlier events omitted)");
        return sb.ToString();
    }

    public static string Describe(GalaxySkeleton s, GalaxyEvent e)
    {
        string actor = Name(s, e.ActorPolityId);
        string target = e.TargetPolityId >= 0 ? Name(s, e.TargetPolityId) : "";
        string at = $"[{e.Q},{e.R}]";
        return e.Type switch
        {
            GalaxyEventType.CellClaimed => $"epoch {e.Epoch}: {actor} claimed {at}",
            GalaxyEventType.WarStarted =>
                $"epoch {e.Epoch}: {actor} declared a {(WarGoal)e.Detail} war on {target} at {at}",
            GalaxyEventType.CellTaken => $"epoch {e.Epoch}: {actor} took {at} from {target}",
            GalaxyEventType.LostCapital => $"epoch {e.Epoch}: {target} lost its capital {at} to {actor}",
            GalaxyEventType.PolityExtinct => $"epoch {e.Epoch}: {actor} extinguished {target} at {at}",
            GalaxyEventType.WarEnded =>
                $"epoch {e.Epoch}: war of {actor} vs {target} ended - {(WarOutcome)e.Detail} at {at}",
            GalaxyEventType.TechAdvance => $"epoch {e.Epoch}: {actor} reached tech tier {e.Detail}",
            GalaxyEventType.Famine =>
                $"epoch {e.Epoch}: famine in {actor} territory around {at} (magnitude {e.Magnitude:F1})",
            GalaxyEventType.TradeBlocked =>
                $"epoch {e.Epoch}: {actor}'s trade blockaded (lost {e.Magnitude:F1})",
            _ => $"epoch {e.Epoch}: {e.Type} {actor} {at}",
        };
    }

    private static string Name(GalaxySkeleton s, int id) =>
        id >= 0 && id < s.Polities.Count ? s.Polities[id].Name : $"polity {id}";
}
