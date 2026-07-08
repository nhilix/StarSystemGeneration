# Atlas Setup Knobs + Live Shape Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose galaxy-formation knobs (shape, resources, history) on the atlas setup screen with a live density-shape preview, backed by new `GalaxyConfig` fields, a shape-only Core build path, and a schema-v3 artifact stamp.

**Architecture:** Five new `GalaxyConfig` fields replace literals in `DensityField.ShapeAt` and `SkeletonBuilder.PassResourceAnchors`; `SkeletonBuilder.BuildShape` is the cheap density-only build that `Build` now starts from. The Unity side passes a full `GalaxyConfig` through `GalaxyService`, rebuilds the setup pane as slider foldouts that raise `ConfigEdited`, and the controller consumes a dirty flag once per frame to re-render a shape-only preview on the Density layer.

**Tech Stack:** C# netstandard2.1 Core (xunit tests via `dotnet test`), Unity 6000.5.2f1 URP 2D + UI Toolkit (edit-mode NUnit tests via batchmode CLI), Unity MCP bridge for final live acceptance.

## Global Constraints

- New config defaults MUST equal the old literals exactly: `ArmStrength` 0.9, `CoreRadius` 0.18, `DiscFalloff` 0.55, `MineralAnchorMultiplier` 1.0, `PrecursorAnchorMultiplier` 1.0. A default-config galaxy serializes byte-identically to pre-change output (spec §1 invariant).
- `GalaxySkeleton.SchemaVersion` becomes exactly `3`; the CONFIG record field order is exactly spec §5's: existing ten fields, then `ArmStrength|CoreRadius|DiscFalloff|MineralAnchorMultiplier|PrecursorAnchorMultiplier`, doubles as `"R"` invariant culture.
- No generation logic in Unity; `GalaxyConfig` is the settings DTO crossing the seam (spec §6).
- UI slider floats are rounded to 4 decimals when read into `GalaxyConfig` doubles so UI defaults equal Core defaults exactly (0.35f must become 0.35, not 0.34999999404).
- `Reset defaults` never touches the seed field (spec §6).
- The preview is Density-layer only; no hover/click/tooltip on the Setup screen (spec §2).
- NO changes to files under `src/Core` other than `GalaxyConfig.cs`, `DensityField.cs`, `SkeletonBuilder.cs`, `GalaxySkeleton.cs` (SchemaVersion const only), `SkeletonSerializer.cs`.
- Unity tasks (5–6): the Unity editor MUST be closed (batchmode gates fail with "already open" otherwise — if the compile log shows the project is locked, STOP and report BLOCKED). Task 7 needs the editor OPEN with the MCP bridge (controller runs it).
- Verification integrity (all implementers): reports must include the verbatim output of `stat -c "%n %y" <results file>` for any Unity test run plus the grepped pass/fail totals line; dotnet runs must quote the `Passed!/Failed!` summary line verbatim.
- Branch: `atlas-setup-knobs` off `main`. Working directory for all commands: `C:/Users/Jaaco/Documents/Dev/StarSystemGeneration`.

**Unity batchmode gate commands (Tasks 5–6), run from the repo root in PowerShell:**

Compile gate:
```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity' -logFile 'C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity\compile.log' | Out-Null
Select-String -Path 'unity\compile.log' -Pattern 'error CS'
```
Expected: no matches (empty output). Any `error CS` line = gate failed.

Edit-mode test gate:
```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity' -runTests -testPlatform EditMode -testResults 'C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity\test-results.xml' -logFile 'C:\Users\Jaaco\Documents\Dev\StarSystemGeneration\unity\test.log' | Out-Null
Select-String -Path 'unity\test-results.xml' -Pattern 'test-run.*result'
```
Expected: the `<test-run ...>` line shows `result="Passed(...)"` with `failed="0"`. Both `compile.log`/`test.log` and `test-results.xml` are git-ignored scratch.

---

### Task 1: GalaxyConfig shape fields + ShapeAt wiring

**Files:**
- Modify: `src/Core/Galaxy/GalaxyConfig.cs`
- Modify: `src/Core/Galaxy/DensityField.cs` (ShapeAt only)
- Test: `tests/Core.Tests/Galaxy/DensityFieldTests.cs`

**Interfaces:**
- Consumes: existing `DensityField.ShapeAt(GalaxyConfig, double, double)`.
- Produces: `GalaxyConfig.ArmStrength/CoreRadius/DiscFalloff/MineralAnchorMultiplier/PrecursorAnchorMultiplier` (all `double get/set`, defaults 0.9 / 0.18 / 0.55 / 1.0 / 1.0) — Tasks 2, 4, 5, 6 rely on these exact names.

- [ ] **Step 1: Write the failing tests.** Append to `tests/Core.Tests/Galaxy/DensityFieldTests.cs` (add `using System;` if not present):

