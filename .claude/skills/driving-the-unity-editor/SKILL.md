---
name: driving-the-unity-editor
description: Use when running any Unity-side gate in this repo — compile check, EditMode tests, AtlasSmoke screenshots, scene regen, or the multi-seed atlas grid — and when adding a new editor tool that automation should be able to call. Read BEFORE the first Unity command; the failure modes here are silent.
---

# Driving the Unity editor (CLI + pipeline package)

## Overview

Two ways to run a Unity gate in this repo:

| | Warm editor (`unity command …`) | Batchmode (`Unity.exe -batchmode …`) |
|---|---|---|
| Editor | must be **open** | must be **closed** |
| EditMode tests | **~1.5s** | ~50s |
| Atlas smoke (18 PNGs) | **~3s** | full editor launch |
| Status | the **working** path during a slice | the **canonical merge gate** |

Use warm for the fast inner loop. Use batchmode for the pre-merge gate, and
whenever the editor isn't up. Proven equivalent: the sorted `(test name, status)`
set is identical between the two (Slice UP, 2026-07-24).

**Version-stamped, pre-1.0, moves fast.** Verified against `unity` CLI
`1.0.0-beta.3` and `com.unity.pipeline` `0.4.0-exp.1`. Both moved a full version
in *two days* during the spike. Treat exact command surfaces as a dated
snapshot — re-check `--help` rather than trusting docs.unity.com, which lags
badly (26 of 37 top-level commands are missing from its reference page).

## The five traps — read these before anything else

Every one of these fails **silently or misleadingly**. They are the reason this
skill exists.

| # | Trap | What you see | Do this |
|---|---|---|---|
| 1 | **`key=value` args are ignored** | `success: true`, command runs with **defaults** | Args are **flag-style**: `--path X`, `--mode editor`. Never `path=X`. |
| 2 | **`menu` ignores `--timeout`, caps at 30s** | `COMMAND_FAILED … timed out after 30000ms` while the editor **finishes fine** | Confirm completion by **polling for the artifacts**, never by exit code |
| 3 | **`--project-path` is effectively mandatory** | `No Unity Editor instances found with reachable Pipeline servers` — even though it *is* running | Always pass `--project-path <repo>\unity` |
| 4 | **Editor without `-automated`** | modal popups can block a run mid-flight | Launch with `-automated`, always |
| 5 | **Server port drifts** across domain reloads (7800→7801→7802→…) | stale-port connection failures | Never cache the port; the CLI re-resolves per call |

Bonus: **`unity open` does not work detached** — it hangs as a live CLI process
and never spawns an editor. Launch `Unity.exe` directly.

## Setup

The CLI installs to `%LOCALAPPDATA%\Unity\bin` on the **user** PATH, so
already-open shells don't see it. Prefix **every** invocation:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Unity\bin;$env:PATH"; $env:UNITY_NO_BANNER=1
$P='C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity'
```

Launch the editor (once per session; ~30s to `ready` on a warm Library):

```powershell
Start-Process 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' `
  -ArgumentList '-projectPath',$P,'-automated'
```

Confirm it's up before driving it:

```powershell
unity status --format json          # want state=ready, note the port
unity pipeline list                 # want Pipeline=true, Server Reachable=true
```

If `unity status` shows nothing, the editor is still importing — poll, don't
assume failure.

**The package is not in a clean checkout.** `unity/Packages/manifest.json` is
gitignored, so a fresh clone/worktree lacks `com.unity.pipeline`. Restore it by
adding one line to `dependencies`:

```json
"com.unity.pipeline": "0.4.0-exp.1"
```

(or `unity pipeline install --project-path $P --package-version 0.4.0-exp.1`).
The editor assembly still compiles without it — `AtlasGrid`'s pipeline face is
behind `#if HAS_UNITY_PIPELINE` — you just lose `unity command atlas_grid`.

## The gates

```powershell
# compile — poll until completed
unity command recompile --project-path $P --format json
unity command recompile_status --project-path $P --format json
#   want: {"status":"completed","failed":false,"errors":[]}

# EditMode tests (16 in this project)
unity command run_tests --mode editor --project-path $P --timeout 600 --format json
#   read data.result.Summary → {Total, Passed, Failed, Skipped}

# fire an editor menu item (see trap 2 — poll for output, ignore the exit code)
unity command menu --path 'StarGen/Atlas Smoke Shots' --project-path $P --format json
unity command menu --path 'StarGen/Setup Atlas Scene' --project-path $P --format json

# the multi-seed atlas grid → PNGs + a self-contained contact sheet
unity command atlas_grid --project-path $P --format json
unity command atlas_grid --seeds 42,9091 --lenses trade,war --project-path $P --format json
```

