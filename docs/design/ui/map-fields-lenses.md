# Map fields & lenses

**The layer that occupies the atlas's pixels.** This document is the spec for
the four things the map draws underneath its marks — the sky, the nature base,
the territory field and the lattice — for the lens model that decides which of
them speaks, for the compositing rule that lets them coexist, and for the
vocabulary the map uses when a lens has nothing to say.

It rides `docs/design/ui/camera-nav-lod.md`, which owns bands and curves; §9 of
that document names what this group inherits. Evidence for every claim is in
the Group-2 section of `docs/superpowers/plans/2026-07-25-ui-pass-ledger.md`
(regeneration recipes, measured tables). `docs/design/ui/inventory.md` records
the prior behaviour wherever this document departs from it.

---

## 1. The governing idea: one field, one question

Group 1's spine is *altitude asks a question*. The parallel here is narrower and
sharper:

> **A lens answers one question about one subject, and it draws in one channel.
> Channels that carry areas hold one lens at a time; channels that carry marks
> hold as many as you like.**

That single sentence is the whole exclusivity rule, and it is the thing the
shipped atlas never states. Today `{war, tension, tech, currency}` are
radio-exclusive and `{lanes, traffic, trade}` are radio-exclusive with no
explanation offered anywhere, `price` stacks freely on top of the domains it
obliterates, and `tech` sits in the rail's KNOWLEDGE group while being
exclusive with three chips in POLITICAL — so clicking a knowledge chip silently
turns off a political one (`LensRail.cs:148-150`).

None of that is arbitrary once the channel is named. Two area lenses cannot
share a channel because they compete for the same pixels with the same
geometry. Marks can pile up because each one occupies a point.

### 1.1 The four channels

| Channel | Geometry | Carries | Occupancy |
|---|---|---|---|
| **The sky** | additive points over the whole disc | the galaxy's stellar body | always on; not a lens |
| **The base** | continuous per-cell raster | one **nature** question | one at a time, or none |
| **The field** | union of port service areas | one **political-economic** question (the *accent*) | one at a time, or none |
| **Strokes** | lines between ports | one **network** question | one at a time (Group 4) |
| **Marks** | billboards at hexes | any number of **entity/event** questions | additive (Group 3) |

