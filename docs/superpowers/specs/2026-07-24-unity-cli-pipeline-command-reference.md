# Unity CLI + Pipeline Command Reference

**Date**: 2026-07-24
**Pinned CLI version**: `1.0.0-beta.3` (beta channel — the only channel that currently
resolves; `stable`/`alpha` release manifests 404 as of this writing)
**Scope**: task UP2 of Slice UP — exhaustive `--help` inventory of the installed
`unity` CLI, gathered by actually running `unity <command> --help` for every
top-level command and every subcommand one and two levels deep, never by reading
the published docs.

> **Trust `--help`, not docs.unity.com.** The published CLI reference page and the
> release-notes page both lag the CDN. Everything in this document (except the
> explicitly-labeled "Divergences" section) was captured directly from the
> installed binary. Where this document and the web docs disagree, the installed
> `--help` wins — that's what actually runs.

This document is a durable artifact: the annotated table and the raw appendix are
meant to be greppable references for the rest of Slice UP (and beyond), not a
one-time research note.

---

## 1. Install & pin

**docs.unity.com bug**: the published "Windows (PowerShell)" install heading shows
a *bash* one-liner that cannot run in PowerShell:

```
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

The actual working PowerShell route (also on the same page, just mislabeled) is:

```powershell
$env:UNITY_CLI_CHANNEL = 'beta'
irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

**What we did instead** (more auditable, and pins an exact version): downloaded
`install.ps1`, inspected it, then ran it locally with an explicit target:

```powershell
.\install.ps1 -Target "1.0.0-beta.3" -Channel beta
```

Observed behavior of the script:
- SHA-256-verifies the downloaded binary against a published manifest before
  installing.
- Installs to `%LOCALAPPDATA%\Unity\bin\unity.exe`.
- Edits the **user** `PATH` environment variable only — no machine-wide change,
  no admin elevation needed.
- Only the `beta` channel manifest currently exists; `stable` and `alpha` channel
  manifests both 404'd when probed.