```csharp
    [Fact]
    public void ShapeAt_Defaults_ReproduceLegacyConstants()
    {
        var config = new GalaxyConfig();
        for (double nx = -0.9; nx <= 0.91; nx += 0.3)
            for (double ny = -0.9; ny <= 0.91; ny += 0.3)
            {
                double r = Math.Sqrt(nx * nx + ny * ny);
                double expected;
                if (r >= 1.0) expected = 0.0;
                else
                {
                    double theta = Math.Atan2(ny, nx);
                    double core = Math.Exp(-(r * r) / (2 * 0.18 * 0.18));
                    double disc = Math.Exp(-(r * r) / (2 * 0.55 * 0.55));
                    double armAngle = Math.Log(Math.Max(r, 0.05)) / config.ArmTightness;
                    double phase = (theta - armAngle) * config.ArmCount / (2 * Math.PI);
                    double toRidge = Math.Abs(phase - Math.Round(phase)) * 2;
                    double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                                  * (1 - core) * 0.9;
                    expected = Math.Clamp(core + disc * 0.45 + arms * disc, 0.0, 1.0);
                }
                Assert.Equal(expected, DensityField.ShapeAt(config, nx, ny), 12);
            }
    }

    [Fact]
    public void ShapeAt_ZeroArmStrength_IsAngleInvariant()
    {
        var config = new GalaxyConfig { ArmStrength = 0.0 };
        double reference = DensityField.ShapeAt(config, 0.6, 0.0);
        for (int i = 1; i < 16; i++)
        {
            double theta = i * Math.PI / 8;
            Assert.Equal(reference,
                DensityField.ShapeAt(config, 0.6 * Math.Cos(theta), 0.6 * Math.Sin(theta)), 12);
        }
    }

    [Fact]
    public void ShapeAt_LargerDiscFalloff_RaisesRimDensity()
    {
        var tight = new GalaxyConfig { DiscFalloff = 0.35, ArmStrength = 0.0 };
        var flat = new GalaxyConfig { DiscFalloff = 0.9, ArmStrength = 0.0 };
        Assert.True(DensityField.ShapeAt(flat, 0.8, 0.0) > DensityField.ShapeAt(tight, 0.8, 0.0));
    }
```

- [ ] **Step 2: Run to verify failure.** `dotnet test tests/Core.Tests --nologo --filter "ShapeAt" -v q` — expect FAIL: `ArmStrength`/`CoreRadius`/`DiscFalloff` don't compile yet (CS0117/CS1061-style errors count as the failing state for this step).

- [ ] **Step 3: Add the config fields.** In `src/Core/Galaxy/GalaxyConfig.cs`, after `ArmWidth`:

```csharp
    /// <summary>Arm contrast vs. the smooth disc; 0 = armless elliptical galaxy.</summary>
    public double ArmStrength { get; set; } = 0.9;
    /// <summary>Bright-center bulge sigma in rim-normalized units.</summary>
    public double CoreRadius { get; set; } = 0.18;
    /// <summary>Disc density falloff sigma; higher = flatter, denser rim.</summary>
    public double DiscFalloff { get; set; } = 0.55;
    /// <summary>Scales mineral-rich anchor chance (1 = stock, 0 = none).</summary>
    public double MineralAnchorMultiplier { get; set; } = 1.0;
    /// <summary>Scales precursor-site anchor chance (1 = stock, 0 = none).</summary>
    public double PrecursorAnchorMultiplier { get; set; } = 1.0;
```

- [ ] **Step 4: Wire ShapeAt.** In `src/Core/Galaxy/DensityField.cs` `ShapeAt`, replace the three literals (formula otherwise untouched):

```csharp
        double core = Math.Exp(-(r * r) / (2 * config.CoreRadius * config.CoreRadius));
        double disc = Math.Exp(-(r * r) / (2 * config.DiscFalloff * config.DiscFalloff));
```
and the arm weight line becomes:
```csharp
        double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                      * (1 - core) * config.ArmStrength;
```
Also update the two comment strings `// bright center` / `// broad falloff` positions if the edit moves them — content stays.

- [ ] **Step 5: Run the full Core suite.** `dotnet test StarSystemGeneration.sln --nologo -v q` — expect `Passed!` with 0 failed (existing 102 + 3 new = 105 total).

- [ ] **Step 6: Commit.**
```bash
git add src/Core/Galaxy/GalaxyConfig.cs src/Core/Galaxy/DensityField.cs tests/Core.Tests/Galaxy/DensityFieldTests.cs
git commit -m "feat: shape knobs ArmStrength/CoreRadius/DiscFalloff in GalaxyConfig"
```

