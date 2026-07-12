using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarGen.AtlasView
{
    /// <summary>The hex lattice as the artifact draws it: faint outlines
    /// only, fading in as the camera approaches Region and gone above it.
    /// GPU lines, built lazily on first approach.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LatticeLayer : MonoBehaviour
    {
        private const float Z = -0.02f;
        private static readonly Color LineColor = new(140 / 255f, 160 / 255f,
                                                      200 / 255f, 1f);
        private Mesh _mesh;
        private Material _material;
        private AtlasReadModel _model;
        private bool _built;

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

        public void Prepare(AtlasReadModel model)
        {
            _model = model;
            _built = false;
        }

        /// <summary>Continuous fade driven by camera altitude; builds the
        /// line mesh on first approach.</summary>
        public void OnZoom(float cameraDistance, float galaxyExtent)
        {
            float alpha = LodBands.LatticeAlpha(cameraDistance, galaxyExtent);
            var renderer = GetComponent<MeshRenderer>();
            if (alpha <= 0.001f)
            {
                renderer.enabled = false;
                return;
            }
            if (!_built && _model != null) Build();
            renderer.enabled = true;
            var c = LineColor;
            c.a = alpha;
            _material.color = c;
        }

        private void Build()
        {
            _built = true;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            // Every hex in the disc is present — the wilds are real,
            // addressable space nobody bothered to claim.
            foreach (var cell in _model.Cells)
            {
                var center = HexGrid.CellCenter(cell.Coord);
                foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
                {
                    var (wx, wy) = HexGrid.HexToWorld(hex);
                    int baseIndex = vertices.Count;
                    for (int c = 0; c < 6; c++)
                    {
                        var (ox, oy) = HexGrid.CornerOffsets[c];
                        vertices.Add(new Vector3((float)(wx + ox),
                                                 (float)(wy + oy), Z));
                    }
                    for (int c = 0; c < 6; c++)
                    {
                        indices.Add(baseIndex + c);
                        indices.Add(baseIndex + (c + 1) % 6);
                    }
                }
            }
            if (_mesh != null) DestroyResource(_mesh);
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32,
                               hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(vertices);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
