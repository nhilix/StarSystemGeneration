# The icon set

**The atlas's authored vocabulary: what it must depict, what each mark means in
the world, and the rules every drawing obeys.** This document is the
commissioning brief for the icon set and the reference for anyone reading one.

It rides `docs/design/ui/marks-glyphs.md`, which owns the mark budget, the
collar, and the per-band mark size these drawings have to live inside (§6 there
is the summary; this is the set).
Populations quoted here are measured across the six mature radius-21 seeds and
three degenerate worlds in the Group-3 evidence base — recipes in
`docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` §"Group 3".

---

## 1. What an icon is for

**A mark's icon is the only thing that says what kind of thing is there.** Size
carries nothing (`marks-glyphs.md` §3); colour carries owner and family;
position carries place. Kind is the icon's job, and quantities — hulls,
magnitude, progress, tier — live in the tooltip and the panel, not on the map.

That makes the icon load-bearing, and it means the drawings have to survive the
sizes the map can actually give them.

**20 px is a measured floor.** Every icon in the shipped placeholder sheet was
rendered through `StarGen/AtlasGlyph` at 6, 8, 10, 12, 14, 16, 20, 24, 32 and
48 px: below 12 px everything is a speck; at 14–16 only a single closed
silhouette reads at all; 20 px is where a well-drawn set arrives.

**And the map's own geometry decides where 20 px is available.** Two marks at
adjacent hexes cannot both be wider than the on-screen distance between those
hexes, which is a pure function of altitude:

| Surface | Adjacent hexes | Mark | The icon is |
|---|---|---|---|
| Realm | **2.1 px** | 10 px | a **locator** — a shape with a tint. No drawing can be read here; the map has no room for one |
| Domains | 6.4 px | 12 px | a locator |
| **Reach** | 15.2 px | **20 px** | **read.** The working altitude, and the reason the floor is 20 |
| Ground | 80.7 px | 20 px, growing | read, large |
| Hover tooltip · legend key · panel row | — | 16–24 px | read |

**One drawing per subject, at every band.** Nothing switches between a "form
map" and an "icon map"; only the scale changes. So the mark a player learns to
recognise at Reach is the same mark they are already looking at from Realm — and
an icon has to work *both* as a picture at 20 px and as a silhouette at 10.

**Nothing in the atlas is blocked on a drawing.** A family whose icon does not
exist yet renders as a plain disc at the band's size, wearing the same tint and
the same collar, so the set can be commissioned and landed **family by family**.

---

## 2. The design language: hex-cut

**Nothing in this world exists.** No player has ever seen a starport, a
precursor site, a gate under construction or a sterilization scar. There is no
photograph to simplify and no object to recognise, so an icon here cannot work
the way a printer icon works — by reminding you of a thing you already know.

> **The set is a notation, not a collection of pictures. Every mark is learned,
> and the set's first duty is to be learnable.**

That single fact decides the design language. A notation is learnable when it is
built from few parts combined in ways that mean the same thing everywhere, so
that a mark you have never seen can still be read. The rules below serve that,
and the shape library in §2.4 *is* the notation.

The second fact is the medium: near-black, additive, hexagonal, tinted at
runtime, and read at a glance from a distance — 20 px at the working altitude
(§1).

### 2.1 The construction system — what makes it one family

1. **The hexagon is a field, not a cutter.** Every icon is composed on the
   flat-top hexagonal field in the lattice's orientation, inherits its angles
   and its optical size, and may align to or touch its edges. **Icons do not
   fill it.** A mark cut flush to the envelope has surrendered its own
   silhouette to a shape the whole set shares, and silhouette is the channel
   that survives when everything else has gone (rule 7). The field shows up as
   geometry and scale, not as a boundary stamped over the drawing.
2. **The 60° family.** Edges run at 0°, ±60°, ±120°, plus vertical where an icon
   has a vertical axis of symmetry. Curves only as full circles or 60°-centred
   arcs. This is what ties the marks to the lattice they sit on, and it is house
   identity rather than a legibility claim — a free-angle set of the same
   subjects reads neither better nor worse.
3. **One optical weight.** Every icon carries the same visual heft, so no mark
   shouts over its neighbours in a legend row, on a collar, or in a system view.
   A spindly mark among solid ones reads as *broken* rather than as different.
   This is the strongest cohesion signal the set has. It is judged by eye across
   a rendered row; ink coverage is a diagnostic for finding outliers, never the
   target.
