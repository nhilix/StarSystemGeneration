# Orbit-Diagram System View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the selected hex's star system as a nested-concentric 2D orbit diagram on a new `AtlasScreen.System` drill level, with hover tooltips and click-to-inspect wired to the existing data panel.

**Architecture:** Pure geometry (`OrbitLayout`) and a mesh accumulator (`OrbitMeshBuilder`) feed a single-procedural-mesh `SystemView` MonoBehaviour, exactly mirroring the `AtlasNavigator`/`HexMeshBuilder`/`CellView` seams. `SystemPanelBuilder` grows a `SystemPanel` wrapper (BodyRef→row mapping) so diagram clicks highlight panel lines. Zero generation logic Unity-side.

**Tech Stack:** Unity 6000.5.2f1 URP 2D + UI Toolkit (edit-mode NUnit tests via batchmode CLI), StarGen.Core consumed read-only, Unity MCP bridge for the final live acceptance.

**Spec:** `docs/superpowers/specs/2026-07-08-orbit-diagram-design.md` (all § references below).

## Global Constraints

- Work on branch `orbit-diagram` cut from `main`. Merge back per finishing-a-development-branch; user pushes manually.
- **Tasks 1–7 require the Unity editor CLOSED** (batchmode gates fail with "already open" — if the log shows the project is locked, STOP and report BLOCKED, do not force). Task 8 requires the editor OPEN with the MCP bridge (controller-led).
- No generation logic in Unity (DESIGN.md §2); the diagram is a pure projection of the Core model. Core (`src/Core`) is NOT modified by this plan.
- C# 9 max in Unity assemblies: **no `record` / `record struct`** — use plain `readonly struct` (the spec's `record struct` note is adapted here deliberately).
- `BodyRef.Slot` is the **position in `Star.Slots`**, not `OrbitSlot.Index` (they coincide today; the list position is the contract everywhere).
- Unity generates a `.meta` file for every new source file during the compile gate — `git add` those alongside the `.cs` files in the same commit.
- `unity/ProjectSettings/*` has pre-existing uncommitted churn — **never `git add` anything under `unity/ProjectSettings/`** (handoff-mandated).
- Implementer reports MUST include the verbatim `<test-run ...>` summary line from `unity/test-results.xml` AND the file's `LastWriteTime` (fabrication guard, house rule).

**Compile gate** (PowerShell, any directory):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity' -logFile 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\compile.log' | Out-Null
Select-String -Path 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\compile.log' -Pattern 'error CS'
```

No output ⇒ compile OK. Lines ⇒ compile errors (quote them in reports).

**Test gate** (PowerShell):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity' -runTests -testPlatform EditMode -testResults 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\test-results.xml' -logFile 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\test.log' | Out-Null
Select-String -Path 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\test-results.xml' -Pattern '<test-run' | Select-Object -First 1
(Get-Item 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\test-results.xml').LastWriteTime
```

Report the `<test-run ... passed="N" failed="0" ...>` line verbatim plus the timestamp.

Current edit-mode baseline: **19/19**. Expected totals per task: T1 → 31, T2 → 34, T3 → 38, T4 → 38 (no new tests), T5 → 42, T6 → 45, T7 → 45.

---

### Task 1: `BodyRef` + `OrbitLayout` pure geometry + tests

**Files:**
- Create: `unity/Assets/Scripts/Atlas/OrbitLayout.cs`
- Create: `unity/Assets/Scripts/Atlas/Tests/TestSystems.cs`
- Create: `unity/Assets/Scripts/Atlas/Tests/OrbitLayoutTests.cs`

**Interfaces:**
- Consumes: `StarSystem`/`Star`/`OrbitSlot`/`Body` from `StarGen.Core.Model`; `StableHash.Mix(ulong,ulong,ulong,ulong)` from `StarGen.Core.Rng`.
- Produces: `readonly struct BodyRef(int Star, int Slot, int Moon)` with value equality; `OrbitLayout.Compute(StarSystem) → OrbitLayoutResult` (`Rings`/`HabBands`/`Stars`/`Bodies`/`Picks`/`Bounds`); `OrbitLayout.PickAt(OrbitLayoutResult, Vector2) → BodyRef?`; public constants `R0, DR, PrimaryDisc, CompanionDisc, BodyDiscBase, BodyDiscPerSize, MoonDisc, MoonOrbitPad, RingStroke, SubDrMin, MinPickRadius, BeltPickRadius, HabHalfWidthFactor`; `OrbitLayout.CompanionClearance(int subSlotCount) → float`. Test fixture `TestSystems.BuildTrinary()` (used again by Tasks 3 and 6).

- [ ] **Step 1: Write the test fixture** — `unity/Assets/Scripts/Atlas/Tests/TestSystems.cs`:

```csharp
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    /// <summary>Hand-built model fixtures for orbit-diagram tests: fully specified
    /// so geometry assertions don't depend on the generator.</summary>
    public static class TestSystems
    {
        public static Body MakeBody(BodyKind kind, int size, Settlement settlement = Settlement.None)
            => new Body { Kind = kind, Size = size, Settlement = settlement };

        public static Star MakeStar(string typeId, int slotCount, int habStart, int habEnd,
                                    int? companionSlot = null)
        {
            var star = new Star { TypeId = typeId, TypeName = typeId, CompanionSlotIndex = companionSlot };
            for (int i = 0; i < slotCount; i++)
                star.Slots.Add(new OrbitSlot
                {
                    Index = i,
                    Band = i >= habStart && i <= habEnd ? OrbitBand.Habitable
                         : i < habStart ? OrbitBand.Inner : OrbitBand.Outer,
                });
            return star;
        }

        /// <summary>gold_main primary (8 slots, hab 3–4): rocky@1, belt@2, settled
        /// colony with two moons@3, gas giant@5; ember_dwarf companion (3 slots,
        /// hab 1, ice world@1) at primary slot 6; collapsed_core companion (1 slot,
        /// no hab band, empty) at primary slot 4. Primary slots 0 and 7 empty.</summary>
        public static StarSystem BuildTrinary()
        {
            var system = new StarSystem("SGC 2048-2048") { Arrangement = StarArrangement.Trinary };

            var primary = MakeStar("gold_main", 8, 3, 4);
            primary.Slots[1].Body = MakeBody(BodyKind.RockyWorld, 5);
            primary.Slots[2].Body = MakeBody(BodyKind.PlanetoidBelt, 0);
            var colony = MakeBody(BodyKind.RockyWorld, 7, Settlement.Colony);
            colony.Satellites.Add(MakeBody(BodyKind.RockyWorld, 1));
            colony.Satellites.Add(MakeBody(BodyKind.IceWorld, 1));
            primary.Slots[3].Body = colony;
            primary.Slots[5].Body = MakeBody(BodyKind.GasGiant, 9);
            system.Stars.Add(primary);

            var emberCompanion = MakeStar("ember_dwarf", 3, 1, 1, companionSlot: 6);
            emberCompanion.Slots[1].Body = MakeBody(BodyKind.IceWorld, 3);
            system.Stars.Add(emberCompanion);

            system.Stars.Add(MakeStar("collapsed_core", 1, 99, 99, companionSlot: 4));
            return system;
        }
    }
}
```