### Task 2: Anchor-rate multipliers

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (PassResourceAnchors only)
- Test: `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Consumes: `GalaxyConfig.MineralAnchorMultiplier` / `PrecursorAnchorMultiplier` (Task 1).
- Produces: no new API; behavior change only.

- [ ] **Step 1: Write the failing test.** Append to `tests/Core.Tests/Galaxy/SeedingPassTests.cs` (it already has `using System.Linq;` and builds skeletons; add either if missing):

```csharp
    [Fact]
    public void AnchorMultipliers_ScaleAnchorCounts()
    {
        static GalaxySkeleton BuildWith(double mineral, double precursor) =>
            SkeletonBuilder.Build(new GalaxyConfig
            {
                MasterSeed = 99, GalaxyRadiusCells = 8,
                MineralAnchorMultiplier = mineral,
                PrecursorAnchorMultiplier = precursor,
            });

        static int Count(GalaxySkeleton s, AnchorType type) =>
            s.Cells.Sum(c => c.Anchors.Count(a => a.Type == type));

        var stock = BuildWith(1.0, 1.0);
        var none = BuildWith(0.0, 0.0);
        var rich = BuildWith(3.0, 3.0);

        Assert.Equal(0, Count(none, AnchorType.MineralRich));
        Assert.Equal(0, Count(none, AnchorType.PrecursorSite));
        // Fixed seed: a larger multiplier only raises thresholds against the
        // same rolls, so rich anchors are a strict superset of stock's.
        Assert.True(Count(rich, AnchorType.MineralRich) > Count(stock, AnchorType.MineralRich));
        Assert.True(Count(rich, AnchorType.PrecursorSite) > Count(stock, AnchorType.PrecursorSite));
    }
```

- [ ] **Step 2: Run to verify failure.** `dotnet test tests/Core.Tests --nologo --filter "AnchorMultipliers" -v q` — expect FAIL: multiplier 0 still produces anchors (counts equal stock).

- [ ] **Step 3: Implement.** In `PassResourceAnchors` replace the two chance lines:

```csharp
                double mineralChance = (0.10 + 0.25 * cell.Metallicity) * config.MineralAnchorMultiplier;
```
```csharp
            double precursorChance = (0.02 + (cell.Lean == StellarLean.RemnantGraveyard ? 0.02 : 0.0))
                                     * config.PrecursorAnchorMultiplier;
```

- [ ] **Step 4: Run the full Core suite.** `dotnet test StarSystemGeneration.sln --nologo -v q` — expect `Passed!`, 106 total.

- [ ] **Step 5: Commit.**
```bash
git add src/Core/Galaxy/SkeletonBuilder.cs tests/Core.Tests/Galaxy/SeedingPassTests.cs
git commit -m "feat: mineral/precursor anchor-rate multipliers"
```

### Task 3: BuildShape entry point

**Files:**
- Modify: `src/Core/Galaxy/SkeletonBuilder.cs` (Build + new BuildShape)
- Test: `tests/Core.Tests/Galaxy/SeedingPassTests.cs`

**Interfaces:**
- Produces: `public static GalaxySkeleton SkeletonBuilder.BuildShape(GalaxyConfig config)` — Task 5's `GalaxyService.BuildShapeOnly` calls this.

- [ ] **Step 1: Write the failing test.** Append to `tests/Core.Tests/Galaxy/SeedingPassTests.cs`:

```csharp
    [Fact]
    public void BuildShape_MatchesFullBuildDensities_AndSkipsSim()
    {
        var config = new GalaxyConfig { MasterSeed = 5, GalaxyRadiusCells = 8 };
        var shape = SkeletonBuilder.BuildShape(config);
        var full = SkeletonBuilder.Build(config);

        Assert.Equal(full.Cells.Count, shape.Cells.Count);
        for (int i = 0; i < full.Cells.Count; i++)
        {
            Assert.Equal(full.Cells[i].MeanDensity, shape.Cells[i].MeanDensity);
            Assert.Equal(full.Cells[i].IsVoid, shape.Cells[i].IsVoid);
        }
        Assert.Empty(shape.Species);
        Assert.Empty(shape.Polities);
        Assert.Empty(shape.Events);
        Assert.All(shape.Cells, c => Assert.Empty(c.Anchors));
    }
```

- [ ] **Step 2: Run to verify failure.** `dotnet test tests/Core.Tests --nologo --filter "BuildShape" -v q` — expect compile FAIL: `BuildShape` undefined.

- [ ] **Step 3: Implement.** In `src/Core/Galaxy/SkeletonBuilder.cs` replace `Build` and add `BuildShape` directly below it:

```csharp
    public static GalaxySkeleton Build(GalaxyConfig config)
    {
        var skeleton = BuildShape(config);
        PassStellarPopulation(skeleton);
        PassResourceAnchors(skeleton);
        PassHomeworlds(skeleton);
        EpochSim.Run(skeleton);
        return skeleton;
    }

    /// <summary>Skeleton with cell densities/void/chokepoint marks only — no anchors,
    /// homeworlds, or history. The cheap path behind the atlas setup live preview;
    /// PassDensitySummary here is the same pass Build runs, so a preview's density
    /// layer is pixel-identical to the same config's full build (setup-knobs spec §4.1).</summary>
    public static GalaxySkeleton BuildShape(GalaxyConfig config)
    {
        var skeleton = new GalaxySkeleton(config);
        PassDensitySummary(skeleton);
        return skeleton;
    }
