# Slice MC — the seed-99 bifurcation: investigation

Worktree `.worktrees/slice-mc`, branch `slice-mc-price-clock` @ `fd01fd8`.
`git diff c7afaec..HEAD` is **docs-only** — my source is byte-identical to the
source the baseline measured. All instrumentation reverted; `git status` clean;
`dotnet test StarSystemGeneration.sln` green (1081 passed).

## TL;DR

**The seed-99 bifurcation does not exist.** It is a measurement artifact of the
baseline's (deleted) probe harness. Seed 99 at 1y/epoch ignites strongly:
**2 → 32 ports**, Σ receipts **704,072** — the largest economy measured, not a
dead world. The baseline's "1 port, 47.3 receipts, flat for 250 years" does not
reproduce, in three independent processes.

There is therefore **no second clock-dependence**, no ignition mechanism to find,
and **nothing to scope**. Questions 1, 2 and 4 dissolve.

Question 3 is answered and is the useful residue: across **20 seeds** the real
economy diverges **1.0–4.67×** (ports) and **always in the same direction**
(fine ≥ coarse, never once inverted). The nominal/real gap is real, universal,
and **wider than the baseline reported** (nominal up to 264×).

**Bonus, and it matters more than the assignment:** I ran the Q2 experiment
anyway. **The band-bid budget cap is NOT the root of the nominal divergence
either.** Neutralising it completely — cap bind rate driven from 36–53% to
**0.0%** — leaves the divergence untouched (seed 42: 61.5× → 61.5×). That is a
**fourth refuted diagnosis**, and it refutes the premise the slice is currently
being scoped on. See §5.

---

## 1 — What actually happens to seed 99 at 1y: it ignites

From-genesis, standard test galaxy (`GalaxyRadiusCells = 8`), mints OFF,
250 world-years. Traced per world-year:

| world-yr | ports | segs | facilities | expeditions | colony hulls (reserve) |
|---|---|---|---|---|---|
| 1   | 2  | 2  | 12  | 0 | 2 |
| 8   | 2  | 2  | 12  | 1 | 1 |
| 10  | 3  | 3  | 14  | 1 | 1 |
| 26  | 4  | 4  | 22  | 1 | 2 |
| 36  | 6  | 6  | 28  | 3 | 0 |
| 80  | 11 | 11 | 56  | 6 | 2 |
| 130 | 16 | 17 | 87  | 13 | 1 |
| 250 | **32** | 37 | 170 | 26 | 2 |

First expedition dispatches at **world-year 8**; the first new port lands at
**year 10**. Continuous expansion for 250 years. Compare 25y/epoch: 12 ports at
year 250. **Fine expands 2.67× more than coarse** — the same direction as every
other live seed.

The founding-cadence gate (`Expansion.FoundingCadenceYears`, default 25) and the
L2 groundbreak gate were the prompt's early suspects. They are **not** blocking
ignition: expeditions fire at years 8, 30, 33, 60, 70 … — the cadence window is
per-polity and per-expedition, and as new polities/ports appear the aggregate
founding rate rises freely. No gate starves the fine clock.

## 2 — Why the baseline's number is wrong

Three of five baseline seeds reproduce **to the last decimal** at all three
clocks — proof my measurement frame is identical to theirs:

| seed | clock | baseline Σ receipts (mints OFF) | mine | verdict |
|---|---|---|---|---|
| 7    | 1y  | 366,590 | **366,590.0** | exact |
| 2024 | 1y  | 217,642 | **217,642.4** | exact |
| 13   | 1y  | 44.9    | **44.9**      | exact |
| 42   | 25y | 15,606  | **15,605.7**  | exact |
| 99   | 25y | 57,624  | **57,624.2**  | exact |
| **42** | **1y** | **191,189** | **959,830.6** | **5.0× off** |
| **99** | **1y** | **47.3**    | **704,071.5** | **14,880× off** |
| **99** | **5y** | **91,787**  | **445,520.8** | **4.9× off** |

Every 25y row reproduces. Every fine row reproduces **except seeds 42 and 99** —
the two largest worlds. Their fine rows are wrong; the rest of the baseline is sound.

