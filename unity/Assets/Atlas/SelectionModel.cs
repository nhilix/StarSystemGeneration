using System;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarGen.AtlasView
{
    public enum SelectionKind
    { None, Hex, Port, Project, Shipment, Fleet, Poi }

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

        private HexCoordinate? _hovered;
        private HexInfo _hoverInfo;
        private Vector2 _pressPos;
        private bool _pressLive;
        private GameObject _marker;

        public HexCoordinate? HoveredHex => _hovered;
        public HexInfo HoverInfo => _hoverInfo;

        public event Action HoverChanged;
        public event Action<Selection> Selected;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private void Update()
        {
            if (root == null || root.SimHost?.Model == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var screenPos = mouse.position.ReadValue();

            if (AtlasPointerGuard.Blocks(screenPos))
            {
                SetHovered(null);
                _pressLive = false;
                return;
            }

            var hex = HexUnder(screenPos);
            SetHovered(hex);

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
                if ((screenPos - _pressPos).sqrMagnitude <= 25f && hex != null)
                    Select(hex.Value);
            }
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
            HoverChanged?.Invoke();
        }

        /// <summary>Resolve what the click means, most specific first.</summary>
        private void Select(HexCoordinate hex)
        {
            var model = root.SimHost.Model;
            var state = root.SimHost.State;
            var eye = EyeContext.God(state.WorldYear);

            var selection = new Selection(SelectionKind.Hex, -1, hex);
            var info = _hoverInfo ?? HexQuery.At(model, eye, hex);
            if (info.PortId >= 0)
                selection = new Selection(SelectionKind.Port, info.PortId, hex);
            else if (FindProject(state, hex) is int projectId)
                selection = new Selection(SelectionKind.Project, projectId, hex);
            else if (FindFreight(model, eye, hex) is int shipmentId)
                selection = new Selection(SelectionKind.Shipment, shipmentId, hex);
            else if (FindFleet(state, hex) is int fleetId)
                selection = new Selection(SelectionKind.Fleet, fleetId, hex);
            else if (info.LivePois.Count > 0)
                selection = new Selection(SelectionKind.Poi,
                                          info.LivePois[0].Id, hex);
            MarkHex(hex);
            Selected?.Invoke(selection);
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

        // ---- the selected-hex ring ----

        private void MarkHex(HexCoordinate hex)
        {
            if (_marker == null)
            {
                _marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _marker.name = "SelectionRing";
                UnityEngine.Object.Destroy(
                    _marker.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Unlit/Transparent"))
                { mainTexture = AtlasTextures.Ring, renderQueue = 3200 };
                mat.hideFlags = HideFlags.HideAndDontSave;
                _marker.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            _marker.transform.position = AtlasGeometry.HexToWorld(hex, -0.5f);
            _marker.SetActive(true);
        }

        private void LateUpdate()
        {
            if (_marker == null || !_marker.activeSelf
                || root?.CameraRig == null) return;
            // screen-constant-ish: scale with camera distance
            float s = Mathf.Max(1.2f, root.CameraRig.Distance * 0.02f);
            _marker.transform.localScale = new Vector3(s, s, 1f);
        }

        private void OnDisable()
        {
            if (_marker != null) _marker.SetActive(false);
        }
    }
}
