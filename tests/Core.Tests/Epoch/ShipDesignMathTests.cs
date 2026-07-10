using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice E task 1: the chassis catalog and the two-layer stat model
/// as pure functions (fleets/ships-and-fleets.md) — grid validity, sheet
/// derivation (embodiment/doctrine/tech/grade), aggregation vectors, posted
/// throughput.</summary>
public class ShipDesignMathTests
{
    private static DesignSheet Neutral(ShipRole role, ShipSize size) =>
        DesignMath.Sheet(role, size, Embodiment.TerranAnalog,
                         militancy: 0.5, openness: 0.5, techTier: 2, grade: 0.5);

    [Fact]
    public void ChassisGrid_MatchesTheDesignDoc()
    {
        // the doc's "—" cells and nothing else
        Assert.False(ShipCatalog.IsValid(ShipRole.Escort, ShipSize.Capital));
        Assert.False(ShipCatalog.IsValid(ShipRole.Carrier, ShipSize.Light));
        Assert.False(ShipCatalog.IsValid(ShipRole.Scout, ShipSize.Capital));
        Assert.False(ShipCatalog.IsValid(ShipRole.Colony, ShipSize.Light));
        int valid = 0;
        for (int r = 0; r < ShipCatalog.RoleCount; r++)
            for (int s = 0; s < ShipCatalog.SizeCount; s++)
                if (ShipCatalog.IsValid((ShipRole)r, (ShipSize)s)) valid++;
        Assert.Equal(20, valid);
        Assert.Equal("hauler", ShipCatalog.CellName(ShipRole.Freight, ShipSize.Medium));
        Assert.Equal("dreadnought", ShipCatalog.CellName(ShipRole.Line, ShipSize.Capital));
        Assert.Throws<System.ArgumentException>(
            () => ShipCatalog.CellName(ShipRole.Colony, ShipSize.Light));
    }

    [Fact]
    public void RoleEmphasis_DistinguishesTheRows()
    {
        var freight = Neutral(ShipRole.Freight, ShipSize.Medium);
        var escort = Neutral(ShipRole.Escort, ShipSize.Medium);
        var line = Neutral(ShipRole.Line, ShipSize.Medium);
        var scout = Neutral(ShipRole.Scout, ShipSize.Medium);
        var colony = Neutral(ShipRole.Colony, ShipSize.Medium);
        // line = armor + sustained; escort = tracking + PD (the doc's table)
        Assert.True(line[ShipStat.Armor] > escort[ShipStat.Armor]);
        Assert.True(line[ShipStat.SustainedFire] > escort[ShipStat.SustainedFire]);
        Assert.True(escort[ShipStat.Tracking] > line[ShipStat.Tracking]);
        Assert.True(escort[ShipStat.PointDefense] > line[ShipStat.PointDefense]);
        // freighters hold, scouts see and endure, colony ships carry people
        Assert.True(freight[ShipStat.Cargo] > escort[ShipStat.Cargo]);
        Assert.True(scout[ShipStat.Sensors] > freight[ShipStat.Sensors]);
        Assert.True(scout[ShipStat.OffLaneEndurance] > freight[ShipStat.OffLaneEndurance]);
        Assert.True(colony[ShipStat.Berths] > freight[ShipStat.Berths]);
    }

    [Fact]
    public void Size_ScalesBulk_AndSlowsHulls()
    {
        var hauler = Neutral(ShipRole.Freight, ShipSize.Medium);
        var bulk = Neutral(ShipRole.Freight, ShipSize.Heavy);
        Assert.True(bulk[ShipStat.Cargo] > 2 * hauler[ShipStat.Cargo] * 0.99);
        Assert.True(bulk[ShipStat.LaneSpeed] < hauler[ShipStat.LaneSpeed]);
        Assert.True(bulk[ShipStat.Signature] > hauler[ShipStat.Signature]);
        Assert.True(bulk[ShipStat.Upkeep] > hauler[ShipStat.Upkeep]);
    }

    [Fact]
    public void Embodiment_ShapesTheHull()
    {
        var terran = Neutral(ShipRole.Line, ShipSize.Medium);
        var machine = DesignMath.Sheet(ShipRole.Line, ShipSize.Medium,
            Embodiment.Machine, 0.5, 0.5, 2, 0.5);
        var lithic = DesignMath.Sheet(ShipRole.Line, ShipSize.Medium,
            Embodiment.Lithic, 0.5, 0.5, 2, 0.5);
        var hive = DesignMath.Sheet(ShipRole.Colony, ShipSize.Medium,
            Embodiment.Hive, 0.5, 0.5, 2, 0.5);
        Assert.True(machine[ShipStat.CrewDraw] < terran[ShipStat.CrewDraw] * 0.5);
        Assert.True(machine[ShipStat.Automation] > terran[ShipStat.Automation]);
        Assert.True(lithic[ShipStat.Armor] > terran[ShipStat.Armor]);
        Assert.True(lithic[ShipStat.LaneSpeed] < terran[ShipStat.LaneSpeed]);
        Assert.True(hive[ShipStat.Berths]
                    > Neutral(ShipRole.Colony, ShipSize.Medium)[ShipStat.Berths]);
    }

