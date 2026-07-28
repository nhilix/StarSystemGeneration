# The icon set

**The atlas's authored vocabulary: what it must depict, what each mark means in
the world, and the rules every drawing obeys.** This document is the
commissioning brief for the icon set and the reference for anyone reading one.

It rides `docs/design/ui/marks-glyphs.md`, which owns the mark budget, the
collar and the two-tier split (§6 there is the summary; this is the set).
Populations quoted here are measured across the six mature radius-21 seeds and
three degenerate worlds in the Group-3 evidence base — recipes in
`docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` §"Group 3".

---

## 1. What an icon is for

The atlas draws in **two tiers**, and only one of them is art.

| Tier | Floor | Drawn by | Carries |
|---|---|---|---|
| **Form** | 8–10 px | code, no assets | which *kind of place* this is, and which *families* are present here (the collar's six slots) |
| **Icon** | **20 px** | this document | which *kind within a family* — this ruin rather than that one, a gate rather than a shipyard |

**20 px is a measured floor, not a preference.** Every icon in the shipped
placeholder sheet was rendered through `StarGen/AtlasGlyph` at 6, 8, 10, 12, 14,
16, 20, 24, 32 and 48 px: below 12 px everything is a speck; at 14–16 only a
single closed silhouette reads at all; 20 px is where a well-drawn set arrives.
The map draws its marks at 11–20 px, so **icons do not appear on the map at
Reach and above**. They appear on four surfaces that all have the pixels:

| Surface | Size | Role |
|---|---|---|
| **Ground** (the lowest map band) | 20–56 px | the keystone becomes its icon; each lit collar vertex becomes its family's icon |
| **Hover tooltip** | 16–20 px | naming what is under the pointer |
| **Legend key** | 16–20 px | teaching the vocabulary |
| **Panel rows** | 16–24 px | the row's subject, beside its text |

**Consequence for scheduling: the map is complete without any of this.** Realm,
Domains and Reach are the form tier end to end, so the set can be commissioned
and landed **family by family**, each family falling back to its collar pip
until its icons exist. Nothing in the atlas is ever blocked on a drawing.

---

## 2. The design language: hex-cut

The atlas is hexagonal, near-black, additive, and read at a glance from a
distance. The rules follow from that, and they are what make the set *this
project's* rather than a library that happens to be installed.

1. **Hexagonal envelope.** Every icon is cut inside a flat-top hexagon on a
   24-unit grid, in the orientation the lattice draws. The mark sits at a hex
   centre; its picture is hex-shaped. Nothing else on screen is.
2. **The 60° family only.** Edges run at 0°, ±60°, ±120°, plus 90° where an icon
   has a vertical axis of symmetry. No arbitrary angles; no free curves except
   full circles and 60°-centred arcs.
3. **Solid mass. No outline, no line art.** Icons are filled silhouettes.
   Measured requirement: outlines are the first thing to die between 32 px and
   20 px.
4. **One connected mass**, or one mass plus at most one deliberate satellite of
   ≥ 4 units. Floating detail is noise at 20 px.
5. **Minimum feature 2.5 units** (2 px at the floor — what survives a bilinear
   downsample); counter-forms (holes) ≥ 3 units.
6. **Even optical weight:** 34–46% of the envelope inked, so no icon shouts over
   its neighbours in a legend row or on a collar.
7. **Shared optical centre and baseline**, so a row aligns without per-icon
   nudges.
8. **Orientation is meaning, never decoration.** What points, points outward
   from its port or across a lane.
9. **Pure white on transparent, tinted at runtime.** No gradients, no baked
   colour, no shading: the atlas renders linear with a per-instance tint and
   every layer already assumes exactly this.
10. **Passes the ladder at 20 px against its family siblings** before it enters
    the set. The test is a regeneration of the ladder sheet, not an opinion.

Rules 1, 2 and 8 are the ones nothing off the shelf will satisfy. A stock
library can meet 3–7 by accident.

### 2.1 The grammar: a set, not thirty drawings

The icons share a small vocabulary of sub-forms. Composition is what makes the
set learnable — a player who knows six shapes can read an icon they have never
seen — and it is what lets the set grow later without a redesign.

| Sub-form | Means | Appears in |
|---|---|---|
| **solid disc** | a market — a place that trades | starport, market |
| **solid diamond** | a claim without a market | outpost, sterilization scar |
| **hex ring** | an enclosure, a made boundary | gate, blockade |
| **chevron** | motion or rise; it points | port raised, expedition, patrol, fleet postures |
| **bar** | a barrier or a berth — a line that stops or holds | blockade, fleet posted, reserve |
| **seated block** | a built thing, resting on ground | facility, hull batch |
| **shards outward** | deep time — something that radiated and ended | precursor site, origin |
| **bites inward** | damage taken; the whole, reduced | infected, ruin, battlefield |
| **crossed axes** | conflict | battlefield, mobilization |
| **radiating carries** | something propagating from a source | news, AGN outburst |

Three composition rules:

- **A place icon contains its form.** A port's form is a solid disc, so the
  starport icon is a disc *elaborated* — berth arms added — not a different
  shape. An outpost's form is a diamond, so its icon is that diamond with a
  stake. Descending from the pip map to the icon map therefore **adds detail to
  a shape the player already knows** instead of swapping it. This is the same
  continuity the collar gives the badges.
- **An event and its residue share a root.** A battlefield is the crossed axes
  *sunk into the ground*; a ruin is the seated block *bitten*; a sterilization
  scar is the origin diamond *hollowed*. The pair reads as cause and consequence
  without a caption, and it halves the invention.
- **Orientation separates the fleet postures.** They are one family doing
  different things, so they share the chevron and differ by where it points and
  what holds it: *posted* faces outward at its berth, *reserve* faces inward
  against its bar, *patrol* runs the circuit, *expedition* leaves the frame.

### 2.2 Tint

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

Twenty-seven icons. Every entry states what it depicts, what a player learns
from it, the Core query that produces it, and its measured population per mature
world. **Build tier** is A (the shippable core), B (the second pass), or C
(completes the vocabulary), ordered by population × how much a decision leans on
it.

### 3.1 Places — where people are

Source: `PortLens.Markers`, `DomainInteriorMarks.Build(...).Outposts`.

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **starport** | the disc, with berth arms | *trade happens here.* Its tier sets the service radius that draws the territory around it | 178–219 ports (tiers 1–2 only) | **A** |
| **outpost** | the diamond, on a stake | *someone lives here and there is no market.* A frontier holding inside a domain; it can graduate into a port | 12–26 | **A** |
| **market** | the disc, with a balance across it | *this is where the price you are reading comes from.* Panel and tooltip only — at map scale, a market is what a port **is** | one per port | C |

Port **tier is not an icon distinction.** It is carried by the keystone's size
and by the territory the port projects; three tier icons would be three
near-identical discs. Measured: no world contains a port above tier 2.

### 3.2 Works — the in-flight world

Source: `WorksLens.Sites` → `ProjectKind`. These are the largest family the
shipped placeholders never distinguished: all six kinds currently draw one crane.

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **gate laid** | the hex ring, unclosed | *two ports are being connected.* When it completes a lane opens and the network changes shape — the single most consequential thing under construction | 74–136 | **A** |
| **port raised** | mass under a rising chevron | *a new starport is being founded here.* The map is about to gain a market and a service radius | 38–77 | **A** |
| **facility** | a seated block | *industry is being added to an existing domain* — the domain deepens rather than spreads | 23–72 | **A** |
| **hull batch** | a seated block with a chevron leaving it | *ships are being built.* Fleet strength is about to change | 9–17 | B |
| **mobilization** | crossed axes over a seated block | *this polity is arming.* War is being prepared, not yet fought | 3–18 | B |

`OutpostGraduation` (0–1 per world) draws **port raised** — it is a port being
founded, by promotion rather than expedition, and a cell for a once-per-world
event is a reservation, not a vocabulary item.

`ColonyExpedition` is not here: it is a convoy in transit, which leaves the mark
channel entirely for Group 4's strokes (`marks-glyphs.md` §1.1).

### 3.3 Fleets — hulls, and what they are doing

Source: `FleetLens.Markers` → `FleetPosture`. One family, one chevron, separated
by orientation and by what holds it (§2.1).

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **posted** | chevron facing outward from a berth bar | *freight capacity is assigned to a lane here.* This is what makes trade move | 42–61 | **A** |
| **blockade** | the hex ring, barred across | *this port's approaches are held by someone else.* Its lanes are cut | 0–2 | **A** |
| **reserve** | chevron facing inward against its bar | *hulls are docked and decaying in readiness.* Strength on paper, not in the field | 21–28 | B |
| **patrol** | chevron on a 60° circuit | *legality is being enforced in this domain* | 10–19 | B |
| **expedition** | chevron leaving the envelope | *a war fleet, colony convoy or ruin expedition is out.* Something is being attempted at distance | 1–4 | B |

**War does not get its own shape.** `WarLens.Stations` re-reads the same
postures; a war station is a **blockade or expedition icon in the burn tint**.
That is already how the code works, it is one fewer thing to draw, and it says
the true thing: what makes a fleet a war station is not what it is doing but who
it is doing it to.

`Escort` gets no icon: it never appears as a marker posture on any world
measured. Its meaning stays in panel text.

### 3.4 Health — contagion and its memory

Source: `PlagueLens.Marks` → `PortPlagueStatus`.

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **infected** | the disc, three bites taken | *a strain is burning at this port right now.* Its lanes may be quarantined | 0–2 | **A** |
| **immune** | the disc, three bites healing closed | *this port has survived a strain and is protected until the window lapses* | 0–2 | C |

The pair is deliberately the same drawing at two stages — the residue rule
(§2.1) at its most literal, and the only place in the set where two icons differ
by degree rather than by kind.

### 3.5 Deep time — the galaxy before anyone

Source: `PoiLens.Marks` → `PoiType`; `GalaxySkeleton.Origins`;
`PrecursorWave.Sites` where `PrecursorSiteType.SterilizationScar`.

**This is the largest icon family by population and the one the game is most
distinctive about.** The precursor site alone outnumbers ports.

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **precursor site** | three shards radiating from a centre | *deep-time archaeology is here* — exotics, hazard and research, and a claim worth reaching for. Lifts brighter when **dormant**: a live remnant, not an inert ruin | **178–435** | **A** |
| **battlefield** | crossed axes, sunk | *hulls died here.* Salvage, and a grudge with a date on it | 36–48 | B |
| **memorial** | the diamond, upright on a base | *a famine or an atrocity is remembered here.* It shapes stance and culture, not trade | 19–45 | B |
| **ruin** | the seated block, bitten | *a dead city.* Settlement is suppressed here and there is salvage in it | 4–16 | B |
| **sterilization scar** | the origin diamond, hollowed | *life downstream of here was delayed or erased.* The emergence map still carries the shadow | 77–285 | B |
| **origin** | the diamond, filled, with shards | *sapience started here.* Era tints it: current, precursor, or a pre-spaceflight native | 149–181 | C |

`RuinedCapital` gets no icon: it occurs on **none** of the nine artifacts
measured. If a world ever produces one it draws **ruin** until it earns a cell.

### 3.6 Nature — the galaxy's own history

Source: `GalaxySkeleton.Features` → `GalacticFeatureType`. These are **region**
marks: one icon at the feature's centroid with a dotted rim at its extent, and
they are a **Realm and Domains** read (`marks-glyphs.md` §8).

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **globular cluster** | a dense hex of packed dots | *ancient, compact, metal-poor.* Its hexes roll on a different star table | 4–8 | C |
| **AGN outburst** | radiating carries from an off-frame source | *the core fired once.* A sterilization and enrichment wave crossed this ground | 6–7 | C |
| **merger stream** | a 60° trail of decreasing mass | *another galaxy fell in here.* Foreign metallicity and a datable starburst along the trail | 1–3 | C |
| **emission nebula** | a soft-edged mass, open | *gas, and stars forming in it now* | 0–8 | C |
| **supernova remnant** | a broken ring | *massive stars died here recently.* A young graveyard glow | 6 | C |

`DarkCloud` gets no icon: like `RuinedCapital`, it occurs on none of the nine
artifacts. A quiet gas region reads as an **emission nebula** with the nature
field's own colour saying it is cold — the field is already carrying that
distinction, so an icon would be duplicating it.

### 3.7 Word

Source: `NewsLens.Pulses`.

| Icon | Depicts | What it tells the player | Population | Tier |
|---|---|---|---|---|
| **news origin** | a source with six carries | *something happened here that people are still talking about.* Age fades it; the 40-year display window is stated in the legend | 70–96 shown of 448–573 live | **A** |

### 3.8 The build order

**A — the shippable core (10).** starport · outpost · precursor site · gate laid
· port raised · facility · fleet posted · blockade · infected · news origin.

**B — the second pass (9).** reserve · patrol · expedition · hull batch ·
mobilization · battlefield · memorial · ruin · sterilization scar.

**C — completes the vocabulary (8).** market · immune · origin · globular
cluster · AGN outburst · merger stream · emission nebula · supernova remnant.

---

## 4. What is deliberately absent, and why

| Not drawn | Reason |
|---|---|
| `PoiType.RuinedCapital` | zero occurrences across nine artifacts — draws **ruin** |
| `GalacticFeatureType.DarkCloud` | zero occurrences; the nature field already says "cold gas" |
| `FleetPosture.Escort` | never appears as a marker posture |
| `ProjectKind.OutpostGraduation` | 0–1 per world; it *is* a port being raised |
| Port tier variants | carried by keystone size and service radius; no world exceeds tier 2 |
| A "war" shape | war is a tint on a fleet icon (§3.3) |
| A completed jump gate | a built gate **is a lane**; its terminus mark is Group 4's (`camera-nav-lod.md` §9's open seam) — this set supplies the icon if they want one |
| Freight, convoys, crawls | transients: they ride strokes, not places (`marks-glyphs.md` §1.1) |

