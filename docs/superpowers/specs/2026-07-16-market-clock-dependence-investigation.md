# Sovereign issuance clock-dependence — root-cause investigation

> ## ⚠ SUPERSEDED IN PART — read `2026-07-17-mc-baseline-on-main.md` first
>
> Re-measured on main (post-L2) 2026-07-17. **The conclusion that the sim's
> nominal price formation is not P7-clean HOLDS** — divergence survives at
> comparable magnitude (Σ receipts 12.3×/25.3×/16.4× on seeds 42/7/2024; dead
> control 1.04×), and this document's 25y figures reproduce almost exactly
> (receipts 15,300 vs 14,945), so the frames are comparable.
>
> **But this document locates the defect at the wrong site.** Corrections:
>
> - **`DriftReferencePrices` is a MINOR term (~11%), not the cause.** Mean price
>   level differs only **~1.9×** between clocks — a 2× price term cannot produce a
>   12–25× receipts divergence. A slice pointed only here fixes ~11% and ships a
>   false green.
> - **Finding 5's "ask stock 165 vs 32 = 5.2×" does not reproduce** — re-measured
>   at **1.4×**.
> - **Finding 5's "fine sits at the 59–100 price ceiling" is wrong** — fine's
>   median relative price is **1.000**; only 14% of prices are ceiling-pinned.
> - **The real dominant site is one layer upstream** (`MarketEngine.cs` ~366/401):
>   the band-bid want is correctly `× years`, but is then capped by
>   `budget = seg.Wealth` — a **stock, never scaled to world-time** — so realized
>   demand is step-invariant while supply is year-scaled. **Trade frequency**, not
>   price, is the driver: trades/yr 39.5× × avg-trade-value 0.31× = 12.3× receipts,
>   a decomposition that closes exactly.
> - **Finding 7 still holds**: the cause remains upstream of monetary policy, so
>   BF design §3's freeze of ME's issuance cap is still correct and still stands.
> - **The "purely nominal" control FAILS for 1 of 5 seeds.** Seed 99 bifurcates in
>   the **real** economy — at 1y it never bootstraps (1 port, flat for 250 years);
>   at 25y it is the largest economy measured (12 ports). Genesis is verified
>   clock-independent, so this is trajectory. There may be a second, real-economy
>   ignition clock-dependence.
>
> What this document remains authoritative for: its **refutations** (the issuance
> cap never binds at 25y — not once; zeroing both mints still leaves receipts
> diverging 16×), its method, and Finding 7. Those still stand and should not be
> re-tread.

Branch `slice-bf-bank-flow`, worktree `.worktrees/slice-cu3`. All instrumentation
removed; branch left pristine (`git status` clean, build green).

## TL;DR

**Both the prior diagnosis and the feedback hypothesis are refuted.**

Sovereign issuance is not itself meaningfully clock-dependent. It faithfully
tracks `Receipts`, and **`Receipts` is what is not P7-clean**. The divergence is
inherited from nominal price formation in `MarketEngine.DriftReferencePrices`,
which is upstream of every monetary channel. Issuance is a *thermometer*, not the
fever.

This is **not** a slice-BF-sized fix. It is its own piece of work.

---

## Method

Probe harness (throwaway, deleted): from-genesis runs on the standard test galaxy,
250 world-years integrated at 25y/epoch × 10, 5y × 50, and 1y × 250. Instrumented
`IssueSovereignCredit` (shortfall / cap / issued / which term binds), `FundDeficit`
(reserve draw), and `DriftReferencePrices` (raw demand, raw supply, ratio).
Bisected by zeroing `SovereignIssuanceRate` and `SteadyIssuanceRate` independently.
Swept seeds 42, 7, 13, 99, 2024.

---

## Finding 1 — the cap-per-epoch diagnosis is REFUTED

Instrumenting which term binds `issued = Math.Min(shortfall, cap)` (Phases.cs:717):

| clock | negative-treasury calls | shortfall-bound | **cap-bound** |
|---|---|---|---|
| 25y  | 3   | 3  | **0** |
| 5y   | 15  | 6  | 9 |
| 1y   | 122 | 33 | **89** |

**At 25y the cap never binds — not once.** Coarse issuance is 100%
shortfall-limited. A term that is never the binding constraint on the coarse side
cannot be the cause of the coarse side issuing less. The prior implementer's
"25× the borrowing opportunities" story requires the cap to bind at coarse; it
does not.

The user's arithmetic objection was correct, and the data independently confirms
it from the other direction.

## Finding 2 — the feedback hypothesis is REFUTED

The decisive experiment: zero **both** mints (`SovereignIssuanceRate = 0`,
`SteadyIssuanceRate = 0`). No money is created at all, so the issuance→Credits→
allocation-base→Receipts→cap compounding loop is *structurally absent*.

Seed 42, 250 world-years, mints OFF:

