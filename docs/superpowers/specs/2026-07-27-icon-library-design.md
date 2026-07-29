# The icon library — decision record

**2026-07-27/28.** Group 3 produced `docs/design/ui/icon-set.md` as part of a
wider dive whose main business was the mark budget. Its conclusions were
plausible and internally consistent, but several load-bearing choices were
asserted rather than explored and no alternative was ever put beside them. This
session re-opened the library as a design problem in its own right.

**This document records what was considered and *rejected*.** The accepted
design lives in `docs/design/ui/icon-set.md` §2; the working trail is
`docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` §"Icon library".

---

## 1. The governing decision: the set is a notation

**Rejected: "where a real-world depiction exists, draw the thing."**

This was proposed mid-session as a design principle — icons that depict read
instantly and need no learning, so the compositional grammar should be a
*fallback* for concepts with no picture.

It is wrong here, and the reason is specific to this project: **nothing in this
world exists.** No player has ever seen a starport, a precursor site, a gate
under construction or a sterilization scar. Recognition-by-depiction is not
available at any price, so the grammar is not a fallback — it is the entire
mechanism.

> **The set is a notation, not a collection of pictures. Every mark is learned,
> and the set's first duty is to be learnable.**

Everything in §2 follows from that. It is also why the *parts* get commissioned
before the *marks*: in a notation, consistency comes from the parts being right.

---

## 2. Rejected: a mechanical separation score as the gate

**This is the most important rejection in the document, because it was tried,
it produced numbers, and the numbers were confidently wrong.**

An instrument was built that rasterised every pair of marks at a target size and
scored them on **alpha intersection-over-union** and **differing ink**. It was
then used to drive redesign: five icons were redrawn to improve their scores.

**Three of the five came out unreadable** — sparse scatter that depicted nothing
and scored better. Goodhart's law, in full: the proxy was optimised until it
destroyed the thing it proxied for.

**Why no pixel metric can do this job.** Overlap has no relation to how a person
tells shapes apart. A filled disc and a disc struck through by a bar overlap
heavily and are instantly distinguishable, because the bar is a *silhouette
event*. Two dissimilar spiky marks can overlap barely and still read as the same
small spiky thing. People discriminate on **silhouette contour, dominant axis,
topology and metaphor recognition** — a pixel metric sees none of these.

The worst consequence was doctrinal rather than visual: `facility`/`ruin` scored
0.877 and was "fixed" by making the two marks unrelated. **That destroyed the
right idea.** A standing building and a broken building *should* share most of
their mass; the shared mass is what makes the pair read as cause and consequence.
0.877 was never the disease.

**What survives.** Measurement is kept for what is genuinely mechanical —
populations, on-screen distances, how many pixels a surface actually gives a mark
(§4 below is entirely such a measurement). It may also nominate **suspects to
look at**: the mark whose optical weight is far from its neighbours, or the group
of four that made the same topology-and-axis choice. It nominates; the eye
convicts. The gate in `icon-set.md` §2.3 is a squint test judged by a person, and
the reason a score cannot replace it is written into the document so it is not
retried.

---

## 3. The hexagonal envelope: kept, against the measurement

Four directions were rendered and compared — hex-cut **as built**, hex-cut **as
specified**, free silhouette, and glow-native — with the incumbent entered as its
own existing catalogue geometry rather than a fresh sketch, so it could not lose
to a hasty redraw.

**A finding that reframed the comparison: the envelope had never been
implemented.** There is no `clip()` anywhere in the catalogue geometry; the
flat-top hexagon appears only as a faint stroked guide drawn *behind* each icon
on the display plate. Rule 1 — named as one of the three rules "nothing off the
shelf will satisfy" — had never been exercised by the art that demonstrated the
style.

**Rejected: dropping the envelope.** Implementing it measured *worse* on both
instruments, and the recommendation at the gate was to drop it. The user chose to
keep it, on **cohesion and interest** — the starport and fleet-posted marks gain
structure from it. That judgment is correct and the recommendation was not: a
pairwise distinguishability metric is structurally blind to whether a set hangs
together, which is a primary criterion for a *set*.

**Rejected: the envelope as a cookie cutter.** The measurement was pointing at
something real even through the wrong instrument — if every icon is *cut to* the
hexagon, every icon has the same outline, and outline is the primary channel for
telling shapes apart. The resolution is that **the hexagon is a containing field,
not a cutter**: icons compose within it, inherit its angles and optical size, may
touch its edges, and must not fill it.

