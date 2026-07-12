using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

public enum PortPlagueStatus { Infected, Immune }

/// <summary>One plague-touched port: infected burns, immune carries the
/// scar until the window lapses. Healthy ports leave no mark — absence
/// is the healthy state.</summary>
public readonly record struct PlagueMark(
    int PortId, HexCoordinate Hex, PortPlagueStatus Status, Rgba Color);

/// <summary>The plague lens — contagion made visible (emap plague parity):
/// infection reads through PlagueOps.Afflicted (active strains only), the
/// immunity scar through any strain's lapse clock against the state's
/// world-year. Quarantined approaches are the lane lens's Quarantined
/// status, which the plague presentation re-emphasizes rather than
/// re-deriving.</summary>
public static class PlagueLens
{
    private static readonly Rgba InfectedBurn = new(235, 95, 60, 230);
    private static readonly Rgba ImmuneScar = new(150, 162, 150, 180);

    public static IReadOnlyList<PlagueMark> Marks(AtlasReadModel model,
                                                  EyeContext eye)
    {
        var state = model.State;
        var marks = new List<PlagueMark>();
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (PlagueOps.Afflicted(state, port.Id))
            {
                marks.Add(new PlagueMark(port.Id, port.Hex,
                                         PortPlagueStatus.Infected, InfectedBurn));
                continue;
            }
            foreach (var plague in state.Plagues)
                if (plague.ImmuneUntil.TryGetValue(port.Id, out long lapse)
                    && lapse >= state.WorldYear)
                {
                    marks.Add(new PlagueMark(port.Id, port.Hex,
                                             PortPlagueStatus.Immune, ImmuneScar));
                    break;
                }
        }
        return marks;
    }
}
