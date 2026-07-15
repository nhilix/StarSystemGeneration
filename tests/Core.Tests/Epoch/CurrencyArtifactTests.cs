using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 10: the serializer. Before this task the artifact
/// carried none of the currency system's state — no <see cref="Currency"/>
/// registry, no <see cref="PolityRecord.CurrencyId"/>, no
/// <see cref="Corporation.Holdings"/> — so a save/load round trip silently
/// dropped it, and a loaded-then-continued run diverged from a never-saved
/// one (the exact failure the fixed EconomyArtifactTests/FineTickTests/
/// TimeMachineTests LoadThenContinue gates were red for).</summary>
public class CurrencyArtifactTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static SimState Reload(SimState state) =>
        ArtifactSerializer.Load(new StringReader(ArtifactSerializer.ToText(state)));

    [Fact]
    public void Currency_RoundTrips_EveryField_BitIdenticalDoubles()
    {
        var state = NewState();
        var cur = new Currency(0, "TestCurrency", foundingPolityId: 3)
        {
            Supply = 123456.789012345,
            CumulativeFiatIssued = 42.000000001,
            CumulativeSteadyIssuance = 9999.9999999999,
            CumulativeConvertedIn = 17.5,
            CumulativeConvertedOut = 3.140000000001,
            NumeraireRate = 0.333333333333333,
            Retired = true,
        };
        state.Currencies.Add(cur);
        // a second, non-retired currency at a different id — the registry is
        // never a singleton in a live run
        var cur2 = new Currency(1, "Other", foundingPolityId: 7)
        { NumeraireRate = 2.5 };
        state.Currencies.Add(cur2);

        var loaded = Reload(state);

        Assert.Equal(2, loaded.Currencies.Count);
        var l = loaded.Currencies[0];
        Assert.Equal(cur.Id, l.Id);
        Assert.Equal(cur.Name, l.Name);
        Assert.Equal(cur.FoundingPolityId, l.FoundingPolityId);
        Assert.Equal(cur.Supply, l.Supply);
        Assert.Equal(cur.CumulativeFiatIssued, l.CumulativeFiatIssued);
        Assert.Equal(cur.CumulativeSteadyIssuance, l.CumulativeSteadyIssuance);
        Assert.Equal(cur.CumulativeConvertedIn, l.CumulativeConvertedIn);
        Assert.Equal(cur.CumulativeConvertedOut, l.CumulativeConvertedOut);
        Assert.Equal(cur.NumeraireRate, l.NumeraireRate);
        Assert.True(l.Retired);
        Assert.False(loaded.Currencies[1].Retired);
        Assert.Equal(cur2.NumeraireRate, loaded.Currencies[1].NumeraireRate);
    }

    [Fact]
    public void PolityRecord_CurrencyId_RoundTrips_IncludingPreGenesisSentinel()
    {
        var state = NewState();
        state.Polities.Add(new PolityRecord(0, 0) { CurrencyId = 5, Credits = 10.0 });
        state.Polities.Add(new PolityRecord(1, 0));   // CurrencyId defaults to -1

        var loaded = Reload(state);

        Assert.Equal(5, loaded.Polities[0].CurrencyId);
        Assert.Equal(-1, loaded.Polities[1].CurrencyId);
    }

    [Fact]
    public void CorporationHoldings_RoundTrips_ExactDictionaryContents()
    {
        var state = NewState();
        state.Currencies.Add(new Currency(0, "C0", 0) { NumeraireRate = 1.0 });
        state.Currencies.Add(new Currency(1, "C1", 1) { NumeraireRate = 2.0 });
        state.Currencies.Add(new Currency(2, "C2", 2) { NumeraireRate = 0.5 });
        var corp = new Corporation(0, actorId: 100, name: "Corp0", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);
        corp.Deposit(state, 10.123456789, 0);
        corp.Deposit(state, 5.5, 1);
        corp.Deposit(state, 8.0, 2);
        state.Corporations.Add(corp);

        var loaded = Reload(state);

        var loadedCorp = loaded.Corporations[0];
        Assert.Equal(3, loadedCorp.Holdings.Count);
        Assert.Equal(10.123456789, loadedCorp.Holdings[0]);
        Assert.Equal(5.5, loadedCorp.Holdings[1]);
        Assert.Equal(8.0, loadedCorp.Holdings[2]);
        // Credits is a pure function of Holdings — it must recompute to the
        // same numeraire sum after reload, not merely echo a stale scalar
        Assert.Equal(corp.Credits, loadedCorp.Credits, 9);
    }

    [Fact]
    public void CorporationWithEmptyWallet_RoundTrips_ToEmptyHoldings()
    {
        var state = NewState();
        var corp = new Corporation(0, actorId: 100, name: "Corp0", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);

        var loaded = Reload(state);

        Assert.Empty(loaded.Corporations[0].Holdings);
        Assert.Equal(0.0, loaded.Corporations[0].Credits);
    }

    [Fact]
    public void FullGenesisRun_CurrencyStateSurvivesRoundTrip()
    {
        // the realistic scenario: a live history mints currencies, assigns
        // them to polities, and corporations accumulate multi-bucket wallets
        var state = EpochTestKit.Seeded(42, 10).State;
        state.Config.Sim.EpochCount = 12;
        new EpochEngine().Run(state);

        var loaded = Reload(state);

        Assert.Equal(state.Currencies.Count, loaded.Currencies.Count);
        for (int i = 0; i < state.Currencies.Count; i++)
        {
            var b = state.Currencies[i];
            var l = loaded.Currencies[i];
            Assert.Equal(b.Id, l.Id);
            Assert.Equal(b.Name, l.Name);
            Assert.Equal(b.FoundingPolityId, l.FoundingPolityId);
            Assert.Equal(b.Supply, l.Supply);
            Assert.Equal(b.CumulativeFiatIssued, l.CumulativeFiatIssued);
            Assert.Equal(b.CumulativeSteadyIssuance, l.CumulativeSteadyIssuance);
            Assert.Equal(b.CumulativeConvertedIn, l.CumulativeConvertedIn);
            Assert.Equal(b.CumulativeConvertedOut, l.CumulativeConvertedOut);
            Assert.Equal(b.NumeraireRate, l.NumeraireRate);
            Assert.Equal(b.Retired, l.Retired);
        }
        for (int i = 0; i < state.Polities.Count; i++)
            Assert.Equal(state.Polities[i].CurrencyId, loaded.Polities[i].CurrencyId);
        for (int i = 0; i < state.Corporations.Count; i++)
        {
            var b = state.Corporations[i];
            var l = loaded.Corporations[i];
            Assert.Equal(b.Holdings.Count, l.Holdings.Count);
            foreach (var kv in b.Holdings)
                Assert.Equal(kv.Value, l.Holdings[kv.Key]);
            Assert.Equal(b.Credits, l.Credits, 9);
        }
        // the whole-artifact text must be byte-identical, the strongest
        // available equivalence gate for "nothing silently dropped"
        Assert.Equal(ArtifactSerializer.ToText(state), ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void CurrencyBearingLayers_RefuseVersionMismatches()
    {
        var state = EpochTestKit.Seeded(42, 6).State;
        state.Config.Sim.EpochCount = 3;
        new EpochEngine().Run(state);
        string text = ArtifactSerializer.ToText(state);

        // no migration path exists (greenfield/no-compatibility-shims rule) —
        // a version bump on any of the three layers this task touched must
        // fail loudly, never silently misparse or upgrade
        foreach (var layer in new[] { "actors|9", "markets|5", "corporations|4" })
        {
            var name = layer.Split('|')[0];
            string tampered = text.Replace($"LAYER|{layer}", $"LAYER|{name}|999");
            Assert.Throws<InvalidDataException>(() =>
                ArtifactSerializer.Load(new StringReader(tampered)));
        }
    }
}
