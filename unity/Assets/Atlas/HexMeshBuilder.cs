using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>One mesh per surface: 7 verts + 6 tris per hex, vertex-
    /// colored, recolorable in place (the PoC's proven recipe). Geometry
    /// comes exclusively from HexGrid — the single authority. The fixed
    /// inset doubles as the lattice: invisible zoomed out, a grid up close.</summary>
    public static class HexMeshBuilder
    {
        public const float Inset = 0.08f;

        public static Mesh Build(IReadOnlyList<HexCoordinate> hexes,
                                 IReadOnlyList<Color32> colors)
        {
            if (hexes.Count != colors.Count)
                throw new ArgumentException("hexes and colors must be parallel");
            var vertices = new Vector3[hexes.Count * 7];
            var vertexColors = new Color32[hexes.Count * 7];
            var triangles = new int[hexes.Count * 6 * 3];
            float scale = 1f - Inset;

            for (int i = 0; i < hexes.Count; i++)
            {
                var (wx, wy) = HexGrid.HexToWorld(hexes[i]);
                var center = new Vector3((float)wx, (float)wy, 0f);
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
    }
}
