namespace StarGen.Core.Epoch;

/// <summary>A loan object per economy/markets.md §Credit: (lender, borrower,
/// principal, rate, term). There are no banks as actors — lenders are
/// whoever holds surplus. Principal is the outstanding balance; interest and
/// repayment are conserved ledger moves in Allocation; unpayable obligations
/// trigger a default event. Registry in SimState.Loans, id order (P6).</summary>
public sealed class Loan
{
    public int Id { get; }
    public int LenderActorId { get; }
    public int BorrowerActorId { get; }
    /// <summary>Outstanding balance — drawn down by repayment, grown by
    /// capitalized interest.</summary>
    public double Principal { get; set; }
    public double RatePerYear { get; }
    public int TermYears { get; }
    public int IssuedYear { get; }
    /// <summary>Settled loans stay in the registry as history; the flag keeps
    /// iteration order stable (P6 — no removals).</summary>
    public bool Closed { get; set; }

    public Loan(int id, int lenderActorId, int borrowerActorId, double principal,
                double ratePerYear, int termYears, int issuedYear)
    {
        Id = id;
        LenderActorId = lenderActorId;
        BorrowerActorId = borrowerActorId;
        Principal = principal;
        RatePerYear = ratePerYear;
        TermYears = termYears;
        IssuedYear = issuedYear;
    }
}
