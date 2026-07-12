using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One detected era (`eras` parity).</summary>
public sealed record EraRow(string Name, EraKind Kind, int StartEpoch,
    int EndEpoch, long StartYear, long EndYear);

/// <summary>One chronicle line; EraHeader is set on every line inside
/// that era (the view draws a divider when it changes — NarrativeView's
/// stitched headers, typed).</summary>
public sealed record ChronicleLine(string Text, string? EraHeader);

/// <summary>K3: the detected-eras panel — EraDetector, typed.</summary>
public static class EraQueries
{
    public static List<EraRow> Eras(AtlasReadModel model, EyeContext eye)
    {
        var rows = new List<EraRow>();
        foreach (var era in EraDetector.Detect(model.State))
            rows.Add(new EraRow(era.Name, era.Kind, era.StartEpoch,
                era.EndEpoch, era.StartYear, era.EndYear));
        return rows;
    }
}

/// <summary>K3: the era-annotated chronicle — NarrativeView
/// RenderChronicle parity (era buckets are generations, not integration
/// steps), plus the log's place/actor indexes and the deep-time strata
/// filter.</summary>
public static class ChronicleQueries
{
    /// <summary>Every event described, era-annotated where the
    /// generational stream crosses a boundary.</summary>
    public static List<ChronicleLine> Annotated(AtlasReadModel model,
        EyeContext eye, IEnumerable<WorldEvent> events)
    {
        var state = model.State;
        var eras = EraDetector.Detect(state);
        int years = state.Config.Sim.GenerationYears;
        var lines = new List<ChronicleLine>();
        Era? current = null;
        foreach (var e in events)
        {
            if (e.Stratum == ClockStratum.Generational && e.WorldYear >= 0
                && years > 0)
            {
                var era = EraDetector.EraOf(eras, (int)(e.WorldYear / years));
                if (era != null) current = era;
            }
            lines.Add(new ChronicleLine(SimTraceView.Describe(e),
                current?.Name));
        }
        return lines;
    }

    /// <summary>One actor's biography stream (`chronicle &lt;id&gt;`).</summary>
    public static List<ChronicleLine> ForActor(AtlasReadModel model,
        EyeContext eye, int actorId) =>
        Annotated(model, eye, model.State.Log.ForActor(actorId));

    /// <summary>Everything that happened HERE — the archaeology surface
    /// (`chronicle place`).</summary>
    public static List<ChronicleLine> AtPlace(AtlasReadModel model,
        EyeContext eye, HexCoordinate hex) =>
        Annotated(model, eye, model.State.Log.AtPlace(hex));

    /// <summary>The cosmic/evolutionary strata only (`chronicle deep`).</summary>
    public static List<ChronicleLine> DeepTime(AtlasReadModel model,
                                               EyeContext eye)
    {
        var lines = new List<ChronicleLine>();
        foreach (var e in model.State.Log.Events)
        {
            if (e.Stratum is not (ClockStratum.Cosmic
                or ClockStratum.Evolutionary)) continue;
            lines.Add(new ChronicleLine(SimTraceView.Describe(e), null));
        }
        return lines;
    }
}