### Ruled out as explanations

- **Nondeterminism.** Seed 99 @1y run 3× in-process and in 3 separate processes:
  identical ports (32), identical Σ receipts (704,071.5), identical artifact hash.
- **Mutable static state in `src/Core`.** None exists (grep).
- **Source drift.** `c7afaec..HEAD` touches docs only.
- **Chaotic sensitivity** — *my own best hypothesis, and I killed it.* If large
  worlds were ill-conditioned, the baseline's instrumentation could have nudged
  42/99 onto different trajectories while leaving small worlds alone. Tested by
  perturbing an inert knob by 1e-15 → 1e-9. **The sim is well-conditioned**: a
  1e-9 perturbation moves seed 42's Σ receipts from 959,830.6 to 959,830.4
  (2e-7 relative). Ports identical. Hypothesis dead.
- **Harness slips**: `GenerationYears=1` too, `EpochCount` slip (10 steps not 250),
  radius 6/10. None reproduces "1 port / 47.3".

**I could not reverse-engineer the exact slip**, and I am not going to invent one.
What is established by direct measurement is that the reported value is not what
this code produces. The signature the baseline reported (1 port / 1 segment /
6 facilities) is *seed 13's genesis*, not seed 99's (which is 2 ports / 2 segments
/ 12 facilities) — so a seed/label mix-up in the deleted harness is the most
economical explanation, but it is a **conjecture, not a finding**.

## 3 — Real-economy clock-dependence across 20 seeds (mints OFF, 250 world-yrs)

| seed | ports 25y | ports 1y | ratio | pop ratio | Σ receipts ratio |
|---|---|---|---|---|---|
| 42   | 6  | 15 | 2.50 | 1.17 | 61.5 |
| 7    | 3  | 10 | 3.33 | 1.37 | 25.3 |
| 13   | 1  | 1  | 1.00 | —    | 1.0 (dead control) |
| 99   | 12 | 32 | 2.67 | 1.34 | 12.2 |
| 2024 | 3  | 10 | 3.33 | 1.80 | 16.4 |
| 1    | 3  | 10 | 3.33 | 1.37 | 12.0 |
| 2    | 3  | 7  | 2.33 | 1.33 | 4.4 |
| 3    | 6  | 16 | 2.67 | 1.06 | 18.3 |
| 5    | 4  | 6  | 1.50 | 1.04 | 5.6 |
| 8    | 5  | 16 | 3.20 | 1.53 | 17.9 |
| 11   | 9  | 18 | 2.00 | 0.76 | **264.1** |
| 17   | 5  | 13 | 2.60 | 1.40 | 14.9 |
| 23   | 3  | 14 | 4.67 | 1.73 | 17.3 |
| 55   | 4  | 12 | 3.00 | 1.57 | 22.3 |
| 77   | 5  | 12 | 2.40 | 1.24 | 15.4 |
| 101  | 4  | 6  | 1.50 | 0.84 | 8.6 |
| 123  | 5  | 14 | 2.80 | 1.45 | 21.6 |
| 404  | 4  | 10 | 2.50 | 1.42 | 24.6 |
| 777  | 1  | 1  | 1.00 | 0.55 | 1.3 (second dead world) |
| 999  | 3  | 10 | 3.33 | 1.69 | 15.7 |

- **Ports ratio never falls below 1.0.** No collapse, no inversion, no
  bifurcation — in any seed. The baseline's "direction is inconsistent across
  seeds" claim is **refuted**: every live seed expands more at the fine clock.
- Real divergence band is **1.5–4.67×** (wider than the baseline's 1.7–3.3×, but
  the same phenomenon and the same direction).
- Population is near-invariant (0.55–1.80).
- Nominal divergence is **4.4–264×** — seed 11 at 264× is far worse than any seed
  the baseline sampled.
- Seed 777 is a second dead-world control (1 port both clocks, 1.3×), useful
  alongside seed 13.

**The nominal/real gap holds for every live seed.** That premise is confirmed and
strengthened. There is no real-economy bifurcation to explain.

