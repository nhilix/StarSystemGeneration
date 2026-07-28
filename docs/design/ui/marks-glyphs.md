# Marks, billboards & the glyph vocabulary

**Everything the atlas draws *at a place*.** This document is the spec for the
mark budget — what competes for a point on the map and what wins — for the
shape vocabulary those marks are drawn in, for what size means, and for the
shared billboard machinery underneath all of it.

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
form, one contrast chip. The badges are **pips** on its rim — small, family-
coloured, countable — up to **three**, then a fourth neutral pip meaning *more*.
Hover names them; selection opens them (Group 6).

This is the whole declutter. There is no force-directed dodge and no top-N by
screen density, because both would move a mark away from the thing it describes.
The marks were co-located by the *sim*, so the merge is done in the sim's own
coordinate — the hex — and it is exact, stable and order-independent.

### 2.2 Weight admits: how count falls with altitude

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

### 2.3 What it costs, measured

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

## 3. Size means rank, and nothing else

The size channel currently carries four meanings, and the data says none of them
arrives.

- **Fleet size encodes hulls** as `13 + min(7, hulls × 0.5)`, which saturates at
  14 hulls. Median hulls is **2**, so the **median fleet sits 1.0–1.5 px above
  the floor of a 7 px channel**, and the fleets that would use the channel are
  clamped together at the top.
- **POI size encodes magnitude** as `12 + min(8, magnitude × 0.25)`, saturating
  at 32. Median magnitude is **2–3** against a maximum of **68–120**: the ramp
  spends its whole range on the part of the distribution where nothing lives and
  clamps exactly where the variance is.
- **Works site size encodes purpose and stall** — except `Sites` is a flat
  **15.0 px at every band on every seed**, and of the freight marks that *do*
  vary, **`Stalled` is false on all nine artifacts**, so two of the four sizes
  never render.
- **War size encodes posture**, 16 px blockade against 14 px otherwise, across a
  population of **0–3 stations per world**.

Meanwhile the *rendered* sizes of every glyph family sit inside **12–22 px** at
every band from Realm to Ground, because the pixel floor wins almost everywhere
(§4). Four meanings are being asked to separate inside a 10 px band that is
already shared by five families.

> **Size is rank. There are three sizes: the keystone, its badges, and the
> transient marks Group 4 carries. Quantity moves to admission — a bigger fleet
> or a greater ruin *draws at a higher altitude*, it does not draw larger.**

That is the same sentence as §2.2 read from the other end, and it is why the two
questions have one answer. Everything else the size channel was carrying has a
better home already built:

| Was | Now |
|---|---|
| fleet hulls → size | admission weight; the count reads in the panel |
| POI magnitude → size | admission weight |
| works purpose → size | colour (already: `WorksLens.FreightColorOf`) + glyph (§4.4) |
| works stall → size | colour (already: the one loud red) |
| war posture → size | glyph |
| plague status → size | colour + glyph |

**Keystone size is 10 px** at its floor, because that is the measured size at
which one solid form separates from another (§5.1). **Badges are 6 px pips** on
the keystone rim; a keystone with badges occupies about **24 px** all told —
which is the same footprint one glyph plus its contrast chip occupies today
(19–23 px), while now carrying every state at that place instead of one.

---

## 4. The pixel floor and the world size, re-derived

`GlyphLayerBase` and `PortLayer` size every mark as
`max(worldSize, pxFloor × pxWorld)`, capped at `_MaxPx`. The intent recorded in
the code is "a world size that resolves as the camera descends plus a pixel
floor that keeps the mark visible at altitude". Measured, the world term is
**almost never in play**: rendered glyph sizes are 12–22 px from disc fit all the
way down to `f = 0.16`, and only at Ground does the world size take over — where
it immediately runs into `_MaxPx = 56` and clamps.

That is not a bug to tune out. It is the shape of a real seam, and naming it is
this section's whole contribution:

> **The pixel floor governs the *pip map*; the world size governs the *icon
> map*; the handover between them is the moment a mark stops being a locator and
> becomes a portrait.**

- **Realm, Domains, Reach — the pip map.** Every place is a keystone at its
  floor size with pips for its states. The map answers *where*, *whose*, and
  *what kind of place*. It does not answer *which kind of ruin*; the pointer
  does (Group 2's "hue narrows the field, the pointer resolves it", applied to
  marks).
- **Ground — the icon map.** The world term overtakes the floor, keystones pass
  20 px, and marks **become their glyphs**. This is measured, not asserted:
  `stack-closeup-f005` is the first shot in the project where the authored icons
  are identifiable, and they are 30–56 px there.
