using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StarGen.AtlasView;
using UnityEditor;
using UnityEngine;
// `using System` makes a bare Object ambiguous; AtlasSmoke never needed it.
using Object = UnityEngine.Object;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>The eyeball gate widened from one seed to a grid: load every
    /// artifact in runs/atlas-grid in turn (SimHost.ArtifactPath is the seam —
    /// set it, LoadArtifact(), repeat), shoot the same six lenses on each, and
    /// emit a self-contained contact sheet at atlas-grid/index.html. Reading
    /// *down* a column is the point — one lens across six worlds tells you
    /// whether a look is the design or the seed. Batchmode: -executeMethod
    /// StarGen.AtlasView.EditorTools.AtlasGrid.RunFromCli (graphics ON).</summary>
    public static class AtlasGrid
    {
        // A contact sheet, not an acceptance shot: 36 PNGs at AtlasSmoke's
        // 1600x1000 is a lot of bytes for something read as thumbnails.
        private const int Width = 1200, Height = 750;
        private const string InputDir = "runs/atlas-grid";
        private const string OutputDir = "atlas-grid";

        private static readonly string[] Lenses =
            { "galaxy", "domains", "trade", "price", "war", "works" };

        /// <summary>One row's worth of contact sheet: either six shots plus
        /// the world's vitals, or the load error that killed the row.</summary>
        private sealed class SeedRow
        {
            public string Label;
            public string Error;
            public ulong MasterSeed;
            public int Year, Ports, Lanes, Fleets;
        }

        [MenuItem("StarGen/Atlas Grid")]
        public static void RunFromMenu() => Run(exitOnFailure: false);

        public static void RunFromCli() => Run(exitOnFailure: true);

        private static void Run(bool exitOnFailure)
        {
            string inputDir = RepoPath(InputDir);
            if (!Directory.Exists(inputDir))
            {
                Debug.LogError($"AtlasGrid: no artifact directory at {inputDir} "
                    + "— generate one with the Inspector REPL (`epoch <seed> 40 21`"
                    + " + `esave`).");
                if (exitOnFailure) EditorApplication.Exit(1);
                return;
            }
            var artifacts = Directory.GetFiles(inputDir, "*.txt");
            // Ordinal, not culture: the grid's row order must be the same on
            // every machine that regenerates the sheet.
            Array.Sort(artifacts, StringComparer.Ordinal);
            if (artifacts.Length == 0)
            {
                Debug.LogError($"AtlasGrid: {inputDir} holds no *.txt artifacts.");
                if (exitOnFailure) EditorApplication.Exit(1);
                return;
            }

            string outputDir = RepoPath(OutputDir);
            Directory.CreateDirectory(outputDir);

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
            var flowTrails = Object.FindAnyObjectByType<FlowTrailLayer>();
            var crawlPaths = Object.FindAnyObjectByType<CrawlPathLayer>();
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
            flowTrails.EnsureMaterial();
            crawlPaths.EnsureMaterial();
            plague.EnsureMaterial();
            war.EnsureMaterial();
            news.EnsureMaterial();

            cam.aspect = (float)Width / Height;

            var rows = new List<SeedRow>();
            foreach (string artifact in artifacts)
            {
                string label = Path.GetFileNameWithoutExtension(artifact);
                var row = new SeedRow { Label = label };
                rows.Add(row);

                host.ArtifactPath = artifact;
                if (!host.LoadArtifact())
                {
                    // One malformed artifact must not cost the other five
                    // rows — record it and keep shooting.
                    row.Error = host.LoadError;
                    Debug.LogError($"AtlasGrid: {label} load failed: {host.LoadError}");
                    continue;
                }
                // Same reason as AtlasSmoke: a headless load never plays a
                // step, so CurrentFlows is empty until we advance once and
                // the works lens would render without trails.
                host.StepEpochs(1);

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
                flowTrails.Show(host.Machine.CurrentFlows, host.State.Shipments);
                crawlPaths.Show(model, eye);
                plague.Show(model, eye);
                war.Show(model, eye);
                news.Show(model, eye);

                // The K2 lens layers start hidden — each shot opts in, and
                // every shot restores the default so lenses never bleed into
                // the next cell (or the next row).
                fleets.SetVisible(false);
                pois.SetVisible(false);
                works.SetVisible(false);
                flowTrails.SetVisible(false);
                crawlPaths.SetVisible(false);
                plague.SetVisible(false);
                war.SetVisible(false);
                news.SetVisible(false);
                price.SetVisible(false);
                lanes.SetMode(LaneMode.Status);
                domains.SetAccent(DomainAccent.Owner);

                // Refit per seed: discs differ in extent, so a shared framing
                // would crop some worlds and shrink others.
                var bounds = AtlasGeometry.DiscBounds(model);
                rig.FitTo(bounds);
                float extent = rig.GalaxyExtent;
                float fit = rig.Distance;
                // The mid-zoom lenses anchor on the SETTLED CENTROID, not
                // Ports[0] the way AtlasSmoke does. Ports[0] is an arbitrary
                // port per world — on seed 9091 it sits at the tip of a long
                // tendril, which shoves that whole row's galaxy into a corner.
                // A grid exists to be read DOWN a column, so each seed has to
                // frame the comparable thing: the heart of its settled reach.
                var heart = bounds.center;
                if (host.State.Ports.Count > 0)
                {
                    var sum = Vector3.zero;
                    foreach (var p in host.State.Ports)
                        sum += AtlasGeometry.HexToWorld(p.Hex);
                    heart = sum / host.State.Ports.Count;
                }

                void View(Vector3 focus, float distance, float pitch)
                {
                    rig.SetView(focus, distance, pitch);
                    SetAndStyle(rig, lanes, lattice,
                                fleets, pois, works, plague, war);
                }

                View(bounds.center, fit, 90f);
                Capture(cam, outputDir, label, "galaxy");

                View(heart, extent * 0.7f, 55f);
                Capture(cam, outputDir, label, "domains");

                // The settled reach, glyphs resolved — AtlasSmoke's lens framing.
                View(heart, extent * 0.30f, 62f);

                lanes.SetMode(LaneMode.Trade);
                Capture(cam, outputDir, label, "trade");
                lanes.SetMode(LaneMode.Status);

                price.SetVisible(true);
                Capture(cam, outputDir, label, "price");
                price.SetVisible(false);

                domains.SetAccent(DomainAccent.War);
                war.SetVisible(true);
                Capture(cam, outputDir, label, "war");
                war.SetVisible(false);
                domains.SetAccent(DomainAccent.Owner);

                works.SetVisible(true);
                flowTrails.SetVisible(true);
                crawlPaths.SetVisible(true);
                Capture(cam, outputDir, label, "works");
                works.SetVisible(false);
                flowTrails.SetVisible(false);
                crawlPaths.SetVisible(false);

                row.MasterSeed = host.State.Config.MasterSeed;
                row.Year = host.State.WorldYear;
                row.Ports = host.State.Ports.Count;
                row.Lanes = host.State.Lanes.Count;
                row.Fleets = host.State.Fleets.Count;
                Debug.Log($"AtlasGrid: {label} — seed {row.MasterSeed}, "
                    + $"year {row.Year}, {row.Ports} ports, {row.Lanes} lanes, "
                    + $"{row.Fleets} fleets");
            }

            string indexPath = Path.Combine(outputDir, "index.html");
            File.WriteAllText(indexPath, BuildIndex(rows, inputDir), Encoding.UTF8);
            Debug.Log($"AtlasGrid: contact sheet at {indexPath} — "
                + $"{rows.Count} seeds x {Lenses.Length} lenses");

            if (exitOnFailure) EditorApplication.Exit(0);
        }

        private static string RepoPath(string relative) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", relative));

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
            var flowTrails = Object.FindAnyObjectByType<FlowTrailLayer>();
            flowTrails.SetExtent(rig.GalaxyExtent);
            flowTrails.ViewportPx = Height;
            flowTrails.OnZoom(rig.Distance);
            var crawlPaths = Object.FindAnyObjectByType<CrawlPathLayer>();
            crawlPaths.SetExtent(rig.GalaxyExtent);
            crawlPaths.ViewportPx = Height;
            crawlPaths.OnZoom(rig.Distance);
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

        private static void Capture(Camera cam, string outputDir,
                                    string label, string lens)
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
            File.WriteAllBytes(Path.Combine(outputDir, $"{label}-{lens}.png"),
                               shot.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(shot);
        }

        // ---- the contact sheet ----

        /// <summary>Self-contained by construction: no CDN, no web font, no
        /// library. Images are relative filenames sitting beside this file, so
        /// the whole atlas-grid/ folder is the artifact and moves as one.</summary>
        private static string BuildIndex(List<SeedRow> rows, string inputDir)
        {
            var sb = new StringBuilder();
            sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n")
              .Append("<meta charset=\"utf-8\">\n")
              .Append("<meta name=\"viewport\" content=\"width=device-width,")
              .Append(" initial-scale=1\">\n<title>Atlas Grid</title>\n<style>\n")
              .Append(Css)
              .Append("</style>\n</head>\n<body>\n");

            sb.Append("<header>\n<h1>Atlas Grid</h1>\n<dl>")
              .Append(Meta("generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")))
              .Append(Meta("artifacts", inputDir))
              .Append(Meta("grid", $"{rows.Count} seeds × {Lenses.Length} lenses"))
              .Append(Meta("capture", $"{Width} × {Height}"))
              .Append("</dl>\n</header>\n");

            sb.Append("<div class=\"scroll\">\n<table>\n<thead><tr>")
              .Append("<th class=\"seedhead\">seed</th>");
            foreach (string lens in Lenses)
                sb.Append("<th>").Append(Esc(lens)).Append("</th>");
            sb.Append("</tr></thead>\n<tbody>\n");

            foreach (var row in rows)
            {
                sb.Append("<tr>\n<th class=\"seed\"><span class=\"label\">")
                  .Append(Esc(row.Label)).Append("</span>");
                if (row.Error == null)
                    sb.Append("<span class=\"vitals\">y").Append(row.Year)
                      .Append("<br>").Append(row.Ports).Append(" ports<br>")
                      .Append(row.Lanes).Append(" lanes<br>")
                      .Append(row.Fleets).Append(" fleets</span>");
                sb.Append("</th>\n");

                if (row.Error != null)
                {
                    // Keep the row: a missing world is itself a finding, and a
                    // silently dropped row reads as "only five seeds exist".
                    sb.Append("<td class=\"failed\" colspan=\"")
                      .Append(Lenses.Length)
                      .Append("\"><strong>LOAD FAILED</strong><br>")
                      .Append(Esc(row.Error)).Append("</td>\n</tr>\n");
                    continue;
                }
                foreach (string lens in Lenses)
                {
                    string file = $"{row.Label}-{lens}.png";
                    sb.Append("<td><img src=\"").Append(Esc(file))
                      .Append("\" alt=\"").Append(Esc($"{row.Label} {lens}"))
                      .Append("\" loading=\"lazy\"></td>\n");
                }
                sb.Append("</tr>\n");
            }

            sb.Append("</tbody>\n</table>\n</div>\n")
              .Append("<div id=\"box\"><img id=\"boximg\" alt=\"\"></div>\n")
              .Append("<script>\n").Append(Js).Append("</script>\n")
              .Append("</body>\n</html>\n");
            return sb.ToString();
        }

        private static string Meta(string key, string value) =>
            $"<dt>{Esc(key)}</dt><dd>{Esc(value)}</dd>";

        private static string Esc(string s) => s == null ? "" : s
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;");

        private const string Css = @"
