using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>One market per port (substrate/market-geography.md): where the
/// port's service area meets the lane network. THE MARKET IS THE ORDER
/// BOOK (contract-economy spec §2): goods for sale live on named sellers'
/// orders in SimState.Orders — the anonymous shelf is dead. What remains
/// here per good: the reference price (drifts rate-limited on book
/// imbalance in MatchAndClear — the readout every valuation keeps
/// reading), the last-cleared quantity, and the black book for goods
/// prohibited under local law.
/// Registry in SimState.Markets, parallel to Ports (market id = port id).
/// Iteration is port-id then good-id order everywhere (P6); matching is
/// roll-free.</summary>
public sealed class Market
{
    public int PortId { get; }
    /// <summary>The reference price per good: yesterday's value moved by
    /// the rate-clamped imbalance drift (posted bids + consumption signal
    /// vs resting asks, snapshotted pre-match).</summary>
    public double[] Price { get; }
    /// <summary>Quantity cleared (traded) in the last market step.</summary>
    public double[] LastCleared { get; }
    /// <summary>Unmet demand for locally prohibited goods — smuggler-supplied
    /// volume lands here when smuggling mechanics arrive (H); until then the
    /// black book records the converted demand and its shadow price.</summary>
    public double[] BlackBookDemand { get; }
    /// <summary>Shadow price of prohibited demand — high-margin by design.</summary>
    public double[] BlackBookPrice { get; }

    public Market(int portId, EconomyKnobs eco)
    {
        PortId = portId;
        int n = Goods.All.Count;
        Price = new double[n];
        LastCleared = new double[n];
        BlackBookDemand = new double[n];
        BlackBookPrice = new double[n];
        for (int g = 0; g < n; g++)
            Price[g] = InitialPrice(eco, (GoodId)g);
    }

    /// <summary>Founding price by good tier — value tracks chain depth until
    /// clearing history says otherwise.</summary>
    public static double InitialPrice(EconomyKnobs eco, GoodId good) =>
        Goods.Get(good).Tier switch
        {
            GoodTier.Raw => eco.BasePriceRaw,
            GoodTier.Processed => eco.BasePriceProcessed,
            _ => eco.BasePriceCapital,
        };
}