`atlas_grid` args, all optional: `input` (default `runs/atlas-grid`) · `output`
(default `atlas-grid`) · `lenses` (of `galaxy,domains,trade,price,war,works` —
also sets column order) · `seeds` (accepts `42` or `seed-42`) · `width` · `height`
· `zoom` · `pitch` · `anchor` (`centroid|bounds|port:<n>`). Artifacts come from
the Inspector REPL: `epoch <seed> 40 21` then `esave runs/atlas-grid/seed-<n>.txt`.

**Discovery:** `unity list --project-path $P` shows every registered command with
its parameters, types and defaults (141 today) — `--format json` puts them under
`data.tools`, not `data.commands`. This is the reliable way to look up a
command's arguments.

## Batchmode fallback (the canonical merge gate — editor must be CLOSED)

```powershell
$U='C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe'
& $U -batchmode -quit -projectPath $P -logFile <repo>\unity\compile.log        # compile
& $U -batchmode -projectPath $P -runTests -testPlatform EditMode `
     -testResults <repo>\unity\test-results.xml -logFile <repo>\unity\test.log  # tests
& $U -batchmode -projectPath $P -executeMethod StarGen.AtlasView.EditorTools.AtlasGrid.RunFromCli  # grid
```

Exit codes lie — grep `error CS` in the log and read `test-results.xml`. Omit
`-nographics` for anything that renders. If it reports the project is already
open, **stop** — don't force; close the editor first.

## Adding a new automatable editor tool

Give it a `[MenuItem]`, a `RunFromCli()` twin, and a `[CliCommand]` face —
`AtlasGrid.cs` is the worked example.

```csharp
#if HAS_UNITY_PIPELINE
using Unity.Pipeline.Commands;

[CliCommand("my_tool", "What it does.")]
public static object RunFromPipeline(
    [CliArg("seeds", "Comma-separated seed filter.")] string seeds = null)
    => Run(new MyOptions { Seeds = seeds });
#endif
```

Rules learned the hard way:

- **Guard it with `#if HAS_UNITY_PIPELINE`.** The asmdef needs a `versionDefines`
  entry (`com.unity.pipeline` → `HAS_UNITY_PIPELINE`) plus `Unity.Pipeline` in
  `references`. Without the guard the assembly won't compile on a clean clone —
  and that assembly carries **every** Unity gate, so it breaks all of them.
- **Validation must THROW** (`ArgumentException`). Returning `{success:false}`
  still yields a `success:true` envelope and exit 0. Throwing gives a real
  400 + exit 6. Validate *before* creating output directories.
- **Return an anonymous object** — it serializes straight into `result`.
  Newtonsoft is not reachable from a consumer assembly, so typed DTOs with
  `[JsonProperty]` won't compile.
- Plain C# parameter defaults surface in `unity list`; no `DefaultValue=` needed.
- Coercion covers `string`/`int`/`float`/`bool`.
- Discovery is `TypeCache`-based and cached — a new command appears only after a
  `recompile`.

## Housekeeping

- **`AtlasSmoke`/`AtlasGrid` runs dirty `unity/Assets/Scenes/Atlas.unity`**
  (~650 lines of pure fileID renumbering from `AtlasViewSceneSetup.SetupScene()`).
  Semantically identical — `git checkout -- unity/Assets/Scenes/Atlas.unity`
  after capture runs unless you deliberately intend a scene rebuild.
- **`unity/ProjectSettings` churn stays uncommitted, always** (standing rule).
  A `-automated` editor flips `runInBackground`.
- Grid/smoke output is gitignored (`atlas-grid*/`, `atlas-smoke*.png`). A custom
  `--output` name outside that glob is **not** ignored.

## Full command surface

`docs/superpowers/specs/2026-07-24-unity-cli-pipeline-command-reference.md` —
all 141 pipeline commands categorized, every CLI `--help` verbatim, and the
divergences from docs.unity.com. A **dated snapshot**: re-verify against
`--help` before relying on it. Slice UP's ledger
(`docs/superpowers/plans/2026-07-24-slice-up-ledger.md`) holds the measurements
and how each trap was found.
