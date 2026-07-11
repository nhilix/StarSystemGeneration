using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>The base layer: the density raster as stars (StarfieldLens
    /// placement, lean-tinted, brightness-varied), rendered as additive
    /// soft-dot billboards that stay point-like across the zoom continuum
    /// but hold their place on the plane — arms, bulge and halo emerge
    /// from density alone.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StarfieldLayer : MonoBehaviour
    {
        private Mesh _mesh;
        private Material _material;

        private static readonly Color32 TintBalanced = new(200, 214, 240, 255);
        private static readonly Color32 TintYoung = new(160, 195, 255, 255);
        private static readonly Color32 TintOld = new(255, 205, 160, 255);
        private static readonly Color32 TintRemnant = new(215, 165, 235, 255);

        private void Awake()
        {
            _material = new Material(Shader.Find("StarGen/AtlasBillboard"));
            _material.SetTexture("_MainTex", AtlasTextures.SoftDot);
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.One);   // additive
            _material.SetFloat("_MaxPx", 5f);
            GetComponent<MeshRenderer>().material = _material;
        }

        private void OnDestroy()
        {
            if (_mesh != null) DestroyResource(_mesh);
            if (_material != null) DestroyResource(_material);
        }

        private static void DestroyResource(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>Edit-mode tooling calls this before Show when Awake
        /// never ran (the AtlasSmoke pattern).</summary>
        public void EnsureMaterial()
        {
            if (_material == null) Awake();
        }

        public void Show(AtlasReadModel model)
        {
            var stars = StarfieldLens.Stars(model);
            var vertices = new Vector3[stars.Count * 4];
            var corners = new List<Vector4>(stars.Count * 4);
            var colors = new Color32[stars.Count * 4];
            var triangles = new int[stars.Count * 6];
            for (int i = 0; i < stars.Count; i++)
            {
                var s = stars[i];
                var center = new Vector3((float)s.X, (float)s.Y, 0f);
                float b = (float)s.Brightness;
                float world = 0.30f + 0.45f * b;
                float minPx = 1.0f + 1.2f * b;
                var tint = model.Cells[s.CellIndex].Lean switch
                {
                    StellarLean.YoungBright => TintYoung,
                    StellarLean.OldDim => TintOld,
                    StellarLean.RemnantGraveyard => TintRemnant,
                    _ => TintBalanced,
                };
                var color = new Color32(tint.r, tint.g, tint.b,
                    (byte)(30 + 170 * b));
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = center;
                    colors[v + c] = color;
                }
                corners.Add(new Vector4(-0.5f, -0.5f, world, minPx));
                corners.Add(new Vector4(0.5f, -0.5f, world, minPx));
                corners.Add(new Vector4(0.5f, 0.5f, world, minPx));
                corners.Add(new Vector4(-0.5f, 0.5f, world, minPx));
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, corners);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            // Billboards expand in the shader; keep culling generous.
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(20f);
            _mesh.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
