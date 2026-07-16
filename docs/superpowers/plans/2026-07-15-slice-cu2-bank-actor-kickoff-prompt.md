# Slice CU-2 kickoff — the bank actor

You are opening the direct follow-on to Slice CU-1 (currency & FX
foundation, merged to main 2026-07-15). CU-1 gave every living polity its
own `Currency` with a live FX rate and a working conversion primitive, but
left "who manages exchange" as a bare function call
(`SimState.ConvertCurrency`/`CreditLocal`/`DebitLocal`) with no owning
actor. This session's job is CU-2: a first-class Bank actor that takes
over that role. Read first, don't skip to design.

## Read first, in order

1. `docs/HANDOFF.md` §"Slice CU-1 — currency & FX foundation (closed)" —
   the full mechanism as it shipped, including the review chain's real
   findings (several bugs found only at real acceptance-sweep scale) and
   the exact follow-ups filed. This is your problem statement; don't
   re-derive it from scratch.
2. `docs/superpowers/specs/2026-07-14-cu-currency-fx-design.md` §"Forward
   roadmap: CU-2 through CU-4" — the shape this slice was scoped at when
   CU-1 was designed, including the open questions CU-1 explicitly left
   for this session: is a Bank created 1:1 with each `Currency` at
   founding, or does it need its own separate founding condition; what
   does "regulation" concretely gate (issuance caps? conversion fees/
   spreads?); how banks interact with existing polity AI/behavior.
3. Code — the real surface this slice attaches to:
   `src/Core/Epoch/Currency.cs` (the record a Bank attaches to — `Id`,
   `Supply`, `NumeraireRate`, `CumulativeFiatIssued`/`SteadyIssuance`/
   `ConvertedIn`/`ConvertedOut`, `Retired`); `src/Core/Epoch/FxOps.cs`
   (`RecomputeRates` — the per-epoch rate formula this slice's "regulation"
   question is really asking whether/how to influence); `src/Core/Epoch/
   SimState.cs` (`ConvertCurrency`, `CreditLocal`/`DebitLocal`,
   `RecordConversion` — the primitive whose ownership question this slice
   answers); `src/Core/Epoch/Interior/Corporation.cs` (`Holdings`/
   `Deposit`/`Withdraw` — a Bank is a strong candidate to be modeled as
   something adjacent to this pattern, or genuinely different — that's a
   real design fork, not assumed); `src/Core/Epoch/Phases.cs` (`Borrow`/
   `ServiceLoans` — the sovereign issuance mechanism `IssueSovereignCredit`
   and the steady-issuance term both mint into a polity's own `Currency`
   today with no Bank in the loop at all; this slice's "minting/regulation
   authority" question is whether/how a Bank inserts itself here).
4. `docs/superpowers/plans/2026-07-14-slice-cu-ledger.md` — skim for the
   task-by-task shape this project's slice-session convention actually
   produces (12 planned tasks grew to 15 real tasks across this slice,
   several inserted mid-session by real findings — expect the same kind of
   organic growth here, don't be surprised by it).

## The open design question — don't re-derive it, weigh it

CU-1 built the mechanism (currencies, rates, conversion) but deliberately
left "who decides monetary policy" unowned — `IssueSovereignCredit` and the
steady-issuance term are still called from inside `AllocationPhase.Run`'s
per-polity loop, minting directly into that polity's own `Currency`, with
no institution between the polity's fiscal need and the currency it
controls. This is the same critique that opened Slice CU in the first
place (the Eurozone/EVE-ISK precedent: minting authority belongs to ONE
institution, decoupled from any individual actor's own need for money) —
CU-1 answered it at the CURRENCY level (each polity's own currency, not a
shared one), but left the MINTING-AUTHORITY question fully open within a
single currency. A Bank is the natural next step: an actor that sits
between a polity and its own currency's supply, potentially with its own
behavior/personality, its own solvency, its own relationship to the
polity that "hosts" it (mirroring how a `Corporation` relates to its
`HostPolityId` today, or genuinely not — that's a real fork to weigh, not
assume).

## Phase 1 — brainstorm (this session's primary deliverable, before any code)

Unlike CU-1, this session does NOT need a fresh multi-thread research
pass — CU-1's four research docs already covered real-world currency-union
precedent, game precedent, and genre precedent in depth, and remain valid
evidence for this session's questions. Use `superpowers:brainstorming`
directly: present the real open questions (Bank founding condition, what
regulation concretely gates, Bank-vs-polity relationship, Bank-vs-
corporation architectural kinship or divergence, whether a Bank has agency/
behavior of its own or is a passive rule-holder) to the user one at a time
with trade-offs grounded in CU-1's actual shipped code, and do not assume
any answer the earlier research merely sketched as a candidate.

## Boundary — NOT this session unless the brainstorm changes the picture

- **CU-3** (federation-triggered currency consolidation — replacing CU-1's
  blunt forced-conversion-at-absorption stub with a real mechanic once
  banks exist to be party to a merger) and **CU-4** (bank/currency-union
  strength feeding federation generation) are explicitly later slices in
  this same chain — do not design them now, even if the brainstorm makes
  their shape tempting to sketch. Note where CU-2's design decisions
  constrain them, nothing more.
- CU-1's filed follow-ups (corp bankruptcy near-unreachable, the sub-1e-12
  dust sinks, the conservation tolerance now being relative rather than
  absolute, the three known scope-boundary gaps needing consolidated
  documentation, `Segment.MeanSoL` still below the healthy floor) are real
  but separate — note them if this slice's work happens to touch the same
  code, don't go looking for them.
- Slice L2 (population/off-lane) and Slice K6 (economy surfaces) are
  parallel, worktree-isolated tracks — this project now has multiple
  concurrent slice lines again; none should share a checkout.

## Traps carried from CU-1 (beyond the K4/K5/SH/L lists already in
HANDOFF.md)

- **The real acceptance instrument for anything conservation/invariant-
  sensitive is the full committed multi-seed sweep, not a single seed's
  unit tests.** CU-1 shipped what looked like clean conservation after
  Task 9's seed-42-only tests passed, and the real 32-run sweep
  immediately found a leak 5-9 orders of magnitude over tolerance that
  those tests never could have caught. If this slice mints/converts money
  in any new way (a Bank issuing or regulating currency is exactly that),
  run the sweep — more than once, and specifically after anything that
  touches `IssueSovereignCredit`/the steady-issuance term/`ConvertCurrency`
  — before declaring conservation settled.
- **A subagent's "pre-existing, out of scope" diagnosis needs independent
  verification, not trust.** CU-1's Task 7 misdiagnosed a real in-slice
  regression as inherited work; a reviewer disproved it by bisecting
  commit-by-commit. If a bug looks inherited, prove it (check out the
  actual pre-slice commit and reproduce), don't take the claim at face
  value.
- **`Corporation.Holdings`' no-overdraft `Withdraw` cap silently drops the
  shortfall if a caller doesn't check the return value** — this caused two
  separate real leaks in CU-1 (Task 6b, Task 7b) before the pattern was
  understood. If a Bank ends up implementing anything similar (a reserve
  that can run dry), design the caller contract explicitly from the start.
- `git log main` before any merge-out — other slice lines may move `main`
  mid-session, same as CU-1 itself had to merge Slice L in before its own
  merge-out.
