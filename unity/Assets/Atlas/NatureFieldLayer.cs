using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The nature lens group as soft translucent fields: each
    /// raster bakes into a small data texture (one sample per world
    /// stride, bilinear-filtered) drawn on a plane quad under everything
    /// political — nebular fields, not a hex board. Off by default; the
    /// starfield alone carries density.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class NatureFieldLayer : MonoBehaviour
    {
        private const float Z = 0.10f;
        private const int TextureSize = 320;
        private const byte FieldAlpha = 150;

        private AtlasReadModel _model;
        private EyeContext _eye;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _texture;
        private Bounds _bounds;
        private NatureLayer? _current;

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"))
            { hideFlags = HideFlags.HideAndDontSave };
            GetComponent<MeshRenderer>().material = _material;
            GetComponent<MeshRenderer>().enabled = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) DestroyResource(_mesh);
            if (_material != null) DestroyResource(_material);
            if (_texture != null) DestroyResource(_texture);
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

        public NatureLayer? Current => _current;

        /// <summary>K5 hex→orbit crossfade (Sprites/Default: the material
        /// tint multiplies the baked field texture).</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            if (_material == null) return;   // edit-mode caller ordering
            _material.color = new Color(1f, 1f, 1f,
                LodBands.MapFade(cameraDistance, galaxyExtent));
        }

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _model = model;
            _eye = eye;
            _bounds = AtlasGeometry.DiscBounds(model);
            BuildQuad(_bounds);
            if (_current is { } layer) Bake(layer);
        }

        /// <summary>Select a nature layer (or null = off, starfield only).</summary>
        public void Select(NatureLayer? layer)
        {
            _current = layer;
            var renderer = GetComponent<MeshRenderer>();
            if (layer == null || _model == null)
            {
                renderer.enabled = false;
                return;
            }
            Bake(layer.Value);
            renderer.enabled = true;
        }

        private void Bake(NatureLayer layer)
        {
            // The sampler does the nebular work: Gaussian blend across
            // cells, presence-scaled alpha, void feathering, cloud noise.
            var sampler = new NatureFieldSampler(_model, _eye, layer);
            if (_texture == null)
            {
                _texture = new Texture2D(TextureSize, TextureSize,
                                         TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                double wy = Mathf.Lerp(_bounds.min.y, _bounds.max.y,
                                       (y + 0.5f) / TextureSize);
                for (int x = 0; x < TextureSize; x++)
                {
                    double wx = Mathf.Lerp(_bounds.min.x, _bounds.max.x,
                                           (x + 0.5f) / TextureSize);
                    var s = sampler.Sample(wx, wy);
                    pixels[y * TextureSize + x] = new Color32(
                        s.R, s.G, s.B, (byte)(s.A * FieldAlpha / 255));
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply();
            _material.mainTexture = _texture;
        }

        private void BuildQuad(Bounds b)
        {
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(new[]
            {
                new Vector3(b.min.x, b.min.y, Z),
                new Vector3(b.max.x, b.min.y, Z),
                new Vector3(b.max.x, b.max.y, Z),
                new Vector3(b.min.x, b.max.y, Z),
            });
            _mesh.SetUVs(0, new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            });
            _mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
