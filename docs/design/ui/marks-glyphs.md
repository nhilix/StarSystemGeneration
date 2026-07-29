# Marks, billboards & the glyph vocabulary

**Everything the atlas draws *at a place*.** This document is the spec for the
mark budget — what competes for a point on the map and what wins — for the
badge layout those marks wear, for what size means, and for the shared billboard
machinery underneath all of it. Its sibling **`docs/design/ui/icon-set.md`**
owns the authored vocabulary itself: what must be depicted, what each mark means
in the world, and the rules every drawing obeys.

It rides `docs/design/ui/camera-nav-lod.md` (bands and curves, §9 names what
this group inherits) and `docs/design/ui/map-fields-lenses.md` (channels, the
empty-state vocabulary, the identity palette; §10 likewise). Evidence for every
number is in the Group-3 section of
`docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` — regeneration recipes and
measured tables. `docs/design/ui/inventory.md` §5 records the prior behaviour.

---

## 1. The governing idea: a place draws once

Group 1's spine is *altitude asks a question*. Group 2's is *a lens answers one
question in one channel*. The marks channel needs a third, because it is the one
channel that is **non-exclusive** — every mark lens can be on at once, legally,
and nothing today mediates between them.

> **A mark is a place, or it is something happening at a place. The map draws
> each place once, and what is happening there rides that one mark.**

The shipped atlas has no such rule, and the measurement is stark. On a mature
radius-21 world it draws **957–1347 marks at 373–608 distinct hexes** — every
one of them at the exact hex centre, with no offset, no dodge and no cull.
**42–65% of occupied hexes carry two or more marks stacked on the same point**,
and the worst hex on each seed carries **10 to 13 marks drawn from up to seven
families**. Measured over the frame, **69–89% of on-screen marks have their
centre inside another mark's disc**, at *every* band, on *every* mature seed.

That is not a density problem waiting for a collision solver. It is a grammar
problem: the atlas treats a port, the plague burning at that port, the fleet
posted there and the shipyard under construction there as four peer marks at one
point. They are not peers. **One is the subject; the others are predicates of
it.**

### 1.1 Three kinds of mark

| Kind | What it is | Geometry | Members |
|---|---|---|---|
| **Keystone** | a place with a name and an id | one point per hex | port, outpost, POI, feature, origin, scar, news origin |
| **State** | something true *of* a keystone | a badge on the keystone | war station, plague, works site, fleet posted |
| **Transient** | something *between* places | a point on a stroke — **Group 4** | freight, convoys |

Two consequences fall straight out, and both are changes.

- **Transients leave the mark channel.** A freight crate's position is a
  fraction along an origin→destination line; it has never been at a hex except
  by rounding. It belongs on the stroke that carries it. Interface stated in
  §11.
- **Worked dust has already left** (Group 2 §2.4) — it is a density modulation
  of the field's fill. Nothing here re-litigates that.

---

## 2. The mark budget

### 2.1 One keystone per hex

Every admitted mark resolves to a hex. At each hex the marks sort by **kind
rank** — Port → Outpost → POI → Feature/Origin/Scar → News origin → Works site →
Fleet → War station → Plague — and the first one **is** the keystone. Everything
else at that hex becomes a **badge** on it.

The keystone is a **single billboard at a single point**: one owner tint, one
form, one contrast chip. Everything else at that hex becomes a badge, laid out
by §2.2. Hover names them; selection opens them (Group 6).

This is the whole declutter. There is no force-directed dodge and no top-N by
screen density, because both would move a mark away from the thing it describes.
The marks were co-located by the *sim*, so the merge is done in the sim's own
coordinate — the hex — and it is exact, stable and order-independent.

### 2.2 The collar: badges have addresses, not positions

A badge that lands wherever there is room cannot be read at a glance, because it
has to be *found* before it can be read — and at four pixels there is nothing to
find it by. The badge layout is therefore **fixed**, and it is the map's own
geometry.

> **A keystone wears a collar of six slots at the vertices of a hex. Each state
> family owns one slot, permanently. A slot is lit when that family is present
> at this place and draws nothing when it is not.**

