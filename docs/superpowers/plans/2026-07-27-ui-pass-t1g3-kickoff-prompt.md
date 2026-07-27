# UI design pass — Tier 1, Group 3 kickoff: marks, billboards & the glyph vocabulary

You are opening **Tier 1, Group 3**: everything the atlas draws *at a place* —
port dots, fleets, POIs, works, war stations, plague, outposts, news pulses, the
shared billboard machinery, and the **16 authored icons**. This is the group
that decides what a symbol on this map means, and its icon decisions feed
**Tier 2's icon manifest**.

Groups 1 and 2 are **merged and are now spec**. Both name what you inherit, in
their §9/§10 "interfaces other groups depend on" sections. Read those first;
they are binding.

**Read first, in order:**
1. `docs/design/ui/camera-nav-lod.md` — **§2 (band × layer matrix)** and **§9**.
   Your rows: ports, glyph families, war/news, outposts.
2. `docs/design/ui/map-fields-lenses.md` — **§10 (interfaces)**, **§1.1 (the
   four channels — marks are the *non-exclusive* one)**, **§2.4 (worked dust
   leaves the mark budget; outposts stay marks)**, **§6 (the empty-state
   vocabulary you must extend)**, **§7.2 (Features and Emergence arrive as mark
   lenses)**, **§8 (the colour-authority bridge — the identity palette is now
   allocated, and marks tint from it)**.
3. `docs/design/ui/inventory.md` **§5 (marks & billboards, all four
   subsections)** — the evidence base, including the three family-wide debts.
   **§9.2** for the empty-state finding, **§9.3** for the standing decisions.
4. `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md` — pass spec:
   principles, the Tier 1 template, deliverables.
5. `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` — the pass ledger,
   **§"Group 2"** for method and recipes (four `eval_file` harnesses did that
   whole dive; reuse the pattern). Continue it as the Group 3 section.
6. `.claude/skills/driving-the-unity-editor/SKILL.md` — before your first Unity
   command. The **`eval`/`eval_file` method-body trap** is recorded there:
   scripts inject as a method body, so `using` does not parse — fully qualify.

## What this is — and isn't

A **design** session: a design doc + a mock artifact + ledger evidence, **zero
committed atlas code**. `eval_file` experiments in the warm editor are the
established vehicle and land nothing in `unity/`.

