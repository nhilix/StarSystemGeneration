# Slice CU-4 kickoff — bank/currency-union strength → federation generation

You are opening the fourth and final slice of the CU chain. CU-1 gave every polity
a `Currency`; CU-2 a `Bank`; **BF** made the bank a monetary authority (lending, a
money sink, FX-backing); **CU-3** made currencies+banks *consolidate* at absorption
(reserves pool, claim books inherit). CU-4's job: **close the loop** — let a
polity's monetary strength *feed back into whether and how it federates*, so the
economic layer the CU chain built actually shapes the political map. Read first,
then brainstorm; do NOT skip to design.

## The gift CU-3 handed you (read this first — it is the whole reason CU-4 is ripe)

CU-3's acceptance run surfaced a real, unplanned emergence pattern, and it is
exactly CU-4's raw material:

- **Peer fusions select *saver* super-states** — banks that earned deep reserves
  from FX spread but never lent to their own state (`ClaimOnState` 0). These fuse
  *peacefully* and pool their reserves.
- **Chronic *deficit-borrowers* (deep claim books) exit via *war annexation***,
  often into degenerate hyperinflated "Loyalist" currencies (rate → 0).

So today the correlation between monetary strength and federation *already exists as
an accident of the sim's dynamics* — savers fuse, debtors get conquered — but
**nothing reads bank strength to DRIVE that split**. CU-4 makes it a mechanism
instead of a coincidence: monetary credibility should make a polity a more
attractive federation partner / a more capable federator, and monetary collapse
should push toward absorption. The measures are already real and computable (BF/CU-3):
**reserve depth**, **backing ratio `Reserve ÷ ClaimOnState`**, and the **FX-rate
track record**.

## Read first, in order

1. `docs/superpowers/specs/2026-07-16-bf-bank-flow-design.md` §5 (FX-backing, the
   credibility coupling) and §8 (what BF handed forward). BF made `Reserve ÷
   ClaimOnState` a real credibility signal.
2. `docs/superpowers/specs/2026-07-17-cu3-currency-consolidation-design.md` §6
   ("Constraints carried to CU-4") — CU-3 guarantees the post-merge balance sheet is
   real (a union's reserve = pooled member backing, claim book = pooled sovereign
   debt), so a union's strength is now a meaningful *accumulated* quantity.
3. `docs/superpowers/specs/2026-07-15-cu2-bank-actor-design.md` — the bank's
   founding shape and the CU-4 "candidate strength measures" note.
4. The federation-generation code — **this is the seam you'll read hardest**:
   `src/Core/Epoch/Interpolity/` (`FederationOps` — how federations/vassalage/
   absorption are currently *decided*, not just executed; what inputs the decision
   reads today: strength/military/relations). Find where the sim currently CHOOSES
   to federate or absorb, and what it weighs. CU-4 adds a monetary term to that
   choice. (CU-3 changed only the *execution* seam `MergeInto`; CU-4 is about the
   *decision*.)
5. `src/Core/Epoch/Bank.cs`, `SimState.BankOf`, `FxOps` — the strength measures.

## The open design questions — weigh them in the brainstorm

- **Which measure(s)?** Reserve depth (absolute backing), backing ratio (`Reserve ÷
  ClaimOnState`, credibility), FX-rate level or track record (market's verdict). Each
  says something different; a blend risks over-tuning. Which best captures "a strong
  monetary polity"?
- **What decision does it feed, and how?** Does monetary strength: make a polity a
  more attractive *partner* (others want to federate with it)? A more capable
  *federator* (it can absorb/lead)? A weaker one more *absorbable*? All three? The
  emergence pattern suggests strength→fuse, weakness→conquered — formalize it without
  just hard-coding that.
- **Is it a threshold, a bias, or a probability weight?** The sim's other
  generation choices (find where) use a particular idiom — match it. A monetary term
  should nudge an existing deterministic/hash-driven choice, not become a new
  bespoke subsystem.
- **Determinism.** Federation *generation* is a stateful choice; if it uses hash
  rolls keyed (step, actor, channel), a monetary term must fold in without breaking
  determinism or the fixed iteration order.
- **Does it risk a runaway?** Strength→more federation→more pooled strength→more
  federation. BF found the FX-backing coupling self-amplifies; watch for the same
  here. A monetary term that makes the strong unstoppable may need a countervailing
  force (or may be fine — measure it).

## What CU-4 must NOT break

- **Conservation stays at ~1e-16 relative** (the 32-run sweep). CU-4 changes a
  *decision*, not money movement, so it shouldn't touch conservation — but a changed
  federation rate changes which merges fire, which exercises CU-3's consolidation
  more/differently. Re-run the sweep; a decision change must not surface a latent
  consolidation bug.
- **The clock-invariance instrument** (`2026-07-17-clock-invariance-experiment.json`,
  MC): a federation-*generation* term is a per-decision choice — verify it does not
  reintroduce clock-dependence (a choice made "per epoch" vs "per world-year" is
  exactly the P7 trap MC spent a whole slice on; if CU-4's term gates on a rate or a
  count, check it telescopes across clocks).
- Determinism byte-identity; the hex-tier suite; register any new knob in
  `KnobRegistry.cs`.

## Boundary — this CLOSES the CU chain

CU-4 is the last CU slice. After it, monetary strength shapes the political map and
the loop is closed (economy ← → politics). Note any follow-ups but don't chain a
CU-5 unless a genuine new concern surfaces.

## Traps carried

- **Measure on the committed instruments, never a throwaway harness** (six diagnoses
  died in MC that way). The 32-run sweep for conservation; the clock instrument for
  P7; a real driven history for the eyeball (does a monetarily-strong polity visibly
  federate where a weak one is conquered?).
- **`Money.Supply`/`SegmentWealth` are non-commensurable across currencies**
  (`MetricsOps.cs:6-37`) — use per-currency measures, and seed 7 (single-currency) as
  a control.
- **Dispatch implementation subagents synchronously** (`run_in_background: false`) —
  background dispatch silently did nothing in the BF/MC/CU-3 sessions.
- Slice-session workflow (scope nod · eyeball · merge decision; task ledger;
  subagent-driven-development; one fable whole-branch review + fix wave;
  kickoff-prompt chaining). `git log main` before merge-out.
