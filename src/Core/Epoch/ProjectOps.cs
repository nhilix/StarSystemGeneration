using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;        // HexGrid (completion event midpoints)
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The project lifecycle (spec §§1,4): spawn at groundbreaking,
/// feed and advance every Allocation in priority order, fire the completion
/// payload when the years are delivered. Pure deterministic math over
/// ordered state — no rolls.</summary>
public static class ProjectOps
{
    public static Project Spawn(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder) =>
        SpawnAt(state, kind, ownerActorId, funderActorId, portId, hex,
                yearsRequired, priority, planOrder, state.WorldYear);

    public static Project SpawnAt(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder,
        int startedYear)
    {
        var p = new Project(state.Projects.Count, kind, ownerActorId,
                            funderActorId, portId, hex, yearsRequired,
                            startedYear)
        { Priority = priority, PlanOrder = planOrder };
        state.Projects.Add(p);
        return p;
    }

    /// <summary>Pass 1 (spec §4): per funder in entered actor-id order,
    /// projects in (priority, plan order, id) order — the scarcest input
    /// paces each project; earlier draws starve later ones at a shared
    /// market. Returns completions this step.</summary>
    public static int AdvanceAll(SimState state)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        int spanEnd = state.WorldYear + years;
        int completions = 0;
        var mine = new List<Project>();
        foreach (var actor in state.Actors)                   // id order (P6)
        {
            if (!actor.Entered) continue;
            mine.Clear();
            foreach (var p in state.Projects)                 // id order (P6)
                if (p.InFlight && p.FunderActorId == actor.Id) mine.Add(p);
            if (mine.Count == 0) continue;
            mine.Sort((x, y) =>
            {
                int c = x.Priority.CompareTo(y.Priority);
                if (c != 0) return c;
                c = x.PlanOrder.CompareTo(y.PlanOrder);
                return c != 0 ? c : x.Id.CompareTo(y.Id);
            });
            foreach (var p in mine)
            {
                double span = Math.Min(years, spanEnd - p.StartedYear);
                if (span <= 0) continue;                      // not yet due
                double need = Math.Min(span,
                    p.YearsRequired - p.YearsDelivered);
                if (need > 0) Feed(state, p, need);
                if (p.YearsDelivered >= p.YearsRequired - 1e-9)
                {
                    Complete(state, p);
                    completions++;
                }
            }
        }
        return completions;
    }

    /// <summary>Draw needYears of the basket from the site market, then
    /// the funder's banked reserves (Stage-1 interim — Stage 2 locates
    /// them); the met fraction (min across goods AND the wage stream)
    /// scales progress, consumption, and wages alike: a starved project
    /// neither hoards goods nor pays idle crews.</summary>
    private static void Feed(SimState state, Project p, double needYears)
    {
        var market = state.Markets[p.PortId];
        var funderPolity = FunderPolity(state, p.FunderActorId);
        double fraction = 1.0;
        for (int g = 0; g < p.PerYearBasket.Length; g++)
        {
            double want = p.PerYearBasket[g] * needYears;
            if (want <= 0) continue;
            double have = market.Inventory[g]
                + (funderPolity != null ? funderPolity.ReserveQty[g] : 0.0);
            fraction = Math.Min(fraction, Math.Min(1.0, have / want));
        }
        double wages = p.WagesPerYear * needYears;
        if (wages > 0)
        {
            double treasury = TreasuryAvailable(state, p);
            fraction = Math.Min(fraction,
                Math.Min(1.0, treasury / wages));
        }
        if (fraction > 0)
            for (int g = 0; g < p.PerYearBasket.Length; g++)
            {
                double take = p.PerYearBasket[g] * needYears * fraction;
                if (take <= 0) continue;
                double grade = market.InventoryGrade[g];
                double drawn = market.Draw(g, take);
                market.LastCleared[g] += drawn;
                double shortfall = take - drawn;
                if (shortfall > 0 && funderPolity != null)
                {
                    double fromReserve = Math.Min(shortfall,
                        funderPolity.ReserveQty[g]);
                    grade = funderPolity.ReserveGrade[g];
                    funderPolity.ReserveQty[g] -= fromReserve;
                    if (funderPolity.ReserveQty[g] <= 0)
                        funderPolity.ReserveGrade[g] = 0;
                }
                if (g == (int)GoodId.ShipComponents && take > 0)
                {
                    p.AccumGrade = (p.AccumGrade * p.AccumGradeWeight
                        + grade * take) / (p.AccumGradeWeight + take);
                    p.AccumGradeWeight += take;
                }
            }
        if (wages > 0 && fraction > 0)
        {
            SpendTreasury(state, p, wages * fraction);
            MarketEngine.PayWages(state, p.PortId, wages * fraction);
        }
        p.YearsDelivered = Math.Min(p.YearsRequired,
            p.YearsDelivered + fraction * needYears);
        p.LastFedFraction = fraction;
        if (p.Kind == ProjectKind.Mobilization && FunderPolity(state,
                p.OwnerActorId) is PolityRecord mob)
            mob.Mobilization = Math.Max(mob.Mobilization, p.Progress);
    }

    /// <summary>The treasury a kind streams wages from: development for
    /// civil works, military for hulls and mobilization, corp credits for
    /// corporate funders. Expeditions charged at the act, no stream.</summary>
    private static double TreasuryAvailable(SimState state, Project p)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) return Math.Max(0, corp.Credits);
        var pr = state.PolityOf(p.FunderActorId);
        return p.Kind switch
        {
            ProjectKind.HullBatch or ProjectKind.Mobilization
                => Math.Max(0, pr.MilitaryPoints),
            ProjectKind.ColonyExpedition => double.MaxValue,
            _ => Math.Max(0, pr.DevelopmentPoints),
        };
    }

    private static void SpendTreasury(SimState state, Project p, double amount)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) { corp.Credits -= amount; return; }
        var pr = state.PolityOf(p.FunderActorId);
        switch (p.Kind)
        {
            case ProjectKind.HullBatch:
            case ProjectKind.Mobilization: pr.MilitaryPoints -= amount; break;
            case ProjectKind.ColonyExpedition: break;
            default: pr.DevelopmentPoints -= amount; break;
        }
    }

    private static PolityRecord? FunderPolity(SimState state, int actorId)
    {
        foreach (var pr in state.Polities)
            if (pr.ActorId == actorId) return pr;
        return null;
    }

    /// <summary>The completion payload (spec §1). Kind cases land across
    /// tasks 5–11; each stages its chronicle event.</summary>
    public static void Complete(SimState state, Project p)
    {
        p.Completed = true;
        switch (p.Kind)
        {
            case ProjectKind.FacilityConstruction:
            {
                var f = state.Facilities[p.TargetId];
                f.CommissionedYear = state.WorldYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.FacilityBuilt,
                    new[] { p.OwnerActorId }, f.Hex, Magnitude: f.Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new FacilityBuiltPayload(f.Id, f.TypeId, f.Tier)));
                break;
            }
            case ProjectKind.PortRaise:
            {
                var port = state.Ports[p.TargetId];
                port.Tier++;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.PortTierRaised,
                    new[] { p.OwnerActorId }, port.Hex,
                    Magnitude: port.Tier, Valence: 1.0,
                    EventVisibility.Regional,
                    new PortTierRaisedPayload(port.Id, port.Tier)));
                break;
            }
            case ProjectKind.GatePair:
            {
                var lane = state.Lanes[p.TargetId];
                state.Facilities[lane.GateAId].CommissionedYear = state.WorldYear;
                state.Facilities[lane.GateBId].CommissionedYear = state.WorldYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.LaneOpened,
                    new[] { p.OwnerActorId },
                    HexGrid.Round(
                        (state.Ports[lane.PortAId].Hex.Q
                         + state.Ports[lane.PortBId].Hex.Q) * 0.5,
                        (state.Ports[lane.PortAId].Hex.R
                         + state.Ports[lane.PortBId].Hex.R) * 0.5),
                    Magnitude: state.Facilities[lane.GateAId].Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new LaneOpenedPayload(lane.PortAId, lane.PortBId)));
                break;
            }
            case ProjectKind.HullBatch:      // Task 8
            case ProjectKind.ColonyExpedition: // Task 9
                break;
            case ProjectKind.Mobilization:
            {
                var pr = state.PolityOf(p.OwnerActorId);
                pr.Mobilization = 1.0;
                break;
            }
        }
    }

    /// <summary>A cancelled site is residue with a date and an owner of
    /// record (P1) — sunk goods stay sunk; the next replan simply stops
    /// feeding it.</summary>
    public static void Cancel(SimState state, Project p)
    {
        p.Cancelled = true;
    }
}
