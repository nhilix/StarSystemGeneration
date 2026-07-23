using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>AC2.F2 recent-flow trails: what moved during the step that
    /// produced the current keyframe — courier-violet / war-convoy-red
    /// strokes from origin port hex to dest port hex, a memory riding
    /// under the works lens (visible with the works chip, like the crates).
    /// Subordinate by construction: thinner than the lane strokes, behind
    /// them in depth, alpha floored/capped by Core's RecentFlowQuery so
    /// corridor overdraw reads as intensity, not mud. Flows come from the
    /// TimeMachine's per-keyframe capture (in-memory only) — a freshly
    /// loaded artifact has none until a step runs, which is correct.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FlowTrailLayer : MonoBehaviour
    {
        private const float Z = -0.03f;      // under the lanes' -0.05
        private const float WidthPx = 1.0f;  // thinner than LaneLayer's 1.4
        private const float FovDegrees = 50f;

        private IReadOnlyList<FlowTrailMark> _trails;
        private Mesh _mesh;
        private Material _material;
        private float _width = 0.5f;
        private bool _visible = true;
        private float _extentForFade = 100f;

        /// <summary>Render-target height — Screen.height lies in batch
        /// captures; the root/tooling supplies the real value.</summary>
        public float ViewportPx = 1080f;

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"))
            { hideFlags = HideFlags.HideAndDontSave };
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

        /// <summary>Rebuilds the trails from the current keyframe's
        /// captured flows (Core aggregates corridors and filters to the
        /// two rendering purposes — this only draws).</summary>
        public void Show(AtlasReadModel model, IReadOnlyList<RecentFlow> flows)
        {
            _trails = RecentFlowQuery.Trails(model.State, flows);
            Rebuild();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        public void SetExtent(float galaxyExtent) => _extentForFade = galaxyExtent;

        /// <summary>Screen-constant width plus the lanes' altitude fade —
        /// trails defer to the glows at galaxy distance exactly as the
        /// highways they shadow do.</summary>
        public void OnZoom(float cameraDistance)
        {
            if (_material == null) return;   // edit-mode caller ordering
            _material.color = new Color(1f, 1f, 1f,
                LodBands.LaneFade(cameraDistance, _extentForFade));
            float worldPerPx = 2f * cameraDistance
                * Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad) / ViewportPx;
            float width = Mathf.Max(0.02f, WidthPx * worldPerPx);
            if (_width > 0f && Mathf.Abs(width - _width) / _width < 0.08f) return;
            _width = width;
            Rebuild();
        }

        private void Rebuild()
        {
            if (_trails == null) return;
            var vertices = new Vector3[_trails.Count * 4];
            var colors = new Color[_trails.Count * 4];
            var triangles = new int[_trails.Count * 6];
            for (int i = 0; i < _trails.Count; i++)
            {
                var trail = _trails[i];
                var a = AtlasGeometry.HexToWorld(trail.From, Z);
                var b = AtlasGeometry.HexToWorld(trail.To, Z);
                var dir = (b - a).normalized;
                var side = new Vector3(-dir.y, dir.x, 0f) * (_width * 0.5f);
                // vertex colors CPU-linearized (the project renders linear,
                // tints are sRGB); alpha is never sRGB-encoded
                var color = ((Color)AtlasGeometry.ToColor32(trail.Color)).linear;
                color.a = trail.Color.A / 255f;
                int v = i * 4;
                vertices[v] = a - side;
                vertices[v + 1] = a + side;
                vertices[v + 2] = b + side;
                vertices[v + 3] = b - side;
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
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32,
                               hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            ApplyVisibility();
        }

        private void ApplyVisibility() =>
            GetComponent<MeshRenderer>().enabled = _visible && _mesh != null
                && _mesh.vertexCount > 0;
    }
}
