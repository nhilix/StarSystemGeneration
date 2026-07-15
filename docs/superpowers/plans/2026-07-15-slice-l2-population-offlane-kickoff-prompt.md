# Slice L2 kickoff — population & off-lane (locality, part two)

You are opening the second half of the locality mega-slice: population
segments gain a real body address, facility staffing gets distance-weighted,
Patrol coverage falls off with orbital distance, and off-lane travel becomes
a real elective alternative to a blockaded lane (with a detection-roll risk
model). Design and planning were done back on 2026-07-14, but **the actual
locality foundation shipped very differently than that plan assumed** — read
the reading list in order, don't skip to the plan.

## Why this isn't a straight "read the plan and go" session

The plan below (`2026-07-14-locality-population-offlane-plan.md`) was written
against Plan 1's *original* shape, before Plan 1's own whole-branch review
found its stated throughline didn't actually work and triggered a full
second design+implementation phase (Phase 2: `BodyResources`, real depletable
per-body stock, `BodySiting`'s extraction-rejects-on-no-body behavior). The
population/off-lane plan predates Phase 2 entirely — it has never seen
`BodyResourceOps`, the new `RollChannel.BodyResourceStock`, or the
groundbreaking-rejection semantics. Two concrete, confirmed problems this
creates:

1. **`RollChannel` collision.** The plan's Task 6 claims
   `ShipmentDetection = 77` as the next free channel. Phase 2 already
   claimed `77` for `BodyResourceStock` (`src/Core/Rng/RollChannel.cs`). The
   real next free value is whatever comes after Phase 2's last entry —
   check the file yourself before writing Task 6, don't trust either plan's
   number.
2. **Colony-founding facilities may now be rejected, not just bodiless.**
   Task 2 of this plan assumes `ProjectOps`'s colony-completion path always
   builds its founding facility + agri complex, just with `Body = None` if
   unlucky (which it aims to fix). Phase 2 changed the *rules of the game*:
   groundbreaking now REJECTS an extraction-type facility outright when no
   eligible body exists — but colony founding (`CompleteExpedition`) was
   deliberately left NOT rejecting (filed as a Phase-2 follow-up, see
   HANDOFF.md item 2 in Slice L's section). Read `ProjectOps.cs`'s actual
   current `CompleteExpedition` and `PlaceFacilityBody` before assuming
   Task 2's `FoundColonyFacilities` helper is still the right shape — it may
   already substantially exist under a different name.

Every other file:line reference in the plan below should be treated the same
way — verified against the current file, not trusted. This branch has, by
now, a strong track record of plans going stale within the same session;
expect no different here.

## Reading list (in order)

1. `docs/superpowers/specs/2026-07-14-locality-mega-slice-design.md` — §2
   (intra-system movement), §3 (population locality), §5 (off-lane routing).
   The original design; still the spec for what this slice builds.
2. `docs/superpowers/specs/2026-07-15-body-resource-stock-design.md` — the
   Phase 2 design that shipped in between. You need this for context even
   though this slice doesn't touch extraction directly — it's why some of
   Plan 1's promised interfaces (`BodySiting.Assign`'s no-body behavior,
   `ProjectOps.PlaceFacilityBody`) don't match what the population/off-lane
   plan assumed.
3. `docs/superpowers/plans/2026-07-14-locality-population-offlane-plan.md` —
   **the draft plan for this slice.** 7 tasks, TDD-structured. Read it as a
   strong draft, not a verified-current plan — see the staleness note above.
4. `docs/superpowers/plans/2026-07-15-slice-l-ledger.md` — how both Phase 1
   and Phase 2 actually landed: real commits, real fix waves, what got
   re-tuned and why. This is your ground truth for "what interfaces actually
   exist now," more reliable than either design doc or plan.
5. `docs/HANDOFF.md` — Slice L's section has the full follow-up list. Item 1
   (adjacent-hex spillover) and item 2 (colony-founding bodiless dud) are
   both directly relevant to this slice's Task 1/2 — see Boundary below.
6. `CLAUDE.md` — slice-session workflow, hard rules, subagent-driven-
   development requirement.

## What this slice actually builds (per the design, re-verify against current code)

- Population segments gain a real body address at creation (the settled
  port body — the finest cheap address within a domain).
- Colony-founding facilities (which bypass normal groundbreaking) get the
  same claim-aware body assignment normal construction already has —
  **but check whether Phase 2's `ProjectOps.PlaceFacilityBody` already does
  this**, since Phase 2 explicitly routed `CompleteExpedition` through that
  helper. This task may already be significantly done; verify before
  re-implementing.
- Facility staffing weights each segment's labor contribution by
  hex-hop + local-hop distance to the facility's specific body (an airless
  mine can be crewed by commute from a habitat one local-hop away, at a
  cost) instead of a flat domain-wide labor pool.
- Patrol fleet enforcement coverage falls off with orbital distance from
  wherever the fleet is docked, instead of a flat domain-wide multiplier.
