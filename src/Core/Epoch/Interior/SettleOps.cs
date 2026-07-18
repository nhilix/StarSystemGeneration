using System;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The settle election (domain-hex-expansion design §3, "The settle
/// election") — Stage 2's centerpiece: pop follows work. A dedicated Interior
/// pass, distinct from <see cref="Phases.InteriorPhase"/>'s port-to-port
/// <c>Migrate</c> (ledger decision #1): triggered by SUSTAINED unmet
/// weighted-labor at a worked satellite hex WITHIN one port's domain, it moves
/// an eligible segment's <c>(Hex, Body)</c> to that hex while keeping its
/// <c>PortId</c> (the parent port still administers it), and that settlement
/// event founds a lightweight <see cref="Outpost"/>.
///
/// <para>Three disciplines hold this pass:</para>
/// <list type="bullet">
/// <item>CONSERVATION (flow #1): the habitat cost leaves the segment's Wealth
/// and lands, to the credit, as construction wages across the domain's
/// households (<see cref="MarketEngine.PayWages"/>) — money moves WHERE it
/// lands, never how much exists.</item>
/// <item>WORLD-TIME (P7): "sustained" is DERIVED from facility maturity
/// (<see cref="Facility.CommissionedYear"/>) and a per-domain settle cadence
/// gate — no per-hex timer, no step counts (cf. L2's FineTick saga). A finer
/// clock settles no faster over the same world-years.</item>
/// <item>DETERMINISM (P6): ports in id order → domain hexes in spiral order →
/// segments in id order; the only roll is the keyed outpost name
/// (<see cref="RollChannel.OutpostName"/>).</item>
/// </list></summary>
public static class SettleOps
{
    /// <summary>Run the settle election across every port's domain. Returns
    /// the number of outposts founded this step.</summary>
    public static int Step(SimState state)
    {
        var exp = state.Config.Expansion;
        int founded = 0;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (port.OwnerActorId < 0
                || port.OwnerActorId >= state.Actors.Count
                || !state.Actors[port.OwnerActorId].Entered) continue;
            // per-domain settle cadence (P7, mirrors FoundingCadenceYears):
            // hold fire while this domain's last outpost is younger than the
            // cadence window, so a finer clock does not settle more hexes
            // over the same world-years.
            long lastFounding = long.MinValue;
            foreach (var o in state.Outposts)             // id order (P6)
                if (o.ParentPortId == port.Id && o.FoundingYear > lastFounding)
                    lastFounding = o.FoundingYear;
            if (lastFounding != long.MinValue
                && state.WorldYear - lastFounding < exp.SettleCadenceYears)
                continue;

            if (TrySettleDomain(state, port)) founded++;
        }
        return founded;
    }

    /// <summary>Find the first candidate satellite hex in this port's domain
    /// (spiral order) and, if a segment is eligible, settle it. One outpost
    /// per domain per pass.</summary>
    private static bool TrySettleDomain(SimState state, Port port)
    {
        var cfg = state.Config;
        int radius = PortDomains.ServiceRadius(cfg, port.Tier)
                     + TechOps.AstroRadiusBonus(state, port.OwnerActorId);
        var portCell = HexGrid.CellOf(port.Hex);
        foreach (var cell in state.Skeleton.Cells)        // cell spiral order (P6)
        {
            if (cell.IsVoid) continue;
            var cellCenter = HexGrid.CellCenter(cell.Coord);
            // whole-cell reject: no hex in a cell whose center is farther than
            // radius + CellRadius can be serviced (CapabilityOps' trick).
            if (HexGrid.Distance(port.Hex, cellCenter)
                > radius + HexGrid.CellRadius) continue;
            foreach (var hex in HexGrid.Spiral(cellCenter, HexGrid.CellRadius))
            {                                             // hex spiral order (P6)
                if (HexGrid.Distance(port.Hex, hex) > radius) continue;
                if (hex.Equals(port.Hex)) continue;       // the port hex is home
                if (!IsUnderLaboredWorkedHex(state, port, hex)) continue;
                if (HasResident(state, hex)) continue;    // already settled
                if (PortAtHex(state, hex)) continue;      // never over a port
                var seg = ElectSegment(state, port);
                if (seg == null) continue;                // no eligible funder
                Settle(state, port, seg, hex);
                return true;
            }
        }
        return false;
    }

    /// <summary>A satellite hex qualifies when its worked facilities attached
    /// to this port are MATURE (commissioned ≥ SettleMaturityYears ago — the
    /// world-time "sustained" derivation) and their combined weighted
    /// workforce falls short of their combined LaborRequired by more than the
    /// configured fraction. No producing facility here (or a too-young one) →
    /// not a candidate.</summary>
    private static bool IsUnderLaboredWorkedHex(SimState state, Port port,
                                                HexCoordinate hex)
    {
        var exp = state.Config.Expansion;
        double required = 0, workforce = 0;
        bool anyWorking = false;
        foreach (var f in state.Facilities)               // id order (P6)
        {
            if (!f.Hex.Equals(hex)) continue;
            if (!MarketEngine.IsActive(state, f)) continue;
            if (MarketEngine.AttachedMarketIndex(state, f) != port.Id) continue;
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            if (def.Produces.Count == 0 || def.LaborRequired <= 0) continue;
            // the world-time "sustained" gate: the shortfall only counts once
            // the working has stood for SettleMaturityYears — a brief spike at
            // a fresh working never settles anyone (design §3, P7).
            if (state.WorldYear - f.CommissionedYear < exp.SettleMaturityYears)
                return false;
            anyWorking = true;
            required += def.LaborRequired;
            workforce += StaffingOps.WeightedWorkforce(state, f, port.Id);
        }
        if (!anyWorking || required <= 0) return false;
        return workforce < required * (1.0 - exp.SettleLaborShortfallFraction);
    }

    /// <summary>Any peopled segment already settled at this hex (any domain).</summary>
    private static bool HasResident(SimState state, HexCoordinate hex)
    {
        foreach (var s in state.Segments)                 // id order (P6)
            if (s.Size > 0 && s.Hex.Equals(hex)) return true;
        return false;
    }

    private static bool PortAtHex(SimState state, HexCoordinate hex)
    {
        foreach (var p in state.Ports)                    // id order (P6)
            if (p.Hex.Equals(hex)) return true;
        return false;
    }

    /// <summary>Elect the eligible funder: a segment administered by this port,
    /// still resident at the port hex, with Size &gt; 0 and Wealth ≥ the habitat
    /// cost (below that it cannot fund a meaningful habitat — design §3).
    /// Deterministic pick: largest Size (the segment most able to bankroll and
    /// populate a viable outpost), ties broken by lowest Id — a total order, so
    /// no tiebreak roll is needed.</summary>
    private static PopulationSegment? ElectSegment(SimState state, Port port)
    {
        var exp = state.Config.Expansion;
        PopulationSegment? best = null;
        foreach (var s in state.Segments)                 // id order (P6)
        {
            if (s.PortId != port.Id) continue;
            if (s.Size <= 0) continue;
            if (!s.Hex.Equals(port.Hex)) continue;        // a port household
            if (s.Wealth < exp.SettleHabitatCost) continue;
            if (best == null || s.Size > best.Size)       // ties keep lower id
                best = s;
        }
        return best;
    }

    /// <summary>Pay the habitat cost (conserved), relocate the segment, and
    /// found the outpost.</summary>
    private static void Settle(SimState state, Port port,
                               PopulationSegment seg, HexCoordinate hex)
    {
        var exp = state.Config.Expansion;
        // CONSERVATION flow #1: the habitat cost leaves the segment's Wealth
        // and lands, to the credit, as construction wages across the domain's
        // households (the segment itself banks its size-share back). Clamped to
        // available Wealth so it never drives negative; eligibility already
        // guaranteed Wealth ≥ the cost, so the clamp is a floor, not a haircut.
        double habitatCost = Math.Min(exp.SettleHabitatCost, seg.Wealth);
        seg.Wealth -= habitatCost;
        MarketEngine.PayWages(state, port.Id, habitatCost);

        // pop follows work: the segment now RESIDES at the satellite hex
        // (staffing weights it there at full strength — Task 2.4), still
        // administered by and trading through the parent port.
        seg.Hex = hex;
        seg.Body = PopulationSiting.Assign(state, port.Id, hex);

        // the settlement event IS the outpost founding.
        int outpostId = state.Outposts.Count;
        state.Outposts.Add(new Outpost(outpostId, OutpostName(state, outpostId),
            hex, port.Id, state.WorldYear));
        // surfaces in the narrative/news feed via the same StagedEvent
        // mechanism CompleteExpedition uses for a founding (design §3), but its
        // OWN event type: an outpost is not a port, and port-scoped consumers
        // (news, POI, ExpansionTests' Location==port.Hex contract) must never
        // read it as one. The Location carries the outpost hex; the payload
        // names the founding polity and the outpost id.
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.OutpostFounded,
            new[] { port.OwnerActorId }, hex, Magnitude: 1.0, Valence: 1.0,
            EventVisibility.Regional,
            new OutpostFoundedPayload(state.Actors[port.OwnerActorId].Name,
                                      outpostId)));
    }

    /// <summary>Outpost name: syllables on the keyed OutpostName channel
    /// (step = outpost id, actor = −1), mirroring GraduationOps/FederationOps'
    /// SyllableName. A stateless keyed roll (P6) — same config, same name.</summary>
    private static string OutpostName(SimState state, int key)
    {
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed, RollChannel.OutpostName,
            key, -1, 100) < 0.4 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.OutpostName, key, -1, 10 + i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word);
    }
}
