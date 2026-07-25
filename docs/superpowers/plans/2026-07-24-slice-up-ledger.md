# Slice UP ledger — local Unity Pipeline spike

**Branch** `slice-up-unity-pipeline` from main `9d51673`.
**Kickoff** `docs/superpowers/plans/2026-07-22-slice-up-kickoff-prompt.md`.
**Research** memory `unity-cli-pipeline-research-artifact` →
`https://claude.ai/code/artifact/ba6f69d5-5423-4ead-a795-9ea5aff0caab`.

This is a **spike**: success is knowledge + a working prototype, not polish.
A documented dead end is a successful outcome. Batchmode remains the canonical
merge gate for all slices unless UP6's verdict says otherwise.

## Hard constraints (user-set, non-negotiable)

- **LOCAL ONLY.** No cloud services, no Build/Pipeline Automation, no beta
  sign-ups. The ONE permitted account touchpoint is `unity auth login`. Anything
  beyond it (org enrollment, cloud project link, closed-beta approval) ⇒ **stop,
  document the wall, report**.
- **Serial atlas editing.** One editor, one session touching `unity/`.
- **Zero sim behavior.** No `src/Core` changes. `dotnet test` green untouched;
  seed-42 golden byte-untouched.
- **Everything pre-1.0.** Pin exact versions. Trust `--help` over docs.unity.com.

## Environment baseline (recorded at slice start, 2026-07-24)

- `unity` CLI: **NOT installed** (`Get-Command unity` → not found; not on PATH).
- Unity editor: 6000.5.2f1 (project target).
- `unity/Packages/manifest.json` + `packages-lock.json` are **gitignored** —
  `unity pipeline install` mutates local-only state; the manifest delta is
  recorded verbatim in UP3 so a fresh checkout can reproduce it.

## Task ledger

| # | Task | Gate | Status |
|---|---|---|---|
| UP1 | Install & pin the CLI (Windows route) | `unity editors -i --format json` finds 6000.5.2f1 | ✅ |
| UP2 | Map the command surface → committed reference doc | reference doc committed | ✅ |
| UP3 | `unity auth login` + `unity pipeline install`; append pipeline command inventory | `unity pipeline list` shows Installed | ✅ |
| UP4 | Prove warm-editor gates (`run_tests` ×3 vs batchmode baseline, `recompile`, `menu`) | deterministic across repeats (flakiness documented = finding) | ✅ |
| UP5 | Eyeball grid prototype (multi-seed atlas contact sheet) | user opens one HTML file, sees every seed | ✅ eyeball ACCEPTED |
| UP5b | Parameterize the grid as a pipeline CLI command | filtered runs work, validation fails loudly | ✅ |
| UP6 | Verdict + wrap (HANDOFF, fable review, merge, push) | three-checkpoint protocol | ☐ |

## User checkpoints