**PATH gotcha**: a shell open before the install ran (or before `PATH` propagates)
will not see `unity` on `PATH`. Every invocation in this session needed an
explicit prefix:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Unity\bin;$env:PATH"; $env:UNITY_NO_BANNER=1
```

New shells opened after a normal login should pick up the persisted user `PATH`
without the prefix, but don't assume it — verify with `unity --version` first.

---

## 2. Global options

These options appear on every command and subcommand (shown once here instead of
repeated per row below):

| Flag | Env var | Purpose |
|---|---|---|
| `-V, --version` | — | Output the CLI version number |
| `--format <format>` | `UNITY_FORMAT` | Output format: `human`, `json`, `tsv`, `ndjson` |
| `--json` | — | Shorthand for `--format json` |
| `--no-banner` | `UNITY_NO_BANNER` | Suppress the startup banner |
| `--non-interactive` | `UNITY_NON_INTERACTIVE` | Disable interactive prompts (CI/CD) |
| `--quiet` | `UNITY_QUIET` | Suppress informational output |
| `--verbose` | `UNITY_VERBOSE` | Full error details incl. stack traces on failure |
| `--proxy <url>` | `UNITY_PROXY` | HTTP/HTTPS/SOCKS/PAC proxy URL |
| `--proxy-disable` | — | Disable proxy for this invocation, ignoring all sources |
| `--log-proxy` | `UNITY_LOG_PROXY` | Log every outbound request to `proxy-request.json` |
| `--no-log-proxy` | — | Opt out of `--log-proxy` / env / persisted setting for one run |
| `-h, --help` | — | Display help for the command |

`--format ndjson` and `--format json` are worth remembering for anything we ever
script against (`status`, `list`, `pipeline list`, `command`).

---

## 3. Annotated command table

Legend for **Relevance**: our context is a deterministic C# galaxy-sim project
where Unity is *only* a read-only atlas viewer driven by CI-style gates (batchmode
compile check, 16 EditMode tests, `AtlasSmoke.RunFromCli` screenshot capture,
`AtlasViewSceneSetup.RunFromCli` scene regen). We want to drive a **warm** editor
process and fire menu items/tests against it — not manage installs, licenses, or
cloud projects by hand.

- **Relevant** — used or plausibly used by our CI/automation gates
- **Marginal** — could matter occasionally (editor/module bootstrap, project bookkeeping)
- **Irrelevant** — cloud, mobile-module, licensing-server, hub-GUI territory; not our project

| Command | Aliases | Purpose | Key flags | Relevance |
|---|---|---|---|---|
| `analytics` | — | Manage analytics/telemetry consent (`opt-in`, `opt-out`, `status`) | — | Irrelevant |
| `auth` | `a` | Sign in/out, check login state (`login`, `status`, `logout`) | `--client-id/--client-secret`, `--secret-from-stdin`, `--no-store` | Irrelevant (no cloud use) |
| `bug` | — | Report a bug to Unity's bug reporter | `--email`, `--title`, `--description`, `--steps`, `--reproducibility` | Irrelevant |
| `build` | — | Batchmode build via a required `--execute-method` | `--target`, `--execute-method` (both required), `-o/--output-path`, `--editor-version`, `--allow-install`, extensive Android signing flags, `--versioning-strategy` | Marginal — we don't ship builds today, but this is the template if we ever need a batchmode atlas build |
| `cache` | — | Manage the CLI's own download cache (`info`, `clean`) | — | Marginal (disk hygiene only) |
| `changelog` | — | Show release notes for the installed CLI | — | Marginal (version-drift checks) |
| `cloud` | — | Manage Unity Cloud orgs/projects (`status`, `org`, `project`) | `--cloud-org` | Irrelevant |
| `command` | `cmd` | **Execute a registered Editor command on a connected instance, or list available commands** | `--project-path`, `--runtime`, `--runtime-path`, `--timeout` (default 30s) | **Relevant** — this is how we'd fire `AtlasSmoke.RunFromCli` / `AtlasViewSceneSetup.RunFromCli` against a warm editor without relaunching it |
| `completion` | — | Print a shell completion script | shell: bash/zsh/fish/powershell | Marginal (nice-to-have for interactive use) |
| `config` | — | Persistent CLI config (`proxy`, `update-check`) | — | Irrelevant |
| `diagnose` | — | One-shot paste-safe diagnostic reports (`proxy`) | — | Marginal (support use only) |
| `doctor` | — | Print diagnostic info about the CLI environment | `--tail <lines>` | Marginal (troubleshooting) |
| `editor` | — | Manage **one** editor installation (`add`, `module`) | — | Marginal |
| `editors` | `e` | Manage/list Unity editors; richest of the install-management groups | `-r/--releases`, `-i/--installed`, `-a/--architecture`, `-w/--watch`; subcommands: `add`, `default`, `list`, `running`, `info`, `module`, `install-path`, `path`, `upgrade` | Marginal — `editors running`/`editors list` are handy sanity checks, not part of our automated gates |
| `env` | — | Print Unity Hub environment paths and version | — | Marginal |
| `hub` | — | Manage the Unity Hub application (`install`) | `-f/--force`, `--headless`, `--skip-signature-check`, `--hub-version` | Irrelevant (we don't drive Hub) |
| `install` | `i` | Install a Unity editor version | `-m/--module`, `--cm/--no-cm`, `--accept-eula`, `--dry-run`, `--resume`, `--list-components` | Marginal (one-time bootstrap) |
| `install-modules` | `im` | Install/list modules for an installed editor | `-e/--editor-version`, `-m/--module`, `-l/--list`, `--all`, `--reinstall`, `--retries` | Irrelevant to atlas-viewer work (mobile/console module territory) |
| `install-path` | `ip` | Get/set the global editor install path | `-s/--set`, `-g/--get` | Marginal |
| `language` | `lang` | Show/change CLI display language | `--set <code>` | Irrelevant |
| `license` | — | List/manage Unity licenses (`list`, `status`, `activate`, `return`, `server`) | `--serial`, `--personal`, `--floating`, `--file`, `--generate-request`, `--accept-eula` | Irrelevant (already licensed on this machine) |
| `list` | — | **List tools/commands registered on the connected Unity Editor by the Pipeline package** | `--project-path`, `--runtime`, `--runtime-path` | **Relevant** — discovery step before `command`/`run --command` |
| `logs` | — | Read/tail the Hub log file | `--tail`, `-f/--follow`, `--level` | Marginal |
| `mcp` | — | **MCP server + client config for Unity Editor** (`configure`) | `--project-path`, `--runtime`, `--runtime-path`; `configure [client]` supports claude, claude-code, cursor, vscode, windsurf, cline, codex, kiro, zed, continue, … | Marginal/Notable — see §6; distinct from the `unity-mcp` bridge server already listed in this session's tools |
| `modules` | — | List/manage editor modules globally (`list <version>`) | `-a/--architecture` | Irrelevant |
| `open` | — | Open a project with the correct Editor version | `--editor-version`, `-e/--editor-path`, `--build-target(-group)`, `--args` | Marginal — opens a GUI editor, which is what's currently starting up on this machine; not a headless/warm-editor path |
| `pipeline` | `pipe` | **Unity Editor Pipeline automation commands** (`install`, `upgrade`, `list`, `list-versions`) | `--project-path`, `--force`, `--package-version` | **Relevant** — this is exactly the package our project depends on for warm-editor automation; UP3 inventories its own command surface |
| `projects` | `p` | Manage Unity projects registered in the Hub | 17 subcommands: `list`, `add`, `remove`, `info`, `create`, `clone`, `link`, `new`, `open`, `pin`, `unpin`, `require`, `size`, `unlink`, `upgrade`, `export`, `import` | Marginal — `projects require`/`projects info` are useful CI sanity checks; the rest (`clone`, `link`, cloud/vcs plumbing) is Irrelevant |
| `releases` | — | List available Unity releases | `--lts`, `--stream`, `--since`, `--limit`, `--skip` | Marginal |
| `run` | — | Run a project in batch mode; forward args, or fire a **registered Editor command headlessly** via `--command` | `--editor-version`, `-e/--editor-path`, `--allow-install`, `--command <name>`, `--timeout` | **Relevant** — `--command` is the headless (cold-start) equivalent of `unity command` against a warm instance |
| `releases`/`self-uninstall` | — | Uninstall the CLI itself | `-y/--yes`, `--purge`, `--dry-run` | Irrelevant |
| `shell` | — | **Start a warm-process interactive REPL that runs many commands in one process** | `--protocol ndjson` | **Relevant** — the "keep the editor warm across many invocations" primitive; `--protocol ndjson` makes it machine-drivable |
| `status` | — | **Show live state of every connected Unity Editor** (port, project, version, PID, state) | `--port`, `--project-path` | **Relevant** — the first thing any automation should call to find the warm instance to target |
| `templates` | `t` | Browse/inspect/create/edit/delete project templates | `list`, `info`, `create`, `delete`, `location`, `edit` | Irrelevant (we don't spin up new projects from templates) |
| `test` | — | **Run EditMode/PlayMode tests and write an NUnit XML report** | `--mode`, `--filter`, `--output`, `--editor-version`, `--allow-install`, `--timeout` | **Relevant** — directly maps to our "16 EditMode tests" gate |
| `uninstall` | `u` | Uninstall an installed Unity editor | `-a/--architecture`, `-y/--yes` | Marginal |
| `upgrade` | — | Upgrade the `unity` CLI itself | `--check`, `--changelog`, `--channel`, `--target`, `--rollback`, `--dry-run` | Marginal — this is how we'd move off `1.0.0-beta.3` later |

### Notable subcommand details worth surfacing directly

- **`unity command`** (no subcommand given) lists every command currently
  registered on the connected editor — this is the live discovery mechanism for
  whatever `AtlasSmoke`/`AtlasViewSceneSetup` register via the Pipeline package.
- **`unity pipeline install/upgrade`** take `--project-path` (auto-detected if
  omitted) and `--package-version` to pin/override the installed Pipeline package
  version — this is the install path UP3 will inventory the far side of.
- **`unity mcp configure <client>`** supports `claude`, `claude-code`, `cursor`,
  `vscode`, `vscode-insiders`, `copilot-cli`, `windsurf`, `cline`, `codex`, `kiro`,
  `trae`, `openclaw`, `antigravity`, `zed`, `continue`, and `inspect` (launches the
  MCP Inspector in a browser). This is Unity's **own** MCP server/config writer —
  separate from the `unity-mcp` bridge server already wired into this session's
  tools (`mcp__unity-mcp__*`). Two different MCP surfaces exist side by side;
  worth being deliberate about which one any future automation talks to.
- **`unity editors module` / `unity editor module`** are identical in shape
  (`list`, `add`, `refresh`, `remove <version>`) — `editor` operates on a single
  registered install, `editors` is the plural/discovery-oriented sibling.
- **`unity license server`** (`status`, `list`) is about a *floating* license
  server — organizational licensing infra, not applicable to a single local
  Personal/Pro license.
- **`unity cloud org` / `unity cloud project`** are read/switch-only
  (`list`, `current`, `set-default`, `clear-default`) — no write operations were
  probed (out of scope: LOCAL ONLY).
- **`unity projects link/unlink`** are themselves 2-deep groups with `cloud` and
  `vcs` sub-subcommands — the only 3-level-deep command groups found in the
  surface (alongside `editor(s) module`, `license server`, `cloud org/project`).

---

## 4. Divergences from docs.unity.com

Per instructions, cross-checked against what the published reference page and
release-notes page claim.

### Commands in the binary but absent from the docs.unity.com reference page

The reference page documents **only**: `install`, `install-modules`, `uninstall`,
`editors`, `install-path`, `open`, `projects`, `auth`, `language`, `upgrade`,
`help`. Everything else captured in this document is **undocumented on that
page**, including entire command groups:
`analytics`, `bug`, `build`, `cache`, `changelog`, `cloud`, `command`,
`completion`, `config`, `diagnose`, `doctor`, `editor`, `env`, `hub`, `license`,
`list`, `logs`, `mcp`, `modules`, `pipeline`, `releases`, `run`, `self-uninstall`,
`shell`, `status`, `templates`, `test`.

That's 26 of 37 top-level commands (70%) missing from the "reference" page. The
release-notes page (topping out at 0.1.0-beta.7, 2026-06-16) does mention most of
these by name, so the binary has simply outpaced *both* docs pages, and the
reference page in particular is stale by an entire release generation.

### Commands claimed by release notes but NOT present in the installed binary

Probed directly (`unity <cmd> --help`, checking whether it errors as unknown vs.
falls through to a real command):

| Command | Result |
|---|---|
| `eval` | **Does not exist.** `unity eval --help` printed the top-level `unity --help` output verbatim (exit code 0) — commander's fallback-to-root behavior for an unrecognized token, not real command help. |
| `report-bug` | **Does not exist.** Same fallback-to-root behavior. (Its replacement is `unity bug`, which does exist and is documented above.) |
| `implode` | **Does not exist.** Same fallback-to-root behavior. |
| `completion` | **Exists.** Real subcommand help returned (`bash`/`zsh`/`fish`/`powershell`). |
| `releases` | **Exists.** Real subcommand help returned (`--lts`, `--stream`, `--since`, `--limit`, `--skip`). |

So of the five specifically flagged for verification, three (`eval`,
`report-bug`, `implode`) were retired/renamed between 0.1.0-beta.7 and
1.0.0-beta.3, while `completion` and `releases` persisted.

### Version drift

- 2026-07-22 research recorded: CLI `0.1.0-beta.7`, Pipeline package
  `0.3.1-exp.1`.
- Today (2026-07-24) the CDN serves: CLI `1.0.0-beta.3`, Pipeline package latest
  `0.4.0-exp.1` (per `unity pipeline list-versions`, to be captured in full by
  UP3).
- The CLI jumped a major version number (0.1.x → 1.0.0) across roughly two days
  and picked up/dropped several top-level commands in the process
  (`eval`/`report-bug`/`implode` gone; `mcp`, `shell`, `diagnose`, `command` groups
  either new or newly stable). Given this cadence, **re-verify this document's
  command surface before relying on it in a future session** — don't assume
  today's snapshot is still accurate a week out.

---

## 5. Notable finds

- **`unity shell`** — a warm interactive REPL process (or `--protocol ndjson` for
  a machine-driven request/response loop over stdio) that "runs many commands in
  one warm process." This is a strong candidate for keeping editor-adjacent CLI
  state warm across a batch of automation calls, distinct from keeping the
  *editor* warm (which is what `status`/`command`/`list` target instead).
- **`unity mcp`** — Unity's official MCP server (stdio) plus a config-writer
  (`mcp configure <client>`) for a long list of AI-agent clients. This is
  separate infrastructure from the `unity-mcp` bridge server already present in
  this session's tool list (`mcp__unity-mcp__Unity_RunCommand`,
  `Unity_GetConsoleLogs`, etc.) — two parallel MCP surfaces for talking to the
  same editor. Worth a deliberate decision later about which one (or both) our
  automation should standardize on.
- **`unity diagnose proxy`** and **`unity doctor`** — paste-safe/redacted
  diagnostic dumps clearly built for support tickets; the `--tail <lines>` on
  `doctor` pulls in recent log context automatically.
- **`ndjson` as a first-class format** — available via `--format ndjson` (or
  `unity shell --protocol ndjson`) — suggests the CLI is designed to be piped
  into other processes as an event stream, not just scripted for single-shot
  output.
- **Proxy plumbing is pervasive** — every single command carries
  `--proxy`/`--proxy-disable`/`--log-proxy`/`--no-log-proxy` as global options,
  plus a dedicated `config proxy` and `diagnose proxy`. Corporate-proxy support
  is clearly a first-class concern of this CLI, though irrelevant to our local
  setup.
- **`unity build`/`unity test`/`unity run` all share a spawn-model**: they each
  take `--editor-version`/`-e, --editor-path`/`-a, --architecture`/
  `--allow-install`, spawn the editor in batch mode, and (for `build`) require an
  explicit `--execute-method` because "Unity has no built-in command-line build."
  `run --command <name>` is the odd one out: it can invoke a **registered**
  editor command headlessly, args-after-`--` parsed against that command's own
  schema rather than forwarded to Unity — effectively a cold-start version of
  `unity command`.

---

## 6. Pipeline package command inventory (UP3)

**Package**: `com.unity.pipeline` **0.4.0-exp.1** (pinned in `unity/Packages/manifest.json`).
**Method**: `unity list --project-path unity --format json` (cross-checked against
the plain-text `unity list` table) against the warm editor already running this
project — never `--help` text, since these are runtime-registered Editor
commands, not CLI subcommands.
**Count**: **141** commands total — 140 shipped by the package itself plus one
this slice registered (`atlas_grid`, §6's own worked example, see below). Every
single entry, ours included, reports **`group: built-in`** — the package's
`list` output doesn't distinguish package-native from project-registered
commands by group; only knowing the source tells them apart.

The 141 fall into thirteen functional buckets. Columns: parameter **names**
only (see the Appendix-adjacent raw JSON capture for types/defaults/required
flags, not reproduced here to keep this scannable); one-line purpose.

### 6.1 Tests, compilation & scripting (12)

| Command | Parameters | What it does |
|---|---|---|
| `run_tests` | mode, filter, filter_type, include_explicit, async_tests, timeout | Execute EditMode/PlayMode tests with filtering — our 16-test gate runs through this. |
| `test_status` | — | Poll status of an async `run_tests` call. |
| `cancel_tests` | — | Cancel a running test execution. |
| `list_tests` | mode | List available tests without running them. |
| `recompile` | focus | Force a script recompile (works while unfocused/minimized). |
| `recompile_status` | — | Poll status of the last recompile: idle / triggered / compiling / completed / up_to_date. |
| `reload_file` | filename, timeout, assemblyDir, pdb | Compile and apply in-place `[HotReload]` edits from a source file. |
| `reload_file_override` | filename, timeout, assemblyDir | Compile and apply hot-reload file changes immediately (override variant). |
| `eval` | code, timeout | Evaluate C# code dynamically via the Roslyn compiler. |
| `eval_file` | file, timeout | Evaluate C# code read from a `.cs` file on disk. |
| `create_script` | name, path, namespace, base_class, overwrite | Create a new C# script from a template; the type doesn't exist until the next recompile. |
| `attach_script` | target, type, script | Add a MonoBehaviour by compiled type name or script asset path; retry after `recompile` if not yet compiled. |

### 6.2 Editor lifecycle & menus (7)

| Command | Parameters | What it does |
|---|---|---|
| `menu` | path | Execute an Editor menu item by path, or list every available item when no path is given. |
| `editor_play` | — | Enter Play mode. |
| `editor_pause` | — | Pause Play mode. |
| `editor_stop` | — | Exit Play mode. |
| `editor_focus` | — | Bring the Editor window to the foreground. |
| `editor_status` | — | Detailed Editor status/state snapshot. |
| `set_autotick` | enable, interval_ms | Keep the editor ticking while unfocused (forces `EditorApplication.SignalTick` at a throttled rate). |

### 6.3 Scenes & hierarchy (7)

| Command | Parameters | What it does |
|---|---|---|
| `create_scene` | path, additive, template | Create a new scene, saved under the authoring root. |
| `open_scene` | path, additive | Open an existing scene. |
| `save_scene` | path | Save an open scene (active scene if no path given). |
| `save_all` | — | Save every open scene with unsaved changes. |
| `list_open_scenes` | — | List open scenes with load/active/dirty state. |
| `set_active_scene` | path | Set which open scene new objects get created in. |
| `get_scene_hierarchy` | path | Return an open scene's GameObject tree (instanceId + hierarchyPath per node). |

### 6.4 GameObjects & components (20)

| Command | Parameters | What it does |
|---|---|---|
| `create_gameobject` | name, primitive, parent | Create an empty GameObject or a built-in primitive. |
| `create_gameobjects` | name, primitive, parent, count, positions, rotations, scales | Batch-create N GameObjects/primitives in one call. |
| `delete_gameobject` | target | Delete a GameObject (Undo-reversible). |
| `rename_gameobject` | target, name | Rename a GameObject. |
| `find_gameobjects` | name, tag, type, hierarchy_path, include_inactive | Find GameObjects by name/tag/component type/hierarchy path (filters combine). |
| `set_active` | target, active | Set a GameObject's `activeSelf`. |
| `set_parent` | target, parent, world_position_stays | Reparent under a new parent, or detach to scene root. |
| `set_transform` | target, position, rotation, scale | Set local position/rotation/scale; omitted channels are left unchanged. |
| `set_tag` | target, tag | Set a GameObject's tag (must already exist in the project). |
| `set_layer` | target, layer | Set a GameObject's layer by name or index (0-31). |
| `get_tags_layers` | — | Read the project's tags and named layers. |
| `set_tags_layers` | settings, confirm, dry_run | Add/remove tags, assign layer names (index 8-31). |
| `add_component` | target, type | Add a component by type name. |
| `remove_component` | target, type | Remove a component (by component handle, or GameObject handle + type). |
| `get_component_properties` | target, type | Get a component's serialized properties as a JSON map. |
| `set_component_properties` | target, properties, type | Set serialized properties on a component (one Undo step). |
| `get_serialized_fields` | target, field, component | Read serialized fields of a component/asset (object refs come back as reusable handles). |
| `set_serialized_field` | target, field, value, component | Set a serialized field, incl. array elements via `name.Array.data[i]`. |
| `get_selection` | — | Read the current Editor selection. |
| `set_selection` | instance_ids, paths | Set the Editor selection to given assets/scene objects. |

### 6.5 Prefabs (7)

| Command | Parameters | What it does |
|---|---|---|
| `instantiate_prefab` | prefab, scene_path, name | Instantiate a prefab asset into a loaded scene. |
| `create_prefab` | source, path | Save a GameObject as a prefab asset; the source becomes a connected instance. |
| `create_prefab_variant` | base, path | Create a prefab variant that inherits from a base prefab. |
| `apply_prefab_overrides` | instance | Apply an instance's overrides back to its source prefab asset. |
| `revert_prefab_overrides` | instance | Revert an instance's overrides so it matches its source prefab. |
| `unpack_prefab` | instance, completely | Unpack a prefab instance into plain GameObjects (outermost level or completely). |
| `save_prefab_contents` | prefab, rename_child, new_name, set_active_child, active | Edit a prefab in an isolated prefab stage and save back (nested-prefab safe). |

### 6.6 Assets & import (15)

| Command | Parameters | What it does |
|---|---|---|
| `create_asset` | path, type, shader, confirm, dry_run | Create a ScriptableObject (or other UnityEngine.Object) asset. |
| `delete_asset` | asset, confirm, dry_run | Delete an asset. Destructive. |
| `copy_asset` | asset, destination, confirm, dry_run | Copy an asset to a new path (fresh GUID). |
| `move_asset` | asset, destination, dry_run | Move (or rename via a new path) an asset. Preserves GUID. |
| `rename_asset` | asset, new_name, dry_run | Rename an asset in place (same folder, same GUID). |
| `import_asset` | source, path, confirm, dry_run | Import an external file into the project under the authoring root. |
| `find_assets` | type, name, label, search_in, limit | Find assets by type and/or name and/or label. |
| `get_import_settings` | asset, platform | Read an asset's import settings, structured by importer type. |
| `set_import_settings` | asset, settings, platform, dry_run | Set import settings on an asset and re-import it. |
| `create_folder` | path | Create a folder under the authoring root (creates intermediates). |
| `read_text_file` | path, max_bytes | Read a UTF-8 text file under the authoring root. |
| `write_text_file` | path, contents, confirm, dry_run | Write UTF-8 text to a file then import it; overwrite requires confirm. |
| `set_authoring_root` | root | Set the base folder bare authoring paths resolve/confine to. |
| `get_authoring_root` | — | Get the current authoring root. |
| `search` | query, limit | Run a Unity Search query (`t:Material`, `p: my asset`, `h: Main Camera`, ...). |

### 6.7 Build & platform (10)

| Command | Parameters | What it does |
|---|---|---|
| `build` | target, outputPath, profileName, options, scenes, confirm, dry_run | Trigger an async Player build; poll `build_status`. Destructive/long-running. |
| `build_status` | — | Status of the current/most recent build, with the full BuildReport once completed. |
| `switch_build_target` | target, confirm | Switch the active build target — full reimport + domain reload; poll `switch_build_target_status`. |
| `switch_build_target_status` | — | Status of the last target switch. |
| `list_build_targets` | — | List known BuildTarget values, their group, and whether support is installed. |
| `list_build_profiles` | — | List Build Profile assets (Unity 6 only). |
| `get_build_settings` | — | Read the current build configuration. |
| `set_build_settings` | settings, confirm, dry_run | Set mutable build settings fields (not the scene list, not the target). |
| `add_scene_to_build` | path, enabled | Add a scene to the Build Settings scene list (idempotent). |
| `remove_scene_from_build` | path | Remove a scene from the Build Settings scene list (idempotent). |

### 6.8 Packages (6)

| Command | Parameters | What it does |
|---|---|---|
| `package_add` | identifier, confirm, dry_run, wait | Add a UPM package by name@version / git URL / `file:` path; async, poll `package_status`. |
| `package_remove` | name, confirm, dry_run, wait | Remove a UPM package; async, poll `package_status`. |
| `package_resolve` | — | Resolve/refresh packages from the manifest; may trigger a recompile. |
| `package_status` | — | Status of the last async package op (add/remove/resolve). |
| `package_list` | scope, include_indirect, offline | List packages: installed (default) / available (registry) / all. |
| `package_search` | query, offline | Search packages available in the registry. |

### 6.9 Rendering, lighting & baking (23)

| Command | Parameters | What it does |
|---|---|---|
| `get_material_properties` | material | Read a material's shader, render queue, keywords, and property values. |
| `set_material_properties` | material, shader, properties, renderQueue, enableKeywords, disableKeywords, confirm, dry_run | Set shader properties/queue/keywords on a material, optionally reassign the shader. |
| `get_shader_properties` | shader, material | Introspect a shader's declared property list (by shader name or off a material). |
| `list_shaders` | filter, includeBuiltin, limit | Discover available shaders (for picking a valid name). |
| `get_lighting_settings` | — | Read active LightingSettings (lightmapper, bounces, resolution, AO, ...). |
| `set_lighting_settings` | settings, dry_run | Apply a subset of lighting settings. |
| `bake_lighting` | confirm, dry_run | Async lightmap bake via `Lightmapping.BakeAsync()`; poll `lighting_bake_status`. |
| `lighting_bake_status` | — | Status of the last lighting bake. |
| `cancel_lighting_bake` | — | Cancel an in-progress lighting bake. |
| `clear_baked_lighting` | confirm, include_disk_cache, dry_run | Clear baked lightmap data. Destructive. |
| `bake_occlusion_culling` | smallest_occluder, smallest_hole, backface_threshold, confirm, dry_run | Async occlusion-culling bake; poll `occlusion_bake_status`. |
| `occlusion_bake_status` | — | Status of the last occlusion bake. |
| `cancel_occlusion_bake` | — | Cancel an in-progress occlusion bake. |
| `clear_occlusion_culling` | confirm, dry_run | Clear baked occlusion-culling data. Destructive. |
| `bake_navmesh` | confirm, dry_run | Async legacy NavMesh bake; poll `navmesh_bake_status`. |
| `navmesh_bake_status` | — | Status of the last NavMesh bake. |
| `cancel_navmesh_bake` | — | Cancel an in-progress NavMesh bake. |
| `clear_navmesh` | confirm, dry_run | Clear the baked NavMesh. Destructive. |
| `bake_navmesh_surfaces` | — | Bake NavMeshSurface components (AI Navigation package); v1 stub — `package_not_found` if the package is absent. |
| `get_navmesh_settings` | — | Read the default agent's legacy NavMesh bake settings. |
| `set_navmesh_settings` | settings, dry_run | Apply a subset of legacy NavMesh bake settings. |
| `get_graphics_settings` | — | Read GraphicsSettings (default render pipeline). |
| `set_graphics_settings` | settings, confirm, dry_run | Set the default render pipeline asset. |

### 6.10 Capture / screenshots (3)

| Command | Parameters | What it does |
|---|---|---|
| `capture_scene_view` | width, height, save_path, include_inline_image, max_resolution | Render the active Scene View to a PNG (inline base64 or to a file). |
| `capture_game_view` | width, height, camera, save_path, include_inline_image, max_resolution | Render a camera to a PNG (inline base64 or to a file). |
| `screenshot` | view, output, width, height | Capture the Scene or Game view as a PNG, returning its file path. |

### 6.11 Animation & timeline (14)

| Command | Parameters | What it does |
|---|---|---|
| `get_animation_clip` | clip, includeKeys | Read an AnimationClip's metadata and float curve bindings. |
| `create_animation_clip` | path, frameRate, loop, confirm, dry_run | Create an empty `.anim` AnimationClip. |
| `set_animation_curve` | clip, path, type, property, keys, dry_run | Add or replace a float curve binding on a clip (overwrites, doesn't duplicate). |
| `remove_animation_curve` | clip, path, type, property, confirm, dry_run | Remove a float curve binding. Destructive. |
| `get_animator_controller` | controller | Read an AnimatorController's parameters/layers/states/transitions. |
| `create_animator_controller` | path, confirm, dry_run | Create a `.controller` asset with a default Base Layer. |
| `add_animator_layer` | controller, name, weight, blendingMode, dry_run | Add a layer to an AnimatorController. |
| `add_animator_parameter` | controller, name, type, defaultValue, dry_run | Add a Float / Int / Bool / Trigger parameter. |
| `add_animator_state` | controller, layer, name, motion, isDefault, position, dry_run | Add a state to a layer, optionally as its default. |
| `add_animator_transition` | controller, layer, fromState, toState, conditions, hasExitTime, exitTime, duration, hasFixedDuration, dry_run | Add a transition between states/AnyState/Entry/Exit, with conditions. |
| `get_timeline` | timeline | Read a TimelineAsset's frame rate, duration, tracks and clips. |
| `create_timeline` | path, frameRate, confirm, dry_run | Create a `.playable` TimelineAsset. |
| `add_timeline_track` | timeline, trackType, name, parentTrack, dry_run | Add a track (Animation/Audio/Activation/Control/Playable/Signal/Marker). |
| `add_timeline_clip` | timeline, track, start, duration, asset, dry_run | Add a clip to a named track. |

### 6.12 Settings & diagnostics (16)

| Command | Parameters | What it does |
|---|---|---|
| `get_quality_settings` | — | Read QualitySettings (level, vSync, anti-aliasing). |
| `set_quality_settings` | settings, confirm, dry_run | Change QualitySettings. |
| `get_time_settings` | — | Read Time settings (fixedDeltaTime, maximumDeltaTime, timeScale). |
| `set_time_settings` | settings, confirm, dry_run | Change Time settings. |
| `get_physics_settings` | — | Read Physics settings (gravity, solver iterations, bounce threshold). |
| `set_physics_settings` | settings, confirm, dry_run | Change Physics settings. |
| `get_audio_settings` | — | Read project Audio settings (volume, rolloff scale, doppler factor). |
| `set_audio_settings` | settings, confirm, dry_run | Change project Audio settings. |
| `get_input_settings` | — | Read the legacy Input Manager axes. |
| `set_input_settings` | settings, confirm, dry_run | Tune a legacy Input Manager axis (sensitivity/gravity/dead). |
| `get_player_settings` | — | Read PlayerSettings (company/product/version, scripting backend, API level). |
| `set_player_settings` | settings, confirm, dry_run | Change PlayerSettings; scripting backend/API level changes trigger a domain reload. |
| `get_performance_stats` | — | Read render/memory/frame-timing stats. |
| `get_console_logs` | severity, limit | Read recently captured Editor console logs (structured). |
| `clear_console` | — | Clear the captured log buffer and the Editor console. |
| `console` | tail, level, since | Get captured console output (Editor or Player); supports tail, level filter, follow via cursor. |

### 6.13 Project-registered (ours) (1)

| Command | Parameters | What it does |
|---|---|---|
| `atlas_grid` | input, output, lenses, seeds, width, height, zoom, pitch, anchor | Shoot the atlas contact sheet: every artifact x chosen lenses -> PNGs + a self-contained `index.html`. |

13 tables, 12 + 7 + 7 + 20 + 7 + 15 + 10 + 6 + 23 + 3 + 14 + 16 + 1 = **141** — every
command the package (plus our one addition) registers, none dropped.

### Safety gates

- **`confirm=true` required (29 commands, all destructive or non-undoable)**:
  `delete_asset`, `import_asset` (only when overwriting), `copy_asset`,
  `create_asset`, `create_animation_clip`, `create_animator_controller`,
  `create_timeline`, `remove_animation_curve`, `write_text_file` (only when
  overwriting), `package_add`, `package_remove`, `build`, `switch_build_target`,
  `bake_lighting`, `bake_navmesh`, `bake_occlusion_culling`,
  `clear_baked_lighting`, `clear_navmesh`, `clear_occlusion_culling`,
  `set_audio_settings`, `set_build_settings`, `set_graphics_settings`,
  `set_input_settings`, `set_material_properties`, `set_physics_settings`,
  `set_player_settings`, `set_quality_settings`, `set_tags_layers`,
  `set_time_settings`.
- **`dry_run` supported (40 commands)**: the 29 above minus `switch_build_target`
  (the one confirm-gated command with **no** dry-run preview — it's destructive
  *and* long-running with no way to rehearse it first), plus 12 more that offer
  a preview without being confirm-gated: `add_animator_layer`,
  `add_animator_parameter`, `add_animator_state`, `add_animator_transition`,
  `add_timeline_clip`, `add_timeline_track`, `move_asset`, `rename_asset`,
  `set_animation_curve`, `set_import_settings`, `set_lighting_settings`,
  `set_navmesh_settings`.
- **Async, poll-with-`*_status` pattern (8 operation families)**: `recompile` →
  `recompile_status`; `build` → `build_status`; `switch_build_target` →
  `switch_build_target_status`; `bake_lighting` → `lighting_bake_status` (+
  `cancel_lighting_bake`); `bake_navmesh` → `navmesh_bake_status` (+
  `cancel_navmesh_bake`); `bake_occlusion_culling` → `occlusion_bake_status` (+
  `cancel_occlusion_bake`); `package_add`/`package_remove`/`package_resolve` →
  `package_status`; `run_tests` → `test_status` (+ `cancel_tests`). Every one of
  these returns immediately (`in_progress`/`queued`/`triggered`) and expects the
  caller to poll rather than block — `wait=true` on the two package commands is
  the only opt-out into blocking behavior seen anywhere in the surface.

### What we'd actually use

Our project is a deterministic C# galaxy sim where Unity is **only** a
read-only atlas viewer — no gameplay, no builds, no shipped player. Our actual
gates are: compile check, the 16 EditMode tests, atlas screenshot capture, and
scene regen. Against the 141-command surface above, that maps to a five-command
working set:

- `recompile` + `recompile_status` — the compile-check gate.
- `run_tests` + `test_status` (+ `cancel_tests` if a run hangs) — the EditMode
  test gate.
- `menu` — fires `AtlasSmoke.RunFromCli` / `AtlasViewSceneSetup.RunFromCli` by
  path against the warm editor, exactly as §5 anticipated.
- `atlas_grid` — our own registered command; the actual atlas screenshot
  capture gate, superseding ad hoc use of `capture_scene_view`/`screenshot`
  for that job (though those two remain useful for one-off ad hoc captures).
- `unity status` / `unity list` (CLI level, §3) — discovery: find the warm
  instance, confirm `atlas_grid` is registered after a recompile.

The remaining ~136 commands are real surface but not ours: prefabs (7),
animation/timeline (14), lighting/occlusion/navmesh baking (23),
build/platform (10), packages (6), most of GameObjects/components (20) and
Assets/import (15) are all authoring or shipping-product concerns for a
project that *has* runtime GameObjects, prefabs, and a player build. We have
none of that — the sim lives entirely in `src/Core`, and the Unity side exists
solely to render a fixed set of lenses over already-simulated state for a
human to look at. `screenshot`/`capture_scene_view`/`capture_game_view` are
close cousins of `atlas_grid` but ad hoc rather than driving our own
deterministic contact-sheet path.

### Registering our own commands

`atlas_grid` (`unity/Assets/Editor/AtlasGrid.cs`) is the worked proof that a
project can add to this surface, not just consume it — the most valuable
finding of UP3.

- **`[Unity.Pipeline.Commands.CliCommand(name, description)]`** on a **static**
  method registers it. Optional constructor flags: `MainThreadRequired`
  (default `true` — set `false` only for genuinely thread-safe work) and
  `RuntimeOnly` (default `false`).
- **`[Unity.Pipeline.Commands.CliArg(name, description)]`** on each parameter
  documents it for `unity list`; it exposes `Required` and `DefaultValue`
  properties, but **plain C# parameter defaults already surface correctly** —
  `atlas_grid`'s `width = DefaultWidth` shows up as `default` in `unity list`
  with no `DefaultValue=` set on the attribute. Don't bother setting it twice.
- **The assembly is `Unity.Pipeline`** (the package's Runtime asmdef). Any
  asmdef-based assembly that wants to register commands must reference it
  explicitly — `unity/Assets/Editor/StarGen.AtlasView.Editor.asmdef` now lists
  `Unity.Pipeline` in `references` alongside `StarGen.AtlasView` and
  `StarGen.Core`.
- **Throwing is the failure channel.** An `ArgumentException` (see
  `AtlasGrid.Validate`) comes back as HTTP 400 "Parameter Validation Failed",
  `success:false`, CLI exit code 6. Returning an object with a `success:false`
  field instead would still leave the outer envelope `success:true` —
  validation failures **must** throw, never return a soft-fail shape.
- **Return type is unconstrained.** `RunFromPipeline` returns `object`, and an
  anonymous object serializes straight into the response's `result`. Prefer
  anonymous objects over hand-rolled DTOs — reaching for `Newtonsoft.Json`
  won't help: it only becomes visible to a consumer assembly through
  `Unity.Pipeline`'s own `precompiledReferences` with `overrideReferences:
  true`, so merely referencing `Unity.Pipeline` does **not** hand you
  Newtonsoft.
- **Type coercion** covers `string`/`int`/`float`/`bool` from CLI text
  arguments (plus arrays, seen in several package-native commands like
  `create_gameobjects`'s `positions`/`rotations`/`scales`).
- **Discovery is `TypeCache`-based and cached until a domain reload** — a
  newly-added `[CliCommand]` only appears in `unity list` after `recompile`
  completes. This is why `atlas_grid` wasn't in the pre-registration capture
  taken earlier in this session and had to be re-captured for this section.
- **Worked example**:
  ```
  unity command atlas_grid --seeds 42,9091 --lenses trade,war --project-path <path>
  ```

---

## Appendix: full raw `--help` output

Raw, unedited `--help` text for every command and subcommand captured, in
top-level command order (grouped subcommand help immediately follows its parent).
This is the durable source-of-truth half of the document — re-generate it whenever
the CLI version changes materially (see §4, version drift).

```
Usage: unity [options] [command]

