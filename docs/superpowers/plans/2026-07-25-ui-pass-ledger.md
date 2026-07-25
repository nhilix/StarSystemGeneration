# Atlas UI design pass — pass ledger

The resumability record for **all three tiers** of the atlas UI design pass.
Governing spec: `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`.
Tier 0 kickoff: `docs/superpowers/plans/2026-07-25-ui-pass-tier0-kickoff-prompt.md`.

**This is a design pass.** No atlas code, no sim behavior, no `unity/Assets`
edits, `dotnet test` untouched. Outputs are `docs/design/ui/*` + mock artifacts.

Branch: `ui-pass-tier0` off main `cbb892d`.

---

## Tier 0 — Inventory

| # | Task | Gate | Status |
|---|---|---|---|
| 0.1 | Scope nod | user | ✅ nodded |
| 0.2 | Branch `ui-pass-tier0` | — | ✅ |
| 0.3 | Start this ledger | — | ✅ |
| 0.4 | Code sweep — **inline**, all ~7.9k lines of `unity/Assets/Atlas` + the Core lenses it leans on | — | ✅ |
| 0.5 | Generate evidence — standard grid + degenerate grid + smoke suite | — | ✅ |
| 0.6 | Read the evidence (images, not recall) — 22 shots read | — | ✅ |
| 0.7 | Write `docs/design/ui/inventory.md` | — | ✅ |
| 0.8 | Fix the group partition (incl. camera & navigation) | — | ✅ proposed: 6 groups |
| 0.9 | **User checkpoint** — completeness skim + partition nod | **user** | ⬜ |
| 0.10 | Wrap-up: commit, HANDOFF, push, Trello | — | ⬜ |

**Tier 0 does NOT write the Tier 1 kickoff** — the orchestrator authors tier
kickoffs after each gate (user decision, 2026-07-25). Tier 0 hands back:
inventory path, nodded partition, and Tier-1 notes the inventory doesn't carry.

### 0.4 — the sweep was done inline, not by subagents

**What happened.** Seven parallel Sonnet Explore subagents were dispatched for
the sweep lanes. **None of them ever started**, and the session locked up and had
to be restarted. The branch, the ledger and all 74 evidence artifacts survived;
the sweep did not.

The sweep was then redone **inline** — `unity/Assets/Atlas` is only ~7,900 lines
across 38 files, plus the handful of `src/Core/Atlas` lenses the encodings lean
on. Reading it directly cost more tokens than a fan-out but is deterministic, and
at this size that is the right trade. **A future tier should default to inline
reading for this codebase** rather than re-risking the fan-out.

Two harness notes worth keeping:

- Passing `name:` to the Agent tool makes an agent addressable, but this harness
  also spawns a psmux pane per named agent with *nothing piped into it* — bare
  `PS>` shells that shred the window layout for no benefit. Spawn unnamed.
- The panes vanish with a session restart, so no manual cleanup was needed.

### 0.4b — what the inline sweep covered

`LodBands` · `CameraRig` · `AtlasRoot` · `SimHost` · `AtlasGeometry`/`AtlasTextures` ·
`AtlasGlyphs` · `GlyphLayerBase` · `DotMarkLayer` · `StarfieldLayer` ·
`DomainFieldLayer` · `DomainInteriorLayer`/`Marks` · `OutpostLayer` ·
`NatureFieldLayer` · `PriceFieldLayer` · `LatticeLayer` · `LaneLayer` ·
`FlowTrailLayer` · `CrawlPathLayer` · `PortLayer` · `FleetLayer` · `PoiLayer` ·
`WorksLayer` · `WarLayer` · `PlagueLayer` · `NewsLayer` · `AtlasChrome` ·
`LensRail` · `TopBar` · `LegendPanel` · `TimelineStrip` · `HexTooltip` ·
`SelectionModel` · `InspectorDock` · `DockKit` · `PanelViews` · `SystemStage` ·
`AtlasChrome.uss` · `SSGPalette-Ice.uss`, plus `AtlasPalette`, `TensionLens`,
`TechLens`, `LegendQuery` on the Core side, and both editor capture tools.

### 0.5 — evidence recipes (reproducible; output is gitignored)

Both grids were rendered against branch `ui-pass-tier0` (= main `cbb892d`
atlas code) with a **warm editor** on port 7800, per
`.claude/skills/driving-the-unity-editor/SKILL.md`.

**Standard six-seed × six-lens grid** → `atlas-grid/` (36 PNG + `index.html`):

