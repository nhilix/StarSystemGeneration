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
