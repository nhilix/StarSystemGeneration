# Slice CU kickoff — currency & minting model (research phase first)

You are opening the direct follow-on to slice ME (monetary equilibrium,
merged to main 2026-07-14). ME fixed the treasury spiral — verified
clean across the full committed ensemble — but its own acceptance
dashboard surfaced a foundational question ME does not answer: **who is
allowed to create money, in a galaxy where every polity shares ONE
currency?** This session opens with research, not implementation. Do
not skip to design or code until the research lands and the user has
weighed in on it.

## Read first, in order

1. `docs/HANDOFF.md` §Slice ME — the full mechanism as it shipped, the
   acceptance numbers, and the exact framing of the open question (item
   3 under "Filed as follow-up"). This is your problem statement; don't
   re-derive it from scratch.
2. `docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md` §5
   (sovereign issuance) — the mint mechanism as designed, including the
   design rationale ("some sector has to be a reliable net spender") that
   this session needs to either validate, correct, or replace.
3. `docs/design/economy/markets.md` §Credit — the living design's current
   description of loans/issuance (ME amended this in-branch; it's the
   spec today, and whatever this pass concludes must amend it again, not
   contradict it silently).
4. `docs/SIMHEALTH.md` — the money vocabulary (`Money.*` holder classes,
   `Money.CumulativeFiatIssued`, `Money.CumulativeSteadyIssuance`, the
   conservation residual) and the sweep runner you'll validate any new
   design against.
5. Code: `src/Core/Epoch/Phases.cs` — `IssueSovereignCredit` (~line 600),
   the steady-issuance term inside `AllocationPhase.Run`'s per-polity
   loop, and `Borrow`/`ServiceLoans` for how credit currently moves
   between polities. `src/Core/Epoch/PolityRecord.cs` — confirm there is
   genuinely no currency/nation identifier separating one polity's
   credits from another's; it's the same `double Credits` type
   everywhere, summed into one `Money.Supply` in `MetricsOps.cs`.

## The diagnosed problem — don't re-derive it, weigh it

`Credits` is one universal currency. Every polity, corporation, market,
and household wealth pool holds and spends the SAME unit, with no
exchange rate, no per-polity currency, no shared central authority
gating who may expand the supply. ME gave EACH polity independent,
unilateral minting power over that shared currency:
`IssueSovereignCredit` (reactive, capped at a fraction of that epoch's
own receipts) and a steady growth-linked channel (also per-polity,
every epoch). On the committed acceptance sweep, this produced
**83–97% of the final money supply being fiat-issued** across variants
— the vast majority of credits in every history were printed, not
earned, and every polity's printing dilutes every OTHER polity's
credits with no coordinating mechanism.

Two precedents make this look structurally wrong, not just under-tuned:

- **The Eurozone**: multiple sovereign nations share one currency (the
  euro), and the treaties establishing it specifically forbid member
  states from unilaterally printing euros — only the ECB, one shared
  institution, controls the money supply. A member state that could
  print its own euros could spend freely and export the inflation cost
  to every other member holding the same currency; the Euro was
  designed explicitly to prevent this.
- **EVE Online's ISK** — this project's own repeatedly-cited precedent
  (relay bids, the freight/trade-node model, the "measure every faucet"
  posture already written into `2026-07-13-monetary-equilibrium-design.md`).
  ISK is one universal currency across the entire player base. Its
  faucets (mission rewards, bounties, NPC bounties, etc.) are tuned
  CENTRALLY by CCP, the developer — never by individual players,
  corporations, or NPC factions. No in-game actor gets to decide "I need
  more ISK, so I'll make some."

Both precedents put minting authority in ONE place, decoupled from any
individual actor's own need for money. ME's design puts it in every
actor's own hands, sized to that actor's own shortfall. That is the
open question this slice exists to resolve.

## Phase 1 — research (this session's primary deliverable)

Spawn research subagents (Sonnet per the model-usage table; Opus if a
lookup turns out to need real synthesis judgment, not just retrieval —
decide per dispatch, not up front) to answer, independently:

1. **Real monetary theory on currency unions and shared-currency
   systems.** How do multi-actor economies that share one currency
   actually stay stable? What does the literature say about the
   alternative to "one central issuer" — has anyone modeled or tried
   distributed/federated minting authority successfully, and if so under
   what constraints? (Skip general "how does a central bank work"
   material ME's own design doc already covers that ground via the SFC/
   Godley-Lavoie framing — focus specifically on the MULTI-ACTOR
   shared-currency case.)
2. **How other games with a single universal currency handle money
   creation.** EVE Online (ISK) is the must-cover case — go beyond "CCP
   controls faucets" to the actual mechanisms (what specifically creates
   ISK, what destroys it, how CCP tunes the balance, whether any
   in-universe actor has ever been given legitimate minting power and
   what happened). Survey at least one or two other examples if they
   exist — other MMOs or persistent-economy games with one shared
   currency across many players/factions.
