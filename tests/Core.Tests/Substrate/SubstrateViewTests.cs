using System.Globalization;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class SubstrateViewTests
{
    [Fact]
    public void RenderGoods_NamesEveryGood()
    {
        string text = SubstrateView.RenderGoods();
        foreach (var g in Goods.All)
            Assert.Contains(g.Name, text);
    }

    [Fact]
    public void RenderInfra_NamesEveryFacilityType_AndTheSampleCells()
    {
        string text = SubstrateView.RenderInfra();
        foreach (var d in Infrastructure.All)
            Assert.Contains(d.Name, text);
        Assert.Contains("sample: ore belt", text);
        Assert.Contains("sample: garden world", text);
        Assert.Contains("sample: precursor graveyard", text);
    }

    [Fact]
    public void Render_IsCultureInvariant()
    {
        // slice-A lesson: sv-SE renders '-' as U+2212 under ICU
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string goods = SubstrateView.RenderGoods();
            string infra = SubstrateView.RenderInfra();
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Assert.Equal(goods, SubstrateView.RenderGoods());
            Assert.Equal(infra, SubstrateView.RenderInfra());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void RenderSite_ShowsPotentialsAndTopSites()
    {
        var belt = new CellFields(0.5, StellarLean.Balanced, 0.9, true, false);
        var site = new CellSite(belt, 0.3, false, 0, 0, false);
        string text = SubstrateView.RenderSite("cell [0000-0000]", belt, site, Embodiment.TerranAnalog);
        Assert.Contains("cell [0000-0000]", text);
        Assert.Contains("potentials:", text);
        Assert.Contains("siting:", text);
        Assert.Contains("Mine", text);   // the belt's obvious winner
    }
}
