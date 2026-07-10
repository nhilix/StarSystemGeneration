using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>Layer 2 of the two-layer stat model: a fleet's composition
/// aggregated into the combat/logistics vectors war resolution and the
/// economy consume (fleets/ships-and-fleets.md). Computed on demand, never
/// stored.</summary>
public sealed record FleetVectors(
    double Strike, double Sustained, double Screening, double Tracking,
    double Detection, double Stealth, double Capacity, double EnduranceFloor,
    double Upkeep);

/// <summary>Fleet aggregation and throughput math — pure functions over
/// design sheets and hull counts. State wiring (which fleets sit on which
/// lanes) lives with the postures; nothing here reads SimState.</summary>
public static class FleetMath
{
    /// <summary>Aggregate a composition into its vectors: additive mass for
    /// firepower, screens, sensors, holds and upkeep; formation minima for
    /// stealth (the loudest hull betrays everyone) and endurance (the
    /// slowest hull limits the formation).</summary>
    public static FleetVectors Vectors(
        IReadOnlyList<(DesignSheet Sheet, int Count)> composition)
    {
        double strike = 0, sustained = 0, screening = 0, tracking = 0;
        double detection = 0, capacity = 0, upkeep = 0;
        double worstSignature = 0, enduranceFloor = double.MaxValue;
        bool any = false;
        foreach (var (sheet, count) in composition)
        {
            if (count <= 0) continue;
            any = true;
            strike += sheet[ShipStat.Strike] * count;
            sustained += sheet[ShipStat.SustainedFire] * count;
            screening += (sheet[ShipStat.Screens] + sheet[ShipStat.PointDefense])
                         * 0.5 * count;
            tracking += sheet[ShipStat.Tracking] * count;
            detection += sheet[ShipStat.Sensors] * count;
            capacity += sheet[ShipStat.Cargo] * count;
            upkeep += sheet[ShipStat.Upkeep] * count;
            worstSignature = Math.Max(worstSignature, sheet[ShipStat.Signature]);
            enduranceFloor = Math.Min(enduranceFloor, sheet[ShipStat.OffLaneEndurance]);
        }
        if (!any)
            return new FleetVectors(0, 0, 0, 0, 0, 0, 0, 0, 0);
        return new FleetVectors(strike, sustained, screening, tracking,
            detection, Stealth: 1.0 / Math.Max(0.05, worstSignature),
            capacity, enduranceFloor, upkeep);
    }

    /// <summary>Freight units one posted hull group carries over an epoch:
    /// cargo per hull × round trips. Trips scale with lane transit speed and
    /// shrink with distance — the posted-capacity interface that replaces
    /// the slice-D LaneMath.Capacity stub (markets consume it).</summary>
    public static double PostedCapacityPerEpoch(FleetKnobs knobs,
        DesignSheet sheet, int count, double transitSpeed, int distanceHexes,
        int years)
    {
        if (count <= 0 || distanceHexes <= 0) return 0;
        double tripsPerYear = knobs.FreightTripsPerYearBase * transitSpeed
                              / distanceHexes;
        return sheet[ShipStat.Cargo] * count * tripsPerYear * years;
    }
}