CLI for Unity

Options:
  -V, --version                              output the version number
  --format <format>                          Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json                                     Shorthand for --format json
  --no-banner                                Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive                          Disable interactive prompts. Useful in CI/CD environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                                    Suppress informational output (env: UNITY_QUIET)
  --verbose                                  Show full error details including stack traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                              HTTP/HTTPS/SOCKS/PAC proxy URL. Examples: http://user:pass@host:8080, socks5://host:1080, pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable                            Disable proxy for this invocation, ignoring all sources
  --log-proxy                                Log every outbound request to proxy-request.json for this run. Off by default; typically used once when reproducing a proxy issue for support. Also settable via UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy                             Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted user setting for this run. Use when logging is enabled globally but you want one clean invocation.
  -h, --help                                 display help for command

Commands:
  analytics                                  Manage analytics and telemetry consent
  auth|a                                     Sign in, check login state, or sign out
  bug [options]                              Report a bug directly to the Unity bug reporter
  build [options] [project]                  Build a Unity project from the command line. Spawns the editor in batch mode and forwards conventional CI flags.
  cache                                      Manage the download cache
  changelog                                  Show release notes for the installed CLI
  cloud                                      Manage Unity Cloud organizations and projects
  completion <shell>                         Print a shell completion script
  config                                     View or change persistent CLI configuration
  diagnose                                   One-shot diagnostic commands for support — paste-safe, redacted output
  doctor [options]                           Print diagnostic info about your Unity CLI environment
  editor                                     Manage a Unity editor installation
  editors|e [options]                        Manage Unity editors
  env                                        Print Unity Hub environment paths and version
  hub                                        Manage the Unity Hub application
  install|i [options] [version]              Install a Unity editor
  install-modules|im [options]               Install or list modules for an installed editor (fully interactive when no arguments provided)
  install-path|ip [options]                  Set or get the editor install path
  language|lang [options]                    Show or change the CLI display language
  license                                    List the Unity licenses active on this machine
  list [options]                             List tools available on the connected Unity Editor (commands registered by the Pipeline package)
  logs [options]                             Read and tail the Hub log file
  mcp [options]                              MCP server and client configuration for Unity Editor
  modules                                    List and manage Unity editor modules
  pipeline|pipe                              Unity Editor Pipeline automation commands
  projects|p                                 Manage Unity projects in the Hub registry
  command|cmd [options] [command] [args...]  Execute commands on connected Unity Editor instances, or list available commands
  templates|t                                Browse, inspect, create, edit, and delete Unity project templates
  test [options] [project]                   Run a project's EditMode/PlayMode tests in the editor and write a results report
  open [options] [project]                   Open a Unity project with the correct Editor version
  run [options] [project]                    Run a Unity project in batch mode and forward args to the editor
  releases [options]                         List available Unity releases
  self-uninstall [options]                   Uninstall the unity CLI (removes the binary and environment files)
  shell [options]                            Start an interactive shell (REPL) that runs many commands in one warm process
  status [options]                           Show live state of every connected Unity Editor (port, project, version, PID, state)
  uninstall|u [options] [version]            Uninstall an installed Unity editor
  upgrade [options]                          Upgrade the unity CLI to the latest version
  help [command]                             display help for command
```

```
===== unity analytics --help =====
Usage: unity analytics [options] [command]

Manage analytics and telemetry consent

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  opt-in             Enable anonymous usage data collection
  opt-out            Disable anonymous usage data collection
  status             Show current analytics consent status
  help [command]     display help for command
```

```
===== unity analytics opt-in --help =====
Usage: unity analytics opt-in [options]

Enable anonymous usage data collection

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity analytics opt-out --help =====
Usage: unity analytics opt-out [options]

Disable anonymous usage data collection

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity analytics status --help =====
Usage: unity analytics status [options]

Show current analytics consent status

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity auth --help =====
Usage: unity auth|a [options] [command]

Sign in, check login state, or sign out

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  login [options]    Sign in to your Unity account (opens browser, or pass
                     --client-id/--client-secret for a service account)
  status             Show whether you are signed in
  logout [options]   Sign out and clear stored credentials
  help [command]     display help for command
```

```
===== unity auth login --help =====
Usage: unity auth login [options]

Sign in to your Unity account (opens browser, or pass
--client-id/--client-secret for a service account)

Options:
  --client-id <id>          Service-account key ID for non-interactive sign-in
  --client-secret <secret>  Service-account secret key (prefer
                            --secret-from-stdin in shared shells)
  --secret-from-stdin       Read the service-account secret from stdin (keeps it
                            out of argv / ps)
  --no-store                Do not persist credentials to the keyring (mint
                            in-process only)
  -h, --help                display help for command

Global Options:
  -V, --version             output the version number
  --format <format>         Output format: human, json, tsv, ndjson (env:
                            UNITY_FORMAT)
  --json                    Shorthand for --format json
  --no-banner               Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive         Disable interactive prompts. Useful in CI/CD
                            environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                   Suppress informational output (env: UNITY_QUIET)
  --verbose                 Show full error details including stack traces on
                            failure (env: UNITY_VERBOSE)
  --proxy <url>             HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                            http://user:pass@host:8080, socks5://host:1080,
                            pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable           Disable proxy for this invocation, ignoring all
                            sources
  --log-proxy               Log every outbound request to proxy-request.json for
                            this run. Off by default; typically used once when
                            reproducing a proxy issue for support. Also settable
                            via UNITY_LOG_PROXY=1 or the proxyRequestLogging
                            user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy            Opt out of --log-proxy / UNITY_LOG_PROXY / the
                            persisted user setting for this run. Use when
                            logging is enabled globally but you want one clean
                            invocation.
```

```
===== unity auth status --help =====
Usage: unity auth status [options]

Show whether you are signed in

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity auth logout --help =====
Usage: unity auth logout [options]

Sign out and clear stored credentials

Options:
  -y, --yes          Skip confirmation prompt
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity bug --help =====
Usage: unity bug [options]

Report a bug directly to the Unity bug reporter

Options:
  --email <email>              Contact email for the report (defaults to your
                               Unity account email when signed in; otherwise
                               required for non-interactive runs)
  --title <title>              One-line summary of the bug (at least 3
                               characters)
  --description <description>  What happened (at least 10 characters)
  --steps <steps...>           Steps to reproduce; repeat values to add lines
  --reproducibility <value>    How often the bug reproduces (choices:
                               "first-time", "sometimes", "always")
  -h, --help                   display help for command

Global Options:
  -V, --version                output the version number
  --format <format>            Output format: human, json, tsv, ndjson (env:
                               UNITY_FORMAT)
  --json                       Shorthand for --format json
  --no-banner                  Suppress the startup banner (env:
                               UNITY_NO_BANNER)
  --non-interactive            Disable interactive prompts. Useful in CI/CD
                               environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                      Suppress informational output (env: UNITY_QUIET)
  --verbose                    Show full error details including stack traces on
                               failure (env: UNITY_VERBOSE)
  --proxy <url>                HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                               http://user:pass@host:8080, socks5://host:1080,
                               pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable              Disable proxy for this invocation, ignoring all
                               sources
  --log-proxy                  Log every outbound request to proxy-request.json
                               for this run. Off by default; typically used once
                               when reproducing a proxy issue for support. Also
                               settable via UNITY_LOG_PROXY=1 or the
                               proxyRequestLogging user setting. (env:
                               UNITY_LOG_PROXY)
  --no-log-proxy               Opt out of --log-proxy / UNITY_LOG_PROXY / the
                               persisted user setting for this run. Use when
                               logging is enabled globally but you want one
                               clean invocation.
```

```
===== unity build --help =====
Usage: unity build [options] [project]

Build a Unity project from the command line. Spawns the editor in batch mode and
forwards conventional CI flags.

Arguments:
  project                              Project path or name (defaults to the
                                       current directory)

Options:
  --target <target>                    Build target (e.g. StandaloneWindows64,
                                       Android, iOS, WebGL). Required.
  --execute-method <method>            Static C# method to invoke (e.g.
                                       Builder.PerformBuild). Required — Unity
                                       has no built-in command-line build.
  --build-target-group <group>         Optional build target group, forwarded to
                                       Unity as -buildTargetGroup
  -o, --output-path <path>             Output path passed to Unity as
                                       -buildOutput (your executeMethod is
                                       responsible for honoring it)
  -l, --log-file <path>                Log file path (default:
                                       <project>/Logs/build-<target>-<timestamp>.log)
  --editor-version <version>           Override editor version (default: from
                                       ProjectVersion.txt)
  -e, --editor-path <path>             Path to a specific editor binary
  -a, --architecture <arch>            Editor architecture (x86_64 or arm64)
                                       (default: "unknown")
  --args <string>                      Extra arguments passed to Unity
                                       (shell-split)
  --no-tail                            Do not stream the log to stdout in real
                                       time
  --allow-install                      Install the project's editor version if
                                       it is not already installed
  --android-export-type <type>         Android export type: apk, aab, or
                                       android-studio-project (Android targets
                                       only, default: apk) (choices: "apk",
                                       "aab", "android-studio-project", default:
                                       "apk")
  --android-keystore-base64 <content>  Base64-encoded keystore file contents —
                                       decoded to a temp file before the build
                                       and deleted after (Android targets only;
                                       warning: CLI args may appear in shell
                                       history and CI logs)
  --android-keystore-password <pass>   Password for the keystore (Android
                                       targets only, required with
                                       --android-keystore-base64; warning: CLI
                                       args may appear in shell history and CI
                                       logs)
  --android-key-alias <alias>          Key alias inside the keystore (Android
                                       targets only, required with
                                       --android-keystore-base64)
  --android-key-alias-password <pass>  Password for the key alias (Android
                                       targets only, defaults to
                                       --android-keystore-password; warning: CLI
                                       args may appear in shell history and CI
                                       logs)
  --android-target-sdk-version <N>     Android SDK target version integer, e.g.
                                       34 (Android targets only)
  --android-symbol-type <type>         Debug symbol export type: none, public,
                                       or debugging (Android targets only,
                                       default: none) (choices: "none",
                                       "public", "debugging", default: "none")
  --android-version-code <N>           Explicit Android versionCode integer
                                       (Android targets only)
  --versioning-strategy <strategy>     Versioning strategy to apply to the build
                                       (semantic, tag, custom, none). Default:
                                       none. (choices: "semantic", "tag",
                                       "custom", "none", default: "none")
  --build-version <version>            Explicit version string to stamp on the
                                       build. Only used when
                                       --versioning-strategy is 'custom';
                                       ignored for all other strategies.
  --allow-dirty-build                  Skip the uncommitted-changes guard
                                       (default: false) (default: false)
  -h, --help                           display help for command

