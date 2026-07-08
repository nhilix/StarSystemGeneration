using System;
using System.Collections.Generic;
using StarGen.Core.Model;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>One system as a nested-concentric orbit diagram in a single
    /// procedural mesh (orbit-diagram spec §5). Same scaffolding as CellView:
    /// Show/Pick/MapBounds, plus per-element selection recolor.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SystemView : MonoBehaviour
    {
        private Mesh? _mesh;
        private OrbitLayoutResult? _layout;
        private OrbitMeshBuilder? _builder;
        private readonly Dictionary<BodyRef, Color32> _baseColors = new();
        private BodyRef? _selected;

        public Bounds MapBounds => _mesh != null ? _mesh.bounds : new Bounds();

        private void Awake() =>
            GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));

        public void Show(StarSystem system)
        {
            _selected = null;
            _baseColors.Clear();
            _layout = OrbitLayout.Compute(system);
            _builder = OrbitMeshBuilder.Compose(_layout, _baseColors);
            if (_mesh != null) Destroy(_mesh);
            _mesh = _builder.Build();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        public BodyRef? Pick(Vector2 screenPos, Camera cam)
        {
            if (_layout == null) return null;
            var world = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var local = world - transform.position;
            return OrbitLayout.PickAt(_layout, new Vector2(local.x, local.y));
        }

        public void SetSelected(BodyRef? key)
        {
            if (_mesh == null || _builder == null || Nullable.Equals(_selected, key)) return;
            if (_selected is { } previous
                && _builder.TryGetRange(previous, out int prevStart, out int prevCount))
                OrbitMeshBuilder.Recolor(_mesh, prevStart, prevCount, _baseColors[previous]);
            _selected = null;
            if (key is { } next && _builder.TryGetRange(next, out int nextStart, out int nextCount))
            {
                OrbitMeshBuilder.Recolor(_mesh, nextStart, nextCount,
                    LayerPalette.Highlight(_baseColors[next]));
                _selected = next;
            }
        }
    }
}