**The rail groups by channel, not by theme.** What a chip does to the map is
the thing a player must be able to predict from where the chip sits. Theme
grouping survives *inside* the marks channel, where it costs nothing because
marks are additive. (The widget is Group 5's; this is the semantics it needs.)

Two consequences worth naming, because both are changes:

- **Price joins the field channel**, so price and the domain accents become
  mutually exclusive (§5). It stops being a separate raster.
- **`Features` and `Emergence` leave the nature group** for the marks channel
  (§7.2). Both are sparse point overlays wearing a field's clothes;
  `NatureLens.CellShade` returns `AtlasPalette.Floor` for every unmarked cell
  under `Features`, which is a mark set drawn as a raster.

---

## 2. The field: territory, and everything shaded onto it

`DomainFieldLayer` + `StarGen/DomainField` is the atlas's densest element and
its best idea: a per-pixel union over a port registry, so union fills, border
outlines and Venn overlaps are all shader emergents of read-model data rather
than drawn polygons. The idea stands. Three things about it do not.

### 2.1 One intensity cannot serve five questions

The shipped field draws every accent at `_FillIntensity 0.13` with
`_BorderIntensity 0.50` — a fill that is *four times fainter than its own
outline*. The result is that the map is a line drawing of overlapping circles,
and any accent that carries its meaning in the fill has ~13% of a channel to
carry it in.

Measured on seed-42 (50 polities), as the maximum pairwise CIE76 ΔE between
rendered slot fills:

| `_FillIntensity` | owner spread | tension spread | tech spread | tension vs tech, mean |
|---|---|---|---|---|
| **0.13** (shipped) | 26.7 | **6.7** | **1.6** | **2.7** |
| 0.30 | 64.7 | 21.4 | 4.1 | 8.2 |
| 0.45 | 91.0 | 30.2 | 5.3 | 11.5 |
| 0.60 | 115.7 | 38.6 | 6.6 | 14.4 |
| 1.00 | 174.4 | 58.8 | **10.2** | 22.0 |

Two readings fall straight out.

**Tension is starved, not broken.** Its ramp has range; the fill throws it away.
At 0.45 the same lens separates by ΔE 30 and the map reads as a pressure gauge
(`sweep-tension-f44-b34.png` against `sweep-tension-f12-b50.png`).

**Tech is broken, and no intensity fixes it.** `TechLens.RampCap = 6` while real
Astrogation tiers span **2 to 3** on every mature seed measured. The lens
therefore renders exactly two fills, ΔE 1.56 apart at the shipped intensity and
still only 10.2 apart at the theoretical maximum of 1.0
(`sweep-tech-f44-b34.png` is a uniform grey wash). A ramp the data does not
span is a ramp that shows nothing.

**So the fill intensity is per-accent, and it is the accent's dynamic range:**

| Accent | Fill | Border | Why |
|---|---|---|---|
| Owner | **0.30** | 0.50 | identity carries in hue; a bright fill buys nothing and costs the sense of space |
| Currency | 0.30 | 0.50 | same channel, same reasoning |
| War | **0.45** lit / **0.15** ash | 0.50 | a two-level fill *is* the encoding: the ones still burning are the ones fighting |
| Tension | **0.45** | 0.30 | a scalar needs luminance range; 50 bright outlines of one colour are noise under it |
| Price | **0.45** | 0.30 | same |

Overlap intensity keeps its meaning — *the contested zone outshines either
claim* — which the shipped values invert the moment the fill rises: at fill 0.45
the shipped overlap of 0.26 draws contested space **darker** than either owner,
visible as cut-outs in `sweep-owner-f44-b34.png`. **Overlap is 1.5× the accent's
fill**, always.

### 2.2 Identity is a palette, not a hash

`AtlasPalette.OwnerColor` walks the golden ratio through hue at fixed S/V. That
is collision-*resistant*: it disperses hues without repeating. It is not
perceptual separation, and on real worlds it fails where it matters.

- Across the 50–63 polities of a mature world the closest pair of rendered
  fills is ΔE **0.28–0.40** — identical to any eye.
- Restricted to polities that are actually **adjacent** (service areas that
  overlap, 38–74 such pairs per seed), the worst pair is ΔE **4.0–10.1**, with
  up to 5 pairs under ΔE 10 and up to 9 under ΔE 20 per seed. The neighbouring
  greens in `seed-42-accent-owner.png` are those pairs.
- There is no colourblind safety anywhere in the derivation, and red/green
  adjacency is everywhere.

With 63 territories, no palette on earth makes all of them distinguishable. The
honest goal is therefore not global distinctness but **local** distinctness, and
that is the classical political-map answer:

> **A fixed palette of 16 perceptually-spaced, CVD-checked hues, allocated to
> polities by greedy graph colouring over the adjacency graph — two polities
> being adjacent when their service areas touch.**

Adjacency alone is a weak constraint and a plain greedy walk exploits that too
well: measured on seed-42's 50 polities (58 adjacent pairs, busiest domain
touching 7), it finishes in **three colours** — legal, and a map of three
repeated hues. So the **two-hop neighbourhood is a second, soft constraint**: a
polity also avoids the hues of its neighbours' neighbours where it can, and ties
go to the least-used hue so the palette spreads instead of collapsing. On
seed-42 that allocation uses **all 16 hues with zero collisions at one hop and
zero at two** — no two domains you can see together share a colour, against a
two-hop neighbourhood that reaches 13 domains at its worst.

