using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>The galaxy map: one unit hex per region cell, drawn in
    /// cell-lattice coordinate space (atlas plan: same HexGrid math, coarser
    /// interpretation).</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GalaxyView : MonoBehaviour
    {
        private GalaxyService? _service;
        private AtlasLayer _layer;
        private Mesh? _mesh;
        private readonly List<HexCoordinate> _cells = new();
        private readonly Dictionary<HexCoordinate, int> _indexOf = new();
        private HexCoordinate? _hover;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(GalaxyService service, AtlasLayer layer)
        {
            _service = service;
            _layer = layer;
            _hover = null;
            _cells.Clear();
            _indexOf.Clear();
            var colors = new List<Color32>();
            foreach (var cell in service.Skeleton.Cells)
            {
                _indexOf[cell.Coord] = _cells.Count;
                _cells.Add(cell.Coord);
                colors.Add(LayerPalette.CellColor(service.Skeleton, cell, layer));
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = HexMeshBuilder.Build(_cells, colors);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public void SetLayer(AtlasLayer layer)
        {
            if (_service == null || _mesh == null) return;
            _layer = layer;
            _hover = null;
            var colors = new List<Color32>();
            foreach (var cell in _service.Skeleton.Cells)
                colors.Add(LayerPalette.CellColor(_service.Skeleton, cell, layer));
            HexMeshBuilder.Recolor(_mesh, colors);
        }

        public void SetHover(HexCoordinate? cellCoord)
        {
            if (_service == null || _mesh == null || Equals(_hover, cellCoord)) return;
            if (_hover is { } previous && _indexOf.TryGetValue(previous, out int prevIndex))
                HexMeshBuilder.RecolorOne(_mesh, prevIndex,
                    LayerPalette.CellColor(_service.Skeleton, _service.Skeleton.CellAt(previous), _layer));
            _hover = null;
            if (cellCoord is { } next && _indexOf.TryGetValue(next, out int nextIndex))
            {
                var baseColor = LayerPalette.CellColor(
                    _service.Skeleton, _service.Skeleton.CellAt(next), _layer);
                HexMeshBuilder.RecolorOne(_mesh, nextIndex, LayerPalette.Highlight(baseColor));
                _hover = next;
            }
        }

        public HexCoordinate? Pick(Vector2 screenPos, Camera cam)
        {
            if (_service == null) return null;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var cellCoord = HexGrid.WorldToHex(world.x - transform.position.x, world.y - transform.position.y);
            return _indexOf.ContainsKey(cellCoord) ? cellCoord : null;
        }
    }
}
