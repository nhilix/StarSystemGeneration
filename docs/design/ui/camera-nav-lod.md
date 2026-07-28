# Camera, navigation & the LOD spine

**The atlas's spine.** Every other element's "is this readable?" and "when does
this resolve?" is answered here, in bands and curves. This document is the
spec for the camera rig, the navigation verb set, the band × layer matrix, the
map→orbit transition, and the per-frame budget those imply.

Evidence for every claim is in the Group-1 section of
`docs/superpowers/plans/2026-07-25-ui-pass-ledger.md` (regeneration recipes,
measured tables). Where this document departs from what the atlas does today,
`docs/design/ui/inventory.md` records the prior behaviour.

---

## 1. The governing idea: altitude asks a question

The zoom continuum is not a scale slider. Each altitude asks a **different
question about the world**, and what resolves there is whatever answers it.

| Band | Enters at | The player's question | What the map is |
|---|---|---|---|
| **Realm** | `f ≥ 1.10` | *Who holds what, and where is anything happening?* | generalized territory, the trunk network, live drama |
| **Domains** | `f ≥ 0.45` | *Whose is this, and how far does it reach?* | per-port territory, the whole lane network |
| **Reach** | `f ≥ 0.14` | *How does this place work?* | ports, glyphs, interior marks — the working altitude |
| **Ground** | below Reach | *What is actually here?* | the lattice, then the orbit stage |

`f = cameraDistance / galaxyExtent`. Four bands, not five: the old **Hex** band
is deleted because nothing resolved in it. It spanned 10.8 scroll notches — 39%
of the whole zoom range — and its bottom 5.4 sat below the last curve and above
the crossfade, magnifying a few empty hexes.

**A band is a plateau; its floor is a transition.** Plateaus are correct — the
player needs stable ground to read from. What is not correct is a plateau so
long the player stops believing zoom does anything. The honest unit is the
**scroll notch** (a fixed ×1.25 in distance); a radius-21 galaxy spans 27.5 of
them, and the two longest stretches in which *no curve moves* — 4.4 notches
above `f = 1.10` and 5.4 between the lattice completing and the crossfade
starting — take **36% of the whole range** between them.

The design's target is the **longest such stretch**, which falls from 5.4
notches to 2.3.

**Nothing pops.** Every resolve step is a fade. A band boundary is where a fade
*completes*, never where something switches on.

### 1.1 Scale calibration

`galaxyExtent = 48 + 16.5 × galaxyRadiusCells` exactly — the disc's cell-centre
half-span plus `AtlasGeometry`'s 48-unit pad. Radius 21 → 394.5; radius 5 →
130.5. Every relative threshold is a fraction of this one number.

Bands above Ground are **extent-relative**, so the same gesture reaches the
same altitude on any galaxy. Ground is **absolute** — a hex is a fixed √3 world
units regardless of galaxy size, and the orbit view is a fact about hexes.
That seam is correct and deliberate; its consequence is that the Ground band's
*length* varies with galaxy size (10.8 notches at radius 21, 5.8 at radius 5),
which §4 addresses.

Absolute thresholds are expressed as **hex-fractions of the frame**, never as
world units. At 50° FOV the frame is `0.9326 × d` world units tall, so a hex
occupies `1.857 / d` of the frame height. "The stage owns the frame when a hex
is a quarter of it" is a legible rule; `SystemFloorAbs = 5.0` is a magic number
that means the same thing and says nothing.

> `LodBands.SystemFloor`'s `Math.Min(5.0, 0.14 × extent × 0.6)` guard against a
> toy galaxy is **dead code**: the relative term only wins below extent 59.5,
> i.e. radius < 0.7. `LodBandsTests.ATinyGalaxyKeepsItsHexBand` pins it at
> extent 30 — a galaxy that cannot be generated. The guard goes; the hex-fraction
> form makes it unnecessary.

---

## 2. The band × layer matrix

Read down a column for what a band shows; read across for how an element
behaves over the continuum.

