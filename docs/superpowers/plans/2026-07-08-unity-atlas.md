# Unity Interactive Atlas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The drill-down galaxy browser per `docs/superpowers/specs/2026-07-07-unity-atlas-design.md`: setup → galaxy hex map → cell view → structured system panel, on procedural hex meshes with math picking and UI Toolkit chrome, all in play mode.

**Architecture:** A `StarGen.Atlas` assembly under `unity/Assets/Scripts/Atlas/` referencing `StarGen.Core`. Pure logic (LayerPalette colors, AtlasNavigator state machine, GalaxyService seam, SystemPanelBuilder) is separated from MonoBehaviours (GalaxyView/CellView/AtlasUI/AtlasController) and covered by Unity edit-mode tests. Both map views reuse Core's `HexGrid` math in their own coordinate spaces: the galaxy view draws one unit hex per cell *at the cell's lattice coordinate*, the cell view draws 91 unit hexes at hex coordinates — same transform, same picking, different interpretation. UI is built entirely in C# (no UXML/USS assets); the scene is constructed by an editor menu item so it's reviewable as code.

**Tech Stack:** Unity 6000.5.2f1 (URP 2D), UI Toolkit runtime, Unity Test Framework (edit mode), StarGen.Core via the existing local UPM package. Zero Core changes.

## Global Constraints

- **No `src/Core` changes of any kind** — this plan consumes Core, never edits it. The dotnet suite must remain exactly **101/101** after every task (`dotnet test` from repo root).
- No generation logic in Unity (atlas spec §4): views/UI touch Core types only through `GalaxyService`; the only Core calls outside it are `HexGrid` math (geometry authority) and model reads for display.
- Rendering: **one procedural mesh per view, vertex colors, no per-hex GameObjects, no colliders, no custom shaders, no art assets** (spec §2, §5). Picking is pure math via `HexGrid.WorldToHex`.
- Flat-top hexes; positions from `HexGrid.HexToWorld` (returns `(double X, double Y)` — cast to float at the Unity boundary); mesh corners from `HexGrid.CornerOffsets` scaled by `(1 - inset)`, inset default **0.08**.
- UI Toolkit for all chrome (spec §2); dark theme via inline styles; layers exactly **Density / Polity / Zone / Dev / Lean**.
- **Unity-side gate (editor must be CLOSED during implementation — the controller coordinates this):**
  - Compile: `UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.2f1/Editor/Unity.exe"; "$UNITY" -batchmode -quit -projectPath unity -logFile unity/compile.log; grep -E "error CS" unity/compile.log && echo COMPILE-FAIL || echo COMPILE-OK`
  - Edit-mode tests: `"$UNITY" -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults unity/test-results.xml -logFile unity/test.log; grep -c 'result="Failed"' unity/test-results.xml || echo 0-failures` (the runner exits nonzero on failures too). If either command reports the project is already open in another editor, STOP and report BLOCKED — do not force.
- Commits: conventional style ending with the trailer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

## Core API surface this plan consumes (verified against main @ 101/101)

`HexCoordinate(int q, int r)` with `Q/R`; `HexGrid`: `Directions`, `Neighbors`, `Distance`, `Ring`, `Spiral(center, radius)`, `HexToWorld(hex) → (double X, double Y)`, `WorldToHex(double x, double y)`, `CornerOffsets` (`(double X, double Y)[6]`), `CellRadius = 5`, `CellOf(hex)`, `CellCenter(cell)`; `GalaxyConfig { MasterSeed, GalaxyRadiusCells = 21, ... }`; `GalaxyContext(config) { Config, IsFlatspace, Skeleton }`, `GalaxyContext.Flatspace(seed)`; `SkeletonBuilder.Build(config) → GalaxySkeleton`; `GalaxySkeleton { Config, IReadOnlyList<RegionCell> Cells, Species, Polities, Events, CellAt(HexCoordinate), TryGetCell(HexCoordinate, out RegionCell), CellForHex(HexCoordinate) }`; `RegionCell { Q, R, Coord, SpiralIndex, MeanDensity, IsVoid, IsChokepoint, Lean, Metallicity, Anchors, OwnerPolityId, DevelopmentTier, WarScarred }`; `Polity { Id, Name, SpeciesId, CapitalCoord, Extinct }`; `SpeciesProfile { Id, Name, Embodiment }`; `GalaxyEvent { Epoch, Type, ActorPolityId, TargetPolityId, Q, R }`; `Anchor { Type, Hex, SpeciesId }`; `DensityField.At(config, hex)`, `InGalaxy(config, hex)`; `Generator.Generate(GalaxyContext, HexCoordinate) → HexResult { Coordinate, System, IsEmpty }`; `Designation.For(hex)`; model: `StarSystem { Designation, GivenName, Arrangement, Stars, OverlayId, Tags }`, `Star { TypeName, Age, Slots, CompanionSlotIndex }`, `OrbitSlot { Index, Band, Body }`, `Body { Kind, Size, Atmosphere, Hydrographics, Biosphere, Settlement, Society, Satellites, Tags, Name }`, `Society { PopulationTier, Government, Order, Port }`.

## File Structure

```
unity/Assets/Scripts/Atlas/StarGen.Atlas.asmdef          (Task 1)
unity/Assets/Scripts/Atlas/AtlasLayer.cs                 (Task 2)
unity/Assets/Scripts/Atlas/LayerPalette.cs               (Task 2)
unity/Assets/Scripts/Atlas/GalaxyService.cs              (Task 3)
unity/Assets/Scripts/Atlas/AtlasNavigator.cs             (Task 4)
unity/Assets/Scripts/Atlas/HexMeshBuilder.cs             (Task 5)
unity/Assets/Scripts/Atlas/GalaxyView.cs                 (Task 6)
unity/Assets/Scripts/Atlas/CellView.cs                   (Task 6)
unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs         (Task 7)
unity/Assets/Scripts/Atlas/AtlasUI.cs                    (Task 7)
unity/Assets/Scripts/Atlas/AtlasController.cs            (Task 8)
unity/Assets/Editor/AtlasSceneSetup.cs                   (Task 8)
unity/Assets/Scripts/Atlas/Tests/StarGen.Atlas.EditorTests.asmdef  (Task 1)
unity/Assets/Scripts/Atlas/Tests/SmokeTests.cs           (Task 1, deleted Task 2)
unity/Assets/Scripts/Atlas/Tests/LayerPaletteTests.cs    (Task 2)
unity/Assets/Scripts/Atlas/Tests/GalaxyServiceTests.cs   (Task 3)
unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs  (Task 4)
unity/Assets/Scripts/Atlas/Tests/HexMeshBuilderTests.cs  (Task 5)
unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs (Task 7)
.gitignore                                               (Task 1: unity logs/results)
```

Coordinate-space convention (used by Tasks 5–8): the **galaxy view** treats cell
lattice coords as ordinary hex coords — one unit hex per cell at
`HexToWorld(cell.Coord)`; clicking maps back via `WorldToHex → cell coord`. The
**cell view** draws the cell's 91 member hexes at
`HexToWorld(hex) − HexToWorld(CellCenter(cellCoord))` (centered at origin);
clicking adds the center offset back before `WorldToHex`. One math library, two
interpretations, zero bespoke geometry.

---
### Task 1: Atlas assemblies + edit-mode test harness

**Files:**
- Create: `unity/Assets/Scripts/Atlas/StarGen.Atlas.asmdef`, `unity/Assets/Scripts/Atlas/Tests/StarGen.Atlas.EditorTests.asmdef`, `unity/Assets/Scripts/Atlas/Tests/SmokeTests.cs`
- Modify: `.gitignore` (unity batch logs/results)

**Interfaces:**
- Produces: the `StarGen.Atlas` assembly (auto-referenced off, references Core) all later tasks compile into, and the edit-mode test assembly + proven batchmode gate.

- [ ] **Step 1: Create the assemblies.** `unity/Assets/Scripts/Atlas/StarGen.Atlas.asmdef`:

```json
{
    "name": "StarGen.Atlas",
    "rootNamespace": "StarGen.Atlas",
    "references": ["StarGen.Core"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": false,
    "noEngineReferences": false
}
```

`unity/Assets/Scripts/Atlas/Tests/StarGen.Atlas.EditorTests.asmdef`:

```json
{
    "name": "StarGen.Atlas.EditorTests",
    "rootNamespace": "StarGen.Atlas.Tests",
    "references": ["StarGen.Atlas", "StarGen.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "precompiledReferences": ["nunit.framework.dll"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false,
    "overrideReferences": true
}
```

`unity/Assets/Scripts/Atlas/Tests/SmokeTests.cs` (deleted in Task 2 once real tests exist):

```csharp
using NUnit.Framework;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class SmokeTests
    {
        [Test]
        public void CoreIsReachable_FromAtlasTestAssembly()
        {
            Assert.AreEqual(1, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(1, 0)));
        }
    }
}
```

Append to `.gitignore`:

```
# Unity batchmode gate artifacts
unity/compile.log
unity/test.log
unity/test-results.xml
```

- [ ] **Step 2: Run the compile gate** (editor closed — see Global Constraints for the exact commands). Expected: `COMPILE-OK`.

- [ ] **Step 3: Run the edit-mode test gate.** Expected: `test-results.xml` exists with the smoke test passing (0 failures).

- [ ] **Step 4: Guard the dotnet suite** — `dotnet test` → 101/101 (nothing outside `unity/` changed except .gitignore).

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: atlas assembly and edit-mode test harness"`

---

### Task 2: AtlasLayer + LayerPalette (pure color mapping)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/AtlasLayer.cs`, `unity/Assets/Scripts/Atlas/LayerPalette.cs`
- Create: `unity/Assets/Scripts/Atlas/Tests/LayerPaletteTests.cs`; Delete: `Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: `GalaxySkeleton`, `RegionCell`, `Polity`, `StellarLean`.
- Produces:
  - `enum AtlasLayer { Density, Polity, Zone, Dev, Lean }`
  - `enum HexState { Void, Empty, System, Settled, Anchored }` (cell-view fills)
  - `static class LayerPalette`: `Color32 CellColor(GalaxySkeleton s, RegionCell c, AtlasLayer layer)`, `Color32 HexColor(HexState state)`, `Color32 Highlight(Color32 c)` (hover brighten: each channel `min(255, v + 60)`). Conventions carried from the retired spike/ASCII atlas: golden-ratio hue per polity id (`hue = (id * 0.6180339887f) % 1f`, sat 0.75, value `0.55 + 0.09 * min(5, dev)`), capitals white, unclaimed `(40,40,40)`, void near-black `(10,10,14)`, density grayscale, zone (war-scarred red `(200,60,50)`, chokepoint cyan `(70,160,180)`, quiet dim gray), dev grayscale ramp by tier/5, lean (young-bright `(120,170,255)`, old-dim `(200,120,80)`, remnant `(150,60,150)`, balanced gray).

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/LayerPaletteTests.cs`:

```csharp
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.Atlas.Tests
{
    public class LayerPaletteTests
    {
        private static GalaxySkeleton Skeleton() =>
            SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });

        [Test]
        public void DensityLayer_IsGrayscale_ScalingWithDensity()
        {
            var s = Skeleton();
            var cell = new RegionCell { MeanDensity = 0.5 };
            var c = LayerPalette.CellColor(s, cell, AtlasLayer.Density);
            Assert.AreEqual(c.r, c.g);
            Assert.AreEqual(c.g, c.b);
            var brighter = LayerPalette.CellColor(s, new RegionCell { MeanDensity = 0.9 }, AtlasLayer.Density);
            Assert.Greater(brighter.r, c.r);
        }

        [Test]
        public void PolityLayer_VoidBlack_UnclaimedGray_CapitalWhite_OwnersDistinct()
        {
            var s = Skeleton();
            var voidCell = new RegionCell { IsVoid = true };
            Assert.AreEqual(10, LayerPalette.CellColor(s, voidCell, AtlasLayer.Polity).r);
            var unclaimed = new RegionCell { OwnerPolityId = -1 };
            Assert.AreEqual(40, LayerPalette.CellColor(s, unclaimed, AtlasLayer.Polity).r);
            var polity = s.Polities[0];
            var capital = s.CellAt(polity.CapitalCoord);
            var capColor = LayerPalette.CellColor(s, capital, AtlasLayer.Polity);
            Assert.AreEqual(255, capColor.r);
            Assert.AreEqual(255, capColor.g);
            Assert.AreEqual(255, capColor.b);
            var owned0 = new RegionCell { OwnerPolityId = 0, DevelopmentTier = 2, Q = 99, R = 99 };
            var owned1 = new RegionCell { OwnerPolityId = 1, DevelopmentTier = 2, Q = 99, R = 99 };
            Assert.AreNotEqual(LayerPalette.CellColor(s, owned0, AtlasLayer.Polity),
                               LayerPalette.CellColor(s, owned1, AtlasLayer.Polity));
        }

        [Test]
        public void HexStates_AllDistinct_AndHighlightBrightens()
        {
            var states = new[] { HexState.Void, HexState.Empty, HexState.System, HexState.Settled, HexState.Anchored };
            for (int i = 0; i < states.Length; i++)
                for (int j = i + 1; j < states.Length; j++)
                    Assert.AreNotEqual(LayerPalette.HexColor(states[i]), LayerPalette.HexColor(states[j]));
            var baseColor = LayerPalette.HexColor(HexState.System);
            var hi = LayerPalette.Highlight(baseColor);
            Assert.Greater(hi.r, baseColor.r);
        }

        [Test]
        public void EveryLayer_ProducesAColor_ForEveryRealCell()
        {
            var s = Skeleton();
            foreach (var cell in s.Cells)
                foreach (AtlasLayer layer in System.Enum.GetValues(typeof(AtlasLayer)))
                    LayerPalette.CellColor(s, cell, layer);   // must not throw
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 2: Run the test gate** — expected: 4 FAIL (types missing → compile fail counts).

- [ ] **Step 3: Implement.** `unity/Assets/Scripts/Atlas/AtlasLayer.cs`:

```csharp
namespace StarGen.Atlas
{
    public enum AtlasLayer { Density, Polity, Zone, Dev, Lean }

    /// <summary>Cell-view hex fills (the ASCII atlas glyph scheme as colors).</summary>
    public enum HexState { Void, Empty, System, Settled, Anchored }
}
```

`unity/Assets/Scripts/Atlas/LayerPalette.cs`:

```csharp
using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Pure layer->color mapping (atlas spec §4). Carries the spike's
    /// conventions: golden-ratio polity hues, brightness by development,
    /// grayscale density, white capitals.</summary>
    public static class LayerPalette
    {
        private static readonly Color32 Void = new(10, 10, 14, 255);
        private static readonly Color32 Unclaimed = new(40, 40, 40, 255);

        public static Color32 CellColor(GalaxySkeleton s, RegionCell c, AtlasLayer layer)
        {
            switch (layer)
            {
                case AtlasLayer.Polity:
                    if (c.IsVoid) return Void;
                    foreach (var p in s.Polities)
                        if (!p.Extinct && p.CapitalCoord.Equals(c.Coord))
                            return new Color32(255, 255, 255, 255);
                    if (c.OwnerPolityId < 0) return Unclaimed;
                    float hue = (c.OwnerPolityId * 0.6180339887f) % 1f;
                    float value = 0.55f + 0.09f * Mathf.Min(5, c.DevelopmentTier);
                    return (Color32)Color.HSVToRGB(hue, 0.75f, value);
                case AtlasLayer.Zone:
                    if (c.IsVoid) return Void;
                    if (c.WarScarred) return new Color32(200, 60, 50, 255);
                    if (c.IsChokepoint) return new Color32(70, 160, 180, 255);
                    return new Color32(55, 55, 60, 255);
                case AtlasLayer.Dev:
                    if (c.IsVoid) return Void;
                    if (c.OwnerPolityId < 0) return Unclaimed;
                    byte d = (byte)(70 + 37 * Mathf.Min(5, c.DevelopmentTier));
                    return new Color32(d, d, d, 255);
                case AtlasLayer.Lean:
                    if (c.IsVoid) return Void;
                    return c.Lean switch
                    {
                        StellarLean.YoungBright => new Color32(120, 170, 255, 255),
                        StellarLean.OldDim => new Color32(200, 120, 80, 255),
                        StellarLean.RemnantGraveyard => new Color32(150, 60, 150, 255),
                        _ => new Color32(110, 110, 110, 255),
                    };
                default:   // Density
                    byte g = (byte)(255 * Mathf.Clamp01((float)c.MeanDensity));
                    return new Color32(g, g, g, 255);
            }
        }

        public static Color32 HexColor(HexState state) => state switch
        {
            HexState.Void => Void,
            HexState.Empty => new Color32(28, 28, 34, 255),
            HexState.System => new Color32(190, 190, 200, 255),
            HexState.Settled => new Color32(255, 190, 80, 255),
            HexState.Anchored => new Color32(120, 220, 160, 255),
            _ => Void,
        };

        public static Color32 Highlight(Color32 c) => new(
            (byte)Mathf.Min(255, c.r + 60), (byte)Mathf.Min(255, c.g + 60),
            (byte)Mathf.Min(255, c.b + 60), 255);
    }
}
```

Delete `Tests/SmokeTests.cs` (real tests supersede the wiring check).

- [ ] **Step 4: Run gates** — edit-mode tests: 4 PASS, 0 failures. `dotnet test`: 101/101.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: atlas layers and pure color palette"`

