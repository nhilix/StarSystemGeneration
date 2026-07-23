using System.IO;
using StarGen.AtlasView;
using UnityEditor;
using UnityEngine;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Headless acceptance shots: build the scene, load the
    /// default artifact (the seed-42 golden), render the K1 base views
    /// plus one shot per K2 lens — the pre-eyeball loop against REPL
    /// `emap`. PNGs land at the repo root. Batchmode: -executeMethod
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
            var interior = Object.FindAnyObjectByType<DomainInteriorLayer>();
            var outposts = Object.FindAnyObjectByType<OutpostLayer>();
            var nature = Object.FindAnyObjectByType<NatureFieldLayer>();
            var price = Object.FindAnyObjectByType<PriceFieldLayer>();
            var lattice = Object.FindAnyObjectByType<LatticeLayer>();
            var lanes = Object.FindAnyObjectByType<LaneLayer>();
            var ports = Object.FindAnyObjectByType<PortLayer>();
            var fleets = Object.FindAnyObjectByType<FleetLayer>();
            var pois = Object.FindAnyObjectByType<PoiLayer>();
            var works = Object.FindAnyObjectByType<WorksLayer>();
            var plague = Object.FindAnyObjectByType<PlagueLayer>();
            var war = Object.FindAnyObjectByType<WarLayer>();
            var news = Object.FindAnyObjectByType<NewsLayer>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            var cam = rig.Cam;

            // Edit mode: Awake never ran; each layer builds its own material.
            stars.EnsureMaterial();
            domains.EnsureMaterial();
            interior.EnsureMaterial();
            outposts.EnsureMaterial();
            nature.EnsureMaterial();
            price.EnsureMaterial();
            lattice.EnsureMaterial();
            lanes.EnsureMaterial();
            ports.EnsureMaterial();
            fleets.EnsureMaterial();
            pois.EnsureMaterial();
            works.EnsureMaterial();
            plague.EnsureMaterial();
            war.EnsureMaterial();
            news.EnsureMaterial();

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
            interior.Show(model, eye);
            outposts.Show(model, eye);
            nature.Show(model, eye);
            price.Show(model, eye);
            lattice.Prepare(model);
            lanes.Show(model, eye);
            ports.Show(model, eye);
            fleets.Show(model, eye);
            pois.Show(model, eye);
            works.Show(model, eye);
            plague.Show(model, eye);
            war.Show(model, eye);
            news.Show(model, eye);

            // The K2 lens layers start hidden — each shot opts in.
            fleets.SetVisible(false);
            pois.SetVisible(false);
            works.SetVisible(false);
            plague.SetVisible(false);
            war.SetVisible(false);
            news.SetVisible(false);
            price.SetVisible(false);

            cam.aspect = (float)Width / Height;
            var bounds = AtlasGeometry.DiscBounds(model);
            rig.FitTo(bounds);
            float extent = rig.GalaxyExtent;
            float fit = rig.Distance;
            var port0 = AtlasGeometry.HexToWorld(host.State.Ports[0].Hex);

            void View(Vector3 focus, float distance, float pitch)
            {
                rig.SetView(focus, distance, pitch);
                SetAndStyle(rig, lanes, lattice,
                            fleets, pois, works, plague, war);
            }

            // ---- K1 base views ----
            View(bounds.center, fit, 90f);
            Capture(cam, "atlas-smoke.png");
            Debug.Log($"AtlasSmoke: galaxy top-down — year {host.State.WorldYear}, "
                + $"{host.State.Ports.Count} ports, {host.State.Lanes.Count} lanes, "
                + $"band {rig.Band}");

            nature.Select(StarGen.Core.Atlas.NatureLayer.Gas);
            Capture(cam, "atlas-smoke-nature.png");
            nature.Select(null);

            View(port0, extent * 0.7f, 55f);
            Capture(cam, "atlas-smoke-domains.png");

            View(port0, extent * 0.11f, 60f);
            Capture(cam, "atlas-smoke-region.png");

            // ---- K2 lens shots: the settled reach, glyphs resolved ----
            var lensView = port0;
            float lensDistance = extent * 0.30f;

            View(lensView, lensDistance, 62f);

            lanes.SetMode(LaneMode.Traffic);
            Capture(cam, "atlas-smoke-traffic.png");
            lanes.SetMode(LaneMode.Status);

            lanes.SetMode(LaneMode.Trade);
            Capture(cam, "atlas-smoke-trade.png");
            lanes.SetMode(LaneMode.Status);

            fleets.SetVisible(true);
            Capture(cam, "atlas-smoke-fleets.png");
            fleets.SetVisible(false);

            price.SetVisible(true);
            Capture(cam, "atlas-smoke-price.png");
            price.SetVisible(false);

            domains.SetAccent(DomainAccent.War);
            war.SetVisible(true);
            Capture(cam, "atlas-smoke-war.png");
            war.SetVisible(false);

            domains.SetAccent(DomainAccent.Tension);
            Capture(cam, "atlas-smoke-tension.png");

            domains.SetAccent(DomainAccent.Tech);
            Capture(cam, "atlas-smoke-tech.png");
            domains.SetAccent(DomainAccent.Owner);

            plague.SetVisible(true);
            lanes.SetMode(LaneMode.QuarantineOnly);
            Capture(cam, "atlas-smoke-plague.png");
            plague.SetVisible(false);
            lanes.SetMode(LaneMode.Status);

            news.SetVisible(true);
            Capture(cam, "atlas-smoke-news.png");
            news.SetVisible(false);

            pois.SetVisible(true);
            Capture(cam, "atlas-smoke-pois.png");
            pois.SetVisible(false);

            works.SetVisible(true);
            Capture(cam, "atlas-smoke-works.png");
            works.SetVisible(false);

            // ---- K5: the orbit stage — the map's curves die at this
            // distance (MapFade 0) and the system view stands alone ----
            var stage = Object.FindAnyObjectByType<SystemStage>();
            var portHex = StarGen.Core.Galaxy.HexGrid.WorldToHex(port0.x,
                                                                 port0.y);
            View(port0, 3.6f, 60f);
            stage.RenderAt(model, eye, portHex, 1f);
            Capture(cam, "atlas-smoke-system.png");
            Debug.Log($"AtlasSmoke: system stage — band {rig.Band}, "
                + $"{stage.Pickables.Count} pickables at the port hex");

            // the multi-system field mid-crossfade (every hex renders —
            // the play-mode no-pop-in behavior, area form)
            View(port0, 8.5f, 60f);
            stage.RenderArea(model, eye, portHex, 3,
                LodBands.StageFade(8.5f, rig.GalaxyExtent));
            Capture(cam, "atlas-smoke-system-field.png");
            Debug.Log($"AtlasSmoke: system field — "
                + $"{stage.Pickables.Count} pickables across the area");
            stage.RenderAt(model, eye, portHex, 0f);

            Debug.Log($"AtlasSmoke: lens suite rendered — "
                + $"{host.State.Fleets.Count} fleets, {host.State.Pois.Count} POIs, "
                + $"{host.State.Projects.Count} projects, "
                + $"{host.State.Shipments.Count} shipments, "
                + $"{host.State.Pulses.Count} pulses, "
                + $"{host.State.Plagues.Count} plagues");

            if (exitOnFailure) EditorApplication.Exit(0);
        }

        private static void SetAndStyle(CameraRig rig, LaneLayer lanes,
            LatticeLayer lattice,
            FleetLayer fleets, PoiLayer pois, WorksLayer works,
            PlagueLayer plague, WarLayer war)
        {
            // Edit mode: the rig's ZoomChanged fires, but listeners are
            // wired by AtlasRoot.OnEnable which never ran — style by hand
            // (mirror AtlasRoot.OnZoomChanged, crossfade hooks included).
            lanes.SetExtent(rig.GalaxyExtent);
            lanes.ViewportPx = Height;
            lanes.OnZoom(rig.Distance);
            lattice.OnZoom(rig.Distance, rig.GalaxyExtent);
            float extent = rig.GalaxyExtent;
            fleets.OnZoom(rig.Distance, extent);
            pois.OnZoom(rig.Distance, extent);
            works.OnZoom(rig.Distance, extent);
            plague.OnZoom(rig.Distance, extent);
            war.OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<PortLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<OutpostLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<DomainInteriorLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<NewsLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<DomainFieldLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<NatureFieldLayer>().OnZoom(rig.Distance, extent);
            Object.FindAnyObjectByType<PriceFieldLayer>().OnZoom(rig.Distance, extent);
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