| Slot | Family | Key colour |
|---|---|---|
| 12 o'clock | **war** — a station, a blockade, a siege | `WarLens.StationBurn` |
| 2 o'clock | **plague** — infected or scarred | `PlagueLens` status colour |
| 4 o'clock | **works** — something is being built here | `WorksLens.SiteAmber` |
| 6 o'clock | **news** — word came from here | `NewsLens.Parchment` |
| 8 o'clock | **fleet** — hulls are posted here | owner tint, lifted |
| 10 o'clock | **POI** — the place remembers something | `PoiLens` type colour |

Six is not a budget that might be exceeded; it is the count of state families
that exist. A seventh family would need a seventh vertex, and the collar would
stop being a hex — which is exactly the constraint that keeps the vocabulary
from sprawling.

Five properties make this the layout rather than *a* layout:

1. **Position identifies; colour confirms.** You learn "top vertex means
   fighting" the way you learn a dashboard, and you learn it once. Colour is the
   redundant channel, not the only one — which is the whole of the colour-blind
   answer here, since at 4 px no *form* can carry redundancy (§5.2).
2. **Count is a shape, not a tally.** Three lit vertices out of six is a
   silhouette the eye resolves before it reads anything. There is no "+n" pip and
   nothing to count sequentially.
3. **Nothing can overlap, by construction.** Slots are fixed and non-adjacent at
   60°; a keystone with all six lit is still a clean figure.
4. **It is this map's geometry.** Marks sit at hex centres on a hex lattice; a
   hexagonal collar is the shape the map is already made of, not a widget
   borrowed from somewhere else.
5. **It scales with the band and does not change shape.** The collar is always
   the same six vertices at the same six angles; only its radius follows the
   band's mark size (§4.1). The position a player learns at Realm is the position
   they read at Ground.

**Sizing.** The collar's radius is **0.8 × the mark**, its pips **0.4 ×**, so a
badged place occupies about **1.7 × the mark** — 17 px at Realm's 10 px mark,
34 px at Reach's 20 px. At Reach that is a little larger than one of today's
glyphs plus its contrast chip (19–23 px), and it carries six states instead of
one. The contrast chip (§9.1) covers the collar, so the pips sit on the same dark
ground the keystone does.

**Pips are colour and position only, at every band.** A pip is 0.4 × the mark —
4 px at Realm, 8 px at Reach — so it is always below the 20 px icon floor and
below the 8 px form floor (§5.2). That is not a shortfall: family is exactly what
colour and a fixed address are good at, and *kind within family* is the
keystone's job.

**Degree is not encoded.** Two fleets at one hex light the same slot as one. The
collar answers *what is here*; the pointer answers *how much*. That is the same
division Group 2 drew for the field — hue narrows, the pointer resolves — and it
is what keeps a four-pixel mark honest.

### 2.3 Weight admits: how count falls with altitude

Group 1 requires that mark **count** fall as altitude rises, since the pixel
floor means mark *size* cannot. The lever is admission, and every family already
carries the number to do it with.

> **Every family exposes a weight. Each band sets a floor. A mark is admitted
> when its weight clears the floor — and a keystone is admitted whenever any of
> its states is.**

| Family | Weight | Realm | Domains | Reach | Ground |
|---|---|---|---|---|---|
| Ports | tier | **top tier present on this world** | ≥ top − 1 | all | all |
| Outposts | — | — | all | all | all |
| POIs | magnitude | ≥ p99 | ≥ p90 | ≥ p50 | all |
| News origins | magnitude | ≥ p50 | all | all | all |
| War stations | — | all | all | all | all |
| Works sites | — | — | — | all | all |
| Fleets | hulls | — | — | all | all |
| Plague | — | — | — | all | all |
| Features / Emergence | — | all | all | fading | — |

Three things about that table are load-bearing.

**The port filter is *relative*, not absolute.** Group 1 specified "tier 3+ at
Realm, tier 2+ at Domains". **No seed we have contains a port above tier 2** —
port tiers across the nine artifacts are t1 = 97–128 and t2 = 75–98, and nothing
higher anywhere. The shipped rule would therefore draw **zero ports at Realm on
every world in existence**. Ranking against the world's own top tier halves the
count (218 → 98 on seed-42) and can never empty the map. This is the amendment
in §12.

**The quantile floors are the world's own.** A degenerate world's p90 is not a
mature world's p90, so the same rule that thins 322 POIs to 3 at Realm leaves a
19-POI world showing its top two. No constant survives contact with `epoch 42 2
21`; a quantile does.

