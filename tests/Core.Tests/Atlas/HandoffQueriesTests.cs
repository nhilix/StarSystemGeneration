using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Open Threads panel — the atlas's opening screen.
/// Parity target: `threads` (HandoffView.OpenThreads, Kind+Text verbatim);
/// each row additionally carries the camera-jump hex for its subject
/// (kickoff: "each row jumps the camera to its subject").</summary>
public class HandoffQueriesTests
{
    private static SimState Run(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void RowsMatchOpenThreadsOneToOne()
    {
        var state = Run();
        var threads = HandoffView.OpenThreads(state);
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        Assert.Equal(threads.Count, rows.Count);
        for (int i = 0; i < threads.Count; i++)
        {
            Assert.Equal(threads[i].Kind, rows[i].Kind);
            Assert.Equal(threads[i].Text, rows[i].Text);
        }
    }

    [Fact]
    public void EveryRowOfARealHistoryCarriesAJumpHex()
    {
        var state = Run();
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        Assert.True(rows.Count > 0, "a museum handoff proves nothing");
        foreach (var row in rows)
            Assert.NotNull(row.JumpHex);
    }

    [Fact]
    public void AWarThreadJumpsToItsContestedFront()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = FirstFreeHex(state);
        state.Ports.Add(new Port(0, state.Actors[0].Id, portHex,
                                 tier: 2, foundedYear: 0));
        var war = new War(0, "the Test War", state.Actors[0].Id,
                          state.Actors[1].Id, CasusBelli.BorderIncident,
                          -1, WarDemand.CedeObjectives, state.WorldYear);
        war.Objectives.Add(new WarObjective(
            0, WarObjectiveType.CapturePort, targetId: 0));
        state.Wars.Add(war);
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows, r => r.Kind == "war");
        Assert.Equal(portHex, row.JumpHex);
    }

    [Fact]
    public void AWarWithNoPortFrontJumpsToTheAttackersSeat()
    {
        var (_, state) = EpochTestKit.Seeded();
        var war = new War(0, "the Fleet War", state.Actors[0].Id,
                          state.Actors[1].Id, CasusBelli.Containment,
                          -1, WarDemand.Reparations, state.WorldYear);
        war.Objectives.Add(new WarObjective(
            0, WarObjectiveType.DestroyFleet, targetId: state.Actors[1].Id));
        state.Wars.Add(war);
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows, r => r.Kind == "war");
        Assert.Equal(state.Actors[war.AttackerId].Seat, row.JumpHex);
    }

    [Fact]
    public void AQuarantineThreadJumpsToTheLane()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a = FirstFreeHex(state);
        state.Ports.Add(new Port(0, state.Actors[0].Id, a, 2, 0));
        state.Ports.Add(new Port(1, state.Actors[1].Id,
            new HexCoordinate(a.Q + 8, a.R), 2, 0));
        var lane = EpochTestKit.AddLane(state, 0, 1);
        lane.QuarantinedUntil = state.WorldYear + 5;
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows, r => r.Kind == "quarantine");
        Assert.Equal(state.Ports[lane.PortAId].Hex, row.JumpHex);
    }

    [Fact]
    public void AnOfferThreadJumpsToTheOfferersSeat()
    {
        var (_, state) = EpochTestKit.Seeded();
        // relations only read live (Entered) pairs — genesis stages entry
        // over epochs, so put two polities on stage (BeliefTests idiom)
        var entered = new System.Collections.Generic.List<int>();
        foreach (var actor in state.Actors)
        {
            if (actor.Kind != ActorKind.Polity || entered.Count == 2) continue;
            actor.Entered = true;
            entered.Add(actor.Id);
        }
        var rel = new PolityRelation(entered[0], entered[1], 0)
        {
            OfferedRung = TreatyRung.TradePact,
            OfferedById = entered[0],
        };
        state.Relations.Add(rel);
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows, r => r.Kind == "offer");
        Assert.Equal(state.Actors[entered[0]].Seat, row.JumpHex);
    }

    [Fact]
    public void ABurningPlagueJumpsToItsOriginPort()
    {
        var (_, state) = EpochTestKit.Seeded();
        var portHex = FirstFreeHex(state);
        state.Ports.Add(new Port(0, state.Actors[0].Id, portHex, 2, 0));
        var plague = new Plague(0, "Test Pox", 0, state.WorldYear);
        plague.InfectedSince.Add(0, state.WorldYear);
        state.Plagues.Add(plague);
        var rows = HandoffQueries.ThreadRows(
            new AtlasReadModel(state), EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows, r => r.Kind == "plague");
        Assert.Equal(portHex, row.JumpHex);
    }

    private static HexCoordinate FirstFreeHex(SimState state)
    {
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid)
                return HexGrid.CellCenter(cell.Coord);
        throw new System.InvalidOperationException("all-void skeleton");
    }
}
