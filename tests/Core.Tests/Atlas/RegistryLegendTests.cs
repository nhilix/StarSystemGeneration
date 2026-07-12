using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the registry drawer (find/stats/goods/knobs behind the
/// topbar search) and the per-lens LEGEND (the K2 eyeball ask). The
/// legend's entries are built from the SAME lens constants the layers
/// draw with — asserted here against the public lens derivations so the
/// mapping can never drift.</summary>
public class RegistryLegendTests
{
    private static (AtlasReadModel Model, SimState State) Base()
    {
        var (_, state) = EpochTestKit.Seeded();
        return (new AtlasReadModel(state), state);
    }

    // ---- the registry drawer ----

    [Fact]
    public void KnobsReadLiveValues_AndFilter()
    {
        var (model, state) = Base();
        var eye = EyeContext.God(state.WorldYear);
        var all = RegistryQueries.Knobs(model, eye);
        Assert.Equal(KnobRegistry.All.Count, all.Count);
        var stock = RegistryQueries.Knobs(model, eye, "StockCap");
        Assert.Equal(2, stock.Count);
        foreach (var row in stock)
        {
            Assert.Contains("StockCap", row.Name);
            Assert.False(string.IsNullOrEmpty(row.Doc));
        }
        var one = RegistryQueries.Knobs(model, eye,
            "Economy.StockCapPerPortTier");
        Assert.Equal(state.Config.Economy.StockCapPerPortTier,
                     Assert.Single(one).Value);
    }

    [Fact]
    public void TheGoodsCatalogIsThe17GoodVocabulary()
    {
        var (model, state) = Base();
        var rows = RegistryQueries.GoodsCatalog(model,
            EyeContext.God(state.WorldYear));
        Assert.Equal(Goods.All.Count, rows.Count);
        Assert.Equal(Goods.All[0].Name, rows[0].Name);
        Assert.Equal(Goods.All[0].Tier, rows[0].Tier);
    }

    [Fact]
    public void FindSearchesTheRegistriesByName()
    {
        var (model, state) = Base();
        HexCoordinate hex = default;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { hex = HexGrid.CellCenter(cell.Coord); break; }
        state.Characters.Add(new Character(0, "Vexxaro the Bold", 0, 0,
            state.Polities[0].ActorId, state.WorldYear - 30));
        state.Pois.Add(new PoiRecord(0, PoiType.Ruins, hex, 2.0, 10));
        var eye = EyeContext.God(state.WorldYear);

        var hits = RegistryQueries.Find(model, eye, "vexxaro");
        var hit = Assert.Single(hits);
        Assert.Equal(FindKind.Character, hit.Kind);
        Assert.Equal("Vexxaro the Bold", hit.Name);

        var ruins = RegistryQueries.Find(model, eye, "ruins");
        Assert.Contains(ruins, h => h.Kind == FindKind.Poi
                                    && Equals(h.JumpHex, hex));

        // every entered polity is findable by its name
        foreach (var a in state.Actors)
        {
            if (a.Kind != ActorKind.Polity) continue;
            Assert.Contains(RegistryQueries.Find(model, eye, a.Name),
                h => h.Kind == FindKind.Actor && h.Id == a.Id);
            break;
        }
    }

    [Fact]
    public void StatsSummarizeTheRegistries()
    {
        var (model, state) = Base();
        var stats = RegistryQueries.Stats(model,
            EyeContext.God(state.WorldYear));
        Assert.Equal(state.Ports.Count, stats.Ports);
        Assert.Equal(state.Lanes.Count, stats.Lanes);
        Assert.Equal(state.WorldYear, stats.WorldYear);
        Assert.Equal(state.Log.Events.Count, stats.Events);
    }

    // ---- the per-lens legend ----

    [Fact]
    public void EveryRailLensHasALegend()
    {
        foreach (var key in new[] { "domains", "war", "tension", "lanes",
            "traffic", "fleets", "works", "price", "tech", "plague",
            "news", "pois", "ports", "nature" })
        {
            var entries = LegendQuery.For(key, GoodId.Provisions);
            Assert.True(entries.Count > 0, $"no legend for '{key}'");
            foreach (var e in entries)
                Assert.False(string.IsNullOrEmpty(e.Label),
                    $"unlabeled legend entry in '{key}'");
        }
        Assert.Empty(LegendQuery.For("starfield", GoodId.Provisions));
    }

    [Fact]
    public void ThePriceLegendIsShadeOfItself()
    {
        var entries = LegendQuery.For("price", GoodId.Alloys);
        Assert.Equal(7, entries.Count);   // the seven PriceGlyph bands
        // spot the edges: the same colors ShadeOf produces
        Assert.Equal(PriceLens.ShadeOf(0.1), entries[0].Color);
        Assert.Equal(PriceLens.ShadeOf(1.0), entries[2].Color);
        Assert.Equal(PriceLens.ShadeOf(999), entries[6].Color);
    }

    [Fact]
    public void RampLegendsSampleTheLensRamps()
    {
        var tension = LegendQuery.For("tension", GoodId.Provisions);
        Assert.Contains(tension, e => e.Color == TensionLens.HeatColor(0.0));
        Assert.Contains(tension, e => e.Color == TensionLens.HeatColor(1.0));
        var tech = LegendQuery.For("tech", GoodId.Provisions);
        Assert.Contains(tech, e => e.Color == TechLens.TierColor(0));
    }

    [Fact]
    public void GlyphLegendsCarryAtlasGlyphKeys()
    {
        var fleets = LegendQuery.For("fleets", GoodId.Provisions);
        Assert.Contains(fleets, e => e.GlyphKey == "FleetPatrol");
        Assert.Contains(fleets, e => e.GlyphKey == "FleetBlockade");
        var pois = LegendQuery.For("pois", GoodId.Provisions);
        Assert.Contains(pois, e => e.GlyphKey == "PoiBattlefield");
        var works = LegendQuery.For("works", GoodId.Provisions);
        Assert.Contains(works, e => e.GlyphKey == "WorkSite");
        Assert.Contains(works, e => e.GlyphKey == "WorkFreight");
    }
}
