using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.Atlas
{
    /// <summary>Accumulates the orbit diagram's primitives into one vertex-colored
    /// mesh (orbit-diagram spec §5), recording the vertex range per BodyRef so
    /// single elements recolor in place (HexMeshBuilder.RecolorOne idiom).
    /// Sprites/Default culls neither face, so triangle winding is not significant.</summary>
    public sealed class OrbitMeshBuilder
    {
        public const int RingSegments = 96;
        public const int DashCount = 48;
        public const int DiscSegments = 24;
        private const float SettledOutlinePad = 0.03f;
        private const float HaloScale = 1.7f;

        private readonly List<Vector3> _vertices = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _triangles = new();
        private readonly Dictionary<BodyRef, (int Start, int Count)> _ranges = new();

        /// <summary>Draw order per spec §5: hab annuli → rings → stars (halo under
        /// disc) → bodies and moons (detail overlays on top) → settled outlines.
        /// Fills baseColors for selection recolor. Detail overlays (halos, gas
        /// bands, ocean blobs) are deliberately unkeyed: Recolor restores one flat
        /// base color, so keyed detail verts would be wiped on deselect.</summary>
        public static OrbitMeshBuilder Compose(OrbitLayoutResult layout,
            Dictionary<BodyRef, Color32> baseColors)
        {
            var builder = new OrbitMeshBuilder();
            foreach (var band in layout.HabBands)
                builder.AddAnnulus(band.Center, band.Inner, band.Outer, OrbitPalette.HabBand);
            foreach (var ring in layout.Rings)
            {
                if (ring.IsBelt)
                {
                    var beltColor = OrbitPalette.BodyColor(StarGen.Core.Model.BodyKind.PlanetoidBelt);
                    builder.AddDashedRing(ring.Center, ring.Radius, OrbitLayout.RingStroke,
                        beltColor, ring.Ref);
                    baseColors[ring.Ref] = beltColor;
                }
                else
                {
                    builder.AddRing(ring.Center, ring.Radius, OrbitLayout.RingStroke,
                        OrbitPalette.Ring);
                }
            }
            foreach (var star in layout.Stars)
            {
                var color = OrbitPalette.StarColor(star.TypeId);
                var key = new BodyRef(star.StarIndex, -1, -1);
                builder.AddDisc(star.Pos, star.Radius * HaloScale, OrbitPalette.Halo(color));
                builder.AddDisc(star.Pos, star.Radius, color, key);
                baseColors[key] = color;
            }
            foreach (var body in layout.Bodies)
            {
                var color = body.Ref.Moon >= 0 ? OrbitPalette.Moon : OrbitPalette.BodyColor(body.Kind);
                builder.AddDisc(body.Pos, body.Radius, color, body.Ref);
                baseColors[body.Ref] = color;
                if (body.Ref.Moon < 0) builder.AddBodyDetail(body);
            }
            foreach (var body in layout.Bodies)
                if (body.Settled)
                    builder.AddRing(body.Pos, body.Radius + SettledOutlinePad,
                        OrbitLayout.RingStroke, OrbitPalette.SettledOutline);
            return builder;
        }

        public void AddRing(Vector2 center, float radius, float stroke, Color32 color) =>
            AddArcStrip(center, radius, stroke, 0f, 2f * Mathf.PI, RingSegments, color);

        public void AddDashedRing(Vector2 center, float radius, float stroke, Color32 color,
            BodyRef? key = null)
        {
            int start = _vertices.Count;
            float slice = 2f * Mathf.PI / DashCount;
            for (int d = 0; d < DashCount; d++)
                AddArcStrip(center, radius, stroke, d * slice, d * slice + slice * 0.55f, 2, color);
            if (key is { } k) _ranges[k] = (start, _vertices.Count - start);
        }

        public void AddDisc(Vector2 center, float radius, Color32 color, BodyRef? key = null)
        {
            int start = _vertices.Count;
            _vertices.Add(new Vector3(center.x, center.y, 0f));
            _colors.Add(color);
            for (int i = 0; i <= DiscSegments; i++)
            {
                float a = 2f * Mathf.PI * i / DiscSegments;
                _vertices.Add(new Vector3(center.x + radius * Mathf.Cos(a),
                    center.y + radius * Mathf.Sin(a), 0f));
                _colors.Add(color);
            }
            for (int i = 0; i < DiscSegments; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + 1 + i);
                _triangles.Add(start + 2 + i);
            }
            if (key is { } k) _ranges[k] = (start, _vertices.Count - start);
        }

        public void AddAnnulus(Vector2 center, float inner, float outer, Color32 color) =>
            AddArcStrip(center, (inner + outer) * 0.5f, outer - inner, 0f, 2f * Mathf.PI,
                RingSegments, color);

        /// <summary>Horizontal stripe clipped to stay inside a disc of discRadius:
        /// the gas-giant band motif. Offsets/heights are fractions of the radius.</summary>
        public void AddDiscBand(Vector2 center, float discRadius, float yOffsetFrac,
            float heightFrac, Color32 color)
        {
            float y0 = yOffsetFrac * discRadius;
            float h = heightFrac * discRadius;
            float yEdge = Mathf.Min(Mathf.Abs(y0) + h * 0.5f, discRadius * 0.98f);
            float w = Mathf.Sqrt(Mathf.Max(0f, discRadius * discRadius - yEdge * yEdge));
            int v = _vertices.Count;
            _vertices.Add(new Vector3(center.x - w, center.y + y0 - h * 0.5f, 0f));
            _vertices.Add(new Vector3(center.x + w, center.y + y0 - h * 0.5f, 0f));
            _vertices.Add(new Vector3(center.x + w, center.y + y0 + h * 0.5f, 0f));
            _vertices.Add(new Vector3(center.x - w, center.y + y0 + h * 0.5f, 0f));
            for (int i = 0; i < 4; i++) _colors.Add(color);
            _triangles.Add(v); _triangles.Add(v + 1); _triangles.Add(v + 2);
            _triangles.Add(v); _triangles.Add(v + 2); _triangles.Add(v + 3);
        }

        /// <summary>Per-kind detail overlay: gas giants get one or two horizontal
        /// bands (count from DetailHash), rocky/ice worlds with oceans get blue
        /// blobs covering roughly Hydrographics% of the disc. Placement derives
        /// from DetailHash only — same system, same picture.</summary>
        private void AddBodyDetail(BodySpec body)
        {
            if (body.Kind == StarGen.Core.Model.BodyKind.GasGiant)
            {
                AddDiscBand(body.Pos, body.Radius, 0.25f, 0.22f, OrbitPalette.GasBand);
                if (body.DetailHash >= 0.5f)
                    AddDiscBand(body.Pos, body.Radius, -0.4f, 0.16f, OrbitPalette.GasBand);
                return;
            }
            bool wetWorld = body.Kind == StarGen.Core.Model.BodyKind.RockyWorld
                || body.Kind == StarGen.Core.Model.BodyKind.IceWorld;
            if (!wetWorld || body.Hydrographics <= 0) return;

            int blobs = body.Hydrographics >= 60 ? 3 : 2;
            float blobRadius = body.Radius * Mathf.Sqrt(body.Hydrographics / 100f / blobs);
            for (int j = 0; j < blobs; j++)
            {
                float angle = 2f * Mathf.PI * Frac(body.DetailHash * (j + 1) * 7.31f);
                float dist = (body.Radius - blobRadius) * Frac(body.DetailHash * (j + 2) * 3.77f);
                var blobPos = body.Pos + dist * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddDisc(blobPos, blobRadius, OrbitPalette.Ocean);
            }
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        public bool TryGetRange(BodyRef key, out int start, out int count)
        {
            if (_ranges.TryGetValue(key, out var range))
            {
                start = range.Start;
                count = range.Count;
                return true;
            }
            start = 0;
            count = 0;
            return false;
        }

        public Mesh Build()
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Recolor(Mesh mesh, int start, int count, Color32 color)
        {
            var colors = mesh.colors32;
            for (int v = start; v < start + count; v++) colors[v] = color;
            mesh.SetColors(colors);
        }

        private void AddArcStrip(Vector2 center, float radius, float stroke,
            float angleFrom, float angleTo, int segments, Color32 color)
        {
            float rIn = Mathf.Max(0f, radius - stroke * 0.5f);
            float rOut = radius + stroke * 0.5f;
            int baseVertex = _vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(angleFrom, angleTo, (float)i / segments);
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                _vertices.Add(new Vector3(center.x + cos * rIn, center.y + sin * rIn, 0f));
                _vertices.Add(new Vector3(center.x + cos * rOut, center.y + sin * rOut, 0f));
                _colors.Add(color);
                _colors.Add(color);
            }
            for (int i = 0; i < segments; i++)
            {
                int v = baseVertex + i * 2;
                _triangles.Add(v); _triangles.Add(v + 2); _triangles.Add(v + 1);
                _triangles.Add(v + 1); _triangles.Add(v + 2); _triangles.Add(v + 3);
            }
        }
    }
}
