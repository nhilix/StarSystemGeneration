using System;
using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Substrate;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>Chrome-owns-the-pointer hook: AtlasChrome installs a
    /// screen-position test; the camera rig consults it before spending
    /// pointer input (the K1 carried note, closed in K2; K3 moved the
    /// single installer to AtlasChrome).</summary>
    public static class AtlasPointerGuard
    {
        public static Func<Vector2, bool> Test;
        public static bool Blocks(Vector2 screenPos) =>
            Test != null && Test(screenPos);
    }

    /// <summary>The left-rail lens stack (K2; re-skinned at K3 into the
    /// cassette × ice language — structure classes from AtlasChrome.uss,
    /// palette from the PanelSettings theme). Grouped POLITICAL /
    /// LOGISTICS / KNOWLEDGE / NARRATIVE / NATURE; war/tension/tech and
    /// lanes/traffic are radio-like (one fill, one stroke mode). The K2
    /// year readout retired into the K3 top bar.</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class LensRail : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;

        // The lens selection — what the user has toggled on.
        private bool _domains = true, _war, _tension, _tech, _currency;
        private bool _lanes = true, _traffic, _trade, _fleets, _works, _price;
        private bool _plague, _news, _pois;
        private GoodId _priceGood = GoodId.Provisions;
        private NatureLayer? _nature;

        private readonly Dictionary<string, Button> _chips = new();
        private readonly Dictionary<string, Func<bool>> _chipStates = new();

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        /// <summary>The active lens's rail key — what the legend shows
        /// (accent and stroke lenses win over the always-on base).</summary>
        public string ActiveLegendKey =>
            _war ? "war"
            : _tension ? "tension"
            : _tech ? "tech"
            : _currency ? "currency"
            : _plague ? "plague"
            : _price ? "price"
            : _trade ? "trade"
            : _traffic ? "traffic"
            : _works ? "works"
            : _fleets ? "fleets"
            : _news ? "news"
            : _pois ? "pois"
            : _nature != null ? "nature"
            : _lanes ? "lanes"
            : "domains";

        public GoodId PriceGood => _priceGood;

        /// <summary>Raised after any lens toggle applies — the legend
        /// refreshes from ActiveLegendKey.</summary>
        public event Action LensChanged;

        private void OnEnable()
        {
            BuildUi();
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded += OnLoaded;
                // layers rebuild on a time change too — the lens choice
                // must re-apply over the fresh content
                root.SimHost.TimeChanged += OnLoaded;
            }
        }

        private void OnDisable()
        {
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded -= OnLoaded;
                root.SimHost.TimeChanged -= OnLoaded;
            }
        }

        private void OnLoaded() => Apply();

        // ---- UI construction (code-built onto AtlasChrome.Rail) ----

        private void BuildUi()
        {
            var rail = GetComponent<AtlasChrome>().Rail;
            if (rail == null) return;
            rail.Clear();
            _chips.Clear();
            _chipStates.Clear();

            Group(rail, "POLITICAL");
            Chip(rail, "domains", new Color32(0x46, 0xB5, 0xA4, 255),
                 () => _domains, v => _domains = v);
            Chip(rail, "war", new Color32(0xE0, 0x55, 0x55, 255),
                 () => _war,
                 v => { _war = v; if (v) _tension = _tech = _currency = false; });
            Chip(rail, "tension", new Color32(0xE0, 0x8A, 0x4A, 255),
                 () => _tension,
                 v => { _tension = v; if (v) _war = _tech = _currency = false; });
            // AC3.1: the fifth domain accent — CU-3 consolidation made
            // visible; radio-exclusive with the other three the same way
            // (one fill mode at a time).
            Chip(rail, "currency", new Color32(0xB0, 0x8A, 0xE0, 255),
                 () => _currency,
                 v => { _currency = v; if (v) _war = _tension = _tech = false; });

            Group(rail, "LOGISTICS");
            Chip(rail, "lanes", new Color32(0x56, 0xC4, 0xDC, 255),
                 () => _lanes,
                 v => { _lanes = v; if (v) _traffic = _trade = false; });
            Chip(rail, "traffic", new Color32(0x2E, 0x7E, 0x96, 255),
                 () => _traffic,
                 v => { _traffic = v; if (v) _lanes = _trade = false; });
            Chip(rail, "trade", new Color32(TradeLens.MarginGold.R,
                     TradeLens.MarginGold.G, TradeLens.MarginGold.B, 255),
                 () => _trade,
                 v => { _trade = v; if (v) _lanes = _traffic = false; });
            Chip(rail, "fleets", new Color32(0xC7, 0xD3, 0xEA, 255),
                 () => _fleets, v => _fleets = v);
            Chip(rail, "works", new Color32(0xF0, 0xC3, 0x5F, 255),
                 () => _works, v => _works = v);
            Chip(rail, PriceLabel(), new Color32(0x8F, 0xBF, 0x6A, 255),
                 () => _price, v => _price = v, chipKey: "price");
            var goods = new List<string>();
            foreach (var def in Goods.All) goods.Add(def.Name);
            var goodField = new DropdownField(goods, (int)_priceGood);
            goodField.AddToClassList("ssg-rail__good");
            goodField.RegisterValueChangedCallback(_ =>
            {
                _priceGood = (GoodId)goodField.index;
                RefreshChip("price", PriceLabel());
                Apply();
            });
            rail.Add(goodField);

            Group(rail, "KNOWLEDGE");
            Chip(rail, "tech", new Color32(0x7F, 0xA6, 0xE8, 255),
                 () => _tech,
                 v => { _tech = v; if (v) _war = _tension = _currency = false; });
            Chip(rail, "plague", new Color32(0xB9, 0xE8, 0x6F, 255),
                 () => _plague, v => _plague = v);
            Chip(rail, "news", new Color32(0xE8, 0xD6, 0x6F, 255),
                 () => _news, v => _news = v);

            Group(rail, "NARRATIVE");
            Chip(rail, "POIs", new Color32(0xD8, 0xB4, 0x6F, 255),
                 () => _pois, v => _pois = v);

            Group(rail, "NATURE");
            foreach (NatureLayer layer in Enum.GetValues(typeof(NatureLayer)))
            {
                var captured = layer;
                Chip(rail, layer.ToString().ToLowerInvariant(),
                     new Color32(0x5A, 0x6E, 0x9E, 255),
                     () => _nature == captured,
                     v => _nature = v ? captured : null,
                     chipKey: "nature:" + layer);
            }

            Apply();
        }

        private string PriceLabel() =>
            $"price ▾ {Goods.Get(_priceGood).Name.ToLowerInvariant()}";

        private static void Group(VisualElement rail, string title)
        {
            var label = new Label(title);
            label.AddToClassList("ssg-rail__group");
            rail.Add(label);
        }

        private void Chip(VisualElement rail, string label, Color32 swatch,
                          Func<bool> get, Action<bool> set,
                          string chipKey = null)
        {
            chipKey ??= label;
            var button = new Button { text = string.Empty };
            button.AddToClassList("ssg-chip");
            var swatchBox = new VisualElement
            { style = { backgroundColor = (Color)swatch } };
            swatchBox.AddToClassList("ssg-chip__swatch");
            button.Add(swatchBox);
            var text = new Label(label) { name = "chip-label" };
            text.AddToClassList("ssg-chip__label");
            button.Add(text);
            button.clicked += () =>
            {
                set(!get());
                Apply();
                RefreshAllChips();
            };
            _chips[chipKey] = button;
            _chipStates[chipKey] = get;
            button.EnableInClassList("ssg-chip--on", get());
            rail.Add(button);
        }

        private void RefreshAllChips()
        {
            foreach (var (key, chip) in _chips)
                chip.EnableInClassList("ssg-chip--on", _chipStates[key]());
        }

        private void RefreshChip(string key, string label)
        {
            if (_chips.TryGetValue(key, out var chip))
                chip.Q<Label>("chip-label").text = label;
        }

        // ---- Lens selection → layer routing ----

        /// <summary>One fill, one stroke: the accent lenses ride the
        /// domain field, traffic/quarantine ride the lane strokes.</summary>
        private DomainAccent Accent =>
            _war ? DomainAccent.War
            : _tension ? DomainAccent.Tension
            : _tech ? DomainAccent.Tech
            : _currency ? DomainAccent.Currency
            : DomainAccent.Owner;

        private void Apply()
        {
            if (root == null || root.SimHost?.Model == null) return;

            bool domainsVisible = _domains || _war || _tension || _tech || _currency;
            root.DomainField.SetVisible(domainsVisible);
            root.DomainField.SetAccent(Accent);
            // AC1.3: the worked skeleton is interior structure ON the domains
            // lens — it rides the existing chip, no new rail key. (Outpost
            // marks are always-on, the subordinate sibling of the port dots.)
            // AC-fixwave: null-guarded like FlowTrailLayer/CrawlPathLayer
            // below — a stale/hand-authored scene predating this Phase-1
            // layer degrades gracefully instead of NREing.
            if (root.DomainInterior != null)
                root.DomainInterior.SetVisible(domainsVisible);
            root.WarLayer.SetVisible(_war);

            // AC2.7: the war lens's own stroke mode — contested lanes only,
            // rides the same chip as the domain accent/station glyphs, no
            // new rail key
            bool lanesVisible = _lanes || _traffic || _trade || _plague || _war;
            root.LaneLayer.SetVisible(lanesVisible);
            root.LaneLayer.SetMode(
                _war ? LaneMode.War
                : _traffic ? LaneMode.Traffic
                : _trade ? LaneMode.Trade
                : _lanes ? LaneMode.Status
                : LaneMode.QuarantineOnly);

            root.FleetLayer.SetVisible(_fleets);
            root.WorksLayer.SetVisible(_works);
            // AC2.F2: recent-flow trails ride the works chip — a sibling
            // layer, not a new rail key (null-guarded for an older scene)
            if (root.FlowTrailLayer != null)
                root.FlowTrailLayer.SetVisible(_works);
            // AC4.1: live off-lane crawl paths ride the works chip too —
            // same sibling-layer, null-guarded pattern as the trails
            if (root.CrawlPathLayer != null)
                root.CrawlPathLayer.SetVisible(_works);
            root.PriceField.SetGood(_priceGood);
            root.PriceField.SetVisible(_price);
            root.PlagueLayer.SetVisible(_plague);
            root.NewsLayer.SetVisible(_news);
            root.PoiLayer.SetVisible(_pois);
            root.NatureField.Select(_nature);

            LensChanged?.Invoke();
        }
    }
}
