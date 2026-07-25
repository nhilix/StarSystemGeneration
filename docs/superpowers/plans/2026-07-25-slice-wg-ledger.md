# Slice WG ledger — wire the warm gates (and kill the scene churn)

**Branch** `slice-wg-warm-gates` from main `ca6d6f7`.
**Kickoff** `docs/superpowers/plans/2026-07-24-slice-wg-kickoff-prompt.md`.
**Predecessor** Slice UP (`2026-07-24-slice-up-ledger.md`) — proved the warm
path; WG makes it how the project actually works.

Small and mechanical. If it grows past "wiring + one idempotency fix", it drifted.

## Hard constraints

- **Zero sim behavior.** No `src/Core` / `src/Inspector` / `tests/` changes;
  `dotnet test` green untouched; seed-42 golden byte-untouched.
- **Batchmode must keep working** — it stays the canonical merge gate and the
  clean-clone path. Every change is "prefer warm, fall back", never "replace".
- **The editor assembly must compile WITHOUT `com.unity.pipeline`** (UP's
  Critical). Re-prove by removing the manifest line and compiling.
- **Serial atlas editing**; `unity/ProjectSettings` churn stays uncommitted.

## Task ledger

| # | Task | Gate | Status |
|---|---|---|---|
| WG1 | Make the scene setup non-destructive for capture runs | `atlas_grid` twice ⇒ `git status` clean | ✅ |
| WG2 | Wire CLAUDE.md gates to warm-preferred / batchmode-fallback | doc points at the skill, one source of truth | ✅ |
| WG3 | A sensible multi-seed eyeball default (not frozen) | documented in workflow + skill | ✅ |
| WG4 | One-command gate run — **only if it earns its place** | skip if WG2 makes it a 2-line copy-paste | ⊘ **skipped, deliberately** |
| WG5 | Wrap: ledger · HANDOFF · skill update · fable review · merge · push | three-checkpoint protocol | ◐ |

## User checkpoints

1. **Scope nod** — ✅ user chose WG over the UI design pass, 2026-07-25.
2. **Eyeball** — WG1's proof (a capture run leaving the tree clean) + whatever
   WG3's default recipe renders.
3. **Merge decision.**

## Log

### WG1 — the diagnosis (before touching anything)

`AtlasViewSceneSetup.Build()` unconditionally:

1. `EditorSceneManager.NewScene(EmptyScene, Single)` — discards what's open,
2. constructs the full object graph fresh,
3. **`EditorSceneManager.SaveScene(scene, ScenePath)`** — writes the asset,
4. `AddSceneToBuildSettings(...)` — mutates `EditorBuildSettings`.

Steps 3–4 are why every `AtlasSmoke`/`AtlasGrid` run dirties
`unity/Assets/Scenes/Atlas.unity` (~650± lines) **and** `ProjectSettings/
EditorBuildSettings.asset`. The diff is a pure fileID/ordering permutation —
verified in UP by normalizing the numbers: the added and removed line multisets
are identical.

**The docstring already claims "idempotent: every run starts from a fresh empty
scene".** That is idempotent in *semantics* but not in *bytes*, and that
conflation is the root of the confusion. New fileIDs are assigned on every
construction, so the serialized asset can never be byte-stable this way.

**Therefore the fix is NOT to stabilize fileIDs** (that fights Unity's
serializer). It is that **a capture run never needed the scene written to disk
at all** — it needs it *open and complete in memory*. Splitting those two
concerns removes the write, and with it the churn, entirely.

### WG1 — shipped ✅

`AtlasViewSceneSetup` split three ways, sharing one construction body:

| entry point | what it does | writes? |
|---|---|---|
| `BuildGraph()` | constructs the graph into the **active** scene | no |
| `Rebuild()` — behind `SetupScene()` / `RunFromCli()` | the old destructive+saving path, **unchanged** | **yes** (intended) |
| **`EnsureScene()`** — the capture path | complete? return · else open the committed scene, re-check · else build in memory + **log that the committed scene is stale** | **never** |

Completeness is one static `Type[]` beside the builder (22 entries) covering all
**20** types the two callers resolve via `FindAnyObjectByType` — a strict
superset, independently re-enumerated by the reviewer. A stale scene therefore
triggers a rebuild rather than a null-reference mid-capture.

**Gate GREEN, measured by the slice session, not just the implementer:** from a
clean tree, two `atlas_grid` runs left `unity/Assets/Scenes/Atlas.unity` at md5
`F31389718814CE2B722C7F6097B2D2F8` — identical before, after run 1, after run 2 —
and absent from `git status`. AtlasSmoke still yields 18/18 PNGs. The deliberate
`StarGen/Setup Atlas Scene` rebuild still saves (653±653 lines), now the *only*
path that does.

**Why the reopen path is safe at all:** every cross-reference the capture path
touches (`CameraRig.cam`, all 19 `AtlasRoot` refs, `SystemStage.root`,
`SimHost.artifactPath`) is `[SerializeField]`, so the committed scene carries a
fully-wired graph. Had any of that wiring been runtime-only, `EnsureScene` would
have needed a re-wire step. Worth remembering if wiring ever moves to code.

### WG2 — shipped ✅

CLAUDE.md gains a **Unity gates** section that points at the
`driving-the-unity-editor` skill as the single source of truth (command lines
live in exactly one place), plus a line in the slice-session gates step: start
the editor once at slice start, run gates warm, batchmode at the merge gate.

