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
- [ ] 5. **Seven-phase engine** — Perception → Markets → Allocation → Intent →
      Resolution → Interior → Chronicle; phases execute + log trace; Intent is
      the only controller touchpoint; Chronicle finalizes staged events with
      world-years. Tests: phase order, world-year advance (epoch = 25y
      integration step), events finalized only in Chronicle.
- [ ] 6. **Determinism gate** — same config ⇒ byte-identical event log + trace
      text across two independent runs; different seed ⇒ diverges.
- [ ] 7. **REPL surface** — Inspector command `epoch <seed> [epochs]`: builds
      config, seeds state, steps the frame, prints phase/event trace +
      chronicle. Piped-stdin smoke check via bash printf.
- [ ] 8. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [ ] 9. **Gates**: `dotnet test` green incl. hex-tier suite · determinism
      byte-identity · REPL surface works.
- [ ] 10. **User gate: REPL eyeball acceptance** (phase/event trace of a
      stepped galaxy looks right).
- [ ] 11. **Wrap-up**: merge on user nod · update `docs/HANDOFF.md` · write
      Slice B kickoff prompt (+ Slice C if useful) · flip kickoff checkbox ·
      push only on user say-so.

## Notes / surprises

- (running log — append as encountered)
