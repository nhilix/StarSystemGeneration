using System;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>PROVISIONAL K1 chrome (IMGUI on purpose — zero assets, zero
    /// ceremony): artifact load, nature-layer cycle, lens toggles, band and
    /// year readout. K2 replaces this with the real UI Toolkit lens rail;
    /// nothing here is load-bearing beyond the eyeball gate.</summary>
    public sealed class AtlasHud : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private void OnGUI()
        {
            if (root == null || root.SimHost == null) return;
            var host = root.SimHost;

            GUILayout.BeginArea(new Rect(10, 10, 460, 220),
                                GUI.skin.box);
            GUILayout.Label("StarGen Atlas — K1 skeleton (provisional HUD)");

            GUILayout.BeginHorizontal();
            host.ArtifactPath = GUILayout.TextField(host.ArtifactPath,
                                                    GUILayout.Width(340));
            if (GUILayout.Button("Load")) host.LoadArtifact();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(host.LoadError))
                GUILayout.Label($"load failed: {host.LoadError}");

            if (host.Model == null)
            {
                GUILayout.Label("no artifact loaded");
            }
            else
            {
                var state = host.State;
                GUILayout.Label($"year {state.WorldYear} · epoch {state.EpochIndex}"
                    + $" · seed {state.Config.MasterSeed}"
                    + $" · band {root.CameraRig.Band}");

                GUILayout.BeginHorizontal();
                var surface = root.MapSurface;
                if (GUILayout.Button($"nature: {surface.Nature}"))
                {
                    var values = (NatureLayer[])Enum.GetValues(typeof(NatureLayer));
                    surface.Nature = values[((int)surface.Nature + 1) % values.Length];
                    surface.Restyle();
                }
                bool domains = GUILayout.Toggle(surface.ShowDomains, "domains");
                if (domains != surface.ShowDomains)
                {
                    surface.ShowDomains = domains;
                    surface.Restyle();
                }
                bool lanes = GUILayout.Toggle(_lanesOn, "lanes");
                if (lanes != _lanesOn)
                {
                    _lanesOn = lanes;
                    root.LaneLayer.SetVisible(lanes);
                }
                bool ports = GUILayout.Toggle(_portsOn, "ports");
                if (ports != _portsOn)
                {
                    _portsOn = ports;
                    root.PortLayer.SetVisible(ports);
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("scroll = zoom to cursor · right-drag/WASD = pan");
            }
            GUILayout.EndArea();
        }

        private bool _lanesOn = true;
        private bool _portsOn = true;
    }
}