```
(The old `// PASSES (later tasks append here, in order):` comment can be dropped.)

- [ ] **Step 4: Run the full Core suite.** `dotnet test StarSystemGeneration.sln --nologo -v q` — expect `Passed!`, 107 total.

- [ ] **Step 5: Commit.**
```bash
git add src/Core/Galaxy/SkeletonBuilder.cs tests/Core.Tests/Galaxy/SeedingPassTests.cs
git commit -m "feat: SkeletonBuilder.BuildShape shape-only build path"
```

### Task 4: Serializer schema v3

**Files:**
- Modify: `src/Core/Galaxy/GalaxySkeleton.cs` (SchemaVersion const only)
- Modify: `src/Core/Galaxy/SkeletonSerializer.cs` (CONFIG save + load)
- Test: `tests/Core.Tests/Galaxy/SerializerTests.cs`

**Interfaces:**
- Produces: schema-v3 artifact format (spec §5); no API signature changes.

- [ ] **Step 1: Write the failing tests.** Append to `tests/Core.Tests/Galaxy/SerializerTests.cs` (file already uses `System.IO` and `SkeletonSerializer`; add usings if missing):

```csharp
    [Fact]
    public void RoundTrip_PreservesNewConfigFields()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig
        {
            MasterSeed = 11, GalaxyRadiusCells = 3,
            ArmStrength = 0.6, CoreRadius = 0.25, DiscFalloff = 0.7,
            MineralAnchorMultiplier = 2.0, PrecursorAnchorMultiplier = 0.5,
        });
        string text = SkeletonSerializer.ToText(s);
        var loaded = SkeletonSerializer.Load(new StringReader(text));
        Assert.Equal(0.6, loaded.Config.ArmStrength);
        Assert.Equal(0.25, loaded.Config.CoreRadius);
        Assert.Equal(0.7, loaded.Config.DiscFalloff);
        Assert.Equal(2.0, loaded.Config.MineralAnchorMultiplier);
        Assert.Equal(0.5, loaded.Config.PrecursorAnchorMultiplier);
        Assert.Equal(text, SkeletonSerializer.ToText(loaded));
    }

    [Fact]
    public void Load_RejectsSchemaV2()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|2\nEND\n")));
    }
```

- [ ] **Step 2: Run to verify failure.** `dotnet test tests/Core.Tests --nologo --filter "SerializerTests" -v q` — expect FAIL: `RoundTrip_PreservesNewConfigFields` loses the new fields (loads defaults); `Load_RejectsSchemaV2` fails because 2 is still the current version.

- [ ] **Step 3: Implement.** In `GalaxySkeleton.cs`: `public const int SchemaVersion = 3;`. In `SkeletonSerializer.Save`, the CONFIG line becomes:

```csharp
        w.WriteLine(string.Join("|", "CONFIG",
            c.MasterSeed.ToString(Inv), c.GalaxyRadiusCells.ToString(Inv),
            c.MeanDensityTarget.ToString("R", Inv), c.ArmCount.ToString(Inv),
            c.ArmTightness.ToString("R", Inv), c.ArmWidth.ToString("R", Inv),
            c.EpochCount.ToString(Inv), c.YearsPerEpoch.ToString(Inv),
            c.HomeworldRatePerCell.ToString("R", Inv),
            c.TraversabilityThreshold.ToString("R", Inv),
            c.ArmStrength.ToString("R", Inv), c.CoreRadius.ToString("R", Inv),
            c.DiscFalloff.ToString("R", Inv),
            c.MineralAnchorMultiplier.ToString("R", Inv),
            c.PrecursorAnchorMultiplier.ToString("R", Inv)));
```

In `Load`'s `case "CONFIG":`, the initializer gains:

```csharp
                            ArmStrength = double.Parse(f[11], Inv),
                            CoreRadius = double.Parse(f[12], Inv),
                            DiscFalloff = double.Parse(f[13], Inv),
                            MineralAnchorMultiplier = double.Parse(f[14], Inv),
                            PrecursorAnchorMultiplier = double.Parse(f[15], Inv),
```

- [ ] **Step 4: Run the full Core suite.** `dotnet test StarSystemGeneration.sln --nologo -v q` — expect `Passed!`, 109 total. If an existing serializer test embeds a literal `STARGEN-SKELETON|2` fixture, update that fixture's header to `|3` and count it in the report.