- [ ] **Step 2: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/OrbitLayoutTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas.Tests
{
    public class OrbitLayoutTests
    {
        private static float[] PrimaryRadii(OrbitLayoutResult result, int slotCount)
        {
            // Rings list contract: primary slots first in slot order, then each
            // companion's sub-rings grouped in Stars order.
            var radii = new float[slotCount];
            for (int i = 0; i < slotCount; i++) radii[i] = result.Rings[i].Radius;
            return radii;
        }

        [Test]
        public void Compute_IsDeterministic()
        {
            var system = TestSystems.BuildTrinary();
            var a = OrbitLayout.Compute(system);
            var b = OrbitLayout.Compute(system);
            Assert.AreEqual(a.Rings.Count, b.Rings.Count);
            for (int i = 0; i < a.Rings.Count; i++)
            {
                Assert.AreEqual(a.Rings[i].Center, b.Rings[i].Center);
                Assert.AreEqual(a.Rings[i].Radius, b.Rings[i].Radius);
            }
            Assert.AreEqual(a.Bodies.Count, b.Bodies.Count);
            for (int i = 0; i < a.Bodies.Count; i++)
                Assert.AreEqual(a.Bodies[i].Pos, b.Bodies[i].Pos);
            Assert.AreEqual(a.Bounds, b.Bounds);
        }

        [Test]
        public void PrimaryRings_CumulativeGaps_WidenAroundCompanions()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            Assert.AreEqual(8 + 3 + 1, result.Rings.Count);
            var r = PrimaryRadii(result, 8);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(Vector2.zero, result.Rings[i].Center);
            for (int i = 1; i < 8; i++)
                Assert.Greater(r[i], r[i - 1]);
            Assert.AreEqual(OrbitLayout.R0, r[0], 1e-4f);
            Assert.AreEqual(OrbitLayout.DR, r[1] - r[0], 1e-4f);
            Assert.AreEqual(OrbitLayout.DR, r[2] - r[1], 1e-4f);
            Assert.AreEqual(OrbitLayout.DR, r[3] - r[2], 1e-4f);
            // Companions sit at slots 4 and 6: gaps 4,5 and 6,7 widen to >= 2*DR.
            Assert.GreaterOrEqual(r[4] - r[3], 2f * OrbitLayout.DR - 1e-4f);
            Assert.GreaterOrEqual(r[5] - r[4], 2f * OrbitLayout.DR - 1e-4f);
            Assert.GreaterOrEqual(r[6] - r[5], 2f * OrbitLayout.DR - 1e-4f);
            Assert.GreaterOrEqual(r[7] - r[6], 2f * OrbitLayout.DR - 1e-4f);
        }

        [Test]
        public void Bodies_SitOnTheirRings()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            foreach (var body in result.Bodies)
            {
                if (body.Ref.Moon >= 0) continue;
                var ring = result.Rings.First(rg =>
                    rg.Ref.Star == body.Ref.Star && rg.Ref.Slot == body.Ref.Slot);
                Assert.AreEqual(ring.Radius, Vector2.Distance(body.Pos, ring.Center), 1e-4f);
            }
        }

        [Test]
        public void Companions_CenterOnHostRing_SubRingsBoundedAndSpaced()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            var r = PrimaryRadii(result, 8);
            var ember = result.Stars.First(s => s.StarIndex == 1);
            Assert.AreEqual(r[6], ember.Pos.magnitude, 1e-4f);

            float clearance = OrbitLayout.CompanionClearance(3);
            var subRings = result.Rings.Where(rg => rg.Ref.Star == 1).ToList();
            Assert.AreEqual(3, subRings.Count);
            foreach (var rg in subRings) Assert.AreEqual(ember.Pos, rg.Center);
            var subRadii = subRings.Select(rg => rg.Radius).ToList();
            Assert.Less(subRadii.Max(), 0.9f * clearance);
            Assert.GreaterOrEqual(subRadii[0] - OrbitLayout.CompanionDisc,
                OrbitLayout.SubDrMin - 1e-4f);
            for (int j = 1; j < subRadii.Count; j++)
                Assert.GreaterOrEqual(subRadii[j] - subRadii[j - 1],
                    OrbitLayout.SubDrMin - 1e-4f);
        }

        [Test]
        public void HabBands_SpanHabitableRings()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            var r = PrimaryRadii(result, 8);
            // Primary (hab 3-4) and the ember companion (hab 1); collapsed_core has none.
            Assert.AreEqual(2, result.HabBands.Count);
            var primaryBand = result.HabBands[0];
            Assert.AreEqual(Vector2.zero, primaryBand.Center);
            Assert.AreEqual(r[3] - OrbitLayout.HabHalfWidthFactor * OrbitLayout.DR,
                primaryBand.Inner, 1e-3f);
            Assert.AreEqual(r[4] + OrbitLayout.HabHalfWidthFactor * OrbitLayout.DR,
                primaryBand.Outer, 1e-3f);
        }

        [Test]
        public void BeltSlot_FlagsRing_NoDisc_HasPick()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            Assert.IsTrue(result.Rings[2].IsBelt);
            Assert.IsFalse(result.Rings[0].IsBelt);
            Assert.IsFalse(result.Bodies.Any(b => b.Ref.Equals(new BodyRef(0, 2, -1))));
            Assert.IsTrue(result.Picks.Any(p => p.Ref.Equals(new BodyRef(0, 2, -1))));
        }

        [Test]
        public void PickTargets_CoverEverything_ExceptEmptySlots()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            // 3 stars + 3 primary body discs + 2 moons + 1 belt + 1 companion body.
            Assert.AreEqual(10, result.Picks.Count);
            Assert.IsFalse(result.Picks.Any(p => p.Ref.Star == 0 && p.Ref.Slot == 0));
            Assert.IsFalse(result.Picks.Any(p => p.Ref.Star == 0 && p.Ref.Slot == 7));
            Assert.IsFalse(result.Picks.Any(p => p.Ref.Star == 2 && p.Ref.Slot == 0));
        }

        [Test]
        public void SettledBody_Flagged()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            Assert.IsTrue(result.Bodies.First(b => b.Ref.Equals(new BodyRef(0, 3, -1))).Settled);
            Assert.IsFalse(result.Bodies.First(b => b.Ref.Equals(new BodyRef(0, 1, -1))).Settled);
        }

        [Test]
        public void Moons_OrbitTheirParentDisc()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            var parent = result.Bodies.First(b => b.Ref.Equals(new BodyRef(0, 3, -1)));
            var moons = result.Bodies.Where(b =>
                b.Ref.Star == 0 && b.Ref.Slot == 3 && b.Ref.Moon >= 0).ToList();
            Assert.AreEqual(2, moons.Count);
            foreach (var moon in moons)
                Assert.AreEqual(parent.Radius + OrbitLayout.MoonOrbitPad,
                    Vector2.Distance(moon.Pos, parent.Pos), 1e-4f);
        }

        [Test]
        public void Bounds_ContainAllGeometry()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            foreach (var star in result.Stars) Assert.IsTrue(result.Bounds.Contains(star.Pos));
            foreach (var body in result.Bodies) Assert.IsTrue(result.Bounds.Contains(body.Pos));
            foreach (var ring in result.Rings)
            {
                Assert.IsTrue(result.Bounds.Contains(ring.Center + new Vector2(ring.Radius, 0f)));
                Assert.IsTrue(result.Bounds.Contains(ring.Center - new Vector2(ring.Radius, 0f)));
            }
        }

        [Test]
        public void PickAt_ResolvesNearestTarget()
        {
            var result = OrbitLayout.Compute(TestSystems.BuildTrinary());
            var gasGiant = result.Bodies.First(b => b.Ref.Equals(new BodyRef(0, 5, -1)));
            Assert.AreEqual(gasGiant.Ref, OrbitLayout.PickAt(result, gasGiant.Pos));
            Assert.AreEqual(new BodyRef(0, -1, -1),
                OrbitLayout.PickAt(result, new Vector2(0.01f, 0f)));
            Assert.IsNull(OrbitLayout.PickAt(result, result.Bounds.max + new Vector2(5f, 5f)));
        }

        [Test]
        public void GeneratedSystem_LayoutInvariantsHold()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 2 });
            service.Build();
            StarSystem? system = null;
            foreach (var cell in service.Skeleton.Cells)
            {
                foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius))
                {
                    system = service.Generate(hex).System;
                    if (system != null) break;
                }
                if (system != null) break;
            }
            Assert.IsNotNull(system, "fixture galaxy must contain at least one system");
            var result = OrbitLayout.Compute(system!);
            Assert.AreEqual(system!.Stars.Sum(s => s.Slots.Count), result.Rings.Count);
            Assert.AreEqual(system.Stars.Count, result.Stars.Count);
            foreach (var body in result.Bodies) Assert.IsTrue(result.Bounds.Contains(body.Pos));
        }
    }
}
```

- [ ] **Step 3: Run the compile gate to verify failure** — expected output: `error CS0246 ... 'OrbitLayout' could not be found` (and `BodyRef`, `OrbitLayoutResult`). This is the failing-test state for a batch workflow.

- [ ] **Step 4: Write the implementation** — `unity/Assets/Scripts/Atlas/OrbitLayout.cs`:

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Diagram address of a drawable element: Slot -1 = the star itself,
    /// Moon -1 = the slot body. Slot is the position in Star.Slots (coincides with
    /// OrbitSlot.Index today, but the list position is the contract).</summary>
    public readonly struct BodyRef : IEquatable<BodyRef>
    {
        public readonly int Star;
        public readonly int Slot;
        public readonly int Moon;

        public BodyRef(int star, int slot, int moon)
        {
            Star = star; Slot = slot; Moon = moon;
        }

        public bool Equals(BodyRef other) =>
            Star == other.Star && Slot == other.Slot && Moon == other.Moon;
        public override bool Equals(object? obj) => obj is BodyRef other && Equals(other);
        public override int GetHashCode() => (Star * 397 + Slot) * 397 + Moon;
        public override string ToString() => $"BodyRef({Star},{Slot},{Moon})";
    }

    public readonly struct RingSpec
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly bool IsBelt;
        /// <summary>The slot this ring belongs to (Moon = -1).</summary>
        public readonly BodyRef Ref;

        public RingSpec(Vector2 center, float radius, bool isBelt, BodyRef slotRef)
        {
            Center = center; Radius = radius; IsBelt = isBelt; Ref = slotRef;
        }
    }

    public readonly struct BandSpec
    {
        public readonly Vector2 Center;
        public readonly float Inner;
        public readonly float Outer;

        public BandSpec(Vector2 center, float inner, float outer)
        {
            Center = center; Inner = inner; Outer = outer;
        }
    }

    public readonly struct StarSpec
    {
        public readonly Vector2 Pos;
        public readonly float Radius;
        public readonly int StarIndex;
        public readonly string TypeId;

        public StarSpec(Vector2 pos, float radius, int starIndex, string typeId)
        {
            Pos = pos; Radius = radius; StarIndex = starIndex; TypeId = typeId;
        }
    }

    public readonly struct BodySpec
    {
        public readonly Vector2 Pos;
        public readonly float Radius;
        public readonly BodyRef Ref;
        public readonly BodyKind Kind;
        public readonly bool Settled;

        public BodySpec(Vector2 pos, float radius, BodyRef bodyRef, BodyKind kind, bool settled)
        {
            Pos = pos; Radius = radius; Ref = bodyRef; Kind = kind; Settled = settled;
        }
    }

    public readonly struct PickTarget
    {
        public readonly Vector2 Pos;
        public readonly float PickRadius;
        public readonly BodyRef Ref;

        public PickTarget(Vector2 pos, float pickRadius, BodyRef bodyRef)
        {
            Pos = pos; PickRadius = pickRadius; Ref = bodyRef;
        }
    }

    public sealed class OrbitLayoutResult
    {
        /// <summary>Primary slots first in slot order, then each companion's
        /// sub-rings grouped in Stars order.</summary>
        public List<RingSpec> Rings { get; } = new();
        public List<BandSpec> HabBands { get; } = new();
        public List<StarSpec> Stars { get; } = new();
        public List<BodySpec> Bodies { get; } = new();
        public List<PickTarget> Picks { get; } = new();
        public Rect Bounds { get; internal set; }
    }

    /// <summary>Pure nested-concentric geometry (orbit-diagram spec §4): no render
    /// types, edit-mode testable. All angles are decorative and derive from
    /// StableHash of the designation — a system always draws the same picture.</summary>
    public static class OrbitLayout
    {
        public const float R0 = 1.0f;               // innermost gap
        public const float DR = 0.5f;               // default ring gap
        public const float PrimaryDisc = 0.28f;
        public const float CompanionDisc = 0.16f;
        public const float BodyDiscBase = 0.06f;
        public const float BodyDiscPerSize = 0.016f;
        public const float MoonDisc = 0.035f;
        public const float MoonOrbitPad = 0.09f;
        public const float RingStroke = 0.02f;
        public const float SubDrMin = 0.11f;        // minimum companion sub-ring spacing
        public const float MinPickRadius = 0.12f;
        public const float BeltPickRadius = 0.3f;
        public const float HabHalfWidthFactor = 0.45f;

        private const uint OrbitChannel = 0xA1;
        private const uint MoonChannel = 0xA2;

        /// <summary>Widened gap around a companion slot: at least a doubled swath
        /// (its gravitational influence clears the primary's disc), more when the
        /// companion needs room for many sub-rings (spec §4).</summary>
        public static float CompanionClearance(int subSlotCount) =>
            Math.Max(2f * DR, (CompanionDisc + (subSlotCount + 1) * SubDrMin) / 0.9f);

        public static OrbitLayoutResult Compute(StarSystem system)
        {
            var result = new OrbitLayoutResult();
            if (system.Stars.Count == 0)
            {
                result.Bounds = new Rect(-1f, -1f, 2f, 2f);
                return result;
            }

            int primaryIndex = 0;
            for (int i = 0; i < system.Stars.Count; i++)
                if (system.Stars[i].CompanionSlotIndex == null) { primaryIndex = i; break; }
            var primary = system.Stars[primaryIndex];

            // Companion gap clearances, keyed by the primary slot each occupies.
            var clearance = new Dictionary<int, float>();
            for (int i = 0; i < system.Stars.Count; i++)
            {
                if (i == primaryIndex || system.Stars[i].CompanionSlotIndex is not { } rawSlot)
                    continue;
                if (primary.Slots.Count == 0) break;   // degenerate, never generated
                int slot = Math.Clamp(rawSlot, 0, primary.Slots.Count - 1);
                float widened = CompanionClearance(system.Stars[i].Slots.Count);
                clearance[slot] = clearance.TryGetValue(slot, out var existing)
                    ? Math.Max(existing, widened) : widened;
            }

            // Cumulative primary ring radii: gap i defaults to DR (R0 innermost),
            // widened on both sides of a companion slot.
            var radii = new float[primary.Slots.Count];
            float r = 0f;
            for (int i = 0; i < primary.Slots.Count; i++)
            {
                float gap = i == 0 ? R0 : DR;
                if (clearance.TryGetValue(i, out var into)) gap = Math.Max(gap, into);
                if (i > 0 && clearance.TryGetValue(i - 1, out var outOf)) gap = Math.Max(gap, outOf);
                r += gap;
                radii[i] = r;
            }

            result.Stars.Add(new StarSpec(Vector2.zero, PrimaryDisc, primaryIndex, primary.TypeId));
            result.Picks.Add(new PickTarget(Vector2.zero, PickRadiusFor(PrimaryDisc),
                new BodyRef(primaryIndex, -1, -1)));
            LayoutStar(result, system.Designation, primaryIndex, primary, Vector2.zero,
                radii, HabHalfWidthFactor * DR);

            for (int i = 0; i < system.Stars.Count; i++)
            {
                if (i == primaryIndex || system.Stars[i].CompanionSlotIndex is not { } rawSlot)
                    continue;
                if (primary.Slots.Count == 0) break;
                var companion = system.Stars[i];
                int slot = Math.Clamp(rawSlot, 0, primary.Slots.Count - 1);
                float angle = 2f * Mathf.PI * UnitHash(system.Designation, OrbitChannel, i, slot);
                var center = radii[slot] * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                int subSlots = companion.Slots.Count;
                float subDr = subSlots > 0
                    ? (0.9f * clearance[slot] - CompanionDisc) / (subSlots + 1) : 0f;
                var subRadii = new float[subSlots];
                for (int j = 0; j < subSlots; j++)
                    subRadii[j] = CompanionDisc + (j + 1) * subDr;

                result.Stars.Add(new StarSpec(center, CompanionDisc, i, companion.TypeId));
                result.Picks.Add(new PickTarget(center, PickRadiusFor(CompanionDisc),
                    new BodyRef(i, -1, -1)));
                LayoutStar(result, system.Designation, i, companion, center,
                    subRadii, HabHalfWidthFactor * subDr);
            }

            result.Bounds = ComputeBounds(result);
            return result;
        }

        /// <summary>Nearest pick target containing the world point; null when none.</summary>
        public static BodyRef? PickAt(OrbitLayoutResult layout, Vector2 world)
        {
            BodyRef? best = null;
            float bestDistance = float.MaxValue;
            foreach (var target in layout.Picks)
            {
                float distance = Vector2.Distance(world, target.Pos);
                if (distance <= target.PickRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target.Ref;
                }
            }
            return best;
        }

        private static void LayoutStar(OrbitLayoutResult result, string designation,
            int starIndex, Star star, Vector2 center, float[] radii, float habHalf)
        {
            int firstHab = -1, lastHab = -1;
            for (int i = 0; i < star.Slots.Count; i++)
                if (star.Slots[i].Band == OrbitBand.Habitable)
                {
                    if (firstHab < 0) firstHab = i;
                    lastHab = i;
                }
            if (firstHab >= 0)
                result.HabBands.Add(new BandSpec(center,
                    Math.Max(0f, radii[firstHab] - habHalf), radii[lastHab] + habHalf));

            for (int i = 0; i < star.Slots.Count; i++)
            {
                var slot = star.Slots[i];
                var body = slot.Body;
                bool isBelt = body != null && body.Kind == BodyKind.PlanetoidBelt;
                var slotRef = new BodyRef(starIndex, i, -1);
                result.Rings.Add(new RingSpec(center, radii[i], isBelt, slotRef));

                if (body == null) continue;
                float angle = 2f * Mathf.PI * UnitHash(designation, OrbitChannel, starIndex, i);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (isBelt)
                {
                    // Belts draw as their dashed ring; the pick target is a point on
                    // the ring at the slot angle with an enlarged radius (spec §4).
                    result.Picks.Add(new PickTarget(center + radii[i] * direction,
                        BeltPickRadius, slotRef));
                    continue;
                }

                float discRadius = BodyDiscBase + BodyDiscPerSize * body.Size;
                var pos = center + radii[i] * direction;
                result.Bodies.Add(new BodySpec(pos, discRadius, slotRef, body.Kind,
                    body.Settlement != Settlement.None));
                result.Picks.Add(new PickTarget(pos, PickRadiusFor(discRadius), slotRef));

                int moonCount = body.Satellites.Count;
                for (int m = 0; m < moonCount; m++)
                {
                    float start = UnitHash(designation, MoonChannel, starIndex, i);
                    float moonAngle = 2f * Mathf.PI * (start + (float)m / moonCount);
                    var moonPos = pos + (discRadius + MoonOrbitPad)
                        * new Vector2(Mathf.Cos(moonAngle), Mathf.Sin(moonAngle));
                    var moon = body.Satellites[m];
                    var moonRef = new BodyRef(starIndex, i, m);
                    result.Bodies.Add(new BodySpec(moonPos, MoonDisc, moonRef, moon.Kind,
                        moon.Settlement != Settlement.None));
                    result.Picks.Add(new PickTarget(moonPos, PickRadiusFor(MoonDisc), moonRef));
                }
            }
        }

        private static float PickRadiusFor(float discRadius) =>
            Math.Max(discRadius * 1.6f, MinPickRadius);

        private static float UnitHash(string designation, uint channel, int star, int slot)
        {
            ulong h = StableHash.Mix(Fnv1a(designation), channel,
                (ulong)(uint)star, (ulong)(uint)slot);
            return (float)((h >> 11) * (1.0 / (1UL << 53)));
        }

        private static ulong Fnv1a(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        private static Rect ComputeBounds(OrbitLayoutResult result)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            void Include(Vector2 p, float extent)
            {
                minX = Math.Min(minX, p.x - extent);
                maxX = Math.Max(maxX, p.x + extent);
                minY = Math.Min(minY, p.y - extent);
                maxY = Math.Max(maxY, p.y + extent);
            }

            foreach (var ring in result.Rings) Include(ring.Center, ring.Radius + RingStroke);
            foreach (var band in result.HabBands) Include(band.Center, band.Outer);
            foreach (var star in result.Stars) Include(star.Pos, star.Radius);
            foreach (var body in result.Bodies) Include(body.Pos, body.Radius);
            if (minX > maxX) return new Rect(-1f, -1f, 2f, 2f);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
```

