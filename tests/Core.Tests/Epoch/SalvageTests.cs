using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 8 — salvage and expeditions (chronicle-and-poi.md):
/// salvage corporations found themselves via the ordinary charter rule and
/// strip their fields — hulls become alloys/components at declining grade,
/// precursor digs yield exotics and teach the host, stripped fields stop
/// teaching, and the hull ledger conserves through all of it.</summary>
public class SalvageTests
{
    private static (SimState State, Port Home, PoiRecord Field) BattlefieldFixture()
    {
        var state = EpochTestKit.Seeded().State;
        state.Skeleton.PrecursorWaves.Clear();
        var a0 = state.Actors[0];
        a0.Entered = true;
        var home = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(home);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int sp = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, sp, sp, 3.0)
        { Wealth = 100 });
        state.WorldYear = 100;
        var fieldHex = new HexCoordinate(home.Hex.Q + 5, home.Hex.R);
        // high-grade hulls: the wrecks out-grade the polity's own ceiling,
        // so the field teaches until salvors strip it
        var design = DesignRegistry.Register(state, a0.Id, ShipRole.Line,
                                             ShipSize.Medium, grade: 0.95);
        var fleet = new FleetRecord(state.Fleets.Count, a0.Id, fieldHex);
        fleet.AddHulls(design.Id, 10, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(0).HullsBuilt += 10;
        FleetOps.Wreck(state, fleet, 10, quiet: true);
        new ChroniclePhase().Run(state);
        var field = Assert.Single(state.Pois);
        return (state, home, field);
    }

    private static Corporation Salvor(SimState state, int hostId, int homePort,
                                      int poiId)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation,
            "Test Salvors", state.Ports[homePort].Hex, 0,
            new CorporateController())
        { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId,
            "Test Salvors", hostId, CorporateNiche.Salvage, homePort, 0)
        { Credits = 100, TargetId = poiId };
        state.Corporations.Add(corp);
        return corp;
    }

    [Fact]
    public void Salvors_StripTheField_AndTheHullLedgerConserves()
    {
        var (state, home, field) = BattlefieldFixture();
        Salvor(state, 0, home.Id, field.Id);
        var scratch = new MarketStepScratch(state);
        int worked = CorporationOps.SalvageLands(state, scratch);
        Assert.Equal(1, worked);
        Assert.True(field.HullsSalvaged > 0, "the field is being stripped");
        Assert.True(state.Markets[0].Inventory[(int)GoodId.Alloys] > 0,
            "recovered alloys land at the home market");
        Assert.Contains(scratch.Supplies,
            s => s.OwnerActorId == state.Corporations[0].ActorId);
        // wreckage records stay immutable: the ledger invariant holds
        int wreckTotal = 0;
        foreach (var wr in state.Wreckage) wreckTotal += wr.Hulls;
        Assert.Equal(state.PolityOf(0).HullsWrecked, wreckTotal);
    }

    [Fact]
    public void SalvagedOutFields_Fade_UnlessMonumental()
    {
        var (state, _, field) = BattlefieldFixture();
        field.HullsSalvaged = (int)field.Magnitude;   // stripped bare
        new ChroniclePhase().Run(state);
        Assert.True(field.Depleted, "a 10-hull field fades when stripped");
    }

    [Fact]
    public void StrippedFields_TeachNothing()
    {
        var (state, _, field) = BattlefieldFixture();
        // out-graded wreckage in reach teaches while it lies there
        double before = state.PolityOf(0).TechProgress[(int)TechDomain.Military];
        TechOps.Diffuse(state);
        double taught = state.PolityOf(0).TechProgress[(int)TechDomain.Military];
        Assert.True(taught > before, "an unstripped field teaches");

        field.HullsSalvaged = (int)field.Magnitude;   // salvors got it all
        double after = state.PolityOf(0).TechProgress[(int)TechDomain.Military];
        TechOps.Diffuse(state);
        Assert.Equal(after,
            state.PolityOf(0).TechProgress[(int)TechDomain.Military], 9);
    }

    [Fact]
    public void PrecursorDigs_YieldExotics_Teach_AndDepleteTheSite()
    {
        var (state, home, _) = BattlefieldFixture();
        var site = new PoiRecord(state.Pois.Count, PoiType.PrecursorSite,
            new HexCoordinate(home.Hex.Q - 4, home.Hex.R), magnitude: 2.0,
            foundedYear: 100, subjectId: 0, detail: 1);
        state.Pois.Add(site);
        var corp = Salvor(state, 0, home.Id, site.Id);
        double astro = state.PolityOf(0).TechProgress[(int)TechDomain.Astrogation];
        int epochs = 0;
        while (!site.Depleted && epochs++ < 10)
            CorporationOps.SalvageLands(state, new MarketStepScratch(state));
        Assert.True(site.Depleted, "the site digs out");
        Assert.True(site.Magnitude < 2.0);
        Assert.True(state.Markets[0].Inventory[(int)GoodId.Exotics] > 0,
            "digs yield exotics");
        Assert.True(state.PolityOf(0)
                .TechProgress[(int)TechDomain.Astrogation] > astro,
            "digging the deep past teaches the host");
        Assert.Same(corp, state.Corporations[0]);
    }

    [Fact]
    public void ASalvageCharter_IsARuinExpedition_MintingAnExplorer()
    {
        var (state, home, field) = BattlefieldFixture();
        var pr = state.PolityOf(0);
        InteriorOps.SeatAtEntry(state, pr);
        state.Actors[0].Policies = PolityPolicies.Default
            with { CharterOpenness = 1.0 };
        var merchants = FactionOps.FoundFaction(state, pr,
                                                FactionBasis.Corporate);
        merchants.NicheType = (int)CorporateNiche.Salvage;
        merchants.ContextId = field.Id;
        merchants.NichePersistence = 99;
        merchants.Wealth = 10_000;

        int chartered = CorporationOps.CharterCheck(state);
        Assert.Equal(1, chartered);
        var corp = Assert.Single(state.Corporations);
        Assert.Equal(CorporateNiche.Salvage, corp.Niche);
        Assert.Equal(field.Id, corp.TargetId);
        Assert.Equal(home.Id, corp.HomePortId);
        Assert.Contains(state.Characters,
            c => c.Notable == NotableType.Explorer);
    }
}
