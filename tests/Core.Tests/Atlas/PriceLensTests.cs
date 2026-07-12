using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The price lens — per-good price ratio vs founding price at the
/// nearest servicing port (emap price parity: '_' glut · '-' cheap · '='
/// par · '+' dear · '*' scarce · '#' spike · '!' famine), rendered as a
/// plane field over the service areas; the unserviced wilds stay clear.</summary>
public class PriceLensTests
{
    private static (AtlasReadModel Model, SimState State) WithMarket()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? hex = null;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            hex = HexGrid.CellCenter(cell.Coord);
            break;
        }
        state.Ports.Add(new Port(0, state.Actors[0].Id, hex!.Value, tier: 2,
                                 foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void AFreshMarketReadsParAtItsOwnPort()
    {
        var (model, state) = WithMarket();
        double ratio = PriceLens.RatioAt(model, EyeContext.God(state.WorldYear),
                                         state.Ports[0].Hex, GoodId.Provisions);
        Assert.Equal(1.0, ratio, precision: 9);
    }

    [Fact]
    public void TheRatioTracksTheLivePriceAgainstFounding()
    {
        var (model, state) = WithMarket();
        state.Markets[0].Price[(int)GoodId.Provisions] =
            2.0 * Market.InitialPrice(state.Config.Economy, GoodId.Provisions);
        double ratio = PriceLens.RatioAt(model, EyeContext.God(state.WorldYear),
                                         state.Ports[0].Hex, GoodId.Provisions);
        Assert.Equal(2.0, ratio, precision: 9);
    }

    [Fact]
    public void UnservicedWildsHaveNoPrice()
    {
        var (model, state) = WithMarket();
        // The farthest live cell from the only port — far outside tier-2 reach.
        HexCoordinate far = default;
        int best = -1;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            int d = HexGrid.Distance(HexGrid.CellCenter(cell.Coord),
                                     state.Ports[0].Hex);
            if (d > best) { best = d; far = HexGrid.CellCenter(cell.Coord); }
        }
        Assert.True(double.IsNaN(PriceLens.RatioAt(
            model, EyeContext.God(state.WorldYear), far, GoodId.Provisions)));
    }

    [Fact]
    public void CellShadesCoverServiceAreasAndClearTheWilds()
    {
        var (model, state) = WithMarket();
        var eye = EyeContext.God(state.WorldYear);
        var shades = PriceLens.CellShades(model, eye, GoodId.Provisions);
        Assert.Equal(model.Cells.Count, shades.Count);
        Assert.True(model.TryIndexOfCell(
            HexGrid.CellOf(state.Ports[0].Hex), out int portCell));
        Assert.NotEqual(AtlasPalette.Clear, shades[portCell]);
        int clear = 0;
        foreach (var s in shades) if (s == AtlasPalette.Clear) clear++;
        Assert.True(clear > 0, "somewhere the wilds carry no price");
    }

    [Fact]
    public void ShadesBandOnTheEmapThresholds()
    {
        // One shade per emap glyph band, distinct across every boundary.
        var glut = PriceLens.ShadeOf(0.2);
        var cheap = PriceLens.ShadeOf(0.4);
        var par = PriceLens.ShadeOf(1.0);
        var dear = PriceLens.ShadeOf(2.0);
        var scarce = PriceLens.ShadeOf(5.0);
        var spike = PriceLens.ShadeOf(20.0);
        var famine = PriceLens.ShadeOf(50.0);
        var all = new[] { glut, cheap, par, dear, scarce, spike, famine };
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                Assert.NotEqual(all[i], all[j]);
        // Par sits quiet; the extremes shout.
        Assert.True(par.A < glut.A && par.A < famine.A);
        // Same band, same shade — banded like the glyphs, not a smear.
        Assert.Equal(dear, PriceLens.ShadeOf(2.9));
    }
}
