using System;
using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>Every panel the dock can open (diagram §9 + the T1/T2
    /// additions).</summary>
    public enum PanelType
    {
        Hex, Polity, Market, Project, Shipment, Fleet, Designs, Wars, War,
        Relations, Character, Corporations, Poi, Beliefs, News, Stances,
        Chronicle, ChroniclePlace, Eras, Threads, Find, Goods, Knobs, Stats,
    }

    /// <summary>What to open: a panel type plus its subject id/hex/text
    /// argument (unused fields ignored per type).</summary>
    public readonly struct PanelRequest
    {
        public readonly PanelType Type;
        public readonly int Id;
        public readonly HexCoordinate Hex;
        public readonly string Text;

        public PanelRequest(PanelType type, int id = -1,
                            HexCoordinate hex = default, string text = null)
        {
            Type = type;
            Id = id;
            Hex = hex;
            Text = text;
        }
    }

    /// <summary>What panel builders can do besides build: open further
    /// panels (links) and jump the camera to a subject.</summary>
    public sealed class PanelContext
    {
        public AtlasReadModel Model;
        public EyeContext Eye;
        public Action<PanelRequest> Open;
        public Action<HexCoordinate> JumpTo;
    }

    /// <summary>The right-side inspector dock (K3): selection opens typed
    /// panels; PIN keeps a panel for comparison while unpinned ones are
    /// replaced by the next selection; X closes. Panel content comes from
    /// PanelViews over the T1 queries.</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class InspectorDock : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;
        [SerializeField] private SelectionModel selection;

        private readonly List<(VisualElement Element, bool Pinned)> _open
            = new();

        public void Wire(AtlasRoot atlasRoot, SelectionModel selectionModel)
        {
            root = atlasRoot;
            selection = selectionModel;
        }

        private void OnEnable()
        {
            if (selection != null) selection.Selected += OnSelected;
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded += OpenThreads;
                // Open Threads is the atlas's OPENING SCREEN: the world in
                // motion greets you (spec §Panel catalog)
                if (root.SimHost.Model != null) OpenThreads();
            }
        }

        private void OnDisable()
        {
            if (selection != null) selection.Selected -= OnSelected;
            if (root != null && root.SimHost != null)
                root.SimHost.Loaded -= OpenThreads;
        }

        private void OpenThreads() =>
            Show(new PanelRequest(PanelType.Threads));

        private PanelContext Context()
        {
            var state = root.SimHost.State;
            return new PanelContext
            {
                Model = root.SimHost.Model,
                Eye = EyeContext.God(state.WorldYear),
                Open = request => Show(request),
                JumpTo = hex =>
                {
                    var world = AtlasGeometry.HexToWorld(hex);
                    root.CameraRig.SetView(world, 24f, root.CameraRig.Pitch);
                },
            };
        }

        private void OnSelected(Selection sel)
        {
            switch (sel.Kind)
            {
                case SelectionKind.Port:
                    // the port click populates market AND its owner's
                    // polity panel (the K3 eyeball line) — one clear, two
                    // panels (review finding 1: a second clearing Show
                    // would destroy the polity panel it just opened)
                    var state = root.SimHost.State;
                    if (sel.Id >= 0 && sel.Id < state.Ports.Count)
                        Show(new PanelRequest(PanelType.Polity,
                            state.Ports[sel.Id].OwnerActorId));
                    Show(new PanelRequest(PanelType.Market, sel.Id),
                         clearUnpinned: false);
                    break;
                case SelectionKind.Project:
                    Show(new PanelRequest(PanelType.Project, sel.Id));
                    break;
                case SelectionKind.Shipment:
                    Show(new PanelRequest(PanelType.Shipment, sel.Id));
                    break;
                case SelectionKind.Fleet:
                    Show(new PanelRequest(PanelType.Fleet, sel.Id));
                    break;
                case SelectionKind.Poi:
                    Show(new PanelRequest(PanelType.Poi, sel.Id));
                    break;
                case SelectionKind.Hex:
                    Show(new PanelRequest(PanelType.Hex, hex: sel.Hex));
                    break;
            }
        }

        /// <summary>Open a panel: unpinned panels of any type make way
        /// (pass clearUnpinned: false to stack a second panel in the same
        /// interaction); pinned ones stay for comparison.</summary>
        public void Show(PanelRequest request, bool clearUnpinned = true)
        {
            if (root == null || root.SimHost?.Model == null) return;
            var chrome = GetComponent<AtlasChrome>();
            var dock = chrome.Dock;

            if (clearUnpinned)
                for (int i = _open.Count - 1; i >= 0; i--)
                    if (!_open[i].Pinned)
                    {
                        _open[i].Element.RemoveFromHierarchy();
                        _open.RemoveAt(i);
                    }

            var context = Context();
            var (title, body) = PanelViews.Build(request, context);
            if (body == null) return;

            var panel = new VisualElement();
            panel.AddToClassList("ssg-panel");
            var head = new VisualElement();
            head.AddToClassList("ssg-panel__head");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("ssg-panel__title");
            head.Add(titleLabel);
            var spacer = new VisualElement();
            spacer.AddToClassList("ssg-spacer");
            head.Add(spacer);

            var pin = new Button { text = "PIN" };
            pin.AddToClassList("ssg-panel__btn");
            pin.clicked += () =>
            {
                for (int i = 0; i < _open.Count; i++)
                    if (_open[i].Element == panel)
                    {
                        bool now = !_open[i].Pinned;
                        _open[i] = (panel, now);
                        pin.text = now ? "PINNED" : "PIN";
                        pin.EnableInClassList("ssg-panel__btn--pinned", now);
                        panel.EnableInClassList("ssg-panel--pinned", now);
                        break;
                    }
            };
            head.Add(pin);

            var close = new Button { text = "X" };
            close.AddToClassList("ssg-panel__btn");
            close.clicked += () =>
            {
                panel.RemoveFromHierarchy();
                for (int i = _open.Count - 1; i >= 0; i--)
                    if (_open[i].Element == panel) _open.RemoveAt(i);
            };
            head.Add(close);
            panel.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("ssg-panel__body");
            AtlasChrome.HideScrollers(scroll);
            scroll.Add(body);
            panel.Add(scroll);

            dock.Add(panel);
            _open.Add((panel, false));
        }
    }
}