---
### Task 3: GalaxyService (the Core↔Unity seam)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/GalaxyService.cs`
- Test: `unity/Assets/Scripts/Atlas/Tests/GalaxyServiceTests.cs`

**Interfaces:**
- Consumes: `GalaxyConfig`, `SkeletonBuilder`, `GalaxyContext`, `Generator`, `HexGrid`, model types.
- Produces: `sealed class GalaxyService` — ctor `GalaxyService(ulong seed, int radiusCells)`; `void Build()` (builds skeleton, creates context; sets `BuildMilliseconds`); `GalaxySkeleton Skeleton { get; }` (throws `InvalidOperationException` before Build); `GalaxyContext Context { get; }`; `long BuildMilliseconds { get; }`; `HexResult Generate(HexCoordinate hex)`; `bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell)`; `HexState StateOf(HexCoordinate hex)` — Void if `!DensityField.InGalaxy`, Anchored if the hex's cell has an anchor at exactly this hex, else Settled/System/Empty from `Generate` (settled = any slot body or satellite with `Settlement != None`); `string CellSummary(RegionCell cell)` — the REPL cell dump as a string (owner name or "unclaimed", lean, metallicity, density, void/chokepoint flags, war-scarring, anchor lines, event lines) for the cell side panel. Views never construct Core generation types themselves.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/GalaxyServiceTests.cs`:

```csharp
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class GalaxyServiceTests
    {
        private static GalaxyService Built()
        {
            var service = new GalaxyService(42, 3);
            service.Build();
            return service;
        }

        [Test]
        public void Build_ProducesSkeletonAndContext_AndTimesIt()
        {
            var service = Built();
            Assert.AreEqual(37, service.Skeleton.Cells.Count);   // radius 3 = 3*3*4+1
            Assert.IsNotNull(service.Context.Skeleton);
            Assert.GreaterOrEqual(service.BuildMilliseconds, 0);
        }

        [Test]
        public void Skeleton_BeforeBuild_Throws()
        {
            var service = new GalaxyService(42, 3);
            Assert.Throws<System.InvalidOperationException>(() => _ = service.Skeleton);
        }

        [Test]
        public void Generate_IsDeterministic_AndStateOfIsConsistent()
        {
            var service = Built();
            var center = new HexCoordinate(0, 0);
            Assert.AreEqual(service.Generate(center).IsEmpty, service.Generate(center).IsEmpty);
            // far outside the radius-3 galaxy: Void
            Assert.AreEqual(HexState.Void, service.StateOf(new HexCoordinate(400, 0)));
            // every homeworld anchor hex reports Anchored
            foreach (var cell in service.Skeleton.Cells)
                foreach (var anchor in cell.Anchors)
                    Assert.AreEqual(HexState.Anchored, service.StateOf(anchor.Hex));
        }

        [Test]
        public void CellSummary_NamesOwnerAndLean()
        {
            var service = Built();
            var polity = service.Skeleton.Polities[0];
            var capital = service.Skeleton.CellAt(polity.CapitalCoord);
            var summary = service.CellSummary(capital);
            StringAssert.Contains(polity.Name, summary);
            StringAssert.Contains(capital.Lean.ToString(), summary);
        }
    }
}
```

- [ ] **Step 2: Run the test gate** — expected FAIL (type missing).

- [ ] **Step 3: Implement** — `unity/Assets/Scripts/Atlas/GalaxyService.cs`:

```csharp
using System;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Atlas
{
    /// <summary>The single Core↔Unity seam (atlas spec §4): owns config, skeleton,
    /// and generation; views touch Core only through it (plus HexGrid math).</summary>
    public sealed class GalaxyService
    {
        private readonly GalaxyConfig _config;
        private GalaxyContext? _context;

        public GalaxyService(ulong seed, int radiusCells) =>
            _config = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radiusCells };

        public long BuildMilliseconds { get; private set; }

        public GalaxyContext Context => _context
            ?? throw new InvalidOperationException("call Build() first");

        public GalaxySkeleton Skeleton => Context.Skeleton!;

        public void Build()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var skeleton = SkeletonBuilder.Build(_config);
            timer.Stop();
            BuildMilliseconds = timer.ElapsedMilliseconds;
            _context = new GalaxyContext(_config) { Skeleton = skeleton };
        }

        public HexResult Generate(HexCoordinate hex) => Generator.Generate(Context, hex);

        public bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell) =>
            Skeleton.TryGetCell(cellCoord, out cell);

        public HexState StateOf(HexCoordinate hex)
        {
            if (!DensityField.InGalaxy(_config, hex)) return HexState.Void;
            var cell = Skeleton.CellForHex(hex);
            foreach (var anchor in cell.Anchors)
                if (anchor.Hex.Equals(hex)) return HexState.Anchored;
            var system = Generate(hex).System;
            if (system == null) return HexState.Empty;
            foreach (var star in system.Stars)
                foreach (var slot in star.Slots)
                {
                    if (slot.Body == null) continue;
                    if (slot.Body.Settlement != Settlement.None) return HexState.Settled;
                    foreach (var satellite in slot.Body.Satellites)
                        if (satellite.Settlement != Settlement.None) return HexState.Settled;
                }
            return HexState.System;
        }

        public string CellSummary(RegionCell cell)
        {
            var s = Skeleton;
            var sb = new StringBuilder();
            string owner = cell.OwnerPolityId >= 0 ? s.Polities[cell.OwnerPolityId].Name : "unclaimed";
            sb.AppendLine($"cell ({cell.Q},{cell.R})  density {cell.MeanDensity:F2}"
                + (cell.IsVoid ? "  VOID" : "") + (cell.IsChokepoint ? "  CHOKEPOINT" : ""));
            sb.AppendLine($"{cell.Lean} · metallicity {cell.Metallicity:F2}");
            sb.AppendLine($"owner: {owner} · dev {cell.DevelopmentTier}"
                + (cell.WarScarred ? " · war-scarred" : ""));
            foreach (var anchor in cell.Anchors)
                sb.AppendLine($"anchor: {anchor.Type} at {Core.Naming.Designation.For(anchor.Hex)}"
                    + (anchor.SpeciesId >= 0 ? $" ({s.Species[anchor.SpeciesId].Name})" : ""));
            foreach (var e in s.Events)
                if (e.Q == cell.Q && e.R == cell.R)
                    sb.AppendLine($"epoch {e.Epoch}: {e.Type} by {s.Polities[e.ActorPolityId].Name}"
                        + (e.TargetPolityId >= 0 ? $" vs {s.Polities[e.TargetPolityId].Name}" : ""));
            return sb.ToString();
        }
    }
}
```

