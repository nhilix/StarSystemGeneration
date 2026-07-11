using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 6 — era detection (chronicle-and-poi.md §Chronicle
/// views): epochs cluster by dominant event signature, consecutive runs
/// merge, and each era is named from its participants. A recomputable
/// annotation over the log — never sim state.</summary>
public class EraDetectionTests
{
    private static void Append(SimState state, int epoch, WorldEventType type,
                               params int[] actors)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        state.Log.Append((long)epoch * years, ClockStratum.Generational, type,
            actors, new HexCoordinate(0, 0), 1.0, 0.0,
            EventVisibility.Regional, null);
    }

    private static SimState ScriptedHistory()
    {
        var state = EpochTestKit.Seeded().State;
        // epochs 0–3: the frontier opens
        for (int e = 0; e <= 3; e++)
        {
            Append(state, e, WorldEventType.PortEstablished, 0);
            Append(state, e, WorldEventType.PortEstablished, 1);
            Append(state, e, WorldEventType.LaneOpened, 0);
        }
        // epochs 4–7: polities 0 and 1 burn
        for (int e = 4; e <= 7; e++)
        {
            Append(state, e, WorldEventType.BattleFought, 0, 1);
            Append(state, e, WorldEventType.SiegeBegun, 0, 1);
        }
        // epochs 8–10: the treaties
        for (int e = 8; e <= 10; e++)
        {
            Append(state, e, WorldEventType.TreatySigned, 0, 1);
            Append(state, e, WorldEventType.TreatySigned, 1, 2);
        }
        // epochs 11–15: nothing much
        state.EpochIndex = 16;
        state.WorldYear = 16 * state.Config.Sim.YearsPerEpoch;
        return state;
    }

    [Fact]
    public void Eras_Cluster_Merge_AndCoverTheWholeRun()
    {
        var state = ScriptedHistory();
        var eras = EraDetector.Detect(state);
        Assert.Equal(4, eras.Count);
        Assert.Equal(EraKind.Expansion, eras[0].Kind);
        Assert.Equal(EraKind.War, eras[1].Kind);
        Assert.Equal(EraKind.Treaty, eras[2].Kind);
        Assert.Equal(EraKind.Quiet, eras[3].Kind);
        // contiguous cover of [0, EpochIndex)
        Assert.Equal(0, eras[0].StartEpoch);
        for (int i = 1; i < eras.Count; i++)
            Assert.Equal(eras[i - 1].EndEpoch + 1, eras[i].StartEpoch);
        Assert.Equal(state.EpochIndex - 1, eras[^1].EndEpoch);
    }

    [Fact]
    public void Eras_AreNamed_FromTheirParticipants()
    {
        var state = ScriptedHistory();
        var eras = EraDetector.Detect(state);
        string a = state.Actors[0].Name, b = state.Actors[1].Name;
        Assert.Equal($"The {a}–{b} Wars", eras[1].Name);
        Assert.Contains("Concord", eras[2].Name);
        Assert.Equal("The Long Peace", eras[3].Name);   // ≥ 4 quiet epochs
    }

    [Fact]
    public void RepeatedNames_Disambiguate()
    {
        var state = EpochTestKit.Seeded().State;
        // war, quiet, war between the same pair — the second gets a numeral
        for (int e = 0; e <= 1; e++)
            Append(state, e, WorldEventType.BattleFought, 0, 1);
        for (int e = 6; e <= 7; e++)
            Append(state, e, WorldEventType.BattleFought, 0, 1);
        state.EpochIndex = 8;
        state.WorldYear = 8 * state.Config.Sim.YearsPerEpoch;
        var eras = EraDetector.Detect(state);
        Assert.Equal(eras[0].Name + " II", eras[2].Name);
    }

    [Fact]
    public void ErasAre_Annotation_NeverState()
    {
        // recomputable: two detections over the same log are equal
        var state = ScriptedHistory();
        Assert.Equal((IEnumerable<Era>)EraDetector.Detect(state),
                     EraDetector.Detect(state));
    }
}
