using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>One cell's 91 hexes at hex resolution, recentered on the origin.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CellView : MonoBehaviour
    {
        private GalaxyService? _service;
        private HexCoordinate _cellCoord;
        private Vector3 _centerOffset;
        private Mesh? _mesh;
        private readonly List<HexCoordinate> _hexes = new();
        private readonly Dictionary<HexCoordinate, int> _indexOf = new();
        private HexCoordinate? _selected;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(GalaxyService service, HexCoordinate cellCoord)
        {
            _service = service;
            _cellCoord = cellCoord;
            _selected = null;
            var center = HexGrid.CellCenter(cellCoord);
            var (cx, cy) = HexGrid.HexToWorld(center);
            _centerOffset = new Vector3((float)cx, (float)cy, 0f);

            _hexes.Clear();
            _indexOf.Clear();
            var colors = new List<Color32>();
            foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
            {
                _indexOf[hex] = _hexes.Count;
                _hexes.Add(hex);
                colors.Add(LayerPalette.HexColor(service.StateOf(hex)));
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = HexMeshBuilder.Build(_hexes, colors, positionOf: h =>
            {
                var (wx, wy) = HexGrid.HexToWorld(h);
                return new Vector3((float)wx, (float)wy, 0f) - _centerOffset;
            });
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public void SetSelected(HexCoordinate? hex)
        {
            if (_service == null || _mesh == null || Equals(_selected, hex)) return;
            if (_selected is { } previous && _indexOf.TryGetValue(previous, out int prevIndex))
                HexMeshBuilder.RecolorOne(_mesh, prevIndex,
                    LayerPalette.HexColor(_service.StateOf(previous)));
            _selected = null;
            if (hex is { } next && _indexOf.TryGetValue(next, out int nextIndex))
            {
                HexMeshBuilder.RecolorOne(_mesh, nextIndex,
                    LayerPalette.Highlight(LayerPalette.HexColor(_service.StateOf(next))));
                _selected = next;
            }
        }

        public HexCoordinate? Pick(Vector2 screenPos, Camera cam)
        {
            if (_service == null) return null;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var local = world + _centerOffset - transform.position;
            var hex = HexGrid.WorldToHex(local.x, local.y);
            return _indexOf.ContainsKey(hex) ? hex : null;
        }
    }
}
