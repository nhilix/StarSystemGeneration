using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One pulse in transit: word born at an event's hex, aging
/// toward expiry. Age drives the presentation's ring radius and this
/// mark's fade; magnitude drives loudness.</summary>
public readonly record struct NewsPulseMark(
    int PulseId, HexCoordinate Origin, double AgeYears, double Magnitude,
    int DeliveredCount, Rgba Color);

/// <summary>The news lens — god sees all pulses, full journeys
/// (perception-and-news.md); the controller inbox (what has arrived,
/// what is en route to you) is the reserved eye seam. Liveness parity
/// with BeliefOps.DeliverPulses: 0 ≤ age ≤ PulseMaxYears, expired word
/// is rumor and leaves the map.</summary>
public static class NewsLens
{
    // The narrative parchment gold (§8's news dot #E8D66F).
    private static readonly Rgba Parchment = new(232, 214, 111);
    private const byte FreshAlpha = 230;
    private const byte FadedAlpha = 40;

    public static IReadOnlyList<NewsPulseMark> Pulses(AtlasReadModel model,
                                                      EyeContext eye)
    {
        var state = model.State;
        double maxYears = state.Config.News.PulseMaxYears;
        var marks = new List<NewsPulseMark>();
        foreach (var pulse in state.Pulses)               // id order (P6)
        {
            double age = state.WorldYear - pulse.EmitYear;
            if (age < 0 || age > maxYears) continue;
            double fade = 1.0 - age / maxYears;
            marks.Add(new NewsPulseMark(
                pulse.Id, pulse.Origin, age, pulse.Magnitude,
                pulse.Delivered.Count,
                new Rgba(Parchment.R, Parchment.G, Parchment.B,
                         (byte)(FadedAlpha + (FreshAlpha - FadedAlpha) * fade))));
        }
        return marks;
    }
}
