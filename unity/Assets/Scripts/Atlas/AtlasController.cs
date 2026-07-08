using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using UnityEngine;

namespace StarGen.Atlas
{
    /// <summary>Wires navigator + service + views + UI (atlas spec §3). All state
    /// decisions live in AtlasNavigator; this class only renders and routes input.</summary>
    public sealed class AtlasController : MonoBehaviour
    {
        [SerializeField] private GalaxyView galaxyView = null!;
        [SerializeField] private CellView cellView = null!;
        [SerializeField] private AtlasUI ui = null!;
        [SerializeField] private Camera mainCamera = null!;

        private readonly AtlasNavigator _navigator = new();
        private GalaxyService? _service;

        /// <summary>Automation surface: lets editor acceptance tooling drive the
        /// real navigation paths (map clicks are the only input it can't fake).</summary>
        public AtlasNavigator Navigator => _navigator;
        public GalaxyService? Service => _service;
        private AtlasLayer _layer = AtlasLayer.Polity;
        private ulong _seed;
        private HexCoordinate? _lastHoverPick;
        private bool _hoverValid;

        private void Start()
        {
            ui.GenerateRequested += OnGenerate;
            ui.LayerChanged += layer => { _layer = layer; galaxyView.SetLayer(layer); };
            ui.BreadcrumbClicked += OnBreadcrumb;
            ui.BackRequested += _navigator.Back;
            _navigator.Changed += Render;
            Render();
        }

        private void OnGenerate(ulong seed, int radius)
        {
            try
            {
                var service = new GalaxyService(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });
                service.Build();
                _service = service;
                _seed = seed;
                _navigator.EnterGalaxy();
            }
            catch (System.Exception ex)
            {
                ui.ShowSetup($"build failed: {ex.Message}");
            }
        }

        private void OnBreadcrumb(int depth)
        {
            switch (depth)
            {
                case 0:
                    _navigator.Reset();
                    break;
                case 1:
                    _navigator.EnterGalaxy();
                    break;
                default:
                    break;   // depth 2 (current Cell crumb) and beyond: no-op
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _navigator.Back();
                return;
            }
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            if (ui.IsPointerOverChrome(mousePos))
            {
                // Chrome owns the pointer: no hover, no tooltip, and clicks must
                // not fall through to the map underneath the panels.
                ClearHover();
                return;
            }
            switch (_navigator.Screen)
            {
                case AtlasScreen.Galaxy:
                    UpdateGalaxyScreen(mousePos);
                    break;
                case AtlasScreen.Cell:
                    UpdateCellScreen(mousePos);
                    break;
                default:
                    break;   // Setup: no picking surface
            }
        }

        private void ClearHover()
        {
            if (!_hoverValid && _lastHoverPick == null) return;
            _hoverValid = false;
            _lastHoverPick = null;
            if (_navigator.Screen == AtlasScreen.Galaxy) galaxyView.SetHover(null);
            ui.SetTooltip(null);
        }

        private bool HoverChanged(HexCoordinate? pick)
        {
            if (_hoverValid && System.Nullable.Equals(pick, _lastHoverPick)) return false;
            _hoverValid = true;
            _lastHoverPick = pick;
            return true;
        }

        private void UpdateGalaxyScreen(Vector2 mousePos)
        {
            var pick = galaxyView.Pick(mousePos, mainCamera);
            if (HoverChanged(pick))
            {
                galaxyView.SetHover(pick);

                if (pick is { } cellCoord && _service!.TryGetCell(cellCoord, out var cell))
                {
                    string owner = cell.OwnerPolityId >= 0
                        ? _service.Skeleton.Polities[cell.OwnerPolityId].Name
                        : "unclaimed";
                    ui.SetTooltip($"cell ({cell.Q},{cell.R}) · {owner} · dev {cell.DevelopmentTier}");
                }
                else
                {
                    ui.SetTooltip(null);
                }
            }

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && pick is { } clicked)
                _navigator.DrillToCell(clicked);
        }

        private void UpdateCellScreen(Vector2 mousePos)
        {
            var pick = cellView.Pick(mousePos, mainCamera);

            if (HoverChanged(pick))
            {
                if (pick is { } hex)
                {
                    var state = _service!.StateOf(hex);
                    ui.SetTooltip($"{Designation.For(hex)} · {state.ToString().ToLowerInvariant()}");
                }
                else
                {
                    ui.SetTooltip(null);
                }
            }

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && pick is { } clicked)
                _navigator.SelectHex(clicked);
        }

        private void Render()
        {
            // Screen or content changed: last frame's hover belongs to the old view.
            _hoverValid = false;
            _lastHoverPick = null;

            switch (_navigator.Screen)
            {
                case AtlasScreen.Setup:
                    galaxyView.gameObject.SetActive(false);
                    cellView.gameObject.SetActive(false);
                    ui.ShowSetup();
                    break;

                case AtlasScreen.Galaxy:
                    galaxyView.gameObject.SetActive(true);
                    cellView.gameObject.SetActive(false);
                    galaxyView.Show(_service!, _layer);
                    ui.ShowGalaxyHud($"Galaxy {_seed}");
                    ui.SetBreadcrumb(new[] { "Setup", $"Galaxy {_seed}" });
                    FitCamera(galaxyView.MapBounds);
                    break;

                case AtlasScreen.Cell:
                    RenderCellScreen();
                    break;
            }
        }

        private void RenderCellScreen()
        {
            galaxyView.gameObject.SetActive(false);
            cellView.gameObject.SetActive(true);

            var cellCoord = _navigator.SelectedCell!.Value;
            if (!_service!.TryGetCell(cellCoord, out var cell))
            {
                // Only reachable through automation driving the navigator to a
                // coordinate outside the galaxy; the map never picks one.
                _navigator.Back();
                return;
            }
            cellView.Show(_service, cellCoord);
            ui.ShowCellHud(_service.CellSummary(cell));

            var crumbs = new List<string> { "Setup", $"Galaxy {_seed}", $"Cell ({cellCoord.Q},{cellCoord.R})" };
            if (_navigator.SelectedHex is { } selectedHex)
                crumbs.Add(Designation.For(selectedHex));
            ui.SetBreadcrumb(crumbs);

            FitCamera(cellView.MapBounds);

            if (_navigator.SelectedHex is { } hex)
            {
                var result = _service.Generate(hex);
                double density = result.IsEmpty ? DensityField.At(_service.Context.Config, hex) : double.NaN;
                ui.ShowSystemPanel(SystemPanelBuilder.Build(result, density));
            }
            else
            {
                ui.HideSystemPanel();
            }
            cellView.SetSelected(_navigator.SelectedHex);
        }

        private void FitCamera(Bounds b)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / mainCamera.aspect) * 1.08f;
            mainCamera.transform.position = new Vector3(b.center.x, b.center.y, -10f);
        }
    }
}
