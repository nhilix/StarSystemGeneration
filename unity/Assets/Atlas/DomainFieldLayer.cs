using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The domains lens as a field: one plane-quad over the disc,
    /// shaded per pixel by the port registry (StarGen/DomainField). Ports
    /// carry their polity slot; polity colors and the pairwise relationship
    /// shades (DomainLens.OverlapShade — war/tension/warm/neutral) upload
    /// alongside, so union fills, border outlines and Venn overlaps are all
    /// shader emergents of read-model data.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DomainFieldLayer : MonoBehaviour
    {
        private const int MaxPorts = 512;
        private const int MaxSlots = 32;
        private const float Z = 0.05f;
        /// <summary>The union edge sits at the service-radius edge; a small
        /// bloom keeps borders off the exact hex boundary reads.</summary>
        private const float RadiusBloom = 1.05f;

        private Material _material;
        private Mesh _mesh;
        private Texture2D _relationTex;
        private readonly Vector4[] _ports = new Vector4[MaxPorts];
        private readonly Vector4[] _slotColors = new Vector4[MaxSlots];

        private void Awake()
        {
            _material = new Material(Shader.Find("StarGen/DomainField"));
            // Explicit values — property-block defaults have proven
            // unreliable for runtime-created materials under URP.
            _material.SetFloat("_FillIntensity", 0.13f);
            _material.SetFloat("_OverlapIntensity", 0.26f);
            _material.SetFloat("_BorderIntensity", 0.50f);
            _material.SetFloat("_BorderPx", 1.6f);
            GetComponent<MeshRenderer>().material = _material;
        }

        private void OnDestroy()
        {
            if (_mesh != null) DestroyResource(_mesh);
            if (_material != null) DestroyResource(_material);
            if (_relationTex != null) DestroyResource(_relationTex);
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
            BuildQuad(AtlasGeometry.DiscBounds(model));

            var slots = DomainLens.PolitySlots(model, eye);
            int slotCount = Mathf.Min(slots.Count, MaxSlots);
            var slotOf = new Dictionary<int, int>();
            for (int i = 0; i < slotCount; i++)
            {
                slotOf[slots[i]] = i;
                var own = AtlasPalette.OwnerColor(slots[i]);
                _slotColors[i] = new Vector4(own.R / 255f, own.G / 255f,
                                             own.B / 255f, 1f);
            }

            var markers = PortLens.Markers(model, eye);
            int count = Mathf.Min(markers.Count, MaxPorts);
            for (int i = 0; i < count; i++)
            {
                var m = markers[i];
                var pos = AtlasGeometry.HexToWorld(m.Hex);
                float radius = m.ServiceRadiusHexes * AtlasGeometry.HexStep
                               * RadiusBloom;
                // Polities past MaxSlots fold into the last slot — flagged
                // in the ledger; seed-scale galaxies stay well under 32.
                int slot = slotOf.TryGetValue(m.OwnerActorId, out int s)
                    ? s : MaxSlots - 1;
                _ports[i] = new Vector4(pos.x, pos.y, radius, slot);
            }

            BakeRelations(model, eye, slots, slotCount);

            _material.SetVectorArray("_Ports", _ports);
            _material.SetVectorArray("_SlotColors", _slotColors);
            _material.SetInt("_PortCount", count);
            _material.SetTexture("_RelationTex", _relationTex);
        }

        /// <summary>The pairwise relationship shades as a MaxSlots² lookup
        /// the shader samples at (slotA, slotB).</summary>
        private void BakeRelations(AtlasReadModel model, EyeContext eye,
                                   IReadOnlyList<int> slots, int slotCount)
        {
            if (_relationTex == null)
            {
                // linear:true = no sRGB decode on sample — the shader
                // composes in sRGB terms and linearizes once on output.
                _relationTex = new Texture2D(MaxSlots, MaxSlots,
                                             TextureFormat.RGBA32, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
            }
            var pixels = new Color32[MaxSlots * MaxSlots];
            for (int a = 0; a < slotCount; a++)
                for (int b = 0; b < slotCount; b++)
                {
                    if (a == b) continue;
                    var shade = DomainLens.OverlapShade(model, eye,
                                                        slots[a], slots[b]);
                    pixels[b * MaxSlots + a] = AtlasGeometry.ToColor32(shade);
                }
            _relationTex.SetPixels32(pixels);
            _relationTex.Apply();
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