4. **One level of abstraction.** Every icon is reduced to the same degree. A set
   stops reading as a family the moment one member is more literal than its
   neighbours.
5. **Consistent terminals and junctions.** Members end on a flat cut in the 60°
   family and meet at 60°. Repeating this detail is most of what makes unrelated
   marks look drawn by one hand.
6. **Solid mass; pure white on transparent; tinted at runtime.** No outline, no
   line art, no gradient, no baked colour. Outlines are the first thing to die
   between 32 px and 20 px, and every atlas layer already assumes a per-instance
   tint over white.

### 2.2 The distinctness discipline — what keeps members apart

7. **Silhouette first.** An icon is designed as an outline before it is filled.
   **If two icons have the same outline they are the same icon**, whatever
   differs inside them; interior detail is worth almost nothing at 20 px.
8. **Spread the primary channels.** Four channels carry nearly all of the read
   at small size, in this order: **silhouette · topology** (solid, holed, split,
   radiating) **· dominant axis** (vertical, horizontal, diagonal, radial, none)
   **· mass distribution** (grounded, centred, top-heavy, dispersed). The set is
   designed so these vary deliberately across it. **No more than three icons may
   share one combination of topology and axis** — four marks making the same
   design decision is what a sea of near-identical icons actually is.
9. **A difference is a silhouette event.** Where two marks are deliberately
   related, what separates them is a bold structural change to the outline — a
   break, a collapse, a strike, an opening. Never a small internal annotation and
   never a nick in an edge. A related pair **should** share most of its mass;
   that shared mass is what makes the pair read as one thing in two states. The
   failure mode is a timid difference, not a shared root.

### 2.3 The size contract — what survives the working altitude

10. **Two or three elements. Never more.**
11. **No member thinner than 2.5 units** (2 px at the floor — what survives a
    bilinear downsample); counter-forms ≥ 3 units.
12. **The squint test, against siblings.** Before an icon enters the set it is
    rendered at 20 px beside the marks it shares a family and a screen with, and
    blurred. If the pair still tells apart, it passes. **This is judged by a
    person.** No pixel measure can see whether two marks read as the same *kind
    of thing*, which is the way icon sets actually fail.

### 2.4 The shape library

The notation has three layers: **primitives** name things, **operators** do
things to them, and **the facility ladder** (§2.5) handles the one family too
large to name flatly. A reader who has learned eight primitives and six
operators can read a mark they have never seen, which is the whole return on
building a notation rather than drawing thirty pictures.

#### Primitives — the nouns

| Primitive | Means | Appears in |
|---|---|---|
| **disc** | a settled place that trades | starport, market |
| **diamond** | a claim, or a beginning, without a market | outpost, origin |
| **hex ring** | a made boundary, an enclosure | gate |
| **chevron** | a vessel; it points, and pointing is meaning | fleet postures |
| **bar** | a berth, or a barrier — a line that holds or stops | fleet posted, blockade |
| **block** | a built work, seated on ground | every facility |
| **shard** | deep time — something that radiated and ended | precursor site |
| **carry** | a short radiating stroke: something propagating | news, AGN outburst |

Eight, and the count is a **budget rather than an inventory**: a ninth primitive
costs every player one more thing to learn, so a new subject composes from these
or argues its way in.

#### Operators — the verbs

An operator applies to any primitive and means the same thing wherever it
appears. Each is defined as a **silhouette event** (rule 9), so applying one is
automatically a large change to the outline rather than a decoration on it.

| Operator | The drawing | Means |
|---|---|---|
| **struck** | a bar driven across the primitive | interdicted, cut, held by someone else |
| **broken** | the outline torn open, a part collapsed away | ruined — the residue of a thing |
| **hollowed** | the mass emptied into a shell | erased, scarred — the memory of a thing |
| **raised** | a chevron rising out of the primitive | under construction, becoming |
| **bitten** | bites taken from the rim | damaged, infected |
| **massed** | the primitive repeated three times | a field or cluster of the thing |

Two consequences worth stating, because they replace rules the set used to carry
separately:

- **An event and its residue are one primitive under one operator.** A ruin is
  the block *broken*; a sterilization scar is the diamond *hollowed*; a
  battlefield is the block *broken* in the war tint. Residues need no invention
  of their own — and because every operator is a silhouette event, the pair
  separates without either mark being redesigned away from its sibling.
