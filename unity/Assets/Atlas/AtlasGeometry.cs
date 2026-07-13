using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>Plane conventions and unit conversions. The galactic plane
    /// is world XY at z=0 (HexToWorld verbatim); layers sit at small
    /// negative z offsets toward the camera. One hex step ≈ √3 world units.</summary>
    public static class AtlasGeometry
    {
        public const float HexStep = 1.7320508f;

        public static Vector3 HexToWorld(HexCoordinate h, float z = 0f)
        {
            var (x, y) = HexGrid.HexToWorld(h);
            return new Vector3((float)x, (float)y, z);
        }

        /// <summary>World-space bounds of the disc (cell centers padded by
        /// the superhex circumradius).</summary>
        public static Bounds DiscBounds(AtlasReadModel model)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var cell in model.Cells)
            {
                var (x, y) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
                min = Vector2.Min(min, new Vector2((float)x, (float)y));
                max = Vector2.Max(max, new Vector2((float)x, (float)y));
            }
            // A rim cell's hexes reach ~10 world past its center, rim port
            // glows ~26, and the nature gather feathers ~30 — the quads
            // must circumscribe all of it or the edges clip.
            const float pad = 48f;
            var b = new Bounds();
            b.SetMinMax(new Vector3(min.x - pad, min.y - pad, -1f),
                        new Vector3(max.x + pad, max.y + pad, 1f));
            return b;
        }

        public static Color32 ToColor32(Rgba c) => new(c.R, c.G, c.B, c.A);
    }

    /// <summary>Runtime-generated textures (the asset boundary: the soft
    /// dot is a point-spread function, not an identity glyph — generated,
    /// not authored).</summary>
    public static class AtlasTextures
    {
        private static Texture2D _softDot;
        private static Texture2D _solidDot;
        private static Texture2D _ring;

        /// <summary>64² crisp filled circle with a 2-texel AA rim — the
        /// port marker until authored tier glyphs land (K2).</summary>
        public static Texture2D SolidDot
        {
            get
            {
                if (_solidDot != null) return _solidDot;
                const int size = 64;
                _solidDot = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = 1f - Mathf.Clamp01((d - 0.92f) / 0.08f);
                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)(a * 255f));
                    }
                _solidDot.SetPixels32(pixels);
                _solidDot.Apply();
                return _solidDot;
            }
        }

        /// <summary>64² thin annulus with AA edges — blockade rings and
        /// news-pulse fronts (a ring is a state, not an identity glyph).</summary>
        public static Texture2D Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int size = 64;
                _ring = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        // Band centered at 0.86 diameter, ~0.10 wide.
                        float a = 1f - Mathf.Clamp01(
                            (Mathf.Abs(d - 0.86f) - 0.05f) / 0.05f);
                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)(a * 255f));
                    }
                _ring.SetPixels32(pixels);
                _ring.Apply();
                return _ring;
            }
        }

        private static Texture2D _thinRing;

        /// <summary>64² hairline annulus (~4% band at 0.90 diameter) — the
        /// option-A marker stroke: port and settled-world rings read as
        /// thin outlines, not bands (K5 eyeball wave).</summary>
        public static Texture2D ThinRing
        {
            get
            {
                if (_thinRing != null) return _thinRing;
                const int size = 64;
                _thinRing = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = 1f - Mathf.Clamp01(
                            (Mathf.Abs(d - 0.90f) - 0.02f) / 0.035f);
                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)(a * 255f));
                    }
                _thinRing.SetPixels32(pixels);
                _thinRing.Apply();
                return _thinRing;
            }
        }

        private static Texture2D _squareRing;

        /// <summary>64² thin square outline with AA edges — the facility
        /// mark in the orbit stage (K5; the design mock draws facilities
        /// as square outlines beside their body).</summary>
        public static Texture2D SquareRing
        {
            get
            {
                if (_squareRing != null) return _squareRing;
                const int size = 64;
                _squareRing = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f;
                        float dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                        // Chebyshev distance: a square band at 0.80,
                        // ~0.14 wide, AA shoulders.
                        float d = Mathf.Max(dx, dy);
                        float a = 1f - Mathf.Clamp01(
                            (Mathf.Abs(d - 0.80f) - 0.07f) / 0.06f);
                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)(a * 255f));
                    }
                _squareRing.SetPixels32(pixels);
                _squareRing.Apply();
                return _squareRing;
            }
        }

        /// <summary>64² radial gaussian-ish falloff, white, alpha-carried.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int size = 64;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);   // smoothstep shoulder
                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)(a * 255f));
                    }
                _softDot.SetPixels32(pixels);
                _softDot.Apply();
                return _softDot;
            }
        }
    }
}