```bash
mkdir -p runs/atlas-grid && printf 'epoch 42 40 21\nesave runs/atlas-grid/seed-42.txt\nepoch 7 40 21\nesave runs/atlas-grid/seed-7.txt\nepoch 1234 40 21\nesave runs/atlas-grid/seed-1234.txt\nepoch 9091 40 21\nesave runs/atlas-grid/seed-9091.txt\nepoch 31337 40 21\nesave runs/atlas-grid/seed-31337.txt\nepoch 2718 40 21\nesave runs/atlas-grid/seed-2718.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --project-path $P --format json
```
Defaults: lenses `galaxy,domains,trade,price,war,works`, 1200×750.

**Degenerate/sparse grid** → `atlas-grid-degen/` (18 PNG + `index.html`).
Three distinct degeneracies, deliberately chosen:

| File | Config | The degeneracy it probes |
|---|---|---|
| `seed-42` | `epoch 42 2 21` | **young galaxy, full extent** — 2 polities in a radius-21 field |
| `seed-7` | `epoch 7 5 21` | **young + peaceful** — no wars, no trade, content in one corner |
| `seed-1234` | `epoch 1234 40 5` | **tiny galaxy** — mature but radius 5, ~6 ports, 1 lane |

```bash
mkdir -p runs/atlas-degen && printf 'epoch 42 2 21\nesave runs/atlas-degen/seed-42.txt\nepoch 7 5 21\nesave runs/atlas-degen/seed-7.txt\nepoch 1234 40 5\nesave runs/atlas-degen/seed-1234.txt\nquit\n' | dotnet run --project src/Inspector
```
```powershell
unity command atlas_grid --input runs/atlas-degen --output atlas-grid-degen --seeds 42,7,1234 --project-path $P --format json
```

`atlas-grid-degen/` matches the `atlas-grid*/` gitignore glob — output stays
untracked, as intended.

**Seed-42 warning, carried from the skill:** the grid's seed-42 row is
radius **21**; the committed golden `SimHost` loads is radius **12**
(`tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`, `GCONFIG|42|12|…`).
Same seed, different galaxy. Never cite one as evidence for the other.

Also regenerated: the **smoke suite** (`unity command menu --path 'StarGen/Atlas
Smoke Shots'`), 18 shots at 1600×1000. It carries ten lenses the grid's six never
show — nature, currency, tension, tech, plague, news, POIs, fleets, traffic,
region — plus both SystemStage views. Grid + smoke together are the census.

### 0.6 — two gaps in the evidence apparatus itself

Both are recorded in the inventory's "evidence base" section and both are
**Tier-1 prerequisites, not Tier-0 blockers**.

1. **No capture path renders the chrome.** `AtlasSmoke` and `AtlasGrid` both
   capture via `cam.Render()` into a RenderTexture
   (`unity/Assets/Editor/AtlasSmoke.cs:283`), which bypasses the UI Toolkit
   overlay. Chrome and panels appear in **zero of the 54+18 shots**, so their
   inventory entries are code-cited. Tier 1's chrome and panels groups need a
   chrome-inclusive capture before they can audit how those elements *read*.
2. **The committed acceptance suite photographs a near-empty world.**
   `AtlasSmoke` loads the radius-12 seed-42 golden — 2 domains, ~6 ports — and
   shoots 18 lenses over it. Fleets shows one fleet, plague shows no plague,
   traffic shows one lane. Most of the suite is photographing empty states
   without labelling them as such.

---

### 0.8 — proposed partition (awaiting the nod)

Six groups, not the spec's five. Full rationale in
`docs/design/ui/inventory.md` §10.

1. **Camera, navigation & the LOD spine** — *must run first*; every other
   group's readability questions are answered in terms of bands and fades.
2. **Map fields & lenses**
3. **Marks, billboards & the glyph vocabulary**
4. **Lanes, flows & motion** — split out of the spec's "map fields": strokes are
   a different encoding problem from rasters, and they are the only elements
   carrying time in their form.
5. **Chrome** — blocked on the chrome-capture gap (0.6).
6. **Panels & selection**

**SystemStage is folded in, not dropped** — its concerns distribute across 1, 2,
3 and 6. It works as a standalone seventh group if the user prefers, scheduled
after 1–3.

Ordering: 1 before all. 2–4 mutually independent. 6 benefits from following 5.

---

## Tier 1 — Deep dive per group

Not started. One session per group, each ending in a user eyeball.
Groups fixed by the Tier-0 partition nod (task 0.8/0.9).

**Carry into Tier 1** (not in the inventory itself):
- The chrome-capture gap must be closed before group 5 can do a real audit.
- Prefer inline reading over subagent fan-out in this codebase (see 0.4).

## Tier 2 — Synthesis

Not started. Icon manifest · token conformance · interaction grammar ·
ranked implementation kickoffs.