## 4 — Does it share the budget-cap root? (Q2)

Moot as asked — there is no anomaly to attribute. But I ran the designed
experiment anyway, because the same lever settles the *slice's own* premise. It
did, and the answer is not what anyone expected. See §5.

## 5 — ⚠ The budget cap is NOT the root of the nominal divergence either

**This is the finding the parent needs most, and it contradicts the baseline's
central claim (§4c: "the budget cap is the binding constraint at coarse and it
destroys the intended ×years scaling").**

Experiment: replace `budget = Math.Max(0.0, seg.Wealth)` with
`budget = Math.Max(Math.Max(0.0, seg.Wealth), floor)` and sweep `floor`
0 → 1e12. At floor 1e12 the cap can never bind, so band bids post their full
year-scaled want (`seg.Size * rate * years`) — exactly the P7-clean demand the
baseline says the cap is destroying.

(First attempt used a *multiplier*, which is invalid: `max(0,Wealth) * mult` is
still 0 when wealth ≤ 0. Redone with an additive floor. Reporting this because
the multiplier version gives the same headline and would have been a lucky guess.)

Validity check — the probe is unambiguously live:

| seed | clock | cap bind rate, floor=0 | cap bind rate, floor=1e12 |
|---|---|---|---|
| 42 | 25y | **36.0%** | **0.0%** |
| 42 | 1y  | 1.3%  | 0.0% |
| 99 | 25y | **40.3%** | **0.0%** |
| 99 | 1y  | 10.4% | 0.0% |
| 7  | 25y | **53.3%** | **0.0%** |
| 7  | 1y  | 4.0%  | 0.0% |

The cap's coarse/fine asymmetry is **real and confirmed** (binds 36–53% at
coarse, 1–10% at fine). Removing it changes outcomes (seed 7's coarse ports
3→2). So the lever works. And yet:

| seed | Σ receipts ratio 25y→1y, floor=0 | floor=1e12 |
|---|---|---|
| 42   | 61.5  | **61.5** |
| 7    | 25.3  | 22.1 |
| 99   | 12.2  | 10.4 |
| 2024 | 16.4  | 16.5 |
| 3    | 18.3  | 15.8 |
| 11   | 264.1 | 229.5 |

**Complete removal of the budget cap shaves ~0–15% off the divergence.** Seed 42
does not move at all (61.5 → 61.5); its receipts/yr at 25y is 62.4 with the cap
and 62.4 without it.

Why: with the cap gone, demand per world-year is **still** only 1.8× apart
(coarse 3.02/yr, fine 5.38/yr) — it does not equalise. And 1.8× cannot produce
61×. The budget cap is a **real stock-vs-flow defect that binds often and
matters little**. Fixing it buys ~10%, which is roughly what the baseline said
fixing `DriftReferencePrices` would buy — the slice has now been aimed at two
successive ~10% terms.

### Where the divergence actually appears to live (flagged, NOT diagnosed)

Not my assignment; offered as a lead with numbers, deliberately under-claimed.

Instrumenting all six `Receipts +=` sites, per world-year, seed 42:

| channel | 25y | 1y | ratio |
|---|---|---|---|
| sale tax    | 6.7  | 87.3 | 13.0× |
| sale net    | 36.1 | 471.5 | 13.1× |
| courier / lane fee / burn | ~0 | ~0–0.6 | — |
| **wealth levy** (`Phases.cs:336`) | **19.7** | **3,279.9** | **166×** |
| **total R/yr** | **62.4** | **3,839.3** | **61.5×** |

Goods value actually transacted (`paid/yr`) diverges only **13.1×**, tracking
both sale channels. Receipts diverge **61.5×**. At the fine clock **receipts
exceed total goods value transacted by 4.4×**, because the wealth levy dominates
(85% of fine receipts vs 32% of coarse receipts).

