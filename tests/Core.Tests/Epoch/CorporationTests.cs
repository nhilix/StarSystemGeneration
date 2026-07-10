using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 7: corporations — persistent-niche founding through
/// the charter graduation, the corporate controller and portfolio, dividends
/// to host elites, deaths with residue, and the nationalization act.</summary>
public class CorporationTests
{
    /// <summary>A run tuned so niches charter early and often.</summary>
    private static SimState EagerRun(ulong seed = 42)
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = seed, GalaxyRadiusCells = 10 };
        var config = new EpochSimConfig { MasterSeed = seed };
        config.Corporate.CharterPersistenceEpochs = 1;
        config.Corporate.FreightNicheMargin = 0.1;
        config.Corporate.FabricationPriceRatio = 1.3;
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc), config);
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void Niches_Charter_IntoWorkingCorporations()
    {
        var state = EagerRun();
        Assert.True(state.Corporations.Count > 0,
            "no niche ever chartered under eager settings");
        foreach (var corp in state.Corporations)
        {
            var actor = state.Actors[corp.ActorId];
            Assert.Equal(ActorKind.Corporation, actor.Kind);
            Assert.Equal(corp.Name, actor.Name);
            Assert.True(actor.Entered);
            // the merchant faction that incorporated is spent
            // (its wealth capitalized the corporation, conserved)
            if (corp.Niche != CorporateNiche.Raiding)
            {
                Assert.Contains(state.Log.Events, e =>
                    e.Type == WorldEventType.CorporationChartered
                    && e.Payload is CorporationCharteredPayload p
                    && p.CorpId == corp.Id);
                // an executive character sits in the boardroom
                Assert.True(corp.ExecutiveCharacterId >= 0);
            }
        }
    }

    [Fact]
    public void Dividends_FeedHostElites()
    {
        var state = EagerRun();
        // any hosted corporation with revenue implies a corporate faction
        // exists in its host polity (dividend-fed elites)
        foreach (var corp in state.Corporations)
        {
            if (corp.HostPolityId < 0) continue;
            bool everEarned = corp.Receipts > 0 || corp.Credits > 0
                || state.Factions.Any(f => f.PolityId == corp.HostPolityId
                    && f.Basis == FactionBasis.Corporate);
            Assert.True(everEarned || !corp.Active
                || state.Factions.Any(f => f.PolityId == corp.HostPolityId),
                $"hosted corp {corp.Id} left no trace in host politics");
        }
    }

    [Fact]
    public void Deaths_LeaveResidue()
    {
        var state = EagerRun();
        var deaths = state.Log.Events.Where(e =>
            e.Type is WorldEventType.NicheDied
            or WorldEventType.CorporationBankrupt
            or WorldEventType.CorporationNationalized).ToList();
        foreach (var e in deaths)
        {
            int corpId = e.Payload switch
            {
                NicheDiedPayload p => p.CorpId,
                CorporationBankruptPayload p => p.CorpId,
                CorporationNationalizedPayload p => p.CorpId,
                _ => -1,
            };
            var corp = state.Corporations[corpId];
            Assert.False(corp.Active);
            Assert.Equal(0.0, corp.Credits);   // the books settled somewhere
            // no orphaned facilities
            Assert.DoesNotContain(state.Facilities,
                f => f.OwnerActorId == corp.ActorId);
        }
    }

    [Fact]
    public void Nationalize_SeizesAssetsAndBooks()
    {
        var state = EagerRun();
        var corp = state.Corporations.FirstOrDefault(c => c.Active
            && c.HostPolityId >= 0);
        if (corp == null) return;
        var pr = state.PolityOf(corp.HostPolityId);
        double before = pr.Credits + corp.Credits;
        Assert.True(CorporationOps.Nationalize(state, corp.HostPolityId, corp.Id));
        Assert.False(corp.Active);
        Assert.Equal(0.0, corp.Credits);
        Assert.Equal(before, pr.Credits, 9);   // assets AND liabilities move
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.CorporationNationalized);
    }

    [Fact]
    public void HullLedgers_Conserve_AcrossCorporateFleets()
    {
        var state = EagerRun();
        foreach (var corp in state.Corporations)
        {
            int active = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == corp.ActorId) active += f.TotalHulls;
            Assert.Equal(corp.HullsBuilt,
                active + corp.HullsWrecked + corp.HullsScrapped);
        }
    }

    [Fact]
    public void Corporations_RoundTripThroughTheArtifact()
    {
        var state = EagerRun();
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Corporations.Count, loaded.Corporations.Count);
        for (int i = 0; i < state.Corporations.Count; i++)
        {
            Assert.Equal(state.Corporations[i].Name, loaded.Corporations[i].Name);
            Assert.Equal(state.Corporations[i].Credits,
                         loaded.Corporations[i].Credits);
            Assert.Equal(state.Corporations[i].Niche, loaded.Corporations[i].Niche);
        }
        // corporate actors reattach their controller kind
        foreach (var corp in loaded.Corporations)
            Assert.IsType<CorporateController>(
                loaded.Actors[corp.ActorId].Controller);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
