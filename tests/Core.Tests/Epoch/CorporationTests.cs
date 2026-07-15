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

    /// <summary>Review fix (CE wave, finding 2): the estates pass must
    /// sweep the WHOLE estate — resting buys refund into the settling
    /// books, in-flight cargo and courier-fulfiller roles pass to the
    /// successor — or a dead corp keeps earning into a ledger nobody owns.</summary>
    [Fact]
    public void Nationalize_SweepsBuysShipmentsAndCourierRoles()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id, new StarGen.Core.Model.HexCoordinate(
            a0.Seat.Q + 10, a0.Seat.R), tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        state.Config.Economy.FreightHexesPerYearBase = 0.1;  // stays afloat
        int corpActor = state.Actors.Count;
        state.Actors.Add(new Actor(corpActor, ActorKind.Corporation, "Vex",
            default, state.EpochIndex,
            new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(0, corpActor, "Vex", a0.Id,
            CorporateNiche.Freight, homePortId: 0, state.WorldYear);
        state.Corporations.Add(corp);
        corp.Deposit(state, 100, 0);   // wallet is the corp's whole balance now
        // a resting buy (escrow leaves the corp at post, the convention)
        corp.Withdraw(state, 50, 0);
        OrderOps.PostBuy(state, corpActor, 1,
            (int)StarGen.Core.Substrate.GoodId.Ore, qty: 10, bid: 5,
            expiryYear: state.WorldYear + 1000);
        // an in-flight spread run owned by the corp
        var s = ShipmentOps.Dispatch(state, corpActor, ShipmentChannel.Freight,
            0, 1, new[] { ((int)StarGen.Core.Substrate.GoodId.Ore, 20.0, 0.5) });
        Assert.NotNull(s);
        // a courier the corp signed to haul
        pa.StockQty[(int)StarGen.Core.Substrate.GoodId.Alloys] = 40;
        var c = CourierOps.Post(state, a0.Id, 0, 1,
            new[] { ((int)StarGen.Core.Substrate.GoodId.Alloys, 30.0) },
            fee: 5, CourierPriority.Normal);
        Assert.NotNull(c);
        Assert.True(CourierOps.Accept(state, c!, corpActor));
        var pr = state.PolityOf(a0.Id);
        double before = pr.Credits + corp.Credits + 50;   // incl. the escrow

        Assert.True(CorporationOps.Nationalize(state, a0.Id, corp.Id));

        Assert.Equal(before, pr.Credits, 9);   // buy escrow settled too
        Assert.DoesNotContain(state.Orders,
            o => o.OwnerActorId == corpActor);
        Assert.Equal(a0.Id, s!.OwnerActorId);  // cargo passes to the state
        Assert.Equal(a0.Id, c!.FulfillerActorId);
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
    public void NicheDeath_NeverStrikesInsideTheFoundingGrace()
    {
        // the stillbirth regression: a founding gets FoundingGraceEpochs of
        // build-out plus NicheDeathEpochs of lean before the niche can kill
        // it — no corp dies of niche death in (or near) its founding epoch
        var (_, state) = EpochTestKit.Seeded(42, 10);
        new EpochEngine().Run(state);
        var knobs = state.Config.Corporate;
        int minYears = (knobs.FoundingGraceEpochs + knobs.NicheDeathEpochs)
                       * state.Config.Sim.YearsPerEpoch;
        foreach (var e in state.Log.Events)
        {
            if (e.Type != WorldEventType.NicheDied) continue;
            var corp = state.Corporations[((NicheDiedPayload)e.Payload!).CorpId];
            Assert.True(e.WorldYear - corp.FoundedYear >= minYears,
                $"corp {corp.Id} ({corp.Name}) died of niche death "
                + $"{e.WorldYear - corp.FoundedYear}y after founding");
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

    /// <summary>Length is exposure (lane-economics spec §5): with the raid
    /// floor sitting between raw cargo and length-scaled cargo, the same
    /// haven lane tempts a band only while the exposure term applies.</summary>
    [Fact]
    public void LaneLength_ScalesPiracyExposure()
    {
        var withExposure = LawlessHavenLane(
            new EpochSimConfig().Corporate.PiracyLengthPerHex);
        CorporationOps.WatchNiches(withExposure.State);
        Assert.True(BandOn(withExposure.State, withExposure.Lane.Id),
            "length exposure should lift this cargo over the raid floor");

        var withoutExposure = LawlessHavenLane(0.0);
        CorporationOps.WatchNiches(withoutExposure.State);
        Assert.False(BandOn(withoutExposure.State, withoutExposure.Lane.Id),
            "without the exposure term the same cargo sits under the floor");
    }

    private static bool BandOn(SimState state, int laneId)
    {
        foreach (var c in state.Corporations)
            if (c.Active && c.Niche == CorporateNiche.Raiding
                && c.TargetId == laneId) return true;
        return false;
    }

    /// <summary>A ruins-shadowed 10-hex lane with posted cargo, every other
    /// niche priced out, and the raid floor pinned 1.2× above raw capacity —
    /// only the 1 + PiracyLengthPerHex × 10 exposure clears it.</summary>
    private static (SimState State, Lane Lane) LawlessHavenLane(
        double piracyLengthPerHex)
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 6; i++) engine.Step(state);
        var knobs = state.Config.Corporate;
        knobs.FreightNicheMargin = 1e9;
        knobs.CartelValueFloor = 1e9;
        knobs.DepositNichePotential = 1e9;
        knobs.FabricationPriceRatio = 1e9;
        knobs.PiracyLengthPerHex = piracyLengthPerHex;
        foreach (var poi in state.Pois) poi.Depleted = true;
        // lower-id port must belong to an interior polity (the scan's key).
        // Both lane ends are FRESH ports far out in the wilds: founding
        // links now web up history's ports, and ruins at a homeworld would
        // shadow every lane there — the fixture needs the only lane in the
        // ruin's reach to be its own (FieldsAt is total off-raster).
        foreach (var port in state.Ports)
        {
            int owner = port.OwnerActorId;
            if (owner < 0 || !state.Actors[owner].Entered) continue;
            if (state.PolityOf(owner).Interior == null) continue;
            var hexA = new StarGen.Core.Model.HexCoordinate(
                port.Hex.Q + 40, port.Hex.R + 40);
            var hexB = new StarGen.Core.Model.HexCoordinate(
                hexA.Q + 10, hexA.R);
            var pa = new Port(state.Ports.Count, owner, hexA, 1,
                              (int)state.WorldYear);
            state.Ports.Add(pa);
            state.Markets.Add(new Market(pa.Id, state.Config.Economy));
            var pb = new Port(state.Ports.Count, owner, hexB, 1,
                              (int)state.WorldYear);
            state.Ports.Add(pb);
            state.Markets.Add(new Market(pb.Id, state.Config.Economy));
            var lane = EpochTestKit.AddLane(state, pa.Id, pb.Id);
            EpochTestKit.PostFreight(state, owner, lane.Id, hulls: 8);
            double capacity = FleetOps.PostedCapacity(state, lane);
            Assert.True(capacity > 0);
            // haven path: floorEff = RaidCapacityFloor × LawlessRaidFactor
            knobs.RaidCapacityFloor =
                capacity * 1.2 / state.Config.Poi.LawlessRaidFactor;
            state.Pois.Add(new PoiRecord(state.Pois.Count, PoiType.Ruins,
                hexA, magnitude: 2.0, state.WorldYear));
            return (state, lane);
        }
        throw new System.InvalidOperationException("no interior polity port");
    }
}
