# Slice J Kickoff — Session Prompt

You are starting **Slice J (Handoff & certification)** of the epoch-sim
implementation roadmap, under the lighter protocol in `/CLAUDE.md` (read
it first). J is where the generated galaxy stops being a simulation
output and becomes **a game's initial conditions, delivered whole and in
motion**: the world-state handoff layer, the resumability contract (the
same seven-phase machine stepping at play-tick resolution), the delta
boundary (a save = config + artifact + deltas + the log's continuation),
a full-design acceptance pass against the design tree, and the final
docs/diagram sync. After J only the Unity atlas rebuild (K) remains.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row J
   (consumes I's certified perception layer; K needs J's settled model).
3. **The design docs J implements**:
   - `docs/design/narrative/handoff.md` — the whole slice's spec:
     contents (complete registries + open threads), the resumability
     contract (P7's final test), controller handover (P2's final test),
     the never-closing log, the delta boundary.
   - `docs/design/frame/time.md` — the four clocks; play-tick semantics.
   - `docs/design/frame/system-map.md` §Artifact discipline.
   - `docs/design/frame/controller-contract.md` — the player occupies a
     slot by answering the same Intent question.
   - `docs/design/frame/principles.md` — the P-numbers the acceptance
     pass certifies against.
4. **What slice I left ready** (HANDOFF + ledger
   `docs/superpowers/plans/2026-07-11-slice-i-ledger.md`):
   - The artifact is **22 layers, byte-stable**, with LoadThenContinue
     proven over every registry INCLUDING each actor's compressed
     perception state (`belief` layer: PBEL/WBEL/CBEL/STANCE), pulses,
     POIs, and plagues — handoff.md's content item 1 already
     serializes; J adds the *handoff framing* (indexes, open-thread
     surfacing), not new persistence.
   - "Artifact finalization compiles nothing" already holds: the POI
     compiler runs inside Chronicle every epoch; the map is current at
     every save point.
   - Controllers reattach on load; `IController` is the single decision
     interface — the player handover is a slot swap
     (`Actor.Controller`), certified by a test that drives a polity
     with a scripted controller mid-run and byte-compares the rest.
   - **All rates are per world-year** (the P7 discipline every slice
     kept): `Sim.YearsPerEpoch` is the integration step. Fine-tick
     resumability = the same `EpochEngine` stepping with a small
     YearsPerEpoch over a loaded artifact. Expect integration-rate
     effects (drift targets, growth clamps) to surface — certifying
     and bounding them IS the slice; a mechanic that breaks at fine
     tick is a bug to fix, not to hide.
   - Events next free: economic 211, political 314, military 409,
     diplomatic 511, corporate 605, character 704. `RollChannel` next
     free: 72. Knob families through `Plague.*` in KnobRegistry +
     TUNING.
5. **Deferred items J may adopt** (flagged in HANDOFF, user's call at
   the scope nod): ruins-POI lawlessness/piracy wiring and memorial
   stance anchors (chronicle-and-poi.md's live-effects table); both are
   small wires, neither blocks handoff.

## Scope (roadmap row J)

- **World-state handoff layer**: the final artifact framing — complete
  registries with computed indexes (per-place, per-actor, per-war,
  per-character views), open threads surfaced deliberately (loaded
  tensions, pending successions, half-won wars, leveraged
  corporations), delivered as the live game's initial conditions.
- **Resumability tests**: the same state machine steps at play-tick
  resolution — load the epoch artifact, step with fine YearsPerEpoch,
  certify no genesis-only mechanic exists and the seven phases hold
  (posted fleets, price drift, character continuity). Determinism
  byte-identity at fine tick.
- **Controller handover certification**: a scripted/player controller
  occupies a polity/corporation/character slot mid-run; nothing inside
  the sim cares who is driving.
- **Delta boundary**: everything the live game mutates records as
  deltas against the artifact + the continuing event log; the
  procedural baseline (genesis strata, hex tier) stays pure. A save =
  GalaxyConfig + artifact + deltas + log continuation.
- **Full-design acceptance pass**: sweep `docs/design/` against the
  implementation; every P-number certified or the gap filed; remaining
  perfect-info/stub comments hunted down.
- **Docs/diagram final sync**: design tree amendments from the sweep;
  the generation-flow diagram (`docs/diagrams/generation-flow.html`)
  updated and republished to its existing artifact URL.

**Boundary**: the Unity atlas is K. Espionage stays reserved. No new
simulation mechanics beyond the two adopted deferrals (if the user says
yes) — J certifies, frames, and hands off.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod (ask about the two
   deferred wires).
2. Branch `slice-j-handoff` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-j-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (hex tier untouched) · determinism
   byte-identity at BOTH tick resolutions · LoadThenContinue at fine
   tick · delta round-trip (artifact + deltas + log ≡ live state) ·
   conservation through fine-tick runs.
4. REPL surface: a handoff/save-load command pair (`esave`/`eload`
   exist — J adds the delta save) · a fine-tick `estep` variant · an
   open-threads panel ("the world in motion" summary) · `watch` intact.
5. User gates: scope nod · REPL eyeball (suggestion: **load a finished
   epoch run, step it at fine tick, and watch the same war keep
   burning** — then take a polity's controller slot and quarantine a
   lane by hand) · merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice K kickoff prompt**
   (Unity atlas rebuild — read the roadmap row and the PoC atlas notes
   first) · flip the box below · push only on user say-so.

- [ ] Slice J complete
