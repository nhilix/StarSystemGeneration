# UI design pass — Icon Library deep dive (IL)

You are re-opening **the icon set** as a design problem in its own right.
Group 3 produced `docs/design/ui/icon-set.md` — 27 marks, a ten-sub-form
grammar, the "hex-cut" house style, three build tiers — as *part* of a wider
dive whose main business was the mark budget. **The user's judgment is that it
landed too fast**: the conclusions are plausible and internally consistent, but
several load-bearing choices were asserted rather than explored, and no
alternative was ever put next to them.

This session gives that library the full treatment: **a brainstorm, with the
user in the loop, at a narrow scope.**

**Why it is worth a session.** The icon set is the longest-lived artifact this
pass produces. It becomes commissioned art, an append-only enum contract, and
the thing every future mark inherits from. It is the most expensive item in the
pass to get wrong and the cheapest to revisit *now*, before Tier 2 turns it
into a production checklist and before anyone draws anything.

**Read first, in order:**
1. `docs/design/ui/icon-set.md` — in full. This is the subject.
2. `docs/design/ui/marks-glyphs.md` — **§3 (size carries nothing)**, **§2.2 (the
   collar)**, **§4.1 (per-band mark size)**. This is the **frame** the library
   lives inside, and it is **accepted — not open**.
3. `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` §"Group 3" — the
   measurements, the eight `eval_file` harnesses, and the ladder-sheet recipe.
   Continue the ledger in a new **§"Icon library"** section.
4. `docs/design/ui/inventory.md` §5.2 (the sixteen placeholders as shipped) and
   §9.3 (**"the 2.5D space/glows/billboards grammar"** — read this one twice;
   see the first assumption below).
5. `docs/superpowers/specs/2026-07-24-ui-design-pass-design.md` — governing
   principles and what **Tier 2** still owns (the manifest, i.e. the production
   checklist derived from your design — do not write it here).
6. **The two Group-3 artifacts** — this is the work you are re-opening, and it
   is rendered, not described:
   - the Group-3 mock (📍), whose Decision-B exhibit builds the ten Tier-A marks
     live from 60° polygon lists and runs them through the ladder —
     https://claude.ai/code/artifact/de81e106-e169-46a3-a8d7-0134cca5e60d
   - the `icon-set.md` catalogue (🔶), all twenty-seven marks constructed live,
     each at 36 px and 20 px, plus the grouped plate and the full ladder —
     https://claude.ai/code/artifact/981127f2-9d42-43ff-9edd-2eb73d039738
7. `.claude/skills/driving-the-unity-editor/SKILL.md` before your first Unity
   command. Note two recorded traps: `eval`/`eval_file` inject as a **method
   body** (no `using`), and **`eval_file` is capped at 30 s regardless of
   `--timeout`** — five of Group 3's eight harnesses reported failure *while
   completing normally*. Poll for the output file; never trust the envelope.

## Method — brainstorm first, and show, don't argue

**Use `superpowers:brainstorming`.** This session's first phase is divergence,
not authoring: candidate directions explored with the user, then one chosen and
developed. Do not open by writing a revised document.

Three working rules for the whole session:

- **Every style claim is rendered, never argued.** The Group-3 mock proved the
  technique: icons constructed live from polygon lists in the browser, at real
  pixel sizes. A prose case for a style is not evidence; a sheet at 10/16/20/32
  px is.
- **Alternatives come in threes.** Any place where the current document states a
  rule, put at least two genuine competitors beside it and let the ladder
  decide what it can.
- **The user is in the loop more than usual here.** This session deliberately
  has **more gates than a normal group dive** (below) — that is the point of
  it. Keep each one to a picture plus a ≤150-word brief, per CLAUDE.md's
  "Talking to the user at checkpoints".

## The assumptions to interrogate

These are the ones that look asserted. Treat the list as the agenda, not as
conclusions — and add any you find.

1. **The hexagonal envelope may fight the atlas's own grammar.** `icon-set.md`
   §2 rule 1 cuts every icon inside a flat-top hexagon "in the orientation the
   lattice draws." But the project's standing visual grammar is 2.5D
   space/glows/billboards and **explicitly not the PoC hex board** — and Group 2
   found the price field's hard hex blocks were "the one element that reads as
   the old hex board rather than as space," and re-encoded it for exactly that
   reason. A library that makes *every mark* hexagonal deserves that same
   scrutiny. Maybe it is right — a hex envelope is a strong, ownable house
   style. It has not been argued against once.
2. **The 20 px floor was measured on the wrong art.** Every ladder reading came
   from the *shipped placeholder sheet* — game-icons.net drawings, which are
   line-art-heavy, exactly the property §2 rule 3 says dies first. A floor
   measured on unsuitable art may be measuring the art, not the medium.
   Re-measure with silhouette-first candidates; if the real floor is 14–16 px,
   several downstream conclusions change.
