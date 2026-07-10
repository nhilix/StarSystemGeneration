using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>One market per port (substrate/market-geography.md): where the
/// port's service area meets the lane network. State per good: price,
/// inventory carried between steps (unsold stock — gluts are visible state),
/// mean grade of that inventory, last-cleared quantity, and the black book
/// for goods prohibited under local law (thin volume, high margins).
/// Registry in SimState.Markets, parallel to Ports (market id = port id),
/// created whenever a port is established. Iteration is port-id then good-id
/// order everywhere (P6); the economy is roll-free.</summary>
public sealed class Market
{
    public int PortId { get; }
    /// <summary>Persistent per-good price — drifts toward clearing, never
    /// perfectly (economy/markets.md: persistent gradients ARE the trade
    /// opportunities).</summary>
    public double[] Price { get; }
    /// <summary>Unsold stock carried between steps, per good.</summary>
    public double[] Inventory { get; }
    /// <summary>Quantity-weighted mean grade of the inventory (0 when empty).</summary>
    public double[] InventoryGrade { get; }
    /// <summary>Quantity cleared (consumed) in the last market step.</summary>
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
        Inventory = new double[n];
        InventoryGrade = new double[n];
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

    /// <summary>Blend a delivery into the inventory: quantities add, grade is
    /// the quantity-weighted mean (commodities.md: no FIFO bookkeeping).</summary>
    public void Deposit(int good, double quantity, double grade)
    {
        if (quantity <= 0) return;
        double total = Inventory[good] + quantity;
        InventoryGrade[good] =
            (Inventory[good] * InventoryGrade[good] + quantity * grade) / total;
        Inventory[good] = total;
    }

    /// <summary>Draw up to <paramref name="quantity"/> from the inventory at
    /// the mean grade; returns the quantity actually drawn.</summary>
    public double Draw(int good, double quantity)
    {
        double drawn = quantity < Inventory[good] ? quantity : Inventory[good];
        if (drawn <= 0) return 0;
        Inventory[good] -= drawn;
        if (Inventory[good] <= 0) { Inventory[good] = 0; InventoryGrade[good] = 0; }
        return drawn;
    }
}
