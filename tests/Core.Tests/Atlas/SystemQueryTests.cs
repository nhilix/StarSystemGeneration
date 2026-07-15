using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K5: the SystemStage's read model — the hex-tier system laid
/// out as orbit rows (pure function, computed on demand, never persisted)
/// plus the epoch overlays (port, facilities, in-flight sites) ATTACHED
/// to orbits by deterministic affinity rules.</summary>
public class SystemQueryTests
{
    private static (AtlasReadModel Model, SimState State) Base()
    {
        var (_, state) = EpochTestKit.Seeded();
        return (new AtlasReadModel(state), state);
    }

    /// <summary>A hex whose hex-tier roll holds a system, and one that is
    /// an empty reach — hunted deterministically from the cell centers.</summary>
    private static (HexCoordinate SystemHex, HexCoordinate EmptyHex)
        Hexes(AtlasReadModel model)
    {
        HexCoordinate? sys = null, empty = null;
        foreach (var cell in model.Cells)
        {
            if (cell.IsVoid) continue;
            var hex = HexGrid.CellCenter(cell.Coord);
            var context = new GalaxyContext(model.Skeleton.Config)
            { Skeleton = model.Skeleton };
            var result = Core.Generation.Generator.Generate(context, hex);
            if (result.System != null) sys ??= hex; else empty ??= hex;
            if (sys != null && empty != null) break;
        }
        Assert.NotNull(sys);
        Assert.NotNull(empty);
        return (sys!.Value, empty!.Value);
    }

    // ---- the full query ----

    [Fact]
    public void ASystemHexReadsTheHexTier_Deterministically()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        var eye = EyeContext.God(state.WorldYear);
        var once = SystemQuery.At(model, eye, hex);
        var twice = SystemQuery.At(model, eye, hex);

