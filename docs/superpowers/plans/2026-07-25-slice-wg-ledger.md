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
| WG1 | Make the scene setup non-destructive for capture runs | `atlas_grid` twice ⇒ `git status` clean | ☐ |
| WG2 | Wire CLAUDE.md gates to warm-preferred / batchmode-fallback | doc points at the skill, one source of truth | ☐ |
| WG3 | A sensible multi-seed eyeball default (not frozen) | documented in workflow + skill | ☐ |
| WG4 | One-command gate run — **only if it earns its place** | skip if WG2 makes it a 2-line copy-paste | ☐ |
| WG5 | Wrap: ledger · HANDOFF · skill update · fable review · merge · push | three-checkpoint protocol | ☐ |

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