| clock | Σ Receipts | Receipts/yr (end) | pop | ports | facilities |
|---|---|---|---|---|---|
| 25y | 14,892  | 28.57    | 13 | 6  | 32 |
| 1y  | 239,199 | 1,952.78 | 14 | 14 | 92 |

**16× receipts divergence with zero mints.** The feedback loop cannot be the cause
of a divergence that survives its complete removal at near-full magnitude.

Note also the fine run ends at −72,905 credits with mints off — deeply negative,
yet still booking 68× the nominal receipts per year. Receipts are gross transaction
taxes; they do not require net money to inflate.

## Finding 3 — the divergence is inherited from Receipts, and it is NOMINAL

The real economy is only ~2–3× apart; the nominal economy is 16–68× apart:

- population: 13 → 14 (**1.1×**)
- ports: 6 → 14 (**2.3×**)
- facilities: 32 → 92 (**2.9×**)
- **Receipts/world-year: 28.6 → 1,952.8 (68×)**

Receipts are not tracking real output. The gap is price level.

**Exact decomposition** (seed 42, 250y, stock config):

| term | value |
|---|---|
| issuance divergence | 74,822 / 3,296 = **22.7×** |
| receipts divergence | 240,191 / 14,945 = **16.1×** |
| issuance intensity (issued/receipts) | 0.221 → 0.311 = **1.41×** |

16.1 × 1.41 = 22.7 — the decomposition closes exactly.

**~89% of the divergence (log-share: ln 16.1 / ln 22.7 = 0.89) is inherited
receipts divergence. ~11% (the 1.41× intensity term) is the issuance mechanism's
own residual clock sensitivity** — the cap binding at fine and never at coarse,
i.e. *deficit dynamics*, the third candidate in the brief. That 1.41× is the only
part that lives in `IssueSovereignCredit`, and it is a consequence of the receipts
divergence, not an independent cause.

Applied to the reported 2.9× (different horizon/frame): the issuance mechanism's
own share would be ≈2.9^0.11 ≈ **1.1×**. Fixing everything inside
`IssueSovereignCredit` buys ~10%. The other ~90% is not in the monetary layer.

`FundDeficit`'s reserve draw is **not** a contributor: `drawn = 0.0` at every clock
in every configuration measured — the reserve is empty in these runs, so stage 1
is inert and everything falls through to the fiat backstop.

## Finding 4 — multi-seed: structural, not a seed-42 artifact

Mints OFF, Σ Receipts, 250 world-years:

| seed | 25y | 1y | ratio | real (ports 25y→1y) |
|---|---|---|---|---|
| 42   | 14,892 | 239,199   | 16×  | 6→14 |
| 7    | 12,913 | 314,807   | 24×  | 2→9 |
| 13   | 43     | 45        | 1.0× | 1→1 (dead world, no economy) |
| 99   | 51,123 | 3,089,213 | 60×  | 11→34 |
| 2024 | 13,103 | 306,960   | 23×  | 3→10 |

Every live economy diverges 16–60× nominally while the real economy diverges 2–3×.
Seed 13 (no economy at all) diverges 1.0× — the control, and it confirms the
divergence is generated by market activity specifically.

## Finding 5 — located: `MarketEngine.DriftReferencePrices` (MarketEngine.cs:1130–1167)

```
demand = (unfilledBids + SignalDemand) / StepFraction   // FLOW, normalized to a generation
supply = unsoldAsks                                     // STOCK, raw, NOT normalized
factor = (demand/supply)^PriceDriftExponent             // applied ONCE PER STEP
factor = clamp(factor, 1/cap, cap)   where cap = exp(PriceDriftMaxPerYear × years)
```

Two defects compose:

1. **Stock/flow normalization mismatch.** Demand is normalized to a generation's
   worth; supply is the raw resting-ask stock, whose size scales with step length.
   Measured average resting ask stock (good 0): **165 at 25y vs 32 at fine** — 5.2×,
   because a 25y step lands 25 years of production into a single clearing. The ratio
   fed to the drift is therefore not span-invariant.

2. **Clamp saturation in opposite directions.** The clamp itself *is* P7-clean —
   `exp(0.04×1)^25 = exp(0.04×25) = 2.718` exactly. But both clocks spend most of
   their time saturated against it, in **opposite directions**: coarse dumps 25y of
   supply at once → permanent glut → prices pinned at/below reference (measured
   `max = 1.000` across *all* markets, every config); fine trickles supply → markets
   frequently stock out entirely (`supply = 0` → `demand/eps` ≈ 1e9 → clamped to
   `cap` every step) → prices run to the ceiling (measured `max = 59–100`,
   `mean = 7.6–13.8`).

Coarse and fine are not sampling the same economy at different resolutions. They
are in **different price regimes** — one structurally glutted, one structurally
starved. Nominal receipts follow, and issuance follows receipts.

(This is consistent with the existing note in `FineTickTests` about seed 42's
"near-floor deep-glut regime" — that comment is describing this defect's coarse
side and attributing it to legitimate trajectory drift.)

## Finding 6 — is a year-scaling fix possible? Not a simple one. TESTED, NOT ASSUMED.

