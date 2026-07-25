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
| UP2 | Map the command surface → committed reference doc | reference doc committed | ☐ |
| UP3 | `unity auth login` + `unity pipeline install`; append pipeline command inventory | `unity pipeline list` shows Installed | ◐ |
| UP4 | Prove warm-editor gates (`run_tests` ×3 vs batchmode baseline, `recompile`, `menu`) | deterministic across repeats (flakiness documented = finding) | ☐ |
| UP5 | Eyeball grid prototype (multi-seed atlas contact sheet) | user opens one HTML file, sees every seed | ☐ |
| UP6 | Verdict + wrap (HANDOFF, fable review, merge, push) | three-checkpoint protocol | ☐ |

## User checkpoints

1. **Scope nod** — ✅ accepted 2026-07-24.
2. **Eyeball** — the UP5 grid itself.
3. **Merge decision.**

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

### UP3 — pipeline package installed ◐ (editor-compile step pending)

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
