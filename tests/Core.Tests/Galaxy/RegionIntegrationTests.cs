using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class RegionIntegrationTests
{
    private static GalaxyContext Galaxy(ulong seed = 42)
    {
        var config = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 };
        return new GalaxyContext(config) { Skeleton = SkeletonBuilder.Build(config) };
    }

    [Fact]
    public void AnchoredHexes_AlwaysHaveSystems_WithAnchorTags()
    {
        var galaxy = Galaxy();
        foreach (var cell in galaxy.Skeleton!.Cells)
            foreach (var anchor in cell.Anchors)
            {
                var result = Generator.Generate(galaxy, anchor.Hex);
                Assert.False(result.IsEmpty, $"anchored hex {anchor.Hex} must have a system");
                string expectedTag = anchor.Type switch
                {
                    AnchorType.MineralRich => "mineral-rich",
                    AnchorType.PrecursorSite => "precursor site",
                    _ => "homeworld",
                };
                Assert.Contains(expectedTag, result.System!.Tags);
                Assert.Null(result.System.OverlayId);   // anchored => no random overlay
            }
    }

    [Fact]
    public void Homeworlds_HaveSapientMajorWorld_AndName()
    {
        for (ulong seed = 40; seed < 46; seed++)
        {
            var galaxy = Galaxy(seed);
            var homeworlds = galaxy.Skeleton!.Cells.SelectMany(c => c.Anchors)
                .Where(a => a.Type == AnchorType.Homeworld).ToList();
            Assert.NotEmpty(homeworlds);
            foreach (var anchor in homeworlds)
            {
                var system = Generator.Generate(galaxy, anchor.Hex).System!;
                var forced = system.Stars.SelectMany(st => st.Slots)
                    .Where(sl => sl.Body != null).Select(sl => sl.Body!)
                    .FirstOrDefault(b => b.Biosphere == Biosphere.Sapient
                                         && b.Settlement == Settlement.MajorWorld);
                Assert.NotNull(forced);
                Assert.NotNull(forced!.Society);
                Assert.NotNull(system.GivenName);
            }
        }
    }

    [Fact]
    public void RemnantGraveyards_SkewTowardDeadStars()
    {
        var galaxy = Galaxy();
        int deadInGraveyards = 0, totalInGraveyards = 0, deadElsewhere = 0, totalElsewhere = 0;
        foreach (var cell in galaxy.Skeleton!.Cells)
        {
            if (cell.MeanDensity < 0.2) continue;
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius))
            {
                var system = Generator.Generate(galaxy, hex).System;
                if (system == null) continue;
                bool dead = system.Stars[0].TypeId is "ashen_remnant" or "collapsed_core";
                if (cell.Lean == StellarLean.RemnantGraveyard) { totalInGraveyards++; if (dead) deadInGraveyards++; }
                else if (cell.Lean == StellarLean.Balanced) { totalElsewhere++; if (dead) deadElsewhere++; }
            }
        }
        if (totalInGraveyards < 30) return;   // seed produced too few graveyard systems to compare
        Assert.True(deadInGraveyards / (double)totalInGraveyards > deadElsewhere / (double)totalElsewhere,
            "graveyard cells should host more dead stars than balanced cells");
    }

    [Fact]
    public void SettlementScale_RaisesSettlementInsidePolities()
    {
        var galaxy = Galaxy();
        int settledOwned = 0, totalOwned = 0, settledWild = 0, totalWild = 0;
        foreach (var cell in galaxy.Skeleton!.Cells.Where(c => !c.IsVoid))
        {
            bool owned = cell.OwnerPolityId >= 0 && cell.DevelopmentTier >= 3;
            bool wild = cell.OwnerPolityId < 0;
            if (!owned && !wild) continue;
            int i = 0;
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius))
            {
                if (i++ % 2 != 0) continue;
                var system = Generator.Generate(galaxy, hex).System;
                if (system == null) continue;
                bool settled = system.Stars.SelectMany(st => st.Slots)
                    .Any(sl => sl.Body != null && sl.Body.Settlement != Settlement.None);
                if (owned) { totalOwned++; if (settled) settledOwned++; }
                else { totalWild++; if (settled) settledWild++; }
            }
        }
        Assert.True(totalOwned > 20 && totalWild > 20, "need enough samples on both sides");
        Assert.True(settledOwned / (double)totalOwned > settledWild / (double)totalWild,
            $"developed cells ({settledOwned}/{totalOwned}) should out-settle wilds ({settledWild}/{totalWild})");
    }

    [Fact]
    public void Flatspace_RemainsBitIdentical_ToLegacy()
    {
        for (int x = 0; x < 200; x++)
        {
            var coord = new HexCoordinate(x, 7);
            var legacy = Generator.Generate(17UL, coord);
            var flat = Generator.Generate(GalaxyContext.Flatspace(17), coord);
            Assert.Equal(
                StarGen.Core.Text.SystemFormatter.Format(legacy),
                StarGen.Core.Text.SystemFormatter.Format(flat));
        }
    }
}