- [ ] **Step 5: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 6: Run the test gate** — expected: `<test-run ... total="31" passed="31" failed="0" ...>`. Report the line + `LastWriteTime` verbatim.

- [ ] **Step 7: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/OrbitLayout.cs unity/Assets/Scripts/Atlas/OrbitLayout.cs.meta unity/Assets/Scripts/Atlas/Tests/TestSystems.cs unity/Assets/Scripts/Atlas/Tests/TestSystems.cs.meta unity/Assets/Scripts/Atlas/Tests/OrbitLayoutTests.cs unity/Assets/Scripts/Atlas/Tests/OrbitLayoutTests.cs.meta
git commit -m "feat: OrbitLayout pure nested-concentric geometry + BodyRef"
```

---

### Task 2: `OrbitPalette` + tests

**Files:**
- Create: `unity/Assets/Scripts/Atlas/OrbitPalette.cs`
- Create: `unity/Assets/Scripts/Atlas/Tests/OrbitPaletteTests.cs`

**Interfaces:**
- Consumes: `BodyKind` from `StarGen.Core.Model`; `StarTypes.Table.Entries` from `StarGen.Core.Content` (test only).
- Produces: `OrbitPalette.StarColor(string typeId) → Color32`, `OrbitPalette.BodyColor(BodyKind) → Color32`, statics `Fallback, Moon, Ring, HabBand, SettledOutline` (all `Color32`). Task 3's `Compose` and Task 4's view consume these.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/OrbitPaletteTests.cs`:

```csharp
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Content;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class OrbitPaletteTests
    {
        [Test]
        public void EveryStarType_HasANonFallbackColor()
        {
            foreach (var (def, _) in StarTypes.Table.Entries)
                Assert.AreNotEqual(OrbitPalette.Fallback, OrbitPalette.StarColor(def.Id), def.Id);
        }

        [Test]
        public void EveryBodyKind_HasANonFallbackColor()
        {
            foreach (BodyKind kind in System.Enum.GetValues(typeof(BodyKind)))
                Assert.AreNotEqual(OrbitPalette.Fallback, OrbitPalette.BodyColor(kind),
                    kind.ToString());
        }

        [Test]
        public void UnknownStarType_FallsBack() =>
            Assert.AreEqual(OrbitPalette.Fallback, OrbitPalette.StarColor("mystery_type"));
    }
}
```

- [ ] **Step 2: Run the compile gate to verify failure** — expected: `error CS0246 ... 'OrbitPalette' could not be found`.

- [ ] **Step 3: Write the implementation** — `unity/Assets/Scripts/Atlas/OrbitPalette.cs` (colors verbatim from spec §5):

