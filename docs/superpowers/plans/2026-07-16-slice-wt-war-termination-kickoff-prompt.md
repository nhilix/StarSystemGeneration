# Slice WT kickoff — war termination: the siege clock vs the settlement clock

You are opening a **design slice**, not an implementation slice. Slice L2's
slice-end investigation surfaced — and then *measured* — a real war-design
problem that has been quietly shaping every war the sim has ever run. Read the
evidence before you form an opinion; this one has hard numbers.

## The finding (measured, not theorised)

**Overwhelming naval force makes conquest LESS likely.** Not "irrelevant" —
*counterproductive*.

`ResolutionPhase` (`src/Core/Epoch/Phases.cs:1333-1335`) runs `WarConduct.FightWars`
AND `WarResolution.Terminate` in the same phase. A siege advances on a clock
(25 → 50 → 75 → threshold). But the battles that *ride* the siege grind down the
defender's navy, which trips `SideBroke`'s **fleet-strength** condition
(`src/Core/Epoch/Interpolity/WarResolution.cs:108-133`) — note: NOT an exhaustion
ceiling; measured attacker exhaustion was 0.36/0.43 against a 1.0 bound.
`Terminate` then settles a nominal attacker victory, but `Settle` only cedes
objectives already `Taken` (`WarResolution.cs:204-210`). A still-`Contested`
siege — one epoch from landing — cedes **nothing**. `war.Active = false`, and
`FightWars` skips it forever. The siege freezes at 75/100 for the rest of history.

Two clocks race. Piling on hulls speeds up the wrong one.

**The evidence** (seed 42, 40 epochs, measured on real histories):
- A staged siege was instrumented epoch-by-epoch: clock advanced cleanly
  25 → 50 → 75, needed ONE more epoch, then `PeaceSettled@475` froze it.
- **5000 staged attacker hulls still would not fall the target.** Force is not
  the lever.
