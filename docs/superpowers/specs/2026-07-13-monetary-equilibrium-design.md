# Monetary equilibrium — the credit loop closes — design (2026-07-13)

The fix for the treasury spiral (HANDOFF flag 1, diagnosed structural in
slice SH: `docs/superpowers/plans/2026-07-12-debt-diagnosis.md`). Every
entered polity ends every seed negative, monotonically, by construction —
not tuning, not bad luck. This slice designs the mechanism; it does not
re-run the diagnosis (already done) and does not touch multi-hop actor
runs, population/fleet locality, or the M4 carried-debt knob mutation
(separate, later slices — see the kickoff's Boundary section).

Two findings drove the design past the diagnosis's own four candidate
levers:

1. **The six budget shares sum to exactly 1.0**
   (`PolityPolicies.Default.Budget`: Development .30 + Military .20 +
   Research .15 + Expansion .20 + Appeasement .05 + Reserves .10). Zero
   slack exists anywhere for the costs that draw on `Credits` *after* the
   sweep — facility upkeep (`RunUpkeep`), loan service (`ServiceLoans`,
   which runs *before* the base is even computed), and vassal tribute.
   Changing only the allocation-base formula (e.g. to `Receipts` alone)
   would not fix the spiral — it would make it worse, deterministically:
   100% of the epoch's income committed to earmarked pools, real bills
   still due, `Credits` falling by exactly (upkeep + loan service) every
   single epoch regardless of how rich receipts are.
2. **The credit supply is fixed for most of every history.** The *only*
   mint in the system is the one-time `InitialCreditsPerPolity` +
   homeworld wealth endowment, fired once per `PolityEmerged` event
   (confirmed by reading `GraduationOps` — splits the parent's existing
   wealth proportionally, no new money — and colony founding — the new
   segment's wealth is explicitly the recycled expedition purse, P4). New
   polities stop appearing by `Genesis.NativeWindowYears` (900) at the
   latest, in a 1000-year/40-epoch history — meaning the entire galaxy's
   credit stock is frozen for the back tenth of every history, while
   population (`SegmentGrowthPerYear`), facilities, fleets, and trade
   volume keep compounding throughout. A fixed money stock funding an
   ever-growing real economy can only resolve as rising velocity or
   falling prices — pure redistribution of the existing stock (the
   diagnosis's own four levers, taken literally) never touches this.

Both findings reframe the "declared closure rule" the SFC lens
(`2026-07-12-debt-diagnosis.md` §The SFC lens) asked for: it isn't only
*what the allocation base is*, it's *what absorbs the slack the base
formula and the fixed-supply assumption both leave undeclared*.

## 1. The allocation base

`AllocationPhase.Run` (Phases.cs:369):

```csharp
// before
double allocatable = Math.Max(0.0, Math.Max(pr.Credits, pr.Receipts));
// after
double allocatable = Math.Max(0.0, pr.Receipts);
```

Drops `Credits` from the base entirely. A polity with a positive balance
stops harvesting its *entire historical stock* into pools every epoch;
`Credits` becomes a real accumulating stock again, moved only by
transactions, loan service, tribute, the Operations margin (§2), pool
decay (§3), the wealth levy (§4), and sovereign issuance (§5). Deficit
financing during a downturn is unchanged in shape — that path never
depended on `Credits` (a negative-balance polity already budgeted off
`Receipts` alone; SH's diagnosis called this "by design").

## 2. The Operations margin

Add a 7th field to `BudgetWeights` (`Policies.cs`): `Operations`. It
participates in the six-way split's sum (still 1.0) but is **never**
subtracted from `Credits` in `AllocationPhase.Run` — the existing
subtraction line keeps summing only `(Expansion + Development + Military
+ Reserves)`; `Operations`'s slice of `allocatable` simply stays in
`Credits` as the cash margin that pays upkeep, loan service, and tribute
without those costs having to draw the balance negative by construction.

Proposed rebalanced defaults (sum retained at 1.0, exact split is a
tuning question for the acceptance sweep, not a brainstorm-level
decision):

| Share | Old default | New default |
|---|---|---|
| Development | 0.30 | 0.25 |
| Military | 0.20 | 0.20 |
| Research | 0.15 | 0.15 |
| Expansion | 0.20 | 0.15 |
| Appeasement | 0.05 | 0.05 |
| Reserves | 0.10 | 0.10 |
| **Operations** | — | **0.10** |

Implementation note for the plan: `FactionOps.PressedBudget` must not let
faction pressure squeeze `Operations` toward zero as a side effect of
redistributing the other six shares — no faction basis agenda currently
targets it, but the pressure math needs to be checked, not assumed.

## 3. Idle-pool decay

SH found `Money.PolityPools` (Expansion/Development/Military points)
accrue roughly 2x faster than the Planner spends them — the Planner's
`PlanSavingsDrawdownYears` (5) assumption resets every `YearsPerEpoch`
(25) cycle against a freshly larger balance, systematically underpacking.
Add `Economy.PoolIdleDecayPerYear` (new `KnobRegistry` entry): after
`Groundbreak`/`BuildLanes`/`FleetOps` spend for the epoch, whatever
remains in `ExpansionPoints`, `DevelopmentPoints`, `MilitaryPoints` decays
a bounded fraction back into `Credits` — compounded per world-year, the
same shape as `StockpileDecayPerYear` (P7-honest: a 25-year step decays
exactly what twenty-five 1-year steps would). A recirculation into the
buffer stock, not a leak.

`ReservePoints` is excluded — it funds physical stockpile targets with
its own decay dynamic already (`StockpileDecayPerYear` × perishability at
the port), not idle cash.

```csharp
double decayed = pr.DevelopmentPoints
    * (1 - Math.Pow(1 - cfg.Economy.PoolIdleDecayPerYear, years));
pr.DevelopmentPoints -= decayed;
pr.Credits += decayed;
// same shape for ExpansionPoints, MilitaryPoints
```

## 4. Household wealth recirculation

Segment `Wealth` only spends up to the three demand bands' capped
per-year rates (`SubsistenceUnitsPerPopPerYear` / `SoLUnitsPerPopPerYear`
/ `LuxuryUnitsPerPopPerYear`); anything above that sits forever — SH's
"no wealth tax, no luxury drain scaled to stock" finding. Add two knobs:

- `Economy.WealthTaxRatePerYear` — the levy rate on wealth above the
  floor.
- `Economy.WealthTaxFloorPerPop` — an exemption below which a segment's
  per-capita wealth is never taxed (subsistence households untouched).

A new pass in `MarketsPhase.Run`, placed immediately before
`LastIncomePerYear` is computed (so proceeds land in the *same* epoch's
`Receipts` and feed both that epoch's `allocatable` and next epoch's
income trailing figure): each segment pays its port owner's sovereign a
bounded levy on wealth above the floor, added to both `Credits` and
`Receipts` (mirrors the existing tax pattern in `OrderOps.SettleSale`).

This does double duty once sovereign issuance (§5) exists: it is also the
inflation-control valve — the sink that drains money back out of
circulation, exactly as real fiat taxation does, rather than only "tax
funds spending."

## 5. Sovereign issuance — the second declared mint

**The gap this closes**: the only mint in the system is the one-time
entry endowment (§ above, finding 2). After `Genesis.NativeWindowYears`
the credit supply is entirely fixed while the real economy keeps
compounding. Sections 1-4 are pure redistribution of that fixed stock —
they stop money being needlessly parked or harvested, but they don't
grow the supply to match a growing economy.

**The mechanism**: the polity treasury becomes a bounded currency issuer,
matching the SFC lens's Kalecki-identity sanity check ("some sector has
to be a reliable net spender into the system... whatever design lands
should be able to point at which sector plays that role" —
`2026-07-12-debt-diagnosis.md` kickoff). Placed as the **last** step in
`AllocationPhase`'s per-polity loop, after `RunUpkeep`/`DecayStockpiles`,
so it sees the epoch's true end-of-epoch shortfall:

```csharp
double shortfall = Math.Max(0.0, -pr.Credits);
double cap = cfg.Economy.SovereignIssuanceRate * Math.Max(0.0, pr.Receipts);
double issued = Math.Min(shortfall, cap);
pr.Credits += issued;
state.CumulativeFiatIssued += issued;
```

New knob: `Economy.SovereignIssuanceRate` — bounds issuance to a fraction
of the epoch's *own real receipts*, not to how deep in debt the polity
already is. This ties issuance capacity to real economic weight rather
than to indebtedness (no moral hazard toward the largest debtor), and
guarantees the mint is bounded — it does not, and must not, absorb every
shortfall: `Polity.NegativeTreasuries` must still breathe (go negative
and recover), not vanish. Issuance **never covers loan service** — that
boundary is deliberate: `ServiceLoans` runs before this step and against
last epoch's balance regardless, so default and collateral seizure stay
real consequences; debt overhang still bites.

**Inflation needs no bespoke mechanic.** Issued credits get spent into
the real economy through the existing upkeep/tribute/appeasement payment
paths (already real transactions against real sellers/factions), so the
*existing* order-book price-drift mechanism responds to more money
chasing the same goods automatically — emergent inflation, for free, from
machinery that already exists. `Money.Supply` (an existing metric)
becomes the legible readout: does the supply actually grow once
emergence-window mints stop, and do prices trend with it.

**Conservation impact** — the one deliberate, tracked change to a
load-bearing invariant, done in the same commit per the kickoff's own
rule ("new mints/sinks must enter the SIMHEALTH inventory and the
residual formula in the same commit"):

- New `SimState.CumulativeFiatIssued` (running total, mirrors how
  `endowed` counts cumulative `PolityEmerged` events today).
- New metric `Money.CumulativeFiatIssued` — a level, diffed by the reader
  exactly like the existing `EndowedEntries` count (`MetricRow` field +
  `MetricRegistry` entry + `SIMHEALTH.md` line; never a per-epoch flow
  field, per the "levels and counts only" discipline).
- `MetricsOps`'s residual formula extends:
  `residual = supply delta − (endowment mint) − (this epoch's issuance)`.
- `ConservationTests` updates in the same commit to expect the widened
  formula — not broken, deliberately extended.

## 6. Credit mechanism — broadened lender pool

`Phases.Borrow`'s 2x-collateral gate is unchanged mechanically — it
should start finding lenders again once treasuries can hold real
surplus (sections 1-2) instead of being swept to zero every epoch. The
lender search broadens to scan `state.Corporations` (`CorpCredits`)
alongside polities: corporations are the other sector the Kalecki
identity says should reliably net-save (dividends aside), and this is a
low-risk addition — it doesn't touch the collateral math, only the
candidate pool.

One accepted consequence of §1: borrowed principal lands in `Credits`,
and `allocatable` no longer reads `Credits` at all, so a loan no longer
directly inflates `MilitaryPoints`/`DevelopmentPoints` the way it does
today. A loan becomes bridge financing — it keeps the treasury solvent
(pays bills, avoids default, reduces reliance on sovereign issuance) —
rather than a direct investment injection. Real investment growth comes
from real receipts (and, at the margin, bounded issuance). This is a
deliberate consequence of decoupling the base from the stock, not an
oversight; `markets.md` §Credit's "war loans finance wars" language
should be read as financing the war *economy's solvency*, not as a
pool-injection mechanism, when this lands.

## Acceptance mapping

Re-run the committed sweep
(`docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`):

- `Polity.NegativeTreasuries` breathes — dips still happen (bills can
  outrun the Operations margin and the bounded issuance cap in a bad
  epoch) but now recover, since `Credits` is no longer swept regardless
  of income.
- A live loan market past epoch 4, `Money.LoanPrincipal` nonzero
  somewhere in the second half — treasuries (and now corp books) can
  hold real 2x-collateral surplus.
- `Economy.LoanRatePerYear` variants produce different histories — loans
  persisting for many epochs let the rate actually compound.
- Conservation residual stays ~0 against the **widened** formula
  (endowment mint + sovereign issuance) — every other new flow (pool
  decay, wealth levy) is a symmetric transfer between existing holder
  classes, not a mint, so it needs no residual change.

**Eyeball gate**: an updated dashboard from the re-run sweep telling the
after-story (SH's dashboard is the before), plus `ehealth` on a stepped
sim, showing `Money.Supply` actually growing past the emergence window
and `Money.CumulativeFiatIssued` nonzero-but-bounded.

## Design docs to amend in-branch

`docs/design/economy/markets.md` §Credit gets the sovereign-issuance
mechanism and the widened Operations/wealth-levy budget shape folded in
(the design is the spec — this is a genuine deviation from "there are no
banks as actors; lenders are whoever holds surplus," since the sovereign
now also issues, not just relends; the doc needs to say so explicitly).
`docs/TUNING.md` (four new knobs, one new `BudgetWeights` field) and
`docs/SIMHEALTH.md` (new metric, widened residual formula, the "treasury
spiral" pathology entry updated from open to resolved-with-mechanism)
both get amended in the same branch.

## Out of scope (unchanged from the kickoff's Boundary)

Multi-hop actor runs / retiring relay bids; population-fleet locality and
the genesis-vs-simulation body disconnect; the off-lane/smuggling gap;
the M4 carried-debt knob-mutation issue (unless this design ends up
touching mid-run knob mutation, which it doesn't); K5/K6 atlas work; no
new UI.