**Eventfulness overrides rank.** A tier-1 port under blockade draws at Realm,
because its war badge is admitted there. That single clause is what makes the
Realm band answer its own question: **the galaxy view shows the important places
and the eventful ones**, and nothing else.

### 2.4 What it costs, measured

The rule above was run against all nine artifacts and compared with the shipped
draw, same framing, same viewport, same projection.

| Band | Shipped on-screen | Shipped occluded | **Proposed on-screen** | **Proposed occluded** |
|---|---|---|---|---|
| Realm | 957–1347 | 86–89% | **92–120** | **1.1–5.0%** |
| Domains | 854–1111 | 70–83% | **174–228** | **3.8–9.8%** |
| Reach | 291–368 | 56–79% | **91–187** | **0.0–1.0%** |
| Ground | 237–381 | 73–83% | **84–153** | **10.2–24.8%** |

On seed-42 the admitted set falls **1014 → 371 → 196** from Reach to Realm, so
count falls with altitude by construction. The keystone merge does most of the
work at the working altitude: at Reach, 1014 admitted marks collapse to **420
keystones**, with 594 riding as badges.

Two honest residuals:

- **On-screen count peaks at Domains** (210 against Reach's 134 on seed-42),
  because that band's frame grows about five-fold while admission tightens only
  2.7-fold. It costs 3.8% occlusion, which is nothing, and the *admitted* set is
  monotone. The rule is not violated; the frame is simply bigger.
- **Ground carries the highest residual occlusion**, 10–25%. That is correct
  rather than tolerated: at Ground the world-size term overtakes the pixel floor
  (§4), marks become 20–56 px portraits, and two overlapping portraits are a
  legible overlap rather than a smudge.

---

## 3. Size carries no information

> **Size is not a channel. At any altitude every mark is the same size, and the
> size changes with the band — never with the datum. What a thing *is* comes
> from its icon; how much of it there is comes from the tooltip and the panel.**

The shipped atlas asks size to carry four different meanings, and the data says
none of them arrives.

- **Fleet size encodes hulls** as `13 + min(7, hulls × 0.5)`, saturating at 14
  hulls. Median hulls is **2**, so the **median fleet sits 1.0–1.5 px above the
  floor of a 7 px channel**, and every fleet big enough to use the channel is
  clamped together at the top.
- **POI size encodes magnitude** as `12 + min(8, magnitude × 0.25)`, saturating
  at 32. Median magnitude is **2–3** against a maximum of **68–120**: the ramp
  spends its whole range where nothing lives and clamps exactly where the
  variance is.
- **Works site size encodes purpose and stall** — except `Sites` is a flat
  **15.0 px at every band on every seed**, and of the freight marks that *do*
  vary, **`Stalled` is false on all nine artifacts**, so two of the four sizes
  never render.
- **War size encodes posture**, 16 px against 14 px, across a population of
  **0–3 stations per world**.

Meanwhile every rendered glyph in the atlas sits inside **12–22 px** at every
band from Realm to Ground, because the pixel floor wins almost everywhere (§4).
Four meanings were being asked to separate inside a 10 px band already shared by
five families.

**Recalibrating those ramps was the obvious fix and it is the wrong one.** Size
is a weak channel with a narrow range and no zero; it competes with every other
mark for the same pixels; and it cannot say *what kind of thing* this is, which
is the question a mark on a map is actually asked. So the channel is retired
rather than tuned:

| Was carried by size | Now carried by |
|---|---|
| fleet hulls | the tooltip; the fleet panel |
| POI magnitude | **admission** (§2.3) — a greater ruin draws at a higher altitude — and the panel |
| works purpose | its **own icon** (`icon-set.md` §3.2) |
| works stall | colour: the one loud red, unchanged |
| war posture | its **own icon**, in the burn tint |
| plague status | its **own icon** — infected and immune are two drawings |
| port tier | the **territory the port projects**, which is tier-derived already |

Port tier deserves its own line, because it is the one place a rank genuinely
exists. A tier-2 port's service radius is larger, so **the field around it is
already the size read** — Group 2's territory does the work, and putting the
same fact in the dot as well would be redundancy competing for pixels. Measured:
no world contains a port above tier 2 anyway, so the whole encoding was carrying
one bit.

---

## 4. How big a mark can be

If size says nothing, the only question left is *how large should every mark
be*, and the answer is not taste — it is the map's own geometry against the
icon's readable floor.

**The constraint is the distance between adjacent hexes on screen.** Two marks
at neighbouring hexes cannot both be wider than that distance without
overlapping, and it is a pure function of the band:

| Band | Adjacent hexes, on screen |
|---|---|
| Realm (disc fit) | **2.1 px** |
| Domains (`f = 0.70`) | **6.4 px** |
| Reach (`f = 0.30`) | **15.2 px** |
| Ground (`f = 0.05`) | **80.7 px** |

That single row of numbers settles the argument that ran through the whole
group. **An icon needs 20 px (§6.2). Realm gives a mark 2.1 px of room before
its neighbour.** No drawing, no matter how well cut, can be read at galaxy
altitude — not because the art is wrong but because the map has no space for it.
Reach is the first band where the geometry and the floor meet.

### 4.1 The size per band

Measured against the admitted set of §2.3, one keystone per hex, occlusion being
the fraction of on-screen marks whose centre lies inside another mark (badged
marks count their collar, 1.7×):

| Band | Mark | Occlusion, mature seeds | What the mark is |
|---|---|---|---|
| **Realm** | **10 px** | 1.1–5.0% | a **locator**. Kind is not the Realm question; the collar's colours say which families are here |
| **Domains** | **12 px** | 5.2–12.4% | a locator |
| **Reach** | **20 px** | 5.9–12.6% | **the icon, read.** This is the working altitude and the reason the floor is 20 |
| **Ground** | 20 px, growing with world size | 25–45% | the icon, large |

Three things this table is honest about.

**Reaching the icon floor costs occlusion, and it is worth it.** At Reach a 16 px
mark would occlude 1.0–8.1% and a 20 px mark occludes 5.9–12.6%. The extra few
points buy the difference between a mark you can identify and one you cannot,
and an overlap between two 20 px icons is a *legible* overlap — you can see both
and hover either — where the shipped 12 px smudges at 56–79% occlusion were
neither.

**Ground's number is perspective, not density.** At the 25° pitch floor the
ground plane runs to the horizon, so distant hexes compress toward the top of
the frame and pile up there. It is also the band that is already crossfading
into the orbit stage (Group 1 §4), so those marks are on their way out as it
happens.

**Above Reach the icon is drawn but not read.** The same drawing appears at every
band; at 10–12 px it is a shape with a tint, which is exactly what a locator
should be. Nothing switches, nothing pops, and the mark a player learns to
recognise at Reach is the same mark they are looking at from Realm.

---

## 5. Below the icon floor

At Realm and Domains a mark cannot say *what kind of thing* it is. That is not a
gap to be filled with a second vocabulary — the two bands do not ask that
question. What they do need is carried by the two channels that still work at
10 px:

- **Colour** — the keystone takes its owner's hue from the allocated identity
  palette (Group 2 §2.2), and each lit collar slot takes its family's key colour.
  At 4 px a pip is pure colour, and it works: hue is the one channel that does
  not degrade with size.
- **Position** — the collar's six fixed slots (§2.2). A player reading a Realm
  frame can see *that* a place is fighting and *that* another is building,
  because the lit vertex is in a known place, without either mark being
  identifiable as a drawing.

### 5.1 What a mark is before its icon exists

The icon set lands family by family (`icon-set.md` §1). A family with no drawing
yet renders as a **plain disc** at the band's size, wearing the same tint and the
same collar. Nothing about the map is wrong in the meantime — it is exactly the
Realm/Domains treatment applied at every band — and no atlas work is ever blocked
on art.

### 5.2 The floors, measured

Recorded because they are what §4 and §6.2 rest on. Eight candidate forms
rendered through the shipped `StarGen/AtlasGlyph` path at exact pixel sizes
(ledger §3.4, `form-ladder.png`):

| Form | Separates from a disc at |
|---|---|
| thick ring (≥30% band) | 8 px |
| solid square | 8–10 px |
| solid diamond | 10 px |
| triangle | 10 px |
| square ring | 12 px |
| hollow diamond | 14–16 px |
| thin ring (10% band) | 14–16 px |

**Below 8 px nothing separates from anything** — a 4–6 px mark of any form is
one to three lit pixels. Together with the 20 px icon floor this brackets the
whole design: under 8 px a mark is a dot, from 8 to 20 px it is a silhouette
with a tint, and at 20 px and above it is a picture.

---

## 6. The glyph vocabulary

**The set itself is `docs/design/ui/icon-set.md`** — what must be depicted, what
each mark means in the world, the hex-cut design language, and the build order.
This section states only what the mark budget depends on.

### 6.1 There isn't one yet

`Resources/AtlasGlyphs.png` holds sixteen icons pulled from game-icons.net during
Slice K2 to prove the atlas *plumbing* — an authored sprite, a UV rect, a runtime
tint, a contrast chip. They are licence-free placeholders picked for
availability, not drawn for this map, and every measurement says so:

- **`PoiRuinedCapital` (castle-ruins) and `FleetEscort` (checked-shield) are
  never drawn on any of the nine artifacts.** Two cells with no data behind them.
- **`PoiPrecursor` (crystal-growth) is 57–87% of every POI population**
  (178–435 of 276–498). One icon is most of what the POI lens ever says.
- **All six works kinds draw the same crane** — `GatePair` (74–136 per seed),
  `PortRaise` (38–77), `FacilityConstruction` (23–72), `HullBatch`,
  `Mobilization`, `OutpostGraduation`. The largest real distinction in the whole
  mark set has no shape at all.
- **`ancient-ruins` and `castle-ruins` never separate from each other**, at any
  size on the ladder.

So the question was never which of the sixteen survive a cull. **The atlas has no
icon vocabulary**; `icon-set.md` designs one, and the sixteen are what it
replaces.

### 6.2 The floor the design has to clear

Every one of the sixteen was rendered through the real `StarGen/AtlasGlyph`
shader at 6, 8, 10, 12, 14, 16, 20, 24, 32 and 48 px (`glyph-ladder-bare.png`).
The result is a property of *drawings*, not of these drawings:

- **6–12 px** — everything is a speck, including the simple shapes.
- **14–16 px** — only a *single closed silhouette* reads: `cancel`,
  `crossed-swords`, `checked-shield`, `tombstone`, `anchor`.
- **20 px** — where most of a well-drawn set arrives.
- **24–32 px** — where detailed line art finally resolves.

> **20 px is the floor for an authored icon, and it is only reachable by a
> drawing built as one silhouette. Below that a mark is a locator (§5), never a
> picture.**

The atlas currently draws its glyphs at **11–20 px**, which is the whole reason
`seed-42-works.png` is a field of orange smudges.

### 6.3 Where the icon is read

One drawing per subject, drawn at the band's size (§4.1). It is *read* wherever
it has 20 px:

| Surface | Size | The icon is |
|---|---|---|
| Realm, Domains | 10–12 px | a locator — a shape with a tint |
| **Reach** | **20 px** | **read.** The working altitude, and the reason the floor is 20 |
| Ground | 20 px and growing | read, large |
| Hover tooltip · legend key · panel row | 16–24 px | read |

Nothing switches between a "form map" and an "icon map": the same drawing
appears everywhere and only its scale changes, so the mark a player learns to
recognise at Reach is the one they are already looking at from Realm.

**And no atlas work is blocked on art.** A family whose icon does not exist yet
draws a plain disc at the band's size (§5.1), wearing the same tint and the same
collar. The set lands family by family.

### 6.4 The two rules that gate the set

Stated here because they are budget constraints, not art direction:

1. **Read at 20 px against family siblings.** An icon enters the set when a
   person can still tell it from the marks it shares a family and a screen with,
   rendered at 20 px and blurred (`icon-set.md` §2.3). This is deliberately a
   judgment: a mechanical overlap score was tried and abandoned, because pixel
   coincidence has no relation to how a person tells shapes apart, and drawings
   optimised against such a score become unreadable.
2. **Have a population.** A cell for a type that never occurs is a reservation,
   not a vocabulary item. Three of the shipped sixteen are reservations.

Everything else — the hexagonal field, the 60° edge family, the notation of
primitives and operators, the facility ladder, the entries and their meanings,
the atlas repack, the sourcing — is `icon-set.md`.

## 7. War and news at galaxy altitude

Group 1 requires that war stations and news pulses resolve at Realm, since
"where is anything happening" is the Realm question. Today neither does, for two
different reasons — and one of them is arithmetic nobody had written down.

**`GlyphFade` is exactly 0.000 at Realm *and* through most of Domains.** Its
window is `f = 0.63 → 0.315`; the Domains band starts at `f = 0.45`. So at
`f = 2.25` (disc fit) and `f = 0.70` the fleets, POIs, works, plague **and war**
layers all draw at alpha zero. The all-families-on frame and the news-only frame
at Realm are the same picture, and the measured 86–89% mark occlusion at Realm
is occlusion among marks nobody can see.

War is fixed by §2.3: war stations carry no weight floor and are admitted at
every band, and they drag their keystone in with them. The population is 0–3 per
world, so it costs nothing.

**News is the harder one, because the ring is the wrong geometry for a mark.**
A pulse draws as an expanding additive ring front, `_MaxPx = 4096` — deliberately
never pixel-capped, because the front is spatial. Measured, that means one pulse
is **28 px at disc fit and 155–320 px at Reach**, with 25–44 of them in frame.
`seed-42-news-reach.png` is the consequence: three dozen heavy olive hoops that
beat the territory, the lanes and every other mark in the image, while at Realm —
the band the lens is supposed to serve — the same pulse is a dot among the
domain confetti. The element is inverted with respect to the question.

Two encodings inside it are dead as well:

- **Magnitude is inert.** The alpha term is `clamp01(0.35 + 0.65 × magnitude)`,
  but pulse magnitude is unbounded — 0.5 to **16,964** across the seeds — so
  **94–98% of displayed pulses clamp to 1.0**.
- **Age has exactly one value.** On an artifact loaded and stepped once, every
  live pulse shares an emission year: `age p0 = p50 = p100 = 25` on all nine
  worlds. Radius and fade are therefore constants, and "the story is where rings
  cluster" was never testable because *every ring is the same ring*.

> **A news pulse is a keystone at its origin — a point, admitted by magnitude,
> aged by alpha. The expanding ring survives only as the emission animation:
> one ring, once, when the pulse first appears or the scrub crosses its year.**

Motion, not a persistent layer. The clustering read then comes from where the
keystones *and their badge counts* concentrate, which is legible at Realm — the
band that needed it — and the 25–44 hoops leave Reach entirely. The 40-year
display cutoff stays (Core keeps pulses live to 150; a century-old ring is
history) and is now **stated in the legend head** rather than being a silent
constant: *no word in the last forty years*.

### 7.1 The rule the two cases share

A ring's radius is a world quantity; its *screen* size is a world quantity
divided by altitude. So:

> **A spatial ring belongs to the band where its radius is small in screen
> terms.** News rings span ten hexes and therefore belong at Realm or nowhere;
> feature rims span a galaxy fraction and therefore belong at Realm too — which
> is exactly where §8 puts them.

---

## 8. Features and Emergence

Group 2 moved both out of the nature rasters and into the marks channel with no
encoding: *"sparse point sets wearing a raster's clothes"*. Measured, one of them
is sparse and the other is not, and neither is a point set.

| | seed-42 | range across seeds |
|---|---|---|
| features | 25 | 22–32 |
| cells those features occupy | 602 | 337–686 |
| origins | 149 | 11–181 |
| sterilization scars | 77 | 7–285 |

**A galactic feature is a region, not a point.** `FeaturesOverlay` marks every
cell of every feature, which is why it renders as a field: a 686-cell overlay is
not a mark set. As marks: **one keystone per feature at its centroid, carrying
its type glyph, with a dotted rim at the feature's own radius.** That is the
same geometry the news ring just lost — and it is right here for the reason §7.1
gives: a feature's rim is a galaxy fraction, so it is small on screen exactly at
Realm, where the lens belongs.

**Emergence is a point set, but a large one** — 226 to 466 marks between origins
and scars. It takes the same treatment as everything else: keystones, one per
cell, admitted by era (Current everywhere, Precursor and Other from Domains
down).

**Both are Realm-and-Domains lenses, and both fall out through Reach.** They ask
galaxy-scale questions about the *skeleton*, not the sim; they resolve precisely
where the entity families are absent; and they leave the working altitude to the
families that answer *how does this place work*. The marks channel at Realm is
empty today — these two are what belongs in it.

---

## 9. The shared machinery, re-examined

### 9.1 The contrast chip stays, once per place

The chip is a dark disc at `(9, 11, 17, 195)` and 1.45× scale under every glyph,
added because an owner-tinted glyph on an owner-tinted port dot is camouflage.
The reason still holds — Group 2's compositing budget explicitly caps a map pixel
at ~0.55 luminance *so that the chip always wins* — so the device stays.

What changes is the count. Today **every mark draws its own chip**, so N marks at
one hex stack N chips: at seed-9091's worst hex that is 13 chips at alpha 195,
which composites to **effectively opaque black**. Those are the dark blobs under
the marks in `stack-closeup-f005`. Under §2.1 there is one keystone per hex, so
there is **one chip per place** — one quad instead of thirteen, and it can never
go opaque.

Over empty space the chip is invisible by construction: `(9,11,17)` sits between
`AtlasPalette.Void` (10,10,14) and `Floor` (24,26,32). That is correct — it is a
contrast device, and it should only be visible where there is something to
contrast against.

### 9.2 The queue biases go

`WarLayer` biases its transparent queue by 120 and `PlagueLayer` by 110 so their
glyphs sort above the port dots at the same hex. With one keystone per hex there
is nothing to sort: the badge is part of the keystone's own mesh, in index order,
exactly as the chip already is. Both constants come out.

### 9.3 The dual sizing rule is replaced by a per-band size

`max(worldSize, pxFloor × pxWorld)` was written as "a world size that resolves as
the camera descends plus a pixel floor that keeps the mark visible at altitude".
Measured, the world term is almost never in play — rendered glyph sizes are
12–22 px from disc fit down to `f = 0.16`, and the world term only takes over at
Ground, where it immediately clamps at `_MaxPx = 56`. So the rule was a pixel
floor with a decoration on it.

**A mark's size is the band's size (§4.1), on the same continuous curve family as
every other fade.** Below Ground the world term takes over as it does today, so
the mark grows with the world as the camera lands, and `_MaxPx` stays as the
clamp that stops one mark eating the frame. What goes is the pretence that two
mechanisms are negotiating: one number, set by the band, and the datum never
touches it (§3).

---

## 10. Empty and degenerate states

Group 2 §6 gives the map three states — **speaking**, **silent**, **blind** — and
one carrier, the legend head. Marks inherit it unchanged, and it matters more
here than anywhere: a mark family with nothing to place draws **nothing at all**,
so the head is its only voice.

| Lens | State | The line |
|---|---|---|
| ports | blind | *nobody has built a port. There is nothing to trade between.* |
| outposts | silent | *no one has settled past their own port core.* |
| fleets | silent | *no hulls posted anywhere in view.* |
| fleets | blind | *no polity keeps ships — nothing has needed defending yet.* |
| works | silent | *nothing under construction anywhere.* |
| works | blind | *the trail begins at the next step — nothing has moved since this world was loaded.* |
| pois | silent | *nothing has happened here yet worth remembering.* |
| pois | blind | *too young for ruins.* |
| war | silent | *no fleet is on station. The wars, if any, are on paper.* |
| war | blind | *too young for enemies — no polity has met another.* |
| plague | silent | *clean lanes — no strain in the reach.* |
| plague | blind | *no strain has ever reached a port here.* |
| news | silent | *no word in the last forty years.* |
| news | blind | *nothing has happened that anyone would carry.* |
| features | silent | *this galaxy grew no features of that kind.* |
| emergence | silent | *life started once, and only here.* |
| emergence | blind | *nothing ever started. This galaxy is sterile.* |

Two degenerate cases the budget itself has to answer:

| Situation | Behaviour |
|---|---|
| A hex whose only mark is a badge-class state (a fleet in the wilds) | The state **becomes** the keystone — the rank list is a preference, not a requirement |
| Every state family present at one hex | All six collar slots light. There is no overflow case, because there are exactly six families and six vertices (§2.2) |
| Two of the same family at one hex (two fleets) | One lit slot. The collar says *what is here*; hover says how many |
| A world with one port tier (every world we have) | The Realm filter admits that tier — a relative rank can never empty the map |
| A world with 19 POIs (`epoch 42 2 21`) | Quantile floors are the world's own, so the top two still draw at Realm |
| A pulse magnitude of 16,964 | Admission is a quantile, so an unbounded quantity needs no normalization |

---

## 11. Motion

The marks channel owns three motions and no others.

- **Emission.** A news pulse rings once, outward, when it first appears or when
  the scrub crosses its year (§7). This is the atlas's only *event* motion and
  it is the one thing the news layer was always good at.
- **Resolve.** A mark's size follows the band (§4.1) on the same continuous
  curve family as everything else. Nothing switches and nothing pops — the
  drawing never changes, only its scale.
- **Selection.** The selection ring never fades and never animates on the mark;
  it is Group 6's, and it is the one mark affordance that outranks the budget.

Explicitly **not** motion: badges do not pulse, keystones do not throb, and a
stalled freight does not blink. The atlas has one loud red for "broken"; adding
motion to it would make every busy frame flicker.

---

## 12. ⚠ Amendment requested — `camera-nav-lod.md` §2

**The band × layer matrix's mark rows become weight floors, not on/off
switches**, and the ports row's tiers become **relative to the world's own top
tier**.

The evidence is one measurement: **no artifact contains a port above tier 2**
(t1 = 97–128, t2 = 75–98 on the six mature seeds; t2 = 2–5 on the degenerate
ones), so the shipped rule "ports: tier 3+ at Realm" draws **zero ports at the
band whose question is *who holds what***. An absolute tier cannot be right for a
sim whose tier ceiling is a function of its own economy.

Two smaller riders in the same row set:

- **Outposts resolve at Domains**, not at Reach. There are only 12–26 per world,
  and "how far does this reach" is literally the Domains question — the frontier
  is the answer to it.
- **POIs, fleets and works keep their Reach row as a *floor of ∞ above it***,
  which is the same behaviour written in the new vocabulary, except that a POI in
  the world's top percentile draws at Realm. On seed-42 that is three marks.

Recorded in `camera-nav-lod.md` §2 with the reasoning, per the hard rule, and
detailed here in §2.3.

---

## 13. Interfaces other groups depend on

- **Group 4 (lanes, flows & motion)** — **transient marks are yours.** Freight
  and convoys leave the mark channel entirely (§1.1); a shipment's position is a
  fraction along its stroke, and its purpose colour (`WorksLens.FreightColorOf`)
  and the one loud stalled red come with it unchanged. The mark side asks for one
  thing: a transient never merges into a hex keystone, so the two channels never
  argue about the same point. Group 1's open **lane-terminus seam** is
  unaffected by anything here.
- **Group 5 (chrome)** — the legend must show **the collar itself**, not a list
  of pip colours: six vertices, each labelled with its family, is the one figure
  that teaches the layout, and the pip colours come from the same declaration the
  rail's chips read from (Group 2 §8). The legend head
  carries the mark family's silent/blind line (§10); and a mark lens that is
  admitted-but-empty at this band is **silent**, not off, so its chip must say
  so. The band's current weight floors are legend content — *"showing the top
  tenth"* is a fact the player needs.
- **Group 6 (panels & selection)** — **every per-entity quantity the map used to
  put in a mark's size now lives with you** (§3): fleet hulls, POI magnitude,
  works progress and stall, port tier. Hover on a keystone must list every badge
  behind it, and selection must be able to address a badge, not just its
  keystone — `PanelRequest.SubId` already carries exactly that shape for
  outposts. Above Reach a mark is a locator rather than a picture (§4.1), so the
  tooltip is the only thing that can say *what* it is up there. Selection
  outranks the budget: a selected mark draws whatever its band would have culled.
- **Tier 2 (synthesis)** — **`docs/design/ui/icon-set.md` is the icon design**:
  the hex-cut design language, the notation of primitives and operators every
  mark composes from, the facility ladder, and per entry what it depicts, what it
  tells the player, its Core query, its measured population and its build tier,
  plus the atlas repack. Tier 2's manifest is the *production checklist* derived
  from it — each entry's atlas cell and its recorded read at 20 px, tracked
  through the build tiers — not an audit of the sixteen placeholders, which
  retire wholesale. **What gets commissioned first is the notation's fourteen
  parts**, not the marks; the marks compose from them.
