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

        [Test]
        public void DiscBand_QuadCounts_AndStaysInsideDisc()
        {
            var builder = new OrbitMeshBuilder();
            builder.AddDiscBand(Vector2.zero, 1f, 0.25f, 0.22f, new Color32(255, 255, 255, 255));
            var mesh = builder.Build();
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
            foreach (var v in mesh.vertices)
                Assert.LessOrEqual(new Vector2(v.x, v.y).magnitude, 1f + 1e-4f);
        }
    }
}