:root { color-scheme: dark; }
* { box-sizing: border-box; }
body {
  margin: 0; padding: 20px 24px 40px;
  background: #0a0e17; color: #c9d1e0;
  font: 13px/1.5 ui-monospace, 'Cascadia Mono', Consolas, monospace;
  overflow-x: hidden;
}
h1 { margin: 0 0 6px; font-size: 17px; letter-spacing: .12em;
     text-transform: uppercase; color: #e8edf7; font-weight: 600; }
header { margin-bottom: 18px; }
dl { display: flex; flex-wrap: wrap; gap: 4px 18px; margin: 0; font-size: 11px; }
dt { color: #5c6780; text-transform: uppercase; letter-spacing: .08em; }
dt::after { content: ':'; }
dd { margin: 0 12px 0 0; color: #8d99b3; }
.scroll { overflow: auto; max-height: calc(100vh - 120px);
          border: 1px solid #1c2434; border-radius: 4px; background: #070a11; }
table { border-collapse: separate; border-spacing: 0; }
thead th {
  position: sticky; top: 0; z-index: 3;
  background: #111827; color: #9fb0d0; font-weight: 600;
  text-transform: uppercase; letter-spacing: .1em; font-size: 11px;
  padding: 8px 10px; text-align: left; border-bottom: 1px solid #1c2434;
}
thead th.seedhead { left: 0; z-index: 4; }
th.seed {
  position: sticky; left: 0; z-index: 2;
  background: #111827; border-right: 1px solid #1c2434;
  border-bottom: 1px solid #1c2434;
  padding: 8px 12px; text-align: left; vertical-align: top;
  white-space: nowrap; font-weight: 400;
}
.label { display: block; color: #e8edf7; font-weight: 600; margin-bottom: 4px; }
.vitals { display: block; color: #67738f; font-size: 11px; }
td { padding: 4px; border-bottom: 1px solid #141b28; vertical-align: top; }
td img { display: block; width: 320px; height: auto; cursor: zoom-in;
         border-radius: 2px; background: #0a0e17; }
td img:hover { outline: 1px solid #3d557f; }
td.failed { color: #e0796b; padding: 16px; vertical-align: middle; }
#box { display: none; position: fixed; inset: 0; z-index: 10;
       background: rgba(4, 6, 11, .93); cursor: zoom-out;
       align-items: center; justify-content: center; padding: 16px; }
#box.on { display: flex; }
#box img { max-width: 100%; max-height: 100%; }
";

        private const string Js = @"
var box = document.getElementById('box'), img = document.getElementById('boximg');
document.querySelectorAll('td img').forEach(function (t) {
  t.onclick = function () { img.src = t.src; img.alt = t.alt; box.classList.add('on'); };
});
box.onclick = function () { box.classList.remove('on'); };
document.onkeydown = function (e) { if (e.key === 'Escape') box.classList.remove('on'); };
";
    }
}