```csharp
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Pure element→color mapping for the orbit diagram (orbit-diagram
    /// spec §5), sibling of LayerPalette. Settled outline matches the data
    /// panel's accent.</summary>
    public static class OrbitPalette
    {
        public static readonly Color32 Fallback = new(0xFF, 0xFF, 0xFF, 255);
        public static readonly Color32 Moon = new(0xB9, 0xBF, 0xD0, 255);
        public static readonly Color32 Ring = new(0x26, 0x2C, 0x3F, 255);
        public static readonly Color32 HabBand = new(0x3F, 0xBF, 0x7F, 26);   // 0.10 alpha
        public static readonly Color32 SettledOutline = new(0xFF, 0xBF, 0x4F, 255);

        public static Color32 StarColor(string typeId) => typeId switch
        {
            "ember_dwarf" => new Color32(0xFF, 0x8A, 0x5C, 255),
            "amber_dwarf" => new Color32(0xFF, 0xB3, 0x47, 255),
            "gold_main" => new Color32(0xFF, 0xD0, 0x66, 255),
            "white_blaze" => new Color32(0xEA, 0xF2, 0xFF, 255),
            "blue_titan" => new Color32(0x7F, 0xB8, 0xFF, 255),
            "ashen_remnant" => new Color32(0x9A, 0xA0, 0xAE, 255),
            "collapsed_core" => new Color32(0xB4, 0x8A, 0xFF, 255),
            _ => Fallback,
        };

        public static Color32 BodyColor(BodyKind kind) => kind switch
        {
            BodyKind.RockyWorld => new Color32(0xC9, 0xA0, 0x6A, 255),
            BodyKind.IceWorld => new Color32(0xA8, 0xD8, 0xE8, 255),
            BodyKind.GasGiant => new Color32(0xE0, 0x88, 0x40, 255),
            BodyKind.PlanetoidBelt => new Color32(0x9A, 0x8F, 0x7A, 255),
            BodyKind.Wreckage => new Color32(0x8A, 0x5C, 0x5C, 255),
            _ => Fallback,
        };
    }
}
```

- [ ] **Step 4: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 5: Run the test gate** — expected: `<test-run ... total="34" passed="34" failed="0" ...>`. Report line + timestamp.

- [ ] **Step 6: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/OrbitPalette.cs unity/Assets/Scripts/Atlas/OrbitPalette.cs.meta unity/Assets/Scripts/Atlas/Tests/OrbitPaletteTests.cs unity/Assets/Scripts/Atlas/Tests/OrbitPaletteTests.cs.meta
git commit -m "feat: OrbitPalette star/body/diagram colors"
```

---

### Task 3: `OrbitMeshBuilder` + `Compose` + tests

**Files:**
- Create: `unity/Assets/Scripts/Atlas/OrbitMeshBuilder.cs`
- Create: `unity/Assets/Scripts/Atlas/Tests/OrbitMeshBuilderTests.cs`

**Interfaces:**
- Consumes: `OrbitLayoutResult`/`BodyRef`/`OrbitLayout.RingStroke` (Task 1), `OrbitPalette` (Task 2).
- Produces: instance methods `AddRing(Vector2, float, float, Color32)`, `AddDashedRing(Vector2, float, float, Color32, BodyRef? key = null)`, `AddDisc(Vector2, float, Color32, BodyRef? key = null)`, `AddAnnulus(Vector2, float, float, Color32)`, `TryGetRange(BodyRef, out int start, out int count)`, `Build() → Mesh`; statics `Recolor(Mesh, int start, int count, Color32)` and `Compose(OrbitLayoutResult, Dictionary<BodyRef, Color32> baseColors) → OrbitMeshBuilder`; consts `RingSegments = 96, DashCount = 48, DiscSegments = 24`. Task 4's `SystemView` calls `Compose`/`Build`/`TryGetRange`/`Recolor`.

- [ ] **Step 1: Write the failing tests** — `unity/Assets/Scripts/Atlas/Tests/OrbitMeshBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using StarGen.Atlas;
using UnityEngine;

namespace StarGen.Atlas.Tests
{
    public class OrbitMeshBuilderTests
    {
        [Test]
        public void Disc_VertexAndIndexCounts()
        {
            var builder = new OrbitMeshBuilder();
            builder.AddDisc(Vector2.zero, 1f, new Color32(255, 0, 0, 255), new BodyRef(0, 0, -1));
            var mesh = builder.Build();
            Assert.AreEqual(OrbitMeshBuilder.DiscSegments + 2, mesh.vertexCount);
            Assert.AreEqual(OrbitMeshBuilder.DiscSegments * 3, mesh.triangles.Length);
        }

        [Test]
        public void Ring_VertexAndIndexCounts()
        {
            var builder = new OrbitMeshBuilder();
            builder.AddRing(Vector2.zero, 1f, 0.02f, new Color32(255, 255, 255, 255));
            var mesh = builder.Build();
            Assert.AreEqual((OrbitMeshBuilder.RingSegments + 1) * 2, mesh.vertexCount);
            Assert.AreEqual(OrbitMeshBuilder.RingSegments * 6, mesh.triangles.Length);
        }

        [Test]
        public void Ranges_AreDisjoint_AndRecolorInPlace()
        {
            var builder = new OrbitMeshBuilder();
            var red = new Color32(255, 0, 0, 255);
            var keyA = new BodyRef(0, 1, -1);
            var keyB = new BodyRef(0, 2, -1);
            builder.AddDisc(new Vector2(-1f, 0f), 0.5f, red, keyA);
            builder.AddDisc(new Vector2(1f, 0f), 0.5f, red, keyB);
            Assert.IsTrue(builder.TryGetRange(keyA, out int startA, out int countA));
            Assert.IsTrue(builder.TryGetRange(keyB, out int startB, out int countB));
            Assert.AreEqual(startA + countA, startB);   // appended contiguously, disjoint

            var mesh = builder.Build();
            var green = new Color32(0, 255, 0, 255);
            OrbitMeshBuilder.Recolor(mesh, startB, countB, green);
            var colors = mesh.colors32;
            for (int v = startA; v < startA + countA; v++) Assert.AreEqual(red, colors[v]);
            for (int v = startB; v < startB + countB; v++) Assert.AreEqual(green, colors[v]);
        }

        [Test]
        public void ComposedDiagram_HasFiniteVertices_AndSelectableElements()
        {
            var layout = OrbitLayout.Compute(TestSystems.BuildTrinary());
            var baseColors = new Dictionary<BodyRef, Color32>();
            var builder = OrbitMeshBuilder.Compose(layout, baseColors);
            var mesh = builder.Build();
            Assert.Greater(mesh.vertexCount, 0);
            foreach (var v in mesh.vertices)
                Assert.IsTrue(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z));
            // Every pick target must be recolorable (selection contract).
            foreach (var pick in layout.Picks)
            {
                Assert.IsTrue(builder.TryGetRange(pick.Ref, out _, out int count), pick.Ref.ToString());
                Assert.Greater(count, 0);
                Assert.IsTrue(baseColors.ContainsKey(pick.Ref));
            }
        }
    }
}
```

- [ ] **Step 2: Run the compile gate to verify failure** — expected: `error CS0246 ... 'OrbitMeshBuilder' could not be found`.

- [ ] **Step 3: Write the implementation** — `unity/Assets/Scripts/Atlas/OrbitMeshBuilder.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.Atlas
{
    /// <summary>Accumulates the orbit diagram's primitives into one vertex-colored
    /// mesh (orbit-diagram spec §5), recording the vertex range per BodyRef so
    /// single elements recolor in place (HexMeshBuilder.RecolorOne idiom).
    /// Sprites/Default culls neither face, so triangle winding is not significant.</summary>
    public sealed class OrbitMeshBuilder
    {
        public const int RingSegments = 96;
        public const int DashCount = 48;
        public const int DiscSegments = 24;
        private const float SettledOutlinePad = 0.03f;

        private readonly List<Vector3> _vertices = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _triangles = new();
        private readonly Dictionary<BodyRef, (int Start, int Count)> _ranges = new();

        /// <summary>Draw order per spec §5: hab annuli → rings → stars → bodies and
        /// moons → settled outlines. Fills baseColors for selection recolor.</summary>
        public static OrbitMeshBuilder Compose(OrbitLayoutResult layout,
            Dictionary<BodyRef, Color32> baseColors)
        {
            var builder = new OrbitMeshBuilder();
            foreach (var band in layout.HabBands)
                builder.AddAnnulus(band.Center, band.Inner, band.Outer, OrbitPalette.HabBand);
            foreach (var ring in layout.Rings)
            {
                if (ring.IsBelt)
                {
                    var beltColor = OrbitPalette.BodyColor(StarGen.Core.Model.BodyKind.PlanetoidBelt);
                    builder.AddDashedRing(ring.Center, ring.Radius, OrbitLayout.RingStroke,
                        beltColor, ring.Ref);
                    baseColors[ring.Ref] = beltColor;
                }
                else
                {
                    builder.AddRing(ring.Center, ring.Radius, OrbitLayout.RingStroke,
                        OrbitPalette.Ring);
                }
            }
            foreach (var star in layout.Stars)
            {
                var color = OrbitPalette.StarColor(star.TypeId);
                var key = new BodyRef(star.StarIndex, -1, -1);
                builder.AddDisc(star.Pos, star.Radius, color, key);
                baseColors[key] = color;
            }
            foreach (var body in layout.Bodies)
            {
                var color = body.Ref.Moon >= 0 ? OrbitPalette.Moon : OrbitPalette.BodyColor(body.Kind);
                builder.AddDisc(body.Pos, body.Radius, color, body.Ref);
                baseColors[body.Ref] = color;
            }
            foreach (var body in layout.Bodies)
                if (body.Settled)
                    builder.AddRing(body.Pos, body.Radius + SettledOutlinePad,
                        OrbitLayout.RingStroke, OrbitPalette.SettledOutline);
            return builder;
        }

        public void AddRing(Vector2 center, float radius, float stroke, Color32 color) =>
            AddArcStrip(center, radius, stroke, 0f, 2f * Mathf.PI, RingSegments, color);

        public void AddDashedRing(Vector2 center, float radius, float stroke, Color32 color,
            BodyRef? key = null)
        {
            int start = _vertices.Count;
            float slice = 2f * Mathf.PI / DashCount;
            for (int d = 0; d < DashCount; d++)
                AddArcStrip(center, radius, stroke, d * slice, d * slice + slice * 0.55f, 2, color);
            if (key is { } k) _ranges[k] = (start, _vertices.Count - start);
        }

        public void AddDisc(Vector2 center, float radius, Color32 color, BodyRef? key = null)
        {
            int start = _vertices.Count;
            _vertices.Add(new Vector3(center.x, center.y, 0f));
            _colors.Add(color);
            for (int i = 0; i <= DiscSegments; i++)
            {
                float a = 2f * Mathf.PI * i / DiscSegments;
                _vertices.Add(new Vector3(center.x + radius * Mathf.Cos(a),
                    center.y + radius * Mathf.Sin(a), 0f));
                _colors.Add(color);
            }
            for (int i = 0; i < DiscSegments; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + 1 + i);
                _triangles.Add(start + 2 + i);
            }
            if (key is { } k) _ranges[k] = (start, _vertices.Count - start);
        }

        public void AddAnnulus(Vector2 center, float inner, float outer, Color32 color) =>
            AddArcStrip(center, (inner + outer) * 0.5f, outer - inner, 0f, 2f * Mathf.PI,
                RingSegments, color);

        public bool TryGetRange(BodyRef key, out int start, out int count)
        {
            if (_ranges.TryGetValue(key, out var range))
            {
                start = range.Start;
                count = range.Count;
                return true;
            }
            start = 0;
            count = 0;
            return false;
        }

        public Mesh Build()
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Recolor(Mesh mesh, int start, int count, Color32 color)
        {
            var colors = mesh.colors32;
            for (int v = start; v < start + count; v++) colors[v] = color;
            mesh.SetColors(colors);
        }

        private void AddArcStrip(Vector2 center, float radius, float stroke,
            float angleFrom, float angleTo, int segments, Color32 color)
        {
            float rIn = Mathf.Max(0f, radius - stroke * 0.5f);
            float rOut = radius + stroke * 0.5f;
            int baseVertex = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(angleFrom, angleTo, (float)i / segments);
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                _vertices.Add(new Vector3(center.x + cos * rIn, center.y + sin * rIn, 0f));
                _vertices.Add(new Vector3(center.x + cos * rOut, center.y + sin * rOut, 0f));
                _colors.Add(color);
                _colors.Add(color);
            }
            for (int i = 0; i < segments; i++)
            {
                int v = baseVertex + i * 2;
                _triangles.Add(v); _triangles.Add(v + 2); _triangles.Add(v + 1);
                _triangles.Add(v + 1); _triangles.Add(v + 2); _triangles.Add(v + 3);
            }
        }
    }
}
```

- [ ] **Step 4: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 5: Run the test gate** — expected: `<test-run ... total="38" passed="38" failed="0" ...>`. Report line + timestamp.

- [ ] **Step 6: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/OrbitMeshBuilder.cs unity/Assets/Scripts/Atlas/OrbitMeshBuilder.cs.meta unity/Assets/Scripts/Atlas/Tests/OrbitMeshBuilderTests.cs unity/Assets/Scripts/Atlas/Tests/OrbitMeshBuilderTests.cs.meta
git commit -m "feat: OrbitMeshBuilder single-mesh diagram primitives with recolor ranges"
```