| Layer | Realm | Domains | Reach | Ground | Crossfade |
|---|---|---|---|---|---|
| Starfield | attenuated | attenuated | full | full | **persists** |
| Territory fill (domain field + accents) | **generalized** | per-port | per-port | per-port | fades **first** |
| Price raster | off | off | on | on | fades **first** |
| Nature raster | **full** | full | falling | near-nothing | fades **first** |
| Lanes | **trunk only** | full network | full network | full | fades **late** |
| Flow trails / crawls | off | on | on | on | fades late |
| Ports | **top tier on this world** | ≥ top − 1 | all | all | **hands over** |
| Outposts | off | **on** | on | on | fades late |
| Worked dust | *(left the mark channel — Group 2 §2.4)* | | | | |
| Fleet / works / plague glyphs | off | off | **resolve** | on | fades late |
| POI glyphs | **top percentile** | top decile | **resolve** | all | fades late |
| War stations, news pulses | **on** | on | on | on | fades late |
| Lattice | off | off | resolves | full | **fades last** |
| Orbit stage | off | off | off | resolves | **fades up** |
| Selection highlight | on | on | on | on | **never fades** |

Four things in that table are changes from the shipped atlas, and each answers
a measured failure.

**Territory generalizes at Realm.** A radius-21 world at maximum altitude draws
218 individual service-radius blobs, and they read as confetti — the political
map is present but unreadable, because a service radius is small against a
394-unit disc and the per-port unions never merge. At Realm the field's
smoothing radius scales with altitude so a polity's scattered holdings resolve
into **one territory shape**. (Implementation is the domain field's shader —
Group 2 owns the mechanism; this document owns the requirement: at Realm,
territory is per-polity, not per-port.)

**Ports filter by tier, they do not shrink.** The dual-sizing pixel floor means
a mark never falls below a few pixels, so at altitude 218 ports occupy 218
irreducible dots. Culling by tier is the only way altitude reduces mark count.
Which tiers, and whether the cull is a fade or a merge, is Group 3's; the
requirement is that mark *count* falls as altitude rises.

**The mark rows are weight floors, and the tiers are relative**
*(amended 2026-07-27 by Group 3, on evidence)*. This document originally wrote
the ports row as absolute tiers — "tier 3+ at Realm, tier 2+ at Domains" — and
the glyph rows as on/off switches. **No artifact contains a port above tier 2**
(t1 = 97–128, t2 = 75–98 across the six mature seeds), so the absolute rule
draws *zero* ports at the band whose question is *who holds what*. An absolute
tier cannot be right for a sim whose tier ceiling is a function of its own
economy. So a band sets a **weight floor** per family and a mark is admitted
when its weight clears it — ports against the world's own top tier, POIs against
the world's own magnitude quantiles — **and a place is admitted whenever
anything happening there is**, which is how the Realm band comes to show the
important places *and* the eventful ones. Outposts move up a band with the same
reasoning: there are only 12–26 per world and the frontier is the literal answer
to the Domains question. Details in `docs/design/ui/marks-glyphs.md` §2.3 and
§12.

**War and news resolve at Realm, not at Reach.** "Where is anything happening"
is the Realm question, and today every glyph family waits until `f = 0.63` to
begin appearing and `f = 0.315` to complete — so at galaxy altitude the one
thing the band exists to show is invisible. War stations and news pulses are the
exception to the glyph curve. Everything else (fleets, POIs, works, plague) is
correctly a Reach-band concern.

**Nature runs the other way** *(amended 2026-07-27 by Group 2, on evidence)*.
This document originally grouped price and nature into one row, off at Realm and
Domains and on at Reach. That is right for price and backwards for nature: a
per-cell galactic field has nothing left to vary once one cell is most of the
frame. The gas layer at disc fit is a full nebular spiral; the same layer at
`extent × 0.30` is a flat blue-grey wash. So nature is a **Realm and Domains**
read whose alpha *falls* as the camera descends, reaching near-nothing at
Ground, where the gas fraction of a cell is context for a system rather than the
subject. It still leads the crossfade out, because by then it is almost gone.
Details in `docs/design/ui/map-fields-lenses.md` §7.1.

**The starfield gets a curve.** It is the only layer with no LOD response at
all, and on a sparse world it buries the content it sits behind: `epoch 42 2 21`
at Realm is two ports invisible inside a dense bright disc. At Realm and Domains
the starfield attenuates so the political read wins; at Reach and below it
returns to full, where it is context rather than competition. It **never**
attenuates during the crossfade — space is still space under the orbit view, and
the starfield is the single element continuous across the whole transition,
which is what makes the transition read as *descent* rather than as a scene
change.

