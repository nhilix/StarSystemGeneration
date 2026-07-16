using System.Collections.Generic;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The knob registry is the single index of every calibration dial:
/// name → doc → live get/set on a config. It drives the artifact's KNOB
/// lines, the REPL `knobs` command, and docs/TUNING.md — one authoritative
/// list, so a dial can never silently exist outside it.</summary>
public class KnobRegistryTests
{
    [Fact]
    public void Names_AreUnique_Sorted_AndDocumented()
    {
        string? previous = null;
        var seen = new HashSet<string>();
        foreach (var knob in KnobRegistry.All)
        {
            Assert.True(seen.Add(knob.Name), $"duplicate knob '{knob.Name}'");
            Assert.False(string.IsNullOrWhiteSpace(knob.Doc),
                $"knob '{knob.Name}' lacks documentation");
            Assert.Contains(".", knob.Name);   // Family.Name convention
            if (previous != null)
                Assert.True(string.CompareOrdinal(previous, knob.Name) < 0,
                    $"registry must be name-sorted: '{previous}' >= '{knob.Name}'");
            previous = knob.Name;
        }
        Assert.True(KnobRegistry.All.Count >= 50,
            $"the registry should cover the dial surface ({KnobRegistry.All.Count})");
    }

    [Fact]
    public void EveryKnob_RoundTripsThroughItsAccessors()
    {
        var config = new EpochSimConfig();
        foreach (var knob in KnobRegistry.All)
        {
            double original = knob.Get(config);
            Assert.True(double.IsFinite(original), $"'{knob.Name}' default not finite");
            double probe = original == 0 ? 1.0 : original * 2;
            knob.Set(config, probe);
            Assert.Equal(probe, knob.Get(config), 6);
            knob.Set(config, original);
        }
    }

    [Fact]
    public void Cu2Knobs_NonDefault_SurviveArtifactRoundTrip()
    {
        // slice CU-2 task 8 fix 1: both bank knobs are registered, so a
        // non-default value must survive ToText -> Load rather than silently
        // reverting to the compiled default (the fresh-vs-reloaded divergence).
        var state = EpochTestKit.Seeded().State;
        state.Config.Economy.ConversionSpread = 0.0173;      // non-default
        state.Config.Economy.IssuanceReserveRatio = 0.37;    // non-default

        var text = ArtifactSerializer.ToText(state);
        Assert.Contains("KNOB|Economy.ConversionSpread|0.0173", text);
        Assert.Contains("KNOB|Economy.IssuanceReserveRatio|0.37", text);

        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(0.0173, loaded.Config.Economy.ConversionSpread, 12);
        Assert.Equal(0.37, loaded.Config.Economy.IssuanceReserveRatio, 12);
    }

    [Fact]
    public void UnknownKnob_RefusesToLoad()
    {
        var state = EpochTestKit.Seeded().State;
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("KNOB|Economy.LaborShare|", text);   // the new format
        string tampered = text.Replace("KNOB|Economy.LaborShare|",
                                       "KNOB|Economy.Nonsense|");
        Assert.Throws<System.IO.InvalidDataException>(() =>
            ArtifactSerializer.Load(new System.IO.StringReader(tampered)));
    }
}
