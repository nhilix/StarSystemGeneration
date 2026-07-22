using System;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarGen.AtlasView
{
    public enum SelectionKind
    { None, Hex, Port, Outpost, Project, Shipment, Fleet, Poi, Facility, System }

    /// <summary>What the user selected: a typed registry id plus the hex
    /// it stands on. The dock routes kinds to panels.</summary>
    public readonly struct Selection
    {
        public readonly SelectionKind Kind;
        public readonly int Id;
        public readonly HexCoordinate Hex;

        public Selection(SelectionKind kind, int id, HexCoordinate hex)
        {
            Kind = kind;
            Id = id;
            Hex = hex;
        }
    }

    /// <summary>K3 selection: plane-intersection picking (no colliders —
    /// the PoC lesson holds), hover hex + HexQuery info for the tooltip,
    /// click → typed selection resolved from the read model in priority
    /// order (port · construction site · freight · fleet · live POI ·
    /// bare hex). A faint ring marks the selected hex.</summary>
    public sealed class SelectionModel : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;
        [SerializeField] private SystemStage stage;

        private HexCoordinate? _hovered;
        private HexInfo _hoverInfo;
        private OutpostMark? _hoveredOutpost;
        private StagePick? _stageHover;
        private Vector2 _pressPos;
        private bool _pressLive;
        private Vector2 _rightPressPos;
        private bool _rightPressLive;
        private GameObject _marker;

        public HexCoordinate? HoveredHex => _hovered;
        public HexInfo HoverInfo => _hoverInfo;
        /// <summary>The live outpost under the cursor (AC1.4), or null — the
        /// hover source for the outpost tooltip line. Graduated outposts are a
        /// port now and read as the port, so they never surface here.</summary>
        public OutpostMark? HoveredOutpost => _hoveredOutpost;
        /// <summary>The orbit-view thing under the cursor while the stage
        /// is live (K5) — same selection model, one more source.</summary>
        public StagePick? StageHover => _stageHover;

        public event Action HoverChanged;
        public event Action<Selection> Selected;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        public void WireStage(SystemStage systemStage) => stage = systemStage;

        private void Update()
        {
            if (root == null || root.SimHost?.Model == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var screenPos = mouse.position.ReadValue();

            if (AtlasPointerGuard.Blocks(screenPos))
            {
                SetHovered(null);
                SetStageHover(null);
                _pressLive = false;
                return;
            }

            var hex = HexUnder(screenPos);
            SetHovered(hex);
            SetStageHover(stage != null && stage.Live
                ? PickStage(screenPos) : null);

            // A click is a press and release that never wandered — drags
            // belong to the camera.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressPos = screenPos;
                _pressLive = true;
            }
            if (_pressLive && mouse.leftButton.wasReleasedThisFrame)
            {
                _pressLive = false;
                if ((screenPos - _pressPos).sqrMagnitude <= 25f)
                {
                    // the orbit view wins while it is live — same panels,
                    // stage subjects (spec: same selection model)
                    if (_stageHover is StagePick pick)
                    {
                        MarkHex(pick.Hex);
                        Selected?.Invoke(new Selection(pick.Kind, pick.Id,
                                                       pick.Hex));
                    }
                    else if (hex != null)
                    {
                        Select(hex.Value);
                    }
                }
            }

            // a right-CLICK (no wander — right-drag stays the camera pan)
            // clears the selection highlight
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightPressPos = screenPos;
                _rightPressLive = true;
            }
            if (_rightPressLive && mouse.rightButton.wasReleasedThisFrame)
            {
                _rightPressLive = false;
                if ((screenPos - _rightPressPos).sqrMagnitude <= 25f)
                    ClearSelection();
            }
        }

        /// <summary>Drop the selection highlight (right-click; panels keep
        /// their own X buttons).</summary>
        public void ClearSelection()
        {
            if (_marker != null) _marker.SetActive(false);
        }

        private HexCoordinate? HexUnder(Vector2 screenPos)
        {
            var cam = root.CameraRig.Cam;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(
                new Vector3(screenPos.x, screenPos.y, 0f));
            float denom = ray.direction.z;
            if (Mathf.Abs(denom) < 1e-5f) return null;
            float t = -ray.origin.z / denom;
            if (t < 0f) return null;
            var p = ray.origin + ray.direction * t;
            return HexGrid.WorldToHex(p.x, p.y);
        }

        private void SetHovered(HexCoordinate? hex)
        {
            if (Nullable.Equals(hex, _hovered)) return;
            _hovered = hex;
            _hoverInfo = hex != null
                ? HexQuery.At(root.SimHost.Model,
                    EyeContext.God(root.SimHost.State.WorldYear), hex.Value)
                : null;
            _hoveredOutpost = hex != null ? OutpostAt(hex.Value) : null;
            HoverChanged?.Invoke();
        }

        private void SetStageHover(StagePick? pick)
        {
            if (Nullable.Equals(pick?.Kind, _stageHover?.Kind)
                && pick?.Id == _stageHover?.Id
                && Nullable.Equals(pick?.Hex, _stageHover?.Hex)
                && pick?.Label == _stageHover?.Label) return;
            _stageHover = pick;
            HoverChanged?.Invoke();
        }

        /// <summary>The nearest stage pickable under the cursor: distance
        /// on the stage's plane against each pickable's world radius (with
        /// a pixel floor so small marks stay clickable at altitude).</summary>
        private StagePick? PickStage(Vector2 screenPos)
        {
            var cam = root.CameraRig.Cam;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(
                new Vector3(screenPos.x, screenPos.y, 0f));
            float denom = ray.direction.z;
            if (Mathf.Abs(denom) < 1e-5f) return null;
            float t = (SystemStage.StageZ - ray.origin.z) / denom;
            if (t < 0f) return null;
            var p = ray.origin + ray.direction * t;

            // ~9 px of grace: world units one pixel spans at this depth
            float pxWorld = 2f * root.CameraRig.Distance
                * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)
                / Mathf.Max(1, cam.pixelHeight);
            float grace = 9f * pxWorld;

            StagePick? best = null;
            float bestDist = float.MaxValue;
            foreach (var pick in stage.Pickables)
            {
                var d = new Vector2(p.x - pick.WorldPos.x,
                                    p.y - pick.WorldPos.y).magnitude;
                float reach = Mathf.Max(pick.Radius, grace);
                if (d > reach) continue;
                // near-ties go to the higher priority — the port ring
                // wraps its own body and must win the shared center (the
                // map's port-first order, kept)
                bool tie = best != null
                    && Mathf.Abs(d - bestDist) <= grace * 0.5f;
                if (tie ? pick.Priority > best.Value.Priority : d < bestDist)
                {
                    bestDist = d;
                    best = pick;
                }
            }
            return best;
        }

        /// <summary>Resolve what the click means, most specific first.</summary>
        private void Select(HexCoordinate hex)
        {
            var model = root.SimHost.Model;
            var state = root.SimHost.State;
            var eye = EyeContext.God(state.WorldYear);
            var info = _hoverInfo ?? HexQuery.At(model, eye, hex);
            MarkHex(hex);
            Selected?.Invoke(Resolve(model, state, eye, hex, info));
        }

        /// <summary>Resolve a clicked hex to a typed selection, most specific
        /// first (the K3 order, AC1.4 inserting the outpost): port · outpost ·
        /// construction site · freight · fleet · live POI · bare hex. An
        /// outpost is the headline of its hex — after a real port (a graduated
        /// outpost's hex IS a port, which wins), before the works. Pure over
        /// the read model so resolution is EditMode-coverable without the
        /// pointer stack.</summary>
        public static Selection Resolve(AtlasReadModel model,
            Core.Epoch.SimState state, EyeContext eye, HexCoordinate hex,
            HexInfo info)
        {
            if (info.PortId >= 0)
                return new Selection(SelectionKind.Port, info.PortId, hex);
            if (FindOutpost(state, hex) is int outpostId)
                return new Selection(SelectionKind.Outpost, outpostId, hex);
            if (FindProject(state, hex) is int projectId)
                return new Selection(SelectionKind.Project, projectId, hex);
            if (FindFreight(model, eye, hex) is int shipmentId)
                return new Selection(SelectionKind.Shipment, shipmentId, hex);
            if (FindFleet(state, hex) is int fleetId)
                return new Selection(SelectionKind.Fleet, fleetId, hex);
            if (info.LivePois.Count > 0)
                return new Selection(SelectionKind.Poi, info.LivePois[0].Id, hex);
            return new Selection(SelectionKind.Hex, -1, hex);
        }

        private static int? FindOutpost(Core.Epoch.SimState state,
                                        HexCoordinate hex)
        {
            foreach (var o in state.Outposts)         // id order (P6), first match
                if (o.Hex.Equals(hex)) return o.Id;
            return null;
        }

        /// <summary>The live outpost mark at a hex (name + candidacy for the
        /// tooltip), resolved through the same DomainInteriorMarks derivation
        /// the outpost layer draws.</summary>
        private OutpostMark? OutpostAt(HexCoordinate hex)
        {
            var model = root.SimHost.Model;
            var state = root.SimHost.State;
            if (model == null || state == null) return null;
            var eye = EyeContext.God(state.WorldYear);
            foreach (var o in state.Outposts)
            {
                if (o.Graduated || !o.Hex.Equals(hex)) continue;
                foreach (var m in
                         DomainInteriorMarks.ForPort(model, eye, o.ParentPortId)
                             .Outposts)
                    if (m.OutpostId == o.Id) return m;
                return null;
            }
            return null;
        }

        private static int? FindProject(Core.Epoch.SimState state,
                                        HexCoordinate hex)
        {
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.Hex.Equals(hex)) return p.Id;
            return null;
        }

        private static int? FindFleet(Core.Epoch.SimState state,
                                      HexCoordinate hex)
        {
            foreach (var f in state.Fleets)               // id order (P6)
                if (f.TotalHulls > 0 && f.Hex.Equals(hex)) return f.Id;
            return null;
        }

        private static int? FindFreight(AtlasReadModel model, EyeContext eye,
                                        HexCoordinate hex)
        {
            foreach (var mark in WorksLens.Freight(model, eye))
                if (mark.Hex.Equals(hex)) return mark.ShipmentId;
            return null;
        }

        // ---- the selected-hex highlight: the lattice's own grammar, one
        // hex, bolder — a hexagonal ring mesh (6 trapezoid quads on the
        // same CornerOffsets the lattice draws), moved to the selection,
        // never LOD-faded ----

        private static readonly Color OutlineColor =
            new Color32(0x86, 0xD7, 0xFF, 0xE6);   // the UI accent — an
        // affordance over the map, not a data color

        private Mesh _ringMesh;
        private Material _ringMaterial;
        private float _ringThickness = -1f;

        private void MarkHex(HexCoordinate hex)
        {
            if (_marker == null)
            {
                _marker = new GameObject("SelectionHighlight");
                _marker.AddComponent<MeshFilter>();
                var renderer = _marker.AddComponent<MeshRenderer>();
                _ringMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    color = OutlineColor,
                    renderQueue = 3200,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                renderer.sharedMaterial = _ringMaterial;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            // above the lattice (its Z is -0.02), below screen-space chrome
            _marker.transform.position = AtlasGeometry.HexToWorld(hex, -0.03f);
            _marker.SetActive(true);
            RebuildRing(force: false);
        }

        /// <summary>The ring in local space: outer edge exactly on the hex
        /// border, inner edge inset by the stroke — rebuilt only when the
        /// zoom band moves the stroke meaningfully.</summary>
        private void RebuildRing(bool force)
        {
            float distance = root?.CameraRig != null
                ? root.CameraRig.Distance : 20f;
            // screen-constant ~3px stroke (the lattice's weight, eyeball
            // wave: the old clamp read bulky at system zoom) — world size
            // still grows with distance, so it never LOD-vanishes
            float thickness = Mathf.Clamp(distance * 0.0028f, 0.008f, 0.6f);
            if (!force && Mathf.Abs(thickness - _ringThickness)
                    < _ringThickness * 0.15f) return;
            _ringThickness = thickness;

            var vertices = new Vector3[12];
            for (int c = 0; c < 6; c++)
            {
                var (ox, oy) = HexGrid.CornerOffsets[c];
                vertices[c] = new Vector3((float)ox, (float)oy, 0f);
                vertices[6 + c] = new Vector3(
                    (float)(ox * (1f - thickness)),
                    (float)(oy * (1f - thickness)), 0f);
            }
            var triangles = new int[36];
            for (int c = 0; c < 6; c++)
            {
                int n = (c + 1) % 6;
                int t = c * 6;
                triangles[t] = c; triangles[t + 1] = n; triangles[t + 2] = 6 + c;
                triangles[t + 3] = n; triangles[t + 4] = 6 + n; triangles[t + 5] = 6 + c;
            }
            if (_ringMesh == null)
                _ringMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _ringMesh.Clear();
            _ringMesh.vertices = vertices;
            _ringMesh.triangles = triangles;
            _ringMesh.RecalculateBounds();
            _marker.GetComponent<MeshFilter>().sharedMesh = _ringMesh;
        }

        private void LateUpdate()
        {
            if (_marker == null || !_marker.activeSelf) return;
            RebuildRing(force: false);
        }

        private void OnDisable()
        {
            if (_marker != null) _marker.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_ringMesh != null) Destroy(_ringMesh);
            if (_marker != null) Destroy(_marker);
            _marker = null;
            _ringMaterial = null;
            _ringMesh = null;
        }
    }
}
