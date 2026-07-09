using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class PotentialsTests
{
    // Fixture cells: an ore belt, a garden world, a precursor graveyard.
    private static readonly CellFields Belt = new(
        MeanDensity: 0.5, Lean: StellarLean.Balanced, Metallicity: 0.9,
        HasMineralAnchor: true, HasPrecursorAnchor: false);
    private static readonly CellFields Garden = new(
        MeanDensity: 0.8, Lean: StellarLean.Balanced, Metallicity: 0.2,
        HasMineralAnchor: false, HasPrecursorAnchor: false);
    private static readonly CellFields Graveyard = new(
        MeanDensity: 0.2, Lean: StellarLean.RemnantGraveyard, Metallicity: 0.6,
        HasMineralAnchor: false, HasPrecursorAnchor: true);

    [Fact]
    public void AllPotentials_StayInUnitInterval_AcrossTheFieldSpace()
    {
        foreach (StellarLean lean in Enum.GetValues(typeof(StellarLean)))
            for (double d = 0; d <= 1.001; d += 0.25)
                for (double m = 0; m <= 1.001; m += 0.25)
                    foreach (bool mineral in new[] { false, true })
                        foreach (bool precursor in new[] { false, true })
                        {
                            var f = new CellFields(d, lean, m, mineral, precursor);
                            Assert.InRange(Potentials.Ore(f), 0.0, 1.0);
                            Assert.InRange(Potentials.Volatiles(f), 0.0, 1.0);
                            Assert.InRange(Potentials.Biosphere(f), 0.0, 1.0);
                            Assert.InRange(Potentials.Exotics(f), 0.0, 1.0);
                        }
    }

    [Fact]
    public void Ore_RootsInMetallicityAndBelts()
    {
        Assert.True(Potentials.Ore(Belt) > Potentials.Ore(Garden));
        var poor = Garden with { Metallicity = 0.1 };
        var rich = Garden with { Metallicity = 0.9 };
        Assert.True(Potentials.Ore(rich) > Potentials.Ore(poor));
        Assert.True(Potentials.Ore(Belt with { HasMineralAnchor = true })
                  > Potentials.Ore(Belt with { HasMineralAnchor = false }));
    }

    [Fact]
    public void Volatiles_FavorGasRichRegions()
    {
        var gasRich = new CellFields(0.8, StellarLean.YoungBright, 0.3, false, false);
        var sparseGraveyard = new CellFields(0.2, StellarLean.RemnantGraveyard, 0.3, false, false);
        Assert.True(Potentials.Volatiles(gasRich) > Potentials.Volatiles(sparseGraveyard));
    }

    [Fact]
    public void Biosphere_LovesGardens_ShunsGraveyards()
    {
        Assert.True(Potentials.Biosphere(Garden) > Potentials.Biosphere(Belt));
        Assert.True(Potentials.Biosphere(Graveyard) < 0.1);
    }

    [Fact]
    public void Exotics_ConcentrateAtPrecursorSites()
    {
        Assert.True(Potentials.Exotics(Graveyard) >= 0.7, "anchored site must be rich");
        Assert.True(Potentials.Exotics(Garden) <= 0.2, "unanchored cells stay scarce by design");
    }

    [Fact]
    public void RawGrade_RichCellsYieldBetterNotJustMore()
    {
        Assert.True(Potentials.RawGrade(0.9) > Potentials.RawGrade(0.2));
        Assert.InRange(Potentials.RawGrade(0.0), 0.0, 1.0);
        Assert.InRange(Potentials.RawGrade(1.0), 0.0, 1.0);
        // a rich source still doesn't reach precursor grades on its own
        Assert.True(Potentials.RawGrade(1.0) < Grades.PrecursorFloor);
    }

    [Fact]
    public void EmbodimentAffinity_MirrorsTheDesignsSpeciesTerrain()
    {
        var youngBright = Garden with { Lean = StellarLean.YoungBright };
        var oldDim = Garden with { Lean = StellarLean.OldDim };
        // cryophiles prefer old dim regions; aquatics the young bright ones
        Assert.True(Potentials.EmbodimentAffinity(Embodiment.Cryophilic, oldDim)
                  > Potentials.EmbodimentAffinity(Embodiment.Cryophilic, youngBright));
        Assert.True(Potentials.EmbodimentAffinity(Embodiment.Aquatic, youngBright)
                  > Potentials.EmbodimentAffinity(Embodiment.Aquatic, oldDim));
        // lithics track metallicity
        Assert.True(Potentials.EmbodimentAffinity(Embodiment.Lithic, Belt)
                  > Potentials.EmbodimentAffinity(Embodiment.Lithic, Garden));
        // machines tolerate everything equally
        Assert.Equal(Potentials.EmbodimentAffinity(Embodiment.Machine, Graveyard),
                     Potentials.EmbodimentAffinity(Embodiment.Machine, Garden), 10);
    }

    [Fact]
    public void Siting_BeltsWantMines_GardensWantAgri()
    {
        var belt = new CellSite(Belt, Connectivity: 0.3, IsPortHeart: false,
                                PortTier: 0, DevelopmentTier: 0, IsChokepoint: false);
        var garden = belt with { Fields = Garden };
        Assert.True(Siting.Score(InfraTypeId.Mine, belt, Embodiment.TerranAnalog)
                  > Siting.Score(InfraTypeId.AgriComplex, belt, Embodiment.TerranAnalog));
        Assert.True(Siting.Score(InfraTypeId.AgriComplex, garden, Embodiment.TerranAnalog)
                  > Siting.Score(InfraTypeId.Mine, garden, Embodiment.TerranAnalog));
        Assert.True(Siting.Score(InfraTypeId.Mine, belt, Embodiment.TerranAnalog)
                  > Siting.Score(InfraTypeId.Mine, garden, Embodiment.TerranAnalog));
    }

    [Fact]
    public void Siting_PortHeartsPullProcessingAndYards()
    {
        var wilds = new CellSite(Garden, 0.2, false, 0, 0, false);
        var heart = new CellSite(Garden, 0.8, true, 2, 2, false);
        foreach (var type in new[] { InfraTypeId.Fabricator, InfraTypeId.Shipyard,
                                     InfraTypeId.Refinery, InfraTypeId.Depot })
            Assert.True(Siting.Score(type, heart, Embodiment.TerranAnalog)
                      > Siting.Score(type, wilds, Embodiment.TerranAnalog),
                $"{type} wants the port heart");
    }

    [Fact]
    public void Siting_FortressesWantChokepointsAndApproaches()
    {
        var open = new CellSite(Garden, 0.5, false, 0, 1, IsChokepoint: false);
        var choke = open with { IsChokepoint = true };
        Assert.True(Siting.Score(InfraTypeId.Fortress, choke, Embodiment.TerranAnalog)
                  > Siting.Score(InfraTypeId.Fortress, open, Embodiment.TerranAnalog));
    }

    [Fact]
    public void Siting_ExcavationOnlyPaysAtAnchors()
    {
        var anchored = new CellSite(Graveyard, 0.2, false, 0, 0, false);
        var empty = new CellSite(Garden, 0.2, false, 0, 0, false);
        Assert.True(Siting.Score(InfraTypeId.ExcavationSite, anchored, Embodiment.TerranAnalog) >= 0.7);
        Assert.True(Siting.Score(InfraTypeId.ExcavationSite, empty, Embodiment.TerranAnalog) <= 0.2);
    }

    [Fact]
    public void Siting_AllScoresStayInUnitInterval()
    {
        var extremes = new[]
        {
            new CellSite(new CellFields(1, StellarLean.Balanced, 1, true, true), 1, true, 3, 3, true),
            new CellSite(new CellFields(0, StellarLean.RemnantGraveyard, 0, false, false), 0, false, 0, 0, false),
        };
        foreach (var site in extremes)
            foreach (InfraTypeId type in Enum.GetValues(typeof(InfraTypeId)))
                foreach (Embodiment e in Enum.GetValues(typeof(Embodiment)))
                    Assert.InRange(Siting.Score(type, site, e), 0.0, 1.0);
    }
}