- Pre-L2 main vs post-L2 (L2 ~11×'d the navies): wars 9 → 18, wars taking an
  objective 2 → 6 (22% → 33%) — but **"no war ever reaches 2/2 objectives" is
  IDENTICAL before and after.** More war, more partial conquest, still never a
  war fully won. That invariance is the fingerprint of a structural gate, not a
  tuning problem.

So: ~2/3 of wars end having taken nothing, and *no* war in a 1000-year history
ever achieves all its stated objectives. Whatever the design intends war to
*mean*, this is probably not it.

## Why this is a design slice

`docs/superpowers/specs/2026-07-11-design-acceptance.md` and the project's own
precedent research already flag **war-peace as an open genre problem** — not a
thing to patch, a thing to design. There are at least three defensible models
and they are genuinely different games:

- **Uti possidetis** (today's rule, ungated): you cede what you *hold*. Defensible
  — but then a war won by annihilating a fleet yields nothing, and the siege clock
  is the only path to territory, which the settlement clock keeps pre-empting.
- **Partial-siege cession**: a siege past some progress cedes at settlement (the
  besieger "had it"). Cheap to implement; makes force worth something again.
- **Siege-aware break conditions**: `SideBroke` shouldn't fire while the breaker
  is winning a siege — the defender under active siege doesn't get to walk away.
  Arguably the most realistic; changes when wars end, not what they yield.
- **Phase ordering**: `FightWars` before `Terminate` within a step already holds;
  the issue is that both run in one `ResolutionPhase` with no "let the siege land"
  grace. A settlement-delay window is a fourth axis.

These are not equivalent. Pick deliberately, with the user.

## Reading list (in order)

1. **The evidence**: `docs/superpowers/plans/2026-07-15-slice-l2-ledger.md` —
   Task 10's diagnosis section (the instrumented epoch trace and the
   force-is-counterproductive proof) and the "Follow-up filed" section.
2. `docs/HANDOFF.md` — Slice L2's section, follow-up #1, and the measured
   pre/post comparison.
3. `src/Core/Epoch/Interpolity/WarResolution.cs` — `SideBroke` (:108-133) and
   `Settle` (:204-210). **The two functions this slice is about.**
4. `src/Core/Epoch/Phases.cs:1333-1335` — `ResolutionPhase`'s FightWars +
   Terminate pairing.
5. `src/Core/Epoch/WarConduct.cs` — the siege clock, `SiegeThreshold`,
   `TransferPort`, objective status transitions.
6. `docs/design/interpolity/relations.md` + whatever `docs/design/` says about
   war aims and peace — **the design plane is the spec; if it already states an
   intent, that intent wins and this is a bug, not a design question.** Check
   first. (L2's detection-hostility finding was exactly this: the doc already
   said "hostile" and the code was simply wrong.)
7. `tests/Core.Tests/Epoch/WarConductTests.cs` — note
   `Siege_FallsThePort_SegmentsIntact`, which L2 rebuilt to drive the real
   `FightWars` path WITHOUT `Terminate` racing it. **That test is deliberately
   isolated from this bug** — it proves the siege mechanic is sound. Do not
   "fix" it; it's your control.
8. `CLAUDE.md` — workflow, hard rules, model usage.

## Scope

**Step 1 is brainstorming, not coding.** Use superpowers:brainstorming with the
user to pick the model. Then a design doc in `docs/superpowers/specs/`, then
implementation via subagent-driven-development. This is the "reopen the design
properly" path, and it is the *expected* shape here — do not skip to a patch.

The likely surface once decided: `WarResolution.SideBroke` / `Settle`,
possibly `ResolutionPhase`'s ordering, possibly a new knob or two. Small code,
large consequence — every war in every history changes.

**Mechanical acceptance**: `dotnet test` green (hex-tier never breaks);
determinism byte-identity; golden re-frozen once at slice end; **the 32-run
committed sweep** (`dotnet run --project src/Inspector -c Release -- sweep
docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`) — this slice
will move war outcomes economy-wide, so the sweep is not optional.

**Measure the thing you changed.** The pre/post table L2 built is your baseline
and your acceptance instrument:

| | pre-L2 | post-L2 (current main) |
|---|---|---|
| wars declared (seed 42, 40ep) | 9 | 18 |
| wars taking ≥1 objective | 2 (22%) | 6 (33%) |
| max objectives any war reaches | 1/2 | 1/2 |

Reproduce it on your branch (`printf 'epoch 42 40\nwars\nquit\n' | dotnet run
--project src/Inspector -c Release`, read the `objectives N/M` column) and show
what your model does to that third row. **If wars still never reach 2/2, you
have not fixed it.** Do this across several seeds — seed 42 alone is not
evidence (the CU-1 lesson).

**Eyeball gate**: the user reads `wars` on a real history and judges whether war
now *concludes* in a way that reads right — do conquerors get what they fought
for, do defenders ever hold, does peace mean anything.

## Boundary (NOT this slice)

- War *initiation* / casus belli / the `WarSpark` roll — untouched.
- Battle resolution, exhaustion math, escorts, interdiction — untouched.
- The siege mechanic itself: it WORKS (L2 proved it — reachable, establishes,
  clock advances). Do not rebuild it.
- Adjacent-hex spillover, the CU/bank roadmap, the atlas — other lineages.
- Do NOT widen `SiegeThreshold` or nudge force knobs to make sieges land faster:
  that treats the symptom and L2 already proved force is the wrong lever.

## Model usage (per CLAUDE.md)

Brainstorming + the design doc: **Opus**. Implementation tasks:
**Sonnet default, Opus escalation** when touching conservation/determinism or
spanning subsystems (war settlement touches money via reparations — escalate
those). One fresh-eyes whole-branch review pinned to **fable** before merge, one
fix wave.

## A warning from L2, earned the hard way

L2 went looking for a red tuned band and found two stacked structural invariant
breaks under it; each fix revealed the next layer. Budget for that here. The
discipline that worked: **instrument and bisect before touching a threshold; if
the root cause is bigger than a fix wave, stop and reopen the design rather than
patch around it; and treat "the test file was never touched" as the strongest
evidence a fix is honest.** Also: when a finding contradicts the design doc,
check which is wrong before assuming it's the code.

## Wrap-up, in order (per CLAUDE.md)

Merge to main locally → update `docs/HANDOFF.md` → write the next kickoff prompt
(or say plainly if the gap-list backlog is the more natural next step) → sync
Trello → push only on the user's say-so.