- **Orientation belongs to the chevron alone.** It is the one primitive whose
  meaning includes pointing, so the fleet postures differ by where the chevron
  points and by what holds it. Nothing else in the notation uses rotation to mean
  anything, because a rotated mark is among the first things a downsample
  destroys.

### 2.5 The facility ladder — the family that cannot be flat

`InfraTypeId` carries **fourteen buildable facility types** and the set draws
them all as one block. At map scale that is defensible — a works site is a works
site. **In the system view it is not:** a mature system becomes a field of
identical blocks, which tells the player nothing about what the place does.

The notation answers this the way a notation should — with a ladder, not with
fourteen inventions:

> **A facility mark is its family's root. The map draws it and the system view
> reads it. Which member of the family it is gets named in the tooltip and the
> panel, not drawn.**

The four roots are the sim's own `InfraFamily`, so the vocabulary cannot drift
from what the sim actually emits:

| Family root | Members | The root says |
|---|---|---|
| **Extraction** | Mine · Skimmer · Agri-complex · Excavation site | *something is being taken out of a body* |
| **Processing** | Refinery · Chemworks · Fabricator · Exotics lab | *something is being turned into something else* |
| **Heavy** | Foundry · Shipyard · Arsenal · Compute core | *the strategic works — hulls, arms, thought* |
| **Support** | Depot · Fortress | *the place is held and supplied* |

**The system view has less room than it looks, and that is what fixes the
ladder's shape.** Measured against the shipped stage: a facility mark is
**8.1 px** where the orbit stage fades in and **16.3 px** at the closest the
camera can go, on a 1000-px-tall viewport (11.7 → 23.5 px at 1440). The
billboard's `_MaxPx = 36` is never reached — the binding constraint is
`CameraRig._minDistance = 2.5`. **A facility mark never clears the 20 px icon
floor on a 1080p-class window.**

Four roots at 16 px is precisely the band where *a single closed silhouette*
reads (§1), so the roots work. Fourteen member variants at 16 px do not, and no
amount of drawing skill changes that — which is why the member layer is named
rather than drawn.

**And the marks are not crowded, only identical.** Consecutive facilities on one
body sit **23.3 px apart centre-to-centre against a 16.3 px mark**: the 0.85 rad
angular step in the stage's layout dominates its 0.016 radial step, so they never
touch. The sea of identical icons comes from one shared drawing and nothing else,
which is exactly why replacing that one drawing with four is the whole fix.

Three properties make this the answer rather than *an* answer:

1. **One vocabulary, both surfaces.** Four roots replace one block at map scale
   *and* in the system view — one set of drawings, no second vocabulary to learn
   on the way down.
2. **It obeys rule 8 by construction.** Four roots is under the three-per-
   combination limit as long as each takes its own topology and axis, which is
   the whole design job for this family.
3. **It scales with the sim.** A fifteenth facility type joins a family and
   inherits its root. It never costs a new primitive and never forces a redesign.

**If the member layer is ever wanted on the mark**, the measurement says exactly
what it would cost: raising the stage's facility world size from **0.038 to
0.047** puts the mark at 20 px at closest zoom on a 1000-tall viewport. That is
a one-line atlas change, it is not made here, and it should be argued on whether
a player needs *mine versus skimmer* at a glance — the panel already answers it
without spending a pixel.

### 2.6 Tint

Icons are white; every colour is applied at draw time from the same Core
declarations the map and the legend already read (Group 2 §8). No icon carries
its own colour.