- Off-lane travel becomes a first-class routing alternative computed
  *alongside* the lane path (not only when no lane path exists at all) — a
  blockaded/severed/quarantined lane becomes a real elected option, priced
  in real time and real risk (a detection roll modulated by Patrol coverage).
- The courier job board gets the same off-lane election when its lane is
  severed.

## Scope

Implement the plan's 7 tasks, task-by-task, **re-verifying every file:line
reference and every "Consumes" interface against the actual current code
before writing that task's test/implementation** — this is not optional
diligence, it's necessary given the two confirmed staleness points above.
Where a task's assumed starting state ("colony founding leaves Body = None")
turns out to already be handled differently by Phase 2, adapt the task to
what's actually true rather than forcing the plan's literal diff.

The plan's own Global Constraints section is binding: determinism (the ONLY
new nondeterminism is the detection roll, on whatever the real next-free
`RollChannel` value is — not literally 77), conservation (P4 — off-lane
seizure is a conserved transfer to the detecting patrol's port, never a
sink/mint; staffing re-weighting redistributes *who* earns labor, never the
total), C# 9 language level, knob discipline.

**Mechanical acceptance:** `dotnet test` green, determinism byte-identity,
goldens re-frozen once at slice end (staffing/off-lane output legitimately
changes). New coverage per the plan: segment body assignment, staffing
proximity weight, Patrol coverage falloff, off-lane route election, the
detection roll's conserved seizure.

**Eyeball gate:** REPL — a blockaded lane shows freight electing an off-lane
crawl instead of stalling; a population segment's `Body` field carries a
real address (data exists below the port for the first time; rendering
segments on the system stage is still deferred, design boundary).

## Boundary (NOT this slice)

- Local-hop cost scaling with port tier/astrogation tech.
- The exact off-lane election weighting formula (urgency/cargo
  value/risk tolerance) — this slice implements the structural capability
  and a minimal severed-lane election only.
- Intra-domain population *relocation* between bodies over time — only the
  *arrival* address gets finer this slice.
- Local-hop travel visualization in the atlas.
- **Adjacent-hex spillover** (Slice L Phase 1 follow-up item #1, raised
  directly by the user): what happens when a hex's eligible bodies are all
  claimed/depleted and a facility wants to expand there anyway. This is
  RELATED to this slice's staffing/off-lane work (both are about what
  happens at the edges of a body-poor or fully-claimed domain) but is its
  own design question — changes `Facility.Hex` semantics, touches the
  separate `Siting.cs` hex-ranking module. **Decide explicitly at this
  session's scope nod whether to fold a design pass for it into this slice
  or keep deferring it** — don't silently carry it forward a third time
  without a decision.
- Colony-founding's bodiless-dud gap (follow-up item #2) — decide at scope
  nod whether Task 2 of this plan is the right place to close it (it may be,
  since Task 2 already touches the same code path) or whether it's a
  separate, smaller fix that shouldn't block this slice.

## Worktree setup

Use the `using-git-worktrees` skill; project convention is `.worktrees/`
at repo root (gitignored for exactly this). Expect
`.worktrees/slice-l2-population-offlane/`. Copy the gitignored files a fresh
worktree needs before any build/batch run: `src/Core/csc.rsp`,
`unity/Packages/manifest.json`, `unity/Packages/packages-lock.json` — check
the Slice L ledger for anything added to this list since it was last
confirmed. **New this slice**: Windows worktree removal can fail with
"Filename too long" on Unity's `Library/PackageCache` — use a
`\\?\`-prefixed `cmd /c rd /s /q` fallback if `git worktree remove` fails,
not plain `rm -rf`.

**Also check `git log main` before branching** — `slice-cu-currency` (a
separate, still-unmerged 12-task currency/FX slice) is being finished in a
parallel session and may land on `main` before or during this one. If it has,
re-check whether it touched any of `MarketEngine.cs`/`ShipmentOps.cs`/
`ArtifactSerializer.cs` in ways that add another staleness point beyond the
two already confirmed above.

## Model usage (per CLAUDE.md)

Route every task through subagent-driven-development — Sonnet default,
escalate to Opus per-task when it touches conservation/determinism invariants
directly or spans multiple `src/Core` subsystems. Task 6 (the detection roll
— a new stateless hash-roll channel, conserved seizure mirroring the
interdiction pattern) is a strong Opus-escalation candidate, matching how
Slice L Phase 1/2 escalated their own roll-channel and conservation-sensitive
tasks. Decide the rest per task at dispatch time. One fresh-eyes whole-branch
review (pinned to `model: fable`) before merge, one fix wave, per standard
protocol — and given this slice's track record so far, budget for the
possibility that the review surfaces something bigger than a fix wave, same
as Phase 1's did. If it does, follow the same discipline: stop, brainstorm
properly, don't patch around it.

## Wrap-up, in order (per CLAUDE.md)

Merge to main locally → update `docs/HANDOFF.md` → write the next slice's
kickoff prompt (or, if the gap-list backlog is the more natural next step by
then, say so instead of forcing a kickoff prompt that doesn't fit) → sync
Trello (`StarSystemGeneration` board — check whether this slice has its own
card yet; if not, create one) → push only when the user says to.
