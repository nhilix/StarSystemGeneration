using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Character/Bio panel (`characters`/`bio` parity —
/// living roster, log-reconstructed life), the Corporation panel (`corps`
/// parity + funded projects via Project.FunderActorId — corp standing
/// plans do NOT exist yet), and the POI panel (`poi` parity).</summary>
public class CharacterCorpPoiPanelTests
{
    private static (AtlasReadModel Model, SimState State) Base()
    {
        var (_, state) = EpochTestKit.Seeded();
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void TheRosterListsTheLiving_FilteredByPolity()
    {
        var (model, state) = Base();
        int polity = state.Polities[0].ActorId;
        state.Characters.Add(new Character(0, "Vex", 0, 0, polity,
            state.WorldYear - 30) { Role = CharacterRole.Ruler });
        state.Characters.Add(new Character(1, "Mor", 0, 0, polity + 1,
            state.WorldYear - 50));
        state.Characters.Add(new Character(2, "Gone", 0, 0, polity,
            state.WorldYear - 90) { Alive = false });
        var eye = EyeContext.God(state.WorldYear);
        Assert.Equal(2, CharacterPanel.Roster(model, eye).Count);
        var row = Assert.Single(CharacterPanel.Roster(model, eye, polity));
        Assert.Equal("Vex", row.Name);
        Assert.Equal(30, row.Age);
        Assert.Equal(CharacterRole.Ruler, row.Role);
    }

    [Fact]
    public void TheBioReadsTraitsAndTheLog()
    {
        var (model, state) = Base();
        state.Characters.Add(new Character(0, "Vex", 0, 0,
            state.Polities[0].ActorId, state.WorldYear - 30)
        { Boldness = 0.8, Zeal = 0.2, Competence = 0.6, Ambition = 0.9 });
        var bio = CharacterPanel.Bio(model,
            EyeContext.God(state.WorldYear), 0);
        Assert.NotNull(bio);
        Assert.Equal(0.8, bio!.Boldness);
        Assert.Equal(0.9, bio.Ambition);
        Assert.True(bio.Alive);
        Assert.Empty(bio.Chronicle);   // a quiet life
        Assert.Null(CharacterPanel.Bio(model,
            EyeContext.God(state.WorldYear), 99));
    }

    [Fact]
    public void CorporationsCountAssetsAndCarryFundedProjects()
    {
        var (model, state) = Base();
        int host = state.Polities[0].ActorId;
        // the corp actor: corps live in the actor registry too
        var corpActorId = state.Actors.Count;
        state.Actors.Add(new Actor(corpActorId, ActorKind.Corporation,
            "Vex Combine", default, state.EpochIndex,
            new CorporateController()) { Entered = true });
        state.Corporations.Add(new Corporation(0, corpActorId,
            "Vex Combine", host, CorporateNiche.Freight, 0,
            state.WorldYear) { Credits = 300 });
        state.Facilities.Add(new Facility(0,
            (int)InfraTypeId.Depot, 1, default, corpActorId, 0));
        state.Projects.Add(new Project(0,
            ProjectKind.FacilityConstruction, host, corpActorId, 0,
            default, 8, (int)state.WorldYear));

        var row = Assert.Single(CorporationPanel.Rows(model,
            EyeContext.God(state.WorldYear)));
        Assert.Equal("Vex Combine", row.Name);
        Assert.Equal(host, row.HostPolityId);
        Assert.Equal(1, row.FacilityCount);
        Assert.Equal(300.0, row.Credits);
        var funded = Assert.Single(row.FundedProjectIds);
        Assert.Equal(0, funded);
    }

    [Fact]
    public void PoisReadTheRegistry_LiveAndFaded()
    {
        var (model, state) = Base();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { hex = HexGrid.CellCenter(cell.Coord); break; }
        state.Pois.Add(new PoiRecord(0, PoiType.Battlefield, hex,
            magnitude: 4.5, foundedYear: 12) { HullsSalvaged = 1 });
        state.Pois.Add(new PoiRecord(1, PoiType.Ruins, hex,
            magnitude: 2.0, foundedYear: 40) { Depleted = true });

        var eye = EyeContext.God(state.WorldYear);
        var rows = PoiPanel.Rows(model, eye);
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, PoiPanel.LiveCount(model, eye));
        Assert.Equal(4.5, rows[0].Magnitude);
        Assert.Equal(3.5, rows[0].SalvageRemaining);
        Assert.True(rows[1].Depleted);
        var card = PoiPanel.Card(model, eye, 0);
        Assert.NotNull(card);
        Assert.Equal(PoiType.Battlefield, card!.Row.Type);
        Assert.Empty(card.Chronicle);
    }
}
