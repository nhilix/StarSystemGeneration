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
            state.Decisions.Clear();
            return hex;
        }
        return default;
    }

    [Fact]
    public void MineralColony_FoundsWithAMine_AndASubsistenceFarm()
    {
        var state = EnteredFixture();
        var hex = FoundAt(state, c =>
        {
            foreach (var a in c.Anchors)
                if (a.Type == AnchorType.MineralRich) return true;
            return false;
        });
        if (hex.Equals(default(HexCoordinate))) return;  // no such cell in reach

        bool mine = false, farm = false;
        foreach (var f in state.Facilities)
            if (f.Hex.Equals(hex))
            {
                if (f.TypeId == (int)InfraTypeId.Mine) mine = true;
                if (f.TypeId == (int)InfraTypeId.AgriComplex) farm = true;
            }
        Assert.True(mine, "the expedition ships the equipment for what it came for");
        Assert.True(farm, "and seeds against the food risk");
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
    // Import parity disciplines connected prices
    // ------------------------------------------------------------------

    [Fact]
    public void ConnectedMarket_PriceIsCappedByImportParity()
    {
        var state = EnteredFixture();
        var far = new Port(1, 0, new HexCoordinate(state.Ports[0].Hex.Q + 10,
            state.Ports[0].Hex.R), tier: 2, foundedYear: 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.Lanes.Add(new Lane(0, 0, 1, 0));
        int species = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(1, 1, species, species, 3.0)
        { Wealth = 500 });

        var mA = state.Markets[0];
        var mB = state.Markets[1];
        mA.Deposit((int)GoodId.Medicine, 5000, 0.5);   // glut at the neighbor
        mB.Price[(int)GoodId.Medicine] = 200.0;        // absurd local memory
        EpochTestKit.PostFreight(state, 0, laneId: 0, hulls: 4);   // hulls carry parity

        var scratch = new MarketStepScratch(state);
        MarketEngine.AssembleDemand(state, scratch);
        MarketEngine.AdjustPrices(state, scratch);

        // nobody pays 200 next to a glut one hop away: the price falls to
        // import parity (source price + transport, grossed up for the
        // exporter's realized margin), far below the ceiling
        Assert.True(mB.Price[(int)GoodId.Medicine] < 50.0,
            $"parity should discipline the price, got {mB.Price[(int)GoodId.Medicine]}");
    }

    [Fact]
    public void SeveredLane_LiftsTheParityCap()
    {
        var state = EnteredFixture();
        var far = new Port(1, 0, new HexCoordinate(state.Ports[0].Hex.Q + 10,
            state.Ports[0].Hex.R), tier: 2, foundedYear: 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.Lanes.Add(new Lane(0, 0, 1, 0));
        int species = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(1, 1, species, species, 3.0)
        { Wealth = 5000 });

        state.Markets[0].Deposit((int)GoodId.Medicine, 5000, 0.5);
        state.Markets[1].Price[(int)GoodId.Medicine] = 200.0;
        EpochTestKit.PostFreight(state, 0, laneId: 0, hulls: 4);
        state.SeveredLanes.Add(0);

        var scratch = new MarketStepScratch(state);
        MarketEngine.AssembleDemand(state, scratch);
        MarketEngine.AdjustPrices(state, scratch);

        Assert.True(state.Markets[1].Price[(int)GoodId.Medicine] > 50.0,
            "a blockaded port has no import alternative — the spike is real");
    }
}
