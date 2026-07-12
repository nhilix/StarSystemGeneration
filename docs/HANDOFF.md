# Session Handoff — 2026-07-12 (Slice K4, Timeline — MERGED)

State: `slice-k4-timeline` merged to `main` locally (not pushed — push
on say-so). Gates at merge: **800/800 dotnet** (×3 at review; goldens
untouched — K4 adds zero sim behavior) · **11/11 EditMode** (incl. new
SimHostTimeTests) · AtlasSmoke renders every lens (K3 census intact) ·
fresh-eyes whole-branch review ("with fixes, no criticals") + full fix
wave landed · **user eyeball ACCEPTED 2026-07-12** ("a lot of control")
after one fix wave. ProjectSettings churn stays uncommitted.

## Slice K4 — Timeline (closed)

Ledger `docs/superpowers/plans/2026-07-12-slice-k4-ledger.md` (per-task,
decisions, review findings, the eyeball wave).

- **Core** (`src/Core/Atlas`, TDD): **TimeMachine** — epoch keyframes
  captured as delta saves against the loaded base (`DeltaSerializer`;
  genesis strata never re-record), byte-identical snap-to-keyframe
  scrub (the branch's tick resolution wins over the base's ESIM line on
  rebuild), stepping from a scrubbed-back position REPLAYS recorded
  frames (cross-machine byte-identity proven), resolution change
  mid-run forks a `TimelineBranch` anchored at the current keyframe; a
  genesis-base (y0, unstepped) artifact round-trips — the run-seed
  flow. **TimelineQueries** — event-density sparkline buckets per
  generation (partial last bucket at fine tick; deep-time strata stay
  off the axis).
- **SimHost** (the one writer) grew StepEpochs / ScrubTo /
  SetResolution / RunSeed(seed, radius, epochs) + the play clock.
  **Two events**: `Loaded` = new world (camera refit, Open Threads
  greets, dock closes every panel — pins included) · `TimeChanged` =
  same world new moment (layers ShowAll without refit, lens re-applies,
  TopBar clock refreshes, dock rebuilds unpinned panels in place;
  PINNED panels keep their captured moment — comparison across time;
  missing subjects render the panel's placeholder, only a failing view
  closes). RunSeed bases the timeline on the **unstepped genesis world
  (y0)** and auto-plays to the target epoch — every keyframe captured,
  the map evolves from the start. CRLF normalized at the file boundary
  (Windows checkouts hold goldens as CRLF).
- **TimelineStrip** (bottom chrome host on the one AtlasChrome
  document, cassette × ice): era bands (kind-tinted, clamped to the
  axis), event sparkline, keyframe ticks, active marker; press/drag
  scrub snaps to the nearest keyframe (rebuild deferred during drag);
  transport |< < PLAY > >>; resolution chips (generation/5y/1y — a
  mid-run change forks, FORK chip); run-seed cluster SEED · R · EP
  (values survive play-tick rebuilds). Legend lifted to bottom 122px.

## Carried / flagged

1. **Credit-loop equilibrium** (from K3) → contract-economy slice
   (parallel session may be live in the root checkout).
2. Timeline **branch switch-back UI** — after a fork the root's
   recorded future is unreachable from the UI (TimeMachine already
   tracks ActiveBranch; SwitchBranch is trivial) — K5-or-backlog.
3. **Unbounded keyframe memory** during unattended play (a delta string
   per step) — fine for sessions, cap someday.
4. Per-lens readability deep-dives — backlog (gap list).
5. Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-K).
6. K1 runtime meshes/textures HideAndDontSave sweep — K5.

## Worktree / environment traps (real cost, verified again in K4)

- `unity/Packages/manifest.json`, `packages-lock.json`,
  `src/Core/csc.rsp` are gitignored — copy from the main checkout into
  any fresh worktree BEFORE Unity batch runs.
- Batchmode cannot run while an editor holds the project
  (`unity/Temp/UnityLockfile`); delete stale test-results XML.
- The editor MCP bridge's approval is per-project — a fresh worktree
  starts REVOKED (Project Settings > AI > Unity MCP).
- Main is usually checked out nowhere (root = parallel session):
  merge via a scratch worktree; `git worktree prune` stale entries.
- Goldens are CRLF in a Windows working tree — normalize before
  diffing file text against `ArtifactSerializer.ToText`.

## Next up

1. **Slice K5 (System stage & closeout)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-12-slice-k5-kickoff-prompt.md`
   (hex→system crossfade · SystemStage orbit view on the same
   selection/panels · PoC sweep · the K taste gate · roadmap close).
2. **Contract economy** — still queued (parallel session may be live):
   `docs/superpowers/plans/2026-07-11-contract-economy-kickoff-prompt.md`
   — carries the credit-loop equilibrium flag.
3. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; bash printf for REPL piping; parallel slices take
worktrees (never a shared checkout); every new `src/Core` file gets a
two-line `.meta` with a fresh guid; the design is the spec — deviations
amend `docs/design/` in-branch. The living atlas diagram
(`docs/diagrams/unity-atlas-design.html`) is republished to its stable
URL on change (§7 SimHost/TimelineStrip rows gained the two-event
split, genesis run-seed, and pinned-moments this slice). Unity gates:
`Unity -batchmode -runTests -testPlatform EditMode` + AtlasSmoke batch
twin (editor 6000.5.2f1).