    [Fact]
    public void GradeAndTech_AreQualityTerms()
    {
        var standard = Neutral(ShipRole.Escort, ShipSize.Medium);
        var fine = DesignMath.Sheet(ShipRole.Escort, ShipSize.Medium,
            Embodiment.TerranAnalog, 0.5, 0.5, 2, grade: 0.8);
        // a high-grade escort gains disproportionate tracking and PD…
        Assert.True(fine[ShipStat.Tracking] / standard[ShipStat.Tracking]
                    > fine[ShipStat.Cargo] / standard[ShipStat.Cargo]);
        // …runs quieter, and needs fewer hands
        Assert.True(fine[ShipStat.Signature] < standard[ShipStat.Signature]);
        Assert.True(fine[ShipStat.CrewDraw] < standard[ShipStat.CrewDraw]);
        var earlier = DesignMath.Sheet(ShipRole.Escort, ShipSize.Medium,
            Embodiment.TerranAnalog, 0.5, 0.5, techTier: 1, grade: 0.5);
        Assert.True(earlier[ShipStat.Strike] < standard[ShipStat.Strike]);
        // capacity is physical volume — tech doesn't stretch the hold
        Assert.Equal(standard[ShipStat.Cargo], earlier[ShipStat.Cargo], 9);
    }

    [Fact]
    public void Militancy_LeansTheYardTowardGuns()
    {
        var meek = DesignMath.Sheet(ShipRole.Line, ShipSize.Medium,
            Embodiment.TerranAnalog, militancy: 0.1, 0.5, 2, 0.5);
        var fierce = DesignMath.Sheet(ShipRole.Line, ShipSize.Medium,
            Embodiment.TerranAnalog, militancy: 0.9, 0.5, 2, 0.5);
        Assert.True(fierce[ShipStat.Strike] > meek[ShipStat.Strike]);
        Assert.Equal(meek[ShipStat.Cargo], fierce[ShipStat.Cargo], 9);
    }

    [Fact]
    public void Vectors_AggregateMass_AndFormationMinima()
    {
        var escort = Neutral(ShipRole.Escort, ShipSize.Light);
        var freighter = Neutral(ShipRole.Freight, ShipSize.Heavy);
        var mixed = FleetMath.Vectors(new List<(DesignSheet, int)>
        {
            (escort, 4), (freighter, 2),
        });
        var escortsOnly = FleetMath.Vectors(new List<(DesignSheet, int)>
        {
            (escort, 4),
        });
        // firepower and holds add
        Assert.True(mixed.Strike > escortsOnly.Strike * 0.99);
        Assert.True(mixed.Capacity > escortsOnly.Capacity);
        // the loudest hull betrays the formation; the slowest limits it
        Assert.True(mixed.Stealth < escortsOnly.Stealth);
        Assert.Equal(
            System.Math.Min(escort[ShipStat.OffLaneEndurance],
                            freighter[ShipStat.OffLaneEndurance]),
            mixed.EnduranceFloor, 9);
        var empty = FleetMath.Vectors(new List<(DesignSheet, int)>());
        Assert.Equal(0, empty.Capacity);
        Assert.Equal(0, empty.EnduranceFloor);
    }

    [Fact]
    public void PostedCapacity_ScalesWithHullsAndSpeed_ShrinksWithDistance()
    {
        var knobs = new EpochSimConfig().Fleet;
        var hauler = Neutral(ShipRole.Freight, ShipSize.Medium);
        double one = FleetMath.PostedCapacityPerEpoch(knobs, hauler, 1,
            transitSpeed: 1.5, distanceHexes: 8, years: 25);
        double four = FleetMath.PostedCapacityPerEpoch(knobs, hauler, 4,
            transitSpeed: 1.5, distanceHexes: 8, years: 25);
        double far = FleetMath.PostedCapacityPerEpoch(knobs, hauler, 1,
            transitSpeed: 1.5, distanceHexes: 16, years: 25);
        Assert.True(one > 0);
        Assert.Equal(one * 4, four, 6);
        Assert.Equal(one * 0.5, far, 6);
        Assert.Equal(0, FleetMath.PostedCapacityPerEpoch(knobs, hauler, 0,
            1.5, 8, 25));
    }

    [Fact]
    public void HullCosts_ScaleWithSize_ArmamentsForWarshipsOnly()
    {
        var knobs = new EpochSimConfig().Fleet;
        Assert.True(DesignMath.ComponentsPerHull(knobs, ShipSize.Heavy)
                    > DesignMath.ComponentsPerHull(knobs, ShipSize.Light));
        Assert.True(DesignMath.ArmamentsPerHull(knobs, ShipRole.Line,
                                                ShipSize.Medium) > 0);
        Assert.Equal(0, DesignMath.ArmamentsPerHull(knobs, ShipRole.Freight,
                                                    ShipSize.Medium));
    }
}
