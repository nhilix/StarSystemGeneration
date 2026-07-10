using System;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;

namespace StarGen.Inspector;

/// <summary>Watch mode (slice F): renders genesis working state as map
/// frames while the clocks step — gas gathering into arms, metals blooming
/// out of starbursts, life spreading, precursor domains rising and dying.
/// Frames print sequentially (pipe-safe); observation never changes the run
/// (the sims' observer contract).</summary>
public static class GenesisWatchView
{
    public static string CosmicFrameText(CosmicFrame frame, string layer)
    {
        var s = frame.State;
        // frame-local normalization: the eye tracks shape, not absolute mass
        double max = 1e-12;
        for (int i = 0; i < s.CellCount; i++)
            max = Math.Max(max, layer switch
            {
                "stars" => s.StarMass(i),
                "metals" => s.MetalsIsm[i] + s.StarMetals[i] + s.RemnantMetals[i],
                _ => s.Gas[i],
            });
        double maxLocal = max;
        string body = GalaxyMapView.RenderCells(s.Skeleton, cell =>
        {
            int i = cell.SpiralIndex;
            double v = layer switch
            {
                "stars" => s.StarMass(i),
                "metals" => s.MetalsIsm[i] + s.StarMetals[i] + s.RemnantMetals[i],
                _ => s.Gas[i],
            };
            return v <= 0 ? ' ' : GalaxyMapView.Ramp(v / maxLocal);
        });
        return Header("cosmic", layer, frame.Step, frame.StepCount, frame.WorldGyr)
            + body;
    }

    public static string EvoFrameText(EvoFrame frame, string layer)
    {
        var s = frame.State;
        var skeleton = s.Skeleton;
        string body;
        if (layer == "waves")
        {
            // live waves print as their letter; fallen extents linger as ruins
            var chars = new System.Collections.Generic.Dictionary<Core.Model.HexCoordinate, char>();
            foreach (var wave in skeleton.PrecursorWaves)
            {
                char glyph = wave.FellYear != 0 ? '·'
                    : (char)('A' + wave.Id % 26);
                foreach (var coord in wave.Cells) chars[coord] = glyph;
            }
            body = GalaxyMapView.RenderCells(skeleton, cell =>
                chars.TryGetValue(cell.Coord, out var g) ? g
                : s.Alive[cell.SpiralIndex] ? '.' : ' ');
        }
        else
            body = GalaxyMapView.RenderCells(skeleton, cell =>
            {
                int i = cell.SpiralIndex;
                if (!s.Alive[i]) return cell.IsVoid ? ' ' : '.';
                return GalaxyMapView.Ramp(s.Richness[i]);
            });
        return Header("evolutionary", layer, frame.Step, frame.StepCount, frame.WorldGyr)
            + body;
    }

    private static string Header(string clock, string layer, int step,
                                 int steps, double worldGyr)
    {
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant(
            $"── {clock} clock · {layer} · step {step + 1}/{steps} · "));
        sb.Append(FormattableString.Invariant($"{worldGyr:F2} Gyr "));
        sb.AppendLine(new string('─', 20));
        return sb.ToString();
    }
}
