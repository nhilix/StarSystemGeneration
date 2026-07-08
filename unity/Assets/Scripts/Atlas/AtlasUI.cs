using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>All UI Toolkit chrome for the atlas, built entirely in code (atlas
    /// spec §3/§6): setup form, galaxy/cell HUD, breadcrumb, tooltip, and the
    /// system-detail panel host. No UXML/USS assets. UI plumbing only — behavior
    /// is exercised by Task 9's live acceptance, not unit tests.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AtlasUI : MonoBehaviour
    {
        private static readonly Color PanelBg = new(0.06f, 0.06f, 0.09f, 0.92f);
        private static readonly Color TextColor = new(0.847f, 0.847f, 0.878f);   // #d8d8e0
        private static readonly Color DimTextColor = new(0.6f, 0.6f, 0.68f);
        private static readonly Color WarnColor = new(1.0f, 0.55f, 0.35f);
        private static readonly string[] LayerNames = { "Density", "Polity", "Zone", "Dev", "Lean" };

        public event Action<ulong, int>? GenerateRequested;
        public event Action<AtlasLayer>? LayerChanged;
        public event Action<int>? BreadcrumbClicked;
        public event Action? BackRequested;

        private VisualElement _root = null!;

        // Setup pane.
        private VisualElement _setupPane = null!;
        private TextField _seedField = null!;
        private IntegerField _radiusField = null!;
        private Label _radiusWarnLabel = null!;
        private Label _setupErrorLabel = null!;

        // HUD bar (galaxy + cell).
        private VisualElement _hudBar = null!;
        private Label _hudTitleLabel = null!;
        private VisualElement _breadcrumbRow = null!;
        private VisualElement _layerToggleRow = null!;
        private readonly Toggle[] _layerToggles = new Toggle[5];
        private bool _updatingToggles;

        // Cell side panel.
        private VisualElement _sidePanel = null!;
        private Label _sidePanelLabel = null!;

        // System detail panel.
        private VisualElement _systemPanelHost = null!;

        // Floating tooltip.
        private Label _tooltip = null!;

        private void Awake()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _root.style.backgroundColor = new StyleColor(Color.clear);

            BuildSetupPane();
            BuildHudBar();
            BuildSidePanel();
            BuildSystemPanelHost();
            BuildTooltip();

            _hudBar.style.display = DisplayStyle.None;
            _sidePanel.style.display = DisplayStyle.None;
            _systemPanelHost.style.display = DisplayStyle.None;
            _tooltip.style.display = DisplayStyle.None;
        }

        // ----- construction -----------------------------------------------------

        private void BuildSetupPane()
        {
            _setupPane = new VisualElement { name = "setup-pane" };
            _setupPane.style.position = Position.Absolute;
            _setupPane.style.top = 60;
            _setupPane.style.left = 40;
            _setupPane.style.width = 320;
            StylePanel(_setupPane);

            var title = new Label("Atlas Setup");
            title.style.fontSize = 16;
            title.style.color = TextColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            _setupPane.Add(title);

            _seedField = new TextField("Seed") { value = "42" };
            StyleFieldLabel(_seedField);
            _setupPane.Add(_seedField);

            _radiusField = new IntegerField("Galaxy radius (cells)") { value = 21 };
            StyleFieldLabel(_radiusField);
            _radiusField.RegisterValueChangedCallback(OnRadiusChanged);
            _setupPane.Add(_radiusField);

            _radiusWarnLabel = new Label("large: build + mesh get slow");
            _radiusWarnLabel.style.color = WarnColor;
            _radiusWarnLabel.style.fontSize = 12;
            _radiusWarnLabel.style.marginBottom = 4;
            _radiusWarnLabel.style.display = DisplayStyle.None;
            _setupPane.Add(_radiusWarnLabel);

            _setupErrorLabel = new Label();
            _setupErrorLabel.style.color = WarnColor;
            _setupErrorLabel.style.fontSize = 12;
            _setupErrorLabel.style.marginBottom = 4;
            _setupErrorLabel.style.display = DisplayStyle.None;
            _setupPane.Add(_setupErrorLabel);

            var generateButton = new Button(OnGenerateClicked) { text = "Generate" };
            _setupPane.Add(generateButton);

            _root.Add(_setupPane);
        }

        private void OnRadiusChanged(ChangeEvent<int> evt)
        {
            if (evt.newValue < 2)
            {
                _radiusField.SetValueWithoutNotify(2);
                _radiusWarnLabel.style.display = DisplayStyle.None;
                return;
            }
            _radiusWarnLabel.style.display = evt.newValue > 40 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnGenerateClicked()
        {
            if (!ulong.TryParse(_seedField.value, out var seed))
            {
                ShowSetup("seed must be a non-negative whole number");
                return;
            }
            int radius = _radiusField.value;
            if (radius < 2) radius = 2;
            GenerateRequested?.Invoke(seed, radius);
        }

        private void BuildHudBar()
        {
            _hudBar = new VisualElement { name = "hud-bar" };
            _hudBar.style.position = Position.Absolute;
            _hudBar.style.top = 0;
            _hudBar.style.left = 0;
            _hudBar.style.right = 0;
            StylePanel(_hudBar);
            _hudBar.style.borderBottomLeftRadius = 0;
            _hudBar.style.borderBottomRightRadius = 0;
            _hudBar.style.borderTopLeftRadius = 0;
            _hudBar.style.borderTopRightRadius = 0;

            var topRow = new VisualElement { name = "hud-top-row" };
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;

            var backButton = new Button(() => BackRequested?.Invoke()) { text = "< Back" };
            backButton.style.marginRight = 8;
            topRow.Add(backButton);

            _hudTitleLabel = new Label();
            _hudTitleLabel.style.color = TextColor;
            _hudTitleLabel.style.fontSize = 13;
            _hudTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hudTitleLabel.style.marginRight = 12;
            topRow.Add(_hudTitleLabel);

            _breadcrumbRow = new VisualElement { name = "breadcrumb-row" };
            _breadcrumbRow.style.flexDirection = FlexDirection.Row;
            _breadcrumbRow.style.alignItems = Align.Center;
            topRow.Add(_breadcrumbRow);

            _hudBar.Add(topRow);

            _layerToggleRow = new VisualElement { name = "layer-toggle-row" };
            _layerToggleRow.style.flexDirection = FlexDirection.Row;
            _layerToggleRow.style.marginTop = 4;
            for (int i = 0; i < LayerNames.Length; i++)
            {
                int index = i;
                var toggle = new Toggle(LayerNames[i]);
                toggle.style.marginRight = 10;
                toggle.style.color = TextColor;
                toggle.RegisterValueChangedCallback(evt => OnLayerToggleChanged(index, evt));
                _layerToggleRow.Add(toggle);
                _layerToggles[i] = toggle;
            }
            _layerToggles[1].SetValueWithoutNotify(true);   // Polity — matches AtlasController's default _layer
            _hudBar.Add(_layerToggleRow);

            _root.Add(_hudBar);
        }

        private void OnLayerToggleChanged(int index, ChangeEvent<bool> evt)
        {
            if (_updatingToggles) return;
            _updatingToggles = true;
            try
            {
                if (evt.newValue)
                {
                    for (int i = 0; i < _layerToggles.Length; i++)
                        if (i != index) _layerToggles[i].SetValueWithoutNotify(false);
                    LayerChanged?.Invoke((AtlasLayer)index);
                }
                else
                {
                    // Radio-style: exactly one toggle must always be set.
                    _layerToggles[index].SetValueWithoutNotify(true);
                }
            }
            finally
            {
                _updatingToggles = false;
            }
        }

        private void BuildSidePanel()
        {
            _sidePanel = new VisualElement { name = "side-panel" };
            _sidePanel.style.position = Position.Absolute;
            _sidePanel.style.top = 60;
            _sidePanel.style.left = 0;
            _sidePanel.style.width = 260;
            _sidePanel.style.bottom = 0;
            StylePanel(_sidePanel);

            _sidePanelLabel = new Label();
            _sidePanelLabel.style.color = TextColor;
            _sidePanelLabel.style.fontSize = 12;
            _sidePanelLabel.style.whiteSpace = WhiteSpace.Normal;
            _sidePanel.Add(_sidePanelLabel);

            _root.Add(_sidePanel);
        }

        private void BuildSystemPanelHost()
        {
            _systemPanelHost = new VisualElement { name = "system-panel-host" };
            _systemPanelHost.style.position = Position.Absolute;
            _systemPanelHost.style.top = 60;
            _systemPanelHost.style.right = 0;
            _systemPanelHost.style.width = 360;
            _systemPanelHost.style.bottom = 0;
            StylePanel(_systemPanelHost);

            _root.Add(_systemPanelHost);
        }

        private void BuildTooltip()
        {
            _tooltip = new Label { name = "tooltip" };
            _tooltip.style.position = Position.Absolute;
            _tooltip.style.top = 4;
            _tooltip.style.left = 0;
            _tooltip.style.right = 0;
            _tooltip.style.unityTextAlign = TextAnchor.UpperCenter;
            _tooltip.style.color = TextColor;
            _tooltip.style.fontSize = 12;
            _tooltip.style.backgroundColor = PanelBg;
            _tooltip.style.paddingTop = 2;
            _tooltip.style.paddingBottom = 2;
            _tooltip.style.width = 260;
            _tooltip.style.marginLeft = new StyleLength(new Length(50, LengthUnit.Percent));
            _tooltip.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _tooltip.style.borderTopLeftRadius = 4;
            _tooltip.style.borderTopRightRadius = 4;
            _tooltip.style.borderBottomLeftRadius = 4;
            _tooltip.style.borderBottomRightRadius = 4;
            // A floating readout must never occlude map picking, or it would
            // flicker: appearing under the cursor would count as "over chrome".
            _tooltip.pickingMode = PickingMode.Ignore;

            _root.Add(_tooltip);
        }

        private static void StylePanel(VisualElement element)
        {
            element.style.backgroundColor = PanelBg;
            element.style.paddingTop = 8;
            element.style.paddingBottom = 8;
            element.style.paddingLeft = 8;
            element.style.paddingRight = 8;
            element.style.borderTopLeftRadius = 4;
            element.style.borderTopRightRadius = 4;
            element.style.borderBottomLeftRadius = 4;
            element.style.borderBottomRightRadius = 4;
        }

        private static void StyleFieldLabel(VisualElement field)
        {
            var label = field.Q<Label>();
            if (label != null)
            {
                label.style.color = DimTextColor;
                label.style.fontSize = 12;
            }
        }

        // ----- public API ---------------------------------------------------------

        /// <summary>True when the screen-space position (bottom-left origin, as
        /// reported by the Input System) is over any visible chrome pane. The map
        /// panes are absolute-positioned children of the full-screen root, so
        /// picking anything other than the root means chrome is under the cursor.</summary>
        public bool IsPointerOverChrome(Vector2 screenPos)
        {
            var panel = _root.panel;
            if (panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = panel.Pick(panelPos);
            return picked != null && picked != _root;
        }

        public void ShowSetup(string? error = null)
        {
            _setupPane.style.display = DisplayStyle.Flex;
            _hudBar.style.display = DisplayStyle.None;
            _sidePanel.style.display = DisplayStyle.None;
            _systemPanelHost.style.display = DisplayStyle.None;
            _tooltip.style.display = DisplayStyle.None;

            if (string.IsNullOrEmpty(error))
            {
                _setupErrorLabel.style.display = DisplayStyle.None;
                _setupErrorLabel.text = "";
            }
            else
            {
                _setupErrorLabel.text = error;
                _setupErrorLabel.style.display = DisplayStyle.Flex;
            }
        }

        public void ShowGalaxyHud(string galaxyLabel)
        {
            _setupPane.style.display = DisplayStyle.None;
            _sidePanel.style.display = DisplayStyle.None;
            _systemPanelHost.style.display = DisplayStyle.None;

            _hudBar.style.display = DisplayStyle.Flex;
            _hudTitleLabel.text = galaxyLabel;
            _layerToggleRow.style.display = DisplayStyle.Flex;
        }

        public void ShowCellHud(string cellSummary)
        {
            _setupPane.style.display = DisplayStyle.None;
            _systemPanelHost.style.display = DisplayStyle.None;

            _hudBar.style.display = DisplayStyle.Flex;
            _layerToggleRow.style.display = DisplayStyle.None;

            _sidePanel.style.display = DisplayStyle.Flex;
            _sidePanelLabel.text = cellSummary;
        }

        public void ShowSystemPanel(VisualElement panel)
        {
            _systemPanelHost.Clear();
            _systemPanelHost.Add(panel);
            _systemPanelHost.style.display = DisplayStyle.Flex;
        }

        public void HideSystemPanel()
        {
            _systemPanelHost.style.display = DisplayStyle.None;
            _systemPanelHost.Clear();
        }

        public void SetBreadcrumb(IReadOnlyList<string> trail)
        {
            _breadcrumbRow.Clear();
            for (int i = 0; i < trail.Count; i++)
            {
                int index = i;
                bool isLast = i == trail.Count - 1;
                var crumb = new Button(() => BreadcrumbClicked?.Invoke(index)) { text = trail[i] };
                crumb.SetEnabled(!isLast);
                crumb.style.color = isLast ? TextColor : DimTextColor;
                _breadcrumbRow.Add(crumb);

                if (!isLast)
                {
                    var sep = new Label(">");
                    sep.style.color = DimTextColor;
                    sep.style.marginLeft = 4;
                    sep.style.marginRight = 4;
                    _breadcrumbRow.Add(sep);
                }
            }
        }

        public void SetTooltip(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _tooltip.style.display = DisplayStyle.None;
                return;
            }
            _tooltip.text = text;
            _tooltip.style.display = DisplayStyle.Flex;
        }
    }
}