3. **"One drawing per subject at every band"** (§1) is stated as a principle
   with its alternative unexamined — a two-tier scheme (silhouette at
   Realm/Domains, detailed drawing at Reach and below) is the obvious
   competitor, and §1's own table already says no drawing can be read above
   Reach. Decide it on evidence, either way.
4. **The ten sub-forms and the three composition rules** were invented in one
   pass. Is the grammar learnable, or is it a designer's mnemonic that no player
   ever induces? At minimum, test whether the compositions survive the ladder —
   Group 3 already found *precursor* and *plague* converge below 16 px, which is
   the grammar colliding with itself.
5. **White-and-tinted-at-runtime** (§2.2) forbids two-tone icons and any
   internal value structure. That is a real expressive cost, taken to satisfy
   the tint pipeline. Is there a two-value scheme the shader could carry?
6. **Set membership rests on "an icon must have a population"** (§4) measured
   across nine artifacts of *today's* sim. Several exclusions look like sim
   maturity, not design truth: **no world exceeds port tier 2**, `RuinedCapital`
   never occurs, `FleetEscort` never appears as a posture. The sim has an open
   gap list and is still growing. Decide deliberately whether the vocabulary is
   sized to what the sim emits today or to what it is designed to emit.
7. **Four surfaces, one design.** Icons appear on the map, in legend keys, in
   panel rows and in tooltips (§1), and the set is optimized for the map's
   worst case. Panel rows have room and different neighbours. Is one drawing
   right for all four?
8. **Sourcing and build tiers** (§5.3, §3.8) — "commission it" and an A/B/C
   order. Parametric construction from the grammar is the other real option and
   the Group-3 mock already did it. Which produces the set we can actually
   land, and does the build order still hold under whatever you decide above?

## Gates — four, deliberately

Each: **artifact first**, then a ≤150-word decision brief with your
recommendation and a pointer to the evidence.

1. **Scope nod.** Confirm the agenda and anything you would add or drop.
2. **Direction.** Three or four *rendered* style directions — the same six
   subjects drawn in each, at 10/16/20/32 px, with the ladder result per
   direction. Hex-cut is one of them, defended honestly. The user picks or
   mixes.
3. **Vocabulary.** The set membership and the grammar under the chosen
   direction: what earns a drawing, what shares a root, what is deliberately
   absent, and what the sim-maturity exclusions become.
4. **The set.** Every icon rendered, the ladder sheet, and the revised document
   for acceptance.

## Deliverables

- **`docs/design/ui/icon-set.md`, rewritten** — same living-design rules
  (present tense, no process). Every rule in it must be one of: derived from a
  stated measurement · chosen by the user at a gate with the alternatives
  recorded · explicitly marked **provisional** with what would settle it.
  Nothing asserted.
- **A decision record** at `docs/superpowers/specs/2026-07-27-icon-library-design.md`
  — what was considered and *rejected*, and why. That is the thing Group 3
  could not leave behind, and it is what stops this being re-litigated a third
  time.
- **Mock artifact(s)** — the direction bake-off and the final set, icons built
  live from geometry as Group 3's were. New artifact, own favicon, URL in the
  ledger. Two known traps: an artifact that renders heavily on load reads as a
  **blank page** (render the visible canvas on `requestAnimationFrame`, the rest
  behind an `IntersectionObserver`), and **this harness cannot click or scroll
  inside an artifact's iframe** — interactive controls cannot be self-verified,
  so say so rather than implying they were checked.
- **Amendments**, if the work demands them: `marks-glyphs.md` §6 (the icon
  summary) and `icon-set.md`'s interface section must stay true. A change to
  Group 3's *accepted* frame — the budget, the collar, size-carries-nothing —
  is an amendment flagged to the user, not a silent divergence.

## Boundary

- **The frame is not open.** Mark budget, collar, weight floors, per-band mark
  size, size-carries-nothing: accepted, and the library lives inside them.
- **No production art.** The deliverable is the design and the demonstration
  geometry, not a finished `AtlasGlyphs.png`.
- **Tier 2 still owns the manifest** — the per-entry production checklist. Do
  not write it; make it derivable.
- **Zero committed atlas code**, no sim behaviour, no `docs/design/` edits
  outside `docs/design/ui/`. `unity/ProjectSettings` churn stays uncommitted.
- Group 4 (lanes, flows & motion) is **next after this** and is not your
  concern, beyond keeping §4's offer of a gate-terminus icon intact.

## Wrap-up

Commit docs + ledger · update `docs/HANDOFF.md` · merge to main · **push** ·
sync Trello if reachable (Tier 1 card https://trello.com/c/xEym8e27; file this
as its own card if that reads better — the board's convention is one card per
session's work). Hand back: the doc paths, artifact URLs, what changed from
Group 3's version and **why**, and anything Group 4 or Tier 2 now inherits.
Release the editor (leave clean; no manual Ctrl+S of the atlas scene).