3. **Precedent within games this project already draws on** (Victoria 3,
   EU4, etc. — check `docs/superpowers/specs/` for prior research passes
   this project has already done, e.g. the 2026-07-13 SFC/market-research
   pass cited in ME's kickoff, to avoid re-covering old ground) — how do
   THEY handle money supply when each nation/faction is a separate
   economic actor? Note whether they give each nation its OWN currency
   (sidestepping this problem entirely) rather than a shared one.
4. **What "coordinated" minting could mean mechanically for THIS sim** —
   not a design yet, just the shape of the options a redesign would
   choose between: (a) split `Credits` into per-polity currencies with an
   exchange-rate/FX market between them (sidesteps the shared-currency
   problem entirely, at the cost of a real new subsystem); (b) keep one
   shared currency but centralize minting into a single galaxy-wide rule
   not owned by any individual polity (closer to the EVE/ECB model —
   needs a "who/what" for the issuing authority in a game with no NPC
   central bank actor); (c) keep the current per-polity mechanism but add
   real coordination/discipline (a galaxy-wide cap, or explicit
   redistribution so the diluted value returns to who "wallet-shared" it
   somehow) that makes the per-actor structure survivable rather than
   exploitable.

Each research thread should report findings as a written artifact (the
project's convention — see `2026-07-13` market/locality and novelty/
precedent research artifacts referenced in memory) or a spec-style
document under `docs/superpowers/specs/`, not just a chat answer that
evaporates at session end.

## Phase 2 — brainstorm (only after Phase 1 lands)

Once research is in hand, use `superpowers:brainstorming` properly:
present the candidate directions from Phase 1 with trade-offs, let the
user weigh in question by question, and do NOT default to "keep what ME
built" without the user explicitly choosing it — the user was clear this
needs real reconsideration, not a patch. This is exactly the kind of
foundational, currency-flowing-through-every-mechanic decision the
project's "design is the spec" rule cares about most: whatever lands
here amends `docs/design/economy/markets.md` §Credit again, and this
time may also touch how `Borrow`, `ServiceLoans`, `IssueSovereignCredit`,
and the steady-issuance term all work — a real, not cosmetic, revision.

## Phase 3 — design doc, then (a later session's) implementation

Standard slice-session protocol from here: write the design spec, self-
review it, get user sign-off, THEN branch/implement following
subagent-driven-development exactly as ME did (worktree, task ledger,
Sonnet-default/Opus-escalation per task, one whole-branch fresh-eyes
review at fable, acceptance re-run against the SAME committed sweep ME
used — `2026-07-12-debt-diagnosis-experiment.json` — since it's still
the right instrument for "does the treasury spiral stay fixed" even as
the currency model changes underneath it).

## Boundary — NOT this session unless the research changes the picture

- Re-litigating ME's already-fixed spiral mechanics (receipts-based
  allocation base, the Operations margin, idle-pool decay, the wealth
  levy, `Borrow`'s debt-to-income gate, the loan capitalization ceiling)
  — those are settled, validated across the ensemble, and this slice's
  job is the mint/currency question specifically, not a re-audit of
  everything ME touched.
- The two smaller ME follow-ups filed in HANDOFF.md (SoL still below the
  healthy floor economy-wide; `FederationOps.MergeInto`'s
  `OriginalPrincipal` gap) — real, but separate, narrower items; note
  them if the currency redesign happens to touch that code, don't go
  looking for them.
- Slice L (locality) and Slice K6 (economy surfaces) — parallel,
  worktree-isolated tracks; this project now has three concurrent slice
  lines (CU, K6, L) and none should share a checkout.
- Implementation of ANY kind before Phase 1's research is written down
  and Phase 2's brainstorm has produced a user-approved design.

## Traps carried from ME (beyond the K4/K5/SH lists already in HANDOFF.md)

- Gitignored `src/Core/csc.rsp` + `unity/Packages/manifest.json` +
  `packages-lock.json` must be copied into a fresh worktree.
- `runs/` is gitignored and disposable; the sweep CSVs this slice will
  need to re-validate against are regenerated fresh each time, not kept.
- Watch for a red-window mid-slice the same way ME had one: any change
  to `BudgetWeights`, the allocation base, or the wire format tends to
  perturb the reference golden AND a couple of full-history structural
  tests (`FineTickTests`' provisions-price band, `LaneBuilderTests`'
  isolation tolerance, `InteriorTests`' growth assertion) — ME's own
  history shows the right response is to diagnose the real mechanism
  before touching a tolerance, never to widen blindly.
- `git log main` before any merge-out — at least two other sessions
  (K6, L) are working in parallel worktrees off the same `main`.
