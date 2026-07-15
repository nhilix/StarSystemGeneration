# Slice CU — currency & minting mechanism options (research, 2026-07-14)

Phase-1 research thread 4 for Slice CU. **Raw material for a later brainstorm,
not a design decision.** No option is recommended here; every option is grounded
in the actual code so a future design session can judge feasibility, not just
abstract tradeoffs.

## The ground truth (what the code actually is today)

- **One currency, one field.** `PolityRecord.Credits` (`double`, ll.16–17) and
  `Corporation.Credits` both satisfy `ICreditLedger` (`Interior/Corporation.cs:20`
  — `{ double Credits; double Receipts; }`). There is **no** currency id,
  nation-of-issue tag, or denomination anywhere on the ledger. `SimState.LedgerOf`
  (`SimState.cs:132`) hands back "the conserved credit book behind any earning
  actor" indexed purely by actor id — money is fungible across every holder by
  construction.
- **Every holder class sums into one number.** `MetricsOps.Money`
  (`Health/MetricsOps.cs:49`) adds `PolityCredits + PolityPools + CorpCredits +
  SegmentWealth + FactionWealth + OrderEscrow + CourierEscrow + ExpeditionPurses`
  → `MoneyRow.Supply`. Segment `Wealth` and Faction `Wealth` are the *same unit*
  as `Credits` (the wealth levy in `MarketsPhase.Run`, Phases.cs:317–333, moves
  `seg.Wealth` straight into `sovereign.Credits` 1:1).
- **Three declared mints, all per-polity, all in this one unit.** The one-time
  entry endowment; `IssueSovereignCredit` (`Phases.cs:640`, reactive backstop,
  capped at `SovereignIssuanceRate × Receipts`); the steady term in
  `AllocationPhase.Run` (`Phases.cs:414`, `SteadyIssuanceRate × Receipts`, every
  polity every epoch). Cumulative totals live on `SimState.CumulativeFiatIssued` /
  `CumulativeSteadyIssuance` and are netted out of the conservation residual in
  `MetricsOps.Snapshot` (ll.139–142).
- **Determinism model for any new roll.** `EpochRolls.NextDouble(masterSeed,
  channel, step, actorId, subIndex)` (`EpochRolls.cs:10`) — a stateless FNV mix,
  no RNG state. A new stochastic quantity needs a new **stable** `RollChannel`
  enum value (append-only; the registry `Rng/RollChannel.cs` is at 76, next free =
  77) and must be drawn in a fixed iteration order.
- **`.Credits` surface:** ~109 occurrences across 20 files under `src/Core`
  (grep). The *cross-polity* subset — where money crosses an ownership boundary
  and an FX conversion would have to be decided — is the load-bearing part for
  option (a). Enumerated inventory:

  | Site | File:line | Crossing |
  |---|---|---|
  | `OrderOps.FillBuy` escrow→seller | OrderOps.cs:84–87 | buyer↔seller (any market, any owners) |
  | `OrderOps.SettleSale` tax→sovereign, wages→labor | OrderOps.cs:160–162 | seller↔port sovereign |
  | `MarketEngine` input cost / wage | MarketEngine.cs:235,263 | facility owner↔sellers/labor |
  | `MarketEngine` bid escrow, freight spread | MarketEngine.cs:680–682,791–798 | trader↔book |
  | `MarketEngine` **tariff**→collector, **friction**→dstOwner | MarketEngine.cs:801–811 | trader↔*foreign* sovereign |
  | `Phases.Borrow` lender↔borrower | Phases.cs:771–775 | cross-polity/corp loan |
  | `Phases.ServiceLoans` + collateral seizure | Phases.cs:675–698 | borrower↔lender |
  | `FederationOps.PayTribute` | FederationOps.cs:251–253 | vassal↔overlord |
  | `FederationOps.MergeInto` | FederationOps.cs:375 | absorbed↔absorber |
  | `WarResolution` reparations | WarResolution.cs:231–234 | loser↔victor |
  | `CorporationOps` dividend/lobby/seizure | CorporationOps.cs:489,492,1105 | corp↔host polity |
  | `GraduationOps` parent/child split, faction chest | GraduationOps.cs:110,192,349 | parent↔new polity |

