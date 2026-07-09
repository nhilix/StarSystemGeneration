# Slice A Ledger — Foundations (slice-a-foundations)

Ordered task checklist for Slice A per the kickoff prompt
(`2026-07-09-slice-a-kickoff-prompt.md`). Updated as work proceeds — this is
the resumability record. New code lives in `src/Core/Epoch/`
(`StarGen.Core.Epoch`), fresh beside the untouched prototype.

## Tasks

- [x] 1. **Config** — `EpochSimConfig` with knob families (genesis / economy /
      sim), all rates per world-year, `YearsPerEpoch = 25`, `EpochCount = 40`
      defaults. Tests: defaults sane, rates world-year-denominated.
- [x] 2. **Event grammar v2** — `WorldEvent` record `(id, world-year, clock
      stratum, type, actors[], location, magnitude, valence, visibility,
      typed payload)`; enums `ClockStratum`, `EventFamily` (eight),
      `EventVisibility` (public/regional/secret); append-only `EventLog` with
      per-place and per-actor **views** (computed, never stored). Tests:
      append/id order, view correctness, family mapping.
- [x] 3. **Controller contract** — policy/act records per
      `frame/controller-contract.md` (polity, corporation, character);
      `IController.Decide(perceived) → (policies, acts)`; perfect-info
      `PerceptionView` stub; `TrivialController` default AI. Tests: trivial AI
      returns default policies + no acts; contract types round-trip.
- [x] 4. **Sim state + stub seeding** — `SimState` container (config, epoch
      index, world-year clock, actor registry, event log, phase trace);
      deterministic stub polity seeding from config (emergence-schedule
      placeholder, hash-rolled entry epochs; Slice B replaces). New
      `RollChannel` values appended, never reused. Tests: determinism of
      seeding, staggered entry.
- [x] 5. **Seven-phase engine** — Perception → Markets → Allocation → Intent →
      Resolution → Interior → Chronicle; phases execute + log trace; Intent is
      the only controller touchpoint; Chronicle finalizes staged events with
      world-years. Tests: phase order, world-year advance (epoch = 25y
      integration step), events finalized only in Chronicle.
- [x] 6. **Determinism gate** — same config ⇒ byte-identical event log + trace
      text across two independent runs; different seed ⇒ diverges.
- [x] 7. **REPL surface** — Inspector command `epoch <seed> [epochs]`: builds
      config, seeds state, steps the frame, prints phase/event trace +
      chronicle. Piped-stdin smoke check via bash printf.
- [x] 8. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [x] 9. **Gates**: `dotnet test` green incl. hex-tier suite (206/206) ·
      determinism byte-identity (incl. culture-flip) · REPL surface works
      (`epoch 42` piped smoke, 2 ms full run).
- [x] 10. **User gate: REPL eyeball acceptance** (phase/event trace of a
      stepped galaxy looks right). Accepted 2026-07-09.
- [x] 11. **Wrap-up**: merged on user nod · `docs/HANDOFF.md` updated · Slice B
      **and** C kickoff prompts written · kickoff checkbox flipped · not
      pushed (awaiting user say-so).

## Notes / surprises

- Engine class is named `EpochEngine`, not `EpochSim`, to avoid ambiguity with
  the still-in-tree prototype `StarGen.Core.Galaxy.EpochSim` (deleted in B).
- Records on netstandard2.1 need the `IsExternalInit` shim
  (`src/Core/Epoch/IsExternalInit.cs`).
- Review fix wave: invariant-culture rendering in `SimTraceView` (sv-SE U+2212
  negative sign broke byte-identity — culture-flip test added); `FamilyOf`
  range guard; trace pluralization ("1 polity enters" / "1 event finalized");
  Unity `.meta` files for `src/Core/Epoch` (src/Core is a live Unity package —
  metas are committed everywhere else, so new folders need them too).
- Actors entering during Interior get their first PerceptionView the *next*
  step (Perception runs first); their first Intent follows it. Tested.
- RollChannels 37–39 are slice-A stubs (emergence entry, seat, name); retire —
  never reuse — when B/F replace stub seeding.
