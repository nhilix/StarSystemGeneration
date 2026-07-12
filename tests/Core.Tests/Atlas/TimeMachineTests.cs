using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>Slice K4 — the TimeMachine (unity-atlas-design.md §Time):
/// epoch keyframes captured as delta saves against the loaded base
/// (DeltaSerializer — genesis strata never re-record), byte-identical
/// snap-to-keyframe scrub, and the resolution fork (one timeline belongs
/// to one (config, tick-path) run).</summary>
public class TimeMachineTests
{
    /// <summary>A small settled world, then its artifact text — the
    /// DeltaTests prologue at atlas scale.</summary>
    private static string BaseText(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        state.Config.Sim.EpochCount = 10;
        new EpochEngine().Run(state);
        return ArtifactSerializer.ToText(state);
    }

    [Fact]
    public void Load_StandsAtTheBaseKeyframe()
    {
        string baseText = BaseText();
        var machine = new TimeMachine(baseText);

        // the loaded world is the artifact's
        Assert.Equal(baseText, ArtifactSerializer.ToText(machine.Current));
        // one keyframe: the base itself, position 0, root branch
        Assert.Single(machine.Keyframes);
        Assert.Equal(0, machine.Position);
        Assert.Equal(machine.Current.EpochIndex, machine.Keyframes[0].EpochIndex);
        Assert.Equal(machine.Current.WorldYear, machine.Keyframes[0].WorldYear);
    }

    [Fact]
    public void Step_CapturesAKeyframePerEpoch()
    {
        string baseText = BaseText();
        var machine = new TimeMachine(baseText);
        int baseEpoch = machine.Current.EpochIndex;
        long baseYear = machine.Current.WorldYear;

        machine.Step(3);

        Assert.Equal(4, machine.Keyframes.Count);
        Assert.Equal(3, machine.Position);
        for (int i = 1; i <= 3; i++)
        {
            Assert.Equal(baseEpoch + i, machine.Keyframes[i].EpochIndex);
            Assert.Equal(baseYear + 25L * i, machine.Keyframes[i].WorldYear);
        }
        // each keyframe is a delta save that reconstructs its moment
        // byte-identically over the base
        Assert.Equal(ArtifactSerializer.ToText(machine.Current),
            DeltaSerializer.Apply(baseText, machine.Keyframes[3].Delta));
    }

    [Fact]
    public void Scrub_SnapsToAKeyframe_ByteIdentically()
    {
        string baseText = BaseText();
        var machine = new TimeMachine(baseText);
        machine.Step(2);
        string midText = ArtifactSerializer.ToText(machine.Current);
        machine.Step(2);
        string tipText = ArtifactSerializer.ToText(machine.Current);

        machine.ScrubTo(2);
        Assert.Equal(2, machine.Position);
        Assert.Equal(midText, ArtifactSerializer.ToText(machine.Current));

        machine.ScrubTo(0);              // all the way back to the base
        Assert.Equal(baseText, ArtifactSerializer.ToText(machine.Current));

        machine.ScrubTo(4);              // and forward to the tip again
        Assert.Equal(tipText, ArtifactSerializer.ToText(machine.Current));
    }

    [Fact]
    public void Step_FromAScrubbedPosition_ReplaysTheRecordedTimeline()
    {
        // one timeline, one history: stepping after a scrub-back walks the
        // recorded keyframes (deterministic replay), never re-captures or
        // truncates — and stepping past the tip continues byte-identically
        // with a machine that stepped straight through
        string baseText = BaseText();
        var machine = new TimeMachine(baseText);
        machine.Step(4);
        machine.ScrubTo(1);

        machine.Step(2);                 // forward over recorded frames
        Assert.Equal(5, machine.Keyframes.Count);
        Assert.Equal(3, machine.Position);
        Assert.Equal(DeltaSerializer.Apply(baseText, machine.Keyframes[3].Delta),
            ArtifactSerializer.ToText(machine.Current));

        machine.Step(2);                 // one recorded + one genuinely new
        Assert.Equal(6, machine.Keyframes.Count);
        Assert.Equal(5, machine.Position);

        var straight = new TimeMachine(baseText);
        straight.Step(5);
        Assert.Equal(ArtifactSerializer.ToText(straight.Current),
            ArtifactSerializer.ToText(machine.Current));
    }

    [Fact]
    public void SetResolution_BeforeAnyStep_RetunesInPlace()
    {
        // no run has started — there is no tick-path to belong to yet
        var machine = new TimeMachine(BaseText());
        long baseYear = machine.Current.WorldYear;

        machine.SetResolution(5);

        Assert.Single(machine.Branches);
        machine.Step(1);
        Assert.Equal(5, machine.Keyframes[1].YearsPerEpoch);
        Assert.Equal(baseYear + 5, machine.Keyframes[1].WorldYear);
    }

    [Fact]
    public void ResolutionChange_MidRun_ForksABranch()
    {
        // one timeline belongs to one (config, tick-path) run: changing
        // resolution mid-run forks from the current keyframe; the original
        // timeline keeps its recorded future untouched
        var machine = new TimeMachine(BaseText());
        machine.Step(4);
        machine.ScrubTo(2);

        machine.SetResolution(1);

        Assert.Equal(2, machine.Branches.Count);
        Assert.Equal(1, machine.ActiveBranch);
        Assert.Equal(0, machine.Branches[1].ForkedFromBranch);
        Assert.Equal(2, machine.Branches[1].ForkedAtKeyframe);
        // the fork carries the shared past up to its anchor, nothing after
        Assert.Equal(3, machine.Keyframes.Count);
        Assert.Equal(2, machine.Position);

        machine.Step(2);                 // fine ticks on the fork
        Assert.Equal(1, machine.Keyframes[3].YearsPerEpoch);
        Assert.Equal(machine.Keyframes[2].WorldYear + 1,
            machine.Keyframes[3].WorldYear);

        // the root timeline is untouched, coarse tail intact
        Assert.Equal(5, machine.Branches[0].Keyframes.Count);
        Assert.Equal(25, machine.Branches[0].Keyframes[4].YearsPerEpoch);
    }

    [Fact]
    public void TheBranchResolution_SurvivesAScrub()
    {
        // scrubbing rebuilds the state from the base artifact, whose ESIM
        // line carries the base resolution — the branch's own tick must win
        var machine = new TimeMachine(BaseText());
        machine.Step(2);
        machine.SetResolution(1);
        machine.Step(2);
        long tipYear = machine.Current.WorldYear;

        machine.ScrubTo(2);
        machine.Step(3);                 // replay 2, then one new fine tick

        Assert.Equal(1, machine.Keyframes[5].YearsPerEpoch);
        Assert.Equal(tipYear + 1, machine.Current.WorldYear);
    }

    [Fact]
    public void AGenesisBase_StepsAndScrubsByteIdentically()
    {
        // the run-seed flow: the base is the seeded world at y0, epoch 0,
        // UNSTEPPED — playing from genesis captures the whole evolution
        var state = EpochTestKit.Seeded(42).State;
        string baseText = ArtifactSerializer.ToText(state);
        var machine = new TimeMachine(baseText);

        Assert.Equal(0, machine.Current.EpochIndex);
        Assert.Equal(0, machine.Current.WorldYear);

        machine.Step(2);
        string tipText = ArtifactSerializer.ToText(machine.Current);
        machine.ScrubTo(0);
        Assert.Equal(baseText, ArtifactSerializer.ToText(machine.Current));
        machine.ScrubTo(2);
        Assert.Equal(tipText, ArtifactSerializer.ToText(machine.Current));
    }
}