- **Serializer.** `ArtifactSerializer` persists `Credits` positionally as one
  field on the `POLITY` row (`p.Credits`, l.154) and the `CORP` row (l.351); loads
  at ll.1043 / 1287. Segment/faction `Wealth` likewise. Any per-currency
  denomination or FX-rate state is *new columns / new record types* in the v4
  markets format.

---

## Option (a) — split `Credits` into per-polity currencies + an FX market

### What changes

The `ICreditLedger` abstraction is where the whole edifice rests, and it assumes
one unit. Splitting means one of two shapes:

1. **Denominated balances.** `Credits` becomes a map/vector `Dictionary<int,double>`
   (currency-id → amount) or a `double` plus a currency-of-issue tag. Every one of
   the ~109 `.Credits` reads/writes must now ask *which* currency, and every
   cross-boundary site in the table above must run an **FX conversion** at a rate.
2. **Home-currency balances + conversion at the boundary.** Each actor holds only
   its own currency; the ~12 cross-polity sites convert on the way through. Fewer
   touched lines but a live FX-rate lookup on every trade fill — and the order book
   itself (`MarketEngine`/`OrderOps`, ~24 `.Credits` sites between them) is
   *intrinsically* cross-owner: a market at a port owned by A matches a sell from
   corp C (hosted by B) against a bid from polity D. `OrderOps.Fill` /
   `SettleSale` / the tariff+friction block are no longer single-currency
   operations. **This is the deep cost: the order book is not a per-polity thing,
   it is the shared meeting point, so FX is not at the "edges" — it is in the hot
   path of every match.**

New state required:
- An **FX rate table** — N×N or N-vs-numeraire — as first-class `SimState` state,
  persisted (new serializer record type) and step-transient-or-not (a live stock
  that drifts, like prices, vs. recomputed each epoch).
- A rate-formation rule. Options: peg to relative money supplies (computable from
  the same aggregates `MetricsOps.Money` already walks); drift like the order-book
  reference price does (a mean-reversion + shock term); or a genuine FX order book
  (large — a second `MarketEngine`).

### Determinism implications

If rates drift stochastically, each epoch's FX shock is a **new `RollChannel`**
(append value 77, e.g. `FxRateShock`, keyed `(step, currencyPairId, 0)`), drawn in
a fixed pair-iteration order. If rates are a **pure formula** over existing
aggregates (supply ratios), *no new roll at all* — deterministic by construction,
which is the cleaner path. The conservation residual math
(`MetricsOps.Snapshot`) breaks conceptually: `Money.Supply` currently sums
unlike-denominated numbers; it would need a numeraire to be meaningful at all, and
"supply grew by X" becomes "supply grew by X *at this epoch's rates*" — the
residual invariant that ME just stabilized would need re-derivation per currency.

### Surface area

**Largest of the three by far.** ~20 files, ~109 `.Credits` sites all in scope;
`ICreditLedger` redefined (touches `PolityRecord`, `Corporation`, `SimState.LedgerOf`
and all ~11 downstream ops files); serializer gains currency columns + an FX-table
record + version bump; `MetricsOps`/`MoneyRow`/`MetricRow` aggregation reworked
around a numeraire; the CE order-book path (`OrderOps`, `MarketEngine`, `BookOps`,
`CourierOps` — CE spec `2026-07-12-contract-economy-design.md` §§1–4) becomes
FX-aware in its match loop. Plausibly the biggest single mechanical change since
the slice-B state rewrite.

### Open questions / risks

