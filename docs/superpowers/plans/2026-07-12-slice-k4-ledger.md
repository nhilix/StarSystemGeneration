# Slice K4 Ledger — Timeline

Branch `slice-k4-timeline` off main (`1c158de`, the K3 merge), in worktree
`.claude/worktrees/slice-k4-timeline` (contract-economy session runs
parallel — never a shared checkout). Governing plan:
`2026-07-11-slice-k-roadmap.md` row K4; kickoff:
`2026-07-12-slice-k4-kickoff-prompt.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md` §Time + diagram
§5 (time model) / §7 (TimeMachine, TimelineStrip) +
`docs/superpowers/specs/2026-07-12-ui-language-design.md` (the strip's
visual language).

Scope nod 2026-07-12: kickoff scope confirmed unamended — TimeMachine
(`src/Core/Atlas`, TDD: epoch keyframes as delta saves against the loaded
base via `DeltaSerializer.Diff/Apply`; snap-to-keyframe scrub re-queries
everything; chronicle/era queries stay log-backed), TimelineStrip (new
bottom host in AtlasChrome, cassette × ice: era bands via EraQueries,
event-density sparkline via EventLog, world-year scrubber, active-tick
marker), play/step coarse + fine (`EpochEngine` as-is at any
`YearsPerEpoch`; `GenerationYears` stays the calendar; play =
step-per-interval, the ewatch experience), resolution change forks a
branch from the current keyframe, SimHost run-seed in-editor
(artifact-load default stays). Boundary: no system stage (K5) ·
panels/lenses only refresh over time (panel bug found = fix, not feature)
· no new sim mechanics · controller eye stays a seam · per-lens
readability stays backlog.

Baseline at branch: **790/790** `dotnet test` in the fresh worktree
(gitignored `unity/Packages/manifest.json`, `packages-lock.json`,
`src/Core/csc.rsp` copied from the main checkout per the K3 trap list).

## Key surfaces (mapped at kickoff)

- `DeltaSerializer.Diff(baseText, liveText)` / `Apply(baseText, delta)` —
  byte-exact, fnv64 base check, events layer appends, genesis strata
  never re-record (DeltaTests.cs is the pattern; `EpochTestKit.Seeded`).
- `EpochEngine.Step(state)` integrates `state.Config.Sim.YearsPerEpoch`
  world-years; the estep pattern mutates `Config.Sim.YearsPerEpoch`
  in place (Repl.cs:320).
- Run-seed pattern (Repl.cs:117): `SkeletonBuilder.Build(new
  GalaxyConfig{MasterSeed, GalaxyRadiusCells})` →
  `EpochGenesis.Seed(skeleton, config)` → `EpochEngine().Run(state)`.
- `ArtifactSerializer.ToText(state)` / `Load(TextReader)`.
- `EraQueries.Eras(model, eye)` → EraRow(Name, Kind, Start/EndEpoch,
  Start/EndYear); `state.Log.Events` (WorldYear, Stratum) for density.
- SimHost raises `Loaded`; every K1–K3 layer + TopBar rebuild on it —
  re-raising after step/scrub is the integration (measure before
  inventing incremental refresh).
- AtlasChrome owns the one UIDocument + named hosts — the strip gets a
  new bottom host there, never a second UIDocument or pointer guard.

## Tasks

- [x] **T0 — Worktree + baseline**: `slice-k4-timeline` @ 1c158de,
      790/790, gitignored trio copied.
- [x] **T1 — TimeMachine, TDD** (`src/Core/Atlas/TimeMachine.cs`,
      7 tests): keyframe per epoch as Diff(base, live); ScrubTo
      byte-identical (branch resolution wins over the base's ESIM line
      on rebuild); Step from a scrubbed-back position REPLAYS recorded
      frames (never re-captures/truncates; cross-machine byte-identity
      asserted — resume-from-delta re-proven at TimeMachine level);
      SetResolution mid-run forks a TimelineBranch anchored at the
      current keyframe (before any step: retunes in place); goldens
      safe by construction (all in-memory). 797/797.
- [x] **T2 — TimelineQueries, TDD** (2 tests): EventDensity buckets the
      generational stream per generation, year 0 → live year, partial
      last bucket mid-generation (fine ticks); deep-time strata stay
      off the axis. Era bands stay EraQueries; keyframe marks stay
      TimeMachine. NOTE: the partial-bucket test passed first run — the
      behavior was over-built in cycle 1's green; test kept as the pin.
- [ ] **T3 — SimHost grows the writer role**: wraps TimeMachine
      (StepEpochs / ScrubTo / SetResolution / Play state), run-seed
      in-editor (the Repl seed pattern; artifact-load default stays);
      re-raises `Loaded` after step/scrub; DECIDE + document: dock
      re-Shows unpinned panels on `Loaded` (pinned keep their snapshot).
- [ ] **T4 — TimelineStrip UI** (new bottom host in AtlasChrome,
      `ssg-` classes, var() tokens only): era bands · sparkline ·
      scrubber + active-tick marker · play/pause + step buttons ·
      coarse/fine resolution control (fork indicator when branched) ·
      run-seed box; TopBar readouts shared, not duplicated.
- [ ] **T5 — EditMode tests where they pay** (strip builds, host wiring,
      no second guard).
- [ ] **T6 — Gates**: `dotnet test` green ×3 · golden untouched ·
      determinism untouched · EditMode green · AtlasSmoke renders every
      lens.
- [ ] **T7 — Fresh-eyes whole-branch review** + one fix wave.
- [ ] **T8 — USER: timeline eyeball** — watch 40 epochs animate on the
      domains lens; scrub back to a mid-war year; step fine.
- [ ] **T9 — Wrap-up**: merge · HANDOFF · tick K4 in the K roadmap ·
      write the K5 kickoff prompt · republish the living diagram if the
      time model taught us anything · push on say-so.

## Decisions / deviations

(recorded as they happen)

## Carried notes

- Credit-loop equilibrium flag rides the contract-economy slice (not K4).
- K1 runtime meshes/textures lack HideAndDontSave in edit mode — sweep
  opportunistically.
- Batchmode cannot run while the editor holds the project; stale
  test-results XML lies — delete before re-runs. Editor MCP bridge
  verifies compiles live. UI Toolkit never renders in batch captures —
  the animation eyeball is in-editor by nature.
