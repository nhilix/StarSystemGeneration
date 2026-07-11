using System.Collections.Generic;
using NUnit.Framework;
using StarGen.AtlasView;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.AtlasView.Tests
{
    /// <summary>The salvaged mesh recipe: 7 verts + 6 tris per hex,
    /// vertex-colored, recolorable in place.</summary>
    public class HexMeshBuilderTests
    {
        private static readonly List<HexCoordinate> ThreeHexes = new()
        {
            new HexCoordinate(0, 0), new HexCoordinate(1, 0), new HexCoordinate(0, 1),
        };

        private static List<Color32> Colors(byte r) => new()
        {
            new Color32(r, 0, 0, 255), new Color32(r, 0, 0, 255), new Color32(r, 0, 0, 255),
        };

        [Test]
        public void SevenVertsSixTrisPerHex()
        {
            var mesh = HexMeshBuilder.Build(ThreeHexes, Colors(10));
            Assert.AreEqual(3 * 7, mesh.vertexCount);
            Assert.AreEqual(3 * 6 * 3, mesh.triangles.Length);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void RecolorSwapsColorsWithoutTouchingGeometry()
        {
            var mesh = HexMeshBuilder.Build(ThreeHexes, Colors(10));
            var before = mesh.vertices;
            HexMeshBuilder.Recolor(mesh, Colors(200));
            Assert.AreEqual(200, mesh.colors32[0].r);
            Assert.AreEqual(before.Length, mesh.vertices.Length);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ParallelListMismatchThrows()
        {
            Assert.Throws<System.ArgumentException>(
                () => HexMeshBuilder.Build(ThreeHexes, Colors(10).GetRange(0, 2)));
        }
    }
}