Distant polities repeat hues, and that is harmless precisely because you can
never see them near each other. Determinism holds: the adjacency graph is
derived from the port registry in id order, the walk is ordered by two-hop
degree then id, and the allocation is recomputed only when the registry
changes — with a **stickiness rule**, a polity keeps its current hue whenever
that hue is still legal in its neighbourhood, so growth recolours the minimum.

Three things this buys beyond legibility:

1. **It deletes the 32-slot cliff** (§2.3) — the shader's slots become palette
   entries, and 16 is a number no world can exceed.
2. **It makes the merge safe.** The field unions by taking `max` over each
   slot's per-port field. Two polities sharing a slot would merge into one
   shape with no border between them — and the colouring guarantees they are
   never adjacent, so their fields are disjoint and each keeps its own outline.
   The failure mode is designed out rather than tolerated.
3. **It costs less to render.** `fields[MAX_SLOTS]` is a per-pixel array in an
   unrolled loop; 16 entries is half the register pressure of 32 and an eighth
   of what raising the cap to 128 would need.

Identity beyond hue stays where it already is: hover names the domain, selection
rings it, the tooltip states the owner. Hue narrows the field; the pointer
resolves it.

### 2.3 Past the cap: the map stops lying

The shipped behaviour is that any polity past `MaxSlots = 32` folds into the
last slot and inherits its colour (`DomainFieldLayer.cs:155-158`), with a code
comment asserting that "seed-scale galaxies stay well under 32."

They do not. Distinct port-owner polities on the six mature radius-21 seeds:
**63 · 46 · 45 · 50 · 56 · 56**. Rasterizing the shader's own union field twice —
once with the fold, once unlimited — **21.8% to 39.9% of all drawn territory is
attributed to the wrong polity**, on every seed. 44 to 80 of 175–215 ports fold.
`seed-1234-domains-fit-foldflagged.png` flags the folded slot in magenta: it is
not one region, it is a third of the political map scattered across the whole
disc, wearing one identity.

Under §2.2 the cap ceases to be a function of polity count, so the ordinary case
disappears. What remains is the pathological one — a polity with more distinct
neighbours than the palette has hues — and it gets a **stated degradation, never
a silent one**:

- the overflow polity draws its **border at full strength and no fill**, which
  is a form nothing else in the field uses;
- the legend head says so in words (§6);
- hover and selection are unaffected, because they never depended on hue.

**The rule this generalizes to: when the map cannot represent something, it
draws that it cannot.** An unrepresentable claim is drawn as an outline with
nothing inside it — a shape the eye reads as "known, unnamed".

### 2.4 The interior is texture on the field, not a mark layer

Worked hexes are currently a separate billboard layer (`DomainInteriorLayer`,
owner hue at alpha 0.55 with a 4.5 px floor) whose whole purpose is to stop a
domain reading as a uniform glow. It carries `MapFade` but not `GlyphFade`, so
at Realm it is thousands of sub-pixel dots that cannot resolve and cannot be
culled by the mark budget Group 1 requires.

**Worked density modulates the field's fill instead.** A low-resolution coverage
raster — the same shape as the price bake it replaces — multiplies into
`_FillIntensity` inside the union, so a domain's worked skeleton appears as
*variation in its own colour* rather than as a competing dot cloud. The
intended read survives, one additive layer leaves the stack, and mark count at
altitude falls without a cull.