**The rule underneath all of these: an icon must have a population.** A cell for
a type that never occurs is not vocabulary, it is a reservation, and the shipped
placeholder sheet has three of them.

---

## 5. Producing it

### 5.1 The atlas sheet

`Resources/AtlasGlyphs.png` is 512 × 640 today: 128 px cells, 4 columns × 5
rows, 17 of 20 used. The set needs **44 cells** — 17 legacy plus 27 new.

- **The sheet becomes 4 columns × 12 rows, 512 × 1536** (48 cells; 44 used, 4
  spare). Column count stays at 4 deliberately: `AtlasGlyphs.UvRect` derives
  every rect from `Columns`/`Rows`, so both the constants and the PNG change
  together and the repack is mechanical.
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

Every icon regenerates the ladder sheet and must **separate from its family
siblings at 20 px**. This is mechanical and it has already caught a real
collision: in the eleven marks built to these rules during the Group-3 dive,
**precursor** (three shards outward) and **plague** (three bites inward) are both
three-fold and converge below 16 px. A taste argument would not have found it.

### 5.3 Sourcing

**Commission it.** §2 is the brief, and it is specific enough to hand to an
illustrator or to build as vector geometry directly — a 60°-grid silhouette on a
hex envelope is closer to parametric construction than to freehand drawing. The
eleven demonstration marks in the Group-3 mock artifact were built exactly that
way, in a browser, from polygon lists.

---

## 6. Interfaces

- **`marks-glyphs.md`** — this set is the icon tier of its §6.4 split; the collar
  (§2.2 there) decides *where* each icon appears at Ground, and the form tier
  decides what is drawn above Ground.
- **Group 4 (lanes & motion)** — freight, convoys and crawls are yours and take
  no icons here; a **completed** jump gate is a lane terminus, and §4 offers the
  gate icon if the terminus wants one.
- **Group 5 (chrome)** — the legend key is one of the four icon surfaces (§1);
  legend and rail tints come from the shared declarations named in §2.2, never
  from literals.
- **Group 6 (panels & selection)** — panel rows are an icon surface; the pip map
  stops at family and the pointer resolves the kind, so a tooltip showing the
  icon is what completes the read.
- **Tier 2 (synthesis)** — this document *is* the icon design. Tier 2's manifest
  is the production checklist derived from it: per entry, the columns of §3 plus
  its atlas cell and its recorded ladder result, tracked through the three build
  tiers of §3.8.