- [ ] **Step 5: Commit.**
```bash
git add src/Core/Galaxy/GalaxySkeleton.cs src/Core/Galaxy/SkeletonSerializer.cs tests/Core.Tests/Galaxy/SerializerTests.cs
git commit -m "feat: skeleton artifact schema v3 stamps formation knobs"
```

### Task 5: GalaxyService config ctor + shape-only build

**Files:**
- Modify: `unity/Assets/Scripts/Atlas/GalaxyService.cs`
- Modify: `unity/Assets/Scripts/Atlas/AtlasController.cs` (interim ctor call only)
- Test: `unity/Assets/Scripts/Atlas/Tests/GalaxyServiceTests.cs` (+ any other test file that calls `new GalaxyService(` — find with `grep -rn "new GalaxyService(" unity/Assets`)

**Interfaces:**
- Consumes: `SkeletonBuilder.BuildShape` (Task 3), `GalaxyConfig` fields (Task 1).
- Produces: `GalaxyService(GalaxyConfig config)`; `void BuildShapeOnly()`; `bool IsShapeOnly { get; }` — Task 6's controller relies on these exact members. The `(ulong, int)` ctor is REMOVED.

**Editor must be closed for this task's gates.**

- [ ] **Step 1: Write the failing tests.** In `unity/Assets/Scripts/Atlas/Tests/GalaxyServiceTests.cs`, update every `new GalaxyService(seed, radius)` call to `new GalaxyService(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius })` (add `using StarGen.Core.Galaxy;` if missing), and add:

```csharp
        [Test]
        public void ConfigCtor_Builds37Cells()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            service.Build();
            Assert.AreEqual(37, service.Skeleton.Cells.Count);
            Assert.IsFalse(service.IsShapeOnly);
        }

        [Test]
        public void BuildShapeOnly_SetsFlag_AndMatchesCellCount()
        {
            var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 };
            var preview = new GalaxyService(config);
            preview.BuildShapeOnly();
            var full = new GalaxyService(config);
            full.Build();
            Assert.IsTrue(preview.IsShapeOnly);
            Assert.AreEqual(full.Skeleton.Cells.Count, preview.Skeleton.Cells.Count);
        }
```

Update other Atlas test fixtures found by the grep the same way.

- [ ] **Step 2: Implement GalaxyService.** Replace the ctor and Build region of `unity/Assets/Scripts/Atlas/GalaxyService.cs`:

```csharp
        public GalaxyService(GalaxyConfig config) => _config = config;

        public long BuildMilliseconds { get; private set; }

        /// <summary>True when the current skeleton came from BuildShapeOnly —
        /// densities only; never ask a shape-only service for Generate/StateOf.</summary>
        public bool IsShapeOnly { get; private set; }

        public void Build()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var skeleton = SkeletonBuilder.Build(_config);
            timer.Stop();
            BuildMilliseconds = timer.ElapsedMilliseconds;
            IsShapeOnly = false;
            _context = new GalaxyContext(_config) { Skeleton = skeleton };
        }

        /// <summary>Cheap preview build for the setup screen (setup-knobs spec §6).</summary>
        public void BuildShapeOnly()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var skeleton = SkeletonBuilder.BuildShape(_config);
            timer.Stop();
            BuildMilliseconds = timer.ElapsedMilliseconds;
            IsShapeOnly = true;
            _context = new GalaxyContext(_config) { Skeleton = skeleton };
        }
```

- [ ] **Step 3: Interim controller fix (keeps Task 5 compiling; Task 6 replaces it).** In `AtlasController.OnGenerate(ulong seed, int radius)` change only the service construction line:

```csharp
                var service = new GalaxyService(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });
```
(`using StarGen.Core.Galaxy;` is already imported there.)

- [ ] **Step 4: Compile gate, then test gate** (commands in Global Constraints). Expected: no `error CS`; test-run line `failed="0"` with 19 edit-mode tests (17 existing + 2 new; count may differ if the grep in Step 1 touched more fixtures — report the actual total).

- [ ] **Step 5: Commit.**
```bash
git add unity/Assets/Scripts/Atlas/GalaxyService.cs unity/Assets/Scripts/Atlas/AtlasController.cs unity/Assets/Scripts/Atlas/Tests/
git commit -m "feat: GalaxyService takes GalaxyConfig; shape-only preview build"
```

### Task 6: Setup form rebuild + live preview

**Files:**
- Modify: `unity/Assets/Scripts/Atlas/AtlasUI.cs`
- Modify: `unity/Assets/Scripts/Atlas/AtlasController.cs`