---

### Task 4: `SystemView` MonoBehaviour

**Files:**
- Create: `unity/Assets/Scripts/Atlas/SystemView.cs`

**Interfaces:**
- Consumes: `OrbitLayout.Compute`/`PickAt` (Task 1), `OrbitMeshBuilder.Compose`/`Build`/`TryGetRange`/`Recolor` (Task 3), `LayerPalette.Highlight` (existing).
- Produces: `SystemView : MonoBehaviour` with `Show(StarSystem)`, `Pick(Vector2 screenPos, Camera) → BodyRef?`, `SetSelected(BodyRef?)`, `MapBounds → Bounds`. Task 7 wires it into the controller and scene.

No unit tests: view MonoBehaviours are UI plumbing exercised by Task 8's live acceptance (same convention as `GalaxyView`/`CellView`). Gate is compile + existing suite still green.

- [ ] **Step 1: Write the implementation** — `unity/Assets/Scripts/Atlas/SystemView.cs`:

```csharp
using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>One system as a nested-concentric orbit diagram in a single
    /// procedural mesh (orbit-diagram spec §5). Same scaffolding as CellView:
    /// Show/Pick/MapBounds, plus per-element selection recolor.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SystemView : MonoBehaviour
    {
        private Mesh? _mesh;
        private OrbitLayoutResult? _layout;
        private OrbitMeshBuilder? _builder;
        private readonly Dictionary<BodyRef, Color32> _baseColors = new();
        private BodyRef? _selected;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(StarSystem system)
        {
            _selected = null;
            _baseColors.Clear();
            _layout = OrbitLayout.Compute(system);
            _builder = OrbitMeshBuilder.Compose(_layout, _baseColors);
            if (_mesh != null) Destroy(_mesh);
            _mesh = _builder.Build();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public BodyRef? Pick(Vector2 screenPos, Camera cam)
        {
            if (_layout == null) return null;
            var world = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var local = world - transform.position;
            return OrbitLayout.PickAt(_layout, new Vector2(local.x, local.y));
        }

        public void SetSelected(BodyRef? key)
        {
            if (_mesh == null || _builder == null || Nullable.Equals(_selected, key)) return;
            if (_selected is { } previous
                && _builder.TryGetRange(previous, out int prevStart, out int prevCount))
                OrbitMeshBuilder.Recolor(_mesh, prevStart, prevCount, _baseColors[previous]);
            _selected = null;
            if (key is { } next && _builder.TryGetRange(next, out int nextStart, out int nextCount))
            {
                OrbitMeshBuilder.Recolor(_mesh, nextStart, nextCount,
                    LayerPalette.Highlight(_baseColors[next]));
                _selected = next;
            }
        }
    }
}
```

- [ ] **Step 2: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 3: Run the test gate** — expected: `<test-run ... total="38" passed="38" failed="0" ...>` (unchanged count). Report line + timestamp.

- [ ] **Step 4: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/SystemView.cs unity/Assets/Scripts/Atlas/SystemView.cs.meta
git commit -m "feat: SystemView single-mesh orbit-diagram view"
```

---

### Task 5: Navigator `System` screen + tests

**Files:**
- Modify: `unity/Assets/Scripts/Atlas/AtlasNavigator.cs`
- Modify: `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs`

**Interfaces:**
- Produces: `AtlasScreen.System` enum member; `AtlasNavigator.EnterSystem()` (throws `InvalidOperationException` unless `Screen == Cell && SelectedHex != null`); `Back()` from System → Cell keeping `SelectedHex`; `DrillToCell` legal from System (clears hex, lands on Cell). Task 7's controller calls all of these.

- [ ] **Step 1: Add the failing tests** — append these four tests inside the existing `AtlasNavigatorTests` class in `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs` (leave the four existing tests untouched):

```csharp
        [Test]
        public void EnterSystem_FromCellWithHex_LandsOnSystemScreen()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            Assert.AreEqual(AtlasScreen.System, nav.Screen);
            Assert.AreEqual(new HexCoordinate(11, -5), nav.SelectedHex);
            Assert.AreEqual(new HexCoordinate(1, 0), nav.SelectedCell);
        }

        [Test]
        public void EnterSystem_Throws_OffCellOrWithoutHex()
        {
            var nav = new AtlasNavigator();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());
            nav.EnterGalaxy();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());
            nav.DrillToCell(new HexCoordinate(1, 0));
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());   // no hex
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());   // already there
        }

        [Test]
        public void Back_FromSystem_ReturnsToCell_KeepingHexSelection()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            nav.Back();
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.AreEqual(new HexCoordinate(11, -5), nav.SelectedHex);   // panel context survives
            nav.Back();   // now clears the hex, per existing behavior
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
        }

        [Test]
        public void DrillToCell_FromSystem_ClearsHex_AndLandsOnCell()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            nav.DrillToCell(new HexCoordinate(1, 0));   // breadcrumb "Cell" crumb path
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(new HexCoordinate(1, 0), nav.SelectedCell);
        }
```

- [ ] **Step 2: Run the compile gate to verify failure** — expected: `error CS0117 ... 'AtlasScreen' does not contain a definition for 'System'` and `error CS1061 ... 'EnterSystem'`.

- [ ] **Step 3: Modify the navigator** — in `unity/Assets/Scripts/Atlas/AtlasNavigator.cs`:

Change the enum line:

```csharp
    public enum AtlasScreen { Setup, Galaxy, Cell, System }
```

Change `DrillToCell`'s guard (first statement of the method) to allow the breadcrumb path from System:

```csharp
        public void DrillToCell(HexCoordinate cellCoord)
        {
            if (Screen != AtlasScreen.Galaxy && Screen != AtlasScreen.Cell
                && Screen != AtlasScreen.System)
                throw new InvalidOperationException($"cannot drill to a cell from {Screen}");
            Screen = AtlasScreen.Cell;
            SelectedCell = cellCoord;
            SelectedHex = null;
            Changed?.Invoke();
        }
```

Add `EnterSystem` after `SelectHex`:

```csharp
        public void EnterSystem()
        {
            if (Screen != AtlasScreen.Cell || SelectedHex == null)
                throw new InvalidOperationException($"cannot enter a system from {Screen}");
            Screen = AtlasScreen.System;
            Changed?.Invoke();
        }
```

Replace `Back()` with (System branch first — it must NOT clear the hex):

```csharp
        public void Back()
        {
            if (Screen == AtlasScreen.System) { Screen = AtlasScreen.Cell; }   // hex survives
            else if (SelectedHex != null) { SelectedHex = null; }
            else if (Screen == AtlasScreen.Cell) { Screen = AtlasScreen.Galaxy; SelectedCell = null; }
            else if (Screen == AtlasScreen.Galaxy) { Screen = AtlasScreen.Setup; }
            else return;   // Setup: no-op, no event
            Changed?.Invoke();
        }
```

`EnterGalaxy`, `SelectHex`, `ClearHexSelection`, `Reset` are unchanged.

- [ ] **Step 4: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 5: Run the test gate** — expected: `<test-run ... total="42" passed="42" failed="0" ...>`. Report line + timestamp.

- [ ] **Step 6: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/AtlasNavigator.cs unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs
git commit -m "feat: AtlasScreen.System drill level in the navigator"
```

---

### Task 6: `SystemPanel` wrapper + panel registration + tests

**Files:**
- Create: `unity/Assets/Scripts/Atlas/SystemPanel.cs`
- Modify: `unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs` (full replacement below)
- Modify: `unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs`