---

## 3. The curves

Five continuous curves drive styling. They are the whole of the atlas's LOD
behaviour — bands classify, curves render.

| Curve | Drives | Window |
|---|---|---|
| `RasterFade` | territory fill, price, nature | crossfade: leads |
| `StrokeFade` | lanes, trails, crawls | `f` 1.10 → 0.45 for the trunk→full ramp; crossfade: trails |
| `GlyphFade` | fleets, POIs, works, plague | `f` 0.63 → 0.315 |
| `LatticeAlpha` | the lattice, 0 → 0.12 | `f` 0.224 → 0.084; crossfade: last out |
| `StageFade` | the orbit stage | the crossfade window, inverted |

Each is monotone in altitude, `C¹` at its endpoints, and clamped to [0,1]. No
curve reads any state but `(distance, extent)` — they stay pure and
EditMode-testable, which is why the spine has held up.

### 3.1 Hysteresis

`BandFor` is a bare threshold. Nothing gated on `Band` chatters today for the
simple reason that **nothing is gated on `Band`** — `CameraRig.Band` and
`BandChanged` have no consumers anywhere in the project. This design gives them
consumers (the resolve steps in §2, the altitude indicator in §6), so the
threshold acquires a **deadband** at the same moment.

`BandFor` becomes stateful in the rig: a band is entered at its floor and left
at `floor × 1.08`. 8% is chosen against the input, not the output — one scroll
notch is a 25% distance change, so an 8% deadband can never make a notch fail to
cross a boundary, while it absorbs any resting jitter completely.

The continuous curves take no hysteresis. They are already smooth, and a
deadband on a fade would be visible as a stall.

---

## 4. The signature transition

The whole map dissolving into the orbit view is the atlas's largest motion
moment. It is a **staged handoff**, not a crossfade.

Today it is one multiplier: `MapFade` multiplies into every map layer, so
everything dies together over a window from `d = 10` to `d = 5` — 3.1 scroll
notches at the very bottom of a 27.5-notch range, with 5.4 notches of dead
plateau immediately above it. The result is that the biggest thing the camera
does gets almost none of the camera's range, and arrives with no preparation.

**The order is the design:**

1. **Rasters go first.** Territory fill, price and nature would otherwise sit
   *behind* the orbit rings and muddy them. They are gone before the first ring
   is legible.
2. **Strokes trail.** A lane entering a system is context for what you are
   descending into; lanes hold most of their strength until the rings are
   established.
3. **Ports hand over rather than fade.** A port's dot is, in the orbit view, the
   owner-coloured ring around its body. Between the two it stays lit — the same
   subject, continuously present, changing representation. This is the moment
   that makes the descent read as *the same place seen closer* rather than as
   two views swapped.
4. **The lattice is last out.** `SystemStage` is deliberately coplanar with the
   lattice (`StageZ = −0.02`, draw order by `renderQueue` so nothing
   parallaxes). The lattice is therefore the frame the systems appear *inside*,
   and it should still be there when they do — it is what carries the eye from
   map space into system space.
5. **The starfield never moves**, and **the selection highlight never fades**.
   Both already true; both load-bearing. If you descend with a port selected,
   the ring is on it the whole way down.

### 4.1 The hole the handover leaves: lanes have no system-scale form

The port handover in step 3 works because **a port lives *in* a hex** — it has an
in-system anchor to become. A **lane lives *between* hexes**, and the orbit view
has no representation of one at all. So at the bottom of the descent the player
loses, with nothing replacing it:

- which lanes touch this system, and in what directions;
- whether this is a hub or a dead end — the single clearest thing the map says
  about a place;
- lane *state* that was legible one moment earlier: contested, quarantined,
  carrying trade.

This is the handover's weakest seam, and it is structural rather than a tuning
problem: no fade order can hand over something that has no destination form.

