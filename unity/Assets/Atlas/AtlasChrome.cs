using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>The one UI Toolkit chrome skeleton (K3 unified UI layer):
    /// owns the UIDocument, attaches the cassette structure stylesheet
    /// (AtlasChrome.uss via Resources; palette rides the PanelSettings
    /// theme — SSG-Ice), builds the named hosts every chrome module
    /// populates (top bar, rail, dock, tooltip layer, legend), and owns
    /// the AtlasPointerGuard test for ALL chrome. Modules never install a
    /// second guard.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AtlasChrome : MonoBehaviour
    {
        private VisualElement _topBar;
        private ScrollView _rail;
        private ScrollView _dock;
        private VisualElement _tooltipLayer;
        private VisualElement _legend;
        private VisualElement _timeline;
        private bool _built;

        public VisualElement TopBar { get { EnsureBuilt(); return _topBar; } }
        public ScrollView Rail { get { EnsureBuilt(); return _rail; } }
        public ScrollView Dock { get { EnsureBuilt(); return _dock; } }
        public VisualElement TooltipLayer
        { get { EnsureBuilt(); return _tooltipLayer; } }
        public VisualElement Legend { get { EnsureBuilt(); return _legend; } }
        public VisualElement Timeline { get { EnsureBuilt(); return _timeline; } }

        private void OnEnable()
        {
            EnsureBuilt();
            AtlasPointerGuard.Test = BlocksPointer;
        }

        private void OnDisable()
        {
            if (AtlasPointerGuard.Test == (Func<Vector2, bool>)BlocksPointer)
                AtlasPointerGuard.Test = null;
            _built = false;
        }

        /// <summary>Idempotent build — sibling modules may touch the hosts
        /// from their own OnEnable before ours ran.</summary>
        public void EnsureBuilt()
        {
            if (_built) return;
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Clear();
            // The document root stretches over the whole screen — it must
            // never pick, or the pointer guard reports chrome everywhere.
            root.pickingMode = PickingMode.Ignore;

            var sheet = Resources.Load<StyleSheet>("AtlasChrome");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("AtlasChrome: Resources/AtlasChrome.uss missing");

            _topBar = new VisualElement();
            _topBar.AddToClassList("ssg-topbar");

            _rail = new ScrollView(ScrollViewMode.Vertical);
            _rail.AddToClassList("ssg-rail");
            HideScrollers(_rail);

            _dock = new ScrollView(ScrollViewMode.Vertical);
            _dock.AddToClassList("ssg-dock");
            // an empty dock must not eat map clicks down its whole column —
            // the ScrollView's own chain (root, viewport, container) all
            // ignore; only the panels inside pick
            _dock.pickingMode = PickingMode.Ignore;
            _dock.contentViewport.pickingMode = PickingMode.Ignore;
            _dock.contentContainer.pickingMode = PickingMode.Ignore;
            HideScrollers(_dock);

            _legend = new VisualElement();
            _legend.AddToClassList("ssg-legend");
            _legend.style.display = DisplayStyle.None;

            _timeline = new VisualElement();
            _timeline.AddToClassList("ssg-strip");

            _tooltipLayer = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute,
                          left = 0, top = 0, right = 0, bottom = 0 },
            };
            _tooltipLayer.style.display = DisplayStyle.None;

            // paint order = child order: bars and panels, then the legend,
            // the tooltip always on top
            root.Add(_rail);
            root.Add(_dock);
            root.Add(_timeline);
            root.Add(_topBar);
            root.Add(_legend);
            root.Add(_tooltipLayer);
            _built = true;
        }

        /// <summary>Chrome never shows scroll bars (the eyeball note):
        /// scrolling is a natural result of the wheel, not a widget.</summary>
        public static void HideScrollers(ScrollView scroll)
        {
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        /// <summary>True when the screen position (input-system coords,
        /// origin bottom-left) lands on any chrome. The tooltip layer is
        /// picking-ignored, so it never blocks the map.</summary>
        private bool BlocksPointer(Vector2 screenPos)
        {
            if (_topBar?.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(
                _topBar.panel, new Vector2(screenPos.x,
                    Screen.height - screenPos.y));
            return _topBar.panel.Pick(panelPos) != null;
        }
    }
}
