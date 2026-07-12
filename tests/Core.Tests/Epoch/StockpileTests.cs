using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Stage 2 (spec §4b): stock has an address — stockpiles live per
/// port, per good, banked; ownership is the port's owner, so conquest,
/// federation, and schism move stock by moving the port.</summary>
public class StockpileTests
{
    [Fact]
    public void Port_CarriesALocatedStockpile_DepositBlendsAndDrawDepletes()
    {
        var port = new Port(0, 0, new HexCoordinate(0, 0), tier: 1,
                            foundedYear: 0);
        Assert.Equal(Goods.All.Count, port.StockQty.Length);
        Assert.Equal(Goods.All.Count, port.StockGrade.Length);
        int g = (int)GoodId.Provisions;
        port.DepositStock(g, 10, 0.8);
        port.DepositStock(g, 10, 0.4);           // blend, not replace
        Assert.Equal(20.0, port.StockQty[g], 6);
        Assert.Equal(0.6, port.StockGrade[g], 6);
        double drawn = port.DrawStock(g, 15);
        Assert.Equal(15.0, drawn, 6);
        Assert.Equal(5.0, port.StockQty[g], 6);
        Assert.Equal(0.6, port.StockGrade[g], 6); // grade survives a partial draw
        Assert.Equal(5.0, port.DrawStock(g, 99), 6);  // clamps to what's there
        Assert.Equal(0.0, port.StockGrade[g], 6);     // empty zeroes the grade
    }
}