**The requirement this design places on the system view:** a lane must terminate
in a **system-scale mark at the system's rim, on the lane's true bearing**,
carrying the lane's mode colour — so the stroke the player was following
shortens into a terminus rather than evaporating. That mark is what `LaneFade`
hands over to, exactly as the port dot hands over to its body's ring.

The encoding itself — what a gate or terminus looks like, whether it also
carries direction and volume, how several lanes on close bearings resolve —
belongs to **Group 4** (strokes) working with the fuller system rendering, not
to this dive. It is recorded here because the handover is specified here and is
incomplete without it. Until that mark exists, the descent's last beat is a
known information loss, accepted deliberately rather than overlooked.

### 4.2 The window, and the cost that bounds it

The window wants to be wider than it is. It cannot be, yet, and the reason is
measured rather than aesthetic.

`SystemStage` rebuilds **every visible system whenever the visible hex set
changes** — a full rebuild keyed on an FNV hash of the set. Cost is linear at
**0.12 ms per hex**: 37 hexes → 4.5 ms, 169 hexes → 21.7 ms. Mid-crossfade at
pitch 62 the frustum rect already covers 66 hexes (~8 ms), and any camera motion
changes the set, so that cost recurs **every frame of the gesture**. The
`MaxVisibleHexes = 160` cap (≈19 ms — a dropped frame on its own) already binds
at pitch 62 / d = 13, just above the current window.

So: **the crossfade window's width is a function of the stage's build model.**

- **Today, unchanged:** the window stays at its current width (a factor of 2 in
  distance, 3.1 notches), because widening it multiplies a per-frame cost that
  is already at half the frame budget.
- **Once the stage builds incrementally** — a per-hex cache, adding entering
  hexes and dropping leaving ones instead of rebuilding the set — the window
  opens to a factor of 3 (5.4 notches), running from a hex at 1/12 of the frame
  height down to a hex at 1/4. That consumes the bottom half of the Ground
  plateau and gives the transition room to stage itself.

The incremental stage build is therefore a **prerequisite of the widened
window**, and the two land together or not at all.

Two further constraints on the stage, both from the same measurements:

- **The cap must truncate by distance from focus.** `ComputeVisibleHexes`
  iterates `q` then `r` ascending and `return`s on hitting the cap, so a bound
  set is a wedge on one side of the frame rather than the systems nearest the
  player. Order by distance from focus before truncating.
- **Low pitch degenerates the visible rect.** At the 25° floor the frustum's top
  edge is parallel to the plane, so the top corners never intersect it and the
  rect collapses to the near band — during the crossfade at low pitch, systems
  visibly on screen are not built. The rect must be derived from the *rendered*
  horizon distance, not from four corner intersections that can fail.

---

## 5. Navigation

### 5.1 Two easings, and what each one means

| Easing | Feel | Used for |
|---|---|---|
| **Glide** | exponential damping toward the target, 0.09 s half-life | **everything the player does** — including every jump the player asked for |
| **Cut** | targets and state snap together, applied immediately | tooling and capture only, plus the first frame of a newly loaded world |

The distinction is not stylistic. A spatial view is a mental map, and a cut
destroys it — after a cut the player must re-derive where they are. A glide
costs a fraction of a second and preserves the relationship between where they
were and where they are.

The shipped atlas has this exactly inverted at the one place it matters: the
only jump a player can trigger — clicking a link in a panel — routes through
`SetView`, the cut. Every panel link teleports. **Player-triggered jumps glide.**

A glide over a long distance is an **arc, not a slide**: the camera rises toward
the altitude that contains both endpoints, translates, and descends. Sliding
across a galaxy at Reach altitude is a smear of unreadable frames; rising first
shows the player the relationship between the two places, which is the whole
reason to glide rather than cut.

### 5.2 The verb set

| Verb | Binding | Easing | Destination |
|---|---|---|---|
| Dolly | scroll wheel | glide | ×1.25 per notch, **toward the cursor's plane point** |
| Pan | right-drag, WASD | glide | grabbed point stays under the cursor |
| Tilt | middle-drag | glide | 0.2°/px, clamped per §5.5 |
| **Frame all** | `Home` | glide | fit **content** (§5.3) |
| **Focus selection** | `F`, double-click | glide | the subject, at its kind's altitude |
| **Focus from a panel** | panel link | glide | the subject, at Reach |
| **Back** | `Backspace` | glide | the previous framed view (stack depth 8) |
| **Level** | `R` | glide | pitch to 65°, altitude and focus unchanged |

