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
        // gate economics can defer history's first lane past this short
        // run — manufacture the raid target the niche scan expects (the
        // scan keys on the LOWER-id port's owner)
        foreach (var port in state.Ports)
        {
            int owner = port.OwnerActorId;
            if (owner < 0 || !state.Actors[owner].Entered) continue;
            if (state.PolityOf(owner).Interior == null) continue;
            Port? far = null;
            foreach (var other in state.Ports)
                if (other.Id > port.Id) { far = other; break; }
            if (far == null)
            {
                far = new Port(state.Ports.Count, owner,
                    new HexCoordinate(port.Hex.Q + 6, port.Hex.R), 1,
                    (int)state.WorldYear);
                state.Ports.Add(far);
                state.Markets.Add(new Market(far.Id, state.Config.Economy));
            }
            var made = EpochTestKit.AddLane(state, port.Id, far.Id);
            return (state, made, owner);
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

    /// <summary>Two entered polities from a seeded history — observer and
    /// subject for the stance-anchor tests.</summary>
    private static (SimState State, int Observer, int Subject) TwoPolities()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        int observer = -1, subject = -1;
        // staggered entry: step until two polities are on the stage
        for (int epoch = 0; epoch < 30 && subject < 0; epoch++)
        {
            engine.Step(state);
            observer = subject = -1;
            foreach (var a in state.Actors)
            {
                if (a.Kind != ActorKind.Polity || !a.Entered || a.Retired)
                    continue;
                if (observer < 0) observer = a.Id;
                else { subject = a.Id; break; }
            }
        }
        Assert.True(subject >= 0, "history needs two entered polities");
        return (state, observer, subject);
    }

    [Fact]
    public void Memorials_HoldStances_AgainstThePerpetrator()
    {
        var (state, observer, subject) = TwoPolities();
        double anchor = -state.Config.Poi.MemorialStanceAnchor;
        state.Actors[observer].Beliefs.Stances[subject] = -0.6;
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Memorial,
            state.Actors[subject].Seat, magnitude: 3.0, state.WorldYear,
            subjectId: subject, detail: 1));
        for (int i = 0; i < 400; i++) ReputationOps.DecayStances(state);
        Assert.Equal(anchor,
            ReputationOps.StanceOf(state, observer, subject), 9);
    }

    [Fact]
    public void FadedMemorials_LetMemoryFade()
    {
        var (state, observer, subject) = TwoPolities();
        state.Actors[observer].Beliefs.Stances[subject] = -0.6;
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Memorial,
            state.Actors[subject].Seat, magnitude: 3.0, state.WorldYear,
            subjectId: subject, detail: 1)
        { Depleted = true });
        for (int i = 0; i < 400; i++) ReputationOps.DecayStances(state);
        Assert.True(ReputationOps.StanceOf(state, observer, subject) > -0.01,
            "with the memorial gone, grief should fade to indifference");
    }

    [Fact]
    public void MildDisapproval_FadesEvenUnderAMemorial()
    {
        var (state, observer, subject) = TwoPolities();
        double anchor = -state.Config.Poi.MemorialStanceAnchor;
        // a stance that never reached the anchor is not held by it
        state.Actors[observer].Beliefs.Stances[subject] = anchor * 0.5;
        state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Memorial,
            state.Actors[subject].Seat, magnitude: 3.0, state.WorldYear,
            subjectId: subject, detail: 1));
        for (int i = 0; i < 400; i++) ReputationOps.DecayStances(state);
        Assert.True(ReputationOps.StanceOf(state, observer, subject) > -0.01,
            "shallow disapproval should still fade to indifference");
    }

    [Fact]
    public void SuppressionMemorials_NameThePerpetrator()
    {
        var (state, _, _) = TwoPolities();
        // a suppression this epoch: the compiler should anchor a memorial
        // whose subject is the suppressing polity
        int host = -1;
        foreach (var a in state.Actors)
            if (a.Kind == ActorKind.Polity && a.Entered && !a.Retired)
            { host = a.Id; break; }
        var hex = state.Actors[host].Seat;
        state.Log.Append(state.WorldYear, ClockStratum.Generational,
            WorldEventType.EmergenceSuppressed, new[] { host }, hex,
            magnitude: 4.0, valence: -0.9, EventVisibility.Public,
            new EmergenceSuppressedPayload(-1, host,
                state.Actors[host].Name, "Testfolk", Policy: 2));
        new EpochEngine().Step(state);
        Assert.Contains(state.Pois, p => p.Type == PoiType.Memorial
            && p.Detail == 1 && p.SubjectId == host);
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
