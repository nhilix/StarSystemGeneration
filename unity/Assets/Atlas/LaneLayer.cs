using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>What the lane strokes say (K2): Status is the K1 read
    /// (open/quarantined/severed colors), Traffic weights width and
    /// brightness by posted trips/year, QuarantineOnly is the plague
    /// lens's approaches-closed read with the rest of the network dark,
    /// Trade (AC2.3) weights width and brightness by the steepest
    /// actionable price gradient on the lane (margin gold).</summary>
    public enum LaneMode { Status, Traffic, QuarantineOnly, Trade }

    /// <summary>Lanes as thin, screen-constant highways on the plane.
    /// Base width tracks camera altitude continuously; each stroke can
    /// scale it (traffic mode widens busy lanes). The perspective
    /// foreshortening near the horizon is intentional depth.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LaneLayer : MonoBehaviour
    {
        private const float Z = -0.05f;
        private const float WidthPx = 1.4f;
        private const float FovDegrees = 50f;
        // Dead-lane trade idle read — matches TrafficLens.IdleAlpha and
        // TradeLens's own flat-spread floor (both 45): posted, nothing
        // to arbitrage.
        private const byte IdleTradeAlpha = 45;

        private readonly struct Stroke
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Color32 Color;
            public readonly float WidthFactor;

            public Stroke(Vector3 a, Vector3 b, Color32 color, float widthFactor)
            {
                A = a;
                B = b;
                Color = color;
                WidthFactor = widthFactor;
            }
        }

        private AtlasReadModel _model;
        private EyeContext _eye;
        private LaneMode _mode = LaneMode.Status;
        private List<Stroke> _strokes;
        private Mesh _mesh;
        private Material _material;
        private float _width = 0.5f;

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

        public LaneMode Mode => _mode;

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _model = model;
            _eye = eye;
            BuildStrokes();
            Rebuild();
        }

        public void SetMode(LaneMode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            if (_model == null) return;
            BuildStrokes();
            Rebuild();
        }

        public void SetVisible(bool visible) =>
            GetComponent<MeshRenderer>().enabled = visible;

        /// <summary>Render-target height — Screen.height lies in batch
        /// captures; the root/tooling supplies the real value.</summary>
        public float ViewportPx = 1080f;

        /// <summary>Screen-constant width plus the altitude fade: lanes
        /// defer to the glows at galaxy distance. Rebuilds only when the
        /// width drifts >8%; the fade rides the material tint.</summary>
        public void OnZoom(float cameraDistance)
        {
            _material.color = new Color(1f, 1f, 1f,
                LodBands.LaneFade(cameraDistance, _extentForFade));
            float worldPerPx = 2f * cameraDistance
                * Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad) / ViewportPx;
            float width = Mathf.Max(0.02f, WidthPx * worldPerPx);
            if (_width > 0f && Mathf.Abs(width - _width) / _width < 0.08f) return;
            _width = width;
            Rebuild();
        }

        private float _extentForFade = 100f;

        public void SetExtent(float galaxyExtent) => _extentForFade = galaxyExtent;

        private void BuildStrokes()
        {
            _strokes = new List<Stroke>();
            if (_mode == LaneMode.Traffic)
            {
                foreach (var seg in TrafficLens.Segments(_model, _eye))
                {
                    // Idle lanes stay ghost-thin; busy ones widen toward 3×.
                    float factor = 0.45f + 2.55f * (float)seg.Weight;
                    _strokes.Add(new Stroke(
                        AtlasGeometry.HexToWorld(seg.A, Z),
                        AtlasGeometry.HexToWorld(seg.B, Z),
                        AtlasGeometry.ToColor32(seg.Color), factor));
                }
                return;
            }
            if (_mode == LaneMode.Trade)
            {
                // Segments emits LIVE lanes only — match by LaneId, never by
                // index. A lane with no reading (dead, or the lane lens's
                // wound) draws the same idle treatment traffic gives a
                // posted-but-quiet lane: ghost-thin margin gold.
                var byLane = new Dictionary<int, TradeSegment>();
                foreach (var seg in TradeLens.Segments(_model, _eye))
                    byLane[seg.LaneId] = seg;
                foreach (var lane in _model.State.Lanes)
                {
                    var a = _model.State.Ports[lane.PortAId].Hex;
                    var b = _model.State.Ports[lane.PortBId].Hex;
                    if (byLane.TryGetValue(lane.Id, out var seg))
                    {
                        float factor = 0.45f + 2.55f * (float)seg.Weight;
                        _strokes.Add(new Stroke(
                            AtlasGeometry.HexToWorld(seg.A, Z),
                            AtlasGeometry.HexToWorld(seg.B, Z),
                            AtlasGeometry.ToColor32(seg.Color), factor));
                    }
                    else
                    {
                        var idle = new Rgba(TradeLens.MarginGold.R,
                            TradeLens.MarginGold.G, TradeLens.MarginGold.B,
                            IdleTradeAlpha);
                        _strokes.Add(new Stroke(
                            AtlasGeometry.HexToWorld(a, Z),
                            AtlasGeometry.HexToWorld(b, Z),
                            AtlasGeometry.ToColor32(idle), 0.45f));
                    }
                }
                return;
            }
            foreach (var seg in LaneLens.Segments(_model, _eye))
            {
                if (_mode == LaneMode.QuarantineOnly
                    && seg.Status != LaneStatus.Quarantined) continue;
                float factor = _mode == LaneMode.QuarantineOnly ? 1.8f : 1f;
                _strokes.Add(new Stroke(
                    AtlasGeometry.HexToWorld(seg.A, Z),
                    AtlasGeometry.HexToWorld(seg.B, Z),
                    AtlasGeometry.ToColor32(seg.Color), factor));
            }
        }

        private void Rebuild()
        {
            if (_strokes == null) return;
            var vertices = new Vector3[_strokes.Count * 4];
            var colors = new Color32[_strokes.Count * 4];
            var triangles = new int[_strokes.Count * 6];
            for (int i = 0; i < _strokes.Count; i++)
            {
                var stroke = _strokes[i];
                var dir = (stroke.B - stroke.A).normalized;
                var side = new Vector3(-dir.y, dir.x, 0f)
                    * (_width * stroke.WidthFactor * 0.5f);
                int v = i * 4;
                vertices[v] = stroke.A - side;
                vertices[v + 1] = stroke.A + side;
                vertices[v + 2] = stroke.B + side;
                vertices[v + 3] = stroke.B - side;
                for (int k = 0; k < 4; k++) colors[v + k] = stroke.Color;
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
        }
    }
}
