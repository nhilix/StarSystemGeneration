# Slice ME task ledger — monetary equilibrium

Executed via subagent-driven-development in the `slice-me-monetary` worktree
(`.worktrees/slice-me-monetary`). Design doc:
`docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md` — read
for rationale; this ledger carries the exact values and file-level
instructions each task needs to implement without re-deriving them.

## Global constraints (bind every task)

- TDD: write/extend a failing test before the implementation change; run
  `dotnet test StarSystemGeneration.sln` (from the worktree root) green
  before committing.
- Determinism: no wall-clock, no unseeded randomness, no dictionary/hash-set
  iteration where order isn't already stably keyed. Fixed iteration order
  (actor-id / id order) matches the surrounding code's existing pattern —
  copy it, don't invent a new one.
- Conservation (P4): every new credit flow must be a symmetric transfer
  (subtract from one holder class, add the same amount to another) unless
  the task is explicitly the sovereign-issuance mint (Task 3) — that one
  is a deliberate, tracked exception, finished by Task 6 in the same
  ledger, not left dangling.
- No comments beyond WHY (hidden constraint, subtle invariant, workaround).
  Match the file's existing comment density and tone — most of these files
  use one doc-comment per method, not inline chatter.
- Commit frequently (one commit per coherent change within the task is
  fine); do not squash into one giant commit.
- Do not touch `unity/ProjectSettings/*` or anything under `runs/`.

## Task 1: `BudgetWeights.Operations` — the declared operating margin

**Context**: `PolityPolicies.Default.Budget`'s six shares
(`Development .30, Military .20, Research .15, Expansion .20, Appeasement
.05, Reserves .10`) sum to exactly 1.0, leaving no margin for the costs
`AllocationPhase` draws from `Credits` after the sweep (upkeep, loan
service, tribute). This task only adds the declared 7th share and updates
every call site that constructs `BudgetWeights` or serializes it — it does
NOT change `AllocationPhase`'s spend logic (that's Task 3).

**1a. `src/Core/Epoch/Policies.cs`**

Add `Operations` as a 7th positional parameter to the `BudgetWeights`
record (append it — don't insert in the middle, so existing positional
constructions and the `w[0..5]` faction-pressure array stay valid where
they should):

```csharp
public sealed record BudgetWeights(
    double Development, double Military, double Research,
    double Expansion, double Appeasement, double Reserves,
    double Operations);
```

Update `PolityPolicies.Default` to the rebalanced split (sum stays 1.0):

```csharp
Budget: new BudgetWeights(Development: 0.25, Military: 0.20, Research: 0.15,
                          Expansion: 0.15, Appeasement: 0.05, Reserves: 0.10,
                          Operations: 0.10),
```

**1b. `src/Core/Epoch/Interior/FactionOps.cs:91`** (`PressedBudget`)

`Operations` is never subject to faction pressure — no faction basis
agenda targets it, and it must stay a stable margin regardless of
politics. The `Span<double> w` stays size 6 (only the pressureable six
shares participate in the redistribution/renormalization already there).
Change only the return line to pass `declared.Operations` through
unchanged:

```csharp
return new BudgetWeights(w[0], w[1], w[2], w[3], w[4], w[5], declared.Operations);
```

**1c. `src/Core/Epoch/ControllerContract.cs:~402`** (the war-budget shift)

Add `Operations: b.Operations` to the `with` expression's `BudgetWeights`
construction, unchanged (war reallocates Development/Military/Expansion
only, never touches the operating margin).

**1d. `src/Core/Epoch/ArtifactSerializer.cs`** — the wire format