(Note the fully-qualified `Core.Naming.Designation` — add `using StarGen.Core.Naming;` and shorten if preferred; nullable annotations require the file to compile under Unity's project-wide settings, which allow `?` on reference types via the asmdef's default C# version in Unity 6.)

- [ ] **Step 4: Run gates** — edit-mode tests all green; `dotnet test` 101/101.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: GalaxyService seam for the atlas"`

---

### Task 4: AtlasNavigator (drill-down state machine)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/AtlasNavigator.cs`
- Test: `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs`

**Interfaces:**
- Consumes: `HexCoordinate` only (pure state; no Unity, no service).
- Produces: `enum AtlasScreen { Setup, Galaxy, Cell }`; `sealed class AtlasNavigator`: `AtlasScreen Screen { get; }` (starts Setup), `HexCoordinate? SelectedCell { get; }`, `HexCoordinate? SelectedHex { get; }`, `event Action Changed`, methods `EnterGalaxy()`, `DrillToCell(HexCoordinate)` (only from Galaxy or Cell), `SelectHex(HexCoordinate)` (only in Cell), `ClearHexSelection()`, `Back()` (hex selected → clear it; Cell → Galaxy; Galaxy → Setup; Setup → no-op), `Reset()` (back to Setup, selections cleared). Every mutation fires `Changed` exactly once.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs`:

```csharp
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class AtlasNavigatorTests
    {
        [Test]
        public void DrillDown_AndBack_WalksTheLadder()
        {
            var nav = new AtlasNavigator();
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
            nav.EnterGalaxy();
            Assert.AreEqual(AtlasScreen.Galaxy, nav.Screen);
            nav.DrillToCell(new HexCoordinate(2, -1));
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.AreEqual(new HexCoordinate(2, -1), nav.SelectedCell);
            nav.SelectHex(new HexCoordinate(23, -12));
            Assert.AreEqual(new HexCoordinate(23, -12), nav.SelectedHex);
            nav.Back();   // clears hex selection first
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            nav.Back();
            Assert.AreEqual(AtlasScreen.Galaxy, nav.Screen);
            Assert.IsNull(nav.SelectedCell);
            nav.Back();
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
            nav.Back();   // no-op at the root
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
        }

        [Test]
        public void IllegalTransitions_Throw()
        {
            var nav = new AtlasNavigator();
            Assert.Throws<System.InvalidOperationException>(() => nav.DrillToCell(new HexCoordinate(0, 0)));
            Assert.Throws<System.InvalidOperationException>(() => nav.SelectHex(new HexCoordinate(0, 0)));
        }

        [Test]
        public void EveryMutation_FiresChangedOnce()
        {
            var nav = new AtlasNavigator();
            int fired = 0;
            nav.Changed += () => fired++;
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 1));
            nav.SelectHex(new HexCoordinate(16, 1));
            nav.ClearHexSelection();
            nav.Back();
            nav.Reset();
            Assert.AreEqual(6, fired);
        }

        [Test]
        public void DrillToCell_FromCell_SwitchesCell_AndClearsHex()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.DrillToCell(new HexCoordinate(0, 1));   // breadcrumb-style sibling jump
            Assert.AreEqual(new HexCoordinate(0, 1), nav.SelectedCell);
            Assert.IsNull(nav.SelectedHex);
        }
    }
}
```

- [ ] **Step 2: Run the test gate** — expected FAIL.

- [ ] **Step 3: Implement** — `unity/Assets/Scripts/Atlas/AtlasNavigator.cs`:

```csharp
using System;
using StarGen.Core.Model;

namespace StarGen.Atlas
{
    public enum AtlasScreen { Setup, Galaxy, Cell }

    /// <summary>Pure drill-down state (atlas spec §3): Setup → Galaxy → Cell (+ hex
    /// selection). No Unity types — fully edit-mode testable.</summary>
    public sealed class AtlasNavigator
    {
        public AtlasScreen Screen { get; private set; } = AtlasScreen.Setup;
        public HexCoordinate? SelectedCell { get; private set; }
        public HexCoordinate? SelectedHex { get; private set; }
        public event Action? Changed;

