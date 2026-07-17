namespace StarGen.Core.Epoch;

/// <summary>One polity's money (currency-and-FX design, slice CU-1). Every
/// living polity mints exactly one currency; corporations hold wallets across
/// many (<see cref="Corporation.Holdings"/>). Registry in
/// <see cref="SimState.Currencies"/>, id order (P6). A conversion is a transfer
/// between two currencies' supplies, never a mint — the paired
/// <see cref="CumulativeConvertedIn"/>/<see cref="CumulativeConvertedOut"/>
/// counters let the per-currency conservation residual net transfers out
/// (wired in the conservation task; this slice only declares the fields).</summary>
public sealed class Currency
{
    public int Id { get; }
    public string Name { get; }
    /// <summary>The polity that minted this currency at founding; the currency
    /// outlives it as a <see cref="Retired"/> record once the polity dies.</summary>
    public int FoundingPolityId { get; }
    /// <summary>This currency's own circulating money supply — the per-currency
    /// analogue of the old single galaxy-wide total.</summary>
    public double Supply { get; set; }
    /// <summary>Running total minted into this currency by bounded sovereign
    /// issuance (per-currency mirror of the monetary-equilibrium field).</summary>
    public double CumulativeFiatIssued { get; set; }
    /// <summary>Running total minted into this currency by the always-on steady
    /// issuance channel (per-currency mirror).</summary>
    public double CumulativeSteadyIssuance { get; set; }
    /// <summary>Value ever converted INTO this currency (signed pair with
    /// <see cref="CumulativeConvertedOut"/>) — the conservation residual nets
    /// the two so a conversion counts as neither mint nor loss.</summary>
    public double CumulativeConvertedIn { get; set; }
    /// <summary>Value ever converted OUT of this currency.</summary>
    public double CumulativeConvertedOut { get; set; }
    /// <summary>Running total destroyed when the polity repays its bank's
    /// <see cref="Bank.ClaimOnState"/> principal (slice BF bank-flow design)
    /// — the sim's first monetary sink. Unlike every other cumulative counter
    /// here, this is the <b>negative</b> term in the per-currency conservation
    /// identity:
    /// <c>Supply + Reserve == endowment + CumulativeFiatIssued +
    /// CumulativeSteadyIssuance + CumulativeConvertedIn − CumulativeConvertedOut
    /// − CumulativeFiatRetired</c> (later task wires the servicing pass that
    /// grows it).</summary>
    public double CumulativeFiatRetired { get; set; }
    /// <summary>This currency's value in the shared synthetic numeraire unit —
    /// converting between A and B is amount × A.NumeraireRate / B.NumeraireRate
    /// (O(N) state, not an N×N table). Starts at 1.0 at founding; recomputed
    /// once per epoch from prior supply/output (the FX-rate task).</summary>
    public double NumeraireRate { get; set; } = 1.0;
    /// <summary>True once the issuing polity is absorbed or dies — the record
    /// persists (history, dangling holdings) but no new money mints into it.</summary>
    public bool Retired { get; set; }

    public Currency(int id, string name, int foundingPolityId)
    {
        Id = id;
        Name = name;
        FoundingPolityId = foundingPolityId;
    }
}
