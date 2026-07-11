using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The domains lens as a field: one plane-quad over the disc,
    /// shaded per pixel by the port registry (StarGen/DomainField). The
    /// port array re-uploads on every state change; organic borders and
    /// contested light are shader emergents, not geometry.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DomainFieldLayer : MonoBehaviour
    {
        private const int MaxPorts = 512;
        private const float Z = 0.05f;
        /// <summary>Glow bleeds a little past the hard service edge.</summary>
        private const float RadiusBloom = 1.25f;

        private Material _material;
        private Mesh _mesh;
        private readonly Vector4[] _ports = new Vector4[MaxPorts];
        private readonly Vector4[] _colors = new Vector4[MaxPorts];

        private void Awake()
        {
            _material = new Material(Shader.Find("StarGen/DomainField"));
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

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            var bounds = AtlasGeometry.DiscBounds(model);
            BuildQuad(bounds);

            var markers = PortLens.Markers(model, eye);
            int count = Mathf.Min(markers.Count, MaxPorts);
            for (int i = 0; i < count; i++)
            {
                var m = markers[i];
                var pos = AtlasGeometry.HexToWorld(m.Hex);
                float radius = m.ServiceRadiusHexes * AtlasGeometry.HexStep
                               * RadiusBloom;
                _ports[i] = new Vector4(pos.x, pos.y, radius, 0f);
                var own = AtlasPalette.OwnerColor(m.OwnerActorId);
                _colors[i] = new Vector4(own.R / 255f, own.G / 255f,
                                         own.B / 255f, 1f);
            }
            _material.SetVectorArray("_Ports", _ports);
            _material.SetVectorArray("_PortColors", _colors);
            _material.SetInt("_PortCount", count);
        }

        private void BuildQuad(Bounds b)
        {
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh();
            _mesh.SetVertices(new[]
            {
                new Vector3(b.min.x, b.min.y, Z),
                new Vector3(b.max.x, b.min.y, Z),
                new Vector3(b.max.x, b.max.y, Z),
                new Vector3(b.min.x, b.max.y, Z),
            });
            _mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