Global Options:
  -V, --version                        output the version number
  --format <format>                    Output format: human, json, tsv, ndjson
                                       (env: UNITY_FORMAT)
  --json                               Shorthand for --format json
  --no-banner                          Suppress the startup banner (env:
                                       UNITY_NO_BANNER)
  --non-interactive                    Disable interactive prompts. Useful in
                                       CI/CD environments. (env:
                                       UNITY_NON_INTERACTIVE)
  --quiet                              Suppress informational output (env:
                                       UNITY_QUIET)
  --verbose                            Show full error details including stack
                                       traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                        HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                       http://user:pass@host:8080,
                                       socks5://host:1080,
                                       pac+http://wpad/proxy.pac (env:
                                       UNITY_PROXY)
  --proxy-disable                      Disable proxy for this invocation,
                                       ignoring all sources
  --log-proxy                          Log every outbound request to
                                       proxy-request.json for this run. Off by
                                       default; typically used once when
                                       reproducing a proxy issue for support.
                                       Also settable via UNITY_LOG_PROXY=1 or
                                       the proxyRequestLogging user setting.
                                       (env: UNITY_LOG_PROXY)
  --no-log-proxy                       Opt out of --log-proxy / UNITY_LOG_PROXY
                                       / the persisted user setting for this
                                       run. Use when logging is enabled globally
                                       but you want one clean invocation.

Examples:
  $ unity build --target StandaloneWindows64 --execute-method Builder.PerformBuild
  $ unity build ./MyGame --target Android --execute-method Builder.AndroidBuild --output-path ./out/app.apk
  $ unity build "My Game" --target WebGL --execute-method Builder.WebGLBuild --editor-version 6000.0
  $ unity build --target iOS --execute-method Builder.iOSBuild --allow-install --no-tail
  $ unity build --target Android --execute-method Builder.Build --android-export-type aab --android-keystore-base64 <b64> --android-keystore-password <password> --android-key-alias mykey

```

```
===== unity cache --help =====
Usage: unity cache [options] [command]

Manage the download cache

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  info               Show download cache location and size
  clean [options]    Remove all files from the download cache
  help [command]     display help for command
```

```
===== unity cache info --help =====
Usage: unity cache info [options]

Show download cache location and size

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cache clean --help =====
Usage: unity cache clean [options]

Remove all files from the download cache

Options:
  -y, --yes          Skip confirmation prompt
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity changelog --help =====
Usage: unity changelog [options]

Show release notes for the installed CLI

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud --help =====
Usage: unity cloud [options] [command]

Manage Unity Cloud organizations and projects

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  status             Show cloud sign-in state and active organization
  org                Manage Unity Cloud organizations
  project [options]  Manage Unity Cloud projects
  help [command]     display help for command
```

```
===== unity cloud status --help =====
Usage: unity cloud status [options]

Show cloud sign-in state and active organization

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud org --help =====
Usage: unity cloud org [options] [command]

Manage Unity Cloud organizations

Options:
  -h, --help                display help for command

Global Options:
  -V, --version             output the version number
  --format <format>         Output format: human, json, tsv, ndjson (env:
                            UNITY_FORMAT)
  --json                    Shorthand for --format json
  --no-banner               Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive         Disable interactive prompts. Useful in CI/CD
                            environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                   Suppress informational output (env: UNITY_QUIET)
  --verbose                 Show full error details including stack traces on
                            failure (env: UNITY_VERBOSE)
  --proxy <url>             HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                            http://user:pass@host:8080, socks5://host:1080,
                            pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable           Disable proxy for this invocation, ignoring all
                            sources
  --log-proxy               Log every outbound request to proxy-request.json for
                            this run. Off by default; typically used once when
                            reproducing a proxy issue for support. Also settable
                            via UNITY_LOG_PROXY=1 or the proxyRequestLogging
                            user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy            Opt out of --log-proxy / UNITY_LOG_PROXY / the
                            persisted user setting for this run. Use when
                            logging is enabled globally but you want one clean
                            invocation.

Commands:
  list                      List your Unity Cloud organizations
  current                   Print the active default organization id
  set-default <id-or-name>  Set the active default organization
  clear-default             Clear the active default organization (revert to
                            'All Organizations')
  help [command]            display help for command
```

```
===== unity cloud org list --help =====
Usage: unity cloud org list [options]

List your Unity Cloud organizations

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud org current --help =====
Usage: unity cloud org current [options]

Print the active default organization id

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud org set-default --help =====
Usage: unity cloud org set-default [options] <id-or-name>

Set the active default organization

Arguments:
  id-or-name         Organization id or exact name

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud org clear-default --help =====
Usage: unity cloud org clear-default [options]

Clear the active default organization (revert to 'All Organizations')

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity cloud project --help =====
Usage: unity cloud project [options] [command]

Manage Unity Cloud projects

Options:
  --cloud-org <id-or-name>  Override the active organization (id, Genesis id, or
                            exact name) (env: UNITY_CLOUD_ORG)
  -h, --help                display help for command

Global Options:
  -V, --version             output the version number
  --format <format>         Output format: human, json, tsv, ndjson (env:
                            UNITY_FORMAT)
  --json                    Shorthand for --format json
  --no-banner               Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive         Disable interactive prompts. Useful in CI/CD
                            environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                   Suppress informational output (env: UNITY_QUIET)
  --verbose                 Show full error details including stack traces on
                            failure (env: UNITY_VERBOSE)
  --proxy <url>             HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                            http://user:pass@host:8080, socks5://host:1080,
                            pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable           Disable proxy for this invocation, ignoring all
                            sources
  --log-proxy               Log every outbound request to proxy-request.json for
                            this run. Off by default; typically used once when
                            reproducing a proxy issue for support. Also settable
                            via UNITY_LOG_PROXY=1 or the proxyRequestLogging
                            user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy            Opt out of --log-proxy / UNITY_LOG_PROXY / the
                            persisted user setting for this run. Use when
                            logging is enabled globally but you want one clean
                            invocation.

Commands:
  list [options]            List Unity Cloud projects in the active organization
  help [command]            display help for command
```

```
===== unity cloud project list --help =====
Usage: unity cloud project list [options]

List Unity Cloud projects in the active organization

Options:
  --search <q>              Filter projects by name (server-side substring
                            match)
  --limit <n>               Single-page page size. Supplying --limit or --skip
                            switches off the default drain-all behavior.
  --skip <n>                Single-page offset. Supplying --skip or --limit
                            switches off the default drain-all behavior.
  -h, --help                display help for command

Global Options:
  --cloud-org <id-or-name>  Override the active organization (id, Genesis id, or
                            exact name) (env: UNITY_CLOUD_ORG)
  -V, --version             output the version number
  --format <format>         Output format: human, json, tsv, ndjson (env:
                            UNITY_FORMAT)
  --json                    Shorthand for --format json
  --no-banner               Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive         Disable interactive prompts. Useful in CI/CD
                            environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                   Suppress informational output (env: UNITY_QUIET)
  --verbose                 Show full error details including stack traces on
                            failure (env: UNITY_VERBOSE)
  --proxy <url>             HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                            http://user:pass@host:8080, socks5://host:1080,
                            pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable           Disable proxy for this invocation, ignoring all
                            sources
  --log-proxy               Log every outbound request to proxy-request.json for
                            this run. Off by default; typically used once when
                            reproducing a proxy issue for support. Also settable
                            via UNITY_LOG_PROXY=1 or the proxyRequestLogging
                            user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy            Opt out of --log-proxy / UNITY_LOG_PROXY / the
                            persisted user setting for this run. Use when
                            logging is enabled globally but you want one clean
                            invocation.
```

```
===== unity completion --help =====
Usage: unity completion [options] <shell>

Print a shell completion script

Arguments:
  shell              Shell type (bash, zsh, fish, powershell) (choices: "bash",
                     "zsh", "fish", "powershell")

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ source <(unity completion bash)
  $ unity completion zsh >> ~/.zshrc && source ~/.zshrc
  $ unity completion fish > ~/.config/fish/completions/unity.fish
  $ unity completion powershell | Invoke-Expression
```

```
===== unity config --help =====
Usage: unity config [options] [command]

View or change persistent CLI configuration

Options:
  -h, --help             display help for command

Global Options:
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.

Commands:
  proxy [options] [url]  View or change the configured proxy server
  update-check [state]   Enable or disable the background check for CLI updates
  help [command]         display help for command
```

```
===== unity config proxy --help =====
Usage: unity config proxy [options] [url]

View or change the configured proxy server

Arguments:
  url                Proxy URL (http://, https://, socks://, socks4://,
                     socks4a://, socks5://, socks5h://, pac+http://,
                     pac+https://, pac+file://)

Options:
  --unset            Remove the persisted proxy configuration
  --bypass <hosts>   Comma-separated list of hosts that bypass the proxy
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity config update-check --help =====
Usage: unity config update-check [options] [state]

Enable or disable the background check for CLI updates

Arguments:
  state              Either on or off. Omit to show the current setting.

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity diagnose --help =====
Usage: unity diagnose [options] [command]

One-shot diagnostic commands for support — paste-safe, redacted output

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  proxy              Print a redacted proxy diagnostic report and write a copy
                     to the logs directory
  help [command]     display help for command
```

```
===== unity diagnose proxy --help =====
Usage: unity diagnose proxy [options]

Print a redacted proxy diagnostic report and write a copy to the logs directory

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity doctor --help =====
Usage: unity doctor [options]

Print diagnostic info about your Unity CLI environment

Options:
  --tail <lines>     Number of recent log lines to include (default: 20)
                     (default: 20)
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity editor --help =====
Usage: unity editor [options] [command]

Manage a Unity editor installation

Options:
  -h, --help            display help for command

Global Options:
  -V, --version         output the version number
  --format <format>     Output format: human, json, tsv, ndjson (env:
                        UNITY_FORMAT)
  --json                Shorthand for --format json
  --no-banner           Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive     Disable interactive prompts. Useful in CI/CD
                        environments. (env: UNITY_NON_INTERACTIVE)
  --quiet               Suppress informational output (env: UNITY_QUIET)
  --verbose             Show full error details including stack traces on
                        failure (env: UNITY_VERBOSE)
  --proxy <url>         HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                        http://user:pass@host:8080, socks5://host:1080,
                        pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable       Disable proxy for this invocation, ignoring all sources
  --log-proxy           Log every outbound request to proxy-request.json for
                        this run. Off by default; typically used once when
                        reproducing a proxy issue for support. Also settable via
                        UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                        setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy        Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                        user setting for this run. Use when logging is enabled
                        globally but you want one clean invocation.

Commands:
  add [options] <path>  Add an existing Unity editor to the Hub
  module                Manage modules for an installed editor
  help [command]        display help for command
```

```
===== unity editor add --help =====
Usage: unity editor add [options] <path>

Add an existing Unity editor to the Hub

Arguments:
  path                Path to the Unity Editor installation to register

Options:
  --no-fetch-modules  Skip fetching the module list from Unity Cloud.
  -h, --help          display help for command

Global Options:
  -V, --version       output the version number
  --format <format>   Output format: human, json, tsv, ndjson (env:
                      UNITY_FORMAT)
  --json              Shorthand for --format json
  --no-banner         Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive   Disable interactive prompts. Useful in CI/CD environments.
                      (env: UNITY_NON_INTERACTIVE)
  --quiet             Suppress informational output (env: UNITY_QUIET)
  --verbose           Show full error details including stack traces on failure
                      (env: UNITY_VERBOSE)
  --proxy <url>       HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                      http://user:pass@host:8080, socks5://host:1080,
                      pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable     Disable proxy for this invocation, ignoring all sources
  --log-proxy         Log every outbound request to proxy-request.json for this
                      run. Off by default; typically used once when reproducing
                      a proxy issue for support. Also settable via
                      UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                      (env: UNITY_LOG_PROXY)
  --no-log-proxy      Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                      user setting for this run. Use when logging is enabled
                      globally but you want one clean invocation.

Examples:
  $ unity editor add /Applications/Unity/Hub/Editor/2023.1.0f1/Unity.app
  $ unity editor add "C:\Program Files\Unity\Hub\Editor\2023.1.0f1"

```

```
===== unity editor module --help =====
Usage: unity editor module [options] [command]

Manage modules for an installed editor

Options:
  -h, --help                   display help for command

Global Options:
  -V, --version                output the version number
  --format <format>            Output format: human, json, tsv, ndjson (env:
                               UNITY_FORMAT)
  --json                       Shorthand for --format json
  --no-banner                  Suppress the startup banner (env:
                               UNITY_NO_BANNER)
  --non-interactive            Disable interactive prompts. Useful in CI/CD
                               environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                      Suppress informational output (env: UNITY_QUIET)
  --verbose                    Show full error details including stack traces on
                               failure (env: UNITY_VERBOSE)
  --proxy <url>                HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                               http://user:pass@host:8080, socks5://host:1080,
                               pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable              Disable proxy for this invocation, ignoring all
                               sources
  --log-proxy                  Log every outbound request to proxy-request.json
                               for this run. Off by default; typically used once
                               when reproducing a proxy issue for support. Also
                               settable via UNITY_LOG_PROXY=1 or the
                               proxyRequestLogging user setting. (env:
                               UNITY_LOG_PROXY)
  --no-log-proxy               Opt out of --log-proxy / UNITY_LOG_PROXY / the
                               persisted user setting for this run. Use when
                               logging is enabled globally but you want one
                               clean invocation.

Commands:
  list [options] <version>     List available modules for an installed editor
  add [options] <version>      Add modules to an installed editor
  refresh [options] <version>  Re-fetch the module list for a located editor.
  remove [options] <version>   Remove installed modules from an editor
  help [command]               display help for command
```

```
===== unity editor module list --help =====
Usage: unity editor module list [options] <version>

List available modules for an installed editor

Arguments:
  version                            Editor version to list modules for

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editor module add --help =====
Usage: unity editor module add [options] <version>

Add modules to an installed editor

Arguments:
  version                            Editor version to add modules to

Options:
  -m, --module <module...>           Module ID(s) to add
  --all                              Add all available modules
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --child-modules                    Install all child modules automatically
  --no-child-modules                 Do not install child modules automatically
  --accept-eula                      Accept all module license agreements
                                     automatically
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editor module refresh --help =====
Usage: unity editor module refresh [options] <version>

Re-fetch the module list for a located editor.

Arguments:
  version                            Editor version to refresh modules for

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editor module remove --help =====
Usage: unity editor module remove [options] <version>

Remove installed modules from an editor

Arguments:
  version                            Editor version to remove modules from

Options:
  -m, --module <module...>           Module ID(s) to remove
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -y, --yes                          Skip the confirmation prompt
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors --help =====
Usage: unity editors|e [options] [command]

Manage Unity editors

Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.

Commands:
  add [options] <path...>            Add an existing Unity editor to the Hub
  default [options] [version]        Show or set the default editor version used
                                     when no version is specified
  list                               List installed Unity editors or available
                                     releases for download
  running                            List running Unity editors and the project
                                     each has open
  info <version>                     Show release details for a Unity editor
                                     version
  module                             Manage modules for an installed editor
  install-path|ip [options]          Show or change the global editor install
                                     path
  path <version>                     Print the install directory of an installed
                                     Unity editor version
  upgrade [options] [editor]         Upgrade an installed editor to the newest
                                     patch in its release line
```

```
===== unity editors add --help =====
Usage: unity editors add [options] <path...>

Add an existing Unity editor to the Hub

Options:
  --skip-signature-check             Skip code-signature and bundle-ID checks
                                     (use for development builds)
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors default --help =====
Usage: unity editors default [options] [version]

Show or set the default editor version used when no version is specified

Options:
  --unset                            Clear the stored default editor version
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors list --help =====
Usage: unity editors list [options]

List installed Unity editors or available releases for download

Options:
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors running --help =====
Usage: unity editors running [options]

List running Unity editors and the project each has open

Options:
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors info --help =====
Usage: unity editors info [options] <version>

Show release details for a Unity editor version

Arguments:
  version                            Unity editor version (e.g. 6000.0.26f1)

Options:
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.

Examples:
  $ unity editors info 6000.0.26f1
  $ unity editors info 2022.3.50f1 --json
  $ unity editors info 6000.0.26f1 --format json

```

```
===== unity editors module --help =====
Usage: unity editors module [options] [command]

Manage modules for an installed editor

Options:
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.

Commands:
  list [options] <version>           List available modules for an installed
                                     editor
  add [options] <version>            Add modules to an installed editor
  refresh [options] <version>        Re-fetch the module list for a located
                                     editor.
  remove [options] <version>         Remove installed modules from an editor
  help [command]                     display help for command
```

```
===== unity editors module list --help =====
Usage: unity editors module list [options] <version>

List available modules for an installed editor

Arguments:
  version                            Editor version to list modules for

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors module add --help =====
Usage: unity editors module add [options] <version>

Add modules to an installed editor

Arguments:
  version                            Editor version to add modules to

Options:
  -m, --module <module...>           Module ID(s) to add
  --all                              Add all available modules
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --child-modules                    Install all child modules automatically
  --no-child-modules                 Do not install child modules automatically
  --accept-eula                      Accept all module license agreements
                                     automatically
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors module refresh --help =====
Usage: unity editors module refresh [options] <version>

Re-fetch the module list for a located editor.

Arguments:
  version                            Editor version to refresh modules for

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors module remove --help =====
Usage: unity editors module remove [options] <version>

Remove installed modules from an editor

Arguments:
  version                            Editor version to remove modules from

Options:
  -m, --module <module...>           Module ID(s) to remove
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -y, --yes                          Skip the confirmation prompt
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors install-path --help =====
Usage: unity editors install-path|ip [options]

Show or change the global editor install path

Options:
  -s, --set <path>                   Change the install path to <path>
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity editors path --help =====
Usage: unity editors path [options] <version>

Print the install directory of an installed Unity editor version

Arguments:
  version                            Unity editor version (e.g. 6000.0.26f1,
                                     latest, lts)

Options:
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.

Examples:
  $ unity editors path 6000.3.10f1
  $ unity editors path 6000.3.10f1 --json
  $ unity editors path 6000.3.10f1 --architecture arm64

```

```
===== unity editors upgrade --help =====
Usage: unity editors upgrade [options] [editor]

Upgrade an installed editor to the newest patch in its release line

Arguments:
  editor                             Installed editor to upgrade (version, line
                                     like 2022.3, or latest/lts/default)

Options:
  --all                              Upgrade every installed editor that has a
                                     newer patch
  --replace                          Uninstall the old editor after the upgrade
                                     succeeds
  --remove-old                       Alias for --replace
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown", env:
                                     UNITY_ARCHITECTURE)
  -m, --module <module...>           Extra module to install, on top of the
                                     modules carried over (repeatable)
  --no-modules                       Skip carrying over the current editor’s
                                     modules
  -y, --yes                          Skip confirmation prompts
  --accept-eula                      Accept module license agreements
                                     automatically
  --dry-run                          Show available upgrades without installing
  --check                            Alias for --dry-run
  -h, --help                         display help for command

