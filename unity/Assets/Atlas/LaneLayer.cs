using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>Lanes as thin, screen-constant highways on the plane
    /// (LaneLens status colors: open subtle, quarantined/severed loud
    /// enough to read). Width tracks camera altitude continuously; the
    /// perspective foreshortening near the horizon is intentional depth.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LaneLayer : MonoBehaviour
    {
        private const float Z = -0.05f;
        private const float WidthPx = 1.4f;
        private const float FovDegrees = 50f;

        private IReadOnlyList<LaneSegment> _segments;
        private Mesh _mesh;
        private Material _material;
        private float _width = 0.5f;

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
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

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _segments = LaneLens.Segments(model, eye);
            Rebuild();
        }

        public void SetVisible(bool visible) =>
            GetComponent<MeshRenderer>().enabled = visible;

        /// <summary>Screen-constant width plus the altitude fade: lanes
        /// defer to the glows at galaxy distance. Rebuilds only when the
        /// width drifts >8%; the fade rides the material tint.</summary>
        public void OnZoom(float cameraDistance)
        {
            _material.color = new Color(1f, 1f, 1f,
                LodBands.LaneFade(cameraDistance, _extentForFade));
            float worldPerPx = 2f * cameraDistance
                * Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad) / Screen.height;
            float width = Mathf.Max(0.02f, WidthPx * worldPerPx);
            if (_width > 0f && Mathf.Abs(width - _width) / _width < 0.08f) return;
            _width = width;
            Rebuild();
        }

        private float _extentForFade = 100f;

        public void SetExtent(float galaxyExtent) => _extentForFade = galaxyExtent;

        private void Rebuild()
        {
            if (_segments == null) return;
            var vertices = new Vector3[_segments.Count * 4];
            var colors = new Color32[_segments.Count * 4];
            var triangles = new int[_segments.Count * 6];
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var a = AtlasGeometry.HexToWorld(seg.A, Z);
                var b = AtlasGeometry.HexToWorld(seg.B, Z);
                var dir = (b - a).normalized;
                var side = new Vector3(-dir.y, dir.x, 0f) * (_width * 0.5f);
                int v = i * 4;
                vertices[v] = a - side;
                vertices[v + 1] = a + side;
                vertices[v + 2] = b + side;
                vertices[v + 3] = b - side;
                var color = AtlasGeometry.ToColor32(seg.Color);
                for (int k = 0; k < 4; k++) colors[v + k] = color;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