**Interfaces:**
- Consumes: `GalaxyService(GalaxyConfig)`, `BuildShapeOnly()`, `IsShapeOnly` (Task 5); `AtlasLayer.Density`; existing `galaxyView.Show(service, layer)`, `FitCamera(Bounds)`, `ui.IsPointerOverChrome`.
- Produces: `AtlasUI.GenerateRequested` and `AtlasUI.ConfigEdited`, both `event Action<GalaxyConfig>?`; `public bool AtlasUI.TryReadConfig(out GalaxyConfig config)`.

**Editor must be closed for this task's gates.**

- [ ] **Step 1: Rebuild the AtlasUI setup pane.** In `unity/Assets/Scripts/Atlas/AtlasUI.cs`:

Add `using StarGen.Core.Galaxy;` to the usings. Replace the two events:

```csharp
        public event Action<GalaxyConfig>? GenerateRequested;
        public event Action<GalaxyConfig>? ConfigEdited;
```

Add control fields next to `_seedField`/`_radiusField`:

```csharp
        private SliderInt _armCount = null!, _epochCount = null!, _yearsPerEpoch = null!;
        private Slider _armTightness = null!, _armWidth = null!, _armStrength = null!,
            _coreRadius = null!, _discFalloff = null!, _meanDensity = null!,
            _mineralMult = null!, _precursorMult = null!, _homeworldRate = null!;
```

Replace `BuildSetupPane` with:

```csharp
        private void BuildSetupPane()
        {
            _setupPane = new VisualElement { name = "setup-pane" };
            _setupPane.style.position = Position.Absolute;
            _setupPane.style.top = 60;
            _setupPane.style.left = 40;
            _setupPane.style.width = 340;
            _setupPane.style.maxHeight = Length.Percent(85);
            StylePanel(_setupPane);

            var title = new Label("Atlas Setup");
            title.style.fontSize = 16;
            title.style.color = TextColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            _setupPane.Add(title);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _setupPane.Add(scroll);

            _seedField = new TextField("Seed") { value = "42" };
            StyleFieldLabel(_seedField);
            _seedField.RegisterValueChangedCallback(_ => RaiseConfigEdited());
            scroll.Add(_seedField);

            _radiusField = new IntegerField("Galaxy radius (cells)") { value = 21 };
            StyleFieldLabel(_radiusField);
            _radiusField.RegisterValueChangedCallback(evt =>
            {
                OnRadiusChanged(evt);
                RaiseConfigEdited();
            });
            scroll.Add(_radiusField);

            _radiusWarnLabel = new Label("large: build + mesh get slow");
            _radiusWarnLabel.style.color = WarnColor;
            _radiusWarnLabel.style.fontSize = 12;
            _radiusWarnLabel.style.marginBottom = 4;
            _radiusWarnLabel.style.display = DisplayStyle.None;
            scroll.Add(_radiusWarnLabel);

            _setupErrorLabel = new Label();
            _setupErrorLabel.style.color = WarnColor;
            _setupErrorLabel.style.fontSize = 12;
            _setupErrorLabel.style.marginBottom = 4;
            _setupErrorLabel.style.display = DisplayStyle.None;
            scroll.Add(_setupErrorLabel);

            var shape = MakeFoldout("Shape", open: true);
            _armCount = AddIntSlider(shape, "Arm count", 1, 8, 3);
            _armTightness = AddSlider(shape, "Arm tightness", 0.15f, 0.8f, 0.35f);
            _armWidth = AddSlider(shape, "Arm width", 0.05f, 0.5f, 0.18f);
            _armStrength = AddSlider(shape, "Arm strength", 0f, 1f, 0.9f);
            _coreRadius = AddSlider(shape, "Core bulge size", 0.08f, 0.4f, 0.18f);
            _discFalloff = AddSlider(shape, "Disc falloff", 0.25f, 1f, 0.55f);
            _meanDensity = AddSlider(shape, "Mean density", 0.2f, 0.8f, 0.5f);
            scroll.Add(shape);

            var resources = MakeFoldout("Resources", open: false);
            _mineralMult = AddSlider(resources, "Mineral-rich anchors ×", 0f, 3f, 1f);
            _precursorMult = AddSlider(resources, "Precursor sites ×", 0f, 3f, 1f);
            _homeworldRate = AddSlider(resources, "Homeworld rate", 0.005f, 0.06f, 0.02f);
            scroll.Add(resources);

            var history = MakeFoldout("History", open: false);
            _epochCount = AddIntSlider(history, "Epochs", 0, 24, 12);
            _yearsPerEpoch = AddIntSlider(history, "Years per epoch", 10, 200, 50);
            scroll.Add(history);

            var resetButton = new Button(ResetDefaults) { text = "Reset defaults" };
            resetButton.style.marginTop = 6;
            _setupPane.Add(resetButton);

            var generateButton = new Button(OnGenerateClicked) { text = "Generate" };
            _setupPane.Add(generateButton);

            _root.Add(_setupPane);
        }

        private Foldout MakeFoldout(string text, bool open)
        {
            var foldout = new Foldout { text = text, value = open };
            foldout.style.marginTop = 4;
            foldout.style.color = TextColor;
            return foldout;
        }

        private Slider AddSlider(VisualElement parent, string label, float lo, float hi, float value)
        {
            var slider = new Slider(label, lo, hi) { value = value, showInputField = true };
            StyleFieldLabel(slider);
            slider.RegisterValueChangedCallback(_ => RaiseConfigEdited());
            parent.Add(slider);
            return slider;
        }

        private SliderInt AddIntSlider(VisualElement parent, string label, int lo, int hi, int value)
        {
            var slider = new SliderInt(label, lo, hi) { value = value, showInputField = true };
            StyleFieldLabel(slider);
            slider.RegisterValueChangedCallback(_ => RaiseConfigEdited());
            parent.Add(slider);
            return slider;
        }
```