Global Options:
  -r, --releases                     List available Unity releases for download
  -i, --installed                    List installed Unity editors
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --verbose                          Show detailed information (full module
                                     names, paths)
  -w, --watch                        Watch for editor changes and refresh output
                                     (Ctrl-C to stop)
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity env --help =====
Usage: unity env [options]

Print Unity Hub environment paths and version

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity hub --help =====
Usage: unity hub [options] [command]

Manage the Unity Hub application

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  install [options]  Install the Unity Hub
  help [command]     display help for command
```

```
===== unity hub install --help =====
Usage: unity hub install [options]

Install the Unity Hub

Options:
  -f, --force                        Reinstall even if Unity Hub is already
                                     installed
  --headless                         Run the installer silently without a
                                     graphical interface (Windows only)
  --skip-signature-check             Install without verifying the installer's
                                     code signature (not recommended)
  -a, --architecture <architecture>  Target architecture (x64 or arm64, default:
                                     system architecture) (default: "unknown",
                                     env: UNITY_ARCHITECTURE)
  --hub-version <version>            Hub version to install (e.g. 3.17.0).
                                     Defaults to latest.
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity install --help =====
Usage: unity install|i [options] [version]

Install a Unity editor

Arguments:
  version                            Version of the editor to install
                                     (interactive selection if omitted)

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown", env:
                                     UNITY_ARCHITECTURE)
  -c, --changeset <changeset>        Changeset for archive installs
  -m, --module <module...>           Module IDs to install alongside editor
  --cm, --childModules               Install all child modules automatically
  --no-cm, --no-childModules         Do not install child modules automatically
  -f, --force                        Force reinstall of an already-installed
                                     editor
  -y, --yes                          Automatically select the first match
                                     without prompting
  --accept-eula                      Accept all module license agreements
                                     automatically
  --dry-run                          Print what would be downloaded without
                                     installing
  --resume                           Resume interrupted downloads from cache
  --no-elevate                       Skip the elevated (UAC) install helper on
                                     Windows; installs into protected paths fail
                                     instead of prompting
  --list-components                  List the editor's modules with the Hub id
                                     and unity-downloader-cli-compatible name,
                                     then exit
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity install-modules --help =====
Usage: unity install-modules|im [options]

Install or list modules for an installed editor (fully interactive when no
arguments provided)

Options:
  -e, --editor-version <version>     Version of the editor (interactive
                                     selection if omitted)
  -m, --module <module...>           Module IDs to install
  -l, --list                         List available modules for the editor
  --all                              Install all available modules
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  --cm, --childModules               Install all child modules automatically
  --no-cm, --no-childModules         Do not install child modules automatically
  --accept-eula                      Accept all module license agreements
                                     automatically
  --dry-run                          Print what would be downloaded without
                                     installing
  -y, --yes                          Automatically select the first match
                                     without prompting
  --reinstall                        Reinstall modules that are already
                                     installed (repair)
  -f, --force                        Reinstall modules and skip confirmation
                                     prompts (implies --reinstall; also
                                     auto-includes child modules)
  --no-elevate                       Skip the elevated (UAC) install helper on
                                     Windows; installs into protected paths fail
                                     instead of prompting
  --retries <n>                      Retry a failed module download or
                                     validation this many times with backoff
                                     before giving up (0 disables retries)
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity install-path --help =====
Usage: unity install-path|ip [options]

Set or get the editor install path

Options:
  -s, --set <path>   Set the install path
  -g, --get          Get the current install path
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity language --help =====
Usage: unity language|lang [options]

Show or change the CLI display language

Options:
  --set <code>       Set the display language (e.g. en, fr, de)
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity license --help =====
Usage: unity license [options] [command]

List the Unity licenses active on this machine

Options:
  -h, --help          display help for command

Global Options:
  -V, --version       output the version number
  --format <format>   Output format: human, json, tsv, ndjson (env:
                      UNITY_FORMAT)
  --json              Shorthand for --format json
  --no-banner         Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive   Disable interactive prompts. Useful in CI/CD environments.
                      (env: UNITY_NON_INTERACTIVE)
  --quiet             Suppress informational output (env: UNITY_QUIET)
  --verbose           Show full error details including stack traces on failure
                      (env: UNITY_VERBOSE)
  --proxy <url>       HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                      http://user:pass@host:8080, socks5://host:1080,
                      pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable     Disable proxy for this invocation, ignoring all sources
  --log-proxy         Log every outbound request to proxy-request.json for this
                      run. Off by default; typically used once when reproducing
                      a proxy issue for support. Also settable via
                      UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                      (env: UNITY_LOG_PROXY)
  --no-log-proxy      Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                      user setting for this run. Use when logging is enabled
                      globally but you want one clean invocation.

Commands:
  list                List the Unity licenses active on this machine
  status              Show the current Unity license state
  activate [options]  Activate a Unity license
  return [options]    Return the active Unity licenses
  server              Manage the floating license server
```

```
===== unity license list --help =====
Usage: unity license list [options]

List the Unity licenses active on this machine

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity license status --help =====
Usage: unity license status [options]

Show the current Unity license state

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity license activate --help =====
Usage: unity license activate [options]

Activate a Unity license

Options:
  --serial <serial>          Activate with a license serial number
  --personal                 Activate the free Unity Personal license
  --floating                 Acquire a license from the configured floating
                             license server
  --file <path>              Activate offline from a license file (.ulf or .xml)
  --generate-request <path>  Save an offline activation request (.alf) to the
                             given path
  --accept-eula              Accept the Unity Personal license terms (required
                             with --personal)
  -h, --help                 display help for command

Global Options:
  -V, --version              output the version number
  --format <format>          Output format: human, json, tsv, ndjson (env:
                             UNITY_FORMAT)
  --json                     Shorthand for --format json
  --no-banner                Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive          Disable interactive prompts. Useful in CI/CD
                             environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                    Suppress informational output (env: UNITY_QUIET)
  --verbose                  Show full error details including stack traces on
                             failure (env: UNITY_VERBOSE)
  --proxy <url>              HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                             http://user:pass@host:8080, socks5://host:1080,
                             pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable            Disable proxy for this invocation, ignoring all
                             sources
  --log-proxy                Log every outbound request to proxy-request.json
                             for this run. Off by default; typically used once
                             when reproducing a proxy issue for support. Also
                             settable via UNITY_LOG_PROXY=1 or the
                             proxyRequestLogging user setting. (env:
                             UNITY_LOG_PROXY)
  --no-log-proxy             Opt out of --log-proxy / UNITY_LOG_PROXY / the
                             persisted user setting for this run. Use when
                             logging is enabled globally but you want one clean
                             invocation.
```

```
===== unity license return --help =====
Usage: unity license return [options]

Return the active Unity licenses

Options:
  -y, --yes          Return without confirmation
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity license server --help =====
Usage: unity license server [options] [command]

Manage the floating license server

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  status             Show the floating license server status and available seats
  list               List the configured floating license servers
  help [command]     display help for command
```

```
===== unity license server status --help =====
Usage: unity license server status [options]

Show the floating license server status and available seats

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity license server list --help =====
Usage: unity license server list [options]

List the configured floating license servers

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity list --help =====
Usage: unity list [options]

List tools available on the connected Unity Editor (commands registered by the
Pipeline package)

Options:
  --project-path <path>  Path to Unity project (auto-detected if not specified)
                         (env: UNITY_PROJECT_PATH)
  --runtime <name>       Connect to a Unity Player runtime instance (search by
                         process name)
  --runtime-path <path>  Connect to a Unity Player by path to its runtime port
                         file
  -h, --help             display help for command

Global Options:
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.
```

```
===== unity logs --help =====
Usage: unity logs [options]

Read and tail the Hub log file

Options:
  --tail <lines>     Number of recent log entries to show (default: 20; 0 = show
                     all) (default: 20)
  -f, --follow       Stream new log entries as they are written (like tail -f)
                     (default: false)
  --level <level>    Minimum log level to show (trace, debug, info, warn, error,
                     fatal) (default: "info")
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity mcp --help =====
Usage: unity mcp [options] [command]

MCP server and client configuration for Unity Editor

Options:
  --project-path <path>         Locate the Editor by its project path; with
                                configure, pin the entry to that project
  --runtime <version>           Filter by Unity runtime version
  --runtime-path <path>         Directory containing the Unity Player port
                                descriptor file
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

Commands:
  configure [options] [client]  Write MCP server config for an AI agent client

Subcommands:
  configure <client>   Write MCP server entry for an AI agent client
                       Run 'unity mcp configure --list' to see all clients

Examples:
  unity mcp                                   Start the MCP stdio server
  unity mcp --project-path /path/to/MyProject Start server for a specific project
  unity mcp configure claude                  Configure Claude Desktop
  unity mcp configure --list                  Show all supported clients
```

```
===== unity mcp configure --help =====
Usage: unity mcp configure [options] [client]

Write MCP server config for an AI agent client

Arguments:
  client                 Client to configure (claude, cursor, vscode, windsurf,
                         …)

Options:
  --list                 List all supported clients and their config paths
  --local                Write project-local config (for clients that support
                         it, e.g. cursor, windsurf)
  --yes                  Skip the "already exists, update?" confirmation prompt
  --dry-run              Print what would be written without modifying any files
  -h, --help             display help for command

Global Options:
  --project-path <path>  Locate the Editor by its project path; with configure,
                         pin the entry to that project
  --runtime <version>    Filter by Unity runtime version
  --runtime-path <path>  Directory containing the Unity Player port descriptor
                         file
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.

Supported clients:
  claude          Claude Desktop (global)
  claude-code     Claude Code CLI (via claude mcp add)
  cursor          Cursor — global (~/.cursor/mcp.json)
  cursor --local  Cursor — project-local (.cursor/mcp.json)
  vscode          VS Code / GitHub Copilot (.vscode/mcp.json)
  vscode-insiders VS Code Insiders / GitHub Copilot
  copilot-cli     GitHub Copilot CLI
  windsurf        Windsurf — global
  windsurf --local Windsurf — project-local
  cline           Cline VS Code extension
  codex           OpenAI Codex CLI
  kiro            AWS Kiro IDE
  trae            Trae (prints manual instructions)
  openclaw        OpenClaw (prints manual instructions)
  antigravity     Antigravity 2.0 / Antigravity IDE
  zed             Zed editor
  continue        Continue VS Code extension
  inspect         Launch MCP Inspector in browser

Examples:
  unity mcp configure --list
  unity mcp configure claude
  unity mcp configure cursor --local
  unity mcp configure claude --project-path /path/to/MyProject
  unity mcp configure vscode --dry-run
```

```
===== unity modules --help =====
Usage: unity modules [options] [command]

List and manage Unity editor modules

Options:
  -h, --help                display help for command

Global Options:
  -V, --version             output the version number
  --format <format>         Output format: human, json, tsv, ndjson (env:
                            UNITY_FORMAT)
  --json                    Shorthand for --format json
  --no-banner               Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive         Disable interactive prompts. Useful in CI/CD
                            environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                   Suppress informational output (env: UNITY_QUIET)
  --verbose                 Show full error details including stack traces on
                            failure (env: UNITY_VERBOSE)
  --proxy <url>             HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                            http://user:pass@host:8080, socks5://host:1080,
                            pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable           Disable proxy for this invocation, ignoring all
                            sources
  --log-proxy               Log every outbound request to proxy-request.json for
                            this run. Off by default; typically used once when
                            reproducing a proxy issue for support. Also settable
                            via UNITY_LOG_PROXY=1 or the proxyRequestLogging
                            user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy            Opt out of --log-proxy / UNITY_LOG_PROXY / the
                            persisted user setting for this run. Use when
                            logging is enabled globally but you want one clean
                            invocation.

Commands:
  list [options] <version>  List available modules for an editor version
  help [command]            display help for command
```

```
===== unity modules list --help =====
Usage: unity modules list [options] <version>

List available modules for an editor version

Arguments:
  version                            Editor version to query (e.g. 2023.1.0f1)

Options:
  -a, --architecture <architecture>  CPU architecture to use when multiple
                                     builds are installed for the same version
                                     (arm64 or x86_64) (default: "unknown")
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.
```

```
===== unity pipeline --help =====
Usage: unity pipeline|pipe [options] [command]

Unity Editor Pipeline automation commands

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Commands:
  install [options]  Install the Unity Pipeline package to a Unity project
  upgrade [options]  Upgrade the Unity Pipeline package to the latest version if
                     a newer one is available
  list               List all Unity Editor instances and their Pipeline package
                     status
  list-versions      List all available versions of the Unity Pipeline package
  help [command]     display help for command

Install command options:
  --project-path <path>        Path to Unity project (auto-detected if not specified)
  --force                      Re-resolve and update the package to the latest version even if already installed
  --package-version <version>  Install a specific version of the package (overwrites any version already installed)

Examples:
  unity pipeline list
  unity pipeline list-versions
  unity pipeline install
  unity pipeline install --project-path /path/to/project
  unity pipeline install --force
  unity pipeline install --package-version 0.3.0-exp.1
  unity pipeline upgrade
  unity pipeline upgrade --project-path /path/to/project
```

```
===== unity pipeline install --help =====
Usage: unity pipeline install [options]

Install the Unity Pipeline package to a Unity project

Options:
  --project-path <path>        Path to Unity project (auto-detected if not
                               specified) (env: UNITY_PROJECT_PATH)
  --force                      Re-resolve and update the package to the latest
                               version even if already installed
  --package-version <version>  Install a specific version of the package
                               (overwrites any version already installed)
  -h, --help                   display help for command

Global Options:
  -V, --version                output the version number
  --format <format>            Output format: human, json, tsv, ndjson (env:
                               UNITY_FORMAT)
  --json                       Shorthand for --format json
  --no-banner                  Suppress the startup banner (env:
                               UNITY_NO_BANNER)
  --non-interactive            Disable interactive prompts. Useful in CI/CD
                               environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                      Suppress informational output (env: UNITY_QUIET)
  --verbose                    Show full error details including stack traces on
                               failure (env: UNITY_VERBOSE)
  --proxy <url>                HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                               http://user:pass@host:8080, socks5://host:1080,
                               pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable              Disable proxy for this invocation, ignoring all
                               sources
  --log-proxy                  Log every outbound request to proxy-request.json
                               for this run. Off by default; typically used once
                               when reproducing a proxy issue for support. Also
                               settable via UNITY_LOG_PROXY=1 or the
                               proxyRequestLogging user setting. (env:
                               UNITY_LOG_PROXY)
  --no-log-proxy               Opt out of --log-proxy / UNITY_LOG_PROXY / the
                               persisted user setting for this run. Use when
                               logging is enabled globally but you want one
                               clean invocation.
