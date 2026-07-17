using System.IO;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 2: the `banks` artifact layer. Wired the moment
/// the state exists (HANDOFF records that CU-1/L's BodyResources broke
/// save/reload determinism because serialization lagged the state) — reserves
/// are still 0 in every live run until task 4 feeds them, but a state with
/// nonzero Reserve / cumulative counters must still round-trip exactly.</summary>
public class BankArtifactTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            StarGen.Core.Galaxy.SkeletonBuilder.Build(new StarGen.Core.Galaxy.GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static SimState Reload(SimState state) =>
        ArtifactSerializer.Load(new StringReader(ArtifactSerializer.ToText(state)));

    [Fact]
    public void Banks_RoundTrip_EveryField_BitIdenticalDoubles()
    {
        var state = NewState();
        state.Currencies.Add(new Currency(0, "C0", 0));
        state.Currencies.Add(new Currency(1, "C1", 1));
        state.Banks.Add(new Bank(0)
        {
            Reserve = 12345.6789012345,
            CumulativeSpreadIntake = 42.000000001,
            CumulativeReserveFunded = 9999.9999999999,
            ClaimOnState = 777.777777777,
            CumulativeLentToState = 888.888888888,
            CumulativeRetired = 111.111111111,
        });
        state.Banks.Add(new Bank(1)
        {
            Reserve = 0.0,
            CumulativeSpreadIntake = 3.140000000001,
            CumulativeReserveFunded = 0.333333333333333,
            ClaimOnState = 22.22,
            CumulativeLentToState = 33.33,
            CumulativeRetired = 44.44,
        });

        var text1 = ArtifactSerializer.ToText(state);
        var loaded = Reload(state);
        var text2 = ArtifactSerializer.ToText(loaded);

        Assert.Equal(2, loaded.Banks.Count);
        var b0 = loaded.Banks[0];
        Assert.Equal(0, b0.CurrencyId);
        Assert.Equal(12345.6789012345, b0.Reserve);
        Assert.Equal(42.000000001, b0.CumulativeSpreadIntake);
        Assert.Equal(9999.9999999999, b0.CumulativeReserveFunded);
        Assert.Equal(777.777777777, b0.ClaimOnState);
        Assert.Equal(888.888888888, b0.CumulativeLentToState);
        Assert.Equal(111.111111111, b0.CumulativeRetired);
        var b1 = loaded.Banks[1];
        Assert.Equal(1, b1.CurrencyId);
        Assert.Equal(0.0, b1.Reserve);
        Assert.Equal(3.140000000001, b1.CumulativeSpreadIntake);
        Assert.Equal(0.333333333333333, b1.CumulativeReserveFunded);
        Assert.Equal(22.22, b1.ClaimOnState);
        Assert.Equal(33.33, b1.CumulativeLentToState);
        Assert.Equal(44.44, b1.CumulativeRetired);

        // the strongest available equivalence gate — nothing silently dropped
        Assert.Equal(text1, text2);
    }

    [Fact]
    public void FreshVsReloaded_FullGenesisRun_BanksMatchByteIdentical()
    {
        // the realistic scenario: genesis mints currencies (and their 1:1
        // banks) as polities are seeded — reserves stay 0 until task 4, but
        // the layer must still be present and stable across reload.
        var state = EpochTestKit.Seeded(42, 10).State;
        state.Config.Sim.EpochCount = 12;
        new EpochEngine().Run(state);

        var loaded = Reload(state);

        Assert.Equal(state.Banks.Count, loaded.Banks.Count);
        for (int i = 0; i < state.Banks.Count; i++)
        {
            var b = state.Banks[i];
            var l = loaded.Banks[i];
            Assert.Equal(b.CurrencyId, l.CurrencyId);
            Assert.Equal(b.Reserve, l.Reserve);
            Assert.Equal(b.CumulativeSpreadIntake, l.CumulativeSpreadIntake);
            Assert.Equal(b.CumulativeReserveFunded, l.CumulativeReserveFunded);
            Assert.Equal(b.ClaimOnState, l.ClaimOnState);
            Assert.Equal(b.CumulativeLentToState, l.CumulativeLentToState);
            Assert.Equal(b.CumulativeRetired, l.CumulativeRetired);
        }
        Assert.Equal(ArtifactSerializer.ToText(state), ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void Load_RejectsArtifactMissingTheBanksLayer()
    {
        var (_, state) = EpochTestKit.Seeded();
        var text = ArtifactSerializer.ToText(state);

        var lines = new System.Collections.Generic.List<string>(text.Split('\n'));
        lines.RemoveAll(l => l.StartsWith("LAYER|banks") || l.StartsWith("BANK|"));
        var stripped = string.Join("\n", lines);

        Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(stripped)));
    }
}
