using System;
using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Substrate;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>Chrome-owns-the-pointer hook: the rail installs a screen-
    /// position test; the camera rig consults it before spending pointer
    /// input (the K1 carried note, closed).</summary>
    public static class AtlasPointerGuard
    {
        public static Func<Vector2, bool> Test;
        public static bool Blocks(Vector2 screenPos) =>
            Test != null && Test(screenPos);
    }

    /// <summary>The left-rail lens stack (K2, replacing the provisional
    /// IMGUI HUD): UI Toolkit built entirely in code (the PoC lesson),
    /// grouped POLITICAL / LOGISTICS / KNOWLEDGE / NARRATIVE / NATURE.
    /// Toggle chips carry their lens swatch; the price chip carries its
    /// good; war/tension/tech and lanes/traffic are radio-like (one fill,
    /// one stroke mode — they cannot stack by nature). A minimal year
    /// readout stays here until K3's top bar.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LensRail : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;

        // The lens selection — what the user has toggled on.
        private bool _domains = true, _war, _tension, _tech;
        private bool _lanes = true, _traffic, _fleets, _works, _price;
        private bool _plague, _news, _pois;
        private GoodId _priceGood = GoodId.Provisions;
        private NatureLayer? _nature;

        private readonly Dictionary<string, Button> _chips = new();
        private Label _yearLabel;
        private VisualElement _railRoot;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private void OnEnable()
        {
            BuildUi();
            if (root != null && root.SimHost != null)
                root.SimHost.Loaded += OnLoaded;
            AtlasPointerGuard.Test = BlocksPointer;
        }

        private void OnDisable()
        {
            if (root != null && root.SimHost != null)
                root.SimHost.Loaded -= OnLoaded;
            if (AtlasPointerGuard.Test == (Func<Vector2, bool>)BlocksPointer)
                AtlasPointerGuard.Test = null;
        }

        private void OnLoaded()
        {
            RefreshYear();
            Apply();
        }

        /// <summary>True when the screen position (input-system coords,
        /// origin bottom-left) lands on rail chrome.</summary>
        private bool BlocksPointer(Vector2 screenPos)
        {
            if (_railRoot?.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(
                _railRoot.panel, new Vector2(screenPos.x,
                    Screen.height - screenPos.y));
            var picked = _railRoot.panel.Pick(panelPos);
            return picked != null;
        }

        // ---- UI construction (code-built; no UXML/USS assets) ----

        private static readonly Color RailBg = new(0.05f, 0.06f, 0.09f, 0.92f);
        private static readonly Color GroupInk = new(0.55f, 0.60f, 0.72f);
        private static readonly Color ChipInk = new(0.86f, 0.89f, 0.95f);
        private static readonly Color ChipBgOff = new(0.10f, 0.11f, 0.15f, 0.85f);
        private static readonly Color ChipBgOn = new(0.20f, 0.24f, 0.33f, 0.95f);

        private void BuildUi()
        {
            var doc = GetComponent<UIDocument>();
            var uiRoot = doc.rootVisualElement;
            uiRoot.Clear();
            // The document root stretches over the whole screen and picks
            // by default — left as-is, panel.Pick would report chrome
            // EVERYWHERE and the pointer guard would kill map input.
            uiRoot.pickingMode = PickingMode.Ignore;

            _railRoot = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = 196,
                    backgroundColor = RailBg,
                    paddingLeft = 10, paddingRight = 10,
                    paddingTop = 8, paddingBottom = 8,
                },
            };
            uiRoot.Add(_railRoot);

            _yearLabel = new Label("no artifact")
            {
                style =
                {
                    color = ChipInk, fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 6, whiteSpace = WhiteSpace.Normal,
                },
            };
            _railRoot.Add(_yearLabel);

            Group("POLITICAL");
            Chip("domains", new Color32(0x46, 0xB5, 0xA4, 255),
                 () => _domains, v => _domains = v);
            Chip("war", new Color32(0xE0, 0x55, 0x55, 255),
                 () => _war, v => { _war = v; if (v) _tension = _tech = false; });
            Chip("tension", new Color32(0xE0, 0x8A, 0x4A, 255),
                 () => _tension, v => { _tension = v; if (v) _war = _tech = false; });

            Group("LOGISTICS");
            Chip("lanes", new Color32(0x56, 0xC4, 0xDC, 255),
                 () => _lanes, v => { _lanes = v; if (v) _traffic = false; });
            Chip("traffic", new Color32(0x2E, 0x7E, 0x96, 255),
                 () => _traffic, v => { _traffic = v; if (v) _lanes = false; });
            Chip("fleets", new Color32(0xC7, 0xD3, 0xEA, 255),
                 () => _fleets, v => _fleets = v);
            Chip("works", new Color32(0xF0, 0xC3, 0x5F, 255),
                 () => _works, v => _works = v);
            Chip(PriceLabel(), new Color32(0x8F, 0xBF, 0x6A, 255),
                 () => _price, v => _price = v, chipKey: "price");
            var goods = new List<string>();
            foreach (var def in Goods.All) goods.Add(def.Name);
            var goodField = new DropdownField(goods, (int)_priceGood)
            {
                style = { marginLeft = 18, marginBottom = 4, height = 18, fontSize = 10 },
            };
            goodField.RegisterValueChangedCallback(_ =>
            {
                _priceGood = (GoodId)goodField.index;
                RefreshChip("price", PriceLabel());
                Apply();
            });
            _railRoot.Add(goodField);

            Group("KNOWLEDGE");
            Chip("tech", new Color32(0x7F, 0xA6, 0xE8, 255),
                 () => _tech, v => { _tech = v; if (v) _war = _tension = false; });
            Chip("plague", new Color32(0xB9, 0xE8, 0x6F, 255),
                 () => _plague, v => _plague = v);
            Chip("news", new Color32(0xE8, 0xD6, 0x6F, 255),
                 () => _news, v => _news = v);

            Group("NARRATIVE");
            Chip("POIs", new Color32(0xD8, 0xB4, 0x6F, 255),
                 () => _pois, v => _pois = v);

            Group("NATURE");
            foreach (NatureLayer layer in Enum.GetValues(typeof(NatureLayer)))
            {
                var captured = layer;
                Chip(layer.ToString().ToLowerInvariant(),
                     new Color32(0x5A, 0x6E, 0x9E, 255),
                     () => _nature == captured,
                     v => _nature = v ? captured : null,
                     chipKey: "nature:" + layer);
            }

            Apply();
            RefreshYear();
        }

        private string PriceLabel() =>
            $"price ▾ {Goods.Get(_priceGood).Name.ToLowerInvariant()}";

        private void Group(string title) => _railRoot.Add(new Label(title)
        {
            style =
            {
                color = GroupInk, fontSize = 9,
                letterSpacing = 2, marginTop = 8, marginBottom = 3,
                unityFontStyleAndWeight = FontStyle.Bold,
            },
        });

        private void Chip(string label, Color32 swatch, Func<bool> get,
                          Action<bool> set, string chipKey = null)
        {
            chipKey ??= label;
            var button = new Button
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexStart,
                    backgroundColor = get() ? ChipBgOn : ChipBgOff,
                    color = ChipInk, fontSize = 11, height = 22,
                    marginBottom = 3, marginTop = 0,
                    marginLeft = 0, marginRight = 0,
                    paddingLeft = 6,
                    borderTopWidth = 0, borderBottomWidth = 0,
                    borderLeftWidth = 0, borderRightWidth = 0,
                },
            };
            button.Add(new VisualElement
            {
                style =
                {
                    width = 10, height = 10, marginRight = 6,
                    backgroundColor = (Color)swatch,
                },
            });
            button.Add(new Label(label)
            {
                name = "chip-label",
                style = { color = ChipInk, fontSize = 11 },
            });
            button.clicked += () =>
            {
                set(!get());
                Apply();
                RefreshAllChips();
            };
            _chips[chipKey] = button;
            _railRoot.Add(button);
            _chipStates[chipKey] = get;
        }

        private readonly Dictionary<string, Func<bool>> _chipStates = new();

        private void RefreshAllChips()
        {
            foreach (var (key, chip) in _chips)
                chip.style.backgroundColor =
                    _chipStates[key]() ? ChipBgOn : ChipBgOff;
        }

        private void RefreshChip(string key, string label)
        {
            if (_chips.TryGetValue(key, out var chip))
                chip.Q<Label>("chip-label").text = label;
        }

        private void RefreshYear()
        {
            if (root == null || root.SimHost?.State == null) return;
            var state = root.SimHost.State;
            _yearLabel.text = $"y{state.WorldYear} · epoch {state.EpochIndex}"
                + $" · seed {state.Config.MasterSeed}";
        }

        // ---- Lens selection → layer routing ----

        /// <summary>One fill, one stroke: the accent lenses ride the
        /// domain field, traffic/quarantine ride the lane strokes.</summary>
        private DomainAccent Accent =>
            _war ? DomainAccent.War
            : _tension ? DomainAccent.Tension
            : _tech ? DomainAccent.Tech
            : DomainAccent.Owner;

        private void Apply()
        {
            if (root == null || root.SimHost?.Model == null) return;

            bool domainsVisible = _domains || _war || _tension || _tech;
            root.DomainField.SetVisible(domainsVisible);
            root.DomainField.SetAccent(Accent);
            root.WarLayer.SetVisible(_war);

            bool lanesVisible = _lanes || _traffic || _plague;
            root.LaneLayer.SetVisible(lanesVisible);
            root.LaneLayer.SetMode(
                _traffic ? LaneMode.Traffic
                : _lanes ? LaneMode.Status
                : LaneMode.QuarantineOnly);

            root.FleetLayer.SetVisible(_fleets);
            root.WorksLayer.SetVisible(_works);
            root.PriceField.SetGood(_priceGood);
            root.PriceField.SetVisible(_price);
            root.PlagueLayer.SetVisible(_plague);
            root.NewsLayer.SetVisible(_news);
            root.PoiLayer.SetVisible(_pois);
            root.NatureField.Select(_nature);
        }
    }
}
