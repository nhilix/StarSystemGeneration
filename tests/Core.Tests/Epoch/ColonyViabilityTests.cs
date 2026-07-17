using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D — colonial viability (eyeball-gate fixes): the expedition
/// ships the equipment for what it came for, expansion pauses while the
/// realm starves, and lane-connected prices are disciplined by import parity
/// (no market pays ceiling prices for what a neighbor sells at glut).</summary>
public class ColonyViabilityTests
{
    // ------------------------------------------------------------------
    // Founding facility matches the site
    // ------------------------------------------------------------------

    private static SimState EnteredFixture()
    {
        var state = EpochTestKit.Seeded().State;
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, species, species, 3.0));
        state.PolityOf(actor.Id).ExpansionPoints = 100;
        state.WorldYear = 100;
        // founding is physical (slice E): the fixture needs a colony convoy
        DesignRegistry.RegisterEntryDesigns(state, actor.Id, militancy: 0.5);
        FleetOps.SeedStarterFleet(state, actor.Id, port, militancy: 0.5);
        return state;
    }

    private static HexCoordinate FoundAt(SimState state, System.Func<RegionCell, bool> pick)
    {
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid || !pick(cell)) continue;
            var hex = HexGrid.CellCenter(cell.Coord);
            if (HexGrid.Distance(state.Ports[0].Hex, hex)
                > state.Config.Expansion.ColonizationReachHexes) continue;
            bool taken = false;
            foreach (var p in state.Ports) if (p.Hex.Equals(hex)) taken = true;
            foreach (var a in cell.Anchors)
                if (a.Type == AnchorType.Homeworld) taken = true;
            if (taken) continue;
            state.Decisions.Add(new ActorDecision(0, new ControllerDecision(
                PolityPolicies.Default,
                new Act[] { new FoundColonyAct(0, hex) })));
            new ResolutionPhase().Run(state);
            // founding runs in world-time now (Task 9): the expedition ships
            // the equipment on arrival — fly the crossing to completion
            ProjectOps.AdvanceAll(state);
            state.Decisions.Clear();
            return hex;
        }
        return default;
    }

    // The mineral colony's founding industry is a Mine (ore-dominant raster).
    // NEW invariant (Slice L2 — AgriComplex renewable yield has no floor, so a
    // barren-dry body farms nothing): the colony seeds a subsistence AgriComplex
    // IFF its hex has a farmable (biosphere) body. A fully-barren mineral system
    // founds a Mine and NO farm — it imports food — rather than shipping the
    // equipment for a permanently non-functional (bodiless) dud farm.
    //
    // (This replaces the old "always founds a farm" expectation, which only held
    // because the retired 0.3 renewable floor manufactured a bodiless dud farm.
    // At seed 42 every hex in colonization reach is biosphere-barren, so the
    // farmable half is exercised with an injected mine+garden system at the same
    // ore-dominant hex — its raster keeps FoundingIndustry = Mine.)

    private static HexCoordinate FirstMineralHexInReach(SimState state)
    {
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            bool mineral = false;
            foreach (var a in cell.Anchors)
                if (a.Type == AnchorType.MineralRich) mineral = true;
            if (!mineral) continue;
            var hex = HexGrid.CellCenter(cell.Coord);
            if (HexGrid.Distance(state.Actors[0].Seat, hex)
                > state.Config.Expansion.ColonizationReachHexes) continue;
            return hex;
        }
        return default;
    }

    [Fact]
    public void MineralColony_AtABarrenHex_FoundsAMine_ButNoFarm()
    {
        // real founding path (ResolutionPhase -> CompleteExpedition): the sole
        // mineral hex in reach at this seed is fully biosphere-barren.
        var state = EnteredFixture();
        var hex = FoundAt(state, c =>
        {
            foreach (var a in c.Anchors)
                if (a.Type == AnchorType.MineralRich) return true;
            return false;
        });
        Assert.False(hex.Equals(default(HexCoordinate)));   // a mineral hex exists
        // precondition: no farmable body in this committed system
        var sys = SystemRegistry.Commit(state, hex);
        Assert.NotNull(sys);
        foreach (var star in sys!.Stars)
            foreach (var slot in star.Slots)
                if (slot.Body != null)
                    Assert.Equal(Biosphere.Barren, slot.Body.Biosphere);

        bool mine = false, farm = false;
        foreach (var f in state.Facilities)
            if (f.Hex.Equals(hex))
            {
                if (f.TypeId == (int)InfraTypeId.Mine) mine = true;
                if (f.TypeId == (int)InfraTypeId.AgriComplex) farm = true;
            }
        Assert.True(mine, "the ore colony founds its mine");
        Assert.False(farm, "a fully-barren hex farms nothing — no bodiless dud farm");
    }

    [Fact]
    public void MineralColony_AtAFarmableHex_FoundsAMine_AndASubsistenceFarm()
    {
        var state = EpochTestKit.Seeded().State;
        var hex = FirstMineralHexInReach(state);       // ore-dominant raster => Mine
        Assert.False(hex.Equals(default(HexCoordinate)));
        // keep the ore-dominant raster (FoundingIndustry stays Mine) but give the
        // hex a system that DOES carry a biosphere world for the subsistence farm.
        var sys = new StarSystem("MINE_AND_GARDEN");
        var star = new Star();
        star.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 5 } });
        star.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 4,
                Biosphere = Biosphere.Flourishing, Hydrographics = 60 } });
        sys.Stars.Add(star);
        state.SettledSystems[hex] = sys;

        int before = state.Facilities.Count;
        ProjectOps.FoundColonyFacilities(state, hex, state.Actors[0].Id, 100);
        bool mine = false, farm = false;
        for (int i = before; i < state.Facilities.Count; i++)
        {
            var f = state.Facilities[i];
            if (f.TypeId == (int)InfraTypeId.Mine)
            { mine = true; Assert.False(f.Body.IsNone); }
            if (f.TypeId == (int)InfraTypeId.AgriComplex)
            { farm = true; Assert.False(f.Body.IsNone); }
        }
        Assert.True(mine, "the ore colony founds its mine");
        Assert.True(farm,
            "and seeds a subsistence farm on the biosphere body (food risk)");
    }

    [Fact]
    public void GardenColony_FoundsWithAFarmOnly()
    {
        var state = EnteredFixture();
        var hex = FoundAt(state, c =>
            c.Lean == StellarLean.Balanced && c.MeanDensity > 0.5
            && c.Metallicity < 0.4 && c.Anchors.Count == 0);
        if (hex.Equals(default(HexCoordinate))) return;

        int count = 0;
        bool farm = false;
        foreach (var f in state.Facilities)
            if (f.Hex.Equals(hex))
            {
                count++;
                if (f.TypeId == (int)InfraTypeId.AgriComplex) farm = true;
            }
        Assert.True(farm);
        Assert.Equal(1, count);
    }

    // ------------------------------------------------------------------
    // Expansion pauses while the realm starves
    // ------------------------------------------------------------------

    [Fact]
    public void Controller_HoldsExpansion_WhileTheRealmStarves()
    {
        var config = new EpochSimConfig();
        var candidates = new[] { new ColonyCandidate(new HexCoordinate(3, 3), 1.0) };
        var starving = new PerceptionView(0, 0, new int[0],
            expansionPoints: 1000, colonyCandidates: candidates,
            realmSubsistence: 0.5, colonyHullsAvailable: 1);
        Assert.Empty(new GenesisController(config).Decide(starving).Acts);

        var fed = new PerceptionView(0, 0, new int[0],
            expansionPoints: 1000, colonyCandidates: candidates,
            realmSubsistence: 1.0, colonyHullsAvailable: 1);
        Assert.NotEmpty(new GenesisController(config).Decide(fed).Acts);
    }

    // ------------------------------------------------------------------
    // Imports discipline connected prices (the book replaced parity math:
    // real freight fills the dear end's bids and the glut's unsold asks
    // drag the reference down — convergence is physical now)
    // ------------------------------------------------------------------

    private static void BookStep(SimState state)
    {
        var scratch = new MarketStepScratch(state);
        BookOps.RepriceAsks(state);
        MarketEngine.PostBandBids(state, scratch);
        MarketEngine.MoveFreight(state, scratch);
        MarketEngine.MatchAndClear(state, scratch);
    }

    [Fact]
    public void ConnectedMarket_ImportsDisciplineThePrice()
    {
        var state = EnteredFixture();
        var far = new Port(1, 0, new HexCoordinate(state.Ports[0].Hex.Q + 10,
            state.Ports[0].Hex.R), tier: 2, foundedYear: 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int species = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(1, 1, species, species, 3.0)
        { Wealth = 500 });

        // glut at the neighbor; absurd local price memory at the colony
        EpochTestKit.Stock(state, 0, (int)GoodId.Medicine, 5000, 0.5);
        state.Markets[1].Price[(int)GoodId.Medicine] = 200.0;
        EpochTestKit.PostFreight(state, 0, laneId: 0, hulls: 4);

        for (int i = 0; i < 6; i++) BookStep(state);

        // nobody pays 200 next to a glut one hop away: imports land on the
        // colony's book and the unsold depth drags the reference down
        Assert.True(state.Markets[1].Price[(int)GoodId.Medicine] < 100.0,
            $"imports should discipline the price, got "
            + $"{state.Markets[1].Price[(int)GoodId.Medicine]}");
    }

    [Fact]
    public void SeveredLane_LetsThePriceStayHigh()
    {
        var state = EnteredFixture();
        var far = new Port(1, 0, new HexCoordinate(state.Ports[0].Hex.Q + 10,
            state.Ports[0].Hex.R), tier: 2, foundedYear: 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int species = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(1, 1, species, species, 3.0)
        { Wealth = 5000 });

        EpochTestKit.Stock(state, 0, (int)GoodId.Medicine, 5000, 0.5);
        state.Markets[1].Price[(int)GoodId.Medicine] = 200.0;
        EpochTestKit.PostFreight(state, 0, laneId: 0, hulls: 4);
        // a real blockade severs the lane (slice H replaced the debug cut)
        EpochTestKit.BlockadePort(state, 1, portId: 0);

        for (int i = 0; i < 6; i++) BookStep(state);

        Assert.True(state.Markets[1].Price[(int)GoodId.Medicine] > 100.0,
            "a blockaded port has no import alternative — the spike is real");
    }
}
