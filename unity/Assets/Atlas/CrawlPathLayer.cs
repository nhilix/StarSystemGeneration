using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>AC4.1: LIVE off-lane crawl paths — the direct origin→dest
    /// line an off-lane shipment sails for decades (25–60y observed at
    /// seed 42's late epochs), dashed so it reads as travel happening NOW.
    /// Distinct from FlowTrailLayer's solid, faded recent-flow strokes (a
    /// memory of what already moved) — a live crawl vs a memory, the
    /// eyeball-fix distinction (AC4.1 taste call, flagged for Eyeball 4).
    /// Core.Atlas.WorksLens.CrawlPaths owns the dash count/gap and the
    /// dimmer, purpose-tinted alpha (the freight mark itself always reads
    /// brighter — this only draws the dashes it's handed). Visible with
    /// the works chip, same as the crates and the flow trails (LensRail).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CrawlPathLayer : MonoBehaviour
    {
        private const float Z = -0.028f;     // just above FlowTrailLayer's -0.03
        private const float WidthPx = 1.0f;  // same screen-constant width as trails
        private const float FovDegrees = 50f;

        private IReadOnlyList<CrawlDashMark> _dashes;
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

        /// <summary>Rebuilds the dashes from the current keyframe's
        /// off-lane shipments (Core derives dash geometry and tint; this
        /// only draws).</summary>
        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _dashes = WorksLens.CrawlPaths(model, eye);
            Rebuild();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        public void SetExtent(float galaxyExtent) => _extentForFade = galaxyExtent;

        /// <summary>Screen-constant width plus the lanes' altitude fade —
        /// the crawl path defers to the glows at galaxy distance exactly
        /// as the highways and the trails do.</summary>
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
            if (_dashes == null) return;
            var vertices = new Vector3[_dashes.Count * 4];
            var colors = new Color[_dashes.Count * 4];
            var triangles = new int[_dashes.Count * 6];
            for (int i = 0; i < _dashes.Count; i++)
            {
                var dash = _dashes[i];
                var origin = AtlasGeometry.HexToWorld(dash.Origin, Z);
                var dest = AtlasGeometry.HexToWorld(dash.Dest, Z);
                var a = Vector3.Lerp(origin, dest, (float)dash.FromFraction);
                var b = Vector3.Lerp(origin, dest, (float)dash.ToFraction);
                var dir = b - a;
                if (dir.sqrMagnitude < 1e-12f) dir = Vector3.right;
                dir = dir.normalized;
                var side = new Vector3(-dir.y, dir.x, 0f) * (_width * 0.5f);
                // vertex colors CPU-linearized (the project renders linear,
                // tints are sRGB); alpha is never sRGB-encoded
                var color = ((Color)AtlasGeometry.ToColor32(dash.Color)).linear;
                color.a = dash.Color.A / 255f;
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