Replace `OnGenerateClicked` and add the config plumbing (keep `OnRadiusChanged` as-is):

```csharp
        /// <summary>Reads all setup controls into a config. Slider floats round to
        /// 4 decimals so a stock UI reads back exactly Core's double defaults
        /// (0.35f must become 0.35). False when the seed doesn't parse.</summary>
        public bool TryReadConfig(out GalaxyConfig config)
        {
            config = null!;
            if (!ulong.TryParse(_seedField.value, out var seed)) return false;
            int radius = _radiusField.value;
            if (radius < 2) radius = 2;
            config = new GalaxyConfig
            {
                MasterSeed = seed,
                GalaxyRadiusCells = radius,
                ArmCount = _armCount.value,
                ArmTightness = Math.Round(_armTightness.value, 4),
                ArmWidth = Math.Round(_armWidth.value, 4),
                ArmStrength = Math.Round(_armStrength.value, 4),
                CoreRadius = Math.Round(_coreRadius.value, 4),
                DiscFalloff = Math.Round(_discFalloff.value, 4),
                MeanDensityTarget = Math.Round(_meanDensity.value, 4),
                MineralAnchorMultiplier = Math.Round(_mineralMult.value, 4),
                PrecursorAnchorMultiplier = Math.Round(_precursorMult.value, 4),
                HomeworldRatePerCell = Math.Round(_homeworldRate.value, 4),
                EpochCount = _epochCount.value,
                YearsPerEpoch = _yearsPerEpoch.value,
            };
            return true;
        }

        private void RaiseConfigEdited()
        {
            // An unparsable seed suppresses the event; the last valid preview stays.
            if (TryReadConfig(out var config)) ConfigEdited?.Invoke(config);
        }

        private void OnGenerateClicked()
        {
            if (!TryReadConfig(out var config))
            {
                ShowSetup("seed must be a non-negative whole number");
                return;
            }
            GenerateRequested?.Invoke(config);
        }

        private void ResetDefaults()
        {
            var d = new GalaxyConfig();
            // Seed deliberately untouched: resetting knobs must not discard the
            // seed being explored (setup-knobs spec §6).
            _radiusField.value = d.GalaxyRadiusCells;
            _armCount.value = d.ArmCount;
            _armTightness.value = (float)d.ArmTightness;
            _armWidth.value = (float)d.ArmWidth;
            _armStrength.value = (float)d.ArmStrength;
            _coreRadius.value = (float)d.CoreRadius;
            _discFalloff.value = (float)d.DiscFalloff;
            _meanDensity.value = (float)d.MeanDensityTarget;
            _mineralMult.value = (float)d.MineralAnchorMultiplier;
            _precursorMult.value = (float)d.PrecursorAnchorMultiplier;
            _homeworldRate.value = (float)d.HomeworldRatePerCell;
            _epochCount.value = d.EpochCount;
            _yearsPerEpoch.value = d.YearsPerEpoch;
        }
```

`Math` here needs `using System;` — the file already has `using System;`. Note `AddSlider` labels use `×` (U+00D7); keep the file UTF-8.

- [ ] **Step 2: Wire the controller preview.** In `unity/Assets/Scripts/Atlas/AtlasController.cs`:

Add fields next to `_service`:

```csharp
        private GalaxyService? _previewService;
        private GalaxyConfig? _pendingPreview;
```

In `Start()` add after the `GenerateRequested` subscription:

```csharp
            ui.ConfigEdited += config => _pendingPreview = config;
```

Replace `OnGenerate` with:

```csharp
        private void OnGenerate(GalaxyConfig config)
        {
            try
            {
                var service = new GalaxyService(config);
                service.Build();
                _service = service;
                _seed = config.MasterSeed;
                _navigator.EnterGalaxy();
            }
            catch (System.Exception ex)
            {
                ui.ShowSetup($"build failed: {ex.Message}");
            }
        }
```

