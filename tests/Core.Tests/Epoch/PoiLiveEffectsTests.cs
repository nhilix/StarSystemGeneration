using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice J: the two POI live-effect wires deferred from slice I
/// (chronicle-and-poi.md §The POI compiler live-effects table) — ruins
/// project lawlessness (piracy havens), memorials anchor stances.</summary>
public class PoiLiveEffectsTests
{
    /// <summary>A short real history whose corporate scan can only ever
    /// read the raiding niche — every other niche priced out of reach.</summary>
    private static (SimState State, Lane Lane, int Owner) RaidOnlyRun()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 6; i++) engine.Step(state);
        var corp = state.Config.Corporate;
        corp.FreightNicheMargin = 1e9;
        corp.CartelValueFloor = 1e9;
        corp.DepositNichePotential = 1e9;
        corp.FabricationPriceRatio = 1e9;
        // no salvage preemption: history's own POIs read as spent
        foreach (var poi in state.Pois) poi.Depleted = true;
        foreach (var lane in state.Lanes)
        {
            int owner = state.Ports[lane.PortAId].OwnerActorId;
            if (owner < 0 || !state.Actors[owner].Entered) continue;
            if (state.PolityOf(owner).Interior == null) continue;
            return (state, lane, owner);
        }
        throw new System.InvalidOperationException("no owned lane in history");
    }

    private static bool BandOn(SimState state, int laneId)
    {
        foreach (var c in state.Corporations)
            if (c.Active && c.Niche == CorporateNiche.Raiding
                && c.TargetId == laneId)
                return true;
        return false;
    }

    [Fact]
    public void RuinsNearALane_MakeItAPirateHaven()
    {
        var (state, lane, owner) = RaidOnlyRun();
        EpochTestKit.PostFreight(state, owner, lane.Id, hulls: 8);
        double capacity = FleetOps.PostedCapacity(state, lane);
        Assert.True(capacity > 0, "posted freight should read as cargo");
        // rich enough only under lawlessness: the plain raid floor sits
        // above the lane's cargo, the ruins-discounted one below it
        state.Config.Corporate.RaidCapacityFloor = capacity * 1.2;

        // control: no standing ruin — no band, whatever the navy situation
        CorporationOps.WatchNiches(state);
        Assert.False(BandOn(state, lane.Id),
            "band founded with no ruins in reach");

        // a ruin beyond lawlessness reach changes nothing
        var hexFar = state.Ports[lane.PortAId].Hex;
        hexFar = new HexCoordinate(
            hexFar.Q + state.Config.Poi.LawlessnessReachHexes * 3,
            hexFar.R);
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Ruins,
            hexFar, magnitude: 2.0, state.WorldYear));
        CorporationOps.WatchNiches(state);
        Assert.False(BandOn(state, lane.Id),
            "a distant ruin should not project lawlessness here");

        // a standing ruin at the lane's mouth is a haven: the discounted
        // floor is met and no navy roots a band out of the walls
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Ruins,
            state.Ports[lane.PortAId].Hex, magnitude: 2.0, state.WorldYear));
        CorporationOps.WatchNiches(state);
        Assert.True(BandOn(state, lane.Id),
            "ruins at the lane mouth should found a pirate band");
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.PirateBandFormed);
    }

    [Fact]
    public void DepletedRuins_ProjectNoLawlessness()
    {
        var (state, lane, owner) = RaidOnlyRun();
        EpochTestKit.PostFreight(state, owner, lane.Id, hulls: 8);
        double capacity = FleetOps.PostedCapacity(state, lane);
        state.Config.Corporate.RaidCapacityFloor = capacity * 1.2;
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Ruins,
            state.Ports[lane.PortAId].Hex, magnitude: 2.0, state.WorldYear)
        { Depleted = true });
        CorporationOps.WatchNiches(state);
        Assert.False(BandOn(state, lane.Id),
            "a faded ruin is history, not a haven");
    }

    [Fact]
    public void Lawlessness_StillWantsCargo()
    {
        var (state, lane, owner) = RaidOnlyRun();
        EpochTestKit.PostFreight(state, owner, lane.Id, hulls: 8);
        double capacity = FleetOps.PostedCapacity(state, lane);
        // even the discounted floor sits above this lane's cargo
        state.Config.Corporate.RaidCapacityFloor =
            capacity / state.Config.Poi.LawlessRaidFactor * 1.5;
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Ruins,
            state.Ports[lane.PortAId].Hex, magnitude: 2.0, state.WorldYear));
        CorporationOps.WatchNiches(state);
        Assert.False(BandOn(state, lane.Id),
            "lawlessness discounts the raid floor, it does not erase it");
    }
}
