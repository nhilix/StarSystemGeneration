# The atlas UI design pass — tiered element-by-element design review

**Decision record, 2026-07-24.** A methodical, three-tier design pass over
every major UI element in the Unity atlas: inventory what exists, deep-dive
each element group against a fixed template, then synthesize the cross-cutting
visual language and emit ranked implementation kickoffs. This is a **design
effort** — its outputs are living design docs (`docs/design/ui/`) and mock
artifacts, not atlas code. Implementation happens in later slices that this
pass's Tier 2 defines.

## Governing principles

1. **Nothing in the current atlas is sacred.** We are in early exploratory
   development. The inventory documents what IS purely as evidence, never as
   constraint — a deep dive may propose replacing an element outright, merging
   elements, or inventing surfaces that don't exist, and no idea is discounted
   because of how something currently works. (Extends the standing
   no-settled-parts stance: IMPL status is build status, not design
   validation.)
2. **Standing decisions are inputs, not walls.** Cassette×Ice (UI Language
   Lab, 2026-07-12), the 2.5D space/glows/billboards grammar, and "fields are
   computed, glyphs are authored sprites, placement is always data" are the
   current bests — the pass builds on them by default but may challenge any of
   them explicitly, flagged as such, with the mock to back it up.
3. **Evidence over recall.** Every critique cites either the multi-seed
   eyeball grid (Slice UP's deliverable), a committed smoke shot, or code —
   not memory of how the atlas looked.
4. **Design is the spec.** Accepted deep-dive outputs land in
   `docs/design/ui/` (present tense, no process); the process trail stays in
   plans/ledgers.

## Scope

- **In:** the Unity atlas — map fields/lenses, marks/billboards, chrome,
  panels/selection/tooltips, SystemStage. Visual design, information design,
  interaction design, iconography, motion, theme/token conformance.
- **Out:** the main-menu scene (keeps its Cassette×Ice mock), the Inspector
  REPL (dev tool; its parity contract with panels is noted per group but its
  presentation is not redesigned), any sim behavior, any atlas implementation.

## Sequencing

Runs **after Slice UP merges** (user decision): UP's multi-seed eyeball grid
(seeds × lenses contact sheets) is this pass's evidence base — elements are
judged on how they read across varied galaxies, including sparse/degenerate
ones, not on seed-42 alone. Atlas editing stays serial throughout.

## Tier 0 — Inventory (one session, mostly Explore subagents)

Sweep `src/Core/Atlas/`, `unity/Assets/Atlas/`, the K1–K5 + AC ledgers, and
the UP grid. Output: **`docs/design/ui/inventory.md`** — one entry per
element: what it renders, the Core query/data source feeding it, current
interactions and states, current visual encoding, LOD behavior, and known
debts (from ledgers' deferred lists). The inventory also fixes the group
partition for Tier 1 — expected partition, adjustable on evidence:

1. **Map fields & lenses** — starfield, domain/nature/price/war/tension/
   tech/plague fields, trade lens, currency-zone tint, off-lane crawls,
   lattice/region outlines.
2. **Marks & billboards** — port dots/glows, fleets, POIs, outposts,
   worked-dust, all glyph sprites and their tinting.
3. **Chrome** — top bar, lens rail, legend panel, timeline strip, news
   surfaces.
4. **Panels & selection** — the DockKit family (polity, market incl. the
   outpost section, war, contracts, order book), hex tooltip, SelectionModel
   states end-to-end.
5. **SystemStage** — the K5 system view: orbits, bodies, works, settlement
   rings, its own LOD.

Gate: user skims the inventory (completeness check, group partition nod).

## Tier 1 — Deep dive per group (one session per group or split at natural
boundaries; every group ends in a user eyeball)

Each group is analyzed against the **fixed template** — the methodical core
of the pass:

- **What it presents.** The sim truth behind the element: which state, at
  what fidelity, and what the player is supposed to *learn or decide* from
  it. Mismatches (truth shown but unreadable; truth readable but never
  needed) are findings.
- **How it reads.** Visual encoding audit: channel use (color/size/glow/
  position/motion), hierarchy, competition with neighbors, LOD behavior,
  multi-seed readability (UP grid), colorblind-safety of lens palettes, text
  legibility across resolutions.
- **How it's operated.** Every interaction: click, hover, drag, keys, scrub;
  the full state chart (default/hover/selected/disabled) and — first-class —
  **empty and degenerate states** (dead port, zero-trade galaxy, one-polity
  map), which is where encodings collapse and which ties to the parked
  flat/sparse-economy work.
- **Where custom icons/elements pay.** Concrete proposals: which glyphs
  deserve authored icons (sourced game-icons/Kenney vs custom-drawn), which
  generic widgets deserve bespoke elements, each justified by an interest or
  clarity gain — mocked, not just named.
- **Motion.** What should pulse, fade, or transition here, in one shared
  grammar (event pulses, lens crossfades, scrub feedback) rather than
  per-element habits.
- **Consistency & debts.** Drift from Cassette×Ice tokens and from sibling
  elements; ledger-deferred polish items absorbed or explicitly dropped.
- **Open challenges.** Anything principle-2-level: proposals that overturn a
  standing decision, argued with a mock (includes the parked
  structure-follows-Eye seam: instrument = god eye, cassette = controller
  eye — decide or re-park it explicitly during the Chrome group).

Outputs per group: a **`docs/design/ui/<group>.md`** design doc (the accepted
design, present tense) and a **mock artifact** in the UI Language Lab's
token-block style (extending the Lab where possible so mocks stay comparable).
Gate per group: user eyeballs the mock + doc, accepts or redirects.

## Tier 2 — Synthesis (one session)

- **Icon manifest** — every accepted glyph/icon across groups: name, meaning,
  source (library vs custom), tint rules, LOD visibility; becomes the
  production checklist for implementation slices.
- **Token & theme conformance doc** — the Cassette×Ice token set as actually
  needed by the accepted designs; deltas from the current .tss/USS.
- **Interaction grammar doc** — the unified selection/hover/scrub/motion
  rules every element obeys.
- **Ranked implementation kickoffs** — the accepted designs decomposed into
  ordered implementation slices (each a normal slice-session kickoff prompt),
  ranked by clarity-gain per effort with the user.

Gate: user reviews the synthesis + ranking; the top kickoff enters the normal
queue.

## Execution model

- One **Opus design-pass worker session per tier**, spawned per the psmux
  worker protocol; Tier 1 additionally splits into fresh sessions at group
  gates whenever context runs long (AC's phase-boundary precedent) — the
  session/tier mapping is a resumability tactic, never a design boundary.
  Handoff via a committed pass ledger
  (`docs/superpowers/plans/2026-07-XX-ui-pass-ledger.md`) + HANDOFF.
- Within sessions: Explore/research subagents (Sonnet) for sweeps; the deep
  dives' design writing is the worker's own (Opus) work; mock artifacts follow
  the USS-translation + Lab token conventions.
- The orchestrator spawns tiers and holds the gates; it does not write the
  dives.
- No atlas code changes anywhere in the pass. `dotnet test` untouched; no
  Unity editor dependency except reading the UP grid (so the pass never
  contends for the editor).

## Deliverables summary

`docs/design/ui/inventory.md` · five `docs/design/ui/<group>.md` docs · per-
group mock artifacts · icon manifest · token conformance doc · interaction
grammar doc · ranked implementation kickoff prompts · pass ledger.
