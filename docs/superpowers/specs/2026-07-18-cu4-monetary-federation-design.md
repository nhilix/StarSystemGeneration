# Slice CU-4 — bank/currency-union strength → federation generation

Design date 2026-07-18. **The fourth and final slice of the CU chain.** CU-1 gave
every polity a `Currency`; CU-2 a `Bank`; **BF** made the bank a monetary authority
(`ClaimOnState`, lending, a money sink, FX-backing); **CU-3** made currencies+banks
*consolidate* at absorption (reserves pool, claim books inherit). CU-4 **closes the
loop**: a polity's monetary strength feeds back into *whether and how it federates*,
so the economic layer the CU chain built shapes the political map.

Today the "savers fuse / debtors get conquered" correlation exists only as an
*accident* of the sim's dynamics (CU-3's acceptance run surfaced it). CU-4 makes it
a *mechanism*: monetary credibility makes a polity a more attractive federation
partner, and monetary weakness pushes a vassal toward peaceful absorption. War /
conquest decisions are **out of scope** (debtor-conquest stays emergent; the war
seam is Slice WT's territory).

## 1. What CU-4 owns

CU-4 changes a **decision** (which federations/absorptions fire), never money
movement. It adds a single monetary term — a *bias*, an additive discount on an
existing deterministic warmth threshold — to two existing gates, using the idiom the
federation gate already uses for border entanglement (`FederationOverlapDiscount ×
OverlapShare`). No new subsystem, no new hash-roll channel, no new iteration order.

- **Peer fusion** — monetary credibility of *both* partners lowers the federation
  warmth gate. Strong-backed allies fuse more readily.
- **Vassal absorption** — a monetarily weak vassal under a credible overlord
  completes its bond into peaceful annexation sooner.

## 2. The credibility signal (`Bank.BackedShare`)

One per-polity primitive, the bounded-share sibling of BF's `unbacked` FX signal
(`FxOps.cs:80`, `unbacked = max(0, ClaimOnState − Reserve)`). Where BF measures
*un*-backing as supply-equivalent money (unbounded, in currency units), CU-4 needs a
bounded [0,1] *share* to slot into a warmth-gate discount beside `OverlapShare ∈
[0,1]`:

```
// on Bank (a pure computed property — no state, no allocation)
public double BackedShare =>
    (Reserve + ClaimOnState) <= 0 ? 0.0
                                  : Reserve / (Reserve + ClaimOnState);
```

- **1.0** — a pure *saver*: deep reserve, `ClaimOnState == 0` (exactly the CU-3
  "saver super-state" the emergence selects).
- **0.0** — a pure *debtor*: `Reserve == 0`, positive claim book (the "chronic
  deficit-borrower").
- **0.5** — reserve exactly backs the claim.
- **0.0** — an *unearned* fresh polity (`Reserve == ClaimOnState == 0`): the guard
  maps 0/0 → 0, so a brand-new polity gets no credibility bonus until it earns
  reserve. Deliberate and conservative — credibility is *accumulated*, not granted.

`BackedShare` is monotonic in the same direction as BF's signal (more reserve
relative to claim ⇒ more credible ⇒ less unbacked). It reads only the two live
balances CU-3 guarantees are real post-merge (a union's `Reserve` is its members'
pooled backing; its `ClaimOnState` their pooled sovereign debt), so it is meaningful
across a union's whole absorption history.

**Resolving a polity's `BackedShare`:** `state.BankOf(pr.CurrencyId).BackedShare`,
guarded — `pr.CurrencyId < 0` (pre-genesis, never true for two entered polities at
these gates) ⇒ treat as `0.0`. `BankOf` is safe registry-wide (every currency,
retired ones included, has a bank founded 1:1 at `FoundCurrency`).

## 3. Seam 1 — peer fusion (both gates, mirrored)

The federation warmth gate is computed in **two mirrored places**, exactly as the
overlap discount is:

- **`FederationOps.FederationGateHolds`** (`FederationOps.cs:39–42`) — the
  **true-state** gate, verified at Resolution.
- **`ControllerContract.EffectiveGate`** (`ControllerContract.cs:627–634`) — the
  **perceived** gate a controller uses to decide whether to *offer/accept*
  federation (`ControllerContract.cs:260, 266`).

**Both must carry the discount.** A discount on the true gate alone is inert: a pair
whose warmth sits below the perceived offer gate never generates an offer, so the
true gate never sees it. This is precisely why `FederationOverlapDiscount` already
appears in *both* — CU-4's credibility discount follows it one-for-one.

### 3a. The pair rule — `min`

Fusion aggregates the two partners' credibility with **`min`**, so *both* must be
credible for the discount to bite:

```
gate = TreatyGate(Federation)
     − FederationOverlapDiscount     × OverlapShare(A,B)
     − FederationCredibilityDiscount × min(BackedShare_A, BackedShare_B)   // new
```

`min` (not pair-mean) is truest to the observed emergence — savers fuse *with
savers*; a debtor partner drags the term to ~0 — and is the strongest runaway brake
(a strong union cannot cheaply pull a weak partner in). It deliberately diverges from
the pair-*mean* openness check two lines up (`FederationGateHolds` :50–52 / the
`× 0.5` in `FederationTermsAgreeable` :651): openness asks "can one partner *carry*
a warier one"; credibility asks "are *both* monetarily sound" — a different question,
answered `min`.

### 3b. Perceived-view plumbing (the fusion cost)

The true gate reads banks directly (`state.BankOf`). The perceived gate cannot — it
runs over the belief snapshot. So the fusion side adds one field on each side,
mirroring how `OverlapShare` is already threaded:

- **`RelationBrief`** (`ControllerContract.cs:22–30`) gains a trailing
  `double OtherCredibility` — the *other* partner's `BackedShare`.
- **`PerceptionView`** (`ControllerContract.cs:50`) gains `double OwnCredibility`
  beside `OwnStrength` (:113) — the *self* `BackedShare`.
- **`EffectiveGate`** takes `OwnCredibility` threaded from the caller (both call
  sites at :260/:266 have `perceived` in scope) and applies
  `− FederationCredibilityDiscount × min(perceived.OwnCredibility,
  rel.OtherCredibility)`.

**Computed LIVE at snapshot build, not stale-belief.** Populate `OtherCredibility`
and `OwnCredibility` in `Phases.cs` where the brief is assembled
(`Phases.cs:219–233`, beside the live `RelationsOps.OverlapShare(state, selfId,
other)` call) — reading `state.BankOf(...).BackedShare` directly. This follows the
**`OverlapShare` precedent** (a live structural gate input, computed at snapshot from
true state), **not** the `OtherStrength` precedent (a stale-able belief mediated by
`BeliefOps.About`). Rationale: credibility is treated as a structural fusion
precondition like border entanglement, and the true gate re-verifies on truth
anyway, so a staleness channel would add serialization surface for no decision-
quality gain.

**No serialization change.** `RelationBrief`/`PerceptionView` are transient per-epoch
snapshots rebuilt every step from state (only the underlying `Belief` records are
serialized, and CU-4 touches none of them). Adding live-computed fields to the
snapshot changes no persisted layer.

## 4. Seam 2 — vassal absorption (true-state only)

`FederationOps.VassalExits` (`FederationOps.cs:311–315`) completes a long, warm
vassalage into peaceful annexation when

```
worldYearsBound ≥ VassalAbsorptionEpochs × GenerationYears
&& rel.Warmth   ≥ VassalAbsorptionWarmth
```

CU-4 eases **only the warmth threshold**, by the credibility **gap** between overlord
and vassal:

```
effectiveWarmth = VassalAbsorptionWarmth
                − VassalAbsorptionCredibilityDiscount
                  × max(0, BackedShare_overlord − BackedShare_vassal)
// then test:  rel.Warmth ≥ effectiveWarmth
```

A monetarily weak vassal (low `BackedShare`) under a credible overlord (high
`BackedShare`) is absorbed at a lower warmth bar — the "weakness → absorbed into a
stronger monetary umbrella" half of the emergence. `max(0, …)` means a *more* credible
vassal than its overlord gets **no** discount (never a penalty that would *block* an
otherwise-qualifying absorption). Reads both banks directly on true state
(`state.BankOf(overlord.CurrencyId)`, `state.BankOf(state.PolityOf(vassalId).
CurrencyId)`) — **no perceived-view plumbing**; the binding decision and the duration
gate are untouched.

**The world-year duration gate is deliberately not touched** — easing warmth (an
instantaneous state comparison) keeps clock-invariance intact by construction, where
shortening the epoch-count duration gate would risk the P7 telescoping trap.

## 5. Shape, determinism, clock-invariance

- **Shape:** a *bias* — an additive discount on existing deterministic warmth
  thresholds, bounded by `share ∈ [0,1] × a bounded knob`. Not a new threshold, not
  a probability weight, not a hash roll. It nudges choices the sim already makes
  deterministically.
- **Determinism:** both gates already run inside the deterministic Resolution
  sequence; banks resolve by currency id (no new iteration order). `BackedShare` is a
  pure function of two serialized balances. No new `RollChannel`. Byte-identity holds
  for a fixed config (and, at the inert default of §7, byte-identity with pre-CU-4
  `main`).
- **Clock-invariance:** the credibility term reads **instantaneous** bank balances at
  the moment a gate is evaluated. It does not accumulate, does not gate on a rate or a
  count, and does not change *when* a gate is evaluated — only the threshold height.
  So it telescopes across clocks (the two duration gates it sits beside remain the
  clock-carrying terms, both `× GenerationYears` world-year gates, untouched).
  **Verified on the committed clock instrument**
  (`2026-07-17-clock-invariance-experiment.json`), not a throwaway harness — a
  federation-*generation* term is exactly the per-decision choice MC spent a slice
  proving must telescope.

## 6. Conservation

CU-4 moves **no money** — it changes which absorptions fire, not how value flows
across a merge. It has **no direct conservation surface**. But a changed federation
rate exercises **CU-3's consolidation** more (and on different pairs), so the risk is
*surfacing a latent CU-3 bug*, not introducing one. **Acceptance gate
(non-negotiable):** re-run the 32-run committed conservation sweep
(`2026-07-12-debt-diagnosis-experiment.json`); the worst per-currency
`Money.ConservationResidual` must hold at **~1e-16 relative** (CU-3 held 1.22e-15;
judge on *relative* — post-MC nominal supply inflates the absolute). Federation and
absorption fire across the multi-seed history, so the sweep genuinely exercises the
changed decision (not a single seed-42 unit test). Use seed 7 (single-currency) as a
control — with one currency, cross-currency conversion is identity and the residual
isolates any non-FX arithmetic.

## 7. Config, knobs, defaults, activation

Two new knobs, in the **relations** block (`EpochSimConfig.cs`, beside
`FederationOverlapDiscount` :358 and `VassalAbsorptionWarmth` :380), both registered
in `KnobRegistry.cs` (beside `Relations.FederationOverlapDiscount` :1003 and
`Relations.VassalAbsorptionWarmth` :1083 — a hard rule; an unregistered knob is a
config-artifact-stamping violation and blocks the sweep):

| Knob | Meaning |
|---|---|
| `Relations.FederationCredibilityDiscount` | federation warmth-gate discount per point of the pair's `min` credibility |
| `Relations.VassalAbsorptionCredibilityDiscount` | absorption warmth discount per point of overlord−vassal credibility gap |

**Ship live, via an inert-at-0 checkpoint** (BF's discipline):

1. Build the mechanism with **both knobs defaulting to `0.0`** — the discount term is
   then exactly 0 in both gates, `BackedShare` is computed but never subtracted, and
   the golden is **byte-identical to pre-CU-4 `main`** (proves the term is provably
   inert and isolates it from any later surprise).
2. A dedicated **activation task** sets a conservative **live default** for each knob,
   re-freezes the golden (a merge in the seed-42 history will move it — expected,
   user-accepted at the eyeball, exactly as MC/L2 moved goldens), and runs the sweep +
   clock instrument + eyeball with the mechanism active.

Live defaults are chosen against the acceptance instruments (§8), not up front. A
sensible starting point mirrors the overlap discount's scale (`0.25`) but starts
lower (credibility can stack *on top of* overlap); the sweep and the surviving-polity
metric decide.

**No serialization change** (§3b). **No new `RollChannel`.**

## 8. Runaway analysis + acceptance metrics

The kickoff flags the self-amplifying loop: strength → fuse → pool reserves →
stronger → fuse more (BF found the FX-backing coupling self-amplifies). CU-4 has
**four structural brakes**, three by construction:

1. **`min` aggregation (§3a)** — a strong union gets the discount *only* with an
   equally credible partner; it cannot cheaply absorb weak ones.
2. **The discount only lowers a warmth bar** — it cannot manufacture warmth, a
   sustained alliance, ideology compatibility, openness, or cohesion. Every other
   `FederationGateHolds` precondition still stands; a credible union still needs a
   *genuine warm ally* to fuse with.
3. **CU-3 dilution** — fusing with a lower-`BackedShare` partner pools its claim book,
   *lowering* the union's own `BackedShare`. Credibility is not monotone under fusion;
   a saver that absorbs a debtor is less credible afterward.
4. **Bounded magnitude** — `share ∈ [0,1] × bounded knob`; the warmth bar drops by at
   most the knob, never to zero.

**Stance: measure, don't pre-brake.** Add a countervailing force only if the sweep
shows genuine over-federation. **Acceptance metrics** (on the committed instruments,
vs a pre-CU-4 `main` baseline at the same seeds):

- **Surviving-polity count** at end-of-history — a runaway shows as collapse toward a
  few mega-unions. Must stay within a sane band of baseline (not monotonically
  crushed).
- **Federation-formed and vassal-absorbed counts** — should *rise* (the mechanism is
  meant to increase both) but not explode.
- **Correlation check** — the acceptance *narrative*: the polities that fuse should
  skew high-`BackedShare`; the vassals absorbed should skew low-`BackedShare` under
  high-`BackedShare` overlords. This is the mechanism doing what the emergence did by
  accident — now on purpose.

`Money.Supply`/`SegmentWealth` are non-commensurable across currencies
(`MetricsOps.cs:6–37`) — all measures above are **counts** or the dimensionless
`BackedShare`, never cross-currency money sums. Seed 7 (single-currency) is the
control.

## 9. Tests + eyeball

**TDD unit coverage:**

- `Bank.BackedShare`: 1.0 for a saver (claim 0), 0.0 for a debtor (reserve 0), 0.5
  balanced, 0.0 for the 0/0 fresh bank.
- **Fusion true gate:** two credible allies whose warmth sits *just below* the plain
  federation gate now pass `FederationGateHolds` with a live discount; a credible +
  debtor pair (min → 0) does **not** (the `min` rule); with the knob at 0 the gate is
  identical to pre-CU-4.
- **Fusion perceived gate:** `EffectiveGate` returns the discounted value for a
  credible pair (mirrors the true gate), so an offer is actually generated — the
  true+perceived agreement that makes the discount non-inert.
- **Absorption:** a long, warm-enough-*only-with-the-discount* vassalage under a
  credible overlord absorbs in `VassalExits`; a vassal *more* credible than its
  overlord gets no discount (`max(0, …)`); knob at 0 ⇒ identical to pre-CU-4.
- **Determinism:** byte-identity for a fixed config; at the 0 default, byte-identity
  with pre-CU-4 `main` (the inert checkpoint).

**Gates:** the hex-tier suite never breaks; the golden re-freezes **once** at the
activation step (not before); the 32-run sweep is the real conservation gate (§6);
the clock instrument confirms telescoping (§5).

**Eyeball (the taste gate):** a driven seed history where the news/timeline shows a
monetarily-strong polity visibly **federating** while a monetarily-weak vassal is
peacefully **absorbed** — read off the committed REPL/history surface (the existing
`FederationFormed` / `VassalAbsorbed` staged events plus the BF `bank:`/`claims:`
surface showing the partners' reserve/claim books), never a throwaway harness.

## 10. Boundary + follow-ups

CU-4 **closes the CU chain.** After it, monetary strength shapes the political map and
the economy ←→ politics loop is closed. No CU-5 unless a genuine new concern
surfaces. Candidate follow-ups to *note, not build*:

- **Belief-mediated credibility** — §3b computes the other partner's credibility live.
  If foreign monetary standing should be *discoverable* (stale at distance, like
  `OtherStrength`), route `OtherCredibility` through `BeliefOps` in a later slice.
  Deliberately not done here (structural-input treatment, per the `OverlapShare`
  precedent).
- **The binding gate** (`TryBindVassal`) and the **seek-protector trigger**
  (`ControllerContract` protection market) stay military-driven — CU-4 eases only
  absorption *completion*. If monetary collapse should itself *initiate* a bid for
  protection, that is the perceived-view trigger change CU-4 declined (more surface).
- **War/conquest decisions** remain untouched — debtor-conquest stays emergent; the
  war seam is Slice WT's.