- **Reopens ME? Partially.** The three mints (§5 of the ME design) still exist —
  each polity issues *its own* currency now, which is arguably *more* faithful to
  the Eurozone/ISK critique (no one dilutes anyone else's unit). But the spiral fix
  itself (`Operations` margin, receipts-only base, idle-pool decay, wealth levy) is
  denominated in the polity's *own* currency and is untouched in shape. **Red flag:**
  the acceptance sweep's conservation residual (≤1.3e-9) is defined on a single
  summed `Money.Supply`; that metric loses its meaning under multi-currency and the
  ConservationTests would need rewriting, not just re-freezing — a genuine reopening
  of a *just-settled invariant's harness*, even if the spiral behavior is preserved.
- Does an FX order book need actor demand for foreign currency to price? Today
  nothing "wants" a foreign unit except to pay a cross-border bill — the demand
  signal an FX market would price off doesn't obviously exist yet.
- Player legibility: the atlas panels (`PolityPanel`, `CorporationPanel`) show one
  `Credits` figure; multi-currency needs a display denomination decision.

---

## Option (b) — one shared currency, minting centralized into a galaxy-wide rule

Keep `Credits` fungible (leaves options a's surface untouched). Remove per-polity
minting authority; replace it with a single issuing rule not owned by any polity.
The design problem the task names: there is **no NPC central-bank actor** in this
codebase, and `IssueSovereignCredit` + the steady term are called *inside*
`AllocationPhase.Run`'s per-polity loop. So "the authority" has to be either a
formula, a designated body, or an exogenous schedule.

### Concrete candidate authorities (grounded in what exists)

1. **Pure exogenous schedule — no actor at all.** Money supply grows by a fixed
   rule keyed only to world-time / total real output, computed *once per epoch
   before the per-polity loop*, then distributed. `IssueSovereignCredit` and the
   steady term are **deleted from the per-polity loop**; a new galaxy-wide pass
   computes `targetSupply(worldYear or ΣReceipts)` and mints the delta once, then
   allocates it (see distribution question below). Closest to "CCP tunes ISK
   faucets centrally": the *rule* is the central banker. Zero new actor, zero new
   roll (fully deterministic), smallest conceptual surface.

2. **Formula over galaxy aggregates (`MetricsOps`-style).** The issuing rule reads
   the same aggregates `MetricsOps.Money`/`Snapshot` already computes — total
   `Supply`, total `Receipts`, `Population` — and mints to hold a target ratio
   (e.g. money-to-output). Mechanically identical entry point to candidate 1 (a
   pre-loop galaxy pass) but the *quantity* is feedback-controlled rather than a
   fixed schedule. `IssueSovereignCredit` becomes `IssueGalacticCredit(state)` — a
   single call in `AllocationPhase.Run` outside the polity loop. The health probe
   already proves these aggregates are cheap and deterministic to walk.

3. **Designated "reserve-currency body" — a hex-tier / structural anchor.** Analogous
   to how the atlas keys structures to hex-tier bodies: nominate a
   deterministically-chosen holder (largest polity by receipts? oldest? a specific
   port/body?) as the issuing seat, and route all minting through it. `IssueSovereignCredit`
   still runs but **once**, for the designated body only, sized to a galaxy-wide
   need rather than that body's own shortfall. Risk: it privileges one polity's
   balance sheet — closest to a real reserve-currency issuer, but reintroduces an
   "owner" of the mint, which is the thing the critique dislikes.

4. **Player-facing charter/vote authority — NOT available.** Checked
   `docs/design/` for charter/vote/governance/council/assembly mechanics: the
   matches (`polity/factions-and-government.md`, `frame/controller-contract.md`,
   `polity/characters.md`) are about *internal* faction pressure and government
   *form*, not any galaxy-wide vote or charter body that could set monetary policy.
   **There is no existing collective-decision mechanic to hang issuance on.** Listing
   it only to record that it was searched for and does not exist — building one is a
   whole separate slice, out of proportion to this fix.

### Distribution question (shared by candidates 1–3)

Centralized minting still has to *land* somewhere (money enters a holder's
`Credits`). Deterministic options: split the epoch's galactic mint across entered
polities pro-rata by `Receipts` (mirrors the current per-polity cap's weighting,
so no polity is favored by indebtedness) — this is a **fixed iteration in
actor-id order**, no roll needed. Or land it entirely in the designated body
(candidate 3) and let it spend into circulation. Either way the money still enters
via real `Credits` so `MetricsOps`'s residual stays a single-mint accounting — the
*narrowest* change to the conservation formula (one `CumulativeIssued` term instead
of two per-polity ones).

### Surface area

**Small-to-medium.** Deletes/relocates two call sites in `AllocationPhase.Run`
(the steady term ll.414–420 and the `IssueSovereignCredit` loop ll.496–498), adds
one pre-loop galaxy pass + a distribution loop, one or two new knobs
(`GalacticIssuanceRate`, target ratio), and *simplifies* the conservation residual
(collapses `CumulativeFiatIssued` + `CumulativeSteadyIssuance` into one term).
`ICreditLedger`, the serializer's `Credits` columns, and the entire order-book path
are **untouched** — this is the key advantage over (a). Design-doc amend:
`markets.md` §Credit's sovereign-issuer paragraph (ll.175–188) is rewritten.

### Open questions / risks

- **Reopens ME? Yes, directly — this is the honest red flag.** ME §5's whole
  mechanism is *per-polity bounded issuance tied to own receipts*, validated across
  the 8×4 sweep as the thing that made `NegativeTreasuries` breathe. Centralizing
  the mint means a polity in a bad epoch **no longer has its own backstop** — the
  galactic mint may not reach it. The spiral fix could regress unless the
  distribution rule is designed to still deliver counter-cyclically to shortfall
  polities. The sweep (`2026-07-12-debt-diagnosis-experiment.json`) must be re-run;
  the "spiral fixed" acceptance is genuinely back in question. This is the most
  behaviorally-invasive option despite the smallest code surface.
- Feedback formulas (candidate 2) can oscillate — a control-loop stability concern
  the sweep would have to check.

---

## Option (c) — keep per-polity minting, add real coordination/discipline

Leaves the per-polity mint entry points where they are (`Phases.cs:414`, `:640`),
so the ME spiral fix is *least* disturbed. Adds a coordinating layer on top.

### Candidate 1 — a shared galaxy-wide issuance CAP, rationed each epoch

A single per-epoch ceiling on *total* new issuance across all polities. Requires a
new galaxy-wide aggregate computed **before** the per-polity loop (total desired
issuance = Σ over polities of each one's uncapped `SovereignIssuanceRate × Receipts`
+ steady term), compared against a galactic cap (e.g. `GalacticIssuanceCeiling ×
ΣReceipts`). If demand exceeds the cap, ration.

Deterministic rationing — the ordering/determinism concern: a two-pass structure.
Pass 1 (pre-loop) walks polities in **actor-id order** (the loop already does,
Phases.cs:384) and sums desired issuance — pure aggregation, no roll. Pass 2 scales
each polity's realized mint by `min(1, cap/demand)` — a deterministic proportional
haircut, **no `RollChannel` needed** (rationing is a formula, not a lottery). The
existing `steadyIssuance`/`IssueSovereignCredit` lines multiply their result by the
shared scale factor. The scale factor is one new `double` on `SimState` (or a local
threaded through the phase). This keeps ME's shape (each polity still mints toward
its own receipts/shortfall) but caps the *aggregate* dilution — directly answering
the "83–97% fiat" pathology.

### Candidate 2 — seigniorage redistribution / clearing (dilution flows back)

Keep minting uncapped, but treat each polity's mint as a **dilution tax on every
other holder** and rebate it. After the per-polity loop, compute total minted this
epoch (already tracked: the epoch delta of `CumulativeFiatIssued +
CumulativeSteadyIssuance`), and redistribute a seigniorage pool back to holders in
proportion to their pre-mint `Credits` share (who got diluted gets compensated) —
a clearing/settlement pass in `AllocationPhase.Run` after issuance. Mechanically a
new pass walking `state.Polities` (and possibly corps) in actor-id order, moving
`Credits` between existing holders — **conserved, not a mint**, so `MetricsOps`'s
residual formula is *unchanged* (it only nets mints; a symmetric transfer, like the
wealth levy already is, needs no residual term). Determinism: pure proportional
formula over a fixed iteration, no roll.

### What changes in `AllocationPhase.Run`

- Candidate 1: one pre-loop aggregation pass + a scale factor threaded into the two
  existing issuance lines. ~15–30 lines, no new files.
- Candidate 2: one post-loop clearing pass reading the epoch's mint delta. ~20–40
  lines, one new knob (`SeigniorageRebateShare`). Possibly a new `MoneyRow`/metric
  to make the rebate legible.
- New galaxy-wide aggregate either way: total-desired-issuance (c1) or
  total-minted + total-holder-`Credits` (c2). Both are `MetricsOps.Money`-shaped
  walks — cheap, deterministic, already-proven patterns.

### Surface area

**Smallest of the three.** Confined to `AllocationPhase.Run` + one or two knobs +
(c2) possibly a metric. `ICreditLedger`, serializer, order book, `MetricsOps`
residual (c2) all untouched. Design-doc amend limited to `markets.md` §Credit.

### Open questions / risks

- **Reopens ME? Least of the three, but not zero.** The mint entry points and the
  spiral fix are preserved; the cap/rebate sits *on top*. But a shared cap
  (candidate 1) that binds in a bad epoch could throttle exactly the counter-cyclical
  issuance ME relies on — so `NegativeTreasuries` breathing must be re-checked on
  the sweep. Milder than (b) because each polity still mints *first* toward its own
  need; the cap only trims the aggregate.
- Candidate 1 rationing is "fair" by receipts-share; whether that's the right
  fairness (vs. shortfall-share, vs. equal) is a brainstorm question.
- Candidate 2 rebating by `Credits` share rewards the *already-rich* holder (they
  hold the most, get the most rebate) — may be perverse; rebating by who was
  *diluted* (everyone, proportionally) is what real seigniorage-sharing does, and
  that's the same thing, but the distributional politics deserve scrutiny.
- Neither candidate reduces the *total* money growth much on its own — they change
  *who* controls/absorbs it, not necessarily the "fiat share" headline. If the
  goal is specifically to shrink the 83–97% fiat fraction, candidate 1's cap is the
  lever; candidate 2 only redistributes.

---

## Neutral comparison (no recommendation)

| Option | Core change | New roll / determinism risk | Rough surface area | Reopens ME's settled mechanics? |
|---|---|---|---|---|
| **(a)** per-polity currencies + FX | `ICreditLedger` denominated; FX table + rate rule; order book FX-aware | New `RollChannel` **only if** rates drift stochastically; formula-peg = no roll. Conservation residual loses meaning without a numeraire → harness rewrite | **Largest** — ~20 files, ~109 `.Credits` sites, serializer version bump, order-book match loop, `MetricsOps` reworked | Partially — spiral fix preserved per-currency, but `Money.Supply`/ConservationTests invariant must be re-derived (a just-settled harness) |
| **(b)** central galaxy-wide issuing rule | Move minting out of per-polity loop into one galaxy pass (schedule / formula / designated body) | No new roll (deterministic formula/schedule). Feedback formulas can oscillate — sweep-checked. Residual *simplifies* to one mint term | **Small–medium** — 2 call sites relocated, 1 galaxy pass + distribution loop, 1–2 knobs; order book & serializer untouched | **Yes, directly** — removes each polity's own backstop; spiral "fixed" is back in question, full sweep re-run required |
| **(c)** per-polity mint + coordination (cap / seigniorage-clearing) | Add pre-loop cap+ration (c1) or post-loop redistribution (c2) on top of existing mints | No new roll (proportional formulas, fixed actor-id iteration). c2 residual unchanged (conserved transfer); c1 unchanged | **Smallest** — confined to `AllocationPhase.Run` + 1–2 knobs | **Least** — mint entry points & spiral fix preserved; only a binding shared cap needs `NegativeTreasuries`-breathing re-check |

*All three require re-running the committed sweep
(`2026-07-12-debt-diagnosis-experiment.json`) and re-eyeballing the money-supply
dashboard; the degree to which each disturbs ME's validated `NegativeTreasuries`-breathes
result is the sharpest differentiator, and is inversely correlated with code surface
area — (a) is the most code but preserves the most behavior, (b) is the least code
but the most behavioral reopening.*
