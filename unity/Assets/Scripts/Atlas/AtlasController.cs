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
        [SerializeField] private SystemView systemView = null!;
        [SerializeField] private AtlasUI ui = null!;
        [SerializeField] private Camera mainCamera = null!;

        private readonly AtlasNavigator _navigator = new();
        private GalaxyService? _service;
        private GalaxyService? _previewService;
        private GalaxyConfig? _pendingPreview;

        /// <summary>Automation surface: lets editor acceptance tooling drive the
        /// real navigation paths (map clicks are the only input it can't fake).</summary>
        public AtlasNavigator Navigator => _navigator;
        public GalaxyService? Service => _service;
        private AtlasLayer _layer = AtlasLayer.Polity;
        private ulong _seed;
        private HexCoordinate? _lastHoverPick;
        private bool _hoverValid;
        private BodyRef? _lastSystemPick;
        private bool _systemHoverValid;
        private SystemPanel? _systemPanel;
        private StarSystem? _currentSystem;

        private void Start()
        {
            ui.GenerateRequested += OnGenerate;
            ui.ConfigEdited += config => _pendingPreview = config;
            ui.LayerChanged += layer => { _layer = layer; galaxyView.SetLayer(layer); };
            ui.BreadcrumbClicked += OnBreadcrumb;
            ui.BackRequested += _navigator.Back;
            _navigator.Changed += Render;
            Render();
        }

        private void OnGenerate(GalaxyConfig config)
        {
            try
            {
                var service = new GalaxyService(config);
                service.Build();
                _service = service;
                _seed = config.MasterSeed;
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
                case 2:
                    // The Cell crumb: return to the cell view, hex selection cleared
                    // (orbit-diagram spec §3; closes the inert depth-2 crumb ticket).
                    if (_navigator.SelectedCell is { } cellCoord)
                        _navigator.DrillToCell(cellCoord);
                    break;
                default:
                    break;   // last crumb is "you are here" and stays inert
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

            if (_navigator.Screen == AtlasScreen.Setup && _pendingPreview is { } previewConfig)
            {
                _pendingPreview = null;
                RenderPreview(previewConfig);
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
                case AtlasScreen.System:
                    UpdateSystemScreen(mousePos);
                    break;
                default:
                    break;   // Setup: no picking surface
            }
        }

        private void ClearHover()
        {
            if (!_hoverValid && _lastHoverPick == null && !_systemHoverValid) return;
            _hoverValid = false;
            _lastHoverPick = null;
            _systemHoverValid = false;
            _lastSystemPick = null;
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

        private bool SystemHoverChanged(BodyRef? pick)
        {
            if (_systemHoverValid && System.Nullable.Equals(pick, _lastSystemPick)) return false;
            _systemHoverValid = true;
            _lastSystemPick = pick;
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
            {
                if (System.Nullable.Equals(_navigator.SelectedHex, clicked))
                {
                    // Second click on the selected hex drills into the system;
                    // a selected empty hex stays a no-op (orbit-diagram spec §3).
                    if (_service!.Generate(clicked).System != null)
                        _navigator.EnterSystem();
                }
                else
                {
                    _navigator.SelectHex(clicked);
                }
            }
        }

        private void UpdateSystemScreen(Vector2 mousePos)
        {
            var pick = systemView.Pick(mousePos, mainCamera);
            if (SystemHoverChanged(pick))
                ui.SetTooltip(pick is { } hovered ? SystemTooltip(hovered) : null);

            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Clicking empty space clears both the diagram selection and the
                // panel highlight (pick is null there).
                systemView.SetSelected(pick);
                _systemPanel?.Highlight(pick);
            }
        }

        private string? SystemTooltip(BodyRef pick)
        {
            if (_currentSystem == null) return null;
            var star = _currentSystem.Stars[pick.Star];
            if (pick.Slot < 0)
                return $"Star {(char)('A' + pick.Star)} — {star.TypeName}, "
                    + star.Age.ToString().ToLowerInvariant();
            var slot = star.Slots[pick.Slot];
            var body = slot.Body;
            if (body == null) return null;
            if (pick.Moon >= 0)
            {
                var moon = body.Satellites[pick.Moon];
                return $"moon {(char)('a' + pick.Moon)} — {SystemPanelBuilder.KindName(moon.Kind)}"
                    + (moon.Settlement != Settlement.None
                        ? $" · {moon.Settlement.ToString().ToLowerInvariant()}" : "");
            }
            return SystemPanelBuilder.KindName(body.Kind)
                + (body.Name != null ? $" \"{body.Name}\"" : "")
                + $" · slot {slot.Index} [{slot.Band.ToString().ToLowerInvariant()}]"
                + (body.Size > 0 ? $" · size {body.Size}" : "")
                + (body.Settlement != Settlement.None
                    ? $" · {body.Settlement.ToString().ToLowerInvariant()}" : "");
        }

        /// <summary>Shape-only rebuild behind the setup pane (setup-knobs spec §6).
        /// Preview services are throwaway; Generate always builds fresh from config.</summary>
        private void RenderPreview(GalaxyConfig config)
        {
            var service = new GalaxyService(config);
            service.BuildShapeOnly();
            _previewService = service;
            galaxyView.gameObject.SetActive(true);
            galaxyView.Show(service, AtlasLayer.Density);
            FitCamera(galaxyView.MapBounds);
        }

        private void Render()
        {
            // Screen or content changed: last frame's hover belongs to the old view.
            _hoverValid = false;
            _lastHoverPick = null;
            _systemHoverValid = false;
            _lastSystemPick = null;
            if (_navigator.Screen != AtlasScreen.System)
            {
                _systemPanel = null;
                _currentSystem = null;
            }

            switch (_navigator.Screen)
            {
                case AtlasScreen.Setup:
                    cellView.gameObject.SetActive(false);
                    systemView.gameObject.SetActive(false);
                    ui.ShowSetup();
                    if (_previewService != null)
                    {
                        galaxyView.gameObject.SetActive(true);
                        galaxyView.Show(_previewService, AtlasLayer.Density);
                        FitCamera(galaxyView.MapBounds);
                    }
                    else
                    {
                        galaxyView.gameObject.SetActive(false);
                        // First entry: seed the initial preview from current controls.
                        if (ui.TryReadConfig(out var initial)) _pendingPreview = initial;
                    }
                    break;

                case AtlasScreen.Galaxy:
                    galaxyView.gameObject.SetActive(true);
                    cellView.gameObject.SetActive(false);
                    systemView.gameObject.SetActive(false);
                    galaxyView.Show(_service!, _layer);
                    ui.ShowGalaxyHud($"Galaxy {_seed}");
                    ui.SetBreadcrumb(new[] { "Setup", $"Galaxy {_seed}" });
                    FitCamera(galaxyView.MapBounds);
                    break;

                case AtlasScreen.Cell:
                    RenderCellScreen();
                    break;

                case AtlasScreen.System:
                    RenderSystemScreen();
                    break;
            }
        }

        private void RenderCellScreen()
        {
            galaxyView.gameObject.SetActive(false);
            cellView.gameObject.SetActive(true);
            systemView.gameObject.SetActive(false);

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
                var panel = SystemPanelBuilder.Build(result, density,
                    onOpenSystem: result.System != null ? () => _navigator.EnterSystem() : null);
                ui.ShowSystemPanel(panel.Root);
            }
            else
            {
                ui.HideSystemPanel();
            }
            cellView.SetSelected(_navigator.SelectedHex);
        }

        private void RenderSystemScreen()
        {
            galaxyView.gameObject.SetActive(false);
            cellView.gameObject.SetActive(false);
            systemView.gameObject.SetActive(true);

            var hex = _navigator.SelectedHex!.Value;
            var result = _service!.Generate(hex);
            if (result.System == null)
            {
                // Only reachable through automation calling EnterSystem on an empty
                // hex; the controller's own entry paths check for a system first.
                _navigator.Back();
                return;
            }
            _currentSystem = result.System;

            systemView.Show(result.System);
            ui.ShowCellHud($"{result.System.GivenName ?? result.System.Designation}"
                + $" · {result.System.Designation}"
                + $" · {result.System.Arrangement.ToString().ToLowerInvariant()}");

            var cellCoord = _navigator.SelectedCell!.Value;
            ui.SetBreadcrumb(new[]
            {
                "Setup", $"Galaxy {_seed}", $"Cell ({cellCoord.Q},{cellCoord.R})",
                result.System.GivenName ?? result.System.Designation,
            });

            FitCamera(systemView.MapBounds);

            _systemPanel = SystemPanelBuilder.Build(result);
            ui.ShowSystemPanel(_systemPanel.Root);
        }

        private void FitCamera(Bounds b)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / mainCamera.aspect) * 1.08f;
            mainCamera.transform.position = new Vector3(b.center.x, b.center.y, -10f);
        }
    }
}
