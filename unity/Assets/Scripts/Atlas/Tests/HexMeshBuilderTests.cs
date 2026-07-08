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
