using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K5: the facility click's card — a facility in the orbit view
/// is a subject, not a Market-panel row. Active and market attachment
/// ride the SAME MarketEngine derivations the sim uses (zero drift).</summary>
public class FacilityPanelTests
{
    private static (AtlasReadModel Model, SimState State, HexCoordinate Hex)
        Base()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { hex = HexGrid.CellCenter(cell.Coord); break; }
        return (new AtlasReadModel(state), state, hex);
    }

    [Fact]
    public void AnOutOfRangeIdReadsNull()
    {
        var (model, state, _) = Base();
        Assert.Null(FacilityPanel.Card(model,
            EyeContext.God(state.WorldYear), 0));
        Assert.Null(FacilityPanel.Card(model,
            EyeContext.God(state.WorldYear), -1));
    }

    [Fact]
    public void TheCardReadsTypeOwnerAndMarketAttachment()
    {
        var (model, state, hex) = Base();
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex, tier: 2,
                                 foundedYear: 5));
        var f = new Facility(0, (int)InfraTypeId.Mine, 2, hex,
                             state.Actors[0].Id, 10) { Condition = 0.8 };
        state.Facilities.Add(f);

        var card = FacilityPanel.Card(model,
            EyeContext.God(state.WorldYear), 0)!;
        Assert.Equal("Mine", card.TypeName);
        Assert.Equal(InfraFamily.Extraction, card.Family);
        Assert.Equal(2, card.Tier);
        Assert.Equal(state.Actors[0].Name, card.OwnerName);
        Assert.Equal(state.Actors[0].Kind, card.OwnerKind);
        Assert.Equal(0.8, card.Condition, 6);
        Assert.Equal(MarketEngine.AttachedMarketIndex(state, f),
                     card.MarketPortId);
        Assert.Equal(MarketEngine.IsActive(state, f), card.Active);
        Assert.Contains("Ore", card.Produces);
    }

    [Fact]
    public void AnUncommissionedFacilityReadsInactive()
    {
        var (model, state, hex) = Base();
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Refinery, 1,
            hex, state.Actors[0].Id, 10) { CommissionedYear = -1 });
        var card = FacilityPanel.Card(model,
            EyeContext.God(state.WorldYear), 0)!;
        Assert.False(card.Commissioned);
        Assert.False(card.Active);
    }

    [Fact]
    public void AKeystoneProducesNothing()
    {
        var (model, state, hex) = Base();
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Depot, 1,
            hex, state.Actors[0].Id, 10));
        var card = FacilityPanel.Card(model,
            EyeContext.God(state.WorldYear), 0)!;
        Assert.Empty(card.Produces);
    }
}
