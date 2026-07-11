using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>Lanes as literal highways: one quad per lane between its
    /// port hexes, colored by LaneLens status, width owned by the LOD band.
    /// Sits just above the map surface.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LaneLayer : MonoBehaviour
    {
        private const float Z = -0.2f;
        private IReadOnlyList<LaneSegment> _segments;
        private Mesh _mesh;
        private float _width = 1.4f;

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _segments = LaneLens.Segments(model, eye);
            Rebuild();
        }

        public void SetBand(LodBand band)
        {
            float width = LodBands.LaneWidth(band);
            if (Mathf.Approximately(width, _width)) return;
            _width = width;
            Rebuild();
        }

        public void SetVisible(bool visible) =>
            GetComponent<MeshRenderer>().enabled = visible;

        private void Rebuild()
        {
            if (_segments == null) return;
            var vertices = new Vector3[_segments.Count * 4];
            var colors = new Color32[_segments.Count * 4];
            var triangles = new int[_segments.Count * 6];
            for (int i = 0; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                var (ax, ay) = HexGrid.HexToWorld(seg.A);
                var (bx, by) = HexGrid.HexToWorld(seg.B);
                var a = new Vector3((float)ax, (float)ay, Z);
                var b = new Vector3((float)bx, (float)by, Z);
                var dir = (b - a).normalized;
                var side = new Vector3(-dir.y, dir.x, 0f) * (_width * 0.5f);
                int v = i * 4;
                vertices[v] = a - side;
                vertices[v + 1] = a + side;
                vertices[v + 2] = b + side;
                vertices[v + 3] = b - side;
                var color = new Color32(seg.Color.R, seg.Color.G, seg.Color.B, seg.Color.A);
                for (int k = 0; k < 4; k++) colors[v + k] = color;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