`DecayIdlePools`' shape (`Math.Pow(1 - rate, years)`, Phases.cs:632–643) works
because it acts on a *stock it alone depletes*, with no cross-actor equilibrium.
Price formation has neither property.

I implemented and measured the obvious analogue — drop the `/StepFraction`
normalization (making the ratio span-invariant) and compound the factor per world
year (`factor = Math.Pow(factor, years)`), which is the formula that is *correct on
paper*:

Σ Receipts, mints OFF, seed 42:

| clock | current | candidate P7 fix |
|---|---|---|
| 25y | 14,892  | 10,277 |
| 5y  | 53,486  | 9,217  |
| 1y  | 239,199 | 2,087  |

Divergence improves from 16× to 5× — but does **not** converge, and it *inverts*
(fine now undershoots; fiat at stock config goes 1,512 / 1,241 / **114**). The fine
side still hits the price ceiling (`max = 100.000`).

**Conclusion: the naive year-scaling formula is wrong, and I do not have a proven
correct one.** The reason is structural: `unsoldAsks` is a step-length-dependent
quantity, so no reformulation *of the drift alone* makes the input span-invariant.
A real fix has to address the batching of supply into a single clearing — either by
normalizing the ask stock to a comparable window, or by making the drift read a
flow-vs-flow ratio (cleared-vs-posted over the step) instead of flow-vs-stock. That
is a market-engine design question, not an arithmetic patch, and it will move
every price-sensitive golden in the suite.

## Finding 7 — the fix and the lender-of-last-resort floor are NOT in tension

Good news, and it falls out of Finding 1. The floor's absoluteness lives in
`IssueSovereignCredit` being reserve-blind and unconditional (Phases.cs:711–733,
and the bank-flow design §3 freeze). The root cause is in `DriftReferencePrices`,
which is upstream and monetary-policy-agnostic. **Fixing the price regime does not
require touching the cap, the floor, the reserve, or the trigger.** The backstop
stays absolute.

The tension the brief anticipated would only arise from the *rejected* fixes:
year-scaling or reserve-gating the cap. Since the cap is not the cause (it never
binds at coarse — Finding 1), there is no reason to touch it, and therefore no
trade against ME's spiral cure. Bank-flow design §3's decision to freeze ME's cap
is **correct and should stand**.

I'd flag the converse risk instead: a fix that raised coarse prices to fine levels
would raise coarse `Receipts` ~16×, which raises the cap ~16× — a large, real
change to the monetary regime's operating point, which is what would need ensemble
validation (per the slice-SH bar), not the floor.

## Proportionality — this is its own slice

Against slice BF:

- **BF did not cause it.** Confirmed at `82cc074`, and confirmed here that it
  survives zeroing every mint BF touches. BF's servicing pass amplifies
  (2.9× → 7.3×) because servicing is receipts-proportional too — it is riding the
  same inherited divergence.
- **The fix is not in BF's territory.** BF is the bank-flow slice: issuance,
  claims, servicing, reserves. The root cause is in `MarketEngine`'s price
  formation. Fixing it inside BF means BF silently becomes a market-engine slice
  with no scope nod covering it.
- **There is no proven fix to implement.** Finding 6 shows the obvious formula is
  wrong. Landing it would need a design pass, and per the hard rules, a genuine
  deviation requires amending `docs/design/` — i.e. it wants brainstorming, not an
  inline patch.
- **The blast radius is the whole economy.** Every price, every golden, the
  FineTick bands, and the ME operating point. That is not a fix wave; that is a
  slice with its own eyeball gate.

### Recommendation

1. **Do not fix in BF.** File it as its own slice (natural sibling to ME/SH —
   "nominal price formation is not P7-clean").
2. **For BF's red test** (`FineTick_ProjectCompletions_LandOnWorldYears_NotSteps`):
   this test is failing on a pre-existing defect BF merely amplified. BF should not
   green it by tuning issuance — that would be fitting the thermometer. Either
   widen the band with a comment pointing at the real defect and the new slice (the
   precedent this test's own docstring already sets, repeatedly), or accept the red
   and gate it on the new slice. **This is a user decision** — it is the one place
   where "green the test" and "tell the truth about the sim" diverge, and the
   existing docstring shows this test has absorbed this exact defect under
   "legitimate trajectory drift" language at least twice before. That prior
   attribution now looks wrong and is worth correcting.
3. **Correct the record**: the bank-flow design §3 note and the FineTick docstring
   both attribute this to causes the evidence refutes.

### Existing-code note worth carrying forward

`FineTickTests`' docstring currently explains seed 42's coarse/fine divergence as
"legitimate economy-trajectory drift ... confirmed: no per-tick-vs-per-year formula
defect". Finding 5 contradicts that conclusion directly. There *is* a
per-tick-vs-per-year formula defect; it is in `DriftReferencePrices`. The bands
have been widened three times (0.6 → 0.7 → 0.75 → 0.85) absorbing it.
