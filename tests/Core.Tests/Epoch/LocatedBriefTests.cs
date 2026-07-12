using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Stage 2 (spec §2 "Located stock", §4b "Planner consequence"):
/// the capability brief carries per-port stockpile levels, the scheduler
/// prefers sites near supply, and the quartermaster pre-positions stock
/// before remote groundbreaking.</summary>
public class LocatedBriefTests
{
    [Fact]
    public void PerceptionBrief_CarriesThePortStockpiles()
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        port.DepositStock((int)GoodId.Alloys, 42, 0.5);

        new PerceptionPhase().Run(state);
        var view = state.Actors[0].Perception!;

        var brief = Assert.Single(view.OwnPorts);
        Assert.NotNull(brief.Stock);
        Assert.Equal(42.0, brief.Stock![(int)GoodId.Alloys], 6);
    }

    [Fact]
    public void Planner_PrefersTheSiteNearSupply()
    {
        var cfg = new EpochSimConfig();
        cfg.Sim.YearsPerEpoch = 25;
        var def = Infrastructure.Get(InfraTypeId.Mine);
        // port 0 holds the mine's whole build basket in stock; port 1 bare
        var stocked = new double[Goods.All.Count];
        foreach (var q in def.BuildCost)
            stocked[(int)q.Good] = q.Quantity;
        var ports = new[]
        {
            new PortBrief(0, Tier: 2, YardTiers: 0, Stock: stocked),
            new PortBrief(1, Tier: 2, YardTiers: 0,
                          Stock: new double[Goods.All.Count]),
        };
        var candidates = new[]
        {
            new ConstructionCandidate((int)InfraTypeId.Mine,
                new HexCoordinate(0, 0), PortId: 0, Score: 1.0),
            new ConstructionCandidate((int)InfraTypeId.Mine,
                new HexCoordinate(9, 9), PortId: 1, Score: 1.0),
        };
        // income fits ONE mine at a time: the pack order IS the preference
        var (costPerYear, _) = Planner.CostOf(
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core, 0,
                (int)InfraTypeId.Mine, 0, new HexCoordinate(0, 0), 1),
            new PerceptionView(0, 1000, new int[0], ownPorts: ports),
            cfg);
        var cap = new CapabilityBrief(costPerYear * 1.01, 0.0,
            new double[Goods.All.Count], new CommitmentBrief[0]);
        var view = new PerceptionView(0, 1000, new int[0],
            capability: cap, constructionCandidates: candidates,
            ownPorts: ports);

        var plan = Planner.BuildPlan(view, PolityPolicies.Default, cfg);

        Assert.True(plan.Entries.Count >= 2);
        Assert.Equal(0, plan.Entries[0].PortId);   // supplied site goes first
        Assert.True(plan.Entries[1].StartYear > plan.Entries[0].StartYear,
            "the bare site waits its turn in the pack");
    }

    [Fact]
    public void Requisitions_PrePositionStock_BeforeRemoteGroundbreaking()
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var home = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        var frontier = new Port(1, actor.Id,
            new HexCoordinate(actor.Seat.Q + 10, actor.Seat.R), tier: 1,
            foundedYear: 0);
        state.Ports.Add(home);
        state.Ports.Add(frontier);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        state.WorldYear = 100;
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        home.DepositStock((int)GoodId.Machinery, 100, 0.6);
        // a mine scheduled at the frontier three years out — inside the
        // lead window, no project broken ground yet
        state.Actors[0].Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(new[]
            {
                new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                    state.WorldYear + 3, (int)InfraTypeId.Mine,
                    frontier.Id, frontier.Hex, 1),
            }),
        };

        int raised = ShipmentOps.RaiseRequisitions(state,
            state.PolityOf(actor.Id));

        Assert.True(raised > 0,
            "a due-soon remote entry should pre-position its basket");
        Assert.Contains(state.Shipments,
            s => s.DestPortId == frontier.Id
                 && s.Qty[(int)GoodId.Alloys] > 0);
    }
}
