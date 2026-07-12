using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>The top bar (K3): eye chip (god — controller reserved) ·
    /// world-year + epoch + era name · config stamp (seed, radius,
    /// artifact) · registry search (the drawer) · artifact load box
    /// (SimHost auto-load stays the default). The rail's K2 year readout
    /// retired into this.</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class TopBar : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;
        [SerializeField] private InspectorDock dock;

        private Label _clock;
        private Label _era;
        private Label _stamp;

        public void Wire(AtlasRoot atlasRoot, InspectorDock inspectorDock)
        {
            root = atlasRoot;
            dock = inspectorDock;
        }

        private void OnEnable()
        {
            BuildUi();
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded += Refresh;
                if (root.SimHost.State != null) Refresh();
            }
        }

        private void OnDisable()
        {
            if (root != null && root.SimHost != null)
                root.SimHost.Loaded -= Refresh;
        }

        private void BuildUi()
        {
            var bar = GetComponent<AtlasChrome>().TopBar;
            if (bar == null) return;
            bar.Clear();

            var eye = new Label("GOD ▮");
            eye.AddToClassList("ssg-topbar__eye");
            bar.Add(eye);

            _clock = new Label("no artifact");
            _clock.AddToClassList("ssg-topbar__clock");
            bar.Add(_clock);

            _era = new Label(string.Empty);
            _era.AddToClassList("ssg-topbar__era");
            bar.Add(_era);

            _stamp = new Label(string.Empty);
            _stamp.AddToClassList("ssg-topbar__stamp");
            bar.Add(_stamp);

            var spacer = new VisualElement();
            spacer.AddToClassList("ssg-spacer");
            bar.Add(spacer);

            DrawerButton(bar, "THREADS", PanelType.Threads);
            DrawerButton(bar, "STATS", PanelType.Stats);
            DrawerButton(bar, "GOODS", PanelType.Goods);
            DrawerButton(bar, "KNOBS", PanelType.Knobs);

            var search = new TextField();
            search.AddToClassList("ssg-topbar__search");
            search.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return
                    && e.keyCode != KeyCode.KeypadEnter) return;
                if (!string.IsNullOrWhiteSpace(search.value))
                    dock?.Show(new PanelRequest(PanelType.Find,
                                                text: search.value.Trim()));
            }, TrickleDown.TrickleDown);
            bar.Add(search);
            var hint = new Label("find ▮");
            hint.AddToClassList("ssg-topbar__stamp");
            bar.Add(hint);

            var path = new TextField
            { value = root?.SimHost?.ArtifactPath ?? string.Empty };
            path.AddToClassList("ssg-topbar__search");
            bar.Add(path);
            var load = new Button(() =>
            {
                if (root?.SimHost == null) return;
                root.SimHost.ArtifactPath = path.value;
                if (!root.SimHost.LoadArtifact())
                    _clock.text = "load failed: " + root.SimHost.LoadError;
            }) { text = "LOAD" };
            load.AddToClassList("ssg-btn");
            load.AddToClassList("ssg-btn--accent");
            bar.Add(load);
        }

        private void DrawerButton(VisualElement bar, string label,
                                  PanelType type)
        {
            var btn = new Button(() =>
                dock?.Show(new PanelRequest(type))) { text = label };
            btn.AddToClassList("ssg-btn");
            btn.style.marginLeft = 6;
            bar.Add(btn);
        }

        private void Refresh()
        {
            var state = root.SimHost.State;
            if (state == null) return;
            _clock.text = DockKit.Inv(
                $"y{state.WorldYear} · epoch {state.EpochIndex}");
            _era.text = CurrentEraName(state);
            string artifact = System.IO.Path.GetFileName(
                root.SimHost.ArtifactPath ?? string.Empty);
            _stamp.text = DockKit.Inv(
                $"seed {state.Config.MasterSeed} · r{state.Skeleton.Config.GalaxyRadiusCells} · {artifact}");
        }

        /// <summary>The era the world currently sits in — the last
        /// detected era covering the live epoch (empty when none).</summary>
        private string CurrentEraName(Core.Epoch.SimState state)
        {
            List<EraRow> eras = EraQueries.Eras(root.SimHost.Model,
                root.SimHost.Eye);
            for (int i = eras.Count - 1; i >= 0; i--)
                if (eras[i].StartEpoch <= state.EpochIndex)
                    return eras[i].Name;
            return string.Empty;
        }
    }
}
