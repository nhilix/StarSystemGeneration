using System.IO;
using StarGen.AtlasView;
using UnityEditor;
using UnityEngine;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Headless acceptance shots: build the scene, load the
    /// default artifact (the seed-42 golden), show every K1 layer, render
    /// three views — top-down galaxy, tilted 2.5D domains, tilted region
    /// closeup — to PNGs at the repo root. Batchmode: -executeMethod
    /// StarGen.AtlasView.EditorTools.AtlasSmoke.RunFromCli (graphics ON —
    /// no -nographics — or there is nothing to render with).</summary>
    public static class AtlasSmoke
    {
        private const int Width = 1600, Height = 1000;

        [MenuItem("StarGen/Atlas Smoke Shots")]
        public static void RunFromMenu() => Run(exitOnFailure: false);

        public static void RunFromCli() => Run(exitOnFailure: true);

        private static void Run(bool exitOnFailure)
        {
            AtlasViewSceneSetup.SetupScene();

            var host = Object.FindAnyObjectByType<SimHost>();
            var stars = Object.FindAnyObjectByType<StarfieldLayer>();
            var domains = Object.FindAnyObjectByType<DomainFieldLayer>();
            var nature = Object.FindAnyObjectByType<NatureFieldLayer>();
            var lattice = Object.FindAnyObjectByType<LatticeLayer>();
            var lanes = Object.FindAnyObjectByType<LaneLayer>();
            var ports = Object.FindAnyObjectByType<PortLayer>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            var cam = rig.Cam;

            // Edit mode: Awake never ran; each layer builds its own material.
            stars.EnsureMaterial();
            domains.EnsureMaterial();
            nature.EnsureMaterial();
            lattice.EnsureMaterial();
            lanes.EnsureMaterial();
            ports.EnsureMaterial();

            if (!host.LoadArtifact())
            {
                Debug.LogError($"AtlasSmoke: load failed: {host.LoadError}");
                if (exitOnFailure) EditorApplication.Exit(1);
                return;
            }
            var eye = host.Eye;
            var model = host.Model;
            stars.Show(model);
            domains.Show(model, eye);
            nature.Show(model, eye);
            lattice.Prepare(model);
            lanes.Show(model, eye);
            ports.Show(model, eye);

            cam.aspect = (float)Width / Height;
            var bounds = AtlasGeometry.DiscBounds(model);
            rig.FitTo(bounds);
            float extent = rig.GalaxyExtent;
            float fit = rig.Distance;

            // Shot 1: the whole disc, top-down (the 90° limit).
            SetAndStyle(rig, lanes, lattice, bounds.center, fit, 90f);
            Capture(cam, "atlas-smoke.png");
            Debug.Log($"AtlasSmoke: galaxy top-down — year {host.State.WorldYear}, "
                + $"{host.State.Ports.Count} ports, {host.State.Lanes.Count} lanes, "
                + $"band {rig.Band}");

            // Shot 2: the settled reach, tilted — the 2.5D domains view.
            var port0 = AtlasGeometry.HexToWorld(host.State.Ports[0].Hex);
            SetAndStyle(rig, lanes, lattice, port0, extent * 0.7f, 55f);
            Capture(cam, "atlas-smoke-domains.png");
            Debug.Log($"AtlasSmoke: tilted domains at band {rig.Band}");

            // Shot 3: region closeup, tilted — lattice resolving.
            SetAndStyle(rig, lanes, lattice, port0, extent * 0.11f, 60f);
            Capture(cam, "atlas-smoke-region.png");
            Debug.Log($"AtlasSmoke: region closeup at band {rig.Band}");

            if (exitOnFailure) EditorApplication.Exit(0);
        }

        private static void SetAndStyle(CameraRig rig, LaneLayer lanes,
            LatticeLayer lattice, Vector3 focus, float distance, float pitch)
        {
            rig.SetView(focus, distance, pitch);
            // Edit mode: the rig's ZoomChanged fires, but listeners are
            // wired by AtlasRoot.OnEnable which never ran — style by hand.
            lanes.SetExtent(rig.GalaxyExtent);
            lanes.ViewportPx = Height;
            lanes.OnZoom(rig.Distance);
            lattice.OnZoom(rig.Distance, rig.GalaxyExtent);
        }

        private static void Capture(Camera cam, string fileName)
        {
            var rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            cam.aspect = (float)Width / Height;
            Shader.SetGlobalFloat("_AtlasFocalY",
                1f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            Shader.SetGlobalFloat("_AtlasViewportPx", Height);
            cam.Render();
            RenderTexture.active = rt;
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", fileName));
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(shot);
            Debug.Log($"AtlasSmoke: wrote {path}");
        }
    }
}