This is the delicate part. The `POLICY` line is positional
(`f[2]..f[7]` = the six budget shares today, then `f[8]` = TaxRate, and so
on through `f[21]` = `Research.Life`). **Do not renumber any existing
field.** Follow the exact precedent already in this file: when the
Research split was added (comment: "actors v4 (slice G): the research
split rides along"), it was appended as four NEW trailing fields after
everything that existed before it, not inserted into the middle. Do the
same here: append `Operations` as one new trailing field at `f[22]`, with
a comment `// actors v7 (slice ME): the Operations budget share rides
along`.

- Writer (~line 108-121): add `R(pp.Budget.Operations)` as the new last
  argument to the `Join("POLICY", ...)` call.
- Reader (~line 967-972): add `f[22]` parsed as `Operations` to the
  `BudgetWeights` constructor call, as the 7th argument.
- This is a genuine, deliberate format change — per the project's
  greenfield rule, do NOT add a fallback/default for artifacts missing
  `f[22]`; old goldens get regenerated fresh at slice end (the "red
  window"), not preserved.

**Verification**: `dotnet test` green, including whatever
`ArtifactSerializer` round-trip/golden tests exist (search for them —
they will need their fixture artifacts regenerated if they embed a
POLICY line literally; if a golden fixture breaks because it's missing
the new field, that's expected and it should be regenerated, not
special-cased).

**Report file**: `.superpowers/sdd/task-1-report.md`.

## Task 2: four new `Economy` knobs

**Context**: mechanical — follow the exact existing `KnobRegistry.cs`
pattern (see `Economy.LoanRatePerYear` or any neighboring `Economy.*`
entry for the shape: a property on `EpochSimConfig.Economy` config class,
a name-sorted `K(...)` registry entry with a one-line doc, get/set
accessors). Add to `src/Core/Epoch/EpochSimConfig.cs` (the `Economy`
nested config class) and `src/Core/Epoch/KnobRegistry.cs` (name-sorted
among the existing `Economy.*` entries):

| Knob | Default | One-line doc for the registry |
|---|---|---|
| `Economy.PoolIdleDecayPerYear` | `0.05` | Unspent Expansion/Development/Military points decay back to Credits at this rate per world-year — the idle-pool recycle (raise to recirculate faster, lower to let the planner's own backlog absorb more). |
| `Economy.WealthTaxRatePerYear` | `0.02` | Segment wealth above the exemption floor is levied at this rate per world-year, paid to the port owner — the household-wealth recirculation valve. |
| `Economy.WealthTaxFloorPerPop` | `20.0` | Per-capita wealth exemption below which the levy never bites (subsistence households untouched). |
| `Economy.SovereignIssuanceRate` | `0.5` | Bounds bounded sovereign issuance (the second declared mint) to this fraction of the epoch's own real receipts when covering an end-of-epoch shortfall. |

`KnobRegistryTests` enforces naming/ordering/docs/round-trip automatically
— run it, don't hand-verify.

**Report file**: `.superpowers/sdd/task-2-report.md`.

## Task 3: the allocation base, Operations margin, idle-pool decay, sovereign issuance

**Depends on**: Task 1 (Operations exists on `BudgetWeights`), Task 2 (the
four new knobs exist). This is the core mechanism — read
`docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md` §1, §2,
§3, §5 for the full rationale before starting; this brief gives the exact
code shape.

All changes are inside `src/Core/Epoch/Phases.cs`'s `AllocationPhase`.

**3a. The allocation base** (currently line ~369):

```csharp
// before
double allocatable = Math.Max(0.0, Math.Max(pr.Credits, pr.Receipts));
// after
double allocatable = Math.Max(0.0, pr.Receipts);
```

**3b. The Operations margin**: the existing subtraction line (currently
`pr.Credits -= allocatable * (budget.Expansion + budget.Development +
budget.Military + budget.Reserves);`) stays EXACTLY as-is — do not add
`budget.Operations` to that sum. `Operations`'s slice of `allocatable`
must never be subtracted from `pr.Credits`; it simply isn't moved
anywhere, which is what leaves it in the treasury as the margin.

**3c. Idle-pool decay**: add a new private method, called once per polity
at the same point `DecayStockpiles` is already called (after
`FleetOps.SupplyFleets`/`RunUpkeep`/`DecayStockpiles` in the per-polity
loop), decaying `ExpansionPoints`, `DevelopmentPoints`, `MilitaryPoints`
(NOT `ReservePoints` — it funds physical stockpile targets with its own
existing decay dynamic) back into `Credits`, compounded per world-year
exactly like `StockpileDecayPerYear`'s existing shape:

```csharp
private static void DecayIdlePools(SimState state, PolityRecord pr)
{
    var eco = state.Config.Economy;
    int years = state.Config.Sim.YearsPerEpoch;
    double keep = Math.Pow(1.0 - eco.PoolIdleDecayPerYear, years);
    double DecayOne(double points)
    {
        double decayed = points * (1.0 - keep);
        pr.Credits += decayed;
        return points - decayed;
    }
    pr.ExpansionPoints = DecayOne(pr.ExpansionPoints);
    pr.DevelopmentPoints = DecayOne(pr.DevelopmentPoints);
    pr.MilitaryPoints = DecayOne(pr.MilitaryPoints);
}
```

Call it right after `DecayStockpiles(state, pr, ownPorts);` in the
per-polity loop.

**3d. Sovereign issuance**: add `public double CumulativeFiatIssued { get;
set; }` to `SimState` (`src/Core/Epoch/SimState.cs`, beside the other
top-level fields like `WorldYear`). Add a new private method, called as
the LAST step in the per-polity loop (after `DecayIdlePools`):

```csharp
private static void IssueSovereignCredit(SimState state, PolityRecord pr)
{
    if (pr.Credits >= 0) return;
    double shortfall = -pr.Credits;
    double cap = state.Config.Economy.SovereignIssuanceRate
                 * Math.Max(0.0, pr.Receipts);
    double issued = Math.Min(shortfall, cap);
    if (issued <= 0) return;
    pr.Credits += issued;
    state.CumulativeFiatIssued += issued;
}
```

This must never touch `ServiceLoans` (which already ran earlier in
`AllocationPhase.Run`, before this loop, against last epoch's balance) —
do not add issuance logic anywhere near loan service; the two stay
separate by construction.

**Tests to add** (new test file or extend an existing `AllocationPhase`
test file under `tests/Core.Tests/Epoch/`):
- A polity with `Credits` far above its `Receipts` no longer has its
  entire balance swept into pools in one epoch (assert `Credits` after
  `Allocation` retains something close to the pre-epoch balance minus only
  the Operations-margin-adjusted flow, not minus the whole balance).
- `DecayIdlePools` converts a fraction of leftover points to Credits,
  conserved (points decrease by exactly what Credits increases by).
- `IssueSovereignCredit` never fires when `Credits >= 0`; when negative,
  issued amount is `Min(shortfall, rate * Receipts)` exactly, and
  `state.CumulativeFiatIssued` accumulates it.
- Issuance never fires from within `ServiceLoans`'s own shortfall path (a
  borrower still defaults/partial-pays exactly as before — this task
  changes nothing in `ServiceLoans`).

**Report file**: `.superpowers/sdd/task-3-report.md`.

## Task 4: `Borrow` — broaden the lender pool to corporations

**Depends on**: Task 3 (touches the same `Phases.cs` file; sequencing
after 3 avoids a merge conflict, not a functional dependency).

In `Phases.Borrow` (`src/Core/Epoch/Phases.cs`), the lender search
currently only scans `state.Polities`. Broaden it to also consider
`state.Corporations` (`Corporation.Credits`/`CorpCredits`) as eligible
lenders under the same 2x-collateral gate (`candidate.Credits >= principal
* 2`), picking the single richest eligible candidate across BOTH pools
(don't prefer one kind over the other — richest wins, ties broken by
whatever stable order the existing polity loop already uses, extended
consistently to corp id order). The loan's `LenderActorId` already refers
to `Actor.Id`, and corporations have actor records — confirm via
`state.CorporationOf`/`state.Corporations` how a corp's `ActorId` is
exposed (read `Interior/Corporation.cs` if unclear) and reuse that, don't
invent a parallel identifier scheme.

Do not change the 2x-collateral math itself, only the candidate pool.

**Test**: a scenario where only a corporation (not any polity) holds
2x+ collateral — assert `Borrow` finds it and issues the loan with the
corp as lender.

**Report file**: `.superpowers/sdd/task-4-report.md`.

## Task 5: household wealth levy (recirculation)

**Depends on**: Task 2 (the two wealth-tax knobs exist).

In `src/Core/Epoch/Phases.cs`, `MarketsPhase.Run`, add a new pass
immediately BEFORE the existing
`foreach (var pr in state.Polities) pr.LastIncomePerYear = pr.Receipts / spanYears;`
loop (so proceeds land in the same epoch's `Receipts`). For every
`PopulationSegment` in `state.Segments`:

```csharp
double floor = seg.Size * eco.WealthTaxFloorPerPop;
double taxable = Math.Max(0.0, seg.Wealth - floor);
double levy = taxable * eco.WealthTaxRatePerYear * years;
if (levy <= 0) continue;
seg.Wealth -= levy;
var sovereign = state.PolityOf(state.Ports[seg.PortId].OwnerActorId);
sovereign.Credits += levy;
sovereign.Receipts += levy;
```

Mirror `OrderOps.SettleSale`'s existing tax-transfer shape (symmetric,
conserved) rather than inventing a new pattern. `years` and `eco` are
already in scope in `MarketsPhase.Run` (or trivially derived the same way
the surrounding code already does).

**Test**: a segment with wealth above the floor is levied exactly `(Wealth
- floor) * rate * years`, its port owner's `Credits` and `Receipts` both
increase by exactly that amount (conserved), and a segment below the floor
is untouched.

**Report file**: `.superpowers/sdd/task-5-report.md`.

## Task 6: `Money.CumulativeFiatIssued` metric + widened residual formula

**Depends on**: Task 3 (`SimState.CumulativeFiatIssued` must exist).

**6a. `src/Core/Epoch/Health/MetricsOps.cs`**

Add `int` → actually `double CumulativeFiatIssued` as a new field on the
`MetricRow` record (append at the end, after `ConservationResidual` —
`MetricRow` fields don't need to match any wire order, this one isn't
serialized per SIMHEALTH.md's "never serialized" rule, so append wherever
reads cleanly). Populate it in `Snapshot`/wherever `MetricRow` is
constructed from `state.CumulativeFiatIssued` (a level, exactly like
`EndowedEntries` mirrors a cumulative count).

Extend the residual formula (~line 134-135):

```csharp
double issuedThisEpoch = state.CumulativeFiatIssued - prev.CumulativeFiatIssued;
residual = money.Supply - prev.Money.Supply
    - (endowed - prev.EndowedEntries) * endowment
    - issuedThisEpoch;
```

**6b. `src/Core/Epoch/Health/MetricRegistry.cs`**

Add, name-sorted (falls between `Money.CorpCredits` and
`Money.ExpeditionPurses`):

```csharp
M("Money.CumulativeFiatIssued",
  "total credits minted by bounded sovereign issuance since genesis — the second declared mint beside the entry endowment",
  r => r.CumulativeFiatIssued),
```

`MetricRegistryTests` enforces the rest (ordering, docs, accessor
round-trip) — run it, don't hand-verify.

**6c. Do NOT edit `tests/Core.Tests/Epoch/ConservationTests.cs`.** It
asserts `ConservationResidual` stays near zero without hardcoding the
formula — it is the automatic verifier for your formula change in 6a. If
it fails after your change, your formula has a bug; fix the formula, not
the test.

**Report file**: `.superpowers/sdd/task-6-report.md`.

## Task 7: docs — amend the design tree and the calibration references

**Depends on**: Tasks 1-6 complete (this task documents the true final
state, including whatever exact values/line numbers shifted during
implementation).

- `docs/design/economy/markets.md` §Credit: add the sovereign-issuance
  mechanism (bounded, receipts-scaled, never covers loan service) and note
  that loans are now bridge financing (they land in `Credits`, which the
  allocation base no longer reads) rather than a direct pool-investment
  injection. This is a genuine deviation from the current text ("there are
  no banks as actors; lenders are whoever holds surplus") — say explicitly
  that the sovereign now also issues, not only relends.
- `docs/TUNING.md`: add the four new `Economy` knobs (Task 2's table) in
  the existing Economy section, name-sorted among their neighbors; add a
  line for `BudgetWeights.Operations` under the "Budget weights & policy
  defaults" structural-constants entry.
- `docs/SIMHEALTH.md`: add `Money.CumulativeFiatIssued` to the money
  vocabulary table; update the residual description to mention it now
  nets out sovereign issuance too; change the "Pathology: the treasury
  spiral" section from open to resolved, citing this slice's mechanism
  (keep the historical diagnosis prose — add a short "Resolved (slice ME)"
  note above it, don't delete the diagnosis, it's the evidence record).

**Report file**: `.superpowers/sdd/task-7-report.md`.

## After all tasks: acceptance (orchestrator runs directly, not a subagent task)

1. Re-run the committed sweep:
   `dotnet run --project src/Inspector -- sweep
   docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`
2. Regenerate the dashboard from the new sweep output.
3. Check the acceptance criteria from the design doc's "Acceptance
   mapping" section against the new CSVs.
4. Step a fresh sim in the REPL and read `ehealth` — this is the second
   user checkpoint (REPL eyeball acceptance), not something to self-certify.
