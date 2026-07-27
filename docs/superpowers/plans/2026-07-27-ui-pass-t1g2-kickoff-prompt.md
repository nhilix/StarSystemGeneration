# UI design pass — Tier 1, Group 2 kickoff: map fields & lenses

You are opening **Tier 1, Group 2** of the atlas UI design pass: the **rasters
and the lens system** — starfield, the domain field and its five accents,
worked dust and outposts, the nature fields, the price field, the lattice, and
the question of what a *lens* is. This is the layer that occupies the most
pixels in the atlas and carries almost all of its colour.

Group 1 (camera, navigation & the LOD spine) is **merged and is now spec**.
Your layers ride its curves; §9 of its design names exactly what you inherit.

**Read first, in order:**
1. `docs/design/ui/camera-nav-lod.md` — **§2 (the band × layer matrix)** and
   **§9 (interfaces other groups depend on)** are binding on you; §3 (curves),
   §4 (the crossfade) and §8 (empty/degenerate states) for context.
2. `docs/design/ui/inventory.md` **§3 (map fields, all six subsections)**,
   **§9.1–9.3 (colour discipline · the two empty-state regimes · where the
   evidence strains a standing decision)**, **§6.1 (LensRail)** for how lenses
   are currently *selected* — the widget is Group 5's, the semantics are yours.
3. `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md` — the pass
   spec: principles, the Tier 1 template, deliverables.
4. `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` — the pass ledger,
   **§"Group 1"** for method and recipes. Continue it (Group 2 section).
5. `.claude/skills/driving-the-unity-editor/SKILL.md` — before your first Unity
   command. Note the **`eval`/`eval_file` method-body trap** recorded there
   (commit `17034be`): scripts inject as a method body, so `using` directives
   do not parse — fully qualify every type.

## What this is — and isn't

A **design** session: a design doc + a mock artifact + ledger evidence,
**zero committed atlas code**. Look-and-feel experiments through `unity command
eval_file` in the warm editor are encouraged and land nothing in `unity/`.

**Nothing in the current atlas is sacred.** The inventory records what exists
and what it costs; it is evidence, never constraint.

## What Group 1 already decided (binding — do not re-open)

- **Four bands**: Realm · Domains · Reach · Ground. Hex is deleted.
- **Territory generalizes at Realm** — the domain field's smoothing radius
  becomes a function of altitude, so a polity's scattered holdings read as one
  territory shape instead of 218 confetti blobs. G1 owns that *requirement*;
  **you own the mechanism**.
- **Price and nature rasters are off at Realm and Domains**, on at Reach and
  Ground, and **lead the crossfade out**.
- **The starfield takes an altitude attenuation** (attenuated at Realm and
  Domains, full at Reach and below) and **never attenuates during the
  crossfade** — it is the one element continuous across the whole descent,
  which is what makes it read as descent rather than a scene change.
- **The lattice fades last**, resolving at Reach.

If evidence genuinely contradicts one of these, that is an amendment to
`camera-nav-lod.md` flagged to the user — not a silent divergence.

## The design questions this dive must answer

**The accent system (inventory §3.2).** Five accents — owner, war, tension,
tech, currency — all share **one fill channel at intensity 0.13**, and it is
arithmetically fatal: tension's `(95,105,130)` and tech's `(120,95,70)` both
land within a couple of 8-bit steps of each other over near-black, so the two
lenses render as *the same grey image* (currency is that image in red). Decide
whether the fix is the intensity, the ramps, or the premise that five different
questions can share one channel.

**The 32-polity cliff.** Polity 33+ folds into the last slot and inherits its
colour (`DomainFieldLayer.cs:155-158`) — the map lies about ownership with no
warning. Design the behaviour past the cap.

**Owner hue.** Golden-ratio hue at fixed S/V is collision-*resistant* but not
perceptually separated: neighbouring greens are hard to split and red/green
adjacency is everywhere, with no colourblind safety. Palette allocation is a
design problem, not a hash.

**The price field — the loudest element in the atlas.** Hard-edged saturated
blocks over ~40% of the frame at full opacity, obliterating everything beneath;
a **categorical-reading palette for a scalar question**; and **two geometry
idioms for one truth** (hard hex blocks here, soft rounded blobs in the domain
field, visibly disagreeing wherever both are on). It is also the one element
that still reads as the old hex board rather than as space.

**The compositing budget — the spine of this group.** Fields stack: starfield
under domain fill under price under nature under lattice. Nothing states who
wins, at what opacity, in which legal combinations. Design the stacking rule
and the opacity budget such that *any* combination a player can select stays
readable — and say which combinations are illegal, if any.