The levy's own formula (`Phases.cs:325`) *is* P7-clean per step —
`taxable * (1 - (1-rate)^spanYears)`. The suspicion is that it is applied to a
wealth **stock** that the wage channel replenishes every step, so gross churn
booked into `Receipts` scales with step count regardless of the compounding.
That is the same stock-vs-flow *class*, at a third site — but I have **not**
confirmed it and it should be instrumented before anyone believes it.

Note this is seed-specific: seed 99's levy diverges 12.9× (in line with its 12.2×
total) and seed 7's 28×. Seed 42 is the levy-pathological one. **This is exactly
the kind of single-seed story that has been refuted three times already — treat
it as a lead, not a conclusion, and test it on the 20-seed grid.**

## 6 — Judgment (Q4)

**Not a separate slice. Not any slice. There is nothing there.** The seed-99
bifurcation should be struck from the baseline, not scoped.

Concretely:

1. **Correct the record.** `2026-07-17-mc-baseline-on-main.md` §5 and its Verdict
   ¶4 ("Flag seed 99") describe an artifact. §1/§2/§3's seed-42 and seed-99 fine
   rows are wrong. §4b's decomposition is built on seed 42's unreproducible fine
   run (it reports 12.3× receipts; the reproducible figure is 61.5×) — the
   arithmetic closes internally against `paid/yr` (13.1×), so the *decomposition*
   is sound but it was mislabelled as receipts.
2. **The slice's premise needs re-scoping before it is scoped.** §4c's budget-cap
   root cause is refuted by the cap-neutralisation experiment. A slice pointed at
   `MarketEngine.cs` ~366/401 would fix ~10% and ship a false green — precisely
   the failure mode §4's own "Consequence for scope" paragraph warns about, one
   site downstream.
3. **The real-economy divergence (1.5–4.67× ports, direction-consistent) is a
   genuine open question** and is *not* explained by anything measured so far. It
   is modest, universal, and one-directional. It may be legitimate (a finer clock
   genuinely affords more founding decisions per world-year) or may be the same
   defect class. It deserves a line in the design conversation, not a slice.
4. **Method note for whoever scopes next.** Three seeds is an anecdote; this
   investigation only caught the artifact because 3 of 5 seeds reproduced exactly
   while 2 did not. The 20-seed grid in §3 costs ~4 minutes to run and should be
   the standard bar for any clock-dependence claim (consistent with slice SH's
   ensemble lesson). Every headline number in §3 and §5 here is reproducible from
   a clean checkout with a ~50-line throwaway probe.

## Appendix — what I ruled out, explicitly

| Hypothesis | Status | Evidence |
|---|---|---|
| Seed 99 fails to ignite at 1y | **REFUTED** | 2→32 ports, first expedition yr 8, 704,072 receipts, 3 processes |
| `FoundingCadenceYears` gate blocks fine ignition | **REFUTED** | expeditions fire yr 8/30/33/60/70…; 26 by yr 250 |
| L2 groundbreak cadence gate blocks fine ignition | **REFUTED** | facilities grow 12→170 at 1y |
| Colony hull never built / subsistence failure | **REFUTED** | reserve colony hulls present throughout |
| Direction inconsistent across seeds | **REFUTED** | 20/20 seeds have ports ratio ≥ 1.0 |
| Nondeterminism explains the baseline | **REFUTED** | byte-identical artifacts, 3 processes |
| Chaotic sensitivity explains the baseline (my own hypothesis) | **REFUTED** | 1e-9 perturbation → 2e-7 relative change |
| Mutable static state in Core | **REFUTED** | none exists |
| Source drift from baseline commit | **REFUTED** | `c7afaec..HEAD` docs-only |
| Harness slips (GenerationYears, EpochCount, radius) | **REFUTED** | none reproduces 1 port / 47.3 |
| Budget cap is the root of the nominal divergence | **REFUTED** | bind 36–53%→0.0%, divergence 61.5×→61.5× |
| The exact bug in the baseline's deleted harness | **UNSETTLED** | could not reverse-engineer; seed-13 signature suggests a label mix-up (conjecture) |
| Wealth levy drives seed 42's 166× term | **UNTESTED LEAD** | measured, not diagnosed; seed-specific; needs the 20-seed grid |
