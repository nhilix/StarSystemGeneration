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

    [Fact]
    public void HaulingProxyPerHex_IsRegistered()
    {
        // an unregistered knob silently reverts on reload and breaks
        // determinism/tuning (KnobRegistry discipline).
        bool found = false;
        foreach (var k in KnobRegistry.All)
            if (k.Name == "Economy.HaulingProxyPerHex") found = true;
        Assert.True(found, "Economy.HaulingProxyPerHex must be registered");
    }
}
