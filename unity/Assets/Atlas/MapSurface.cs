using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The continuous map: one hex-resolution mesh for the whole
    /// disc (cells are exact radius-5 superhexes, so per-cell spirals
    /// enumerate every hex once). Nature shades per cell expand to their
    /// hexes; the domain overlay samples service radii per hex — that per-
    /// hex sampling is where the organic borders come from. Draw only:
    /// every color decision lives in src/Core/Atlas.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MapSurface : MonoBehaviour
    {
        private AtlasReadModel _model;
        private EyeContext _eye;
        private Mesh _mesh;
        private readonly List<HexCoordinate> _hexes = new();
        private readonly List<int> _cellIndexOfHex = new();
        private IReadOnlyList<Rgba> _domainShades;   // per hex, cached per Show

        public NatureLayer Nature = NatureLayer.Density;
        public bool ShowDomains = true;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        private void OnDestroy()
        {
            if (_mesh != null) DestroySafe(_mesh);
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                DestroySafe(renderer.sharedMaterial);
        }

        public void Show(AtlasReadModel model, EyeContext eye)
        {
            _model = model;
            _eye = eye;
            _hexes.Clear();
            _cellIndexOfHex.Clear();
            for (int c = 0; c < model.Cells.Count; c++)
            {
                var center = HexGrid.CellCenter(model.Cells[c].Coord);
                foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
                {
                    _hexes.Add(hex);
                    _cellIndexOfHex.Add(c);
                }
            }
            _domainShades = DomainLens.HexShades(model, eye, _hexes);

            if (_mesh != null) DestroySafe(_mesh);
            _mesh = HexMeshBuilder.Build(_hexes, ComposeColors());
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        /// <summary>Re-derives colors for the current lens selection and
        /// recolors in place — no geometry rebuild on a lens switch.</summary>
        public void Restyle()
        {
            if (_model == null || _mesh == null) return;
            HexMeshBuilder.Recolor(_mesh, ComposeColors());
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        private List<Color32> ComposeColors()
        {
            var natureByCell = NatureLens.Shades(_model, _eye, Nature);
            var baseShades = new Rgba[_hexes.Count];
            for (int i = 0; i < baseShades.Length; i++)
                baseShades[i] = natureByCell[_cellIndexOfHex[i]];
            IReadOnlyList<Rgba> composed = ShowDomains
                ? LensStack.Composite(baseShades, _domainShades)
                : baseShades;
            var colors = new List<Color32>(_hexes.Count);
            foreach (var s in composed)
                colors.Add(new Color32(s.R, s.G, s.B, s.A));
            return colors;
        }
    }
}