**Interfaces:**
- Consumes: `BodyRef` (Task 1), `TestSystems.BuildTrinary()` (Task 1, tests).
- Produces: `SystemPanelBuilder.Build(HexResult result, double density = double.NaN, Action? onOpenSystem = null) → SystemPanel` (**return type changes** from `VisualElement`); `SystemPanel.Root → VisualElement`, `SystemPanel.Highlight(BodyRef?)`, `SystemPanel.HasRow(BodyRef) → bool`, `SystemPanel.HighlightBg` (public static Color); `SystemPanelBuilder.KindName(BodyKind) → string` (now public, reused by Task 7's tooltips). Task 7 updates the controller call sites (`.Root` + callback).

- [ ] **Step 1: Write the failing tests** — replace `unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs` with:

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
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            service.Build();
            return service;
        }

        private static string AllText(VisualElement root) =>
            string.Join("\n", root.Query<Label>().ToList().Select(l => l.text));

        private static HexResult TrinaryResult() =>
            new HexResult(new HexCoordinate(0, 0), TestSystems.BuildTrinary());

        private static int TintedCount(VisualElement root) =>
            root.Query<Label>().ToList()
                .Count(l => l.style.backgroundColor == new StyleColor(SystemPanel.HighlightBg));

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
            var text = AllText(panel.Root);
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
            var text = AllText(panel.Root);
            StringAssert.Contains("no system", text);
            StringAssert.Contains("0.42", text);
            StringAssert.Contains(Core.Naming.Designation.For(empty.Value), text);
        }

        [Test]
        public void Rows_RegisterForStarsBodiesAndMoons_NotEmptySlots()
        {
            var panel = SystemPanelBuilder.Build(TrinaryResult());
            Assert.IsTrue(panel.HasRow(new BodyRef(0, -1, -1)));   // primary header
            Assert.IsTrue(panel.HasRow(new BodyRef(1, -1, -1)));   // companion header
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 1, -1)));    // rocky world
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 2, -1)));    // belt
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 3, 0)));     // first moon
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 3, 1)));     // second moon
            Assert.IsTrue(panel.HasRow(new BodyRef(1, 1, -1)));    // companion's ice world
            Assert.IsFalse(panel.HasRow(new BodyRef(0, 0, -1)));   // empty slot
            Assert.IsFalse(panel.HasRow(new BodyRef(0, 7, -1)));   // empty slot
        }

        [Test]
        public void OpenSystemButton_PresentOnlyWithCallback()
        {
            bool clicked = false;
            var withButton = SystemPanelBuilder.Build(TrinaryResult(),
                onOpenSystem: () => clicked = true);
            var button = withButton.Root.Q<Button>();
            Assert.IsNotNull(button);
            Assert.AreEqual("Open system", button.text);

            var withoutButton = SystemPanelBuilder.Build(TrinaryResult());
            Assert.IsNull(withoutButton.Root.Q<Button>());
            Assert.IsFalse(clicked);
        }

        [Test]
        public void Highlight_TintsExactlyOneRow_MovesAndClears()
        {
            var panel = SystemPanelBuilder.Build(TrinaryResult());
            Assert.AreEqual(0, TintedCount(panel.Root));
            panel.Highlight(new BodyRef(0, 1, -1));
            Assert.AreEqual(1, TintedCount(panel.Root));
            panel.Highlight(new BodyRef(0, 3, 0));
            Assert.AreEqual(1, TintedCount(panel.Root));   // previous row cleared
            panel.Highlight(null);
            Assert.AreEqual(0, TintedCount(panel.Root));
        }
    }
}
```

- [ ] **Step 2: Run the compile gate to verify failure** — expected: `error CS0246 ... 'SystemPanel'` and `error CS1061` on `.Root` / `HasRow`.

- [ ] **Step 3: Create `unity/Assets/Scripts/Atlas/SystemPanel.cs`:**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>Panel root plus BodyRef→row mapping so the orbit diagram can
    /// highlight the line it just picked (orbit-diagram spec §6).</summary>
    public sealed class SystemPanel
    {
        public static readonly Color HighlightBg = new(1.0f, 0.75f, 0.31f, 0.18f);

        private readonly ScrollView _scroll;
        private readonly Dictionary<BodyRef, VisualElement> _rows = new();
        private BodyRef? _highlighted;

        public VisualElement Root => _scroll;

        internal SystemPanel(ScrollView scroll) => _scroll = scroll;

        internal void Register(BodyRef key, VisualElement row) => _rows[key] = row;

        public bool HasRow(BodyRef key) => _rows.ContainsKey(key);

        public void Highlight(BodyRef? key)
        {
            if (_highlighted is { } previous && _rows.TryGetValue(previous, out var previousRow))
                previousRow.style.backgroundColor = new StyleColor(Color.clear);
            _highlighted = null;
            if (key is { } next && _rows.TryGetValue(next, out var row))
            {
                row.style.backgroundColor = new StyleColor(HighlightBg);
                // ScrollTo needs a live layout pass; headless edit-mode panels
                // have none, so scrolling is runtime-only.
                if (_scroll.panel != null) _scroll.ScrollTo(row);
                _highlighted = next;
            }
        }
    }
}
```

- [ ] **Step 4: Replace `unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs` entirely with:**

```csharp
using System;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>SystemFormatter's content as structured UI elements (atlas spec §3),
    /// wrapped in a SystemPanel that maps BodyRefs to rows (orbit-diagram spec §6).</summary>
    public static class SystemPanelBuilder
    {
        private static readonly Color Text = new(0.85f, 0.85f, 0.88f);
        private static readonly Color Dim = new(0.55f, 0.55f, 0.62f);
        private static readonly Color Accent = new(1.0f, 0.75f, 0.31f);

        public static SystemPanel Build(HexResult result, double density = double.NaN,
            Action? onOpenSystem = null)
        {
            var scroll = new ScrollView();
            var panel = new SystemPanel(scroll);
            var root = scroll.contentContainer;

            if (result.System == null)
            {
                root.Add(Line(Designation.For(result.Coordinate), 16, Accent, bold: true));
                root.Add(Line("no system", 13, Dim));
                if (!double.IsNaN(density)) root.Add(Line($"density {density:F2}", 12, Dim));
                return panel;
            }

            var system = result.System;
            if (onOpenSystem != null)
            {
                var openButton = new Button(onOpenSystem) { text = "Open system" };
                openButton.style.marginBottom = 6;
                root.Add(openButton);
            }

            root.Add(Line(system.GivenName ?? system.Designation, 16, Accent, bold: true));
            root.Add(Line($"{system.Designation} · {system.Arrangement.ToString().ToLowerInvariant()}"
                + (system.OverlayId != null ? $" · overlay: {system.OverlayId}" : ""), 12, Dim));
            foreach (var tag in system.Tags) root.Add(Line($"! {tag}", 12, Accent));

            for (int i = 0; i < system.Stars.Count; i++)
            {
                var star = system.Stars[i];
                string companion = star.CompanionSlotIndex is { } cs ? $" (slot {cs})" : "";
                var header = Line($"Star {(char)('A' + i)} — {star.TypeName}, "
                    + star.Age.ToString().ToLowerInvariant() + companion, 14, Text, bold: true);
                panel.Register(new BodyRef(i, -1, -1), header);
                root.Add(header);
                for (int s = 0; s < star.Slots.Count; s++)
                    AddSlot(panel, root, i, s, star.Slots[s]);
            }
            return panel;
        }

        public static string KindName(BodyKind kind) => kind switch
        {
            BodyKind.RockyWorld => "rocky world",
            BodyKind.IceWorld => "ice world",
            BodyKind.GasGiant => "gas giant",
            BodyKind.PlanetoidBelt => "planetoid belt",
            _ => "wreckage field",
        };

        private static void AddSlot(SystemPanel panel, VisualElement root,
            int starIndex, int slotIndex, OrbitSlot slot)
        {
            string band = slot.Band.ToString().ToLowerInvariant();
            if (slot.Body == null)
            {
                root.Add(Line($"  {slot.Index} [{band}] —", 12, Dim));
                return;
            }
            AddBody(panel, root, new BodyRef(starIndex, slotIndex, -1), slot.Body,
                $"  {slot.Index} [{band}] ");
            for (int m = 0; m < slot.Body.Satellites.Count; m++)
                AddBody(panel, root, new BodyRef(starIndex, slotIndex, m),
                    slot.Body.Satellites[m], $"      moon {(char)('a' + m)}: ");
        }

        private static void AddBody(SystemPanel panel, VisualElement root, BodyRef key,
            Body body, string prefix)
        {
            string text = prefix + KindName(body.Kind)
                + (body.Name != null ? $" \"{body.Name}\"" : "")
                + (body.Size > 0 ? $" · size {body.Size}" : "");
            if (body.Kind == BodyKind.RockyWorld || body.Kind == BodyKind.IceWorld)
            {
                text += $" · {body.Atmosphere.ToString().ToLowerInvariant()}";
                if (body.Hydrographics > 0) text += $" · oceans {body.Hydrographics}%";
                if (body.Biosphere != Biosphere.Barren)
                    text += $" · {body.Biosphere.ToString().ToLowerInvariant()}";
            }
            var row = Line(text, 12, Text);
            panel.Register(key, row);
            root.Add(row);
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

Note: `AtlasController.RenderCellScreen` still calls `SystemPanelBuilder.Build(result, density)` and passes the result to `ui.ShowSystemPanel(...)` — that call site now needs `.Root`. **Fix it in this task** so the assembly compiles: in `unity/Assets/Scripts/Atlas/AtlasController.cs` change

```csharp
                ui.ShowSystemPanel(SystemPanelBuilder.Build(result, density));
```

to

```csharp
                ui.ShowSystemPanel(SystemPanelBuilder.Build(result, density).Root);
