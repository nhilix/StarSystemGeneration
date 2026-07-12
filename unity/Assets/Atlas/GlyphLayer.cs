using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>One authored glyph at an address: the atlas cell picks the
    /// shape, the tint the faction/state, world+pixel the dual sizing
    /// (PortLayer's convention — a pixel floor at altitude, world size
    /// resolving as the camera descends).</summary>
    public readonly struct GlyphInstance
    {
        public readonly Vector3 Center;
        public readonly float WorldSize;
        public readonly float PxSize;
        public readonly Rgba Tint;
        public readonly AtlasGlyph Glyph;

        public GlyphInstance(Vector3 center, float worldSize, float pxSize,
                             Rgba tint, AtlasGlyph glyph)
        {
            Center = center;
            WorldSize = worldSize;
            PxSize = pxSize;
            Tint = tint;
            Glyph = glyph;
        }
    }

    /// <summary>Shared machinery for the glyph-mark layers (fleets, POIs,
    /// works, plague, war stations): one StarGen/AtlasGlyph material over
    /// the authored atlas, one billboard mesh, an LOD fade — subclasses
    /// only translate lens data into GlyphInstances.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class GlyphLayerBase : MonoBehaviour
    {
        private Mesh _mesh;
        private Material _material;
        private bool _visible = true;

        /// <summary>Layer stacking depth toward the camera (negative z).</summary>
        protected abstract float ZOffset { get; }

        /// <summary>Transparent-queue bias past the base layers: renderer
        /// bounds sorting draws the port dots over same-hex glyphs
        /// otherwise (a posted fleet sits AT its home port).</summary>
        protected virtual int QueueBias => 100;

        protected virtual void Awake()
        {
            _material = new Material(Shader.Find("StarGen/AtlasGlyph"))
            { hideFlags = HideFlags.HideAndDontSave };
            _material.SetTexture("_MainTex", AtlasGlyphs.Atlas);
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_MaxPx", 56f);
            _material.SetColor("_Tint", Color.white);
            _material.renderQueue = 3000 + QueueBias;
            GetComponent<MeshRenderer>().material = _material;
        }

        private void OnDestroy()
        {
            if (_mesh != null) DestroyResource(_mesh);
            if (_material != null) DestroyResource(_material);
        }

        protected static void DestroyResource(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        public void EnsureMaterial()
        {
            if (_material == null) Awake();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        /// <summary>Glyphs resolve toward Region (bands gate what resolves;
        /// the fade itself is continuous).</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            if (_material == null) return;   // edit-mode caller ordering
            _material.SetColor("_Tint", new Color(1f, 1f, 1f,
                LodBands.GlyphFade(cameraDistance, galaxyExtent)));
        }

        public abstract void Show(AtlasReadModel model, EyeContext eye);

        private void ApplyVisibility() =>
            GetComponent<MeshRenderer>().enabled = _visible && _mesh != null
                && _mesh.vertexCount > 0;

        // The contrast chip under every glyph (the first smoke's lesson:
        // an owner-tinted glyph over an owner-tinted port dot is
        // camouflage). Drawn as the first quad of each pair — triangles
        // inside one mesh render in index order, so backing precedes
        // glyph at the same queue and both ride the same LOD tint.
        private static readonly Color BackingColor =
            new Color32(9, 11, 17, 195);
        private const float BackingScale = 1.45f;

        /// <summary>Rebuild the billboard mesh from instances (colors
        /// CPU-linearized — the project renders linear, tints are sRGB).</summary>
        protected void Apply(IReadOnlyList<GlyphInstance> instances)
        {
            int quads = instances.Count * 2;
            var vertices = new Vector3[quads * 4];
            var corners = new List<Vector4>(quads * 4);
            var rects = new List<Vector4>(quads * 4);
            var colors = new Color[quads * 4];
            var triangles = new int[quads * 6];
            var backingRect = AtlasGlyphs.UvRect(AtlasGlyph.Backing);
            var backingColor = BackingColor.linear;   // alpha untouched
            for (int i = 0; i < instances.Count; i++)
            {
                var g = instances[i];
                var rect = AtlasGlyphs.UvRect(g.Glyph);
                var color = ((Color)AtlasGeometry.ToColor32(g.Tint)).linear;
                color.a = g.Tint.A / 255f;   // alpha is never sRGB-encoded
                Quad(i * 2, g.Center, g.WorldSize * BackingScale,
                     g.PxSize * BackingScale, backingColor, backingRect);
                Quad(i * 2 + 1, g.Center, g.WorldSize, g.PxSize, color, rect);
            }

            void Quad(int q, Vector3 center, float world, float px,
                      Color color, Vector4 rect)
            {
                int v = q * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = center;
                    colors[v + c] = color;
                    rects.Add(rect);
                }
                corners.Add(new Vector4(-0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, 0.5f, world, px));
                corners.Add(new Vector4(-0.5f, 0.5f, world, px));
                int t = q * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32,
                               hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, corners);
            _mesh.SetUVs(1, rects);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(20f);
            _mesh.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            ApplyVisibility();
        }

        /// <summary>World-space hex position at this layer's depth.</summary>
        protected Vector3 At(StarGen.Core.Model.HexCoordinate hex) =>
            AtlasGeometry.HexToWorld(hex, ZOffset);
    }
}