Dolly-toward-cursor, grabbed-point pan and the damping constant are kept
verbatim — they are good, and they are the reason the camera already feels
right at Reach.

**Focus distances are derived, never constant.** `JumpTo`'s hardcoded distance
of 24 world units is `f = 0.06` on a radius-21 galaxy (deep in the Ground
plateau, showing empty hexes) and `f = 0.18` on a radius-5 one (Reach) — the
same click lands in different bands on different worlds. Destinations are
expressed in `f` or in hexes-across-frame:

| Subject | Framed at |
|---|---|
| Polity | fit its territory bounds |
| Port | its service radius × 3 — the domain and its neighbours |
| Outpost, POI, fleet, shipment | `f = 0.20` (Reach) |
| System / body | the crossfade midpoint |

### 5.3 Framing means content, not the disc

`FitTo` frames `AtlasGeometry.DiscBounds` — every cell in the model, padded 48
units — which is the *possible* world, not the inhabited one. Measured across
nine artifacts, the inhabited region occupies:

| World | Content / frame |
|---|---|
| mature radius-21 (six seeds) | **36–45%** |
| `epoch 1234 40 5` (tiny, mature) | 18.8% |
| `epoch 7 5 21` (young, peaceful) | 10.2% |
| `epoch 42 2 21` (young, full extent) | **1.4%**, and 0.36 extents off-centre |

So even a busy 218-port galaxy opens with 60% of the frame empty, and a young
one opens with 99% empty and its content off to one side.

**Fit frames the content bounds**: every port and outpost, inflated by the
largest service radius on the map so the glows are not clipped. On seed-42 that
is a fit distance of 553 rather than 888; on `epoch 42 2 21` it is 116 rather
than 888 — the same world, 7.7× closer, and centred on what exists.

Two guards make it safe on degenerate worlds:

- **No inhabited marks at all** (a world at year 0) → fall back to disc bounds.
  An empty galaxy is legitimately a whole-disc subject.
- **Content smaller than a neighbourhood** (one port, or two adjacent ones) →
  clamp the fit so the frame never shows fewer than **24 hexes across**. A
  one-port world must not open inside the orbit view.

The camera's maximum distance follows fit, as it does today (`fit × 1.3`), so
content framing also removes most of the Realm plateau: 4.4 dead notches become
2.2.

### 5.4 Pan is leashed, and there is always a way home

`_targetFocus` is unclamped today: the player can pan arbitrarily far into empty
space, and on a sparse galaxy — where the content is 1.4% of the disc — a single
mis-aimed scroll does it.

**The leash clamps the target, not the current position.** `_targetFocus` is
confined to the content bounds inflated by half the frame's world width, so the
content can always be pushed to the frame edge but never out of it. Because the
clamp is on the target and the current position chases it through the existing
damping, **the rubber-band comes free**: drag past the leash and the view eases
back, with no new machinery and no special case.

The leash grows as the camera rises — at Realm the whole galaxy is in frame and
the leash is effectively "stay over the disc".

`Home` is the guaranteed rescue, and it is unconditional: it works from any
state, including one the leash could not prevent (a jump to a subject that was
subsequently removed by a scrub).

### 5.5 Pitch

Pitch is clamped 25°–90°. The floor is not arbitrary: **25° is exactly
`FovDegrees / 2`**, the angle at which the frustum's top edge runs parallel to
the plane and the horizon sits precisely at the top of the frame. That coupling
is currently undocumented and load-bearing — change the FOV without moving the
pitch floor and the horizon enters the frame, with the starfield ending in a
hard line across the middle of the view. **The floor is defined as `fov / 2`,
not as 25.**

**Pitch couples to altitude.** Tilting is for looking *at* something; it is not
a way to look at a galaxy. At Realm and Domains a low pitch produces a frame
that is mostly horizon, and because marks carry pixel floors they do not shrink
with distance — the foreshortened far field packs full-size marks into a
fraction of the screen and becomes an unreadable band, while the near half of
the frame is empty. Measured at `f = 0.30`, pitch 25 against pitch 90: the same
world, the same distance, and the far third of the frame is a solid mass of
overlapping glyphs.