```

```
===== unity pipeline upgrade --help =====
Usage: unity pipeline upgrade [options]

Upgrade the Unity Pipeline package to the latest version if a newer one is
available

Options:
  --project-path <path>  Path to Unity project (auto-detected if not specified)
                         (env: UNITY_PROJECT_PATH)
  -h, --help             display help for command

Global Options:
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.
```

```
===== unity pipeline list --help =====
Usage: unity pipeline list [options]

List all Unity Editor instances and their Pipeline package status

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity pipeline list-versions --help =====
Usage: unity pipeline list-versions [options]

List all available versions of the Unity Pipeline package

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity projects --help =====
Usage: unity projects|p [options] [command]

Manage Unity projects in the Hub registry

Options:
  -h, --help                      display help for command

Global Options:
  -V, --version                   output the version number
  --format <format>               Output format: human, json, tsv, ndjson (env:
                                  UNITY_FORMAT)
  --json                          Shorthand for --format json
  --no-banner                     Suppress the startup banner (env:
                                  UNITY_NO_BANNER)
  --non-interactive               Disable interactive prompts. Useful in CI/CD
                                  environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                         Suppress informational output (env:
                                  UNITY_QUIET)
  --verbose                       Show full error details including stack traces
                                  on failure (env: UNITY_VERBOSE)
  --proxy <url>                   HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                  http://user:pass@host:8080,
                                  socks5://host:1080, pac+http://wpad/proxy.pac
                                  (env: UNITY_PROXY)
  --proxy-disable                 Disable proxy for this invocation, ignoring
                                  all sources
  --log-proxy                     Log every outbound request to
                                  proxy-request.json for this run. Off by
                                  default; typically used once when reproducing
                                  a proxy issue for support. Also settable via
                                  UNITY_LOG_PROXY=1 or the proxyRequestLogging
                                  user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy                  Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                  persisted user setting for this run. Use when
                                  logging is enabled globally but you want one
                                  clean invocation.

Commands:
  list [options] [pattern]        List Unity projects registered in the Hub
  add <paths...>                  Register existing Unity project folders in the
                                  Hub registry
  remove [options] <paths...>     Remove projects from the Hub registry (does
                                  not delete files on disk)
  info [pathOrName]               Show details for a local Unity project.
                                  Defaults to the current directory; pass a path
                                  or a name registered with the Hub.
  create [options] <name>         Create a new Unity project and register it in
                                  the Hub
  clone [options]                 Clone a remote repository and register the
                                  Unity project it contains
  link                            Connect a local project to its cloud or
                                  version-control link
  new [options] <name>            Create a new Unity project (non-interactive,
                                  CI-friendly)
  open [options] [pattern]        Open a Unity project by name, fuzzy title
                                  match, or path
  pin <pattern>                   Pin (favorite) one or more projects by name or
                                  path pattern
  unpin <pattern>                 Unpin (unfavorite) one or more projects by
                                  name or path pattern
  require [options] [pathOrName]  Assert that the required editor version is
                                  installed, installing it if necessary
  size [options] [project]        Report a project's disk usage by folder, or
                                  summarize every registered project
  unlink                          Disconnect a local project from its cloud or
                                  version-control link
  upgrade [options] [pathOrName]  Upgrade a Unity project to a different editor
                                  version
  export [options]                Export the Hub project list to a JSON file
  import [options] [file]         Import a Hub project list from a previously
                                  exported JSON file
  help [command]                  display help for command
```

```
===== unity projects list --help =====
Usage: unity projects list [options] [pattern]

List Unity projects registered in the Hub

Arguments:
  pattern            Optional glob filter on project title or path
                     (case-insensitive)

Options:
  -v, --verbose      Show all columns (name, path, version, modified, cloud,
                     pipeline)
  --editor-version   Show editor version column
  -m, --modified     Show last-modified column
  --cloud            Show Cloud project ID column
  --pipeline         Show render pipeline column
  --vcs              Show VCS provider and repository column
  -a, --all          Print all projects without paging
  --limit <n>        Projects per page (default: 10)
  -w, --watch        Watch for project list changes and refresh output (Ctrl-C
                     to stop)
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects list
  $ unity projects list 'MyGame*'
  $ unity projects list -v
  $ unity projects list --editor-version --pipeline --vcs
  $ unity projects list -a

```

```
===== unity projects add --help =====
Usage: unity projects add [options] <paths...>

Register existing Unity project folders in the Hub registry

Arguments:
  paths              One or more Unity project directory paths

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects add ./MyGame
  $ unity projects add ./MyGame ./Prototype

```

```
===== unity projects remove --help =====
Usage: unity projects remove [options] <paths...>

Remove projects from the Hub registry (does not delete files on disk)

Arguments:
  paths              One or more Hub-registered project directory paths

Options:
  -f, --force        Skip confirmation prompt
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects remove ./MyGame
  $ unity projects remove ./MyGame ./Other --force

```

```
===== unity projects info --help =====
Usage: unity projects info [options] [pathOrName]

Show details for a local Unity project. Defaults to the current directory; pass
a path or a name registered with the Hub.

Arguments:
  pathOrName         Project path or name (defaults to the current directory)

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects info
  $ unity projects info ./MyGame
  $ unity projects info "My Game"
  $ unity projects info /path/to/MyGame --json

```

```
===== unity projects create --help =====
Usage: unity projects create [options] <name>

Create a new Unity project and register it in the Hub

Arguments:
  name                           Project folder name

Options:
  --path <path>                  Parent directory for the new project
  --editor-version <version>     Unity editor version (e.g. 6000.0.26f1, latest,
                                 lts)
  --template <template-id>       Template package name (e.g.
                                 com.unity.template.3d)
  -a, --architecture <arch>      Editor architecture (x86_64 or arm64) (default:
                                 "unknown")
  --open                         Open the project in the Editor after creation
  --cloud                        Create and link a new Unity Cloud project
  --cloud-org <id-or-name>       Organization for the cloud project (id or
                                 name); overrides UNITY_CLOUD_ORG and the saved
                                 default (env: UNITY_CLOUD_ORG)
  --cloud-project <id-or-name>   Link to an existing cloud project (id or name)
                                 instead of creating one
  --vcs <provider>               Create and link a git repository (github or
                                 gitlab)
  --git-namespace <name>         GitHub organization or GitLab group for the
                                 repository; defaults to the authenticated user
  --git-repo <name>              Name for the remote repository; defaults to the
                                 project name
  --git-visibility <visibility>  Repository visibility: private, public, or
                                 internal (default private)
  --git-default-branch <name>    Default branch name for the new repository
                                 (default main)
  --git-token <pat>              Personal access token; prefer --git-token-stdin
                                 or env vars in CI
  --git-token-stdin              Read the personal access token from stdin
                                 (CI-safe)
  --no-initial-commit            Skip the automatic initial commit and push
  --git-lfs                      Initialize Git LFS before the first commit
  --vcs-region <name>            UVCS region for the organization's subscription
                                 (first use only).
  -h, --help                     display help for command

Global Options:
  -V, --version                  output the version number
  --format <format>              Output format: human, json, tsv, ndjson (env:
                                 UNITY_FORMAT)
  --json                         Shorthand for --format json
  --no-banner                    Suppress the startup banner (env:
                                 UNITY_NO_BANNER)
  --non-interactive              Disable interactive prompts. Useful in CI/CD
                                 environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                        Suppress informational output (env:
                                 UNITY_QUIET)
  --verbose                      Show full error details including stack traces
                                 on failure (env: UNITY_VERBOSE)
  --proxy <url>                  HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                 http://user:pass@host:8080, socks5://host:1080,
                                 pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable                Disable proxy for this invocation, ignoring all
                                 sources
  --log-proxy                    Log every outbound request to
                                 proxy-request.json for this run. Off by
                                 default; typically used once when reproducing a
                                 proxy issue for support. Also settable via
                                 UNITY_LOG_PROXY=1 or the proxyRequestLogging
                                 user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy                 Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                 persisted user setting for this run. Use when
                                 logging is enabled globally but you want one
                                 clean invocation.

Examples:
  $ unity projects create MyGame --editor-version 6000.0.30f1
  $ unity projects create MyGame --template com.unity.template.3d --path ~/UnityProjects
  $ unity projects create MyGame --cloud
  $ unity projects create MyGame --cloud-project <id-or-name>
  $ unity projects create MyGame --vcs github --git-token-stdin
  $ unity projects create MyGame --vcs gitlab --git-namespace my-group --git-visibility private
  $ unity projects create MyGame --vcs uvcs --cloud --vcs-region us-east

```

```
===== unity projects clone --help =====
Usage: unity projects clone [options]

Clone a remote repository and register the Unity project it contains

Options:
  --vcs <provider>              Source control provider: github, gitlab, or uvcs
  --vcs-namespace <name>        GitHub organization, GitLab group, or Unity
                                Version Control organization
  --vcs-repo <name>             Repository name (github/gitlab) or repository
                                identity (uvcs)
  --ref <branch|sha|changeset>  Branch, commit, or changeset to check out (an
                                all-digits UVCS ref is read as a changeset)
  --path <dest>                 Destination directory for the clone; defaults to
                                the default projects folder
  --project-path <subpath>      Subpath of the project to register when the
                                repository holds more than one
  --git-token <pat>             Personal access token (github/gitlab); prefer
                                --git-token-stdin or env vars in CI
  --git-token-stdin             Read the personal access token from stdin
                                (CI-safe)
  --no-lfs                      Skip downloading Git LFS assets; leave pointer
                                files in place
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

Examples:
  $ unity projects clone --vcs github --vcs-namespace my-org --vcs-repo MyGame --git-token-stdin
  $ unity projects clone --vcs uvcs --vcs-namespace my-org --vcs-repo MyCloudProject/MyRepo --ref main

Notes:
  --vcs-namespace and --vcs-repo apply to BOTH providers (there is no --git-namespace/--git-repo here).
  --ref: an all-digits UVCS ref is read as a changeset, otherwise as a branch.
  Git LFS assets are fetched automatically (needs git-lfs installed). Use --no-lfs to leave pointer files.

```

```
===== unity projects link --help =====
Usage: unity projects link [options] [command]

Connect a local project to its cloud or version-control link

Options:
  -h, --help              display help for command

Global Options:
  -V, --version           output the version number
  --format <format>       Output format: human, json, tsv, ndjson (env:
                          UNITY_FORMAT)
  --json                  Shorthand for --format json
  --no-banner             Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive       Disable interactive prompts. Useful in CI/CD
                          environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                 Suppress informational output (env: UNITY_QUIET)
  --verbose               Show full error details including stack traces on
                          failure (env: UNITY_VERBOSE)
  --proxy <url>           HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                          http://user:pass@host:8080, socks5://host:1080,
                          pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable         Disable proxy for this invocation, ignoring all
                          sources
  --log-proxy             Log every outbound request to proxy-request.json for
                          this run. Off by default; typically used once when
                          reproducing a proxy issue for support. Also settable
                          via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                          setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy          Opt out of --log-proxy / UNITY_LOG_PROXY / the
                          persisted user setting for this run. Use when logging
                          is enabled globally but you want one clean invocation.

Commands:
  cloud [options] <path>  Connect a local project to a Unity Cloud project
                          (creates one, or links an existing one with
                          --cloud-project)
  vcs [options] <path>    Publish a local project to a new GitHub or GitLab
                          repository
  help [command]          display help for command
```

```
===== unity projects link cloud --help =====
Usage: unity projects link cloud [options] <path>

Connect a local project to a Unity Cloud project (creates one, or links an
existing one with --cloud-project)

Arguments:
  path                          Path to the local project to link

Options:
  --cloud-org <id-or-name>      Organization for the cloud project (id or name);
                                overrides UNITY_CLOUD_ORG and the saved default
                                (env: UNITY_CLOUD_ORG)
  --cloud-project <id-or-name>  Link to an existing cloud project (id or name)
                                instead of creating one
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.
```

```
===== unity projects link vcs --help =====
Usage: unity projects link vcs [options] <path>

Publish a local project to a new GitHub or GitLab repository

Arguments:
  path                           Path to the local project to link

Options:
  --vcs <provider>               Source control provider: github or gitlab
  --git-namespace <name>         GitHub organization or GitLab group for the
                                 repository; defaults to the authenticated user
  --git-repo <name>              Name for the remote repository; defaults to the
                                 project folder name
  --git-visibility <visibility>  Repository visibility: private, public, or
                                 internal (default private)
  --git-default-branch <name>    Default branch name for a new repository
                                 (default main)
  --git-token <pat>              Personal access token; prefer --git-token-stdin
                                 or env vars in CI
  --git-token-stdin              Read the personal access token from stdin
                                 (CI-safe)
  --no-initial-commit            Skip the automatic initial commit and push
  --git-lfs                      Initialize Git LFS before the first commit (new
                                 repositories only)
  --vcs-region <name>            UVCS region for the organization's subscription
                                 (first use only).
  --cloud-org <id-or-name>       Cloud organization id or name (for --vcs uvcs
                                 on a project not yet cloud-linked). (env:
                                 UNITY_CLOUD_ORG)
  --cloud-project <id-or-name>   Existing cloud project id or name to link (for
                                 --vcs uvcs).
  -h, --help                     display help for command

Global Options:
  -V, --version                  output the version number
  --format <format>              Output format: human, json, tsv, ndjson (env:
                                 UNITY_FORMAT)
  --json                         Shorthand for --format json
  --no-banner                    Suppress the startup banner (env:
                                 UNITY_NO_BANNER)
  --non-interactive              Disable interactive prompts. Useful in CI/CD
                                 environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                        Suppress informational output (env:
                                 UNITY_QUIET)
  --verbose                      Show full error details including stack traces
                                 on failure (env: UNITY_VERBOSE)
  --proxy <url>                  HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                 http://user:pass@host:8080, socks5://host:1080,
                                 pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable                Disable proxy for this invocation, ignoring all
                                 sources
  --log-proxy                    Log every outbound request to
                                 proxy-request.json for this run. Off by
                                 default; typically used once when reproducing a
                                 proxy issue for support. Also settable via
                                 UNITY_LOG_PROXY=1 or the proxyRequestLogging
                                 user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy                 Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                 persisted user setting for this run. Use when
                                 logging is enabled globally but you want one
                                 clean invocation.
```

```
===== unity projects new --help =====
Usage: unity projects new [options] <name>

Create a new Unity project (non-interactive, CI-friendly)

Arguments:
  name                        Project folder name

Options:
  --path <path>               Parent directory for the project
  --editor-version <version>  Unity editor version (e.g. 6000.0.26f1, latest,
                              lts)
  --template <template-id>    Template name or package ID (e.g.
                              com.unity.template.3d)
  -a, --architecture <arch>   Editor architecture (x86_64 or arm64) (default:
                              "unknown")
  --open                      Open the project in the Editor after creation
  -h, --help                  display help for command

Global Options:
  -V, --version               output the version number
  --format <format>           Output format: human, json, tsv, ndjson (env:
                              UNITY_FORMAT)
  --json                      Shorthand for --format json
  --no-banner                 Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive           Disable interactive prompts. Useful in CI/CD
                              environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                     Suppress informational output (env: UNITY_QUIET)
  --verbose                   Show full error details including stack traces on
                              failure (env: UNITY_VERBOSE)
  --proxy <url>               HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                              http://user:pass@host:8080, socks5://host:1080,
                              pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable             Disable proxy for this invocation, ignoring all
                              sources
  --log-proxy                 Log every outbound request to proxy-request.json
                              for this run. Off by default; typically used once
                              when reproducing a proxy issue for support. Also
                              settable via UNITY_LOG_PROXY=1 or the
                              proxyRequestLogging user setting. (env:
                              UNITY_LOG_PROXY)
  --no-log-proxy              Opt out of --log-proxy / UNITY_LOG_PROXY / the
                              persisted user setting for this run. Use when
                              logging is enabled globally but you want one clean
                              invocation.

Examples:
  $ unity projects new MyGame --editor-version 6000.0.30f1
  $ unity projects new MyGame --editor-version 6000.0.30f1 --template com.unity.template.3d
  $ unity projects new MyGame --editor-version lts --path ~/UnityProjects --json

```

```
===== unity projects open --help =====
Usage: unity projects open [options] [pattern]

Open a Unity project by name, fuzzy title match, or path

Arguments:
  pattern                       Project name, fuzzy title pattern, glob, or path
                                (defaults to current directory)

