# UI design pass — Tier 0 kickoff (inventory)

You are opening **Tier 0 of the atlas UI design pass**: inventory every major UI
element in the Unity atlas, and fix the group partition the five Tier-1 deep
dives will use.

**The governing spec is `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md`
— read it first and in full.** It defines the principles, the three tiers, the
Tier-1 template, and the deliverables. This kickoff only adds what has changed
since that spec was written, plus the Tier-0 specifics.

## This is a DESIGN pass — no atlas code

Outputs are living design docs (`docs/design/ui/`) and mock artifacts. **No
atlas implementation anywhere in the pass**; implementation happens in later
slices that Tier 2 defines. `dotnet test` untouched, no sim behavior, no
`unity/Assets` edits.

## What changed since the spec (read this, it's the useful part)

Slices **UP** (main `ca6d6f7`) and **WG** (main `5076dfd`) both landed. The
spec's "evidence base" is now a real, *parameterized* instrument:

- **`unity command atlas_grid`** renders seeds × lenses into PNGs plus a
  self-contained `atlas-grid/index.html` contact sheet. **Read it down a
  column** — one lens across many worlds is what tells you whether a look is
  the design or the seed. All nine args are optional:
  `input · output · lenses · seeds · width · height · zoom · pitch · anchor`.
- **`.claude/skills/driving-the-unity-editor/SKILL.md`** is the operating
  manual — how to start the editor, the standard six-seed recipe and its
  regeneration one-liner, and **five silent traps**. Read it before your first
  Unity command; the worst trap (`key=value` args ignored while reporting
  `success:true`) will otherwise waste your time.
- **Capture runs no longer dirty the scene asset** (WG1), so generating
  evidence is cheap and leaves no cleanup. One caveat, in the skill: don't
  manually Ctrl+S the atlas scene after a capture.

**One correction to the spec's execution model.** It says the pass has "no Unity
editor dependency except reading the UP grid (so the pass never contends for the
editor)". Reading *existing* grid output is indeed editor-free — but **generating
fresh evidence needs the editor**, and atlas editing stays serial. So: batch your
capture needs, take the editor when you need it, release it. Don't run alongside
another atlas session.

**Do not trust seed 42 alone, and don't assume it matches the golden.** The
grid's seed-42 row is radius **21**; the committed golden `SimHost` loads is
radius **12** — same seed, different galaxy. (This exact confusion was caught in
review; don't re-introduce it.)

## Tier 0 tasks

Ledger: `docs/superpowers/plans/YYYY-MM-DD-ui-pass-ledger.md` — the pass's
resumability record across all three tiers, not just this one. Start it now.

1. **Sweep the code.** `src/Core/Atlas/` (the queries) and `unity/Assets/Atlas/`
   (the renderers/chrome/panels). Use **Explore/research subagents (Sonnet)** for
   the sweeps — this is exactly the fan-out case. The K1–K5 + AC ledgers carry
   the deferred-polish lists; harvest them as the "known debts" column.
2. **Generate the evidence.** A default six-seed × six-lens grid at minimum. Add
   targeted grids where the inventory needs them — in particular a **sparse or
   degenerate galaxy**, since the spec makes empty/degenerate states first-class
   in Tier 1 and that is where encodings collapse.
3. **Write `docs/design/ui/inventory.md`.** One entry per element:
   what it renders · the Core query/data source feeding it · current
   interactions and states · current visual encoding · LOD behavior · known
   debts. **Evidence over recall** — cite a grid shot, a smoke shot, or code for
   every claim. Never describe an element from memory.

   **Evidence citations must be reproducible.** Grid/smoke output is
   gitignored and disposable — cite by *regeneration recipe* (the exact
   `atlas_grid` args: seeds, lenses, zoom, pitch, anchor) alongside the
   filename, never by filename alone. A future tier must be able to re-render
   any shot the inventory leans on.

   **Completeness floor — the inventory covers at least this census.** Seed it
   from `AtlasViewSceneSetup.BuildGraph()` (the scene builder enumerates every
   layer it constructs — that list is authoritative, read it rather than
   trusting this one): starfield · the field family (domain / nature / price /
   war / tension / tech / plague) · currency-zone tint · trade lens · lattice /
   region outlines · lanes + off-lane crawls · port marks/glows · fleets ·
   POIs · works · outpost marks + worked-dust · news/event surfaces ·
   TopBar · LensRail · LegendPanel (and the glyph vocabulary behind it) ·
   TimelineStrip · HexTooltip · the InspectorDock/DockKit panel family
   (polity · market incl. the outpost section · war · contracts · order book) ·
   SelectionModel states end-to-end · SystemStage (orbits, bodies, works,
   settlement rings). Anything the sweep finds beyond this floor gets an entry
   too — the floor is a miss-detector, not the ceiling.

   **Camera & navigation is a first-class inventory entry**, not ambient
   context: the zoom continuum and LOD bands (`LodBands`), focus/pitch/anchor
   behavior, and what each band promotes or hides. It has no obvious home in
   the spec's five groups — that's a known partition gap for task 4 to
   resolve, not an omission to repeat.
4. **Fix the group partition.** The spec's expected five groups (map fields &
   lenses · marks & billboards · chrome · panels & selection · SystemStage) are
   *adjustable on evidence*. If the inventory says the partition is wrong,
   propose a better one and say why — and place camera & navigation
   explicitly (its own group, or a named part of one).

## Gate

**One user checkpoint:** the user skims the inventory for completeness and nods
the group partition. That's it for Tier 0 — deep dives are Tier 1, one session
per group, each with its own eyeball.

## Wrap-up (after the partition nod)

- Commit the inventory + ledger; update `docs/HANDOFF.md` (Tier 0 done, the
  nodded partition, pointers to evidence recipes); push.
- Sync Trello if reachable (Tier 0 card → Merged/done; file anything new the
  sweep surfaced).
- **Do NOT write the Tier 1 kickoff.** Tier kickoffs for this pass are
  authored by the orchestrator after each gate (user decision, 2026-07-25) —
  the Tier 1 kickoffs depend on the partition the user just nodded, and the
  orchestrator holds the pass's gates per the spec. Hand back: inventory path,
  partition, and anything Tier 1 should know that the inventory doesn't carry.

## Hold the line on principle 1

**Nothing in the current atlas is sacred.** The inventory documents what IS
purely as evidence, *never as constraint*. Resist the pull to write it as a
justification of the status quo — a Tier-1 dive may propose replacing an element
outright, merging elements, or inventing surfaces that don't exist. Record what
exists and what it costs; don't defend it.

Equally: **standing decisions are inputs, not walls.** Cassette×Ice, the 2.5D
space/glows/billboards grammar, and "fields computed / glyphs authored /
placement always data" are current bests, not settled law. Tier 0 doesn't
challenge them, but note where the evidence strains them so Tier 1 can.

Where those decisions live, for the "strains" notes: the UI Language Lab
artifact (Cassette×Ice tokens; `ssg-ui-language-lab.html`), the atlas design
diagram (`docs/diagrams/unity-atlas-design.html`, whose draw code is the
de-facto visual spec), and the K1 ledger's camera/grammar amendments.

## Boundary

- No atlas code, no sim behavior, no `docs/design/` edits outside `docs/design/ui/`.
- Out of scope: the main-menu scene (keeps its Cassette×Ice mock) and the
  Inspector REPL's presentation (note its parity contract per group; don't
  redesign it).
- Don't start Tier 1 dives in this session — Tier 0 ends at the partition nod.