**Outposts stay marks**, because they are selectable, named subjects. Their
encoding requirement goes to Group 3 and is stated, not designed, here: an
outpost must not read as a smaller port dot. Size and lightness are already
spent (`PortLayer` runs `(2 + 1.4·tier)·2` px against the outpost's 5.5), so the
distinction has to be **form**.

---

## 3. The compositing budget

Nothing in the shipped atlas states who wins. `LensStack.Composite` exists
Core-side, documented as *"the blend the user eyeballs is the blend the tests
pin"* — and **has no caller anywhere in the atlas**; only its own xUnit test.
All real compositing is GPU draw order plus per-material blend mode, decided by
z constants scattered across a dozen files.

Measured draw order (the camera sits at −z, so larger +z is farther):

```
nature +0.10  →  domain field +0.05  →  price +0.02  →  STARFIELD 0.00
              →  lattice −0.02  →  crawls/trails/lanes  →  marks (−0.11 … −0.25)
```

Two facts fall out that nobody had written down. The **starfield draws in front
of all three field rasters** — stars are visible over the price blocks in
`seed-42-stack-3-price.png` — so the layer with no LOD response at all sits on
top of every informational raster. And the **nature field, the best image the
atlas produces, is behind everything**: at `extent × 0.30` with the domain and
price fields on, it contributes nothing you can see.

### 3.1 The rule

> **The map composites in four planes, back to front: the base, the field, the
> sky, the structure. Exactly one lens occupies each of the first two. Only the
> base replaces light; everything above it adds.**

| Plane | Layer | Blend | Ceiling |
|---|---|---|---|
| 1 · base | nature raster | alpha over the void | peak alpha **0.45** |
| 2 · field | territory accent | additive | fill ≤ 0.45, border ≤ 0.50, overlap ≤ 1.5× fill |
| 3 · sky | starfield | additive | attenuated per §4 |
| 4 · structure | lattice, strokes, marks | additive / masked | lattice ≤ 0.12 |

### 3.2 Which combinations are illegal

**None.** That is the point of the rule, and it is what removing price as a
raster buys.

With exactly one alpha-replacing layer, and that layer at the bottom, every
layer above it commutes: additive light cannot hide what is beneath it, so no
selection a player can make can produce an unreadable frame. The illegal-
combination question dissolves rather than being answered by a table nobody
would remember.

The shipped atlas has the opposite property, and `seed-42-stack-price-over-
domains.png` is what it looks like: the price raster blends at alpha up to
**0.94** *in front of* the domain field, and the political read is simply gone.

The remaining budget is total emitted light, and it has one target: **a map
pixel never exceeds ~0.55 luminance**, so that the marks' contrast chip — a dark
backing disc at alpha 195, `GlyphLayer.cs:106-108` — always wins over whatever
is under it. Worst case under the ceilings above (rich nature base + lit
territory + dense starfield + lattice) lands under that with room to spare,
because the two brightest contributors are mutually exclusive by geography: the
starfield is brightest where there is no territory (§4), and the field only
draws over serviced space, which is 13–17% of the disc.

---

## 4. The sky

The starfield is 31,000 additive soft-dot billboards on every radius-21 world,
placed and brightened by deterministic hashes off the density raster, with **no
LOD response of any kind**. On `epoch 42 2 21` that is 15,483 stars per port,
and `degen-seed-42-domains-fit.png` is the consequence: two ports, invisible,
inside a beautiful dense disc.

The disc is genuinely beautiful and genuinely informative — arms, bulge and halo
emerge from density alone — so the answer is not less starfield. It is a
starfield that knows what is on top of it. **Two attenuations multiply into the
per-star alpha:**

**Altitude** (Group 1's requirement, mechanism here): `0.35` at Realm and
Domains, ramping to `1.0` by Reach, on the same curve family as the other
fades — and **never applied during the crossfade**, because the starfield is the
one element continuous across the whole descent and that continuity is what
makes the descent read as descent.

**Content**: a star's alpha is multiplied by `1 − 0.5 × serviceCoverage` of its
own cell. Stars inside a domain dim; stars in the wilds do not. This is cheap —
`StarPoint` already carries `CellIndex`, and coverage is a per-cell number the
domain lens can hand over at load, so the cost is O(cells), not O(stars × ports)
— and it is the right story as well as the right legibility: settled space is
where you read, and the dark between is where the sparkle lives.

Tint by `StellarLean` stays exactly as it is. It is the only place the map says
what *kind* of stars these are, it costs nothing, and it survives every
attenuation because it is a hue, not a level.

---

## 5. Price: one truth, one geometry

The price field is the loudest element in the atlas, and three separate things
make it so.

**It is not a raster; it is a Voronoi.** `PriceLens.RatioAt` returns the *nearest
servicing port's* market price, so every hex in one port's service area carries
one identical value. The 256² texture bakes that per-port constant, quantized
through `HexGrid.CellOf` on the way out — which is where the hard hex blocks
come from. At `f = 0.10` the field is a single flat blue plane across 70% of the
frame (`seed-42-lattice-stack.png`). A continuous-looking raster is drawn over
data that has no continuity in it, at a granularity that belongs to neither the
port nor the cell.

**Its palette reads categorical for a scalar question.** Seven bands walk most
of the hue circle — deep blue, teal, olive, amber, orange, red, hot pink — so
"where is this dear" arrives as unordered identity colour. Nothing in the image
says which way is up.

**Its anchor has been left behind by the sim.** The bands are ratios against
*founding* price. Measured over serviced cells, seed-42: famine 86 · glut 63 ·
par 39 · cheap 25 · dear 18 · spike 17 · scarce 10 — and **famine is the largest
band on all six mature seeds** (73–139 cells), with par at 4–15%. Famine draws
hot pink at alpha 240 and glut deep blue at 190; par, the deliberately quiet
one, is where almost nothing lands. The lens is a picture of the sim's nominal
price level (the subject of the parked Slice PL) rather than of geography.

### 5.1 The re-encoding

> **Price is an accent on the territory field, evaluated per port, on a single
> diverging ramp anchored at the galaxy's live median for that good.**

- **Geometry**: the field shader already computes a per-port field before taking
  the per-slot max. The price accent takes the max over *ports* instead of
  polities and colours each port's service area by its own market. One idiom
  replaces two, the granularity becomes the truth's granularity, the wilds stay
  clear for free, and the border/overlap machinery composes unchanged.
- **Ramp**: one hue family, diverging about a neutral midpoint — cheap below,
  dear above. **Par is unfilled**, so a healthy economy is quiet by construction
  rather than by a small alpha that the loud bands ignore.
- **Anchor**: the median live price for that good across all markets, at this
  moment. This is the question the lens claims to ask — *dear compared to
  where?* — and it is stable against the price level, which the founding anchor
  is not. The ramp's ends sit at fixed multiples of the median (÷4 and ×4),
  quantized so a scrub does not shimmer, and **the legend prints both the
  multiplier and the absolute median**, so the player never loses the number.
- **REPL parity is preserved, not broken.** `PriceLens` exposes both anchors;
  `emap`'s `PriceGlyph` keeps the absolute bands it has always had, the map
  defaults to relative, and each surface names its anchor. The contract was
  ever "both read the same query", and it still holds.

Being an accent, price is now exclusive with the other accents. That is a real
loss of a combination the player has today — but the combination it removes is
the one that was destroying the political read anyway, and what replaces it is
strictly more informative: price *drawn as territory* tells you whose market is
dear, which price-as-a-separate-raster never did.

---

## 6. The map's empty states

This is the sharpest gap the inventory found and the highest-value thing this
group leaves behind. The panels carry specific, voiced empty states throughout —
*"(a tidied museum — nothing is in motion; this should worry you more than a
war)"*. **The map has none at all.** No lens says "no wars", "no trade", "no
plague". `degen-seed-7-accent-war.png` is two ash outlines in a starfield: an
empty world and a broken lens are the same picture.

### 6.1 Three states, and the difference that matters

| State | Meaning | How the map says it |
|---|---|---|
| **Speaking** | the lens has values | the field draws |
| **Silent** | the lens is live; the answer is *nothing* | the covered area draws the **floor tone**, and the legend head says so in words |
| **Blind** | the lens *cannot* answer here — data absent, not zero | the covered area draws nothing, and the legend head says why |

**Silent versus blind is the distinction the whole vocabulary turns on.** "No
wars" is a fact about the world. "No flow trails until a step runs"
(`FlowTrailLayer.cs:14-17` — correct behaviour that reads as a broken lens) is a
fact about the instrument. The atlas today has both and shows neither.

The floor tone is `AtlasPalette.Floor` (24,26,32) over exactly the area the lens
covers — visible, uniform, unmistakably *drawn*. The peaceful world already
paints its domains ash by accident; this promotes that accident to the rule and
gives it a voice.

### 6.2 The carrier: the legend head

One mechanism, for the whole map, drift-proof the way the legend already is:
`LegendQuery.For(key, …)` returns a **head** alongside its entries — the lens's
name, its state, and one voiced sentence — from the same Core query that
produces the swatches. When the state is Silent or Blind the legend renders the
head alone. Groups 3 and 4 inherit this unchanged: a mark family or a stroke set
with nothing to place draws nothing, so the head is the *only* carrier they
have, and it already exists.

Two interfaces this places on Group 5: the legend must render a head, and a rail
chip whose lens is **silent** must not look identical to a chip that is merely
**off**.

### 6.3 The vocabulary

The register is the panels': specific, a little wry, never "No data". Each line
below is a state that exists in a world we have measured.

| Lens | State | The line |
|---|---|---|
| war | silent | *nobody is fighting. Every border here is a border by agreement.* |
| war | blind | *too young for enemies — no polity has met another.* |
| tension | silent | *no live pressure anywhere; the gauge is cold.* |
| currency | silent | *every polity mints its own; no two share a currency yet.* |
| tech | silent | *one rung for everyone — no polity has out-sailed another.* |
| price | blind (the wilds) | *no market services this space.* |
| price | blind (good untraded) | *nobody trades this here; there is no price to compare.* |
| trade | silent | *nothing is moving between these ports.* |
| traffic | silent | *the lanes are open and empty.* |
| plague | silent | *clean lanes — no strain in the reach.* |
| works | silent | *nothing under construction, nothing in freight.* |
| works | blind | *the trail begins at the next step — nothing has moved since this world was loaded.* |
| news | silent | *no word in the last forty years.* |
| pois | silent | *nothing has happened here yet worth remembering.* |
| fleets | silent | *no hulls posted anywhere in view.* |
| nature: bio | silent | *nothing living, anywhere in this galaxy.* |
| nature: features | blind | *this galaxy grew no features of that kind.* |
| domains | silent | *nobody has claimed anything. All of this is the wilds.* |

**Currency's silent line is not hypothetical.** Distinct currencies equals
distinct port-owner polities on all nine artifacts measured — zero
consolidations anywhere — so the lens's entire subject is absent on every world
we have, and nothing says so. It is the clearest case in the atlas of a lens
that looks broken because it is honest.

### 6.4 The legend's other debts, closed here

- **The war legend drifts from the war layer.** `LegendQuery` advertises
  `DomainLens.WarShade` (225,70,60) for a belligerent domain and
  `AtlasPalette.Floor` for a peaceful one; `DomainFieldLayer` draws
  `AtlasPalette.OwnerColor(slot)` and (58,62,72). `LegendDriftTests` checks
  glyph-key *names* and non-emptiness only, never colour parity. **The legend
  states the encoding the layer draws** — belligerents in their own hue at the
  lit fill, the peaceful in ash — and the drift test grows a colour arm.
- **The nature legend cannot say which layer it is.** `ActiveLegendKey` returns
  the bare string `"nature"`, so `LegendQuery` gets no layer to key on and every
  nature lens shares one generic "low / high — the raster's floor / peak" card,
  while all nine rail chips share one swatch (`0x5A6E9E`). The key becomes
  `nature:<layer>`, the legend states that layer's own quantity and endpoints,
  and **the chip's swatch is its layer's base hue** — which already exists per
  layer in `NatureLens` and is simply never surfaced.
- **The `ports` legend is unreachable**: `ActiveLegendKey` never returns
  `"ports"`, so an always-on element has no legend on any lens. Ports are not a
  lens — they are the base map — so their entry belongs in the **head of
  whatever legend is showing**, not in a card that must win a priority chain.

---

## 7. The base, and the grid

### 7.1 Nature reads at Realm — the one amendment this group asks for

`camera-nav-lod.md` §2 places the nature rasters **off at Realm and Domains, on
at Reach and Ground**, and has them lead the crossfade out. The evidence says
the first half of that is backwards.

`sweep-nature-fit-gas.png` — the gas layer at disc fit — is a full nebular
spiral, arms and voids and all, and is the most informative single image the
atlas produces. The identical layer at `extent × 0.30`
(`seed-42-stack-1-nature.png`) is a flat blue-grey wash: at Reach framing one
cell is most of the frame, and a per-cell field has nothing left to vary.

That is not a tuning failure; it is what a galaxy-scale quantity looks like when
you stand too close. **Nature is a Realm and Domains read.** Its alpha runs the
*inverse* of the territory and price curves: full at Realm, falling through
Reach, near-nothing at Ground, where the gas fraction of a cell is context for a
system rather than the subject.

The rest of §2's row stands unchanged: nature still leads the crossfade out,
because by then it is already almost gone.

**This is an amendment to `camera-nav-lod.md` §2, flagged to the user rather
than diverged from silently.**

### 7.2 Two nature layers are not fields

`NatureLayer.Features` returns `AtlasPalette.Floor` for every cell that is not
one of a handful of overlay marks, and `Emergence` returns a flat 0.35 bio ramp
plus origin and sterilization-scar marks. Both are sparse point sets wearing a
raster's clothes; drawn as fields they are a uniform floor with a few coloured
cells in it, and their legends can say nothing useful because there is no ramp
to describe.

**They move to the marks channel** — where they become additive, can coexist
with a real nature field underneath, and get legends that name the *kinds* of
thing they mark. Their glyph encoding is Group 3's. The nature group is then
seven honest scalar fields: Density, Lean, Gas, Metal, Age, Minerals, Bio.

### 7.3 The lattice is a spotlight, not a wallpaper

The lattice is the full-galaxy hex mesh — 881,790 vertices at radius 21, built
in one 30.1 ms frame (Group 1's measurement) — drawn at a uniform alpha capped
at 0.12. Group 1 moved the build to load time and made the lattice the last
thing out of the crossfade, the frame the systems appear inside. Both stand.

What remains is that a uniform grid across the whole frame is texture, not a
locating aid: it tells you the world is hexagonal, which you knew, and it does
so equally hard everywhere.

**Alpha falls off with distance from the camera's focus** — full within roughly
six hexes, zero by eighteen. The grid is then a soft disc of scale under the
place you are actually looking, which is what a locating grid is for, and the
frame's periphery stays black space. Nothing else about the layer changes: same
mesh, same cap, same fade curve, one radial term in the vertex colour.

---

## 8. The colour-authority bridge

The chrome honours the token system thoroughly — `AtlasChrome.uss` uses
`var(--…)` 120 times against exactly one literal, and that literal is a
transparent. **The map does not participate at all**: every map colour is a C#
constant in a layer or a Core lens. Two halves of one product, two authorities,
no bridge.

Two properties are load-bearing and a bridge that breaks either is not a bridge.
`AtlasPalette` is deliberately engine-free so every palette decision is
xUnit-coverable. Every layer CPU-linearizes before upload, because the project
renders linear and tints are authored sRGB — the recorded failure being
*"#262C3F rings came out lavender"*.

**The map joins for neutrals and for lens keys; it does not join for ramps.**

- **Neutrals join.** `AtlasPalette.Void` (10,10,14) and `Floor` (24,26,32) and
  the lattice's line colour are the map's darkness; `--ssg-shell` (#04070D) and
  `--ssg-ground` (#060A12) are the chrome's. They are near-identical by
  coincidence and tied by nothing. One declaration, both consumers.
- **Lens keys join.** The rail's chip swatches are hardcoded hex literals
  (`0x46B5A4` for domains, `0xE05555` for war, …) that duplicate colours the
  legend already derives from Core. One authority, and the rail stops being a
  place where the map's vocabulary can drift.
- **The identity palette joins**, as a token set — the 16 hues of §2.2 are named
  values, not a function.
- **Ramps do not join.** `TensionLens.HeatColor(0.7)` is a function of a value.
  A token system has no way to express that and no reason to try.

**Direction of authority: Core declares, the theme is generated.** A small
editor tool emits `SSGPalette-Ice.uss` from the Core constants, and a test
asserts that regenerating produces no diff — the same drift-proof-by-
construction shape `LegendQuery` already uses for the legend. Both properties
survive: the constants stay engine-free C# (a token is a named `Rgba`), and the
linearize step is untouched, because tokens are authored sRGB exactly like
everything else already is.

What this deliberately does **not** do is make the map's colours themeable at
runtime. A Phosphor preset can restyle the chrome; it should not restyle a data
encoding, because the legend and the map would then agree with each other while
both disagreeing with every screenshot, every doc and every player's memory.
**Affordances are themeable; data is not.**

---

## 9. Empty and degenerate states

| Situation | Behaviour |
|---|---|
| A lens with no values | Floor tone over its covered area + a voiced legend head (§6) |
| A lens that cannot answer | Nothing drawn + a legend head that says why (§6) |
| No polity has claimed anything | The domains lens is *silent*, not blank |
| Two ports in a radius-21 field | Starfield content-attenuation is inert (nothing is serviced), so altitude attenuation alone carries it; Group 1's content framing does the rest |
| A polity with more neighbours than the palette has hues | Border at full strength, no fill, and the legend says so (§2.3) |
| A good nobody trades | Price is *blind*, not zero-valued |
| A retired currency | Already correct — the zone leaves the mode; the legend head names it |
| A galaxy with no features of a kind | `Features` is a mark lens and is *blind* |

---

## 10. Interfaces other groups depend on

- **Group 1 (camera & LOD)** — one **amendment** requested: the nature rasters
  read at Realm and Domains and *fall* through Reach (§7.1), inverting §2's
  price/nature row for nature only. Price keeps §2's row exactly. The starfield
  attenuation §2 asks for is specified in §4 and honours the
  never-during-crossfade rule.
- **Group 3 (marks & glyphs)** — worked dust leaves the mark budget entirely
  (§2.4); **an outpost must be distinguished from a port by form, not by size or
  lightness**, both of which are spent; `Features` and `Emergence` arrive as two
  new mark lenses needing a glyph vocabulary (§7.2); every mark family needs a
  Silent and a Blind line in the §6 vocabulary.
- **Group 4 (lanes & motion)** — strokes are the third exclusive channel (§1.1)
  and the same one-lens rule applies; every stroke lens needs its §6 lines, and
  `QuarantineOnly` — which today hides the network with no explanation — is a
  Silent state, not a hidden layer.
- **Group 5 (chrome)** — the rail **groups by channel, not by theme** (§1.1);
  the legend renders a **head** carrying lens name, state and voiced sentence
  (§6.2); a chip whose lens is silent must not look like a chip that is off; the
  nature chips carry their layer's base hue and the legend keys on
  `nature:<layer>` (§6.4); the price chip's good selector stays, and its legend
  prints the anchor (§5.1); chip swatch colours come from the shared
  declaration, not from literals (§8).
- **Group 6 (panels & selection)** — hue narrows a territory, the pointer
  resolves it (§2.2): hover, selection ring and tooltip are how a player tells
  two same-hued distant polities apart, so they are load-bearing rather than
  convenience.
