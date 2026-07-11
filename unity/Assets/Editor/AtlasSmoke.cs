using System.IO;
using StarGen.AtlasView;
using UnityEditor;
using UnityEngine;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Headless acceptance shot (the PoC AtlasAcceptance pattern):
    /// build the scene, load the default artifact (the seed-42 golden),
    /// show every K1 layer, and render the camera to atlas-smoke.png at the
    /// repo root. Batchmode: -executeMethod
    /// StarGen.AtlasView.EditorTools.AtlasSmoke.RunFromCli (graphics ON —
    /// no -nographics — or there is nothing to render with).</summary>
    public static class AtlasSmoke
    {
        private const int Width = 1600, Height = 1000;

        [MenuItem("StarGen/Atlas Smoke Shot")]
        public static void RunFromMenu() => Run(exitOnFailure: false);

        public static void RunFromCli() => Run(exitOnFailure: true);

        private static void Run(bool exitOnFailure)
        {
            AtlasViewSceneSetup.SetupScene();

            var host = Object.FindFirstObjectByType<SimHost>();
            var map = Object.FindFirstObjectByType<MapSurface>();
            var lanes = Object.FindFirstObjectByType<LaneLayer>();
            var ports = Object.FindFirstObjectByType<PortLayer>();
            var rig = Object.FindFirstObjectByType<CameraRig>();
            var cam = rig.Cam;

            // Edit mode: Awake never ran, so assign the vertex-color
            // material by hand (play mode does this in each layer's Awake).
            var material = new Material(Shader.Find("Sprites/Default"));
            map.GetComponent<MeshRenderer>().sharedMaterial = material;
            lanes.GetComponent<MeshRenderer>().sharedMaterial = material;
            ports.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!host.LoadArtifact())
            {
                Debug.LogError($"AtlasSmoke: load failed: {host.LoadError}");
                if (exitOnFailure) EditorApplication.Exit(1);
                return;
            }
            var eye = host.Eye;
            map.Show(host.Model, eye);
            lanes.Show(host.Model, eye);
            ports.Show(host.Model, eye);
            rig.FitTo(map.MapBounds);
            lanes.SetBand(rig.Band);
            ports.SetBand(rig.Band);

            Capture(cam, "atlas-smoke.png");
            Debug.Log($"AtlasSmoke: galaxy shot — year {host.State.WorldYear}, "
                + $"{host.State.Ports.Count} ports, {host.State.Lanes.Count} lanes, "
                + $"band {rig.Band}");

            // Second shot down the continuum: Region band centered on the
            // first port — per-hex organic borders should resolve.
            float extent = Mathf.Max(map.MapBounds.extents.x, map.MapBounds.extents.y);
            var (px, py) = StarGen.Core.Galaxy.HexGrid.HexToWorld(host.State.Ports[0].Hex);
            cam.transform.position = new Vector3((float)px, (float)py, -10f);
            cam.orthographicSize = extent * 0.12f;
            var band = LodBands.BandFor(cam.orthographicSize, extent);
            lanes.SetBand(band);
            ports.SetBand(band);
            Capture(cam, "atlas-smoke-region.png");
            Debug.Log($"AtlasSmoke: region shot at port 0, band {band}");
            if (exitOnFailure) EditorApplication.Exit(0);
        }

        private static void Capture(Camera cam, string fileName)
        {
            var rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            cam.aspect = (float)Width / Height;
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
