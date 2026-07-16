namespace StarGen.Core.Epoch;

/// <summary>One Currency's central bank — a plain state record (NOT an actor,
/// no controller, not an <see cref="ICreditLedger"/>): founded 1:1 alongside
/// its <see cref="Currency"/> at <see cref="SimState.FoundCurrency"/>, the
/// same single chokepoint every currency mints through, and keyed by currency
/// id in the dense-parallel <see cref="SimState.Banks"/> registry (slice CU-2
/// bank-actor design). This task only declares the record; reserve dynamics
/// (spread intake, reserve funding) are wired by later tasks — for now
/// <see cref="Reserve"/> starts and stays at 0 for every newly founded bank.</summary>
public sealed class Bank
{
    public int CurrencyId { get; }
    public double Reserve { get; set; }
    /// <summary>Running total of spread income the bank has ever taken in —
    /// a level across the whole sim, never reset per epoch (later task).</summary>
    public double CumulativeSpreadIntake { get; set; }
    /// <summary>Running total the bank has ever funded out of its reserve —
    /// a level across the whole sim, never reset per epoch (later task).</summary>
    public double CumulativeReserveFunded { get; set; }

    public Bank(int currencyId)
    {
        CurrencyId = currencyId;
    }
}