So the tilt range opens as the camera descends:

| Band | Pitch range |
|---|---|
| Realm | 70°–90° |
| Domains | 55°–90° |
| Reach | 35°–90° |
| Ground | 25°–90° (full) |

Pitch is re-clamped on altitude change and glides to the new bound, so
descending never snaps the view. The default on fit stays 65°.

This is also where the atlas's 2.5D grammar actually earns its keep: depth reads
at Ground, where bodies orbit and rings have extent, and reads as noise at
Realm, where everything is coplanar anyway.

### 5.6 There is no yaw, and that is the design

The map has a fixed north with no binding to turn it. That is kept, as a
decision rather than an omission:

1. **Nothing in the map is directional.** Hex axial coordinates have a fixed
   orientation, lane strokes are undirected, territory is isotropic. Rotation
   reveals nothing that was hidden.
2. **Glyphs are screen-facing billboards.** Under yaw the icon layer stays
   upright while the map turns — correct, and it means yaw buys the mark layer
   nothing.
3. **Fixed north is what makes spatial memory work.** "The red polity is
   top-left" stays true across a session, across a scrub, and across a reload.
   That is worth more than a rotation the player would use once.
4. **It would cost the orbit stage.** `ComputeVisibleHexes` bounds the frustum
   with a world-axis-aligned rect; under yaw the rect grows by up to √2 in area
   and drives the hex count straight into the 160 cap.

The one thing yaw would have bought — looking behind a foreshortened feature —
is answered by pitch, which is cheaper and already there.

---

## 6. Affordances

This group is interaction-shaped rather than icon-shaped: it earns no new
glyphs and three new elements. Behaviour is specified here; visual design
belongs to Group 5, which owns the chrome language.

**The altitude scale.** A slim vertical scale at the viewport edge: the four
bands as segments, the current altitude as a marker, and the **next thing to
resolve** named. This is the answer to "crossing a band silently changes the
map" — the player learns that *zoom* is the variable, and learns what more zoom
will buy, without being told in a tutorial. Interface Group 5 needs from the
rig: the current `Band`, `f`, and `(nextResolve, atF)`.

**The lost rescue.** When the frame contains no content at all — content bounds
disjoint from the frustum rect — an arrow toward the content centroid appears
with the `Home` hint. It is the visible half of the leash: the leash prevents
the common case, the rescue covers the rest, and neither ever traps the camera.

**The focus reticle.** A brief ring at a glide's destination, in the selection
highlight's language, so the eye knows where the motion is going before it
arrives. Long glides are the case that needs it — the arc in §5.1 means the
destination is off-screen for most of the motion.

All three are **affordances, not data**, and they take chrome tokens
(`--ssg-acc`, `--ssg-ink3`) rather than `AtlasPalette` values. The precedent is
the selection highlight, which is already `#86D7FF` — the `--ssg-acc` token —
and already documented as *"the UI accent, an affordance over the map, not a
data color"*. That comment is the bridge between the atlas's two colour
authorities, and this document promotes it to a rule: **affordances use chrome
tokens; data uses `AtlasPalette`.**

---

## 7. The per-frame budget

`AtlasRoot.OnZoomChanged` runs on every damped distance change — many times per
scroll notch. Its contract:

**A zoom tick costs O(layers) material writes and nothing else.** Fourteen
`SetColor` calls is the correct shape and stays.

**Screen-constant styling quantizes to the zoom lattice.** Three layers
(`LaneLayer`, `FlowTrailLayer`, `CrawlPathLayer`) rebuild their stroke meshes
when the screen-constant width drifts more than 8%. Against a 25% notch that is
**2.9 full mesh rebuilds per notch** — 205 lanes rebuilt three times for one
click of the wheel. Snapping the stroke width to the nearest power of 1.25
gives **exactly one rebuild per notch** with at most ±11% width error, which is
below the threshold at which a 1.4 px stroke reads as a different weight.