- **The handover is a crossfade, not a switch**, on the same curve family as
  everything else, and it completes before the orbit crossfade begins so the two
  never overlap.

`_MaxPx` stays as the clamp that stops a single mark eating the frame; the icon
map's ceiling is the clamp, not an accident.

---

## 5. Form: the vocabulary below the icon floor

### 5.1 What a form costs in pixels — measured

Eight candidate forms were rendered through the shipped `StarGen/AtlasGlyph`
path at exact pixel sizes (ledger §3.4, `form-ladder.png`). The floor at which
each stops reading as a generic blob:

| Form | Separates from a disc at |
|---|---|
| thick ring (≥30% band) | **8 px** |
| solid square | 8–10 px |
| solid diamond | **10 px** |
| triangle | 10 px |
| square ring | 12 px |
| hollow diamond | 14–16 px |
| thin ring (10% band) | 14–16 px |

Below **8 px nothing separates from anything**: a 4–6 px mark of any form is one
to three lit pixels. This sets the keystone floor at 10 px (§3) and it settles
the outpost.

### 5.2 The form vocabulary

| Form | Means | Used by |
|---|---|---|
| **solid disc** | a market — a place that trades | port |
| **solid diamond** | a claim without a market | **outpost** |
| **thick ring** | a place that is gone — history, not commerce | POI |
| **triangle** | a thing of the deep past, oriented | precursor origin, sterilization scar |
| **solid square** | a built thing (already the orbit stage's facility mark) | reserved — SystemStage |

The disc/diamond pair is the decision the outpost needed. **An outpost is a
diamond at the same size as a port's disc**, because size and lightness are both
spent (Group 2 §2.4) and — as §5.1 shows — every *hollow* form needs 12–16 px,
which no keystone can promise. Diamond specifically: the square is already the
orbit stage's facility glyph, and a diamond's 45° axis survives a pixel grid
better than a square's, which aliases toward a disc at exactly the sizes that
matter.

Today an outpost is a **5.5 px dot** against a tier-1 port's **6.8 px dot**, with
a quarter-lift toward white. That is 1.3 px and one luminance step, at every band
on every seed. It is not a distinction; it is a rounding error.

---

## 6. The glyph vocabulary

### 6.1 The readability ladder

Every one of the 16 authored icons was rendered through the real shader at 6, 8,
10, 12, 14, 16, 20, 24, 32 and 48 px (`glyph-ladder-bare.png`,
`glyph-ladder-chip.png`). The result is not one number, because the icons are not
one kind of drawing:

- **6–12 px** — every icon is a speck. Nothing is identifiable, including the
  simple ones.
- **14–16 px** — icons that are a *single closed silhouette* begin to read:
  `cancel`, `crossed-swords`, `checked-shield`, `tombstone`, `anchor`.
- **20 px** — the honest floor. Most of the set reads. `radar-sweep`,
  `flying-flag` and `cardboard-box` arrive here.
- **24–32 px** — the detailed line art finally resolves: `cargo-ship`, `crane`,
  `crystal-growth`, `rocket`, `regeneration`.
- **`ancient-ruins` and `castle-ruins` never separate from each other**, at any
  size in the ladder. Two brown building silhouettes.

> **The readable floor for an authored icon is 20 px, and only for icons drawn
> to a silhouette rule. Below that the mark carries a form, not a picture.**

The atlas draws its glyphs at **11–20 px** — which is to say the entire authored
vocabulary lives in the band where it does not resolve. `seed-42-works.png`'s
orange smudges are not a rendering fault; they are the arithmetic.

One free improvement fell out of the measurement: `Resources/AtlasGlyphs.png` is
**512 × 640, DXT5, `mipmapCount = 1`**. A 128 px cell sampled down to 14 px is a
raw bilinear read of four texels. Generating mipmaps costs nothing and improves
every size below 128 — it does not move the floor (the floor is
information-theoretic), but it removes the shimmer.

### 6.2 The shape budget is allocated backwards

Which authored cells the data actually reaches, across all nine artifacts:

- **`PoiRuinedCapital` (castle-ruins) is never drawn.** Not once, on any seed.
- **`FleetEscort` (checked-shield) is never drawn.** Escort never appears as a
  marker posture.
- **`PoiPrecursor` (crystal-growth) is 57–87% of every POI population**
  (178–435 of 276–498). One icon is most of what the POI lens ever says.
- **All six works kinds draw the same crane.** `GatePair` (74–136 per seed),
  `PortRaise` (38–77), `FacilityConstruction` (23–72), `HullBatch`,
  `Mobilization` and `OutpostGraduation` are genuinely different events — a jump
  gate, a port being raised, a shipyard, a mobilization — and the map calls them
  all "a crane".

So the shape channel spends five cells on a distribution that is one type most
of the time, two cells on things that never happen, and **zero** on the largest
real distinction in the mark set.

### 6.3 The revision

**Three rules, and Tier 2 builds the manifest from them.**

1. **An icon must pass the silhouette test at 20 px** — readable as one filled
   silhouette, and distinct in outline from every sibling in its family. This is
   a **gate on the art**, verified by regenerating the ladder, not a matter of
   taste. `ancient-ruins` / `castle-ruins` fail it as a pair.
2. **An icon must have a population.** A cell whose type never occurs is not a
   vocabulary item; it is a reservation. `PoiRuinedCapital` and `FleetEscort`
   come out (their meanings stay in the panel text) and their cells are
   reclaimed — the atlas is 4 × 5 = 20 cells with 17 in use, so the set has room
   without growing.
3. **Cells go where the distinctions are.** The works family gains icons for its
   real kinds — gate, port raise, facility, hull batch, mobilization — and the
   POI family sheds the ones its data does not populate.

`AtlasGlyphs`' enum order is the atlas layout and **appending is the only legal
edit**, so a re-cut set is a *new tail plus retired cells*, never a reorder. The
retired cells stay in the enum, unused, with the reason recorded — that is the
cheapest way to honour the append rule while changing what is drawn.

### 6.4 Where an icon is still worth drawing

Even under §4's pip map, authored shape earns its place in four surfaces that
have the pixels: **Ground**, the **hover tooltip**, the **legend key**, and the
**panel** row. The 16 icons were never wasted work — they were **filed at the
wrong altitude**.

---

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

War is fixed by §2.2: war stations carry no weight floor and are admitted at
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

### 9.3 The dual sizing rule stays, re-justified

Not as a fallback but as §4's seam. It is the only mechanism in the atlas that
makes a mark behave differently at two altitudes, and now it has a stated job.

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
| More than four states at one hex | Three pips plus a neutral *more* pip; hover lists them all |
| A world with one port tier (every world we have) | The Realm filter admits that tier — a relative rank can never empty the map |
| A world with 19 POIs (`epoch 42 2 21`) | Quantile floors are the world's own, so the top two still draw at Realm |
| A pulse magnitude of 16,964 | Admission is a quantile, so an unbounded quantity needs no normalization |

---

## 11. Motion

The marks channel owns three motions and no others.

- **Emission.** A news pulse rings once, outward, when it first appears or when
  the scrub crosses its year (§7). This is the atlas's only *event* motion and
  it is the one thing the news layer was always good at.
- **Resolve.** The pip→icon handover at Ground (§4) is a crossfade, not a
  switch. Nothing pops.
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
detailed here in §2.2.

---

## 13. Interfaces other groups depend on

- **Group 4 (lanes, flows & motion)** — **transient marks are yours.** Freight
  and convoys leave the mark channel entirely (§1.1); a shipment's position is a
  fraction along its stroke, and its purpose colour (`WorksLens.FreightColorOf`)
  and the one loud stalled red come with it unchanged. The mark side asks for one
  thing: a transient never merges into a hex keystone, so the two channels never
  argue about the same point. Group 1's open **lane-terminus seam** is
  unaffected by anything here.
- **Group 5 (chrome)** — the legend must key **badge pip colours** by family, in
  the same declaration the rail's chips read from (Group 2 §8); the legend head
  carries the mark family's silent/blind line (§10); and a mark lens that is
  admitted-but-empty at this band is **silent**, not off, so its chip must say
  so. The band's current weight floors are legend content — *"showing the top
  tenth"* is a fact the player needs.
- **Group 6 (panels & selection)** — the pip map deliberately stops at family:
  **the pointer resolves the kind.** Hover on a keystone must list every badge
  behind it, and selection must be able to address a badge, not just its
  keystone — `PanelRequest.SubId` already carries exactly that shape for
  outposts. Selection outranks the budget: a selected mark draws whatever its
  band would have culled.
- **Tier 2 (the icon manifest)** — §6.3 is the input. Every entry needs: name,
  meaning, source, **the silhouette-test result at 20 px**, tint rule, the
  surfaces it appears on (Ground / tooltip / legend / panel), and its atlas cell.
  Retired cells stay in the enum with their reason. The two rules the manifest
  enforces are *pass at 20 px* and *have a population*.
