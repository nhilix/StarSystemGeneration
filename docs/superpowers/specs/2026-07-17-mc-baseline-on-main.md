# Slice MC — clock-dependence baseline re-measured on main (post-L2)

Branch `slice-mc-price-clock`, base `c7afaec`. All instrumentation removed;
`git status` clean, `dotnet test StarSystemGeneration.sln` green (1081 passed).

## TL;DR

**The premise HOLDS at comparable magnitude.** Σ receipts diverge 12–25× between
a 25y and a 1y clock over the same 250 world-years, against a real economy that
diverges 1.7–3.3×. L2 did not overlap.

**But the mechanism is NOT the one the prior investigation described**, and the
defect is **one layer upstream of `DriftReferencePrices`**. The prior
investigation's Finding 5 is directionally right about the *symptom* (opposite
saturated price regimes) but wrong about the *cause*, and the price term turns out
to be a **minor** contributor to the receipts divergence.

Two surprises, one of them serious (seed 99 — see §5).

---

## Method

From-genesis runs on the standard test galaxy (`GalaxyRadiusCells = 8`), 250 world
years integrated at 25y × 10, 5y × 50, 1y × 250. Seeds 42, 7, 13, 99, 2024. Both
the stock config and mints-OFF (`SovereignIssuanceRate = 0`,
`SteadyIssuanceRate = 0`) — mints-OFF is the frame the prior investigation's
headline table used, and it removes the monetary feedback loop.

Verified first: **genesis is clock-independent** (identical ports/polities/segments
at all three clocks for every seed), so everything below is trajectory, not seeding.

Frame check — my 25y numbers reproduce the prior investigation's almost exactly
(Σ receipts 15,300 vs their 14,945; Σ fiat 3,309 vs their 3,296), so the
measurement frames are comparable and the deltas below are real.

## 1 — Σ Receipts, mints OFF, 250 world-years

| seed | 25y | 5y | 1y | ratio 25y→1y | pre-L2 ratio |
|---|---|---|---|---|---|
| 42   | 15,606 | 87,598  | 191,189 | **12.3×** | 16× |
| 7    | 14,490 | 184,532 | 366,590 | **25.3×** | 24× |
| 13   | 43.2   | 44.4    | 44.9    | **1.04×** (control holds) | 1.0× |
| 99   | 57,624 | 91,787  | 47.3    | **0.0008× — INVERTED** | 60× |
| 2024 | 13,250 | 58,204  | 217,642 | **16.4×** | 23× |

Pre-L2 seed 42 was 14,892 / 53,486 / 239,199. Now 15,606 / 87,598 / 191,189 —
same ballpark, the 5y point moved most.

## 2 — Σ fiat issued (stock config, mints ON)

| seed | 25y | 5y | 1y | ratio |
|---|---|---|---|---|
| 42   | 3,309  | 28,993 | 58,016 | **17.5×** |
| 7    | 2,485  | 42,232 | 56,678 | 22.8× |
| 2024 | 3,170  | 16,669 | 59,765 | 18.9× |
| 99   | 11,800 | 31,393 | 0      | — (dead at 1y) |

Prior exact-decomposition figure was 3,296 → 74,822 = 22.7×. Now 17.5×.
(The brief's "2.9×" was a different horizon/frame and is not the comparable number;
the 22.7× is.) Issuance still faithfully tracks receipts — it remains a thermometer.

## 3 — the real-economy control (ports, 25y→1y)

| seed | 25y | 1y | ratio |
|---|---|---|---|
| 42   | 6  | 10 | 1.7× |
| 7    | 3  | 10 | 3.3× |
| 2024 | 3  | 10 | 3.3× |
| 13   | 1  | 1  | 1.0× |
| 99   | 12 | 1  | **0.08× — collapsed** |

**Nominal 12–25× vs real 1.7–3.3× — the nominal/real gap survives intact** for
42/7/2024. This is the core of the slice premise and it is confirmed.

## 4 — the mechanism has CHANGED. It is not primarily a price defect.

### 4a. The price regime is not what was described

End-of-run price relative to each good's own reference price (mints OFF, seed 42):

| clock | median rel | % at floor | % at ceiling | mean rel |
|---|---|---|---|---|
| 25y | 1.000 | **82%** | 8%  | 10.7 |
| 1y  | 1.000 | 56%   | 14% | 20.1 |

- **Coarse IS floor-pinned** — 82% of (market, good) cells sit at reference.
  That half of the prior story reproduces.
- **Fine is NOT "at the 59–100 ceiling".** Median rel price is 1.000, only 14% of
  cells are at the ceiling, and 56% are still at the floor.
- The prior claim "coarse `max = 1.000` across all markets, every config" does
  **not** reproduce: every config now touches the price ceiling somewhere,
  including the *dead* seed-13 world. That metric was not discriminating.

**Mean price level differs only ~1.9× (10.7 → 20.1).** A ~2× price term cannot
produce a 12–25× receipts divergence. The prior investigation's diagnosis is
quantitatively insufficient.

### 4b. What actually drives it: trade frequency

Exact decomposition, seed 42, mints OFF, per world-year:

| term | 25y | 1y | ratio |
|---|---|---|---|
| Receipts / yr | 62.4 | 764.8 | **12.3×** |
| Trades / yr | 0.77 | 30.38 | **39.5×** |
| Avg value per trade | 86.95 | 27.13 | **0.31×** |

39.5 × 0.31 = 12.3 — **closes exactly.** The divergence is a **trade-frequency**
divergence that the per-trade size only partially offsets. The coarse clock batches
into fewer, bigger trades — but only 3.2× bigger, where it would need 25× to be
span-invariant. The batching is ~13% efficient.

### 4c. The root: bid quantity is capped by a WEALTH STOCK, not scaled to world-time

Order posting, per *step*, across a 25× step-length span (seed 42):

| term | 25y/step | 5y/step | 1y/step | span |
|---|---|---|---|---|
| Sell qty posted | 194.59 | 16.59 | 2.49  | **78×** — supply scales with the step (over-scales) |
| Buy qty posted  | 79.34  | 31.14 | 24.05 | **3.3×** — demand is nearly step-INVARIANT |

Resulting book balance (supply/demand per step):
- **25y: 194.6 / 79.3 = 2.45 → structurally glutted**
- **1y: 2.49 / 24.05 = 0.10 → structurally starved**

So the *opposite-regimes symptom is real and confirmed* — but it is produced by
**order posting**, not by the drift formula.

The cause is in `MarketEngine.cs`, the band-bid path:

- line ~380: `double bandTotal = seg.Size * rate * years;` — the want IS
  correctly `× years`. P7-clean in intent.
- line ~366: `double budget = Math.Max(0.0, seg.Wealth);` — a **stock**. No
  `× years`, no `StepFraction`.
- line ~401: `if (bid > 0 && qty * bid > budget) qty = budget / bid;`
  `// poverty caps the want`

**The budget cap is the binding constraint at coarse, and it destroys the intended
`× years` scaling.** A segment cannot afford 25 years of consumption in one
clearing, so realized demand collapses to `budget / bid` — a step-invariant,
wealth-limited quantity. Measured: bid qty per world-year 3.17 @25y vs 24.05 @1y =
**7.6×**.

This is the *same defect class* the prior investigation identified (stock compared
against a flow) but at a different, dominant site. And it explains the whole
divergence without needing the drift at all:

**demand-per-year (7.6×) × price level (1.9×) ≈ 14× ≈ the observed 12–25×.**

`DriftReferencePrices`' `/StepFraction` is then better read as a *compensation*
for per-step demand that mis-fires: it amplifies fine demand 25× against an
unnormalized fine ask stock (2.49) → ratio ~241 → clamped to cap → the ceiling
tail. It is a real defect, but it is downstream and it is the ~1.9× term.

**Consequence for scope: fixing `DriftReferencePrices` alone buys the ~2× price
term and leaves the ~7.6× volume term untouched.** The prior investigation
located the slice at the wrong site.

One prior claim that does **not** reproduce: measured resting-ask stock per market
is 286.8 @25y vs 207.4 @1y = only **1.4×** (they reported 165 vs 32 = 5.2× for
good 0). Supply stock is *not* scaling with step length the way Finding 5 asserts.

## 5 — SURPRISE: seed 99 bifurcates in the REAL economy

Seed 99 at 1y **never bootstraps**: 1 port, 1 segment, 6 facilities, 47 receipts —
flat for the entire 250 years, indistinguishable from the dead seed-13 control. At
25y the same seed is the *largest* economy measured (12 ports, 13 segments,
Σ receipts 57,624). Pre-L2 it was the *biggest diverger* in the other direction
(11→34 ports, 60×).

This is not a nominal artifact. It is a **qualitative real-economy
clock-dependence**: whether the economy ever ignites at all depends on the
integration step. It is also **direction-inconsistent** — seed 42 expands faster at
fine (6→10 ports), seed 99 expands faster at coarse (12→1).

This weakens the "real economy only diverges 2–3×, so the divergence is purely
nominal" framing that the prior round leaned on. For 3 of 5 seeds it holds. For
seed 99 it fails outright. Worth naming in the slice scope: there may be a second,
real-economy clock-dependence (expansion/colonization ignition) sitting alongside
the nominal one, and the wealth-stock/budget-cap finding in §4c is a plausible
shared root — a coarse clock's poverty-capped demand starves the colony-founding
loop of the trade volume it needs to ignite.

## Verdict

1. **Premise holds.** 12–25× nominal vs 1.7–3.3× real. Report §1/§2 as the new
   baseline.
2. **Mechanism changed / was mis-located.** The symptom (coarse glut, fine starve)
   is confirmed. The cause is the **stock-vs-flow budget cap on band bids**
   (`MarketEngine.cs` ~366/401), not `DriftReferencePrices`. Price level is only a
   ~1.9× term; trade frequency is a 39× term.
3. **Re-scope, don't re-aim at the drift.** A slice pointed only at
   `DriftReferencePrices` would fix ~11% of the problem and ship a false green.
4. **Flag seed 99** — a real-economy ignition bifurcation that the prior round's
   control framing would have hidden.
