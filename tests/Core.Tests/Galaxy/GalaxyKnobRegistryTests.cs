using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>The galaxy-side twin of KnobRegistry: the single index of every
/// genesis calibration dial (Cosmic, later Evolution families) on
/// GalaxyConfig. Drives the artifact's GKNOB lines, the REPL `knobs`
/// command, and docs/TUNING.md.</summary>
public class GalaxyKnobRegistryTests
{
    [Fact]
    public void Names_AreUnique_Sorted_AndDocumented()
    {
        string? previous = null;
        var seen = new HashSet<string>();
        foreach (var knob in GalaxyKnobRegistry.All)
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
        Assert.True(GalaxyKnobRegistry.All.Count >= 6,
            $"the registry should cover the genesis dial surface ({GalaxyKnobRegistry.All.Count})");
    }

    [Fact]
    public void EveryKnob_RoundTripsThroughItsAccessors()
    {
        var config = new GalaxyConfig();
        foreach (var knob in GalaxyKnobRegistry.All)
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
    public void GalaxyKnobs_AreArtifactStamped_AndSurviveALoad()
    {
        var gc = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        gc.Cosmic.StarFormationEfficiency = 1.7;
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                      new EpochSimConfig { MasterSeed = 42 });

        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("GKNOB|Cosmic.StarFormationEfficiency|1.7", text);

        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(1.7, loaded.Skeleton.Config.Cosmic.StarFormationEfficiency);
    }

    [Fact]
    public void UnknownGalaxyKnob_RefusesToLoad()
    {
        var state = Epoch.EpochTestKit.Seeded().State;
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("GKNOB|Cosmic.MergerCount|", text);
        string tampered = text.Replace("GKNOB|Cosmic.MergerCount|",
                                       "GKNOB|Cosmic.Nonsense|");
        Assert.Throws<System.IO.InvalidDataException>(() =>
            ArtifactSerializer.Load(new System.IO.StringReader(tampered)));
    }
}
