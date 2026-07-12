using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>The hover-hex tooltip (K3): what's here — system summary,
    /// domain owner(s), port tier, live POI — from HexQuery via the
    /// SelectionModel's hover state. Rides AtlasChrome's picking-ignored
    /// tooltip layer, following the cursor.</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class HexTooltip : MonoBehaviour
    {
        [SerializeField] private SelectionModel selection;

        /// <summary>The cursor must REST on a hex this long before the tip
        /// shows (the eyeball note: instant tips spam every hex crossed).</summary>
        private const float HoverRestSeconds = 0.45f;

        private VisualElement _tip;
        private Label _title;
        private VisualElement _lines;
        private float _restStartedAt;
        private bool _pending;

        public void Wire(SelectionModel selectionModel) =>
            selection = selectionModel;

        private void OnEnable()
        {
            var chrome = GetComponent<AtlasChrome>();
            var layer = chrome.TooltipLayer;
            layer.Clear();
            _tip = new VisualElement();
            _tip.AddToClassList("ssg-tip");
            _title = new Label();
            _title.AddToClassList("ssg-tip__title");
            _tip.Add(_title);
            _lines = new VisualElement();
            _tip.Add(_lines);
            layer.Add(_tip);
            if (selection != null) selection.HoverChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (selection != null) selection.HoverChanged -= Refresh;
        }

        private void Refresh()
        {
            // every hex change hides the tip and restarts the rest clock —
            // Update() shows it once the cursor has settled
            var layer = GetComponent<AtlasChrome>().TooltipLayer;
            layer.style.display = DisplayStyle.None;
            var info = selection != null ? selection.HoverInfo : null;
            _pending = info != null;
            _restStartedAt = Time.unscaledTime;
            if (info == null) return;
            // in the orbit view (K5) the hovered THING leads and the hex
            // context dims below it
            if (selection.StageHover is StagePick pick)
            {
                _title.text = pick.Label;
                _lines.Clear();
                Line(info.SystemSummary, dim: true);
                Line($"hex ({info.Hex.Q},{info.Hex.R})", dim: true);
                return;
            }
            _title.text = info.SystemSummary;
            _lines.Clear();
            Line($"hex ({info.Hex.Q},{info.Hex.R})", dim: true);
            if (info.OwnerNames.Count == 1)
                Line($"domain of {info.OwnerNames[0]}");
            else if (info.OwnerNames.Count > 1)
                Line("contested: " + string.Join(" · ", info.OwnerNames));
            else
                Line("the wilds", dim: true);
            if (info.PortId >= 0)
                Line($"port #{info.PortId} · tier {info.PortTier} · "
                     + info.PortOwnerName);
            foreach (var poi in info.LivePois)
                Line($"{poi.TypeName}" + (poi.Dormant ? " · DORMANT" : ""));
        }

        private void Line(string text, bool dim = false)
        {
            var label = new Label(text);
            label.AddToClassList("ssg-tip__line");
            if (dim) label.AddToClassList("ssg-tip__line--dim");
            _lines.Add(label);
        }

        private void Update()
        {
            if (_tip == null || selection == null
                || selection.HoverInfo == null) return;
            if (_pending
                && Time.unscaledTime - _restStartedAt >= HoverRestSeconds)
            {
                _pending = false;
                GetComponent<AtlasChrome>().TooltipLayer.style.display =
                    DisplayStyle.Flex;
            }
            var mouse = Mouse.current;
            if (mouse == null || _tip.panel == null) return;
            var screenPos = mouse.position.ReadValue();
            var panelPos = RuntimePanelUtils.ScreenToPanel(_tip.panel,
                new Vector2(screenPos.x, Screen.height - screenPos.y));
            float x = panelPos.x + 14f;
            float y = panelPos.y + 10f;
            // keep the tip on screen (panel space)
            var panelSize = _tip.panel.visualTree.layout.size;
            if (x + _tip.layout.width + 8 > panelSize.x)
                x = panelPos.x - _tip.layout.width - 10f;
            if (y + _tip.layout.height + 8 > panelSize.y)
                y = panelPos.y - _tip.layout.height - 8f;
            _tip.style.left = x;
            _tip.style.top = y;
        }
    }
}