        public void EnterGalaxy()
        {
            Screen = AtlasScreen.Galaxy;
            SelectedCell = null;
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void DrillToCell(HexCoordinate cellCoord)
        {
            if (Screen != AtlasScreen.Galaxy && Screen != AtlasScreen.Cell)
                throw new InvalidOperationException($"cannot drill to a cell from {Screen}");
            Screen = AtlasScreen.Cell;
            SelectedCell = cellCoord;
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void SelectHex(HexCoordinate hex)
        {
            if (Screen != AtlasScreen.Cell)
                throw new InvalidOperationException($"cannot select a hex from {Screen}");
            SelectedHex = hex;
            Changed?.Invoke();
        }

        public void ClearHexSelection()
        {
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void Back()
        {
            if (SelectedHex != null) { SelectedHex = null; }
            else if (Screen == AtlasScreen.Cell) { Screen = AtlasScreen.Galaxy; SelectedCell = null; }
            else if (Screen == AtlasScreen.Galaxy) { Screen = AtlasScreen.Setup; }
            else return;   // Setup: no-op, no event
            Changed?.Invoke();
        }

        public void Reset()
        {
            Screen = AtlasScreen.Setup;
            SelectedCell = null;
            SelectedHex = null;
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Run gates** — edit-mode tests green; `dotnet test` 101/101.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: atlas drill-down navigator"`

---

### Task 5: HexMeshBuilder (procedural vertex-colored hex meshes)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/HexMeshBuilder.cs`
- Test: `unity/Assets/Scripts/Atlas/Tests/HexMeshBuilderTests.cs`

**Interfaces:**
- Consumes: `HexGrid.HexToWorld/CornerOffsets`, `HexCoordinate`.
- Produces: `static class HexMeshBuilder` — `Mesh Build(IReadOnlyList<HexCoordinate> hexes, IReadOnlyList<Color32> colors, float inset = 0.08f, Func<HexCoordinate, Vector3>? positionOf = null)`: 7 vertices (center + 6 corners scaled by `1 - inset`) and 6 triangles per hex, fan order, vertex colors (all 7 verts of hex i get `colors[i]`), `IndexFormat.UInt32`; default position = `HexToWorld` cast to float, `positionOf` overrides (the cell view uses it to recenter). `void Recolor(Mesh mesh, IReadOnlyList<Color32> colors)` — writes the colors array in place (7 entries per hex), no rebuild. `void RecolorOne(Mesh mesh, int hexIndex, Color32 color)` — hover/selection single-hex update.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/HexMeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas.Tests
{
    public class HexMeshBuilderTests
    {
        private static (List<HexCoordinate> hexes, List<Color32> colors) Fixture()
        {
            var hexes = new List<HexCoordinate>(HexGrid.Spiral(new HexCoordinate(0, 0), 2));
            var colors = new List<Color32>();
            for (int i = 0; i < hexes.Count; i++) colors.Add(new Color32((byte)i, 0, 0, 255));
            return (hexes, colors);
        }

        [Test]
        public void Build_SevenVertsSixTrisPerHex_WithVertexColors()
        {
            var (hexes, colors) = Fixture();   // 19 hexes
            var mesh = HexMeshBuilder.Build(hexes, colors);
            Assert.AreEqual(19 * 7, mesh.vertexCount);
            Assert.AreEqual(19 * 6 * 3, mesh.triangles.Length);
            Assert.AreEqual(19 * 7, mesh.colors32.Length);
            // hex 3's verts all carry colors[3]
            for (int v = 3 * 7; v < 4 * 7; v++)
                Assert.AreEqual((byte)3, mesh.colors32[v].r);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Build_CentersHexesAtHexToWorld_AndInsetsCorners()
        {
            var (hexes, colors) = Fixture();
            var mesh = HexMeshBuilder.Build(hexes, colors, inset: 0.1f);
            var (wx, wy) = HexGrid.HexToWorld(hexes[5]);
            var center = mesh.vertices[5 * 7];
            Assert.AreEqual((float)wx, center.x, 1e-4f);
            Assert.AreEqual((float)wy, center.y, 1e-4f);
            var corner = mesh.vertices[5 * 7 + 1];
            float cornerDist = Vector3.Distance(center, corner);
            Assert.AreEqual(0.9f, cornerDist, 1e-4f);   // unit corner scaled by 1-inset
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Recolor_SwapsInPlace_AndRecolorOne_TouchesOnlyItsHex()
        {
            var (hexes, colors) = Fixture();
            var mesh = HexMeshBuilder.Build(hexes, colors);
            var newColors = new List<Color32>();
            for (int i = 0; i < hexes.Count; i++) newColors.Add(new Color32(0, (byte)i, 0, 255));
            HexMeshBuilder.Recolor(mesh, newColors);
            Assert.AreEqual((byte)7, mesh.colors32[7 * 7].g);
            HexMeshBuilder.RecolorOne(mesh, 2, new Color32(9, 9, 9, 255));
            Assert.AreEqual((byte)9, mesh.colors32[2 * 7].r);
            Assert.AreEqual((byte)3, mesh.colors32[3 * 7].g);   // neighbor untouched
            Object.DestroyImmediate(mesh);
        }
    }
}
```

- [ ] **Step 2: Run the test gate** — expected FAIL.

- [ ] **Step 3: Implement** — `unity/Assets/Scripts/Atlas/HexMeshBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.Atlas
{
    /// <summary>One mesh per view: 7 verts + 6 tris per hex, vertex-colored,
    /// recolorable in place (atlas spec §5). Geometry comes exclusively from
    /// HexGrid (single authority).</summary>
    public static class HexMeshBuilder
    {
        public static Mesh Build(IReadOnlyList<HexCoordinate> hexes, IReadOnlyList<Color32> colors,
                                 float inset = 0.08f, Func<HexCoordinate, Vector3>? positionOf = null)
        {
            if (hexes.Count != colors.Count)
                throw new ArgumentException("hexes and colors must be parallel");
            var vertices = new Vector3[hexes.Count * 7];
            var vertexColors = new Color32[hexes.Count * 7];
            var triangles = new int[hexes.Count * 6 * 3];
            float scale = 1f - inset;

            for (int i = 0; i < hexes.Count; i++)
            {
                Vector3 center;
                if (positionOf != null) center = positionOf(hexes[i]);
                else
                {
                    var (wx, wy) = HexGrid.HexToWorld(hexes[i]);
                    center = new Vector3((float)wx, (float)wy, 0f);
                }
                int baseVertex = i * 7;
                vertices[baseVertex] = center;
                vertexColors[baseVertex] = colors[i];
                for (int c = 0; c < 6; c++)
                {
                    var (ox, oy) = HexGrid.CornerOffsets[c];
                    vertices[baseVertex + 1 + c] =
                        center + new Vector3((float)ox * scale, (float)oy * scale, 0f);
                    vertexColors[baseVertex + 1 + c] = colors[i];
                }
                int baseTri = i * 18;
                for (int t = 0; t < 6; t++)
                {
                    triangles[baseTri + t * 3] = baseVertex;
                    triangles[baseTri + t * 3 + 1] = baseVertex + 1 + (t + 1) % 6;
                    triangles[baseTri + t * 3 + 2] = baseVertex + 1 + t;
                }
            }

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(vertexColors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Recolor(Mesh mesh, IReadOnlyList<Color32> colors)
        {
            var vertexColors = new Color32[colors.Count * 7];
            for (int i = 0; i < colors.Count; i++)
                for (int v = 0; v < 7; v++)
                    vertexColors[i * 7 + v] = colors[i];
            mesh.SetColors(vertexColors);
        }

        public static void RecolorOne(Mesh mesh, int hexIndex, Color32 color)
        {
            var vertexColors = mesh.colors32;
            for (int v = 0; v < 7; v++)
                vertexColors[hexIndex * 7 + v] = color;
            mesh.SetColors(vertexColors);
        }
    }
}
```

(Triangle winding `center, corner[t+1], corner[t]` is clockwise when viewed down
−Z, the front face for Unity's default 2D camera looking at +Z — hexes render
without backface-culling surprises.)

- [ ] **Step 4: Run gates** — edit-mode tests green; `dotnet test` 101/101.

- [ ] **Step 5: Commit** — `git add -A && git commit -m "feat: procedural hex mesh builder with in-place recolor"`

---
### Task 6: GalaxyView + CellView (mesh-backed map MonoBehaviours)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/GalaxyView.cs`, `unity/Assets/Scripts/Atlas/CellView.cs`
- Test: compile gate + dotnet guard only (MonoBehaviour rendering; exercised by Task 9's live acceptance)

**Interfaces:**
- Consumes: `GalaxyService`, `LayerPalette`, `HexMeshBuilder`, `HexGrid`, `AtlasLayer/HexState`.
- Produces (both are `[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]` MonoBehaviours that create their material in `Awake` via `new Material(Shader.Find("Sprites/Default"))` — vertex-color-lit-free rendering under URP, no asset):
  - `GalaxyView`: `void Show(GalaxyService service, AtlasLayer layer)` (builds/rebuilds the mesh: one unit hex per cell at `HexToWorld(cell.Coord)` — cell-lattice space, spiral order); `void SetLayer(AtlasLayer layer)` (Recolor in place); `void SetHover(HexCoordinate? cellCoord)` (RecolorOne highlight, restoring the previous hover's layer color); `HexCoordinate? Pick(Vector2 screenPos, Camera cam)` — plane-z0 unproject → `HexGrid.WorldToHex` → returns the coord iff a cell exists there; `Bounds MapBounds { get; }`.
  - `CellView`: `void Show(GalaxyService service, HexCoordinate cellCoord)` — the cell's 91 hexes (spiral order from `HexGrid.Spiral(CellCenter, CellRadius)`), positions recentered via the builder's `positionOf` (`HexToWorld(hex) − HexToWorld(center)`); colors from `service.StateOf(hex)`; `void SetSelected(HexCoordinate? hex)` (highlight); `HexCoordinate? Pick(Vector2 screenPos, Camera cam)` — unproject, add the center offset back, `WorldToHex`, return iff within `CellRadius` of the center; `Bounds MapBounds { get; }`.
  - Both keep `List<HexCoordinate> _hexes` + `Dictionary<HexCoordinate,int> _indexOf` for O(1) recolor lookups, and destroy their previous mesh on rebuild (`Destroy(_mesh)`).

- [ ] **Step 1: Implement** — `unity/Assets/Scripts/Atlas/GalaxyView.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>The galaxy map: one unit hex per region cell, drawn in
    /// cell-lattice coordinate space (atlas plan: same HexGrid math, coarser
    /// interpretation).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GalaxyView : MonoBehaviour
    {
        private GalaxyService? _service;
        private AtlasLayer _layer;
        private Mesh? _mesh;
        private readonly List<HexCoordinate> _cells = new();
        private readonly Dictionary<HexCoordinate, int> _indexOf = new();
        private HexCoordinate? _hover;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(GalaxyService service, AtlasLayer layer)
        {
            _service = service;
            _layer = layer;
            _hover = null;
            _cells.Clear();
            _indexOf.Clear();
            var colors = new List<Color32>();
            foreach (var cell in service.Skeleton.Cells)
            {
                _indexOf[cell.Coord] = _cells.Count;
                _cells.Add(cell.Coord);
                colors.Add(LayerPalette.CellColor(service.Skeleton, cell, layer));
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = HexMeshBuilder.Build(_cells, colors);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public void SetLayer(AtlasLayer layer)
        {
            if (_service == null || _mesh == null) return;
            _layer = layer;
            _hover = null;
            var colors = new List<Color32>();
            foreach (var cell in _service.Skeleton.Cells)
                colors.Add(LayerPalette.CellColor(_service.Skeleton, cell, layer));
            HexMeshBuilder.Recolor(_mesh, colors);
        }

        public void SetHover(HexCoordinate? cellCoord)
        {
            if (_service == null || _mesh == null || Equals(_hover, cellCoord)) return;
            if (_hover is { } previous && _indexOf.TryGetValue(previous, out int prevIndex))
                HexMeshBuilder.RecolorOne(_mesh, prevIndex,
                    LayerPalette.CellColor(_service.Skeleton, _service.Skeleton.CellAt(previous), _layer));
            _hover = null;
            if (cellCoord is { } next && _indexOf.TryGetValue(next, out int nextIndex))
            {
                var baseColor = LayerPalette.CellColor(
                    _service.Skeleton, _service.Skeleton.CellAt(next), _layer);
                HexMeshBuilder.RecolorOne(_mesh, nextIndex, LayerPalette.Highlight(baseColor));
                _hover = next;
            }
        }

        public HexCoordinate? Pick(Vector2 screenPos, Camera cam)
        {
            if (_service == null) return null;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var cellCoord = HexGrid.WorldToHex(world.x - transform.position.x, world.y - transform.position.y);
            return _indexOf.ContainsKey(cellCoord) ? cellCoord : null;
        }
    }
}
```

`unity/Assets/Scripts/Atlas/CellView.cs`:

```csharp
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>One cell's 91 hexes at hex resolution, recentered on the origin.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CellView : MonoBehaviour
    {
        private GalaxyService? _service;
        private HexCoordinate _cellCoord;
        private Vector3 _centerOffset;
        private Mesh? _mesh;
        private readonly List<HexCoordinate> _hexes = new();
        private readonly Dictionary<HexCoordinate, int> _indexOf = new();
        private HexCoordinate? _selected;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(GalaxyService service, HexCoordinate cellCoord)
        {
            _service = service;
            _cellCoord = cellCoord;
            _selected = null;
            var center = HexGrid.CellCenter(cellCoord);
            var (cx, cy) = HexGrid.HexToWorld(center);
            _centerOffset = new Vector3((float)cx, (float)cy, 0f);

            _hexes.Clear();
            _indexOf.Clear();
            var colors = new List<Color32>();
            foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
            {
                _indexOf[hex] = _hexes.Count;
                _hexes.Add(hex);
                colors.Add(LayerPalette.HexColor(service.StateOf(hex)));
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = HexMeshBuilder.Build(_hexes, colors, positionOf: h =>
            {
                var (wx, wy) = HexGrid.HexToWorld(h);
                return new Vector3((float)wx, (float)wy, 0f) - _centerOffset;
            });
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public void SetSelected(HexCoordinate? hex)
        {
            if (_service == null || _mesh == null || Equals(_selected, hex)) return;
            if (_selected is { } previous && _indexOf.TryGetValue(previous, out int prevIndex))
                HexMeshBuilder.RecolorOne(_mesh, prevIndex,
                    LayerPalette.HexColor(_service.StateOf(previous)));
            _selected = null;
            if (hex is { } next && _indexOf.TryGetValue(next, out int nextIndex))
            {
                HexMeshBuilder.RecolorOne(_mesh, nextIndex,
                    LayerPalette.Highlight(LayerPalette.HexColor(_service.StateOf(next))));
                _selected = next;
            }
        }

        public HexCoordinate? Pick(Vector2 screenPos, Camera cam)
        {
            if (_service == null) return null;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var local = world + _centerOffset - transform.position;
            var hex = HexGrid.WorldToHex(local.x, local.y);
            return _indexOf.ContainsKey(hex) ? hex : null;
        }
    }
}
```

- [ ] **Step 2: Run gates** — compile gate `COMPILE-OK`; edit-mode tests still all green; `dotnet test` 101/101.

- [ ] **Step 3: Commit** — `git add -A && git commit -m "feat: galaxy and cell map views on procedural hex meshes"`

---

### Task 7: SystemPanelBuilder + AtlasUI (UI Toolkit chrome in code)

**Files:**
- Create: `unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs`, `unity/Assets/Scripts/Atlas/AtlasUI.cs`
- Test: `unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs`

**Interfaces:**
- Consumes: `HexResult`/model types, `GalaxyService.CellSummary`, `AtlasLayer`, `UnityEngine.UIElements`.
- Produces:
  - `static class SystemPanelBuilder`: `VisualElement Build(HexResult result)` — a scrollable panel: header `Label` (given name or designation; both when named), sub-header (designation + arrangement + overlay id if any), system tag lines, then per star a bold line (`Star A — {TypeName}, {age}` + companion slot note) and per orbit slot an indented `Label` (`{index} [{band}] {kind} "{name}" · size · atmosphere · oceans% · biosphere`), society lines (`{settlement} · pop tier N · {government} · {order} · {port} port`), satellite lines (`moon a: ...`), body tag lines (`POI: {tag}`). Empty hex → header = designation, body = `"no system"` + `density {value:F2}` line (the caller passes density via the second overload `Build(HexResult result, double density)` used when `System == null`). All text via `Label` elements with class-free inline styles (fontSize 12–14, colors from a small palette; monospace not required).
  - `sealed class AtlasUI : MonoBehaviour` (`[RequireComponent(typeof(UIDocument))]`): builds the whole tree in `Awake` into `UIDocument.rootVisualElement`; public API: events `event Action<ulong, int> GenerateRequested`, `event Action<AtlasLayer> LayerChanged`, `event Action<int> BreadcrumbClicked` (index into the trail), `event Action BackRequested`; methods `ShowSetup(string? error = null)` (seed field default "42", radius `IntegerField` default 21 clamped ≥2 with a warn label above 40, Generate `Button`), `ShowGalaxyHud(string galaxyLabel)` (breadcrumb bar + layer `Toggle` row — exactly five, radio-style: toggling one unsets the rest), `ShowCellHud(string cellSummary)` (breadcrumb + side `Label` with the summary), `ShowSystemPanel(VisualElement panel)` / `HideSystemPanel()`, `SetBreadcrumb(IReadOnlyList<string> trail)`, `SetTooltip(string? text)` (small floating label near the top). Dark theme entirely by inline styles: root `backgroundColor` transparent (map visible beneath), panels `rgba(16,16,22,0.92)`, text `#d8d8e0`.
- **PanelSettings note:** the UIDocument needs a `PanelSettings` asset; Task 8's scene-setup menu creates it (with the default runtime theme if resolvable, else an empty `ThemeStyleSheet` — inline styles carry the design either way). AtlasUI itself never loads assets.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using UnityEngine.UIElements;

namespace StarGen.Atlas.Tests
{
    public class SystemPanelBuilderTests
    {
        private static GalaxyService Built()
        {
            var service = new GalaxyService(42, 3);
            service.Build();
            return service;
        }

        private static string AllText(VisualElement root) =>
            string.Join("\n", root.Query<Label>().ToList().Select(l => l.text));

        [Test]
        public void HomeworldSystem_PanelShowsNameSocietyAndOrbits()
        {
            var service = Built();
            Anchor? homeworld = null;
            foreach (var cell in service.Skeleton.Cells)
                foreach (var anchor in cell.Anchors)
                    if (anchor.Type == AnchorType.Homeworld) { homeworld = anchor; break; }
            Assert.IsNotNull(homeworld, "fixture galaxy must contain a homeworld");
            var result = service.Generate(homeworld!.Hex);
            var panel = SystemPanelBuilder.Build(result);
            var text = AllText(panel);
            StringAssert.Contains(result.System!.GivenName, text);
            StringAssert.Contains(result.System.Designation, text);
            StringAssert.Contains("pop tier", text);
            StringAssert.Contains("Star A", text);
        }

        [Test]
        public void EmptyHex_PanelShowsDesignationAndDensity()
        {
            var service = Built();
            HexCoordinate? empty = null;
            foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 12))
                if (service.StateOf(hex) == HexState.Empty) { empty = hex; break; }
            Assert.IsNotNull(empty, "fixture galaxy must contain an empty in-galaxy hex");
            var result = service.Generate(empty!.Value);
            var panel = SystemPanelBuilder.Build(result, density: 0.42);
            var text = AllText(panel);
            StringAssert.Contains("no system", text);
            StringAssert.Contains("0.42", text);
            StringAssert.Contains(Core.Naming.Designation.For(empty.Value), text);
        }
    }
}
```

(add `using StarGen.Core.Naming;` and use `Designation.For` unqualified if preferred.)

- [ ] **Step 2: Run the test gate** — expected FAIL.

- [ ] **Step 3: Implement `SystemPanelBuilder`** — `unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs`:

```csharp
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>SystemFormatter's content as structured UI elements (atlas spec §3).</summary>
    public static class SystemPanelBuilder
    {
        private static readonly Color Text = new(0.85f, 0.85f, 0.88f);
        private static readonly Color Dim = new(0.55f, 0.55f, 0.62f);
        private static readonly Color Accent = new(1.0f, 0.75f, 0.31f);

        public static VisualElement Build(HexResult result, double density = double.NaN)
        {
            var scroll = new ScrollView();
            var root = scroll.contentContainer;

            if (result.System == null)
            {
                root.Add(Line(Designation.For(result.Coordinate), 16, Accent, bold: true));
                root.Add(Line("no system", 13, Dim));
                if (!double.IsNaN(density)) root.Add(Line($"density {density:F2}", 12, Dim));
                return scroll;
            }

            var system = result.System;
            root.Add(Line(system.GivenName ?? system.Designation, 16, Accent, bold: true));
            root.Add(Line($"{system.Designation} · {system.Arrangement.ToString().ToLowerInvariant()}"
                + (system.OverlayId != null ? $" · overlay: {system.OverlayId}" : ""), 12, Dim));
            foreach (var tag in system.Tags) root.Add(Line($"! {tag}", 12, Accent));

            for (int i = 0; i < system.Stars.Count; i++)
            {
                var star = system.Stars[i];
                string companion = star.CompanionSlotIndex is { } cs ? $" (slot {cs})" : "";
                root.Add(Line($"Star {(char)('A' + i)} — {star.TypeName}, "
                    + star.Age.ToString().ToLowerInvariant() + companion, 14, Text, bold: true));
                foreach (var slot in star.Slots)
                    AddSlot(root, slot);
            }
            return scroll;
        }

        private static void AddSlot(VisualElement root, OrbitSlot slot)
        {
            string band = slot.Band.ToString().ToLowerInvariant();
            if (slot.Body == null)
            {
                root.Add(Line($"  {slot.Index} [{band}] —", 12, Dim));
                return;
            }
            AddBody(root, slot.Body, $"  {slot.Index} [{band}] ");
            for (int m = 0; m < slot.Body.Satellites.Count; m++)
                AddBody(root, slot.Body.Satellites[m], $"      moon {(char)('a' + m)}: ");
        }

        private static void AddBody(VisualElement root, Body body, string prefix)
        {
            string kind = body.Kind switch
            {
                BodyKind.RockyWorld => "rocky world",
                BodyKind.IceWorld => "ice world",
                BodyKind.GasGiant => "gas giant",
                BodyKind.PlanetoidBelt => "planetoid belt",
                _ => "wreckage field",
            };
            string text = prefix + kind
                + (body.Name != null ? $" \"{body.Name}\"" : "")
                + (body.Size > 0 ? $" · size {body.Size}" : "");
            if (body.Kind == BodyKind.RockyWorld || body.Kind == BodyKind.IceWorld)
            {
                text += $" · {body.Atmosphere.ToString().ToLowerInvariant()}";
                if (body.Hydrographics > 0) text += $" · oceans {body.Hydrographics}%";
                if (body.Biosphere != Biosphere.Barren)
                    text += $" · {body.Biosphere.ToString().ToLowerInvariant()}";
            }
            root.Add(Line(text, 12, Text));
            if (body.Society is { } society)
                root.Add(Line($"        {body.Settlement.ToString().ToLowerInvariant()}"
                    + $" · pop tier {society.PopulationTier} · {society.Government}"
                    + $" · {society.Order.ToString().ToLowerInvariant()}"
                    + $" · {society.Port.ToString().ToLowerInvariant()} port", 12, Accent));
            foreach (var tag in body.Tags)
                root.Add(Line($"        POI: {tag}", 12, Accent));
        }

        private static Label Line(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }
    }
}
```

- [ ] **Step 4: Implement `AtlasUI`** — `unity/Assets/Scripts/Atlas/AtlasUI.cs`: build root containers in `Awake` (`_setupPane`, `_hudBar` with breadcrumb `VisualElement` + five layer `Toggle`s, `_tooltip` Label, `_sidePanel` for cell summary, `_systemPanelHost`), all `position: Absolute` over the map; the five toggles implement radio behavior by unsetting siblings inside `RegisterValueChangedCallback` (guard re-entrancy with an `_updatingToggles` bool); `ShowSetup` builds seed `TextField("Seed")` (default "42", parse `ulong.TryParse`, error label on failure), radius `IntegerField("Galaxy radius (cells)")` default 21 (clamp `<2 → 2`; if `>40` show a persistent warn label "large: build + mesh get slow"), Generate `Button` firing `GenerateRequested(seed, radius)`; `SetBreadcrumb` rebuilds the trail as `Button`s (last entry disabled/plain) firing `BreadcrumbClicked(index)`; `ShowGalaxyHud`/`ShowCellHud`/`ShowSystemPanel`/`HideSystemPanel`/`SetTooltip` toggle `style.display`. Panels styled inline: `backgroundColor = new Color(0.06f, 0.06f, 0.09f, 0.92f)`, padding 8, borderRadius 4. This class is UI plumbing — no logic worth unit-testing beyond compile; behavior is exercised in Task 9's live acceptance.

- [ ] **Step 5: Run gates** — edit-mode tests green (2 new); compile gate OK; `dotnet test` 101/101.

- [ ] **Step 6: Commit** — `git add -A && git commit -m "feat: system data panel and UI Toolkit chrome"`

---
### Task 8: AtlasController + scene construction

**Files:**
- Create: `unity/Assets/Scripts/Atlas/AtlasController.cs`, `unity/Assets/Editor/AtlasSceneSetup.cs`
- Test: compile gate + dotnet guard (wiring exercised by Task 9's live acceptance)

**Interfaces:**
- Consumes: everything above.
- Produces:
  - `sealed class AtlasController : MonoBehaviour`: inspector-assigned refs `GalaxyView galaxyView`, `CellView cellView`, `AtlasUI ui`, `Camera mainCamera`; owns `AtlasNavigator _navigator` + `GalaxyService? _service` + `AtlasLayer _layer = AtlasLayer.Polity`. Wiring in `Start`: subscribe `ui.GenerateRequested` → build service (`try/catch` → `ui.ShowSetup(error)`) → `_navigator.EnterGalaxy()`; `ui.LayerChanged` → `_layer` + `galaxyView.SetLayer`; `ui.BreadcrumbClicked(depth)` → depth 0 `_navigator.Reset()`, 1 `EnterGalaxy`, 2 no-op; `ui.BackRequested` + Escape key → `_navigator.Back()`; `_navigator.Changed` → `Render()`. `Update`: Escape → Back; mouse position → active view `.Pick` → `SetHover`/tooltip (galaxy: "cell (q,r) · owner · dev N"; cell: designation + state); left click → galaxy: `DrillToCell(pick)`, cell: `SelectHex(pick)`. `Render()` switch on `_navigator.Screen`: Setup → `ui.ShowSetup()`, views inactive; Galaxy → `galaxyView.Show(_service!, _layer)` + `ui.ShowGalaxyHud($"Galaxy {seed}")` + breadcrumb `["Setup", $"Galaxy {seed}"]` + camera fit to `galaxyView.MapBounds`; Cell → `cellView.Show(_service!, _navigator.SelectedCell!.Value)` + `ui.ShowCellHud(_service.CellSummary(cell))` + breadcrumb `[.., $"Cell ({q},{r})", (hex? designation)]` + camera fit; hex selected → `ui.ShowSystemPanel(SystemPanelBuilder.Build(result, densityForEmpty))` + `cellView.SetSelected(hex)` else `HideSystemPanel`. Camera fit: `FitCamera(Bounds b)` — orthographic, `orthographicSize = max(b.extents.y, b.extents.x / cam.aspect) * 1.08f`, position `(b.center.x, b.center.y, -10)`.
  - `static class AtlasSceneSetup` (Editor): `[MenuItem("StarGen/Setup Atlas Scene")]` — creates/overwrites `unity/Assets/Scenes/Atlas.unity`: new empty scene; `Main Camera` (orthographic, `backgroundColor = #0A0A0E`, `cullingMask` everything, at (0,0,-10)); GO "GalaxyView" + `GalaxyView` component; GO "CellView" + `CellView`; GO "AtlasUI" + `UIDocument` + `AtlasUI` — the `UIDocument.panelSettings` from `Assets/Atlas/PanelSettings.asset`, created if missing via `ScriptableObject.CreateInstance<PanelSettings>()` with `themeStyleSheet` set from the project's default runtime theme if one exists (`AssetDatabase.FindAssets("t:ThemeStyleSheet")` first hit) else a fresh empty `ThemeStyleSheet` asset saved beside it (inline styles carry the design; a console note explains the fallback); GO "Atlas" + `AtlasController` with all refs assigned via serialized fields; save scene + `EditorSceneManager.MarkSceneDirty` handling; also adds the scene to `EditorBuildSettings.scenes` if absent. Idempotent: running it twice rebuilds cleanly.

- [ ] **Step 1: Implement both files** per the interface block above. AtlasController skeleton (verbatim start — fill the remaining handlers exactly as specified in the interface block):

```csharp
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Wires navigator + service + views + UI (atlas spec §3). All state
    /// decisions live in AtlasNavigator; this class only renders and routes input.</summary>
    public sealed class AtlasController : MonoBehaviour
    {
        [SerializeField] private GalaxyView galaxyView = null!;
        [SerializeField] private CellView cellView = null!;
        [SerializeField] private AtlasUI ui = null!;
        [SerializeField] private Camera mainCamera = null!;

        private readonly AtlasNavigator _navigator = new();
        private GalaxyService? _service;
        private AtlasLayer _layer = AtlasLayer.Polity;
        private ulong _seed;

        private void Start()
        {
            ui.GenerateRequested += OnGenerate;
            ui.LayerChanged += layer => { _layer = layer; galaxyView.SetLayer(layer); };
            ui.BreadcrumbClicked += OnBreadcrumb;
            ui.BackRequested += _navigator.Back;
            _navigator.Changed += Render;
            Render();
        }

        private void OnGenerate(ulong seed, int radius)
        {
            try
            {
                var service = new GalaxyService(seed, radius);
                service.Build();
                _service = service;
                _seed = seed;
                _navigator.EnterGalaxy();
            }
            catch (System.Exception ex)
            {
                ui.ShowSetup($"build failed: {ex.Message}");
            }
        }
        // ... OnBreadcrumb, Update (Escape/hover/click), Render, FitCamera per the
        // interface block — every behavior named there must be implemented.
    }
}
```

(The `// ...` above is the ONLY prose-specified code in this plan; the interface block enumerates each handler's exact behavior, and the reviewer will hold the implementation to it line by line.)

- [ ] **Step 2: Run gates** — compile gate `COMPILE-OK`; edit-mode tests all green; `dotnet test` 101/101.

- [ ] **Step 3: Construct the scene headlessly** — `"$UNITY" -batchmode -quit -projectPath unity -executeMethod StarGen.Atlas.EditorTools.AtlasSceneSetup.RunFromCli -logFile unity/scene-setup.log` (give the menu method a `public static void RunFromCli()` twin that calls the same body). Verify `unity/Assets/Scenes/Atlas.unity` + `Assets/Atlas/PanelSettings.asset` exist and the log has no errors.

- [ ] **Step 4: Commit** — `git add -A && git commit -m "feat: atlas controller wiring and scene construction"`

---

### Task 9: Live acceptance (controller-led, MCP)

**Files:**
- Modify: `docs/DESIGN.md` (roadmap phases 2–3 status note)
- No implementer subagent: the session controller drives this task via the Unity MCP bridge with the editor OPEN (coordinated with the human).

- [ ] **Step 1: Editor session.** Human opens the project; controller (via MCP): clears the stale `GalaxyMap` missing-script object from `SampleScene` (deferred cleanup from the hex plan), opens `Atlas.unity`, enters play mode.
- [ ] **Step 2: The acceptance drill (screenshots at every step):** setup screen visible → Generate (seed 42, radius 21) → galaxy map renders (~1,387 hexes, polity layer default) → toggle all five layers (capture each) → click a polity-heartland cell → cell view (91 hexes, state fills, side summary) → click a settled hex → system panel with populated society lines → breadcrumb back to galaxy → Escape back to setup. Also: click a homeworld-anchored hex (find via cell summary anchor line) and verify the panel shows the sapient major world; click an empty hex and verify the "no system" + density panel.
- [ ] **Step 3: Performance sanity** — galaxy build + mesh visible well under a second at radius 21 (log `BuildMilliseconds`).
- [ ] **Step 4: DESIGN.md** — in §4 Roadmap, append to phase 2 and phase 3 *done-when* blocks: `Status: data-panel portion delivered by the Unity atlas (2026-07); orbit-diagram rendering remains.` (phase 2) and `Status: delivered by the Unity atlas (2026-07) at cell resolution; smooth-pan polish deferred.` (phase 3).
- [ ] **Step 5: Commit** — `git add -A && git commit -m "docs: atlas delivery status on roadmap phases 2-3"`

---

## Self-Review Notes

- **Spec coverage:** three-state drill-down + breadcrumb + Escape (T4/T8, spec §3); procedural mesh + math picking + in-place recolor (T5/T6, spec §2/§5); five layer toggles (T2/T7/T8); hover tooltip + selection highlight (T6/T8); GalaxyService seam with views Core-free (T3, spec §4); structured system panel incl. empty-hex density case (T7, spec §3); setup validation + build-failure surfacing (T7/T8, spec §5); camera auto-fit, no pan/zoom (T8, spec §5); edit-mode tests for pure logic (T2–T5, T7, spec §6); live MCP acceptance with the exact §6 drill + layer screenshot set (T9); visual baseline (inline dark styles, no assets/shaders — T2/T7). Deliberately out per spec §7: pan/zoom, orbit diagram, search/stats UI, in-UI save/load.
- **Type consistency:** `GalaxyService(seed, radiusCells)`/`Build()/Skeleton/Context/Generate/TryGetCell/StateOf/CellSummary` used identically in T3/T6/T7/T8; `LayerPalette.CellColor(skeleton, cell, layer)`/`HexColor(state)`/`Highlight`; `HexMeshBuilder.Build(hexes, colors, inset, positionOf)`/`Recolor`/`RecolorOne`; navigator API T4 = T8's usage; `SystemPanelBuilder.Build(result[, density])` T7 = T8.
- **Known accepted risks:** (a) the PanelSettings/theme fallback (empty ThemeStyleSheet) may render default-font-ugly — acceptable for the spec's deliberately modest baseline; the setup menu logs the fallback so it's diagnosable; (b) Task 8's controller has one prose-specified region (handlers enumerated in the interface block rather than verbatim code) — flagged explicitly for the reviewer; (c) batchmode gates require the editor closed — the session controller owns scheduling; if a gate reports the project is open, implementers stop with BLOCKED rather than force; (d) `Shader.Find("Sprites/Default")` at runtime depends on the shader shipping in builds — fine in-editor for this phase; player-build shader stripping is a later-phase concern (noted for the eventual build task).