**Rejected as a legibility claim: the 60° edge family.** A free-silhouette set of
the same six subjects measured a dead heat against hex-cut. The discipline costs
nothing and buys nothing in separation, so it is kept as **house identity** and
is no longer defensible as a readability argument. Stating this matters: a rule
defended on the wrong grounds is a rule nobody can correctly apply.

---

## 4. The facility ladder, and the member rung that was rejected

`InfraTypeId` carries fourteen buildable facility types in four `InfraFamily`
groups; the set drew them all as one block, so the system view is a field of
identical marks. The first design put the fix in two rungs: **the map draws the
family root, the system view draws root plus member.**

**The member rung was rejected on a measurement.** Against the shipped
`SystemStage` (`world 0.038`, `pxFloor 7`, `_MaxPx 36`) and
`CameraRig._minDistance = 2.5`, a facility mark is **8.1 px** where the orbit
stage fades in and **16.3 px** at the closest the camera can go on a 1000-tall
viewport (11.7 → 23.5 at 1440). `_MaxPx` is never reached — the camera floor
binds first. **A facility mark never clears the 20 px floor on a 1080p-class
window.** Four roots fit a single-closed-silhouette band; fourteen member
variants do not.

So membership is **named in the tooltip and the panel, not drawn**. The cost of
changing that is recorded rather than taken: raising the stage's facility world
size 0.038 → 0.047 buys 20 px at closest zoom on a 1000-tall viewport. It is a
one-line atlas change, it was not made here, and it should be argued on whether
a player needs *mine versus skimmer* at a glance.

**Rejected: crowding as an explanation.** The expectation was that facility marks
overlap — the layout's radial step is 0.016 world, 6.9 px against a 16.3 px mark,
a 58% overlap. But the layout also rotates 0.85 rad per mark and the angular term
dominates: real centre-to-centre spacing is **23.3 px against a 16.3 px mark**.
They never touch. **The sea of identical icons is one shared drawing and nothing
else** — which is why replacing that one drawing with four is the whole fix.

---

## 5. Smaller rejections, recorded so they are not re-proposed

| Rejected | Why |
|---|---|
| **"Even optical weight" deleted as a rule** | It was violated by 19 of 27 icons, which was read as evidence the rule was wrong. It is evidence the *set* is incoherent. Even weight is among the strongest family signals there is, and it is reinstated — re-scoped so it binds where marks appear together in a row, and judged by eye rather than by a coverage number. |
| **A two-tier scheme** (silhouette above Reach, detailed drawing at Reach and below) | Never needed. Above Reach the map gives a mark 2.1–6.4 px, where it is a locator whatever is drawn; one drawing per subject already serves every band. |
| **Two drawings per subject for the four surfaces** | The two *jobs* differ — identification on the map, comprehension in legend/tooltip/panel — but they are served by optimising one drawing for the labelled surfaces and auditing it on the map, not by drawing it twice. |
| **`crossed axes` as a primitive** | A ninth primitive earning its keep in two marks. Both now compose from the chevron under **massed** and **broken**, which is also a better reading: a massing of vessels, and vessels broken. |
| **`immune` as "the bites healing closed"** | A half-healed bite is exactly the timid internal annotation rule 9 forbids. Enclosure — `disc + hex ring` — is what immunity means and reads as a different figure. |
| **`blockade` on the hex ring** | The ring carried four marks and generated three of the six worst pairs. Blockade's subject is the *port*, not the hulls, so it becomes `disc struck`. |
| **Resolving the last three collisions on paper** | The four facility roots, `posted`/`reserve` (a mirror pair), and `memorial`/`outpost` are carried into the squint test with their fallbacks written. Deciding them without an eye is what produced §2's failure the first time. |

---

## 6. What this session did not do

- **No art.** The fourteen parts — eight primitives, six operators — are
  specified and not drawn. That is the next session's whole job, and it is the
  first delivery: in a notation, a set whose parts are right is consistent by
  construction.
- **No atlas code**, per the kickoff boundary. The one change the measurement
  argues for (facility world size) is recorded, not made.
- **Gate 4 was not reached.** The kickoff's fourth gate wanted every mark
  rendered against a ladder sheet. The session stopped at the design deliberately,
  after two attempts to drive drawing from a score produced worse icons.