| Family | Tint source |
|---|---|
| Places | `AtlasPalette.OwnerColor`, quarter-lifted (the port keystone's convention) |
| Works | `WorksLens.SiteAmber`, cooling toward ember by `FedFraction` — a starving site is a colour read |
| Fleets | owner hue, quarter-lifted |
| War stations | `WarLens.StationBurn` — a war station is a *fleet icon in the burn tint*, never a separate shape |
| Health | `PlagueLens.InfectedBurn` / `ImmuneScar` |
| Deep time | `PoiLens.ColorOf(type)`; dormant precursor sites lift +40 per channel (a live remnant, not an inert ruin) |
| Nature | `NatureLens`' per-type feature colours |
| Word | `NewsLens.Parchment` |

---

## 3. The set

**Thirty marks.** Every entry states **what it composes from** — the notation of
§2.4, so each mark can be checked against the library rather than taken on trust
— what a player learns from it, the Core query behind it, and its measured
population per mature world. **Build tier** is A (the shippable core), B (the
second pass), or C (completes the vocabulary), ordered by population × how much
a decision leans on it.

Two reading notes. **Primitives combine; operators transform.** `disc + bar` is
two primitives placed together; `disc struck` is one primitive under one
operator. And where an entry names an operator, the difference from its sibling
is a silhouette event by construction (§2.2 rule 9) — that is the whole reason
the operators exist.

### 3.1 Places — where people are

Source: `PortLens.Markers`, `DomainInteriorMarks.Build(...).Outposts`.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **starport** | disc + bar (the berth) | *trade happens here.* Its tier sets the service radius that draws the territory around it | 178–219 ports (tiers 1–2 only) | **A** |
| **outpost** | diamond + bar (the stake) | *someone lives here and there is no market.* A frontier holding inside a domain; it can graduate into a port | 12–26 | **A** |
| **market** | disc + bar, balanced | *this is where the price you are reading comes from* | one per port | C |

**The market is exempt from the map contract.** At map scale a market is what a
port *is*, so it never draws there — it appears only in the panel, the tooltip
and the legend, at 16–24 px in a labelled row. It is the one mark allowed a fine
internal distinction from `starport`, because it is only ever read on a surface
that has room and a caption (§2.2's split of the two jobs).

Port **tier is not an icon distinction, and it is not any other mark distinction
either.** A tier-2 port projects a larger service radius, so **the territory
around it is already the read** — putting the same fact in the mark as well is
redundancy competing for pixels. Three tier icons would be three near-identical
discs, and measured, no world contains a port above tier 2, so the whole
encoding was carrying one bit.

### 3.2 Works — the in-flight world

Source: `WorksLens.Sites` → `ProjectKind`. Everything here is *becoming*, so
everything here carries **raised** — the operator is the family, which is what
lets a player read a works mark they have never seen as "something is being
built" before they know what.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **gate laid** | hex ring, **raised** | *two ports are being connected.* When it completes a lane opens and the network changes shape — the single most consequential thing under construction | 74–136 | **A** |
| **port raised** | disc, **raised** | *a new starport is being founded here.* The map is about to gain a market and a service radius | 38–77 | **A** |
| **facility raised** | block, **raised** | *industry is being added to an existing domain* — the domain deepens rather than spreads | 23–72 | **A** |
| **hull batch** | block + chevron departing | *ships are being built.* Fleet strength is about to change | 9–17 | B |
| **mobilization** | chevron, **massed** | *this polity is arming.* War is being prepared, not yet fought | 3–18 | B |

**`mobilization` and `battlefield` are the notation's clearest pair**: a massing
of vessels, and vessels broken (§3.6). One primitive, two operators, cause and
consequence — and it replaces the old *crossed axes*, which was a ninth
primitive earning its keep in exactly two marks.

**Works do not split by facility family.** `ProjectKind` exposes
`FacilityConstruction` without the `InfraTypeId` beneath it, and at map scale
*industry is arriving* is the read. If the lens ever surfaces the type, the four
roots of §3.3 are already there to carry it.

`OutpostGraduation` (0–1 per world) draws **port raised** — it is a port being
founded, by promotion rather than expedition, and a cell for a once-per-world
event is a reservation, not a vocabulary item.

`ColonyExpedition` is not here: it is a convoy in transit, which leaves the mark
channel entirely for Group 4's strokes (`marks-glyphs.md` §1.1).

### 3.3 Facilities — the built system

Source: `SystemQuery` → `InfraTypeId`, grouped by `InfraFamily`. **New in this
set**, and the answer to the system view's field of identical blocks (§2.5).

These are the *built* facility, not the works site that raised it: no **raised**
operator, and the block is complete.

| Icon | Composes from | What it tells the player | Members | Tier |
|---|---|---|---|---|
| **extraction** | block + shard (driven into a body) | *something is being taken out of this body* | Mine · Skimmer · Agri-complex · Excavation site | **A** |
| **processing** | block + carry (something leaving, changed) | *something is being turned into something else* | Refinery · Chemworks · Fabricator · Exotics lab | **A** |
| **heavy** | block, doubled and massive | *the strategic works — hulls, arms, thought* | Foundry · Shipyard · Arsenal · Compute core | B |
| **support** | block + hex ring (held, supplied) | *the place is held and supplied* | Depot · Fortress | B |

Four marks replace one, and **which member** is named in the tooltip and the
panel rather than drawn — the system view gives a facility mark 8.1–16.3 px on a
1000-tall viewport (§2.5), which is a single-closed-silhouette band. Four roots
fit it; fourteen member variants do not.

⚠ **Rule 8 needs watching here.** All four roots take the block, so they must
separate on axis and mass distribution, and they are the set's densest cluster on
one primitive. This is the group the squint test should be run on first.

### 3.4 Fleets — hulls, and what they are doing

Source: `FleetLens.Markers` → `FleetPosture`. One primitive, the chevron —
**the only primitive allowed to mean something by where it points** (§2.4).

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **posted** | chevron + bar, pointing out | *freight capacity is assigned to a lane here.* This is what makes trade move | 42–61 | **A** |
| **blockade** | disc, **struck** | *this port's approaches are held by someone else.* Its lanes are cut | 0–2 | **A** |
| **reserve** | chevron + bar, pointing in | *hulls are docked and decaying in readiness.* Strength on paper, not in the field | 21–28 | B |
| **patrol** | chevron + arc (the circuit) | *legality is being enforced in this domain* | 10–19 | B |
| **expedition** | chevron, crossing the field's edge | *a war fleet, colony convoy or ruin expedition is out.* Something is being attempted at distance | 1–4 | B |

**`blockade` moved off the chevron and onto `disc struck`.** It is the one fleet
posture whose subject is *the port*, not the hulls — what the player needs is
"this place is cut", and **struck** says exactly that. It also takes a fourth
mark off the chevron, which rule 8 was about to fail.

⚠ **`posted` and `reserve` are a mirror pair**, separated only by where the
chevron points. Rule 7 is marked provisional in §2.1 for precisely this: a mirror
is the one difference a person can fail to read even when it measures wide open.
**This pair is the second thing the squint test should be run on**, and if it
fails, `reserve` takes a different holder rather than a different direction.

**War does not get its own shape.** `WarLens.Stations` re-reads the same
postures; a war station is a **blockade or expedition mark in the burn tint**.
That is already how the code works, it is one fewer thing to draw, and it says
the true thing: what makes a fleet a war station is not what it is doing but who
it is doing it to.

`Escort` gets no mark: it never appears as a marker posture on any world
measured. Its meaning stays in panel text.

### 3.5 Health — contagion and its memory

Source: `PlagueLens.Marks` → `PortPlagueStatus`.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **infected** | disc, **bitten** | *a strain is burning at this port right now.* Its lanes may be quarantined | 0–2 | **A** |
| **immune** | disc + hex ring (enclosed) | *this port has survived a strain and is protected until the window lapses* | 0–2 | C |

**`immune` is no longer "the bites healing closed".** A half-healed bite is a
fine internal annotation and a bad silhouette event, which rule 9 forbids.
Enclosure is what immunity actually *means* — a made boundary around the place —
and it reads as a different figure rather than as a subtler version of the same
one.

### 3.6 Deep time — the galaxy before anyone

Source: `PoiLens.Marks` → `PoiType`; `GalaxySkeleton.Origins`;
`PrecursorWave.Sites` where `PrecursorSiteType.SterilizationScar`.

**This is the largest family by population and the one the game is most
distinctive about.** The precursor site alone outnumbers ports.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **precursor site** | shard, **massed** | *deep-time archaeology is here* — exotics, hazard and research, and a claim worth reaching for. Lifts brighter when **dormant**: a live remnant, not an inert ruin | **178–435** | **A** |
| **battlefield** | chevron, **broken** | *hulls died here.* Salvage, and a grudge with a date on it | 36–48 | B |
| **memorial** | diamond + bar (seated) | *a famine or an atrocity is remembered here.* It shapes stance and culture, not trade | 19–45 | B |
| **ruin** | block, **broken** | *a dead city.* Settlement is suppressed here and there is salvage in it | 4–16 | B |
| **sterilization scar** | diamond, **hollowed** | *life downstream of here was delayed or erased.* The emergence map still carries the shadow | 77–285 | B |
| **origin** | diamond + shard | *sapience started here.* Era tints it: current, precursor, or a pre-spaceflight native | 149–181 | C |

Three of these are residues, and each is **one primitive under one operator** —
`ruin` is the works block broken, `battlefield` is the fleet chevron broken,
`sterilization scar` is the origin diamond hollowed. The pair always shares its
primitive, and the operator always changes the outline, which is the design
holding both halves of §2.2 rule 9 at once.

⚠ **`memorial` (diamond + bar) and `outpost` (diamond + bar) are the same
composition** in different families, separated only by proportion and by tint.
This is a **known collision carried deliberately into the squint test** rather
than resolved on paper: if it fails, `memorial` takes the **massed** operator
(names remembered together) and `outpost` keeps the plain stake.

### 3.7 Nature — the galaxy's own history

Source: `GalaxySkeleton.Features` → `GalacticFeatureType`. These are **region**
marks: one mark at the feature's centroid with a dotted rim at its extent, and
they are a **Realm and Domains** read (`marks-glyphs.md` §8) — so they are read
at 10–12 px as locators, never as pictures, and they are the least demanding
entries in the set.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **globular cluster** | disc, **massed** | *ancient, compact, metal-poor.* Its hexes roll on a different star table | 4–8 | C |
| **AGN outburst** | carry, **massed**, from off-field | *the core fired once.* A sterilization and enrichment wave crossed this ground | 6–7 | C |
| **merger stream** | diamond, **massed** along a 60° trail | *another galaxy fell in here.* Foreign metallicity and a datable starburst along the trail | 1–3 | C |
| **emission nebula** | hex ring, **hollowed** (open on one side) | *gas, and stars forming in it now* | 0–8 | C |
| **supernova remnant** | hex ring, **broken** | *massive stars died here recently.* A young graveyard glow | 6 | C |

`DarkCloud` gets no mark: it occurs on none of the nine artifacts. A quiet gas
region reads as an **emission nebula** with the nature field's own colour saying
it is cold — the field is already carrying that distinction, so a mark would be
duplicating it.

### 3.8 Word

Source: `NewsLens.Pulses`.

| Icon | Composes from | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **news origin** | carry, **massed**, around a source | *something happened here that people are still talking about.* Age fades it; the 40-year display window is stated in the legend | 70–96 shown of 448–573 live | **A** |

⚠ **`news origin` and `AGN outburst` are both `carry, massed`**, separated only
by whether the carries are centred or arrive from off-field. They never share a
band — news is a Reach-and-below mark, AGN a Realm-and-Domains region mark — so
they are never on screen together, which is the reason this is tolerable rather
than a rule-8 breach. Recorded so it is not mistaken for an oversight.

### 3.9 The build order

**A — the shippable core (11).** starport · outpost · precursor site · gate laid
· port raised · facility raised · extraction · processing · fleet posted ·
blockade · infected · news origin.

**B — the second pass (11).** reserve · patrol · expedition · hull batch ·
mobilization · heavy · support · battlefield · memorial · ruin · sterilization
scar.

**C — completes the vocabulary (8).** market · immune · origin · globular
cluster · AGN outburst · merger stream · emission nebula · supernova remnant.

**But the true first delivery is neither.** Because the set is a notation, what
gets drawn first is the **eight primitives and six operators** (§2.4, §5.3) —
fourteen parts, drawn once and drawn well. Every mark above is a composition of
them, and a set whose parts are right is consistent by construction. Tier A is
the first eleven *compositions*, not the first eleven drawings.

## 4. What is deliberately absent, and why

| Not drawn | Reason |
|---|---|
| `PoiType.RuinedCapital` | zero occurrences across nine artifacts — draws **ruin** |
| `GalacticFeatureType.DarkCloud` | zero occurrences; the nature field already says "cold gas" |
| `FleetPosture.Escort` | never appears as a marker posture |
| `ProjectKind.OutpostGraduation` | 0–1 per world; it *is* a port being raised |
| Port tier variants | carried by keystone size and service radius; no world exceeds tier 2 |
| A "war" shape | war is a tint on a fleet mark (§3.4) |
| A completed jump gate | a built gate **is a lane**; its terminus mark is Group 4's (`camera-nav-lod.md` §9's open seam) — this set supplies the mark if they want one |
| Freight, convoys, crawls | transients: they ride strokes, not places (`marks-glyphs.md` §1.1) |
| **The fourteen facility members** | the system view gives a facility mark 8.1–16.3 px (§2.5) — a single-closed-silhouette band. Four family roots fit it; fourteen variants do not, so the member is **named in the tooltip and the panel** |
| **A ninth primitive** | the library is a budget, not an inventory (§2.4). *Crossed axes* was the ninth and it earned its keep in two marks; both now compose from the chevron under **massed** and **broken** |

Two rules underneath all of these.

**A mark must have a population.** A cell for a type that never occurs is not
vocabulary, it is a reservation, and the shipped placeholder sheet has three of
them.

**A mark must be readable where it is read.** A distinction that cannot survive
the pixels its surface actually gives it is not a mark — it is a caption, and it
belongs on a surface that has room for captions. That is the whole argument for
the facility ladder, and it is measured rather than asserted.

---

## 5. Producing it

### 5.1 The atlas sheet

`Resources/AtlasGlyphs.png` is 512 × 640 today: 128 px cells, 4 columns × 5
rows, 17 of 20 used. The set needs **47 cells** — 17 legacy plus 30 new.

- **The sheet becomes 4 columns × 12 rows, 512 × 1536** (48 cells; 47 used, 1
  spare). Column count stays at 4 deliberately: `AtlasGlyphs.UvRect` derives
  every rect from `Columns`/`Rows`, so both the constants and the PNG change
  together and the repack is mechanical. One spare cell is thin — the next
  addition takes the sheet to 4 × 13, which is the same mechanical repack.
- **What is frozen is the enum, not the pixels.** `AtlasGlyph`'s index → meaning
  mapping is the contract that must never be reordered; the sheet geometry is
  derived from it and may be repacked freely.
- **The sixteen placeholders stay in the enum as dead cells**, marked legacy with
  the reason recorded, and the new set is a **new tail from cell 17**. That
  honours the append-only rule exactly, at the cost of a few dead texels.
- **Generate mipmaps.** The sheet ships with `mipmapCount = 1`, so a 128 px cell
  sampled to 20 px is a raw bilinear read of four texels. Mipmaps cost nothing
  and remove the shimmer at every size. They do not move the 20 px floor — that
  is information-theoretic.

### 5.2 The gate on the art

Every icon is rendered at 20 px beside the marks it shares a family and a screen
with, blurred, and **read by a person** (§2.3 rule 12). An icon enters the set
when a reader can still tell it from its siblings.

**The gate is a judgment, and it has to be.** A mechanical separation score —
counting how much two rasterised marks overlap — was tried and abandoned: it
scores *pixel coincidence*, which has no relation to how a person tells shapes
apart. A filled disc and a disc struck through by a bar overlap heavily and are
instantly distinct, because the bar is a silhouette event; two dissimilar spiky
marks can overlap barely and still read as the same small spiky thing. Drawings
optimised against such a score get better scores and become unreadable.

What a measurement is still good for here is finding **outliers to look at** —
the mark whose weight is far from its neighbours (rule 3), or the group of four
that made the same topology-and-axis choice (rule 8). It nominates suspects; the
eye convicts.

### 5.3 Sourcing

**Commission it.** §2 is the brief. Because the set is a notation rather than a
collection of pictures (§2), what is commissioned is **the primitives and the
operators first** — eight nouns and six verbs, drawn once and drawn well — and
only then the marks that compose from them. Getting the fourteen parts right is
the whole job; the rest is composition, and composition is where the set earns
its consistency.

A 60°-grid silhouette on a hexagonal field is closer to parametric construction
than to freehand drawing, so the parts can be handed to an illustrator or built
as vector geometry directly.

---

## 6. Interfaces

- **`marks-glyphs.md`** — owns the mark budget, the per-band mark size (§4.1
  there, which sets what these drawings get) and the collar (§2.2 there, which
  decides where a family's mark sits on a keystone). Its §3 is the decision that
  makes this document load-bearing: size carries nothing, so the icon carries
  kind.
- **Group 4 (lanes & motion)** — freight, convoys and crawls are yours and take
  no icons here; a **completed** jump gate is a lane terminus, and §4 offers the
  gate icon if the terminus wants one.
- **Group 5 (chrome)** — the legend key is one of the four icon surfaces (§1);
  legend and rail tints come from the shared declarations named in §2.6, never
  from literals.
- **Group 6 (panels & selection)** — panel rows are an icon surface, and above
  Reach the mark is a locator, so the **tooltip is the only thing that can name
  what a mark is** at Realm and Domains. Every quantity the map used to put in a
  mark's size now lives with you: hulls, magnitude, progress and stall, port
  tier.
- **Tier 2 (synthesis)** — this document *is* the icon design. Tier 2's manifest
  is the production checklist derived from it: per entry, the columns of §3 plus
  its atlas cell and its recorded ladder result, tracked through the three build
  tiers of §3.9.