**Nothing in the current atlas is sacred.** The inventory is evidence, never
constraint. But Groups 1 and 2's *accepted* decisions are spec — a genuine
contradiction is an amendment to their doc in your branch, flagged to the user
(Group 2 did exactly this to Group 1's nature row; that is the pattern).

## What you inherit (binding)

**From Group 1:**
- **Mark count must fall as altitude rises.** Pixel floors mean mark *size*
  cannot fall, so culling or merging is the only lever. Ports filter **by tier**
  (tier 3+ at Realm, tier 2+ at Domains, all at Reach/Ground) — *which* tiers,
  and whether it is a fade or a merge, is yours.
- **War stations and news pulses resolve at Realm**; fleets, POIs, works and
  plague resolve at **Reach**. "Where is anything happening" is the Realm
  question, and today every family waits for `f = 0.63` — so the galaxy band
  shows none of it.
- **Ports hand over to the orbit stage's rings** rather than fading out. You own
  the mark side of that beat.

**From Group 2:**
- **Worked dust leaves the mark budget entirely** — it becomes a density
  modulation of the field's fill. One fewer additive layer; do not redesign it.
- **An outpost must differ from a port by FORM.** Size and lightness are both
  already spent (`PortLayer` runs `(2 + 1.4·tier)·2` px against the outpost's
  5.5 px). This is stated, not designed, there.
- **`Features` and `Emergence` move from the nature rasters to the marks
  channel** — two new mark lenses needing a glyph vocabulary and legends that
  name the *kinds* of thing they mark.
- **The marks channel is non-exclusive**: any number of mark lenses can be on
  at once. Declutter and cross-family distinctness are therefore constraints,
  not polish.
- **Every mark family needs a *silent* and a *blind* line** in the §6
  vocabulary — a lens that is live-and-empty must say so; one that cannot
  answer must say why.
- Marks tint from the **allocated identity palette** (16 CVD-checked hues, the
  32-slot cap deleted), and identity colours are on the token bridge.

## The design questions this dive must answer

**The mark budget — this group's spine.** There is **no declutter of any kind**
today: no collision avoidance, no importance culling, no top-N. Marks pile up
wherever the sim puts them, and at radius-21 density that is unbounded. Design
the budget: what competes for space, what wins, and how a family degrades when
it loses. Group 1 requires count to fall with altitude; the harder case is
count at *Reach*, where everything is on at once and legal.

**The glyph vocabulary — is authored shape paying for itself?** 16 icons from
game-icons.net encode real distinctions, and **at working zoom they read as
identical smudges** (`atlas-grid/seed-42-works.png`: the works glyphs are
orange blobs). The shape channel is spent and nothing survives to the eye.
Decide what deserves authored shape at all, what the readable floor is in
pixels, and what the manifest should contain — **Tier 2 builds from your
answer**. `AtlasGlyphs`' order is the atlas layout: append, never reorder.

**Size means four different things.** Fleet size encodes hulls, POI size
magnitude, works size purpose-plus-stall, war size posture — one channel, four
meanings, and **no legend entry states any scale**. Decide what size means
atlas-wide, and what the other three meanings move to.

**Outposts, by form.** Per Group 2, and it must survive the contrast chip and
the pixel floor.

**`Features` and `Emergence`** — inherited as mark lenses with no encoding at
all yet. Sparse point sets: origins, sterilization scars, overlay marks.

**War and news at galaxy altitude.** News pulses are spatial rings, never
pixel-capped (`_MaxPx` 4096), alpha-faded over a deliberate 40-year display
cutoff — and a single pulse reads as a heavy olive band across a domain, while
the stated intent ("the story is where rings cluster") has never been testable
on a one-pulse world. Both families now have to work at Realm, where they are
the only thing carrying drama.

**The shared machinery, re-examined.** The contrast chip (a dark backing disc
at 1.45×, `(9,11,17,195)`) exists because owner-tinted glyphs sat on
owner-tinted dots — Group 2's new fills and allocated hues change that ground,
so re-derive whether it is still the right device or a workaround you can
delete. Same for the queue biases (War 120, Plague 110) and the dual
world-size/pixel-floor sizing rule itself.

## Evidence

Generate what you cite; cite by **regeneration recipe, never filename** (grid
output is disposable and gitignored — `atlas-grid*/`).

- **Measure the density claim.** "Unbounded at radius-21" is an inference in the
  inventory; make it a number — marks per frame per family, at each band, on
  mature and degenerate worlds. Counts come from queries, looks from captures.
- **Measure readability in pixels, not in principle.** The question "at what
  size does this icon become identifiable" is answerable with a capture series
  and should not be argued.
- At least one **all-families-on** capture at Reach — the legal worst case that
  no existing shot shows.
- The Tier-0 lesson still stands: *measure, don't infer from a framed shot.*

**The editor is OPEN and clean on port 7800**, `SimHost.ArtifactPath` restored
to the golden. You own it — one editor, one session. Close it for batchmode.

## Deliverables & gate

- **`docs/design/ui/marks-glyphs.md`** — the accepted design, present tense.
  Centrepieces: the mark budget, the glyph vocabulary + readable floor, what
  size means, and the empty-state lines for every family.
- **Mock artifact** — glyphs at their *real* rendered pixel sizes (a sheet that
  argues the readability floor), and a declutter before/after at Reach density.
  New artifact, own favicon; URL in the ledger. **Two traps Group 2 hit:** an
  artifact that renders heavily on load reads as a **blank page** — render the
  visible canvas on `requestAnimationFrame`, the rest behind an
  `IntersectionObserver`; and this harness **cannot click or scroll inside an
  artifact's iframe**, so anything interactive cannot be self-verified before
  the gate — say so in the brief rather than implying it was checked.
- **Gate — one checkpoint.** Per CLAUDE.md's **"Talking to the user at
  checkpoints"** (binding — read it before writing the gate message): lead with
  the artifact, then a **decision brief ≤150 words**, pointers to evidence
  rather than evidence. The gate asks the user to settle exactly these, each
  with your recommendation: **(a)** the mark budget rule — what culls, merges or
  ranks; **(b)** the glyph vocabulary revision and its readable floor (this is
  the one Tier 2 inherits); **(c)** what size encodes atlas-wide; **(d)** the
  outpost's form. Everything else you decide and list in the ledger.

## Wrap-up (after the eyeball)

- Commit doc + ledger · update `docs/HANDOFF.md` · merge to main · **push**.
- Sync Trello if reachable (Tier 1 card https://trello.com/c/xEym8e27, in
  **In Progress** for the whole tier — note G3 done). Ledger records the write
  limits: 2048-char description, ARI card id required.
- **Do NOT write the Group 4 kickoff** — tier/group kickoffs are
  orchestrator-authored after each gate (user decision, 2026-07-25). Hand back:
  doc path, artifact URL, what Group 4 inherits, and **what Tier 2's icon
  manifest must contain**.
- Release the editor (leave clean; **no manual Ctrl+S of the atlas scene**).

## Boundary

- No committed atlas code, no sim behaviour, no `docs/design/` edits outside
  `docs/design/ui/` (except a flagged amendment to Group 1's or Group 2's doc).
  `unity/ProjectSettings` churn stays uncommitted.
- **Group 4 owns lanes, flow trails and crawls** — strokes, not marks. Where a
  mark sits on a stroke (freight, convoys), state the interface and leave the
  stroke design to them. Group 5 owns chrome widgets; what a chip *means* for
  your lenses is yours to state, its visual design is not.
- Slice CS (chrome capture tooling) blocks Groups 5–6 only — not your concern.