**The lattice builds at load, not on approach.** It is a single
full-galaxy line mesh — **881,790 vertices and 1,763,580 line indices, 30.1 ms**
at radius 21 — built lazily in the one frame the camera first crosses
`f = 0.224`. That is a guaranteed two-frame hitch in the middle of a zoom
gesture, every session. Moving it to load time puts it where a hitch is already
expected and costs nothing the player can perceive. (Radius 5: 49,686 vertices,
1.8 ms — the cost is entirely a function of galaxy size, and the biggest galaxy
is where the hitch lands.)

**Nothing rebuilds on a band change.** Bands gate visibility and fade targets;
they never trigger geometry work. This is what makes hysteresis a cheap
correctness measure rather than a performance one.

**Viewport changes restyle.** `ViewportPx` is written only inside
`OnZoomChanged`, so a window resize leaves every screen-constant stroke width
stale until the next scroll. Screen-constant styling is a function of `(distance,
viewportPx)` and must be recomputed when either moves. (Billboards are already
safe — `CameraRig.Apply` writes `_AtlasViewportPx` every frame.)

---

## 8. Empty and degenerate states

Navigation is where a sparse world fails first, because there is nothing to
navigate *by*.

| Situation | Behaviour |
|---|---|
| No ports or outposts at all | Fit falls back to disc bounds; the leash falls back to the disc |
| One port | Fit clamps to 24 hexes across; the frame is a neighbourhood, not an orbit view |
| Content off-centre (`epoch 42 2 21`: 0.36 extents) | Fit centres on **content**, so off-centre content is simply centred |
| Content 1.4% of the disc | Fit is 7.7× closer than disc-fit; the two domains are readable rather than two dots in a starfield |
| Camera panned off the content | Leash prevents it; rescue arrow covers what the leash cannot |
| Starfield burying sparse content | Realm/Domains attenuation (§2) |

The map's wider empty-state problem — no lens ever says "no wars", so an empty
sim is indistinguishable from a broken lens — is a cross-cutting finding that
belongs to the lens groups. Group 1's contribution is the half that is a
navigation problem: on a sparse world the player cannot tell "there is nothing
here" from "I am looking at the wrong place", and content framing plus the
rescue arrow answer exactly that half.

---

## 9. Interfaces other groups depend on

- **Group 2 (fields & lenses)** — territory generalizes at Realm (the field's
  smoothing radius is a function of altitude); rasters lead the crossfade; the
  starfield takes an altitude attenuation, full at Reach and below, and never
  attenuates during the crossfade. **Delivered** in
  `docs/design/ui/map-fields-lenses.md`, which amends §2's nature row (above)
  and specifies the starfield's two attenuations — altitude, plus a content term
  that dims stars inside a domain and leaves the wilds sparkling.
- **Group 3 (marks & glyphs)** — mark *count* must fall with altitude, since
  pixel floors mean mark *size* cannot; war and news resolve at Realm while the
  other glyph families resolve at Reach; ports hand over to the orbit view's
  rings rather than fading. **Delivered** in
  `docs/design/ui/marks-glyphs.md`, which amends §2's mark rows (above) into
  weight floors with world-relative tiers, and answers the count requirement
  with one keystone per hex: 957–1347 marks become 92–120 at Realm and 91–187 at
  Reach, with occlusion falling from 69–89% to 0–10%. It also records that
  **`GlyphFade` is exactly 0.000 at Realm and through most of Domains** (its
  window is `f` 0.63 → 0.315, and Domains starts at 0.45), which is why war
  never resolved at Realm despite this document requiring it.
- **Group 4 (lanes & motion)** — Realm shows a trunk network, not all 205 lanes;
  strokes trail the rasters in the crossfade; stroke widths quantize to the zoom
  lattice. **Open, and the handover's weakest seam (§4.1):** a lane needs a
  system-scale terminus at the system rim, on its true bearing, or the descent
  loses the network entirely at the bottom. Owned by Group 4 together with the
  fuller system rendering.
- **Group 5 (chrome)** — the altitude scale, the rescue arrow and the focus
  reticle need visual design; the rig supplies `Band`, `f`, and the next resolve
  step. Affordances take chrome tokens, not palette values.
- **Group 6 (panels & selection)** — panel links glide rather than cut, and
  their destination altitude is derived from the subject's kind.
