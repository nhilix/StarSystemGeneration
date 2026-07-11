using System;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>PROVISIONAL K1 chrome (IMGUI on purpose — zero assets):
    /// artifact load, nature-layer cycle (off = starfield only), lens
    /// toggles, band/year readout. K2 replaces this with the UI Toolkit
    /// lens rail; nothing here is load-bearing beyond the eyeball gate.</summary>
    public sealed class AtlasHud : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;

        private bool _lanesOn = true;
        private bool _portsOn = true;
        private bool _domainsOn = true;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private void OnGUI()
        {
            if (root == null || root.SimHost == null) return;
            var host = root.SimHost;

            GUILayout.BeginArea(new Rect(10, 10, 470, 190), GUI.skin.box);
            GUILayout.Label("StarGen Atlas — K1 skeleton (provisional HUD)");

            GUILayout.BeginHorizontal();
            host.ArtifactPath = GUILayout.TextField(host.ArtifactPath,
                                                    GUILayout.Width(350));
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
                var rig = root.CameraRig;
                GUILayout.Label($"year {state.WorldYear} · epoch {state.EpochIndex}"
                    + $" · seed {state.Config.MasterSeed}"
                    + $" · band {rig.Band} · pitch {rig.Pitch:0}°");

                GUILayout.BeginHorizontal();
                var nature = root.NatureField;
                string natureLabel = nature.Current is { } l
                    ? l.ToString() : "off";
                if (GUILayout.Button($"nature: {natureLabel}"))
                    nature.Select(NextNature(nature.Current));
                bool domains = GUILayout.Toggle(_domainsOn, "domains");
                if (domains != _domainsOn)
                {
                    _domainsOn = domains;
                    root.DomainField.SetVisible(domains);
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
                GUILayout.Label("scroll = zoom to cursor · right-drag/WASD = pan"
                    + " · middle-drag = tilt (90° = top-down)");
            }
            GUILayout.EndArea();
        }

        private static NatureLayer? NextNature(NatureLayer? current)
        {
            var values = (NatureLayer[])Enum.GetValues(typeof(NatureLayer));
            if (current == null) return values[0];
            int next = (int)current.Value + 1;
            return next >= values.Length ? null : values[next];
        }
    }
}