```

(The callback and highlight wiring land in Task 7.)

- [ ] **Step 5: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 6: Run the test gate** — expected: `<test-run ... total="45" passed="45" failed="0" ...>`. Report line + timestamp.

- [ ] **Step 7: Commit**

```powershell
git add unity/Assets/Scripts/Atlas/SystemPanel.cs unity/Assets/Scripts/Atlas/SystemPanel.cs.meta unity/Assets/Scripts/Atlas/SystemPanelBuilder.cs unity/Assets/Scripts/Atlas/Tests/SystemPanelBuilderTests.cs unity/Assets/Scripts/Atlas/AtlasController.cs
git commit -m "feat: SystemPanel wrapper with BodyRef row highlight + open-system button"
```

---

### Task 7: Controller System screen + scene wiring + acceptance menu items

**Files:**
- Modify: `unity/Assets/Scripts/Atlas/AtlasController.cs` (full replacement below)
- Modify: `unity/Assets/Editor/AtlasSceneSetup.cs`
- Modify: `unity/Assets/Editor/AtlasAcceptance.cs`
- Modify: `unity/Assets/Scenes/Atlas.unity` (regenerated headlessly, then committed)

**Interfaces:**
- Consumes: everything from Tasks 1–6 (`SystemView`, `EnterSystem`, `SystemPanel.Highlight`, `SystemPanelBuilder.KindName`).
- Produces: the complete System screen behavior Task 8 accepts live; acceptance menu items `StarGen/Acceptance/Open System`, `StarGen/Acceptance/Select Binary Or Trinary Hex`, `StarGen/Acceptance/Dump System Layout`.

Behavior contract (spec §3/§6): second click on the selected hex with a system → `EnterSystem` (empty hex second click = no-op); "Open system" button on the cell-screen panel does the same; breadcrumb depth 2 (`Cell` crumb) → `DrillToCell(SelectedCell)` clearing the hex; System screen shows diagram + panel + `{name} · {designation} · {arrangement}` via `ShowCellHud`; hover → tooltip, click → diagram selection recolor + panel highlight; Esc → Back to Cell keeping the hex.

- [ ] **Step 1: Replace `unity/Assets/Scripts/Atlas/AtlasController.cs` entirely with:**

```csharp
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Wires navigator + service + views + UI (atlas spec §3). All state
    /// decisions live in AtlasNavigator; this class only renders and routes input.</summary>
    public sealed class AtlasController : MonoBehaviour
    {
        [SerializeField] private GalaxyView galaxyView = null!;
        [SerializeField] private CellView cellView = null!;
        [SerializeField] private SystemView systemView = null!;
        [SerializeField] private AtlasUI ui = null!;
        [SerializeField] private Camera mainCamera = null!;

        private readonly AtlasNavigator _navigator = new();
        private GalaxyService? _service;
        private GalaxyService? _previewService;
        private GalaxyConfig? _pendingPreview;

        /// <summary>Automation surface: lets editor acceptance tooling drive the
        /// real navigation paths (map clicks are the only input it can't fake).</summary>
        public AtlasNavigator Navigator => _navigator;
        public GalaxyService? Service => _service;
        private AtlasLayer _layer = AtlasLayer.Polity;
        private ulong _seed;
        private HexCoordinate? _lastHoverPick;
        private bool _hoverValid;
        private BodyRef? _lastSystemPick;
        private bool _systemHoverValid;
        private SystemPanel? _systemPanel;
        private StarSystem? _currentSystem;

        private void Start()
        {
            ui.GenerateRequested += OnGenerate;
            ui.ConfigEdited += config => _pendingPreview = config;
            ui.LayerChanged += layer => { _layer = layer; galaxyView.SetLayer(layer); };
            ui.BreadcrumbClicked += OnBreadcrumb;
            ui.BackRequested += _navigator.Back;
            _navigator.Changed += Render;
            Render();
        }

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

        private void OnBreadcrumb(int depth)
        {
            switch (depth)
            {
                case 0:
                    _navigator.Reset();
                    break;
                case 1:
                    _navigator.EnterGalaxy();
                    break;
                case 2:
                    // The Cell crumb: return to the cell view, hex selection cleared
                    // (orbit-diagram spec §3; closes the inert depth-2 crumb ticket).
                    if (_navigator.SelectedCell is { } cellCoord)
                        _navigator.DrillToCell(cellCoord);
                    break;
                default:
                    break;   // last crumb is "you are here" and stays inert
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _navigator.Back();
                return;
            }

            if (_navigator.Screen == AtlasScreen.Setup && _pendingPreview is { } previewConfig)
            {
                _pendingPreview = null;
                RenderPreview(previewConfig);
            }

            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            if (ui.IsPointerOverChrome(mousePos))
            {
                // Chrome owns the pointer: no hover, no tooltip, and clicks must
                // not fall through to the map underneath the panels.
                ClearHover();
                return;
            }
            switch (_navigator.Screen)
            {
                case AtlasScreen.Galaxy:
                    UpdateGalaxyScreen(mousePos);
                    break;
                case AtlasScreen.Cell:
                    UpdateCellScreen(mousePos);
                    break;
                case AtlasScreen.System:
                    UpdateSystemScreen(mousePos);
                    break;
                default:
                    break;   // Setup: no picking surface
            }
        }

        private void ClearHover()
        {
            if (!_hoverValid && _lastHoverPick == null && !_systemHoverValid) return;
            _hoverValid = false;
            _lastHoverPick = null;
            _systemHoverValid = false;
            _lastSystemPick = null;
            if (_navigator.Screen == AtlasScreen.Galaxy) galaxyView.SetHover(null);
            ui.SetTooltip(null);
        }

        private bool HoverChanged(HexCoordinate? pick)
        {
            if (_hoverValid && System.Nullable.Equals(pick, _lastHoverPick)) return false;
            _hoverValid = true;
            _lastHoverPick = pick;
            return true;
        }

        private bool SystemHoverChanged(BodyRef? pick)
        {
            if (_systemHoverValid && System.Nullable.Equals(pick, _lastSystemPick)) return false;
            _systemHoverValid = true;
            _lastSystemPick = pick;
            return true;
        }

        private void UpdateGalaxyScreen(Vector2 mousePos)
        {
            var pick = galaxyView.Pick(mousePos, mainCamera);
            if (HoverChanged(pick))
            {
                galaxyView.SetHover(pick);

                if (pick is { } cellCoord && _service!.TryGetCell(cellCoord, out var cell))
                {
                    string owner = cell.OwnerPolityId >= 0
                        ? _service.Skeleton.Polities[cell.OwnerPolityId].Name
                        : "unclaimed";
                    ui.SetTooltip($"cell ({cell.Q},{cell.R}) · {owner} · dev {cell.DevelopmentTier}");
                }
                else
                {
                    ui.SetTooltip(null);
                }
            }

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && pick is { } clicked)
                _navigator.DrillToCell(clicked);
        }

        private void UpdateCellScreen(Vector2 mousePos)
        {
            var pick = cellView.Pick(mousePos, mainCamera);

            if (HoverChanged(pick))
            {
                if (pick is { } hex)
                {
                    var state = _service!.StateOf(hex);
                    ui.SetTooltip($"{Designation.For(hex)} · {state.ToString().ToLowerInvariant()}");
                }
                else
                {
                    ui.SetTooltip(null);
                }
            }

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && pick is { } clicked)
            {
                if (System.Nullable.Equals(_navigator.SelectedHex, clicked))
                {
                    // Second click on the selected hex drills into the system;
                    // a selected empty hex stays a no-op (orbit-diagram spec §3).
                    if (_service!.Generate(clicked).System != null)
                        _navigator.EnterSystem();
                }
                else
                {
                    _navigator.SelectHex(clicked);
                }
            }
        }

        private void UpdateSystemScreen(Vector2 mousePos)
        {
            var pick = systemView.Pick(mousePos, mainCamera);
            if (SystemHoverChanged(pick))
                ui.SetTooltip(pick is { } hovered ? SystemTooltip(hovered) : null);

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Clicking empty space clears both the diagram selection and the
                // panel highlight (pick is null there).
                systemView.SetSelected(pick);
                _systemPanel?.Highlight(pick);
            }
        }

        private string? SystemTooltip(BodyRef pick)
        {
            if (_currentSystem == null) return null;
            var star = _currentSystem.Stars[pick.Star];
            if (pick.Slot < 0)
                return $"Star {(char)('A' + pick.Star)} — {star.TypeName}, "
                    + star.Age.ToString().ToLowerInvariant();
            var slot = star.Slots[pick.Slot];
            var body = slot.Body;
            if (body == null) return null;
            if (pick.Moon >= 0)
            {
                var moon = body.Satellites[pick.Moon];
                return $"moon {(char)('a' + pick.Moon)} — {SystemPanelBuilder.KindName(moon.Kind)}"
                    + (moon.Settlement != Settlement.None
                        ? $" · {moon.Settlement.ToString().ToLowerInvariant()}" : "");
            }
            return SystemPanelBuilder.KindName(body.Kind)
                + (body.Name != null ? $" \"{body.Name}\"" : "")
                + $" · slot {slot.Index} [{slot.Band.ToString().ToLowerInvariant()}]"
                + (body.Size > 0 ? $" · size {body.Size}" : "")
                + (body.Settlement != Settlement.None
                    ? $" · {body.Settlement.ToString().ToLowerInvariant()}" : "");
        }

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

        private void Render()
        {
            // Screen or content changed: last frame's hover belongs to the old view.
            _hoverValid = false;
            _lastHoverPick = null;
            _systemHoverValid = false;
            _lastSystemPick = null;
            if (_navigator.Screen != AtlasScreen.System)
            {
                _systemPanel = null;
                _currentSystem = null;
            }

            switch (_navigator.Screen)
            {
                case AtlasScreen.Setup:
                    cellView.gameObject.SetActive(false);
                    systemView.gameObject.SetActive(false);
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

                case AtlasScreen.Galaxy:
                    galaxyView.gameObject.SetActive(true);
                    cellView.gameObject.SetActive(false);
                    systemView.gameObject.SetActive(false);
                    galaxyView.Show(_service!, _layer);
                    ui.ShowGalaxyHud($"Galaxy {_seed}");
                    ui.SetBreadcrumb(new[] { "Setup", $"Galaxy {_seed}" });
                    FitCamera(galaxyView.MapBounds);
                    break;

                case AtlasScreen.Cell:
                    RenderCellScreen();
                    break;

                case AtlasScreen.System:
                    RenderSystemScreen();
                    break;
            }
        }

        private void RenderCellScreen()
        {
            galaxyView.gameObject.SetActive(false);
            cellView.gameObject.SetActive(true);
            systemView.gameObject.SetActive(false);

            var cellCoord = _navigator.SelectedCell!.Value;
            if (!_service!.TryGetCell(cellCoord, out var cell))
            {
                // Only reachable through automation driving the navigator to a
                // coordinate outside the galaxy; the map never picks one.
                _navigator.Back();
                return;
            }
            cellView.Show(_service, cellCoord);
            ui.ShowCellHud(_service.CellSummary(cell));

            var crumbs = new List<string> { "Setup", $"Galaxy {_seed}", $"Cell ({cellCoord.Q},{cellCoord.R})" };
            if (_navigator.SelectedHex is { } selectedHex)
                crumbs.Add(Designation.For(selectedHex));
            ui.SetBreadcrumb(crumbs);

            FitCamera(cellView.MapBounds);

            if (_navigator.SelectedHex is { } hex)
            {
                var result = _service.Generate(hex);
                double density = result.IsEmpty ? DensityField.At(_service.Context.Config, hex) : double.NaN;
                var panel = SystemPanelBuilder.Build(result, density,
                    onOpenSystem: result.System != null ? () => _navigator.EnterSystem() : null);
                ui.ShowSystemPanel(panel.Root);
            }
            else
            {
                ui.HideSystemPanel();
            }
            cellView.SetSelected(_navigator.SelectedHex);
        }

        private void RenderSystemScreen()
        {
            galaxyView.gameObject.SetActive(false);
            cellView.gameObject.SetActive(false);
            systemView.gameObject.SetActive(true);

            var hex = _navigator.SelectedHex!.Value;
            var result = _service!.Generate(hex);
            if (result.System == null)
            {
                // Only reachable through automation calling EnterSystem on an empty
                // hex; the controller's own entry paths check for a system first.
                _navigator.Back();
                return;
            }
            _currentSystem = result.System;

            systemView.Show(result.System);
            ui.ShowCellHud($"{result.System.GivenName ?? result.System.Designation}"
                + $" · {result.System.Designation}"
                + $" · {result.System.Arrangement.ToString().ToLowerInvariant()}");

            var cellCoord = _navigator.SelectedCell!.Value;
            ui.SetBreadcrumb(new[]
            {
                "Setup", $"Galaxy {_seed}", $"Cell ({cellCoord.Q},{cellCoord.R})",
                result.System.GivenName ?? result.System.Designation,
            });

            FitCamera(systemView.MapBounds);

            _systemPanel = SystemPanelBuilder.Build(result);
            ui.ShowSystemPanel(_systemPanel.Root);
        }

        private void FitCamera(Bounds b)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / mainCamera.aspect) * 1.08f;
            mainCamera.transform.position = new Vector3(b.center.x, b.center.y, -10f);
        }
    }
}
```

- [ ] **Step 2: Wire `SystemView` into the scene builder** — in `unity/Assets/Editor/AtlasSceneSetup.cs`:

After the `CellView` creation block, add:

```csharp
            var systemGo = new GameObject("SystemView");
            var systemView = systemGo.AddComponent<SystemView>();
