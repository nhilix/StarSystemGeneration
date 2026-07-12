using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>Port dots, as the artifact draws them: small screen-fixed
    /// circles (2px + 1.4px/tier), owner-colored, localized in space as
    /// alpha-blended billboards. Authored tier glyphs replace the soft dot
    /// when the icon vocabulary lands (K2).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PortLayer : MonoBehaviour
    {
        private const float Z = -0.15f;

        private Mesh _mesh;
        private Material _material;

        private void Awake()
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

        /// <summary>K5 hex→orbit crossfade: port dots dissolve with the
        /// rest of the map as the stage fades up.</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            if (_material == null) return;   // edit-mode caller ordering
            _material.SetColor("_Tint", new Color(1f, 1f, 1f,
                LodBands.MapFade(cameraDistance, galaxyExtent)));
        }

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            var markers = PortLens.Markers(model, eye);
            var vertices = new Vector3[markers.Count * 4];
            var corners = new List<Vector4>(markers.Count * 4);
            var colors = new Color[markers.Count * 4];   // linearized floats
            var triangles = new int[markers.Count * 6];
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                var center = AtlasGeometry.HexToWorld(m.Hex, Z);
                // Dual sizing: a tier-scaled pixel floor at altitude (the
                // artifact's 2+1.4·tier px dots), a tier-scaled WORLD size
                // that takes over as the camera descends — tier differences
                // become spatial at region/hex bands.
                float px = (2f + 1.4f * m.Tier) * 2.0f;
                float world = (0.35f + 0.25f * m.Tier) * AtlasGeometry.HexStep;
                var color = ((Color)AtlasGeometry.ToColor32(m.Color)).linear;
                color.a = 1f;
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = center;
                    colors[v + c] = color;
                }
                corners.Add(new Vector4(-0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, 0.5f, world, px));
                corners.Add(new Vector4(-0.5f, 0.5f, world, px));
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
        }
    }
}
