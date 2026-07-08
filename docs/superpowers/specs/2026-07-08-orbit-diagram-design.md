# Orbit-Diagram System View — Design Spec

**Date:** 2026-07-08
**Status:** Approved design, pending implementation plan
**Depends on:** unity atlas spec (2026-07-07), generation rules spec (2026-07-07) §5, setup knobs spec (2026-07-08, merged)

## 1. Overview

The atlas drill ladder ends at hex selection: picking a hex in the cell view opens
the structured data panel (`SystemPanelBuilder`), but the "visual system view"
half of roadmap phase 2 (DESIGN.md §4) is still missing. This feature adds a
fourth screen — `AtlasScreen.System` — that renders the selected hex's system as
a 2D top-down orbit diagram: concentric orbit rings around the primary star,
bodies as colored discs at deterministic angles, moons clustered on their
parents, belts as dashed rings, a tinted habitable-band annulus, and companion
stars nesting their own sub-rings in place on the primary's ring
(**nested-concentric layout**, chosen against linear-strip and disc-per-star
mockups). The existing data panel stays docked beside the diagram; hovering a
body shows the standard tooltip and clicking it highlights and scrolls to its
line in the panel.

**Contract (phase-2 done-when):** the diagram is a pure projection of the Core
model — same `(seed, coordinate)` renders identically structured to the REPL
dump, with zero generation logic Unity-side.

## 2. Goals / Non-Goals

**Goals**
- `AtlasScreen.System` reachable two ways from the cell view: clicking the
  already-selected hex a second time, or an "Open system" button atop the data
  panel. Both paths exist only when the hex has a system.
- Nested-concentric diagram: every orbit slot (empty ones included) draws as a
  ring; habitable band as a translucent annulus per star; belts dashed; body
  disc size scales with `Body.Size`; settled bodies get an amber outline; moons
  orbit their parent disc; companions nest at their `CompanionSlotIndex`.
- Deterministic placement: all angles derive from `StableHash` of the system
  designation + star/slot indices — the same system always draws the same picture.
- Hover tooltip (name/kind one-liner) and click-to-inspect: clicking a star,
  body, or moon highlights its entry in the data panel and scrolls it into view.
- Breadcrumb gains a fourth crumb (system name or designation). The **Cell crumb
  becomes live**: clicking it from the System screen or with a hex selected
  returns to the cell view with hex selection cleared (closes the deferred
  "enabled-but-inert depth-2 crumb" ticket).
- Layout geometry in a pure, edit-mode-testable class; rendering follows the
  established single-procedural-mesh idiom.

**Non-Goals**
- No physical realism: slot index is not distance, no eccentricity, no orbital
  motion/animation. Angles are decorative.
- No in-diagram text labels in v1 (tooltips + panel carry names; a world→screen
  UI Toolkit label overlay is a recorded follow-up).
- No pan/zoom inside the system screen (camera fits the diagram; smooth pan/zoom
  is a separate atlas-polish item).
- No sprite art or shaders — flat vertex-colored discs per the DF-abstract house
  style.
- No layout guarantee if a slot ever holds both a companion star and a body
  (the generator never produces this).
- Inspector REPL unchanged.

## 3. Navigator & entry points

`AtlasNavigator` (unity/Assets/Scripts/Atlas/AtlasNavigator.cs):

- `AtlasScreen` gains `System`.
- `EnterSystem()`: legal only when `Screen == Cell && SelectedHex != null`,
  otherwise `InvalidOperationException` (matching existing guard style). Sets
  `Screen = System`, fires `Changed`. The navigator stays pure — it does not
  know whether a system exists; callers check first.
- `Back()` from System returns to Cell **keeping `SelectedHex`** (panel context
  survives), then the existing chain (Cell→Galaxy, Galaxy→Setup) is unchanged.
- `DrillToCell` guard extends to allow calls from `System` (breadcrumb path);
  it already resets `SelectedHex` and sets `Screen = Cell`.
- `EnterGalaxy` / `Reset` unchanged (already clear cell+hex).

`AtlasController`:

- `UpdateCellScreen` click: when the clicked hex equals `SelectedHex` and
  `_service.Generate(hex).System != null`, call `EnterSystem()` instead of
  re-selecting. Clicking a selected empty hex stays a no-op.
- `OnBreadcrumb` case 2: when `SelectedCell != null`, call
  `DrillToCell(SelectedCell.Value)` (returns to cell view, hex cleared). The
  last crumb on any screen remains inert ("you are here").
- Breadcrumb on System screen:
  `Setup / Galaxy {seed} / Cell (q,r) / {GivenName ?? Designation}`.

## 4. `OrbitLayout` — pure geometry

New `OrbitLayout` static class (unity/Assets/Scripts/Atlas/OrbitLayout.cs, no
render types — `Vector2` only, same testability class as `HexMeshBuilder`).
`OrbitLayout.Compute(StarSystem system)` returns an `OrbitLayoutResult`:

```csharp
// Slot -1 = the star itself; Moon -1 = the slot body.
public readonly record struct BodyRef(int Star, int Slot, int Moon);

public sealed class OrbitLayoutResult
{
    public List<RingSpec> Rings;        // (center, radius, isBelt)
    public List<BandSpec> HabBands;     // (center, innerRadius, outerRadius)
    public List<StarSpec> Stars;        // (pos, radius, starIndex, typeId)
    public List<BodySpec> Bodies;       // (pos, radius, BodyRef, BodyKind, settled) — moons included
    public List<PickTarget> Picks;      // (pos, pickRadius, BodyRef) — stars, bodies, moons, belts
    public Rect Bounds;                 // envelope of all geometry
}
```

Constants (initial values; tunable during implementation, invariants below hold
regardless): `R0 = 1.0` (innermost gap), `DR = 0.5` (ring gap), primary star
disc `0.28`, companion disc `0.16`, body disc `0.06 + 0.016 × Size`, moon disc
`0.035` at parent radius `+ 0.09`, ring stroke `0.02`, `subDRmin = 0.11`
(minimum companion sub-ring spacing).

- **Primary** (the star with `CompanionSlotIndex == null`) sits at the origin.
  Ring radii are cumulative: ring *i* = ring *i−1* + the gap between slots
  *i−1* and *i* (the innermost gap is `R0`). A gap is `DR` unless it is
  adjacent to a companion slot (see below).
- **Habitable annulus** per star spans the contiguous slots with
  `Band == Habitable`: inner = first hab ring − `0.45·DR`, outer = last hab ring
  + `0.45·DR` (companion bands use its sub-spacing). Stars with no habitable
  slots get no annulus.
- **Angles:** slot body angle = `2π × unit-hash(designation, "orbit", starIndex,
  slotIndex)` via `StableHash`; moons space evenly starting from
  `unit-hash(designation, "moon", starIndex, slotIndex)`. Companion position on
  its host ring uses the same slot-angle formula.
- **Belts** (`BodyKind.PlanetoidBelt`) mark their ring `isBelt` (drawn dashed);
  no disc. Their pick target is a point on the ring at the slot angle with an
  enlarged pick radius.
- **Companions:** a star with `CompanionSlotIndex = c` centers on the primary's
  ring *c* at the hashed angle. Both gaps adjacent to slot *c* widen to
  `DRc = max(2·DR, (companionDisc + (subSlotCount + 1)·subDRmin) / 0.9)` —
  the companion's gravitational influence clears a swath of the primary's disc,
  and the widening guarantees its sub-rings at least `subDRmin` spacing however
  many slots it has. Sub-rings start outside the companion's disc:
  `subDR = (0.9·DRc − companionDisc) / (subSlotCount + 1)`, sub-ring *j* radius
  `companionDisc + (j+1)·subDR` — the innermost ring clears the star disc and
  the outermost stays strictly under `0.9·DRc`, never reaching the primary's
  adjacent rings. Trinary = two companions, each on its own slot, each widening
  its own gaps.
- **Pick targets:** every star, non-belt body, moon, and belt gets one; empty
  rings none. Pick radius = `max(discRadius × 1.6, 0.12)`.

## 5. `SystemView` + `OrbitMeshBuilder` — rendering

`OrbitMeshBuilder` (static, sibling of `HexMeshBuilder`): appends tessellated
primitives into one vertex-colored mesh — ring (annulus strip, 96 segments),
dashed ring (48 dashes), filled disc (24-segment fan), filled annulus (hab
band, translucent vertex alpha), outline ring (settled marker). It records the
vertex range per `BodyRef` so single elements can be recolored in place
(`RecolorOne`-style, as `HexMeshBuilder` does).

`SystemView` (MonoBehaviour, `MeshFilter`+`MeshRenderer`, `Sprites/Default` —
identical scaffolding to `CellView`):

- `Show(StarSystem system)`: computes `OrbitLayout`, builds the mesh, stores
  pick targets. Draw order: hab annuli → rings → stars → bodies → moons →
  settled outlines.
- `Pick(Vector2 screenPos, Camera cam)` → `BodyRef?`: nearest pick target
  within its pick radius (same ScreenToWorldPoint pattern as `CellView.Pick`).
- `SetSelected(BodyRef?)`: recolors the previous selection back and the new
  selection to `LayerPalette.Highlight` of its base color.
- `MapBounds` → mesh bounds; `AtlasController.FitCamera` handles framing.

`OrbitPalette` (static, sibling of `LayerPalette`):