Options:
  --editor-version <version>    Override project Editor version (e.g.
                                2021.3.4f1) (env: UNITY_EDITOR_VERSION)
  -e, --editor-path <path>      Use this Unity Editor application path
  -a, --architecture <arch>     Editor architecture (x86_64 or arm64) (default:
                                "unknown", env: UNITY_ARCHITECTURE)
  --build-target <target>       Pass -buildTarget to Unity (e.g. StandaloneOSX)
  --build-target-group <group>  Pass -buildTargetGroup to Unity
  --args <string>               Extra arguments passed to Unity
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

Resolves the project argument in order:
  1. Exact path or name match in the Hub registry
  2. Glob pattern match (e.g. "My Game*")
  3. Fuzzy title match (e.g. "mgame" matches "My Game")
  4. Falls back to treating the argument as a file system path

Examples:
  $ unity projects open
  $ unity projects open "My Game"
  $ unity projects open mgame
  $ unity projects open "My Game*"
  $ unity projects open /path/to/project
```

```
===== unity projects pin --help =====
Usage: unity projects pin [options] <pattern>

Pin (favorite) one or more projects by name or path pattern

Arguments:
  pattern            Glob filter on project title or path — pins all matching
                     projects

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects pin MyGame
  $ unity projects pin 'My*'
  $ unity projects pin ./path/to/project

```

```
===== unity projects unpin --help =====
Usage: unity projects unpin [options] <pattern>

Unpin (unfavorite) one or more projects by name or path pattern

Arguments:
  pattern            Glob filter on project title or path — unpins all matching
                     projects

Options:
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects unpin MyGame
  $ unity projects unpin 'My*'
  $ unity projects unpin ./path/to/project

```

```
===== unity projects require --help =====
Usage: unity projects require [options] [pathOrName]

Assert that the required editor version is installed, installing it if necessary

Arguments:
  pathOrName         Path or name of a Unity project (defaults to current
                     directory)

Options:
  -y, --yes          Skip confirmation prompt and install automatically
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects require
  $ unity projects require ./MyGame
  $ unity projects require MyGame
  $ unity projects require ./MyGame --yes

```

```
===== unity projects size --help =====
Usage: unity projects size [options] [project]

Report a project's disk usage by folder, or summarize every registered project

Arguments:
  project            Path or name of a Unity project (defaults to current
                     directory)

Options:
  -a, --all          Summarize every registered project, sorted by size
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects size
  $ unity projects size ./MyGame
  $ unity projects size MyGame --json
  $ unity projects size --all

```

```
===== unity projects unlink --help =====
Usage: unity projects unlink [options] [command]

Disconnect a local project from its cloud or version-control link

Options:
  -h, --help              display help for command

Global Options:
  -V, --version           output the version number
  --format <format>       Output format: human, json, tsv, ndjson (env:
                          UNITY_FORMAT)
  --json                  Shorthand for --format json
  --no-banner             Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive       Disable interactive prompts. Useful in CI/CD
                          environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                 Suppress informational output (env: UNITY_QUIET)
  --verbose               Show full error details including stack traces on
                          failure (env: UNITY_VERBOSE)
  --proxy <url>           HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                          http://user:pass@host:8080, socks5://host:1080,
                          pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable         Disable proxy for this invocation, ignoring all
                          sources
  --log-proxy             Log every outbound request to proxy-request.json for
                          this run. Off by default; typically used once when
                          reproducing a proxy issue for support. Also settable
                          via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                          setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy          Opt out of --log-proxy / UNITY_LOG_PROXY / the
                          persisted user setting for this run. Use when logging
                          is enabled globally but you want one clean invocation.

Commands:
  cloud [options] <path>  Disconnect a local project from its Unity Cloud
                          project
  vcs [options] <path>    Remove all git remotes from a local project, including
                          any you added yourself such as upstream (the remote
                          repositories themselves are not deleted)
  help [command]          display help for command
```

```
===== unity projects unlink cloud --help =====
Usage: unity projects unlink cloud [options] <path>

Disconnect a local project from its Unity Cloud project

Arguments:
  path                Path to the local project to unlink

Options:
  --cascade-vcs       Also remove the Unity Version Control workspace bound to
                      this project.
  --unlink-workspace  Confirm cascade removal of a UVCS workspace that contains
                      other projects (workspace-wide).
  -h, --help          display help for command

Global Options:
  -V, --version       output the version number
  --format <format>   Output format: human, json, tsv, ndjson (env:
                      UNITY_FORMAT)
  --json              Shorthand for --format json
  --no-banner         Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive   Disable interactive prompts. Useful in CI/CD environments.
                      (env: UNITY_NON_INTERACTIVE)
  --quiet             Suppress informational output (env: UNITY_QUIET)
  --verbose           Show full error details including stack traces on failure
                      (env: UNITY_VERBOSE)
  --proxy <url>       HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                      http://user:pass@host:8080, socks5://host:1080,
                      pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable     Disable proxy for this invocation, ignoring all sources
  --log-proxy         Log every outbound request to proxy-request.json for this
                      run. Off by default; typically used once when reproducing
                      a proxy issue for support. Also settable via
                      UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                      (env: UNITY_LOG_PROXY)
  --no-log-proxy      Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                      user setting for this run. Use when logging is enabled
                      globally but you want one clean invocation.
```

```
===== unity projects unlink vcs --help =====
Usage: unity projects unlink vcs [options] <path>

Remove all git remotes from a local project, including any you added yourself
such as upstream (the remote repositories themselves are not deleted)

Arguments:
  path                Path to the local project to unlink

Options:
  --unlink-workspace  Confirm removal of a UVCS workspace that contains other
                      projects (workspace-wide).
  -h, --help          display help for command

Global Options:
  -V, --version       output the version number
  --format <format>   Output format: human, json, tsv, ndjson (env:
                      UNITY_FORMAT)
  --json              Shorthand for --format json
  --no-banner         Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive   Disable interactive prompts. Useful in CI/CD environments.
                      (env: UNITY_NON_INTERACTIVE)
  --quiet             Suppress informational output (env: UNITY_QUIET)
  --verbose           Show full error details including stack traces on failure
                      (env: UNITY_VERBOSE)
  --proxy <url>       HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                      http://user:pass@host:8080, socks5://host:1080,
                      pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable     Disable proxy for this invocation, ignoring all sources
  --log-proxy         Log every outbound request to proxy-request.json for this
                      run. Off by default; typically used once when reproducing
                      a proxy issue for support. Also settable via
                      UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                      (env: UNITY_LOG_PROXY)
  --no-log-proxy      Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                      user setting for this run. Use when logging is enabled
                      globally but you want one clean invocation.
```

```
===== unity projects upgrade --help =====
Usage: unity projects upgrade [options] [pathOrName]

Upgrade a Unity project to a different editor version

Arguments:
  pathOrName         Project path or name (defaults to the current directory)

Options:
  --to <version>     Target Unity Editor version (e.g. 6000.0.30f1)
  -y, --yes          Skip confirmation prompt
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity projects upgrade --to 6000.0.30f1
  $ unity projects upgrade megacity-metro --to 6000.0.30f1
  $ unity projects upgrade ./MyGame --to 6000.0.30f1
  $ unity projects upgrade /path/to/MyGame --to 6000.0.30f1 --json

```

```
===== unity projects export --help =====
Usage: unity projects export [options]

Export the Hub project list to a JSON file

Options:
  -o, --output <file>  Write the exported JSON to a file instead of stdout
  -h, --help           display help for command

Global Options:
  -V, --version        output the version number
  --format <format>    Output format: human, json, tsv, ndjson (env:
                       UNITY_FORMAT)
  --json               Shorthand for --format json
  --no-banner          Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive    Disable interactive prompts. Useful in CI/CD
                       environments. (env: UNITY_NON_INTERACTIVE)
  --quiet              Suppress informational output (env: UNITY_QUIET)
  --verbose            Show full error details including stack traces on failure
                       (env: UNITY_VERBOSE)
  --proxy <url>        HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                       http://user:pass@host:8080, socks5://host:1080,
                       pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable      Disable proxy for this invocation, ignoring all sources
  --log-proxy          Log every outbound request to proxy-request.json for this
                       run. Off by default; typically used once when reproducing
                       a proxy issue for support. Also settable via
                       UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                       setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy       Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                       user setting for this run. Use when logging is enabled
                       globally but you want one clean invocation.

Examples:
  $ unity projects export                         # write JSON to stdout
  $ unity projects export --output projects.json  # write to file
  $ unity projects export | jq '.data | length'   # count exported projects

```

```
===== unity projects import --help =====
Usage: unity projects import [options] [file]

Import a Hub project list from a previously exported JSON file

Arguments:
  file                JSON export file from 'unity projects export' (reads from
                      stdin if omitted)

Options:
  -i, --input <file>  Read the export JSON from a file
  -h, --help          display help for command

Global Options:
  -V, --version       output the version number
  --format <format>   Output format: human, json, tsv, ndjson (env:
                      UNITY_FORMAT)
  --json              Shorthand for --format json
  --no-banner         Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive   Disable interactive prompts. Useful in CI/CD environments.
                      (env: UNITY_NON_INTERACTIVE)
  --quiet             Suppress informational output (env: UNITY_QUIET)
  --verbose           Show full error details including stack traces on failure
                      (env: UNITY_VERBOSE)
  --proxy <url>       HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                      http://user:pass@host:8080, socks5://host:1080,
                      pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable     Disable proxy for this invocation, ignoring all sources
  --log-proxy         Log every outbound request to proxy-request.json for this
                      run. Off by default; typically used once when reproducing
                      a proxy issue for support. Also settable via
                      UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                      (env: UNITY_LOG_PROXY)
  --no-log-proxy      Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                      user setting for this run. Use when logging is enabled
                      globally but you want one clean invocation.

Examples:
  $ unity projects import projects.json           # import from file
  $ unity projects import --input projects.json   # same with --input flag
  $ cat projects.json | unity projects import     # read from stdin
  $ unity projects export | unity projects import # round-trip via pipe

```

```
===== unity command --help =====
Usage: unity command|cmd [options] [command] [args...]

Execute commands on connected Unity Editor instances, or list available commands

Arguments:
  command                       Command to execute on Unity Editor (omit to list
                                available commands)
  args                          Arguments for the command

Options:
  --project-path <path>         Path to Unity project (auto-detected if not
                                specified) (env: UNITY_PROJECT_PATH)
  --runtime <player exec name>  Connect to a Unity Player runtime instance
                                (search by process name)
  --runtime-path <path>         Connect to a Unity Player by path to port file
  --timeout <seconds>           Timeout for command execution (default: 30)
                                (default: "30")
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.
```

```
===== unity templates --help =====
Usage: unity templates|t [options] [command]

Browse, inspect, create, edit, and delete Unity project templates

Options:
  -h, --help                       display help for command

Global Options:
  -V, --version                    output the version number
  --format <format>                Output format: human, json, tsv, ndjson (env:
                                   UNITY_FORMAT)
  --json                           Shorthand for --format json
  --no-banner                      Suppress the startup banner (env:
                                   UNITY_NO_BANNER)
  --non-interactive                Disable interactive prompts. Useful in CI/CD
                                   environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                          Suppress informational output (env:
                                   UNITY_QUIET)
  --verbose                        Show full error details including stack
                                   traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                    HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                   http://user:pass@host:8080,
                                   socks5://host:1080, pac+http://wpad/proxy.pac
                                   (env: UNITY_PROXY)
  --proxy-disable                  Disable proxy for this invocation, ignoring
                                   all sources
  --log-proxy                      Log every outbound request to
                                   proxy-request.json for this run. Off by
                                   default; typically used once when reproducing
                                   a proxy issue for support. Also settable via
                                   UNITY_LOG_PROXY=1 or the proxyRequestLogging
                                   user setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy                   Opt out of --log-proxy / UNITY_LOG_PROXY /
                                   the persisted user setting for this run. Use
                                   when logging is enabled globally but you want
                                   one clean invocation.

Commands:
  list [options]                   List Unity project templates for an editor
                                   version
  info [options] <name>            Show detailed information about a Unity
                                   project template
  create [options] <project-path>  Create a custom template from an existing
                                   Unity project
  delete [options] <name>          Delete a user-generated custom template
  location [options]               Get, set, or reset the default storage path
                                   for custom templates
  edit [options] <name>            Edit a custom (user-generated) template's
                                   metadata
  help [command]                   display help for command
```

```
===== unity templates list --help =====
Usage: unity templates list [options]

List Unity project templates for an editor version

Options:
  -e, --editor <version>  Unity editor version (uses stored default if omitted)
  -i, --installed         Show only locally installed templates
  -t, --type <type>       Filter by template type (core, learning, sample,
                          custom, new, all)
  --custom                Show only user-generated (custom) templates
  -h, --help              display help for command

Global Options:
  -V, --version           output the version number
  --format <format>       Output format: human, json, tsv, ndjson (env:
                          UNITY_FORMAT)
  --json                  Shorthand for --format json
  --no-banner             Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive       Disable interactive prompts. Useful in CI/CD
                          environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                 Suppress informational output (env: UNITY_QUIET)
  --verbose               Show full error details including stack traces on
                          failure (env: UNITY_VERBOSE)
  --proxy <url>           HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                          http://user:pass@host:8080, socks5://host:1080,
                          pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable         Disable proxy for this invocation, ignoring all
                          sources
  --log-proxy             Log every outbound request to proxy-request.json for
                          this run. Off by default; typically used once when
                          reproducing a proxy issue for support. Also settable
                          via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                          setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy          Opt out of --log-proxy / UNITY_LOG_PROXY / the
                          persisted user setting for this run. Use when logging
                          is enabled globally but you want one clean invocation.

Examples:
  $ unity templates list --editor 6000.0.0f1
  $ unity templates list --editor 6000.0.0f1 --custom
  $ unity templates list --editor 6000.0.0f1 --type core
  $ unity templates list --editor 2023.3 --installed
  $ unity templates list --editor 6000.0.0f1 --json

```

```
===== unity templates info --help =====
Usage: unity templates info [options] <name>

Show detailed information about a Unity project template

Arguments:
  name                    Template package name (e.g. com.unity.template.3d)

Options:
  -e, --editor <version>  Unity editor version (uses stored default if omitted)
  -h, --help              display help for command

Global Options:
  -V, --version           output the version number
  --format <format>       Output format: human, json, tsv, ndjson (env:
                          UNITY_FORMAT)
  --json                  Shorthand for --format json
  --no-banner             Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive       Disable interactive prompts. Useful in CI/CD
                          environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                 Suppress informational output (env: UNITY_QUIET)
  --verbose               Show full error details including stack traces on
                          failure (env: UNITY_VERBOSE)
  --proxy <url>           HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                          http://user:pass@host:8080, socks5://host:1080,
                          pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable         Disable proxy for this invocation, ignoring all
                          sources
  --log-proxy             Log every outbound request to proxy-request.json for
                          this run. Off by default; typically used once when
                          reproducing a proxy issue for support. Also settable
                          via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                          setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy          Opt out of --log-proxy / UNITY_LOG_PROXY / the
                          persisted user setting for this run. Use when logging
                          is enabled globally but you want one clean invocation.

Examples:
  $ unity templates info com.unity.template.3d --editor 6000.0.0f1
  $ unity templates info com.unity.template.urp --editor 6000.0.0f1 --json

```

```
===== unity templates create --help =====
Usage: unity templates create [options] <project-path>

Create a custom template from an existing Unity project

Arguments:
  project-path                  Path to the Unity project to package as a
                                template

