using System.Collections.Generic;
using System.IO;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>One captured moment on a timeline: the sim clock at capture
/// plus the delta save that reconstructs it over the base artifact
/// (empty for the base keyframe itself).</summary>
public sealed record Keyframe(int EpochIndex, long WorldYear,
    int YearsPerEpoch, string Delta)
{
    /// <summary>Flows captured by the step that PRODUCED this keyframe
    /// (AC2.F2 recent flows) — held in-memory beside the frame, never
    /// serialized. The base frame has none: no step preceded it
    /// in-session, and a re-loaded artifact starts the same way.</summary>
    public IReadOnlyList<RecentFlow> RecentFlows { get; init; }
        = System.Array.Empty<RecentFlow>();
}

/// <summary>One timeline: a keyframe list stepped at one resolution.
/// A fork carries the shared past up to its anchor keyframe and nothing
/// after; the root has no parent (ForkedFromBranch −1).</summary>
public sealed class TimelineBranch
{
    public int ForkedFromBranch { get; }
    public int ForkedAtKeyframe { get; }
    /// <summary>YearsPerEpoch this branch steps at — one timeline belongs
    /// to one (config, tick-path) run.</summary>
    public int Resolution { get; internal set; }
    internal List<Keyframe> Frames { get; }
    public IReadOnlyList<Keyframe> Keyframes => Frames;

    internal TimelineBranch(int forkedFromBranch, int forkedAtKeyframe,
        int resolution, List<Keyframe> frames)
    {
        ForkedFromBranch = forkedFromBranch;
        ForkedAtKeyframe = forkedAtKeyframe;
        Resolution = resolution;
        Frames = frames;
    }
}

/// <summary>K4 (unity-atlas-design.md §Time): captures epoch keyframes
/// while stepping, stored as delta saves against the loaded base
/// (DeltaSerializer — base + changed layers only; genesis strata never
/// re-record). Scrubbing snaps to a keyframe and rebuilds the live
/// state byte-identically; every lens and panel re-queries. Changing
/// resolution mid-run forks a branch from the current keyframe. The
/// base artifact text is never mutated — stepping always works on the
/// in-memory reconstruction.</summary>
public sealed class TimeMachine
{
    /// <summary>The base artifact text every keyframe records against.</summary>
    public string BaseText { get; }

    /// <summary>The live state at the current position.</summary>
    public SimState Current { get; private set; }

    private readonly List<TimelineBranch> _branches = new();

    /// <summary>Every timeline, root first, in fork order.</summary>
    public IReadOnlyList<TimelineBranch> Branches => _branches;

    /// <summary>Index into <see cref="Branches"/> of the live timeline.</summary>
    public int ActiveBranch { get; private set; }

    /// <summary>The active timeline's keyframes, oldest first;
    /// [0] is the base artifact itself.</summary>
    public IReadOnlyList<Keyframe> Keyframes => Active.Keyframes;

    /// <summary>Index into <see cref="Keyframes"/> of the current view.</summary>
    public int Position { get; private set; }

    private TimelineBranch Active => _branches[ActiveBranch];

    public TimeMachine(string baseText)
    {
        BaseText = baseText;
        Current = LoadText(baseText);
        _branches.Add(new TimelineBranch(-1, -1,
            Current.Config.Sim.YearsPerEpoch,
            new List<Keyframe>
            {
                new(Current.EpochIndex, Current.WorldYear,
                    Current.Config.Sim.YearsPerEpoch, Delta: ""),
            }));
        ActiveBranch = 0;
        Position = 0;
    }

    /// <summary>Steps the live state <paramref name="epochs"/> integration
    /// steps (EpochEngine, YearsPerEpoch world-years each), capturing a
    /// keyframe after every epoch. From a scrubbed-back position this walks
    /// the recorded keyframes instead of re-simulating — one timeline, one
    /// history; deterministic replay makes the two identical.</summary>
    public void Step(int epochs = 1)
    {
        var engine = new EpochEngine();
        // AC2.F2: tap the step's shipment launches so each keyframe keeps
        // the flows its own step produced (in-memory only). Walking
        // recorded frames below recalls, never re-captures.
        var captured = new List<RecentFlow>();
        engine.ShipmentObserver = l => captured.Add(RecentFlowQuery.Capture(l));
        for (int i = 0; i < epochs; i++)
        {
            if (Position < Active.Frames.Count - 1)
            {
                ScrubTo(Position + 1);
                continue;
            }
            captured.Clear();
            engine.Step(Current);
            Active.Frames.Add(new Keyframe(Current.EpochIndex,
                Current.WorldYear, Current.Config.Sim.YearsPerEpoch,
                DeltaSerializer.Diff(BaseText, ArtifactSerializer.ToText(Current)))
            {
                RecentFlows = captured.Count == 0
                    ? System.Array.Empty<RecentFlow>() : captured.ToArray(),
            });
            Position = Active.Frames.Count - 1;
        }
    }

    /// <summary>The current keyframe's recent flows (AC2.F2): what moved
    /// during the step that produced this moment — empty at the base
    /// frame, recalled per keyframe across scrubs and forks.</summary>
    public IReadOnlyList<RecentFlow> CurrentFlows =>
        Active.Frames[Position].RecentFlows;

    /// <summary>Snaps to a keyframe on the active timeline: rebuilds the
    /// live state from the base plus the keyframe's delta, byte-identically
    /// (the branch's own tick resolution wins over the base's ESIM line).
    /// Every lens and panel re-queries off the rebuilt state.</summary>
    public void ScrubTo(int keyframeIndex)
    {
        var frame = Active.Frames[keyframeIndex];
        Current = LoadText(frame.Delta.Length == 0
            ? BaseText
            : DeltaSerializer.Apply(BaseText, frame.Delta));
        Current.Config.Sim.YearsPerEpoch = Active.Resolution;
        Position = keyframeIndex;
    }

    /// <summary>Changes the integration step. Before any step is taken this
    /// just retunes the root; mid-run it forks a branch anchored at the
    /// current keyframe — the recorded future stays with its own timeline.</summary>
    public void SetResolution(int yearsPerEpoch)
    {
        if (yearsPerEpoch == Active.Resolution) return;
        if (Active.Frames.Count == 1)
        {
            Active.Resolution = yearsPerEpoch;
            Current.Config.Sim.YearsPerEpoch = yearsPerEpoch;
            return;
        }
        var shared = Active.Frames.GetRange(0, Position + 1);
        _branches.Add(new TimelineBranch(ActiveBranch, Position,
            yearsPerEpoch, shared));
        ActiveBranch = _branches.Count - 1;
        Current.Config.Sim.YearsPerEpoch = yearsPerEpoch;
    }

    private static SimState LoadText(string text)
    {
        using var reader = new StringReader(text);
        return ArtifactSerializer.Load(reader);
    }
}