| Element | Color |
|---|---|
| ember_dwarf / amber_dwarf / gold_main | #FF8A5C / #FFB347 / #FFD066 |
| white_blaze / blue_titan | #EAF2FF / #7FB8FF |
| ashen_remnant / collapsed_core | #9AA0AE / #B48AFF |
| unknown star type (fallback) | #FFFFFF |
| rocky / ice / gas giant / belt / wreckage | #C9A06A / #A8D8E8 / #E08840 / #9A8F7A / #8A5C5C |
| moon | #B9BFD0 |
| orbit ring | #262C3F |
| habitable annulus | #3FBF7F at 0.10 alpha |
| settled outline | #FFBF4F (panel accent) |

## 6. Controller, UI, and panel integration

**AtlasController:**
- New serialized `SystemView systemView` reference (scene wiring via
  `AtlasSceneSetup`, like the other views).
- `Render()` gains a `System` case: galaxy/cell views inactive, system view
  active, `systemView.Show(system)`, `FitCamera(systemView.MapBounds)`, HUD
  text `{name} · {designation} · {arrangement}` on the existing cell-HUD
  surface (`ShowCellHud`), breadcrumb per §3, and the
  data panel always shown (`SystemPanelBuilder` output for the selected hex,
  no "Open system" button on this screen).
- `Update()` gains `UpdateSystemScreen(mousePos)`: hover pick → tooltip
  one-liner resolved from the Core model (star: `Star {A+i} — {TypeName}`,
  body: kind + name + size + band, moon: `moon {a+m} — {kind}`); click →
  `systemView.SetSelected(pick)` + `panel.Highlight(pick)`. Esc/Back already
  route through the navigator.

**SystemPanelBuilder:** `Build` returns a `SystemPanel` wrapper instead of a
bare `VisualElement`:

```csharp
public sealed class SystemPanel
{
    public VisualElement Root;
    public void Highlight(BodyRef? key);   // accent-tint the row, ScrollTo it; null clears
}
```

During construction the builder registers each star header and body/moon line
under its `BodyRef`. A new optional `Action? onOpenSystem` parameter adds the
"Open system" button at the top of the panel when non-null (cell screen passes
the callback, system screen passes null). Call sites in `AtlasController`
adapt; `AtlasUI.ShowSystemPanel` keeps taking the root element.

**AtlasAcceptance** gains menu items to drive the new paths headlessly where
possible (`EnterSystem` via navigator, pick queries), per the established
automation-surface convention.

## 7. Testing

**Unity edit-mode (Atlas tests):**
1. *Navigator:* `EnterSystem` legal from Cell+hex, throws from Galaxy/Setup/no-hex;
   `Back` from System keeps `SelectedHex` and lands on Cell; `DrillToCell` from
   System clears hex and lands on Cell; `Reset`/`EnterGalaxy` clear everything.
2. *OrbitLayout determinism:* two `Compute` calls on the same generated system
   yield element-wise identical results.
3. *OrbitLayout geometry invariants* (hand-built systems incl. a trinary with a
   settled world, moons, a belt, and an empty slot): ring count = slot count
   per star; primary radii strictly increasing; non-companion gaps equal `DR`
   and gaps adjacent to a companion slot ≥ `2·DR`; every body sits on its ring
   (|pos − center| = ring radius ± 1e-4); companion center lies on the primary's
   `CompanionSlotIndex` ring; max companion sub-ring < `0.9·DRc` and companion
   sub-ring spacing ≥ `subDRmin`; hab annulus
   spans exactly the habitable rings; belt slots flagged, no belt disc; pick
   targets exist for every star/body/moon/belt and none for empty slots;
   `Bounds` contains all positions.
4. *OrbitPalette:* every `StarTypes.Table` id and every `BodyKind` value maps to
   a non-fallback color.
5. *OrbitMeshBuilder:* vertex/index counts match the primitive mix; recolor
   ranges are valid and disjoint; no NaN vertices.
6. *SystemPanel:* mapping contains a key for every star and body/moon line;
   `Highlight` tints exactly one row and clears the previous one; "Open system"
   button present iff callback non-null.

**Live MCP acceptance (controller-led, editor open):** generate seed 42
defaults; drill to the capital cell and a settled hex; enter the system via the
panel button, back out (hex still selected), re-enter via second click on the
selected hex; verify the diagram against the data panel and a REPL dump of the
same coordinate (star count, slots, moons, belt, hab band — the phase-2
done-when); hover tooltips on star/body/moon; click a moon → panel line
highlights and scrolls; find a binary/trinary system and verify nested
companion sub-rings; selected empty hex second-click is a no-op; Cell crumb
from the System screen returns to cell view with no hex selected; Esc from
System returns to Cell with the panel still open; console clean. Update
DESIGN.md §4 phase-2 status on the branch.

## 8. Follow-ups (recorded, not in scope)

- World→screen UI Toolkit label overlay for body names on the diagram.
- Optional slow orbital-motion animation toggle.
- Hover recolor on the diagram (currently tooltip-only; selection recolors).
- Smooth pan/zoom shared with the general atlas-polish batch.
