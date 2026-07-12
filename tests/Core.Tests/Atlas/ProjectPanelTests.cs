using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Project inspector (NEW, T1) — click a works-lens site
/// mark. Parity target: `eprojects` (Repl.RenderProjects): in-flight
/// filter, funder filter, and the HONEST eta under current starvation —
/// WorldYear + ceil(remaining / max(LastFedFraction, 0.05)).</summary>
public class ProjectPanelTests
{
    private static (AtlasReadModel Model, SimState State) WithProjects()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { hex = HexGrid.CellCenter(cell.Coord); break; }
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex, 2, 0));
        var inFlight = new Project(0, ProjectKind.FacilityConstruction,
            state.Actors[0].Id, state.Actors[0].Id, 0, hex,
            yearsRequired: 10, startedYear: (int)state.WorldYear)
        { LastFedFraction = 0.25, YearsDelivered = 4 };
        var done = new Project(1, ProjectKind.PortRaise,
            state.Actors[0].Id, state.Actors[1].Id, 0, hex,
            yearsRequired: 6, startedYear: (int)state.WorldYear)
        { Completed = true, YearsDelivered = 6 };
        state.Projects.Add(inFlight);
        state.Projects.Add(done);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void CardsDefaultToInFlightOnly_LikeEprojects()
    {
        var (model, state) = WithProjects();
        var eye = EyeContext.God(state.WorldYear);
        var cards = ProjectPanel.Cards(model, eye);
        var card = Assert.Single(cards);
        Assert.Equal(0, card.Id);
        Assert.Equal(2, ProjectPanel.Cards(model, eye, includeAll: true).Count);
    }

    [Fact]
    public void TheFunderFilterDrawsTheTreasuryLine()
    {
        var (model, state) = WithProjects();
        var eye = EyeContext.God(state.WorldYear);
        var forFunder1 = ProjectPanel.Cards(model, eye,
            funderActorId: state.Actors[1].Id, includeAll: true);
        var card = Assert.Single(forFunder1);
        Assert.Equal(1, card.Id);
    }

    [Fact]
    public void TheEtaIsTheHonestOne_UnderCurrentStarvation()
    {
        var (model, state) = WithProjects();
        var card = Assert.Single(
            ProjectPanel.Cards(model, EyeContext.God(state.WorldYear)));
        // remaining 6y at fed 0.25 → 24 more years, ceil'd — eprojects parity
        Assert.Equal(state.WorldYear + 24, card.EtaYear);

        state.Projects[0].LastFedFraction = 0.0;   // fully starved: clamp 0.05
        card = Assert.Single(
            ProjectPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(state.WorldYear
            + (long)System.Math.Ceiling(6 / 0.05), card.EtaYear);
    }

    [Fact]
    public void FinishedWorkCarriesNoEta()
    {
        var (model, state) = WithProjects();
        var all = ProjectPanel.Cards(model,
            EyeContext.God(state.WorldYear), includeAll: true);
        Assert.Null(all[1].EtaYear);
        Assert.True(all[1].Completed);
    }

    [Fact]
    public void TheCardSeparatesFunderFromOwner_AndCarriesTheBasket()
    {
        var (model, state) = WithProjects();
        state.Projects[1].PerYearBasket[(int)GoodId.Alloys] = 3.5;
        var card = ProjectPanel.Card(model,
            EyeContext.God(state.WorldYear), projectId: 1);
        Assert.NotNull(card);
        Assert.Equal(state.Actors[0].Name, card!.OwnerName);
        Assert.Equal(state.Actors[1].Name, card.FunderName);
        var line = Assert.Single(card.Basket);
        Assert.Equal(GoodId.Alloys, line.Good);
        Assert.Equal(3.5, line.QtyPerYear);
    }

    [Fact]
    public void ProgressAndStarvationReadStraightFromTheRegistry()
    {
        var (model, state) = WithProjects();
        var card = Assert.Single(
            ProjectPanel.Cards(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0.4, card.Progress, 10);
        Assert.Equal(0.25, card.FedFraction);
        Assert.Equal(4.0, card.YearsDelivered);
        Assert.Equal(10.0, card.YearsRequired);
    }
}