1. **Scope nod** — ✅ accepted 2026-07-24.
2. **Eyeball** — the UP5 grid itself — ✅ **ACCEPTED** 2026-07-24 ("overall this
   looks really good"), with the flexibility ask that became UP5b.
3. **Merge decision** — pending.

## Log

### 2026-07-24 — slice opened

Branch cut from main `9d51673` (clean). Scope nodded. Two known user-in-the-loop
pauses flagged at the nod: `unity auth login` (UP3, interactive browser) and
launching the editor for UP4's warm-editor gates.

### UP1 — CLI installed & pinned ✅

**Pinned version: `unity` CLI `1.0.0-beta.3`.** Note the version-scheme jump:
the 2026-07-22 research recorded **0.1.0-beta.7**, and the docs release-notes
page still tops out there (June 16 2026). The CDN beta channel now serves a
**1.0.0** beta line. *Two days of drift.* Confirms the kickoff's "trust `--help`,
pin everything" rule harder than expected.

**Channel: `beta` is the ONLY channel.** `latest.json` (stable) and
`latest-alpha.json` both return **404** on the CDN. There is no stable channel
to pin to.

**The Windows install route — the docs bug is real and confirmed.**
`https://docs.unity.com/en-us/unity-cli/use-unity-cli` shows, under a heading
literally titled **"Windows (PowerShell)"**, a *bash* one-liner:

```
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

which cannot work in PowerShell. The working route is the PowerShell script the
release notes reference:

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

**What we actually did** (reproducible, and better than the one-liner): downloaded
`install.ps1`, read it, then ran the local copy with an explicit version pin —

```powershell
.\install.ps1 -Target "1.0.0-beta.3" -Channel beta
```

`install.ps1` behavior, read from source: resolves a per-version manifest at
`<cdn>/<version>/latest.json`, downloads the binary, **SHA-256 verifies** it
(`ff9ef81ade1063041d25e2c549cc7ed14e96d446f4204400bf101b389f7b8502` for
`win32-x64` @ 1.0.0-beta.3), installs to **`%LOCALAPPDATA%\Unity\bin\unity.exe`**,
appends that dir to the **user** PATH only, broadcasts `WM_SETTINGCHANGE`.
**No admin rights, no elevation, no cloud call beyond the CDN.**

⚠ **PATH gotcha for any tooling in this repo:** already-open shells do NOT pick up
the PATH change. Every invocation in this slice prefixes
`$env:PATH = "$env:LOCALAPPDATA\Unity\bin;$env:PATH"`.

**Gate GREEN** — `unity editors -i --format json` returns 6000.5.2f1 (plus a
6000.3.10f1), both discovered from the existing Hub install; the CLI did not need
to install an editor.

### UP3 — pipeline package installed ✅

**No login was required — the one permitted account touchpoint went UNSPENT.**
`unity auth status` reported *already signed in* (`Jason Cohen
<jaacohn@hotmail.com>`); the CLI inherits the existing Unity Hub session. The
kickoff and the research both assumed `unity auth login` would be a hard
requirement with an interactive browser round-trip. It was not. **Zero cloud
touchpoints consumed by this slice.**

**Pinned package version: `com.unity.pipeline` `0.4.0-exp.1`.** Again ahead of
research (which recorded 0.3.1-exp.1). `unity pipeline list-versions` offers
0.4.0-exp.1 (latest), 0.3.1-exp.1, 0.3.0-exp.1, 0.2.0-exp.2.

```powershell
unity pipeline install --project-path <repo>\unity --package-version 0.4.0-exp.1
```

**Manifest delta — verbatim, and it is exactly one line.** `unity/Packages/
manifest.json` is gitignored, so this is the record needed to reproduce the
install on a fresh checkout/worktree:

```diff
     "com.unity.modules.xr": "1.0.0"
+    ,"com.unity.pipeline": "0.4.0-exp.1"
```

i.e. add `"com.unity.pipeline": "0.4.0-exp.1"` as the last entry of
`dependencies`. Nothing else in the manifest changed; `packages-lock.json` was
absent before the install. **Equivalent to editing the manifest by hand** — the
CLI is a convenience, not a requirement, for this step.

**Package resolved + compiled** via a batchmode pass (91.2s cold, 0 `error CS`);
`com.unity.pipeline@0.4.0-exp.1` landed in `Library/PackageCache`.

**Gate GREEN** — with the editor open, `unity pipeline list` reports
`Pipeline=true · Version=0.4.0-exp.1 · Server Reachable=true`, and
`unity status` shows `state=ready`.

**140 registered commands**, all in the `built-in` group (research estimated
"100+"). Full inventory captured to the UP2 reference doc §6. All three UP4 gate
commands present: `run_tests`, `recompile` (+`recompile_status`), `menu`.

### UP4 — warm-editor gates ✅ ALL THREE GREEN, deterministic

**⚠ The editor MUST be launched with `-automated`.** Without it the pipeline
server logs on startup: *"Editor is not in automated mode. Modal Pop up might
break continuous command workflow. Start the editor with -automated"*
(`EditorPipelineServer.cs:32`). The first launch omitted it; the editor was
relaunched with the flag before any gate ran, so no result below is polluted by
self-inflicted modal risk. **This flag belongs in any future wiring.**

Launch used (`unity open` did NOT work — see traps):
```powershell
Start-Process 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' `
  -ArgumentList '-projectPath','<repo>\unity','-automated'
```
Editor reached `state=ready` ~30s after launch (warm Library).

#### Gate 1 — `run_tests` (EditMode) ×3 vs batchmode baseline

| Path | Result | Wall clock |
|---|---|---|
| **Batchmode** `-runTests -testPlatform EditMode` (editor closed) | 16/16 passed | **49.9s** |
| **Warm** `unity command run_tests --mode editor` run 1 | 16/16 | **1.7s** |
| Warm run 2 | 16/16 | 1.5s |
| Warm run 3 | 16/16 | 1.4s |

**~30–38× faster.** And not just matching counts: the sorted
`(FullName, Status)` set of all 16 results was diffed and is **IDENTICAL** across
the three warm runs *and* against the batchmode `test-results.xml`. (An earlier
triple — 1.4/1.2/1.3s — ran before the arg-syntax trap below was found, so it
silently used the default `mode=all`; it also returned the same 16, because the
project has no PlayMode tests. Both triples agree.)

Add ~91s for the cold compile pass batchmode needs first, and the honest
comparison for a full "compile + EditMode" gate is **~141s cold vs ~1.5s warm**.

#### Gate 2 — `recompile` after touching a comment

Appended a comment line to `unity/Assets/Editor/AtlasSmoke.cs`, then
`unity command recompile` → polled `recompile_status`:
**`{"status":"completed","failed":false,"errors":[]}` in 42.3s.** Console
readable, clean result. Probe comment reverted immediately.

#### Gate 3 — `menu`-fire "StarGen/Atlas Smoke Shots" ×3

| Fire | Result | Wall clock |
|---|---|---|
| 1 | success, **18/18 PNGs** | 3.1s |
| 2 | success, 18/18 PNGs | 2.3s |
| 3 | success, 18/18 PNGs | 2.2s |

Editor survived all three (`unity status` still `ready` afterwards). This
previously cost a full cold editor launch per capture run.

#### Traps found (the durable half of this task)

1. **`unity command` argument syntax is FLAG-STYLE and undocumented.**
   `unity command --help` says only "args — Arguments for the command". Probed
   four forms against `menu`:
   | Form | Outcome |
   |---|---|
   | `unity command menu path=X` | **silently ignored** — `path: null`, listed all 668 items |
   | `unity command menu -- path=X` | ignored |
   | `unity command menu '{"path":"X"}'` | ignored |
   | **`unity command menu --path X`** | ✅ **executes** |
   The dangerous one is form 1: it **fails silently and successfully**
   (`success: true`), running the command with defaults. Any script using
   `key=value` will look like it worked. This is the single biggest footgun found.
2. **`unity open` does not work when launched detached.** It hung as a live
   `unity.exe` CLI process (58MB, 0.2s CPU) and never spawned an Editor. Launch
   `Unity.exe` directly.
3. **`--project-path` is effectively mandatory** for `unity command`. Auto-detection
   failed from a non-project CWD with `COMMAND_FAILED: No Unity Editor instances
   found with reachable Pipeline servers` — a misleading error, since the editor
   *was* running and reachable.
4. **The server port drifts across domain reloads** (observed 7800 → 7801 → 7802
   → 7800). The CLI re-resolves it per invocation, so this is invisible unless a
   script caches the port — don't.
5. **`menu` executes eagerly.** Probing arg syntax against
   `StarGen/Setup Atlas Scene` *ran* it and regenerated
   `unity/Assets/Scenes/Atlas.unity` (647±647 lines of GUID churn). Reverted;
   the seed-42 golden was asserted untouched throughout. Use a harmless menu path
   when probing.
6. **`--timeout` is ignored by `menu`; the client hard-caps at 30s.** A run
   longer than 30s returns `COMMAND_FAILED … timed out after 30000ms` **while the
   editor keeps going and finishes normally**. This is a client-side *reporting*
   bug, not a run failure — the 35.5s grid run reported failure and produced all
   36 PNGs. Any wiring must confirm completion by **polling for the artifacts**,
   never by trusting the menu call's exit code.

### UP5 — the eyeball grid ◐ (built, working; awaiting the user's eyeball)

**`unity/Assets/Editor/AtlasGrid.cs`** (~390 lines) — `[MenuItem("StarGen/Atlas
Grid")]` + a `RunFromCli()` twin, following the AtlasSmoke pattern exactly
(EnsureMaterial per layer, `StepEpochs(1)` before capture, the hand-rolled
`SetAndStyle` because `AtlasRoot.OnEnable` never runs in Edit mode).

**The seam that made it cheap:** `SimHost.ArtifactPath` is a public settable
property and `LoadArtifact()` reads it — so the grid is just *set path → load →
shoot → repeat*. No SimHost change was needed. **Zero sim behavior**, zero
`src/Core` edits, golden untouched.

- **Input:** `runs/atlas-grid/*.txt`, ordinal-sorted (row order must be identical
  on any machine). Six artifacts generated via the Inspector REPL
  (`epoch <seed> 40 21` + `esave`) for seeds 42, 7, 1234, 9091, 31337, 2718.
- **Output:** `atlas-grid/<seed>-<lens>.png` at 1200×750 (deliberately below
  AtlasSmoke's 1600×1000 — 36 thumbnails is a lot of bytes) + a self-contained
  `atlas-grid/index.html`. Both gitignored via a new `atlas-grid/` rule.
- **Six lenses:** galaxy · domains · trade · price · war · works. Each restores
  its visibility/mode/accent afterwards so lenses never bleed between cells.
- **A bad artifact does not abort the grid** — the row renders a LOAD FAILED cell
  and the run continues.
- **36 PNGs, 13.1 MB, 35.5s** for the whole six-seed sweep on the warm editor.
  Smallest 105 KB (nothing near the ~2 KB empty-frame signature).

**One design correction made after the first build.** The lens views initially
anchored on `Ports[0]`, copying AtlasSmoke. That is fine for a single known seed
but wrong for a grid: `Ports[0]` is an arbitrary port per world, and on seed 9091
it sits at the tip of a tendril, shoving that row's galaxy into a corner. Since
**the entire purpose of a grid is reading DOWN a column**, the anchor was changed
to the **centroid of all ports** — each world framed on the heart of its own
settled reach, so a column compares like with like. Verified by regenerating and
re-inspecting the previously-degenerate seed-9091 `domains` shot.

Per-seed vitals captured into the sheet (all year 1025, 40 epochs):

| seed | ports | lanes | fleets |
|---|---|---|---|
| 7 | 198 | 179 | 551 |
| 42 | 218 | 211 | 586 |
| 1234 | 215 | 209 | 582 |
| 2718 | 178 | 166 | 453 |
| 9091 | 219 | 207 | 620 |
| 31337 | 186 | 180 | 501 |

**Driven end-to-end through the warm-editor `menu` command** — the UP5 gate as
written. `unity command menu --path 'StarGen/Atlas Grid'`.

**EYEBALL ACCEPTED** (2026-07-24). User verdict: *"overall this looks really
good."* With the qualification that the individual frame content isn't
especially useful yet — correct, and expected: the grid is a **capability**, not
an investigation. Which produced UP5b.

### UP5b — the grid becomes a parameterized instrument ✅

**The user's feedback at the eyeball defined this task:** *"The set of lenses we
will want to look into, how many seeds, where to get the screen captures from
etc all should be flexible and can be determined each time we need to do an
eyeball investigation like this."*

A fixed prototype that must be code-edited per investigation is a demo. The
answer turned out to be the pipeline package's best feature: **a project can
register its own CLI commands with typed parameters.**

```powershell
unity command atlas_grid --seeds 42,9091 --lenses trade,war --zoom 0.5 `
  --project-path <repo>\unity --format json
```

Nine optional args, all defaulting to the eyeball-accepted behavior:
`input · output · lenses · seeds · width · height · zoom · pitch · anchor`.
`lenses` drives both which shots are taken **and** the contact sheet's column
order; `anchor` takes `centroid` (default) | `bounds` | `port:<index>`. The HTML
header records the parameters, so a saved sheet states how it was made. The
`MenuItem` and `RunFromCli` twins still run the all-defaults grid unchanged.

Verified independently by the slice session, not just by the implementer:
`atlas_grid` appears in `unity list` beside the 140 built-ins; a 3-seed × 3-lens
filtered run returned a structured `{success, seeds, lenses, pngCount:9,
failures:[]}` and wrote exactly those 9 PNGs; `--lenses trade,bogus` failed with
`400 Parameter Validation Failed … Valid lenses: galaxy, domains, trade, price,
war, works` and wrote nothing.

**The CliCommand mechanism — what a future session must know** (full detail in
the reference doc §6):
- Attributes live in `Unity.Pipeline.Commands`, assembly **`Unity.Pipeline`** —
  an asmdef-based assembly must list it in `references` explicitly.
- **Throwing is the failure channel.** `ArgumentException` → HTTP 400,
  `success:false`, exit 6. Returning `{success:false}` would leave the envelope
  `success:true` — so validation MUST throw. This is a *trustworthy* failure
  signal, unlike `menu`'s false timeout.
- Plain C# parameter defaults surface in `unity list`; no `DefaultValue=` needed.
- Return an **anonymous object** — it serializes straight into `result`.
  Newtonsoft reaches a consumer assembly only through `Unity.Pipeline`'s
  `precompiledReferences` with `overrideReferences: true`, so referencing the
  assembly does *not* give you Newtonsoft; a typed DTO with `[JsonProperty]`
  won't compile.
- Discovery is `TypeCache`-based and cached until domain reload — a new command
  appears only after `recompile`.

---

## UP6 — VERDICT

### The question the kickoff asked

> Is the warm-editor path deterministic and pleasant enough to (a) wire into
> slice-session Unity gates as the preferred path with batchmode fallback, and
> (b) grow the grid into the standard multi-seed taste gate?

### (a) Wire it in as the preferred path — **YES, ADOPT**, with a fallback kept

Deterministic on the evidence, and the speed difference is not marginal:

| Gate | Batchmode (editor closed) | Warm editor | Speedup |
|---|---|---|---|
| Compile check | 91.2s | 42.3s (`recompile`) | ~2× |
| EditMode tests | 49.9s | 1.4–1.7s | **~30×** |
| Atlas smoke (18 PNGs) | full editor launch | 2.2–3.1s | large |
| Compile + EditMode together | ~141s | ~44s | ~3× |

Determinism was checked properly, not by counts: the sorted `(test name, status)`
set across three warm runs is **identical to each other and to batchmode's
`test-results.xml`**. Three `menu` fires each produced 18/18 PNGs with the editor
surviving.

**Keep batchmode as the fallback, and keep it as the pre-merge gate for now.**
Everything here is pre-1.0 and moved *twice in two days* (CLI 0.1.0-beta.7 →
1.0.0-beta.3; package 0.3.1-exp.1 → 0.4.0-exp.1). The warm path should be the
*working* path during a slice, with batchmode retained for the merge gate until
the tooling stabilizes past 1.0.

**Non-negotiables for any wiring** (all learned the hard way here):
1. Launch the editor with **`-automated`**, or modal popups will break the run.
2. Use **flag-style args** (`--path X`). `key=value` is silently ignored *while
   reporting `success:true`* — the worst failure mode found in this spike.
3. Always pass **`--project-path`**; auto-detection fails with a misleading
   "no reachable Pipeline servers" error.
4. **Confirm completion by polling for artifacts**, never by the CLI's exit code
   — `menu` ignores `--timeout`, hard-caps at 30s, and reports a false failure
   while the editor finishes fine.
5. Never cache the server port; it drifts across domain reloads.

### (b) Grow the grid into the standard multi-seed taste gate — **YES**

UP5b already took it past prototype: it is a parameterized instrument, callable
per investigation without touching code. The eyeball gate can now be "here are
N seeds × M lenses of the thing you just changed" instead of "here is seed 42."

### The finding that most changes what's possible

**Not the speed — the fact that we can register our own commands.** `atlas_grid`
proves a project-defined, typed, self-documenting command can be driven from the
CLI against a warm editor. That is the difference between "Unity automation we
script around" and "Unity automation we extend." Any future atlas instrument
(diffing two artifacts, sweeping a knob visually, capturing a specific port's
domain across seeds) is now a ~30-line editor method plus an attribute.

### Cost, honestly

**Zero cloud touchpoints.** The one permitted `unity auth login` went unspent —
the CLI inherits the existing Hub session. Nothing was enrolled, linked, or
signed up for. The only machine-level change is `%LOCALAPPDATA%\Unity\bin` on
the user PATH, and one gitignored line in `unity/Packages/manifest.json`.

### Filed as follow-ups — NOT fixed in this slice

1. **`AtlasGrid`/`AtlasSmoke` dirty `unity/Assets/Scenes/Atlas.unity` on every
   run** (~650± lines of pure fileID renumbering from
   `AtlasViewSceneSetup.SetupScene()` rebuilding the object graph). Semantically
   identical, reverted by hand each time in this slice. Pre-existing AtlasSmoke
   behavior, not introduced here — but now that captures are cheap and will run
   *often*, it should be made idempotent (skip the rebuild when the scene is
   already current). **This is the top follow-up.**
2. **Wiring the gates** (CLAUDE.md command lines, worker instructions) is
   deliberately out of scope per the kickoff — it's the adopt-path slice.
3. **`unity mcp`** is a second, official MCP surface onto the same editor,
   parallel to the `unity-mcp` bridge already configured. Untouched here per the
   boundary. Worth a deliberate decision later about which to standardize on.
4. **`unity shell`** (a warm CLI REPL process, `--protocol ndjson`) was mapped
   but never exercised — a possible further latency win for batched calls.
5. **Version churn is a standing risk.** Re-verify the reference doc's command
   surface before trusting it in a future session; it is a dated snapshot of a
   pre-1.0 tool that moved twice in two days.