        Assert.True(once.HasSystem);
        Assert.False(string.IsNullOrEmpty(once.Designation));
        Assert.NotEmpty(once.Stars);
        Assert.Equal(once.Designation, twice.Designation);
        Assert.Equal(once.Stars.Count, twice.Stars.Count);
        Assert.Equal(once.Orbits.Count, twice.Orbits.Count);
        for (int i = 0; i < once.Orbits.Count; i++)
            Assert.Equal(once.Orbits[i], twice.Orbits[i]);
    }

    [Fact]
    public void EverySlotGetsARingRow_OccupiedOrNot()
    {
        // option A draws a ring per slot, empty ones included; belts are
        // dashed rings; the habitable band is a tinted annulus — the
        // stage needs ALL slots, not just occupied orbits (eyeball wave)
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);

        int slotTotal = 0;
        var context = new GalaxyContext(model.Skeleton.Config)
        { Skeleton = model.Skeleton };
        var system = Core.Generation.Generator.Generate(context, hex).System!;
        foreach (var star in system.Stars) slotTotal += star.Slots.Count;

        Assert.Equal(slotTotal, info.Rings.Count);
        Assert.True(info.Rings.Count >= info.Orbits.Count);
        foreach (var orbit in info.Orbits)
        {
            var ring = Assert.Single(info.Rings, r =>
                r.StarIndex == orbit.StarIndex
                && r.SlotIndex == orbit.SlotIndex);
            Assert.Equal(orbit.Band, ring.Band);
            Assert.Equal(orbit.Kind == BodyKind.PlanetoidBelt, ring.IsBelt);
        }
    }

    [Fact]
    public void OrbitRowsExistOnlyForOccupiedSlots()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        foreach (var row in info.Orbits)
        {
            Assert.InRange(row.StarIndex, 0, info.Stars.Count - 1);
            Assert.True(row.Size >= 0);
        }
    }

    [Fact]
    public void AnEmptyReachHasNoStars_ButKeepsOverlays()
    {
        var (model, state) = Base();
        var (_, empty) = Hexes(model);
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Depot, 1,
                                          empty, state.Actors[0].Id, 10));
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), empty);
        Assert.False(info.HasSystem);
        Assert.Empty(info.Stars);
        Assert.Empty(info.Orbits);
        var fac = Assert.Single(info.Facilities);
        Assert.Equal("Depot", fac.TypeName);
        Assert.Equal(OrbitRef.None, fac.At);
    }

    [Fact]
    public void ThePortAndFacilitiesSurfaceInIdOrder()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex, tier: 2,
                                 foundedYear: 5));
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Shipyard, 1,
                                          hex, state.Actors[0].Id, 10));
        state.Facilities.Add(new Facility(1, (int)InfraTypeId.Mine, 1,
                                          hex, state.Actors[0].Id, 12));
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);

        Assert.Equal(0, info.PortId);
        Assert.Equal(2, info.PortTier);
        Assert.Equal(state.Actors[0].Name, info.PortOwnerName);
        Assert.Equal(2, info.Facilities.Count);
        Assert.Equal(0, info.Facilities[0].Id);
        Assert.Equal(1, info.Facilities[1].Id);
        Assert.Equal("Shipyard", info.Facilities[0].TypeName);
        Assert.Equal("Mine", info.Facilities[1].TypeName);
    }

    [Fact]
    public void AnUncommissionedFacilityIsItsSite_NotAFacilityMark()
    {
        // the Facility row exists at groundbreaking (CommissionedYear -1)
        // alongside its InFlight project — the stage must draw ONE mark,
        // the site (review finding 2: the double-render + false "idle")
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Refinery, 1,
            hex, state.Actors[0].Id, 10) { CommissionedYear = -1 });
        state.Projects.Add(new Project(0, ProjectKind.FacilityConstruction,
            state.Actors[0].Id, state.Actors[0].Id, 0, hex,
            yearsRequired: 4, startedYear: 10)
        { TypeId = (int)InfraTypeId.Refinery });

        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        Assert.Empty(info.Facilities);
        var site = Assert.Single(info.Sites);
        Assert.Equal("Refinery", site.TypeName);
    }

    [Fact]
    public void FacilityRow_RendersItsDecidedBody_NotAGuess()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        // a commissioned Mine whose body was DECIDED (not the type-affinity
        // guess FacilityOrbit would produce): slot (0,0), whatever it holds.
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, 1,
            hex, state.Actors[0].Id, 10) { Body = new BodyRef(0, 0) });
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        var row = Assert.Single(info.Facilities);
        Assert.Equal(new BodyRef(0, 0), row.At);
    }

    [Fact]
    public void FacilityRow_WithNoBody_FallsBackToThePortBody()
    {
        // Genesis-path facilities (entry starter industry, gate pairs, colony
        // founding) never go through BodySiting.Assign, so their Body stays
        // None. In a settled (non-null) system the row must dock at the port
        // body — mirroring the sites loop — not collapse to deep orbit.
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Refinery, 1,
            hex, state.Actors[0].Id, 10) { Body = BodyRef.None });
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        var row = Assert.Single(info.Facilities);

        var context = new GalaxyContext(model.Skeleton.Config)
        { Skeleton = model.Skeleton };
        var system = Core.Generation.Generator.Generate(context, hex).System!;
        var portAt = SystemQuery.PortOrbit(system);
        Assert.Equal(portAt, row.At);
        Assert.NotEqual(OrbitRef.None, row.At);
    }

    [Fact]
    public void InFlightSitesSurface_CompletedOnesDoNot()
    {
        var (model, state) = Base();
        var (hex, _) = Hexes(model);
        var live = new Project(0, ProjectKind.FacilityConstruction,
            state.Actors[0].Id, state.Actors[0].Id, 0, hex,
            yearsRequired: 4, startedYear: 10)
        { TypeId = (int)InfraTypeId.Refinery };
        var done = new Project(1, ProjectKind.FacilityConstruction,
            state.Actors[0].Id, state.Actors[0].Id, 0, hex,
            yearsRequired: 4, startedYear: 10)
        { TypeId = (int)InfraTypeId.Mine, Completed = true };
        state.Projects.Add(live);
        state.Projects.Add(done);
        var info = SystemQuery.At(model, EyeContext.God(state.WorldYear), hex);
        var site = Assert.Single(info.Sites);
        Assert.Equal(0, site.ProjectId);
        Assert.Equal("Refinery", site.TypeName);
    }

    // ---- the attachment rules (pure, on hand-built systems) ----

    private static StarSystem System(params Body?[] bodies)
    {
        var system = new StarSystem("TEST-0000");
        var star = new Star { TypeId = "G", TypeName = "yellow dwarf" };
        for (int i = 0; i < bodies.Length; i++)
        {
            var band = i == 1 ? OrbitBand.Habitable
                     : i < 1 ? OrbitBand.Inner : OrbitBand.Outer;
            star.Slots.Add(new OrbitSlot
            { Index = i, Band = band, Body = bodies[i] });
        }
        system.Stars.Add(star);
        return system;
    }

    private static Body B(BodyKind kind, int size = 3,
                          Settlement settlement = Settlement.None,
                          Biosphere biosphere = Biosphere.Barren) =>
        new() { Kind = kind, Size = size, Settlement = settlement,
                Biosphere = biosphere };

    [Fact]
    public void ThePortSitsAtTheMostSettledBody()
    {
        var sys = System(
            B(BodyKind.RockyWorld, size: 9, settlement: Settlement.Colony),
            B(BodyKind.RockyWorld, size: 4,
              settlement: Settlement.MajorWorld),
            B(BodyKind.GasGiant, size: 10));
        Assert.Equal(new OrbitRef(0, 1), SystemQuery.PortOrbit(sys));
    }

    [Fact]
    public void AnUnsettledSystemPortsAtTheHabitableBand_ElseFirstBody()
    {
        var habitable = System(
            B(BodyKind.RockyWorld), B(BodyKind.RockyWorld),
            B(BodyKind.IceWorld));
        Assert.Equal(new OrbitRef(0, 1), SystemQuery.PortOrbit(habitable));

        var barren = System(
            B(BodyKind.IceWorld), null, B(BodyKind.GasGiant));
        Assert.Equal(new OrbitRef(0, 0), SystemQuery.PortOrbit(barren));
    }

    [Fact]
    public void ABodilessSystemPortsInDeepOrbit()
    {
        var sys = System(null, null);
        Assert.Equal(OrbitRef.None, SystemQuery.PortOrbit(sys));
    }

    [Fact]
    public void ExtractionSitesWhereItsSubstrateIs()
    {
        var sys = System(
            B(BodyKind.RockyWorld, settlement: Settlement.Colony),
            B(BodyKind.RockyWorld, biosphere: Biosphere.Flourishing),
            B(BodyKind.PlanetoidBelt),
            B(BodyKind.GasGiant));
        var port = SystemQuery.PortOrbit(sys);

        Assert.Equal(new OrbitRef(0, 2),
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Mine, port));
        Assert.Equal(new OrbitRef(0, 3),
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Skimmer, port));
        Assert.Equal(new OrbitRef(0, 1),
            SystemQuery.FacilityOrbit(sys, InfraTypeId.AgriComplex, port));
    }

    [Fact]
    public void ProcessingAndSupportDockAtThePortBody()
    {
        var sys = System(
            B(BodyKind.RockyWorld, settlement: Settlement.Colony),
            B(BodyKind.RockyWorld), B(BodyKind.PlanetoidBelt));
        var port = SystemQuery.PortOrbit(sys);
        Assert.Equal(port,
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Refinery, port));
        Assert.Equal(port,
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Shipyard, port));
        Assert.Equal(port,
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Fortress, port));
    }

    [Fact]
    public void ExcavationPrefersWreckage_ElseRock()
    {
        var wrecked = System(
            B(BodyKind.RockyWorld, settlement: Settlement.Colony),
            B(BodyKind.Wreckage));
        Assert.Equal(new OrbitRef(0, 1), SystemQuery.FacilityOrbit(
            wrecked, InfraTypeId.ExcavationSite,
            SystemQuery.PortOrbit(wrecked)));

        var rocky = System(
            B(BodyKind.GasGiant, settlement: Settlement.Outpost),
            B(BodyKind.RockyWorld));
        Assert.Equal(new OrbitRef(0, 1), SystemQuery.FacilityOrbit(
            rocky, InfraTypeId.ExcavationSite, SystemQuery.PortOrbit(rocky)));
    }

    [Fact]
    public void MissingAffinityFallsBackToThePortBody()
    {
        var sys = System(
            B(BodyKind.RockyWorld, settlement: Settlement.Colony));
        var port = SystemQuery.PortOrbit(sys);
        Assert.Equal(port,
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Skimmer, port));
        Assert.Equal(port,
            SystemQuery.FacilityOrbit(sys, InfraTypeId.Mine, port));
    }

    // ---- layout angles: pure hash, stable, spread ----

    [Fact]
    public void OrbitAnglesAreStable_AndNotDegenerate()
    {
        var hex = new HexCoordinate(3, -2);
        double a0 = SystemQuery.OrbitAngle(hex, 0, 0);
        double a1 = SystemQuery.OrbitAngle(hex, 0, 1);
        Assert.Equal(a0, SystemQuery.OrbitAngle(hex, 0, 0));
        Assert.NotEqual(a0, a1);
        Assert.InRange(a0, 0.0, 6.2832);
        Assert.InRange(a1, 0.0, 6.2832);
    }
}
