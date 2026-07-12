using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>The news lens: every pulse in transit as an expanding
    /// ring-front from its origin — young word tight and bright, old word
    /// wide and fading toward rumor (NewsLens carries age/magnitude; the
    /// growth curve is presentation). World-sized billboards on the K1
    /// point-sprite shader; no pixel floor — the front is spatial.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class NewsLayer : MonoBehaviour
    {
        private const float Z = -0.12f;
        /// <summary>Ring radius in hexes: anchored at 1 hex at birth,
        /// sqrt-growing (word saturates its reach like a diffusion front).</summary>
        private const float GrowthHexesPerSqrtYear = 1.15f;
        private const float MaxRadiusHexes = 10f;
        /// <summary>Display cutoff: Core keeps a pulse live to
        /// PulseMaxYears (150 — belief truth), but a century-old ring is
        /// history, not news; the map draws only word still spreading.
        /// First smoke drowned in 597 lifetime rings (fix wave note).</summary>
        private const float MaxDisplayAgeYears = 40f;

        private Mesh _mesh;
        private Material _material;

        private void Awake()
        {
            _material = new Material(Shader.Find("StarGen/AtlasBillboard"));
            _material.SetTexture("_MainTex", AtlasTextures.Ring);
            // Additive: overlapping fronts brighten instead of stacking
            // into an opaque wall (the starfield's convention).
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.One);
            _material.SetFloat("_MaxPx", 4096f);   // spatial — never pixel-capped
            _material.renderQueue = 3040;          // over fields, under glyphs
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

        public void EnsureMaterial()
        {
            if (_material == null) Awake();
        }

        public void SetVisible(bool visible) =>
            GetComponent<MeshRenderer>().enabled = visible;

        /// <summary>K5 hex→orbit crossfade: pulse fronts blend additively
        /// (SrcAlpha/One), so the fade scales the emitted light too.</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            if (_material == null) return;   // edit-mode caller ordering
            float fade = LodBands.MapFade(cameraDistance, galaxyExtent);
            _material.SetColor("_Tint", new Color(fade, fade, fade, fade));
        }

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            var all = NewsLens.Pulses(model, eye);
            var pulses = new List<StarGen.Core.Atlas.NewsPulseMark>(all.Count);
            foreach (var p in all)
                if (p.AgeYears <= MaxDisplayAgeYears) pulses.Add(p);
            var vertices = new Vector3[pulses.Count * 4];
            var corners = new List<Vector4>(pulses.Count * 4);
            var colors = new Color[pulses.Count * 4];
            var triangles = new int[pulses.Count * 6];
            for (int i = 0; i < pulses.Count; i++)
            {
                var p = pulses[i];
                var center = AtlasGeometry.HexToWorld(p.Origin, Z);
                float radiusHexes = Mathf.Min(MaxRadiusHexes,
                    1f + GrowthHexesPerSqrtYear * Mathf.Sqrt((float)p.AgeYears));
                float world = radiusHexes * 2f * AtlasGeometry.HexStep;
                var color = ((Color)AtlasGeometry.ToColor32(p.Color)).linear;
                // Quiet fronts, additive stacking: young loud word peaks
                // well under full — the story is where rings CLUSTER.
                color.a = 0.35f
                    * (1f - (float)p.AgeYears / MaxDisplayAgeYears)
                    * Mathf.Clamp01(0.35f + 0.65f * (float)p.Magnitude);
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = center;
                    colors[v + c] = color;
                }
                corners.Add(new Vector4(-0.5f, -0.5f, world, 0f));
                corners.Add(new Vector4(0.5f, -0.5f, world, 0f));
                corners.Add(new Vector4(0.5f, 0.5f, world, 0f));
                corners.Add(new Vector4(-0.5f, 0.5f, world, 0f));
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
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(30f);
            _mesh.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
