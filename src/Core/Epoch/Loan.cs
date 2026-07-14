namespace StarGen.Core.Epoch;

/// <summary>A loan object per economy/markets.md §Credit: (lender, borrower,
/// principal, rate, term). There are no banks as actors — lenders are
/// whoever holds surplus. Principal is the outstanding balance; interest and
/// repayment are conserved ledger moves in Allocation; unpayable obligations
/// trigger a default event. Registry in SimState.Loans, id order (P6).</summary>
public sealed class Loan
{
    public int Id { get; }
    /// <summary>The creditor. Set once at issue; reassigned only when a dead
    /// lender's estate transfers the claim to its successor (fix wave 1) —
    /// the same estate-sweep move that reassigns resting sells and shipments.</summary>
    public int LenderActorId { get; set; }
    public int BorrowerActorId { get; }
    /// <summary>Outstanding balance — drawn down by repayment, grown by
    /// capitalized interest.</summary>
    public double Principal { get; set; }
    /// <summary>The principal at issue — the fixed reference the capitalization
    /// ceiling measures against. Never mutated after construction: a loan whose
    /// live Principal has capitalized past a bounded multiple of this is forced
    /// to default rather than compounding without limit.</summary>
    public double OriginalPrincipal { get; }
    public double RatePerYear { get; }
    public int TermYears { get; }
    public int IssuedYear { get; }
    /// <summary>Settled loans stay in the registry as history; the flag keeps
    /// iteration order stable (P6 — no removals).</summary>
    public bool Closed { get; set; }

    public Loan(int id, int lenderActorId, int borrowerActorId, double principal,
                double ratePerYear, int termYears, int issuedYear,
                double? originalPrincipal = null)
    {
        Id = id;
        LenderActorId = lenderActorId;
        BorrowerActorId = borrowerActorId;
        Principal = principal;
        OriginalPrincipal = originalPrincipal ?? principal;
        RatePerYear = ratePerYear;
        TermYears = termYears;
        IssuedYear = issuedYear;
    }
}
