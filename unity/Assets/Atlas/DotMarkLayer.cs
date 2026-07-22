using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>One subordinate dot mark at a hex: the port-mark billboard
    /// idiom (a generated soft-solid dot, dual world+pixel sizing), but no
    /// service ring and no market affordance — the domain interior reads under
    /// the port keystones.</summary>
    public readonly struct DotMark
    {
        public readonly HexCoordinate Hex;
        public readonly float WorldSize;
        public readonly float PxSize;
        public readonly Rgba Color;
        public readonly float Alpha;

        public DotMark(HexCoordinate hex, float worldSize, float pxSize,
                       Rgba color, float alpha)
        {
            Hex = hex;
            WorldSize = worldSize;
            PxSize = pxSize;
            Color = color;
            Alpha = alpha;
        }
    }

    /// <summary>Shared machinery for the domain-interior dot layers (worked
    /// hexes, outposts): one StarGen/AtlasBillboard material over the generated
    /// SolidDot, one billboard mesh, the K5 map-fade — mirroring PortLayer, the
    /// keystone these sit beneath. Subclasses only translate the interior mark
    /// set into <see cref="DotMark"/>s.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public abstract class DotMarkLayer : MonoBehaviour
    {
        /// <summary>Layer stacking depth toward the camera (negative z). Marks
        /// sit BEHIND the port dots (Z = -0.15) so the keystone stays on top.</summary>
        protected abstract float Z { get; }

        private Mesh _mesh;
        private Material _material;
        private bool _visible = true;

        protected virtual void Awake()
        {
            _material = new Material(Shader.Find("StarGen/AtlasBillboard"))
            { hideFlags = HideFlags.HideAndDontSave };
            _material.SetTexture("_MainTex", AtlasTextures.SolidDot);
            _material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat("_MaxPx", 96f);
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

        /// <summary>K5 hex→orbit crossfade: the interior marks dissolve with
        /// the rest of the map (mirrors PortLayer.OnZoom).</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            if (_material == null) return;   // edit-mode caller ordering
            _material.SetColor("_Tint", new Color(1f, 1f, 1f,
                LodBands.MapFade(cameraDistance, galaxyExtent)));
        }

        public abstract void Show(AtlasReadModel model, EyeContext eye);

        private void ApplyVisibility() =>
            GetComponent<MeshRenderer>().enabled = _visible && _mesh != null
                && _mesh.vertexCount > 0;

        /// <summary>Rebuild the billboard mesh from the marks (colors
        /// CPU-linearized — the project renders linear, tints are sRGB; the K5
        /// washed-palette trap, exactly as PortLayer.Show does it).</summary>
        protected void Render(IReadOnlyList<DotMark> marks)
        {
            var vertices = new Vector3[marks.Count * 4];
            var corners = new List<Vector4>(marks.Count * 4);
            var colors = new Color[marks.Count * 4];
            var triangles = new int[marks.Count * 6];
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                var center = AtlasGeometry.HexToWorld(m.Hex, Z);
                var color = ((Color)AtlasGeometry.ToColor32(m.Color)).linear;
                color.a = m.Alpha;
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = center;
                    colors[v + c] = color;
                }
                corners.Add(new Vector4(-0.5f, -0.5f, m.WorldSize, m.PxSize));
                corners.Add(new Vector4(0.5f, -0.5f, m.WorldSize, m.PxSize));
                corners.Add(new Vector4(0.5f, 0.5f, m.WorldSize, m.PxSize));
                corners.Add(new Vector4(-0.5f, 0.5f, m.WorldSize, m.PxSize));
                int t = i * 6;
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
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(20f);
            _mesh.bounds = bounds;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            ApplyVisibility();
        }
    }
}
