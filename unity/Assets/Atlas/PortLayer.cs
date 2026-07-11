using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>Port markers: a diamond per port, sized by tier and LOD
    /// band, brightened owner color (PortLens). The keystone reads above
    /// its own glow. Sits above the lanes.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PortLayer : MonoBehaviour
    {
        private const float Z = -0.4f;
        private IReadOnlyList<PortMarker> _markers;
        private Mesh _mesh;
        private float _scale = 2.5f;

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        private void OnDestroy()
        {
            if (_mesh != null) DestroyResource(_mesh);
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                DestroyResource(renderer.sharedMaterial);
        }

        private static void DestroyResource(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _markers = PortLens.Markers(model, eye);
            Rebuild();
        }

        public void SetBand(LodBand band)
        {
            float scale = LodBands.PortScale(band);
            if (Mathf.Approximately(scale, _scale)) return;
            _scale = scale;
            Rebuild();
        }

        public void SetVisible(bool visible) =>
            GetComponent<MeshRenderer>().enabled = visible;

        private void Rebuild()
        {
            if (_markers == null) return;
            var vertices = new Vector3[_markers.Count * 4];
            var colors = new Color32[_markers.Count * 4];
            var triangles = new int[_markers.Count * 6];
            for (int i = 0; i < _markers.Count; i++)
            {
                var m = _markers[i];
                var (wx, wy) = HexGrid.HexToWorld(m.Hex);
                var c = new Vector3((float)wx, (float)wy, Z);
                float r = (0.5f + 0.25f * m.Tier) * _scale;
                int v = i * 4;
                vertices[v] = c + new Vector3(0, r, 0);
                vertices[v + 1] = c + new Vector3(r, 0, 0);
                vertices[v + 2] = c + new Vector3(0, -r, 0);
                vertices[v + 3] = c + new Vector3(-r, 0, 0);
                var color = new Color32(m.Color.R, m.Color.G, m.Color.B, 255);
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