**Also corrected a badly stale claim, flagged to the user.** CLAUDE.md described
`unity/` as a *"superseded PoC atlas"* in both the header and a **Hard rule** —
but the replacement landed in K1–K5 and was caught up by AC. A fresh worker
reading that would reasonably skip Unity gates entirely. Reworded so the
greenfield rule still names the prototype *sim* and the *original hex-board* as
reference-only (its real intent, preserved), while stating that `unity/` is now
product. Reviewer independently confirmed the PoC `Scripts` dir was deleted in
K5 (`a132853`) and that the rewrite doesn't weaken the sim intent.

### WG3 — shipped ✅

The standard eyeball recipe lives in the skill: six seeds (42, 7, 1234, 9091,
31337, 2718 @ 40 epochs, radius 21), the regeneration one-liner (artifacts are
gitignored, so a fresh checkout must rebuild them), and the framing rule —
**scan down a column**. Explicitly *a starting point, not a fixed gate*, per the
user's steer at UP's eyeball.

### WG4 — skipped, deliberately ⊘

The kickoff said "only if it earns its place. Skip if WG2 already makes the gates
a two-line copy-paste." It does. A helper wrapping three commands would be a
framework for nothing.

## Pre-merge gates (all green)

| Gate | Result |
|---|---|
| `dotnet test` | **1301/1301** — unchanged from main |
| Seed-42 golden | **byte-untouched** (branch touches nothing under `src/`/`tests/`) |
| Unity compile (batchmode) | 0 `error CS` |
| EditMode — **warm** | **16/16 in 2.0s** (dogfooding the path this slice wires) |
| EditMode — **batchmode**, canonical merge gate | **16/16 Passed** |
| Compiles **without** `com.unity.pipeline` | ✅ re-proven — see below |
| Whole-branch fresh-eyes review (fable) | FIX-THEN-MERGE → wave applied |

**Clean-clone constraint re-proven** (the review correctly caught that this
slice's own hard constraint had no recorded outcome): manifest line removed,
editor closed, batchmode compile → exit 0, **0 `error CS`**, and the built
`StarGen.AtlasView.Editor.dll` contains `AtlasGrid` **True** / `EnsureScene`
**True** / `Unity.Pipeline` **False** / `CliCommand` **False**. Manifest restored.

## The fix wave (from the fable whole-branch review)

**FIX-THEN-MERGE — 0 Critical, 3 Important, 2 Minor.** The review verified WG1's
core independently (re-enumerated all 20 caller lookups, checked every wired
field is `[SerializeField]`, confirmed no disk writes remain on the capture
path) and then found three real defects:

**I3 — the one that mattered, and a second-order consequence I did not
anticipate.** By moving captures from a throwaway scene into the **committed**
one, WG1 made anything a capture creates persist in the real scene.
`SystemStage.Child()` created plain saveable GameObjects — K5's `HideAndDontSave`
sweep had covered meshes and materials but not these, *because pre-WG it could
not matter*. Fixed with `HideFlags.DontSave` on those children. Verified: a
`saveAsCopy` of the live in-memory scene contains **0** occurrences of
`StageRings`.

**I1** — the WG3 recipe failed on the exact case it documents. `runs/` is fully
gitignored, so on a fresh clone `runs/atlas-grid/` doesn't exist; `esave` is a
bare `File.WriteAllText` whose `DirectoryNotFoundException` is swallowed by an
`IOException` catch — it prints "cannot save: …" and the REPL still **exits 0**,
writing nothing. Fixed with `mkdir -p runs/atlas-grid &&`. (The swallowed
exception itself is `src/Inspector` and therefore out of scope — filed below.)

**I2** — a **false claim I wrote**: "seed 42 matches the golden's parameters".
It does not. The golden `SimHost` loads is **radius 12** (`GCONFIG|42|12|…`,
`GoldenTests.cs:21`); the recipe generates radius **21**. Same seed, different
galaxy — someone comparing a grid row against an atlas-smoke shot would have
chased the mismatch as a rendering bug. Corrected to "a familiar seed, not the
same world," with both radii stated.

**M1** — the missing-asset fallback skipped the unsaved-work guard (`NewScene`
would silently discard a dirty open scene). Guard hoisted to cover both
scene-replacing paths.

**M2** — this ledger was committed stale with every box unticked. Fixed by the
section you are reading.

### Residual, filed not fixed — component-state drift

The fix wave's honest finding, and a correction to the review's framing: the
scene was **never dirty-flagged**, before or after — programmatic
`new GameObject()` in edit mode without `Undo.RegisterCreatedObjectUndo` doesn't
set the flag. So the close-time "save modified scenes?" prompt never fires, and
the only exposure was always a manual Ctrl+S.

`HideFlags.DontSave` removes the **structural** junk. **Component-state drift
remains**, measured by diffing the post-fix in-memory scene against the committed
asset — **15 lines**: 10 × lens `MeshRenderer.m_Enabled → 0` (whichever lens shot
ran last), the camera transform at capture framing, and 3 × `ViewportPx 1080 →
1000`. A save/restore around the capture would close it; that is more than this
slice's "small and mechanical" scope allows.

**Documented rather than silently left**: both the skill and CLAUDE.md now say
captures no longer rewrite the *asset*, that the in-memory scene does diverge,
that nothing prompts, and **don't manually save after a capture**. The earlier
wording ("captures no longer touch the scene") overclaimed and was corrected.