**What a lens IS.** G1's governing idea is "altitude asks a question." The
parallel question here: does a lens ask one? Today accents are one-at-a-time,
nature is one-at-a-time, price carries a good selector, and nothing explains
the exclusivity. Define the lens model.

**The map's missing empty states (§9.2 — the inventory's sharpest finding).**
Panels have voiced empty states throughout; **the map has none**: no lens ever
says "no wars", "no trade", "no plague". A player cannot tell an empty sim from
a broken lens. Design the **vocabulary** here, once, for the whole map — Groups
3 and 4 will apply it to glyphs and lanes. This is the highest-value thing you
can leave behind for the rest of Tier 1.

**The colour-authority bridge (§9.3).** Chrome honours the token system (120
`var(--…)` against one literal); **the map does not participate at all** —
every map colour is a C# constant in a layer or a Core lens. Decide whether the
map joins, and what that costs: `AtlasPalette` is deliberately engine-free so
every palette decision is xUnit-coverable, and CPU sRGB→linear conversion
before upload is load-bearing (the recorded failure: *"#262C3F rings came out
lavender"*). A bridge that breaks either property is not a bridge.

**Smaller, still real:** the starfield's absolute brightness burying content on
sparse worlds; all nature chips sharing one rail swatch and a generic
"low/high" legend, so the rail cannot say which nature layer is which; the
lattice reading as texture noise across the frame rather than as a locating
grid (and costing 881,790 verts in one 30.1 ms frame — G1's measurement).

**Seams to respect, not design:** worked dust and outposts (§3.3) are *fields*
by placement but ride Group 3's billboard machinery — own their encoding, leave
the mark machinery alone. The rail's chips and legend widgets are Group 5's;
what a chip *means* is yours.

## Evidence

Generate what you cite; cite by **regeneration recipe, never filename** (grid
output is disposable and gitignored). `atlas_grid` serves this group well —
unlike Group 1, your questions are mostly answerable at a fixed altitude.

- Lens × seed sweeps across the accents, including the tension/tech/currency
  triple that collapses, and at least one **degenerate** world.
- At least one **composite** capture per proposed stacking rule — the point is
  what happens when layers are on *together*, which no existing shot shows.
- **Measure, don't infer from a framed shot** (the standing Tier-0 lesson: the
  "near-empty" golden had 72 ports; the sparseness was framing). Counts come
  from queries; looks come from captures.

**The editor is currently OPEN and clean on port 7800** (play mode exited,
autotick off, `SimHost.ArtifactPath` restored to the golden). You own it — one
editor, one session. Close it if you need batchmode.

## Deliverables & gate

- **`docs/design/ui/map-fields-lenses.md`** — the accepted design, present
  tense. Centrepieces: the accent/palette system, the compositing budget, the
  lens model, and the map's empty-state vocabulary.
- **Mock artifact** — this group is colour- and composition-heavy, so a mock
  earns its keep: ramps and palettes side by side (shipped vs proposed), and
  the composite stack toggled layer by layer. New artifact, own favicon; note
  the URL in the ledger.
- **Gate — one checkpoint.** Per CLAUDE.md's **"Talking to the user at
  checkpoints"** (binding, read it before you write the gate message): lead
  with the artifact, then a **decision brief ≤150 words** — pointers to
  evidence, not the evidence. The gate asks the user to settle exactly these,
  each with your recommendation: **(a)** the accent encoding — one channel or
  several; **(b)** the price field's re-encoding; **(c)** the behaviour past 32
  polities; **(d)** whether the map joins the token system. Everything else you
  decide yourself and list in the ledger.

## Wrap-up (after the eyeball)

- Commit doc + ledger · update `docs/HANDOFF.md` · merge to main · **push**.
- Sync Trello if reachable (Tier 1 card https://trello.com/c/xEym8e27 — note G2
  done; the card is in **In Progress** for the whole tier).
- **Do NOT write the Group 3 kickoff** — tier/group kickoffs are
  orchestrator-authored after each gate (user decision, 2026-07-25). Hand back:
  doc path, artifact URL, and what Groups 3 and 4 inherit from your decisions.
- Release the editor (leave clean; **no manual Ctrl+S of the atlas scene** after
  a capture — the in-memory scene diverges by design).

## Boundary

- No committed atlas code, no sim behaviour, no `docs/design/` edits outside
  `docs/design/ui/`. `unity/ProjectSettings` churn stays uncommitted.
- Don't design Group 3's glyph vocabulary, Group 4's lane strokes, or Group 5's
  chrome widgets. Where your decisions touch them, **state the interface** and
  leave the design to their dives.
- Slice CS (chrome capture tooling) blocks Groups 5–6 only — not your concern.
