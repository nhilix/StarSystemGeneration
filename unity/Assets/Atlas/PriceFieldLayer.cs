using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The price lens as a plane field: PriceLens.CellShades
    /// baked into a bilinear data texture over the disc (the
    /// NatureFieldLayer pattern — economics instead of gas). The chip's
    /// good re-bakes; unserviced wilds stay clear.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PriceFieldLayer : MonoBehaviour
    {
        private const float Z = 0.02f;
        private const int TextureSize = 256;

        private AtlasReadModel _model;
        private EyeContext _eye;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _texture;
        private Bounds _bounds;
        private GoodId _good = GoodId.Provisions;
        private bool _on;

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
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

        public GoodId Good => _good;

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _model = model;
            _eye = eye;
            _bounds = AtlasGeometry.DiscBounds(model);
            BuildQuad(_bounds);
            if (_on) Bake();
        }

        public void SetVisible(bool visible)
        {
            _on = visible;
            if (visible && _model != null) Bake();
            GetComponent<MeshRenderer>().enabled = visible && _model != null;
        }

        /// <summary>The parameterized lens's argument — the chip carries
        /// the good; changing it re-bakes the field.</summary>
        public void SetGood(GoodId good)
        {
            if (_good == good) return;
            _good = good;
            if (_on && _model != null) Bake();
        }

        private void Bake()
        {
            var shades = PriceLens.CellShades(_model, _eye, _good);
            if (_texture == null)
            {
                _texture = new Texture2D(TextureSize, TextureSize,
                                         TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
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
                    var cell = HexGrid.CellOf(HexGrid.WorldToHex(wx, wy));
                    pixels[y * TextureSize + x] =
                        _model.TryIndexOfCell(cell, out int idx)
                            ? AtlasGeometry.ToColor32(shades[idx])
                            : default;
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply();
            _material.mainTexture = _texture;
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
