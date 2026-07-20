using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice DX Stage 1 (domain hex-expansion §2): the per-hex opportunity
/// scan. Facility siting drops from cell to hex granularity across a port's
/// whole domain — extraction blooms on the richest FREE body at the frontier
/// while support/processing stays anchored at the port hex; the scan is a
/// deterministic hex spiral and stays roll-free (previews are discarded).</summary>
public class CapabilityDomainScanTests
{
    // -- a controlled minimal polity+port with a hand-built domain: full
    //    command of which hex bears which body, so the score comparison is
    //    unambiguous (no generated-body lottery across the domain). --
    private static (SimState state, int actorId, HexCoordinate portHex)
        MinimalPort(int serviceRadiusBase)
    {
        var gcfg = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 2 };
        var skeleton = new GalaxySkeleton(gcfg);
        var scfg = new EpochSimConfig { MasterSeed = 42 };
        scfg.Infrastructure.ServiceRadiusBaseHexes = serviceRadiusBase;
        scfg.Infrastructure.ServiceRadiusPerTierHexes = 0;   // radius = base, tier-flat
        var state = new SimState(scfg, skeleton);

        int actorId = 0;
        state.Polities.Add(new PolityRecord(actorId, speciesId: 0));
        var portHex = new HexCoordinate(0, 0);               // == cell (0,0) center
        state.Ports.Add(new Port(0, actorId, portHex, tier: 1, foundedYear: 0));
        state.Markets.Add(new Market(0, scfg.Economy));
        return (state, actorId, portHex);
    }

    /// <summary>A star system with a single free planetoid belt at BodyRef(0,0)
    /// — an eligible, unclaimed body a Mine will take.</summary>
    private static StarSystem BeltSystem(string name)
    {
        var sys = new StarSystem(name);
        var star = new Star();
        star.Slots.Add(new OrbitSlot
        {
            Index = 0, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 6 },
        });
        sys.Stars.Add(star);
        return sys;
    }

    /// <summary>A bodiless star system — no extraction body of any class.</summary>
    private static StarSystem BarrenSystem(string name)
    {
        var sys = new StarSystem(name);
        sys.Stars.Add(new Star());
        return sys;
    }

    /// <summary>A star system with TWO free planetoid belts (BodyRef(0,0) and
    /// BodyRef(0,1)) — a second same-class Mine can claim the second belt at the
    /// same hex, the dispersion-floor case (an own working already on this hex).</summary>
    private static StarSystem TwoBeltSystem(string name)
    {
        var sys = new StarSystem(name);
        var star = new Star();
        star.Slots.Add(new OrbitSlot
        {
            Index = 0, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 6 },
        });
        star.Slots.Add(new OrbitSlot
        {
            Index = 1, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 6 },
        });
        sys.Stars.Add(star);
        return sys;
    }

    [Fact]
    public void ConstructionCandidatesFor_IsDeterministic_ByteIdenticalAcrossRuns()
    {
        // the real preview path (unsettled hexes → Generator.Generate, no
        // commit) must be a pure function of (config, hex) — two scans of the
        // same state produce identical candidate lists, scores included.
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);

        var first = CapabilityOps.ConstructionCandidatesFor(state, actor);
        var second = CapabilityOps.ConstructionCandidatesFor(state, actor);

        Assert.NotEmpty(first);                 // a scan with real candidates
        Assert.Equal(first, second);            // record value-equality, Score too
    }

    [Fact]
    public void Extraction_RicherNeighbor_OutcompetesFullyClaimedPortHex()
    {
        // the headline behavior (§2, the generalized overflow case): the port
        // hex's only ore body is already CLAIMED by a competing mine, while a
        // one-hop neighbor bears a free belt — so the mine sites at the
        // neighbor, never at the full port hex.
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 1);
        state.Skeleton.CellAt(new HexCoordinate(0, 0)).Metallicity = 1.0; // rich ore
        // steer the ore price so extraction clears the score floor decisively
        // above the port's support types (isolates the siting question)
        var market = state.Markets[0];
        market.Price[(int)GoodId.Ore] =
            Market.InitialPrice(state.Config.Economy, GoodId.Ore) * 5.0;

        // settle every serviced hex so the preview lottery never intrudes:
        // port hex + all six neighbors are hand-built.
        var neighbor = HexGrid.Neighbor(portHex, 0);         // (1,0), one hop
        state.SettledSystems[portHex] = BeltSystem("PORT");  // belt at BodyRef(0,0)
        foreach (var n in HexGrid.Neighbors(portHex))
            state.SettledSystems[n] = BarrenSystem("N");
        state.SettledSystems[neighbor] = BeltSystem("NEIGH"); // the one free belt

        // a competing mine already holds the PORT hex's belt (fully-claimed)
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, tier: 1,
            portHex, actorId, 0) { Body = new BodyRef(0, 0) });

        var cands = CapabilityOps.ConstructionCandidatesFor(state, actorId);
        var mines = cands.Where(c => c.TypeId == (int)InfraTypeId.Mine).ToList();

        Assert.NotEmpty(mines);                              // a mine sites somewhere
        Assert.DoesNotContain(mines, m => m.Hex.Equals(portHex)); // never the full port hex
        Assert.Contains(mines, m => m.Hex.Equals(neighbor)); // at the free-bodied neighbor
    }

    [Fact]
    public void Support_ClustersAtThePortHex_NotTheDomainEdge()
    {
        // support/processing keeps port-body affinity: with no extraction body
        // anywhere in the domain, the top candidate is a support type sited AT
        // the port hex — the industrial core stays anchored while the frontier
        // is left to extraction.
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 2);
        // barren-settle the whole serviced disk so no extraction body exists
        foreach (var hex in HexGrid.Spiral(portHex, radius: 2))
            state.SettledSystems[hex] = BarrenSystem("B");

        var cands = CapabilityOps.ConstructionCandidatesFor(state, actorId);

        Assert.NotEmpty(cands);
        // one port → the list is the per-port top-3, ranked desc; the winner is
        // a non-extraction type at the port hex itself.
        var best = cands[0];
        Assert.False(BodySiting.IsExtraction((InfraTypeId)best.TypeId),
            "with no bodies in the domain the top candidate must be support/processing");
        Assert.Equal(portHex, best.Hex);
    }

    // -- Corp domain scan (§2 "Corporations run the same domain scan" + "the
    //    owner-filter seam"): a corp is routed through the SAME hex scan, scoped
    //    to its HOME-PORT domain, not by owner identity. --

    [Fact]
    public void CorpDomainScan_ReturnsRealCandidates_WhereOwnerFilterYieldsNone()
    {
        // the owner-filter trap: ConstructionCandidatesFor keeps ports where
        // port.OwnerActorId == actorId. A corp NEVER owns a port (its home port
        // belongs to its HostPolityId), so an owner-scoped scan sees zero ports
        // for the corp — the latent bug the moment a corp id is passed. The
        // home-port domain scan must return REAL candidates instead.
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 1);
        state.Skeleton.CellAt(new HexCoordinate(0, 0)).Metallicity = 1.0; // rich ore
        var market = state.Markets[0];
        market.Price[(int)GoodId.Ore] =
            Market.InitialPrice(state.Config.Economy, GoodId.Ore) * 5.0;
        state.SettledSystems[portHex] = BeltSystem("PORT");   // a free ore belt
        foreach (var n in HexGrid.Neighbors(portHex))
            state.SettledSystems[n] = BarrenSystem("N");

        var corp = new Corporation(0, actorId: 100, "Extractco",
            hostPolityId: actorId, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);

        // no port is owned by the corp — an owner-scoped scan would find nothing.
        Assert.DoesNotContain(state.Ports, p => p.OwnerActorId == corp.ActorId);

        var cands = CapabilityOps.ConstructionCandidatesForCorp(state, corp);
        Assert.NotEmpty(cands);                               // real candidates
        Assert.Contains(cands, c => c.TypeId == (int)InfraTypeId.Mine);
    }

    [Fact]
    public void CorpDomainScan_IsScopedToHomePortDomain_NotOtherPorts()
    {
        // the scan is scoped to corp.HomePortId — every candidate carries the
        // home port id, and a rich second port in another domain is never
        // touched (§8 boundary: corps scan their home domain, not others').
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 1);
        state.Skeleton.CellAt(new HexCoordinate(0, 0)).Metallicity = 1.0;
        state.Markets[0].Price[(int)GoodId.Ore] =
            Market.InitialPrice(state.Config.Economy, GoodId.Ore) * 5.0;
        state.SettledSystems[portHex] = BeltSystem("HOME");
        foreach (var n in HexGrid.Neighbors(portHex))
            state.SettledSystems[n] = BarrenSystem("N");

        // a second port with its own rich belt, well outside port 0's radius.
        var farHex = new HexCoordinate(5, 0);
        state.Ports.Add(new Port(1, actorId, farHex, tier: 1, foundedYear: 0));
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.SettledSystems[farHex] = BeltSystem("FAR");

        var corp = new Corporation(0, actorId: 100, "Extractco",
            hostPolityId: actorId, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);

        var cands = CapabilityOps.ConstructionCandidatesForCorp(state, corp);
        Assert.NotEmpty(cands);
        Assert.All(cands, c => Assert.Equal(corp.HomePortId, c.PortId));
        Assert.DoesNotContain(cands, c => c.Hex.Equals(farHex));
    }

    [Fact]
    public void CorpDomainScan_RestrictsToNicheTypes_NeverMilitaryOrCrossNiche()
    {
        // "the same scan", kept sensible: a Fabrication corp scans the processing
        // chain only — never a Fortress (military) and never an extraction type
        // it has no niche for. A barren domain still yields port-anchored
        // support/processing candidates.
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 1);
        foreach (var hex in HexGrid.Spiral(portHex, radius: 1))
            state.SettledSystems[hex] = BarrenSystem("B");

        var fab = new Corporation(0, actorId: 100, "Fabco",
            hostPolityId: actorId, CorporateNiche.Fabrication,
            homePortId: 0, foundedYear: 0);
        state.Corporations.Add(fab);

        var cands = CapabilityOps.ConstructionCandidatesForCorp(state, fab);
        Assert.NotEmpty(cands);
        Assert.All(cands, c =>
        {
            var type = (InfraTypeId)c.TypeId;
            Assert.NotEqual(InfraTypeId.Fortress, type);      // never military
            Assert.False(BodySiting.IsExtraction(type),       // fabrication ≠ extraction
                "a Fabrication corp must not site an extraction type");
        });
    }

    [Fact]
    public void HaulingDiscountFloor_IsRegistered_AndProxyKnobRetired()
    {
        // an unregistered knob silently reverts on reload and breaks
        // determinism/tuning (KnobRegistry discipline). The old arbitrary-decay
        // proxy knob was RETIRED wholesale (R1) — it must leave no registered
        // trace behind.
        bool floor = false, proxy = false;
        foreach (var k in KnobRegistry.All)
        {
            if (k.Name == "Economy.HaulingDiscountFloor") floor = true;
            if (k.Name == "Economy.HaulingProxyPerHex") proxy = true;
        }
        Assert.True(floor, "Economy.HaulingDiscountFloor must be registered");
        Assert.False(proxy, "Economy.HaulingProxyPerHex must be retired");
    }

    // -- Anti-clustering (dispersion) term (R3, design §2 amended): an
    //    extraction candidate is penalized by proximity to the builder's OWN
    //    nearest existing SAME-CLASS extraction working, so the 2nd/3rd same-
    //    class working fans off the port body. Support/processing is untouched. --

    // the mine candidate's Score at a FIXED hex X = (2,0), in a world whose only
    // free ore body sits at X, with the builder's own existing Mine placed at
    // `ownMineHex`. Scanned through the EXTRACTION-niche corp path so the type
    // set is extraction-only (no support/processing to crowd the single mine out
    // of the per-port top-3). Both own-mine placements attach to the single port,
    // so existing[Mine] == 1 either way and the /(1+existing) saturation factor
    // cancels — only the dispersion factor (nearest own same-class dist) differs.
    private static double MineScoreAtX(HexCoordinate ownMineHex)
    {
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 4);
        state.Skeleton.CellAt(new HexCoordinate(0, 0)).Metallicity = 1.0; // rich ore
        state.Markets[0].Price[(int)GoodId.Ore] =
            Market.InitialPrice(state.Config.Economy, GoodId.Ore) * 5.0;
        var candHex = new HexCoordinate(2, 0);
        foreach (var hex in HexGrid.Spiral(portHex, radius: 4))
            state.SettledSystems[hex] = BarrenSystem("B");
        state.SettledSystems[candHex] = BeltSystem("X");    // the lone free belt
        var corp = new Corporation(0, actorId: 100, "Extractco",
            hostPolityId: actorId, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);
        // the builder's (corp's) own existing mine (Body irrelevant to dispersion)
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, tier: 1,
            ownMineHex, corp.ActorId, 0) { Body = new BodyRef(0, 0) });

        var cands = CapabilityOps.ConstructionCandidatesForCorp(state, corp);
        var mine = cands.Single(c => c.TypeId == (int)InfraTypeId.Mine
            && c.Hex.Equals(candHex));
        return mine.Score;
    }

    [Fact]
    public void Dispersion_AdjacentOwnSameClassWorking_ScoresLowerThanAFarOne()
    {
        // the same candidate mine at X scores LOWER when the builder's own mine
        // sits one hex away than when it sits far off — the dispersion penalty
        // fans the second mine off the first, everything else held equal.
        double adjacent = MineScoreAtX(new HexCoordinate(1, 0));   // 1 hex from X
        double far = MineScoreAtX(new HexCoordinate(20, 0));       // 18 hexes off
        Assert.True(adjacent < far,
            $"adjacent own-mine score {adjacent} must be < far {far}");
    }

    [Fact]
    public void Dispersion_DoesNotAffectSupportProcessingScoring()
    {
        // support/processing never routes through the dispersion term (it keeps
        // port-body affinity, not extraction). Placing an own Mine right beside
        // the port must not move the top support candidate's score at all.
        double TopSupport(bool addMine)
        {
            var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 2);
            foreach (var hex in HexGrid.Spiral(portHex, radius: 2))
                state.SettledSystems[hex] = BarrenSystem("B");
            if (addMine)
                state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine,
                    tier: 1, HexGrid.Neighbor(portHex, 0), actorId, 0)
                { Body = new BodyRef(0, 0) });
            var cands = CapabilityOps.ConstructionCandidatesFor(state, actorId);
            var support = cands.First(
                c => !BodySiting.IsExtraction((InfraTypeId)c.TypeId));
            return support.Score;
        }
        Assert.Equal(TopSupport(addMine: false), TopSupport(addMine: true), 12);
    }

    [Fact]
    public void Dispersion_FloorHolds_NeverZeroesAnOwnHexSecondBody()
    {
        // an own same-class working already ON the candidate hex (nearestDist 0)
        // drives dispersion to its FLOOR 1 − DispersionWeight, NOT to zero: a
        // second free body at that hex still sites (a rich isolated site is kept
        // in contention, the whole point of the floor). Two belts at the port
        // hex; own mine on belt 0; the candidate mine takes belt 1 at dist 0.
        // Scanned through the extraction-niche corp path (extraction-only types).
        var (state, actorId, portHex) = MinimalPort(serviceRadiusBase: 1);
        state.Skeleton.CellAt(new HexCoordinate(0, 0)).Metallicity = 1.0;
        state.Markets[0].Price[(int)GoodId.Ore] =
            Market.InitialPrice(state.Config.Economy, GoodId.Ore) * 10.0;
        foreach (var n in HexGrid.Neighbors(portHex))
            state.SettledSystems[n] = BarrenSystem("N");
        state.SettledSystems[portHex] = TwoBeltSystem("PORT");   // two ore belts
        var corp = new Corporation(0, actorId: 100, "Extractco",
            hostPolityId: actorId, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);
        // the builder's (corp's) own mine already holds belt 0 at the port hex
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, tier: 1,
            portHex, corp.ActorId, 0) { Body = new BodyRef(0, 0) });

        var cands = CapabilityOps.ConstructionCandidatesForCorp(state, corp);
        var mine = cands.FirstOrDefault(c => c.TypeId == (int)InfraTypeId.Mine
            && c.Hex.Equals(portHex));
        Assert.NotNull(mine);           // floored at 1 − W (0.4), not zeroed out
        Assert.True(mine!.Score > 0);
    }

    [Fact]
    public void HaulingDiscount_IsFuelGrounded_GoodSpecificAndFloored()
    {
        // the fuel-grounded freight model (design §2): the discount is the
        // fraction of output value that SURVIVES the freight-fuel bite —
        // freightPerUnit = FuelPerUnitPerHex × steps × fuelPrice.
        var eco = new EpochSimConfig().Economy;
        double fuelPrice = 10.0;

        // a working AT the port (steps 0) pays no freight → full value survives.
        Assert.Equal(1.0, CapabilityOps.HaulingDiscount(
            eco, unitValue: 100.0, fuelPrice, steps: 0), 12);

        // same distance, two goods: the cheap/bulky good keeps LESS of its
        // value than the high-value good — good-specific, not a flat decay.
        double cheapFar = CapabilityOps.HaulingDiscount(
            eco, unitValue: 20.0, fuelPrice, steps: 6);
        double richFar = CapabilityOps.HaulingDiscount(
            eco, unitValue: 500.0, fuelPrice, steps: 6);
        Assert.True(cheapFar < richFar,
            "a cheap good hauled far must be discounted more than a high-value one");
        Assert.True(richFar > 0.9, "a high-value good barely feels the haul");

        // freight that exceeds the good's value floors the discount rather than
        // scoring the working at zero (or negative) — the whole point of the floor.
        double crushed = CapabilityOps.HaulingDiscount(
            eco, unitValue: 1.0, fuelPrice, steps: 100000);
        Assert.Equal(eco.HaulingDiscountFloor, crushed, 12);
    }
}