Options:
  --name <package-name>         Template package name (e.g.
                                com.myorg.template.mytemplate)
  --display-name <name>         Human-readable template name (e.g. "My
                                Template")
  --description <text>          Short description of the template
  --template-version <version>  Template version (e.g. 1.0.0) — use
                                --template-version to avoid conflict with the
                                global --version flag
  --output <path>               Directory to write the template archive to
                                (default: configured user templates location)
  --keep-embedded-packages      Keep embedded packages in the template archive
  --keep-project-settings       Keep project settings in the template archive
  --overwrite                   Replace an existing template archive without
                                error
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

Examples:
  $ unity templates create ./MyProject --name com.myorg.template.mytemplate --display-name "My Template"
  $ unity templates create ./MyProject --name com.myorg.template.mytemplate --display-name "My Template" --template-version 1.0.0 --output ~/templates
  $ unity templates create ./MyProject --name com.myorg.template.mytemplate --display-name "My Template" --overwrite --json

```

```
===== unity templates delete --help =====
Usage: unity templates delete [options] <name>

Delete a user-generated custom template

Arguments:
  name                    Template package name or display name to delete

Options:
  -e, --editor <version>  Unity editor version (uses stored default if omitted)
  -y, --yes               Skip confirmation prompt
  -h, --help              display help for command

Global Options:
  -V, --version           output the version number
  --format <format>       Output format: human, json, tsv, ndjson (env:
                          UNITY_FORMAT)
  --json                  Shorthand for --format json
  --no-banner             Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive       Disable interactive prompts. Useful in CI/CD
                          environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                 Suppress informational output (env: UNITY_QUIET)
  --verbose               Show full error details including stack traces on
                          failure (env: UNITY_VERBOSE)
  --proxy <url>           HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                          http://user:pass@host:8080, socks5://host:1080,
                          pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable         Disable proxy for this invocation, ignoring all
                          sources
  --log-proxy             Log every outbound request to proxy-request.json for
                          this run. Off by default; typically used once when
                          reproducing a proxy issue for support. Also settable
                          via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                          setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy          Opt out of --log-proxy / UNITY_LOG_PROXY / the
                          persisted user setting for this run. Use when logging
                          is enabled globally but you want one clean invocation.

Examples:
  $ unity templates delete com.myorg.template.mytemplate --editor 6000.0.0f1
  $ unity templates delete com.myorg.template.mytemplate --editor 6000.0.0f1 --yes
  $ unity templates delete com.myorg.template.mytemplate --editor 6000.0.0f1 --json

```

```
===== unity templates location --help =====
Usage: unity templates location [options]

Get, set, or reset the default storage path for custom templates

Options:
  -s, --set <path>   Set the default templates storage path
  -r, --reset        Reset to the Hub default templates path
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.

Examples:
  $ unity templates location
  $ unity templates location --set /path/to/templates
  $ unity templates location --reset
  $ unity templates location --json

```

```
===== unity templates edit --help =====
Usage: unity templates edit [options] <name>

Edit a custom (user-generated) template's metadata

Arguments:
  name                          Template package name or display name (e.g.
                                com.myorg.template.mytemplate)

Options:
  -e, --editor <version>        Unity editor version (uses stored default if
                                omitted)
  --display-name <name>         New display name for the template
  --description <text>          New description for the template
  --template-version <version>  New version for the template (e.g. 1.1.0)
  --preview-image <path>        Path to a new preview image for the template
  --remove-preview-image        Remove the current preview image from the
                                template
  -y, --yes                     Skip confirmation prompt
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

Examples:
  $ unity templates edit com.myorg.template.mytemplate --editor 6000.0.0f1 --display-name "My Updated Template"
  $ unity templates edit com.myorg.template.mytemplate --editor 6000.0.0f1 --description "New description" --template-version 1.1.0
  $ unity templates edit com.myorg.template.mytemplate --editor 6000.0.0f1 --preview-image /path/to/image.png
  $ unity templates edit com.myorg.template.mytemplate --editor 6000.0.0f1 --remove-preview-image
  $ unity templates edit com.myorg.template.mytemplate --editor 6000.0.0f1 --display-name "My Template" --yes --json

```

```
===== unity test --help =====
Usage: unity test [options] [project]

Run a project's EditMode/PlayMode tests in the editor and write a results report

Arguments:
  project                     Project path or name (defaults to the current
                              directory)

Options:
  --mode <mode>               Test platform to run: EditMode or PlayMode. If
                              omitted, the editor's default test platform runs.
  --filter <pattern>          Run only tests whose names match this filter
  --output <path>             Path to write the NUnit XML results report
                              (default: "test-results.xml")
  --editor-version <version>  Override editor version (default: from
                              ProjectVersion.txt) (env: UNITY_EDITOR_VERSION)
  -e, --editor-path <path>    Path to a specific editor binary
  -a, --architecture <arch>   Editor architecture (x86_64 or arm64) (default:
                              "unknown", env: UNITY_ARCHITECTURE)
  --allow-install             Install the project's editor version if it is not
                              already installed
  --timeout <seconds>         Kill the Unity process after this many seconds
                              (disabled by default) (env: UNITY_TEST_TIMEOUT)
  -h, --help                  display help for command

Global Options:
  -V, --version               output the version number
  --format <format>           Output format: human, json, tsv, ndjson (env:
                              UNITY_FORMAT)
  --json                      Shorthand for --format json
  --no-banner                 Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive           Disable interactive prompts. Useful in CI/CD
                              environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                     Suppress informational output (env: UNITY_QUIET)
  --verbose                   Show full error details including stack traces on
                              failure (env: UNITY_VERBOSE)
  --proxy <url>               HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                              http://user:pass@host:8080, socks5://host:1080,
                              pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable             Disable proxy for this invocation, ignoring all
                              sources
  --log-proxy                 Log every outbound request to proxy-request.json
                              for this run. Off by default; typically used once
                              when reproducing a proxy issue for support. Also
                              settable via UNITY_LOG_PROXY=1 or the
                              proxyRequestLogging user setting. (env:
                              UNITY_LOG_PROXY)
  --no-log-proxy              Opt out of --log-proxy / UNITY_LOG_PROXY / the
                              persisted user setting for this run. Use when
                              logging is enabled globally but you want one clean
                              invocation.

Examples:
  $ unity test
  $ unity test ./MyProject --mode EditMode
  $ unity test "My Game" --mode PlayMode --output ./results/play.xml
  $ unity test . --filter "MyNamespace.MyTests" --editor-version 6000.0
  $ unity test . -- -nographics

```

```
===== unity open --help =====
Usage: unity open [options] [project]

Open a Unity project with the correct Editor version

Arguments:
  project                       Project name, glob pattern, or file path
                                (defaults to current directory)

Options:
  --editor-version <version>    Override project Editor version (e.g.
                                2021.3.4f1) (env: UNITY_EDITOR_VERSION)
  -e, --editor-path <path>      Use this Unity Editor application path
  -a, --architecture <arch>     Editor architecture (x86_64 or arm64) (default:
                                "unknown", env: UNITY_ARCHITECTURE)
  --build-target <target>       Pass -buildTarget to Unity (e.g. StandaloneOSX)
  --build-target-group <group>  Pass -buildTargetGroup to Unity
  --args <string>               Extra arguments passed to Unity
  -h, --help                    display help for command

Global Options:
  -V, --version                 output the version number
  --format <format>             Output format: human, json, tsv, ndjson (env:
                                UNITY_FORMAT)
  --json                        Shorthand for --format json
  --no-banner                   Suppress the startup banner (env:
                                UNITY_NO_BANNER)
  --non-interactive             Disable interactive prompts. Useful in CI/CD
                                environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                       Suppress informational output (env: UNITY_QUIET)
  --verbose                     Show full error details including stack traces
                                on failure (env: UNITY_VERBOSE)
  --proxy <url>                 HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                http://user:pass@host:8080, socks5://host:1080,
                                pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable               Disable proxy for this invocation, ignoring all
                                sources
  --log-proxy                   Log every outbound request to proxy-request.json
                                for this run. Off by default; typically used
                                once when reproducing a proxy issue for support.
                                Also settable via UNITY_LOG_PROXY=1 or the
                                proxyRequestLogging user setting. (env:
                                UNITY_LOG_PROXY)
  --no-log-proxy                Opt out of --log-proxy / UNITY_LOG_PROXY / the
                                persisted user setting for this run. Use when
                                logging is enabled globally but you want one
                                clean invocation.

The project argument is matched against the Hub project registry first:
  - Exact name or path → opens immediately
  - Glob pattern (e.g. "My Game*") → prompts to select when multiple match
  - No registry match → falls back to treating the argument as a file system path

Examples:
  $ unity open
  $ unity open "My Game"
  $ unity open "My Game*"
  $ unity open /path/to/project
  $ unity open . --editor-version 6000.0.0f1
```

```
===== unity run --help =====
Usage: unity run [options] [project]

Run a Unity project in batch mode and forward args to the editor

Arguments:
  project                     Project path or name (defaults to the current
                              directory)

Options:
  --editor-version <version>  Override editor version (default: from
                              ProjectVersion.txt) (env: UNITY_EDITOR_VERSION)
  -e, --editor-path <path>    Path to a specific editor binary
  -a, --architecture <arch>   Editor architecture (x86_64 or arm64) (default:
                              "unknown", env: UNITY_ARCHITECTURE)
  --allow-install             Install the project's editor version if it is not
                              already installed
  --command <name>            Execute a registered Editor command headlessly
                              (args after -- are parsed against the command's
                              schema, not forwarded to Unity)
  --timeout <seconds>         Kill the Unity process after this many seconds
                              (disabled by default) (env: UNITY_RUN_TIMEOUT)
  -h, --help                  display help for command

Global Options:
  -V, --version               output the version number
  --format <format>           Output format: human, json, tsv, ndjson (env:
                              UNITY_FORMAT)
  --json                      Shorthand for --format json
  --no-banner                 Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive           Disable interactive prompts. Useful in CI/CD
                              environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                     Suppress informational output (env: UNITY_QUIET)
  --verbose                   Show full error details including stack traces on
                              failure (env: UNITY_VERBOSE)
  --proxy <url>               HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                              http://user:pass@host:8080, socks5://host:1080,
                              pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable             Disable proxy for this invocation, ignoring all
                              sources
  --log-proxy                 Log every outbound request to proxy-request.json
                              for this run. Off by default; typically used once
                              when reproducing a proxy issue for support. Also
                              settable via UNITY_LOG_PROXY=1 or the
                              proxyRequestLogging user setting. (env:
                              UNITY_LOG_PROXY)
  --no-log-proxy              Opt out of --log-proxy / UNITY_LOG_PROXY / the
                              persisted user setting for this run. Use when
                              logging is enabled globally but you want one clean
                              invocation.

Examples:
  $ unity run
  $ unity run ./MyProject -- -executeMethod Builder.Build
  $ unity run "My Game" --editor-version 6000.0 -- -nographics -quit
  $ unity run . --allow-install -- -logFile ./build.log -quit
  $ unity run . --command greet -- --name Ada
  $ unity run ./MyProject --command my_build --format json -- --target StandaloneWindows64

```

```
===== unity releases --help =====
Usage: unity releases [options]

List available Unity releases

Options:
  --lts              Show only LTS releases
  --stream <stream>  Filter by release stream: alpha, beta, lts, tech
  --since <year>     Show releases from this year onwards (e.g. 2023)
  --limit <n>        Maximum number of releases to show (default: 20) (default:
                     20)
  --skip <n>         Skip the first N releases (for pagination) (default: 0)
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity self-uninstall --help =====
Usage: unity self-uninstall [options]

Uninstall the unity CLI (removes the binary and environment files)

Options:
  -y, --yes          Skip confirmation prompt
  --purge            Also remove user data directory
  --dry-run          Preview which files would be removed without uninstalling
  -h, --help         display help for command

Global Options:
  -V, --version      output the version number
  --format <format>  Output format: human, json, tsv, ndjson (env: UNITY_FORMAT)
  --json             Shorthand for --format json
  --no-banner        Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive  Disable interactive prompts. Useful in CI/CD environments.
                     (env: UNITY_NON_INTERACTIVE)
  --quiet            Suppress informational output (env: UNITY_QUIET)
  --verbose          Show full error details including stack traces on failure
                     (env: UNITY_VERBOSE)
  --proxy <url>      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                     http://user:pass@host:8080, socks5://host:1080,
                     pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable    Disable proxy for this invocation, ignoring all sources
  --log-proxy        Log every outbound request to proxy-request.json for this
                     run. Off by default; typically used once when reproducing a
                     proxy issue for support. Also settable via
                     UNITY_LOG_PROXY=1 or the proxyRequestLogging user setting.
                     (env: UNITY_LOG_PROXY)
  --no-log-proxy     Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                     user setting for this run. Use when logging is enabled
                     globally but you want one clean invocation.
```

```
===== unity shell --help =====
Usage: unity shell [options]

Start an interactive shell (REPL) that runs many commands in one warm process

Options:
  --protocol <protocol>  Machine-readable request/response protocol over stdio
                         (ndjson) instead of the interactive prompt (choices:
                         "ndjson")
  -h, --help             display help for command

Global Options:
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.
```

```
===== unity status --help =====
Usage: unity status [options]

Show live state of every connected Unity Editor (port, project, version, PID,
state)

Options:
  --port <number>        Filter to a single Editor instance on this port
  --project-path <path>  Filter by project path substring (case-insensitive)
  -h, --help             display help for command

Global Options:
  -V, --version          output the version number
  --format <format>      Output format: human, json, tsv, ndjson (env:
                         UNITY_FORMAT)
  --json                 Shorthand for --format json
  --no-banner            Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive      Disable interactive prompts. Useful in CI/CD
                         environments. (env: UNITY_NON_INTERACTIVE)
  --quiet                Suppress informational output (env: UNITY_QUIET)
  --verbose              Show full error details including stack traces on
                         failure (env: UNITY_VERBOSE)
  --proxy <url>          HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                         http://user:pass@host:8080, socks5://host:1080,
                         pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable        Disable proxy for this invocation, ignoring all sources
  --log-proxy            Log every outbound request to proxy-request.json for
                         this run. Off by default; typically used once when
                         reproducing a proxy issue for support. Also settable
                         via UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                         setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy         Opt out of --log-proxy / UNITY_LOG_PROXY / the
                         persisted user setting for this run. Use when logging
                         is enabled globally but you want one clean invocation.
```

```
===== unity uninstall --help =====
Usage: unity uninstall|u [options] [version]

Uninstall an installed Unity editor

Arguments:
  version                            Version of the editor to uninstall — full
                                     (6000.2.12f1) or short alias (6.2.12f1,
                                     6.2). Uses stored default if omitted.

Options:
  -a, --architecture <architecture>  Editor architecture (x86_64 or arm64)
                                     (default: "unknown")
  -y, --yes                          Automatically select the first match
                                     without prompting
  -h, --help                         display help for command

Global Options:
  -V, --version                      output the version number
  --format <format>                  Output format: human, json, tsv, ndjson
                                     (env: UNITY_FORMAT)
  --json                             Shorthand for --format json
  --no-banner                        Suppress the startup banner (env:
                                     UNITY_NO_BANNER)
  --non-interactive                  Disable interactive prompts. Useful in
                                     CI/CD environments. (env:
                                     UNITY_NON_INTERACTIVE)
  --quiet                            Suppress informational output (env:
                                     UNITY_QUIET)
  --verbose                          Show full error details including stack
                                     traces on failure (env: UNITY_VERBOSE)
  --proxy <url>                      HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                                     http://user:pass@host:8080,
                                     socks5://host:1080,
                                     pac+http://wpad/proxy.pac (env:
                                     UNITY_PROXY)
  --proxy-disable                    Disable proxy for this invocation, ignoring
                                     all sources
  --log-proxy                        Log every outbound request to
                                     proxy-request.json for this run. Off by
                                     default; typically used once when
                                     reproducing a proxy issue for support. Also
                                     settable via UNITY_LOG_PROXY=1 or the
                                     proxyRequestLogging user setting. (env:
                                     UNITY_LOG_PROXY)
  --no-log-proxy                     Opt out of --log-proxy / UNITY_LOG_PROXY /
                                     the persisted user setting for this run.
                                     Use when logging is enabled globally but
                                     you want one clean invocation.

Version aliases:
  Unity 6 and later use a short alias form — run "unity editors" to see the
  "Alias" column alongside each installed editor.

Examples:
  $ unity uninstall 6000.2.12f1        # exact internal version
  $ unity uninstall 6.2.12f1           # short alias (same editor)
  $ unity uninstall 6.2                # matches all 6.2.x — prompts if ambiguous
  $ unity uninstall 6.2 -y             # auto-select first match, no prompt
  $ unity uninstall 6.2 -a x86_64     # narrow by architecture
  $ unity uninstall                    # uses stored default editor version

```

```
===== unity upgrade --help =====
Usage: unity upgrade [options]

Upgrade the unity CLI to the latest version

Options:
  --check              Check for updates without installing
  --changelog          Show release notes for the target version and exit
  -y, --yes            Skip confirmation prompt
  --channel <channel>  Update channel: stable or beta
  --target <version>   Upgrade to a specific version
  --rollback           Restore the previous binary installed before the last
                       upgrade
  --dry-run            Preview the upgrade without downloading or installing
  -h, --help           display help for command

Global Options:
  -V, --version        output the version number
  --format <format>    Output format: human, json, tsv, ndjson (env:
                       UNITY_FORMAT)
  --json               Shorthand for --format json
  --no-banner          Suppress the startup banner (env: UNITY_NO_BANNER)
  --non-interactive    Disable interactive prompts. Useful in CI/CD
                       environments. (env: UNITY_NON_INTERACTIVE)
  --quiet              Suppress informational output (env: UNITY_QUIET)
  --verbose            Show full error details including stack traces on failure
                       (env: UNITY_VERBOSE)
  --proxy <url>        HTTP/HTTPS/SOCKS/PAC proxy URL. Examples:
                       http://user:pass@host:8080, socks5://host:1080,
                       pac+http://wpad/proxy.pac (env: UNITY_PROXY)
  --proxy-disable      Disable proxy for this invocation, ignoring all sources
  --log-proxy          Log every outbound request to proxy-request.json for this
                       run. Off by default; typically used once when reproducing
                       a proxy issue for support. Also settable via
                       UNITY_LOG_PROXY=1 or the proxyRequestLogging user
                       setting. (env: UNITY_LOG_PROXY)
  --no-log-proxy       Opt out of --log-proxy / UNITY_LOG_PROXY / the persisted
                       user setting for this run. Use when logging is enabled
                       globally but you want one clean invocation.
```
