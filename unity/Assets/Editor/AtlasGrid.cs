using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StarGen.AtlasView;
// unity/Packages/manifest.json is gitignored, so a clean checkout can
// legitimately have no com.unity.pipeline. This assembly also carries
// AtlasSmoke, the scene setup and the EditMode tests — every Unity gate —
// so it has to compile without the package: only the pipeline face
// disappears, the menu and batchmode faces stay. The symbol comes from
// versionDefines in StarGen.AtlasView.Editor.asmdef.
#if HAS_UNITY_PIPELINE
using Unity.Pipeline.Commands;
#endif
using UnityEditor;
using UnityEngine;
// `using System` makes a bare Object ambiguous; AtlasSmoke never needed it.
using Object = UnityEngine.Object;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Which point the mid-zoom lenses orbit. The grid is read DOWN a
    /// column, so the anchor decides whether two rows are actually comparable.</summary>
    public enum AtlasAnchor
    {
        /// <summary>Mean of every settled port — the heart of the reach.</summary>
        Centroid,
        /// <summary>Centre of the disc bounds, settled or not.</summary>
        Bounds,
        /// <summary>A specific port index (AtlasSmoke's framing).</summary>
        Port,
    }

    /// <summary>One investigation's worth of grid: which worlds, which lenses,
    /// how framed. Every field carries the shipped default, so an options object
    /// straight out of `new` reproduces the menu item's sheet exactly.</summary>
    public sealed class AtlasGridOptions
    {
        public string InputDir = AtlasGrid.DefaultInputDir;
        public string OutputDir = AtlasGrid.DefaultOutputDir;
        /// <summary>Lens names, in the order they are shot AND columned.</summary>
        public string[] Lenses = AtlasGrid.AllLenses;
        /// <summary>Artifact-label filter; null/empty means every artifact.</summary>
        public string[] Seeds;
        public int Width = AtlasGrid.DefaultWidth;
        public int Height = AtlasGrid.DefaultHeight;
        /// <summary>Mid-zoom distance as a fraction of the galaxy extent.</summary>
        public float Zoom = AtlasGrid.DefaultZoom;
        /// <summary>Mid-zoom camera pitch, degrees.</summary>
        public float Pitch = AtlasGrid.DefaultPitch;
        public AtlasAnchor Anchor = AtlasAnchor.Centroid;
        public int AnchorPort;
    }

    /// <summary>The eyeball gate widened from one seed to a grid: load every
    /// artifact in the input directory in turn (SimHost.ArtifactPath is the seam —
    /// set it, LoadArtifact(), repeat), shoot the chosen lenses on each, and emit
    /// a self-contained contact sheet at &lt;output&gt;/index.html. Reading *down* a
    /// column is the point — one lens across many worlds tells you whether a look
    /// is the design or the seed.
    ///
    /// Not a fixed sheet: which lenses, which seeds, what framing are all per
    /// investigation. Three ways in, one core — the menu item (defaults),
    /// batchmode -executeMethod StarGen.AtlasView.EditorTools.AtlasGrid.RunFromCli
    /// (defaults, graphics ON), and the `atlas_grid` pipeline command, which is
    /// the parameterized one: `unity command atlas_grid --lenses trade,war
    /// --seeds 42,9091 --zoom 0.5`.</summary>
    public static class AtlasGrid
    {
        // A contact sheet, not an acceptance shot: 36 PNGs at AtlasSmoke's
        // 1600x1000 is a lot of bytes for something read as thumbnails.
        public const int DefaultWidth = 1200, DefaultHeight = 750;
        public const float DefaultZoom = 0.30f, DefaultPitch = 62f;
        public const string DefaultInputDir = "runs/atlas-grid";
        public const string DefaultOutputDir = "atlas-grid";
        public const string DefaultAnchor = "centroid";

        public static readonly string[] AllLenses =
            { "galaxy", "domains", "trade", "price", "war", "works" };

        /// <summary>One row's worth of contact sheet: the shots plus the world's
        /// vitals, or the load error that killed the row.</summary>
        private sealed class SeedRow
        {
            public string Label;
            public string Error;
            public ulong MasterSeed;
            public int Year, Ports, Lanes, Fleets;
        }

        [MenuItem("StarGen/Atlas Grid")]
        public static void RunFromMenu() => RunGuarded(new AtlasGridOptions(), exitOnFailure: false);

        public static void RunFromCli() => RunGuarded(new AtlasGridOptions(), exitOnFailure: true);

        /// <summary>The menu/batchmode face of Run. Those two callers have no
        /// envelope to carry an exception, so a caller error becomes a logged
        /// message plus exit 1. The pipeline face deliberately does NOT catch:
        /// there, a thrown exception is the only thing that turns the CLI
        /// envelope red.</summary>
        private static void RunGuarded(AtlasGridOptions options, bool exitOnFailure)
        {
            try
            {
                Run(options, exitOnFailure);
            }
            catch (ArgumentException e)
            {
                Debug.LogError(e.Message);
                if (exitOnFailure) EditorApplication.Exit(1);
            }
        }

#if HAS_UNITY_PIPELINE
        /// <summary>The pipeline face. Everything is optional and defaults to the
        /// menu item's sheet, so `unity command atlas_grid` with no flags is the
        /// full grid. Args are FLAG-style (`--lenses trade,war`); a key=value form
        /// is silently ignored by the CLI while still reporting success.</summary>
        [CliCommand("atlas_grid",
            "Shoot the atlas contact sheet: every artifact x chosen lenses -> PNGs + a self-contained index.html.")]
        public static object RunFromPipeline(
            [CliArg("input", "Artifact directory, repo-root-relative. Default runs/atlas-grid.")]
            string input = DefaultInputDir,
            [CliArg("output", "Output directory, repo-root-relative. Default atlas-grid.")]
            string output = DefaultOutputDir,
            [CliArg("lenses", "Comma-separated subset/order of galaxy,domains,trade,price,war,works. Omit for all six. Sets column order too.")]
            string lenses = null,
            [CliArg("seeds", "Comma-separated artifact-label filter, e.g. 42,9091 or seed-42. Omit for every artifact found.")]
            string seeds = null,
            [CliArg("width", "Capture width in px. Default 1200.")]
            int width = DefaultWidth,
            [CliArg("height", "Capture height in px. Default 750.")]
            int height = DefaultHeight,
            [CliArg("zoom", "Mid-zoom lens distance as a fraction of the galaxy extent. Default 0.30.")]
            float zoom = DefaultZoom,
            [CliArg("pitch", "Mid-zoom camera pitch in degrees. Default 62.")]
            float pitch = DefaultPitch,
            [CliArg("anchor", "Mid-zoom camera anchor: centroid (mean of ports), bounds (disc centre), or port:<index>. Default centroid.")]
            string anchor = DefaultAnchor)
        {
            var options = new AtlasGridOptions
            {
                InputDir = Blank(input) ? DefaultInputDir : input,
                OutputDir = Blank(output) ? DefaultOutputDir : output,
                Lenses = ParseLenses(lenses),
                Seeds = SplitList(seeds),
                Width = width,
                Height = height,
                Zoom = zoom,
                Pitch = pitch,
            };
            ParseAnchor(anchor, options);
            // Validation throws: the server turns an exception into a
            // success=false envelope, which is the only failure signal a caller
            // can trust (`menu` proves logs and exit codes both lie).
            return Run(options, exitOnFailure: false);
        }
#endif

        /// <summary>Shoots the grid and returns the run's report. Throws
        /// ArgumentException on anything the caller got wrong — always before the
        /// first PNG, so a rejected run never leaves a half-written sheet.</summary>
        public static object Run(AtlasGridOptions options, bool exitOnFailure)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            Validate(options);

            string inputDir = RepoPath(options.InputDir);
            // Throw, don't return {success:false}: a returned failure still
            // leaves the CLI envelope success:true / exit 0, so a typo'd
            // --input would report a clean run that shot nothing.
            if (!Directory.Exists(inputDir))
                throw new ArgumentException($"AtlasGrid: no artifact directory at {inputDir} "
                    + "— generate one with the Inspector REPL (`epoch <seed> 40 21` + `esave`).");

            var found = Directory.GetFiles(inputDir, "*.txt");
            // Ordinal, not culture: the grid's row order must be the same on
            // every machine that regenerates the sheet.
            Array.Sort(found, StringComparer.Ordinal);
            if (found.Length == 0)
                throw new ArgumentException($"AtlasGrid: {inputDir} holds no *.txt artifacts.");

            var artifacts = SelectSeeds(found, options.Seeds);

            string outputDir = RepoPath(options.OutputDir);
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

            cam.aspect = (float)options.Width / options.Height;

            var rows = new List<SeedRow>();
            var failures = new List<string>();
            int pngs = 0;
            foreach (string artifact in artifacts)
            {
                string label = Path.GetFileNameWithoutExtension(artifact);
                var row = new SeedRow { Label = label };
                rows.Add(row);

                host.ArtifactPath = artifact;
                if (!host.LoadArtifact())
                {
                    // One malformed artifact must not cost the other rows —
                    // record it and keep shooting.
                    row.Error = host.LoadError;
                    failures.Add($"{label}: {host.LoadError}");
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
                var heart = ResolveAnchor(options, host, bounds, label);

                void View(Vector3 focus, float distance, float pitch)
                {
                    rig.SetView(focus, distance, pitch);
                    SetAndStyle(rig, options.Height, lanes, lattice,
                                fleets, pois, works, plague, war);
                }

                foreach (string lens in options.Lenses)
                {
                    switch (lens)
                    {
                        case "galaxy":
                            // Top-down and fitted: its own framing by design, so
                            // zoom/pitch/anchor never touch it.
                            View(bounds.center, fit, 90f);
                            break;
                        case "domains":
                            View(heart, extent * 0.7f, 55f);
                            break;
                        default:
                            // The settled reach, glyphs resolved — AtlasSmoke's
                            // lens framing, and the only one the caller tunes.
                            View(heart, extent * options.Zoom, options.Pitch);
                            break;
                    }

                    switch (lens)
                    {
                        case "trade":
                            lanes.SetMode(LaneMode.Trade);
                            break;
                        case "price":
                            price.SetVisible(true);
                            break;
                        case "war":
                            domains.SetAccent(DomainAccent.War);
                            war.SetVisible(true);
                            break;
                        case "works":
                            works.SetVisible(true);
                            flowTrails.SetVisible(true);
                            crawlPaths.SetVisible(true);
                            break;
                    }

                    Capture(cam, outputDir, label, lens, options.Width, options.Height);
                    pngs++;

                    switch (lens)
                    {
                        case "trade":
                            lanes.SetMode(LaneMode.Status);
                            break;
                        case "price":
                            price.SetVisible(false);
                            break;
                        case "war":
                            war.SetVisible(false);
                            domains.SetAccent(DomainAccent.Owner);
                            break;
                        case "works":
                            works.SetVisible(false);
                            flowTrails.SetVisible(false);
                            crawlPaths.SetVisible(false);
                            break;
                    }
                }

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
            File.WriteAllText(indexPath, BuildIndex(rows, inputDir, options), Encoding.UTF8);
            Debug.Log($"AtlasGrid: contact sheet at {indexPath} — "
                + $"{rows.Count} seeds x {options.Lenses.Length} lenses");

            // The sheet is written either way — a partial sheet names which
            // world died, which is the finding. Only the exit code changes, so
            // batchmode doesn't call a page of LOAD FAILED cells a green run.
            if (exitOnFailure) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);

            var shot = new List<string>();
            foreach (var r in rows) if (r.Error == null) shot.Add(r.Label);
            return new
            {
                success = failures.Count == 0,
                seeds = shot.ToArray(),
                lenses = options.Lenses,
                pngCount = pngs,
                outputDir,
                indexPath,
                failures = failures.ToArray(),
                message = $"{shot.Count} seed(s) x {options.Lenses.Length} lens(es) "
                    + $"= {pngs} PNG(s) at {options.Width}x{options.Height} in {outputDir}"
                    + (failures.Count == 0 ? "" : $"; {failures.Count} seed(s) failed to load"),
            };
        }

        // ---- arguments ----

        private static bool Blank(string s) => string.IsNullOrWhiteSpace(s);

        /// <summary>Comma-separated list to array; null/blank means "unset", which
        /// each caller reads as its own default (all lenses, all seeds).</summary>
        private static string[] SplitList(string csv)
        {
            if (Blank(csv)) return null;
            var parts = csv.Split(',');
            var kept = new List<string>();
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) kept.Add(t);
            }
            return kept.Count == 0 ? null : kept.ToArray();
        }

        /// <summary>Splits the lens flag; blank means all six. Pure parsing —
        /// Validate() judges the names, so a hand-built AtlasGridOptions faces
        /// exactly the same rules as the CLI.</summary>
        private static string[] ParseLenses(string csv) => SplitList(csv) ?? AllLenses;

        private static void ParseAnchor(string anchor, AtlasGridOptions options)
        {
            if (Blank(anchor) || anchor == "centroid")
            {
                options.Anchor = AtlasAnchor.Centroid;
                return;
            }
            if (anchor == "bounds")
            {
                options.Anchor = AtlasAnchor.Bounds;
                return;
            }
            if (anchor.StartsWith("port:", StringComparison.Ordinal)
                && int.TryParse(anchor.Substring(5), out int index) && index >= 0)
            {
                options.Anchor = AtlasAnchor.Port;
                options.AnchorPort = index;
                return;
            }
            throw new ArgumentException($"AtlasGrid: unknown anchor '{anchor}'. "
                + "Valid anchors: centroid, bounds, port:<index>.");
        }

        private static void Validate(AtlasGridOptions o)
        {
            if (o.Lenses == null || o.Lenses.Length == 0)
                throw new ArgumentException("AtlasGrid: no lenses selected. "
                    + $"Valid lenses: {string.Join(", ", AllLenses)}.");
            for (int i = 0; i < o.Lenses.Length; i++)
            {
                string n = o.Lenses[i];
                if (Array.IndexOf(AllLenses, n) < 0)
                    // Silently dropping an unknown lens would ship a sheet that
                    // is missing exactly the column the investigation wanted.
                    throw new ArgumentException($"AtlasGrid: unknown lens '{n}'. "
                        + $"Valid lenses: {string.Join(", ", AllLenses)}.");
                if (Array.IndexOf(o.Lenses, n) < i)
                    // A repeat shoots the same PNG twice and columns it twice —
                    // always a typo, never an investigation.
                    throw new ArgumentException($"AtlasGrid: lens '{n}' listed twice. "
                        + "Each lens is one column; name it once.");
            }
            if (o.Width <= 0 || o.Height <= 0)
                throw new ArgumentException(
                    $"AtlasGrid: width and height must be positive (got {o.Width}x{o.Height}).");
            if (o.Zoom <= 0f)
                throw new ArgumentException($"AtlasGrid: zoom must be positive (got {o.Zoom}).");
            if (o.Anchor == AtlasAnchor.Port && o.AnchorPort < 0)
                // ParseAnchor guards the CLI; a hand-built options object
                // otherwise indexes ports[-1] AFTER the first PNG is written.
                throw new ArgumentException(
                    $"AtlasGrid: anchor port index must be >= 0 (got {o.AnchorPort}).");
        }

        /// <summary>Filter artifacts by label. `seed-42` and a bare `42` both hit
        /// `seed-42.txt` — the label carries the prefix, the person typing does
        /// not. A filter that matches nothing is an error naming what IS there,
        /// because the silent alternative is an empty sheet read as "no worlds".</summary>
        private static string[] SelectSeeds(string[] artifacts, string[] wanted)
        {
            if (wanted == null || wanted.Length == 0) return artifacts;

            var kept = new List<string>();
            foreach (string artifact in artifacts)
            {
                string label = Path.GetFileNameWithoutExtension(artifact);
                foreach (string w in wanted)
                    if (LabelMatches(label, w)) { kept.Add(artifact); break; }
            }
            if (kept.Count == 0)
            {
                var labels = new List<string>();
                foreach (string a in artifacts)
                    labels.Add(Path.GetFileNameWithoutExtension(a));
                throw new ArgumentException(
                    $"AtlasGrid: seed filter '{string.Join(",", wanted)}' matched no artifact. "
                    + $"Found: {string.Join(", ", labels)}.");
            }
            return kept.ToArray();
        }

        private static bool LabelMatches(string label, string wanted) =>
            string.Equals(label, wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(label, "seed-" + wanted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Strip(label), Strip(wanted), StringComparison.OrdinalIgnoreCase);

        private static string Strip(string s) =>
            s.StartsWith("seed-", StringComparison.OrdinalIgnoreCase) ? s.Substring(5) : s;

        /// <summary>Where the mid-zoom lenses look. The default is the SETTLED
        /// CENTROID, not Ports[0] the way AtlasSmoke does. Ports[0] is an
        /// arbitrary port per world — on seed 9091 it sits at the tip of a long
        /// tendril, which shoves that whole row's galaxy into a corner. A grid
        /// exists to be read DOWN a column, so each seed has to frame the
        /// comparable thing: the heart of its settled reach. `port:<index>` is
        /// there for when you deliberately want the arbitrary one.</summary>
        private static Vector3 ResolveAnchor(AtlasGridOptions options, SimHost host,
                                             Bounds bounds, string label)
        {
            var ports = host.State.Ports;
            switch (options.Anchor)
            {
                case AtlasAnchor.Bounds:
                    return bounds.center;
                case AtlasAnchor.Port:
                    if (options.AnchorPort < ports.Count)
                        return AtlasGeometry.HexToWorld(ports[options.AnchorPort].Hex);
                    // Worlds differ in port count; a row that can't honour the
                    // anchor still belongs in the sheet, framed on the disc.
                    Debug.LogWarning($"AtlasGrid: {label} has {ports.Count} ports — "
                        + $"port:{options.AnchorPort} is out of range, framing on bounds.");
                    return bounds.center;
                default:
                    if (ports.Count == 0) return bounds.center;
                    var sum = Vector3.zero;
                    foreach (var p in ports) sum += AtlasGeometry.HexToWorld(p.Hex);
                    return sum / ports.Count;
            }
        }

        private static string RepoPath(string relative) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", relative));

        // ---- shooting ----

        private static void SetAndStyle(CameraRig rig, int viewportPx, LaneLayer lanes,
            LatticeLayer lattice,
            FleetLayer fleets, PoiLayer pois, WorksLayer works,
            PlagueLayer plague, WarLayer war)
        {
            // Edit mode: the rig's ZoomChanged fires, but listeners are
            // wired by AtlasRoot.OnEnable which never ran — style by hand
            // (mirror AtlasRoot.OnZoomChanged, crossfade hooks included).
            lanes.SetExtent(rig.GalaxyExtent);
            lanes.ViewportPx = viewportPx;
            lanes.OnZoom(rig.Distance);
            var flowTrails = Object.FindAnyObjectByType<FlowTrailLayer>();
            flowTrails.SetExtent(rig.GalaxyExtent);
            flowTrails.ViewportPx = viewportPx;
            flowTrails.OnZoom(rig.Distance);
            var crawlPaths = Object.FindAnyObjectByType<CrawlPathLayer>();
            crawlPaths.SetExtent(rig.GalaxyExtent);
            crawlPaths.ViewportPx = viewportPx;
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

        private static void Capture(Camera cam, string outputDir, string label,
                                    string lens, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            Shader.SetGlobalFloat("_AtlasFocalY",
                1f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            Shader.SetGlobalFloat("_AtlasViewportPx", height);
            cam.Render();
            RenderTexture.active = rt;
            var shot = new Texture2D(width, height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
        /// the whole output folder is the artifact and moves as one. The header
        /// records the run's parameters, so a saved sheet says how it was made.</summary>
        private static string BuildIndex(List<SeedRow> rows, string inputDir,
                                         AtlasGridOptions options)
        {
            var lenses = options.Lenses;
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
              .Append(Meta("grid", $"{rows.Count} seeds × {lenses.Length} lenses"))
              .Append(Meta("capture", $"{options.Width} × {options.Height}"))
              .Append(Meta("lenses", string.Join(",", lenses)))
              .Append(Meta("framing", $"zoom {options.Zoom:0.##} · pitch {options.Pitch:0.#}° "
                    + $"· anchor {AnchorText(options)}"))
              .Append("</dl>\n</header>\n");

            sb.Append("<div class=\"scroll\">\n<table>\n<thead><tr>")
              .Append("<th class=\"seedhead\">seed</th>");
            foreach (string lens in lenses)
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
                    // silently dropped row reads as "that seed doesn't exist".
                    sb.Append("<td class=\"failed\" colspan=\"")
                      .Append(lenses.Length)
                      .Append("\"><strong>LOAD FAILED</strong><br>")
                      .Append(Esc(row.Error)).Append("</td>\n</tr>\n");
                    continue;
                }
                foreach (string lens in lenses)
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

        private static string AnchorText(AtlasGridOptions o) =>
            o.Anchor == AtlasAnchor.Port ? $"port:{o.AnchorPort}"
            : o.Anchor == AtlasAnchor.Bounds ? "bounds" : "centroid";

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