```

Change the `AssignControllerRefs` call to:

```csharp
            AssignControllerRefs(controller, galaxyView, cellView, systemView, atlasUi, cam);
```

Replace `AssignControllerRefs` with:

```csharp
        private static void AssignControllerRefs(
            AtlasController controller, GalaxyView galaxyView, CellView cellView,
            SystemView systemView, AtlasUI ui, Camera cam)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("galaxyView").objectReferenceValue = galaxyView;
            so.FindProperty("cellView").objectReferenceValue = cellView;
            so.FindProperty("systemView").objectReferenceValue = systemView;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
```

- [ ] **Step 3: Add acceptance menu items** — in `unity/Assets/Editor/AtlasAcceptance.cs`, after the `Back` menu item add:

```csharp
        [MenuItem("StarGen/Acceptance/Open System")]
        public static void OpenSystem()
        {
            var controller = Controller();
            controller.Navigator.EnterSystem();
            Debug.Log($"[Acceptance] entered system screen: hex={controller.Navigator.SelectedHex}");
        }

        [MenuItem("StarGen/Acceptance/Select Binary Or Trinary Hex")]
        public static void SelectMultiStar()
        {
            var controller = Controller();
            var cellCoord = controller.Navigator.SelectedCell
                ?? throw new System.InvalidOperationException("not in cell view");
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cellCoord), HexGrid.CellRadius))
            {
                var system = controller.Service!.Generate(hex).System;
                if (system != null && system.Stars.Count > 1)
                {
                    controller.Navigator.SelectHex(hex);
                    Debug.Log($"[Acceptance] selected {system.Arrangement} system "
                        + $"{system.Designation} at {hex}");
                    return;
                }
            }
            Debug.LogWarning($"[Acceptance] no multi-star system in cell {cellCoord}");
        }

        [MenuItem("StarGen/Acceptance/Dump System Layout")]
        public static void DumpSystemLayout()
        {
            var controller = Controller();
            var hex = controller.Navigator.SelectedHex
                ?? throw new System.InvalidOperationException("no hex selected");
            var system = controller.Service!.Generate(hex).System
                ?? throw new System.InvalidOperationException("selected hex has no system");
            var layout = OrbitLayout.Compute(system);
            Debug.Log($"[Acceptance] layout: stars={layout.Stars.Count} rings={layout.Rings.Count}"
                + $" bodies={layout.Bodies.Count} picks={layout.Picks.Count} bounds={layout.Bounds}");
        }
```

- [ ] **Step 4: Run the compile gate** — expected: no `error CS` lines.

- [ ] **Step 5: Regenerate the scene headlessly:**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.2f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity' -executeMethod StarGen.Atlas.EditorTools.AtlasSceneSetup.RunFromCli -logFile 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\scene-setup.log' | Out-Null
Select-String -Path 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\unity\scene-setup.log' -Pattern 'AtlasSceneSetup|error'
```

Expected: `AtlasSceneSetup: scene constructed at Assets/Scenes/Atlas.unity`, no errors. Verify with `git diff --stat unity/Assets/Scenes/Atlas.unity` that the scene gained the SystemView object.

- [ ] **Step 6: Run the test gate** — expected: `<test-run ... total="45" passed="45" failed="0" ...>`. Report line + timestamp.

- [ ] **Step 7: Run the Core regression suite** (no Core changes expected, belt-and-braces):

```powershell
dotnet test 'C:\Users\Jaaco\Documents\dev\StarSystemGeneration\StarSystemGeneration.sln'
```

Expected: `Passed! ... 109 Passed` (quote the summary line verbatim).

- [ ] **Step 8: Commit** (scene included; ProjectSettings excluded per Global Constraints):

```powershell
git add unity/Assets/Scripts/Atlas/AtlasController.cs unity/Assets/Editor/AtlasSceneSetup.cs unity/Assets/Editor/AtlasAcceptance.cs unity/Assets/Scenes/Atlas.unity
git commit -m "feat: System screen wired through controller, scene, and acceptance driver"
```

---

### Task 8: Live MCP acceptance + DESIGN.md status

**This task is executed by the session controller with the Unity editor OPEN and the MCP bridge connected — not by a batchmode subagent.** Coordinate with the user before starting (they must open the editor).

**Files:**
- Modify: `docs/DESIGN.md` (roadmap §4 item 2 status line)

Techniques (hard-won, from the ledger): an unfocused editor does not tick the player loop — drive frames with `EditorApplication.Step()`; capture via manual `Camera.Render()` → RenderTexture → PNG, never `ScreenCapture` (doesn't flush unfocused); trust live UI-tree queries over screenshots (chrome lags one frame); the MCP sandbox can reference `StarGen.Atlas` but NOT `StarGen.Core` types — use the `StarGen/Acceptance/...` menu items and UI-tree queries.

- [ ] **Step 1: Enter play mode, generate, drill** — run menu `StarGen/Acceptance/Generate Seed 42` (defaults, radius 21), then `Drill To First Capital`, then `Select Settled Hex`. Verify the data panel opens with an "Open system" button at the top (UI query: `Q<Button>` with text `Open system` inside `system-panel-host`).

- [ ] **Step 2: Enter via button** — submit the "Open system" button (NavigationSubmitEvent, same pattern as the Generate acceptance). Verify: navigator screen is `System`; the diagram mesh renders (SystemView active, mesh vertexCount > 0); breadcrumb shows 4 crumbs ending in the system name; side panel shows `{name} · {designation} · {arrangement}`; run `Dump System Layout` and check stars/rings/bodies counts against the data panel's content.

- [ ] **Step 3: Cross-check against the REPL (phase-2 done-when)** — from a separate terminal, run the inspector REPL (`dotnet run --project src/Inspector` — or `dotnet <dll>` if Smart App Control blocks the apphost), `gload`/`goto` the same designation at seed 42, and confirm star count, per-star slot count, body kinds, moon counts, and belt slots match the diagram dump exactly.

- [ ] **Step 4: Back and re-enter via second click** — `Back` menu item: screen returns to Cell with the hex still selected and the panel open. Then re-enter by simulating the second-click path: since map clicks can't be faked, call `Open System` again (the second-click branch is the same `EnterSystem` call; note this in the report). Esc path: send Escape via the Input System OR call `Back` — verify System → Cell keeps hex.

- [ ] **Step 5: Hover + click inspect** — with the editor focused (or stepping frames), move the mouse over a body: tooltip shows the kind/name/slot line. Click a moon: its panel row gets the amber tint and scrolls into view (UI query: exactly one Label with backgroundColor == SystemPanel.HighlightBg); the moon's disc brightens on the diagram. Click empty diagram space: both clear.

- [ ] **Step 6: Multi-star system** — `Back` to cell (or galaxy → another cell), run `Select Binary Or Trinary Hex`, then `Open System`. Verify: companion star disc sits on a primary ring; its sub-rings render inside a visibly widened gap; hab annuli render for stars that have habitable slots. Capture one PNG for the record (manual Camera.Render technique) into `.superpowers/shots/`.

- [ ] **Step 7: Negative + breadcrumb paths** — `Select Empty Hex`, verify the panel has NO "Open system" button, then run `Open System` anyway (the menu item calls `EnterSystem` directly, bypassing the controller's system-exists checks — this exercises the `RenderSystemScreen` guard): expected result is an immediate bounce back to Cell (screen == Cell, hex still selected, no unhandled exception in the log). Then re-select a settled hex, enter the System screen, and click the `Cell (q,r)` breadcrumb (send NavigationSubmitEvent to the crumb button): lands on Cell with hex selection cleared.

- [ ] **Step 8: Console check** — `Unity_GetConsoleLogs`: zero errors, zero warnings from app code across the whole run.

- [ ] **Step 9: Update DESIGN.md** — in `docs/DESIGN.md` §4 roadmap item 2, replace:

```
   *Status:* data-panel portion delivered by the Unity atlas (2026-07); orbit-diagram
   rendering remains.
```

with:

```
   *Status:* complete — data panel via the Unity atlas and the orbit-diagram
   system view (both 2026-07).
```

- [ ] **Step 10: Commit**

```powershell
git add docs/DESIGN.md
git commit -m "docs: mark roadmap phase 2 complete (orbit-diagram system view)"
```

---

## Execution Notes

- **Subagent split (established conventions):** Tasks 1–7 are batchmode-safe implementer dispatches (cheap model for verbatim transcription — every task's code is complete in this plan; sonnet if integration judgment is needed). Task 8 is controller-led with the editor open. Final whole-branch review on the fable model + one fix wave, then finishing-a-development-branch (merge to main locally, verify, delete branch; user pushes).
- **Reports:** every implementer report includes the verbatim `<test-run ...>` line, the `test-results.xml` `LastWriteTime`, and any `error CS` lines quoted in full.
- **Deferred/known non-goals** (do NOT implement): in-diagram text labels, orbital motion, hover recolor on the diagram (tooltip-only; selection recolors), pan/zoom, REPL knobs, Atlas.unity as build scene 0.