In `Update()`, insert the preview consumption between the Escape block and the mouse checks (coalesces slider-drag bursts to one rebuild per frame; runs even with no mouse):

```csharp
            if (_navigator.Screen == AtlasScreen.Setup && _pendingPreview is { } previewConfig)
            {
                _pendingPreview = null;
                RenderPreview(previewConfig);
            }
```

Add the method:

```csharp
        /// <summary>Shape-only rebuild behind the setup pane (setup-knobs spec §6).
        /// Preview services are throwaway; Generate always builds fresh from config.</summary>
        private void RenderPreview(GalaxyConfig config)
        {
            var service = new GalaxyService(config);
            service.BuildShapeOnly();
            _previewService = service;
            galaxyView.gameObject.SetActive(true);
            galaxyView.Show(service, AtlasLayer.Density);
            FitCamera(galaxyView.MapBounds);
        }
```

Replace `Render()`'s Setup branch with:

```csharp
                case AtlasScreen.Setup:
                    cellView.gameObject.SetActive(false);
                    ui.ShowSetup();
                    if (_previewService != null)
                    {
                        galaxyView.gameObject.SetActive(true);
                        galaxyView.Show(_previewService, AtlasLayer.Density);
                        FitCamera(galaxyView.MapBounds);
                    }
                    else
                    {
                        galaxyView.gameObject.SetActive(false);
                        // First entry: seed the initial preview from current controls.
                        if (ui.TryReadConfig(out var initial)) _pendingPreview = initial;
                    }
                    break;
```

- [ ] **Step 3: Compile gate, then test gate** (commands in Global Constraints). Expected: no `error CS`; edit-mode totals unchanged from Task 5 with `failed="0"` (this task adds behavior covered by live acceptance, not unit tests, per the atlas convention).

- [ ] **Step 4: Commit.**
```bash
git add unity/Assets/Scripts/Atlas/AtlasUI.cs unity/Assets/Scripts/Atlas/AtlasController.cs
git commit -m "feat: knob foldouts with live shape preview on atlas setup"
```

### Task 7: Live MCP acceptance (controller-led)

**Files:**
- Possibly modify: `unity/Assets/Editor/AtlasAcceptance.cs` (only if driving sliders needs a menu item; UI-tree queries from RunCommand are preferred)
- Modify: `.superpowers/sdd/progress.md` (ledger)

**This task is executed by the session controller with the Unity editor OPEN and the MCP bridge connected — not by a batchmode subagent.**

- [ ] **Step 1:** Enter play mode. Verify the setup screen shows the grouped form (Shape expanded; Resources/History collapsed) with a density preview already visible behind the pane. Screenshot.
- [ ] **Step 2:** Via UI-tree queries, set `Arm count` SliderInt to 6 and `Arm strength` to 1.0 — verify the preview re-renders with six arms WITHOUT clicking Generate. Screenshot.
- [ ] **Step 3:** Change the seed TextField to another value — preview changes. Set an unparsable seed (`"abc"`) — preview stays (last valid). Restore `42`.
- [ ] **Step 4:** Click `Reset defaults` via SendEvent — sliders return to stock values (query `.value` on each), seed still `42`, preview reverts. Screenshot.
- [ ] **Step 5:** Click Generate with defaults — full build runs (`cells=1615`, sim events/polities present), galaxy screen shows Polity layer as today. Verify `buildMs` logged and console clean.
- [ ] **Step 6:** Escape back to setup — preview returns. Set `Mineral-rich anchors ×` to 3.0, Generate — drill to a cell and confirm anchored (white) hexes are visibly denser on the galaxy map than stock. Screenshot.
- [ ] **Step 7:** Check `Unity_GetConsoleLogs` for zero errors/warnings across the run. Append the acceptance record to `.superpowers/sdd/progress.md`; commit any tooling changes plus the ledger-adjacent files.

---

## Self-Review Notes

- Spec §3 table ↔ Task 6 slider ranges/labels verified one-to-one (12 controls + seed + radius).
- Spec §4/§4.1/§5 map to Tasks 1–4; §6 to Tasks 5–6; §7 test list: defaults equivalence (T1), responsiveness (T1), multipliers (T2), BuildShape parity (T3), serializer (T4), edit-mode ctor/preview (T5), live acceptance (T7).
- Type consistency: `ConfigEdited`/`GenerateRequested` are `Action<GalaxyConfig>` in both AtlasUI (T6 produces) and AtlasController (T6 consumes); `BuildShapeOnly`/`IsShapeOnly` names match T5↔T6; `BuildShape` matches T3↔T5.
- `Assert.Equal(double, double, int precision)` xunit overload used with precision 12 — matches existing test style in DensityFieldTests.
