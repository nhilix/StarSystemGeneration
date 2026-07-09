using System;
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class GoodsCatalogTests
{
    [Fact]
    public void Catalog_HasSeventeenGoods_IdsStableAndUnique()
    {
        Assert.Equal(17, Goods.All.Count);
        Assert.Equal(17, Goods.All.Select(g => g.Id).Distinct().Count());
        // Frozen ids — these are the meaning of every goods-keyed int in the
        // controller contract. Renumbering is a breaking change by definition.
        Assert.Equal(0, (int)GoodId.Provisions);
        Assert.Equal(1, (int)GoodId.Ore);
        Assert.Equal(2, (int)GoodId.Volatiles);
        Assert.Equal(3, (int)GoodId.Organics);
        Assert.Equal(4, (int)GoodId.Exotics);
        Assert.Equal(5, (int)GoodId.Alloys);
        Assert.Equal(6, (int)GoodId.Fuel);
        Assert.Equal(7, (int)GoodId.Composites);
        Assert.Equal(8, (int)GoodId.ConsumerGoods);
        Assert.Equal(9, (int)GoodId.Medicine);
        Assert.Equal(10, (int)GoodId.Narcotics);
        Assert.Equal(11, (int)GoodId.RefinedExotics);
        Assert.Equal(12, (int)GoodId.Machinery);
        Assert.Equal(13, (int)GoodId.ShipComponents);
        Assert.Equal(14, (int)GoodId.Armaments);
        Assert.Equal(15, (int)GoodId.Compute);
        Assert.Equal(16, (int)GoodId.Luxuries);
        // Get is id-indexed
        foreach (var def in Goods.All)
            Assert.Same(def, Goods.Get(def.Id));
    }

    [Fact]
    public void Tiers_MatchDesignTables()
    {
        Assert.Equal(5, Goods.All.Count(g => g.Tier == GoodTier.Raw));
        Assert.Equal(7, Goods.All.Count(g => g.Tier == GoodTier.Processed));
        Assert.Equal(5, Goods.All.Count(g => g.Tier == GoodTier.Capital));
        Assert.Equal(GoodTier.Raw, Goods.Get(GoodId.Exotics).Tier);
        Assert.Equal(GoodTier.Processed, Goods.Get(GoodId.Fuel).Tier);
        Assert.Equal(GoodTier.Capital, Goods.Get(GoodId.Compute).Tier);
    }

    [Fact]
    public void RawGoods_AreRecipeFree_OthersHaveRecipes()
    {
        foreach (var g in Goods.All)
        {
            if (g.Tier == GoodTier.Raw) Assert.Empty(g.Recipes);
            else Assert.NotEmpty(g.Recipes);
        }
    }

    [Fact]
    public void RecipeClosure_EveryInputIsACatalogGood_WithPositiveQuantity()
    {
        foreach (var g in Goods.All)
            foreach (var r in g.Recipes)
            {
                Assert.Equal(g.Id, r.Output);
                Assert.NotEmpty(r.Inputs);
                foreach (var input in r.Inputs)
                {
                    Assert.True(Enum.IsDefined(typeof(GoodId), input.Good),
                        $"{g.Name}: input {input.Good} not in catalog");
                    Assert.True(input.Quantity > 0);
                }
                Assert.InRange(r.GradeBase, 0.0, 1.0);
                Assert.InRange(r.MinTechTier, 1, 3);
            }
    }

    [Fact]
    public void Variants_StandardExoticsFree_AdvancedExoticsGated()
    {
        // "the standard variant is exotics-free ... the advanced variant is
        //  exotics-gated (higher grade base, more effect per unit)" — only
        //  where a good lists variants (commodities.md).
        var exoticsLineage = new[] { GoodId.Exotics, GoodId.RefinedExotics };
        foreach (var g in Goods.All.Where(g => g.Recipes.Any(r => r.Kind == RecipeKind.Advanced)))
        {
            var std = g.Recipes.Where(r => r.Kind == RecipeKind.Standard).ToList();
            var adv = g.Recipes.Where(r => r.Kind == RecipeKind.Advanced).ToList();
            Assert.NotEmpty(std);
            foreach (var r in std)
                Assert.DoesNotContain(r.Inputs, i => exoticsLineage.Contains(i.Good));
            foreach (var r in adv)
            {
                Assert.Contains(r.Inputs, i => exoticsLineage.Contains(i.Good));
                Assert.True(r.GradeBase > std.Max(s => s.GradeBase),
                    $"{g.Name}: advanced grade base must exceed standard");
                Assert.True(r.MinTechTier > std.Min(s => s.MinTechTier),
                    $"{g.Name}: advanced variant must be tech-gated above standard");
            }
        }
        // The designed advanced variants exist where the doc lists them.
        foreach (var id in new[] { GoodId.Medicine, GoodId.Machinery,
                                   GoodId.ShipComponents, GoodId.Armaments })
            Assert.Contains(Goods.Get(id).Recipes, r => r.Kind == RecipeKind.Advanced);
    }

    [Fact]
    public void RecipeGraph_Acyclic_DeepestChainIsShipComponentsAtFourNodes()
    {
        // "chains 1–4 nodes deep"; deepest: Ore→Alloys→Machinery→Components.
        var depths = new Dictionary<GoodId, int>();
        int Depth(GoodId id)
        {
            if (depths.TryGetValue(id, out var d)) return d;
            depths[id] = -1;   // cycle sentinel
            var def = Goods.Get(id);
            int result = def.Recipes.Count == 0
                ? 1
                : 1 + def.Recipes.Max(r => r.Inputs.Max(i =>
                    {
                        int di = Depth(i.Good);
                        Assert.True(di > 0, $"recipe cycle through {i.Good}");
                        return di;
                    }));
            depths[id] = result;
            return result;
        }
        foreach (var g in Goods.All) Assert.InRange(Depth(g.Id), 1, 4);
        Assert.Equal(4, Depth(GoodId.ShipComponents));
        Assert.Equal(4, Goods.All.Max(g => Depth(g.Id)));
    }

    [Fact]
    public void LegalitySchema_DefaultsLegalUntaxed()
    {
        var l = GoodLegality.Default;
        Assert.Equal(StarGen.Core.Epoch.LegalityLevel.Legal, l.Level);
        Assert.Equal(0.0, l.Tariff);
    }
}
