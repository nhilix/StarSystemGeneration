using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarGen.AtlasView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>The Atlas scene's object graph — perspective 2.5D camera + the
    /// K2 layer stack (starfield, domain field, nature field, price field,
    /// lattice, lanes, ports, fleets, POIs, works, plague, war, news) + SimHost
    /// + AtlasRoot + SystemStage + the UI Toolkit lens rail.
    ///
    /// Two entry points, and the difference is the whole point of this class:
    ///
    /// <list type="bullet">
    /// <item><b>SetupScene / RunFromCli — deliberate rebuild, SAVES.</b>
    /// Discards the open scene, constructs the graph fresh, writes
    /// Assets/Scenes/Atlas.unity and registers it in EditorBuildSettings. This
    /// is the slice-end regeneration path: run it when the graph actually
    /// changed, then commit the scene.</item>
    /// <item><b>EnsureScene — capture path, NEVER saves.</b> Guarantees a
    /// complete scene is open in memory and touches neither the asset nor the
    /// build settings.</item>
    /// </list>
    ///
    /// Captures use EnsureScene because Unity's serializer hands out fresh
    /// fileIDs on every construction: saving after each run rewrote ~650 lines
    /// of the scene asset that were semantically identical, so every smoke or
    /// grid run left the working tree dirty and had to be hand-reverted. A
    /// capture never needed the scene ON DISK — only OPEN and complete.</summary>
    public static class AtlasViewSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Atlas.unity";
        private const string PanelSettingsPath = "Assets/Atlas/PanelSettings.asset";
        private static readonly Color CameraBackground = new Color32(0x0A, 0x0E, 0x17, 255);

        /// <summary>Everything the capture path (AtlasSmoke, AtlasGrid) resolves
        /// with FindAnyObjectByType. One list, right beside the builder, so the
        /// completeness check can't drift from what Build actually creates — a
        /// type missing here becomes a null-reference mid-capture instead of a
        /// rebuild.</summary>
        private static readonly Type[] RequiredComponents =
        {
            typeof(SimHost), typeof(AtlasRoot), typeof(SelectionModel),
            typeof(CameraRig), typeof(SystemStage),
            typeof(StarfieldLayer), typeof(DomainFieldLayer),
            typeof(DomainInteriorLayer), typeof(OutpostLayer),
            typeof(NatureFieldLayer), typeof(PriceFieldLayer),
            typeof(LatticeLayer), typeof(LaneLayer), typeof(PortLayer),
            typeof(FleetLayer), typeof(PoiLayer), typeof(WorksLayer),
            typeof(FlowTrailLayer), typeof(CrawlPathLayer),
            typeof(PlagueLayer), typeof(WarLayer), typeof(NewsLayer),
        };

        [MenuItem("StarGen/Setup Atlas Scene")]
        public static void SetupScene() => Rebuild();

        /// <summary>Batchmode twin, for -executeMethod
        /// StarGen.AtlasView.EditorTools.AtlasViewSceneSetup.RunFromCli.</summary>
        public static void RunFromCli() => Rebuild();

        /// <summary>Makes sure a complete atlas scene is open, WITHOUT writing
        /// anything: no SaveScene, no EditorBuildSettings. Three steps, cheapest
        /// first — already complete, else open the committed scene, else build in
        /// memory and say loudly that the commit is stale.</summary>
        public static void EnsureScene()
        {
            var missing = MissingComponents();
            if (missing.Count == 0) return;

            // BOTH remaining paths replace the open scene (OpenScene below,
            // NewScene at the bottom), so honour unsaved work the same way the
            // destructive path does — the in-memory fallback discarded it
            // silently while this guard lived inside the open branch.
            // Reachable only when the open scene is BOTH incomplete AND dirty
            // (a complete scene returned above), so an automated capture loop
            // never sees the dialog — and a human mid-edit is never silently
            // overwritten.
            if (SceneManager.GetActiveScene().isDirty
                && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new InvalidOperationException(
                    "AtlasViewSceneSetup: need the atlas scene open, but the "
                    + "current scene has unsaved changes and the save was "
                    + "cancelled. Save or discard, then re-run.");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                missing = MissingComponents();
                if (missing.Count == 0) return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildGraph();
            // The committed scene can't serve this capture. Not fatal — the
            // in-memory graph is the same one Build() saves — but it means
            // Assets/Scenes/Atlas.unity is out of date and wants a deliberate
            // "StarGen/Setup Atlas Scene" (which saves) plus a commit.
            Debug.Log("AtlasViewSceneSetup: rebuilt the atlas scene IN MEMORY "
                + $"(not saved) — {ScenePath} was missing or stale. Absent: "
                + $"{string.Join(", ", missing)}. Regenerate it deliberately with "
                + "StarGen/Setup Atlas Scene and commit the result.");
        }

        /// <summary>Which required components the open scene lacks, empty when it
        /// is capture-ready. Uses FindAnyObjectByType's default (active objects
        /// only) deliberately: that is exactly what AtlasSmoke/AtlasGrid do, so a
        /// component that exists but is disabled must count as missing here too.</summary>
        private static List<string> MissingComponents()
        {
            var missing = new List<string>();
            foreach (var type in RequiredComponents)
                if (UnityEngine.Object.FindAnyObjectByType(type) == null)
                    missing.Add(type.Name);
            return missing;
        }

        /// <summary>Deliberate regeneration: fresh scene, fresh graph, saved to
        /// disk and registered for builds. The only path that writes.</summary>
        private static void Rebuild()
        {
            // Never silently discard someone's open scene (batchmode always
            // proceeds; the dialog only appears for a human).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                    NewSceneMode.Single);
            BuildGraph();

            EditorSceneManager.MarkSceneDirty(scene);
            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (sceneDir != null && !AssetDatabase.IsValidFolder(sceneDir))
                Directory.CreateDirectory(sceneDir);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"AtlasViewSceneSetup: failed to save {ScenePath}");
                return;
            }
            AssetDatabase.Refresh();
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"AtlasViewSceneSetup: scene constructed at {ScenePath}");
        }

        /// <summary>Constructs the whole object graph into the ACTIVE scene.
        /// Shared by both entry points — saving is the caller's business, never
        /// this method's.</summary>
        private static void BuildGraph()
        {
            var cameraGo = new GameObject("Main Camera");
            var cam = cameraGo.AddComponent<Camera>();
            cam.backgroundColor = CameraBackground;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.tag = "MainCamera";
            var rig = cameraGo.AddComponent<CameraRig>();
            rig.Configure(cam);

            var stars = new GameObject("Starfield").AddComponent<StarfieldLayer>();
            var domains = new GameObject("DomainField").AddComponent<DomainFieldLayer>();
            var interior = new GameObject("DomainInterior").AddComponent<DomainInteriorLayer>();
            var outposts = new GameObject("OutpostLayer").AddComponent<OutpostLayer>();
            var nature = new GameObject("NatureField").AddComponent<NatureFieldLayer>();
            var price = new GameObject("PriceField").AddComponent<PriceFieldLayer>();
            var lattice = new GameObject("Lattice").AddComponent<LatticeLayer>();
            var lanes = new GameObject("LaneLayer").AddComponent<LaneLayer>();
            var ports = new GameObject("PortLayer").AddComponent<PortLayer>();
            var fleets = new GameObject("FleetLayer").AddComponent<FleetLayer>();
            var pois = new GameObject("PoiLayer").AddComponent<PoiLayer>();
            var works = new GameObject("WorksLayer").AddComponent<WorksLayer>();
            var flowTrails = new GameObject("FlowTrailLayer")
                .AddComponent<FlowTrailLayer>();
            var crawlPaths = new GameObject("CrawlPathLayer")
                .AddComponent<CrawlPathLayer>();
            var plague = new GameObject("PlagueLayer").AddComponent<PlagueLayer>();
            var war = new GameObject("WarLayer").AddComponent<WarLayer>();
            var news = new GameObject("NewsLayer").AddComponent<NewsLayer>();

            var atlasGo = new GameObject("Atlas");
            var host = atlasGo.AddComponent<SimHost>();
            var root = atlasGo.AddComponent<AtlasRoot>();
            root.Wire(host, stars, domains, interior, outposts, nature, lattice,
                      lanes, ports, rig, fleets, pois, works, plague, war, news,
                      price, flowTrails, crawlPaths);

            var selection = atlasGo.AddComponent<SelectionModel>();
            selection.Wire(root);

            // K5: the orbit-view scene fragment at System LOD
            var stage = new GameObject("SystemStage").AddComponent<SystemStage>();
            stage.Wire(root);
            selection.WireStage(stage);

            var chromeGo = new GameObject("AtlasChrome");
            var doc = chromeGo.AddComponent<UIDocument>();
            doc.panelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (doc.panelSettings == null)
                Debug.LogWarning(
                    $"AtlasViewSceneSetup: no PanelSettings at {PanelSettingsPath}");
            chromeGo.AddComponent<AtlasChrome>();
            var rail = chromeGo.AddComponent<LensRail>();
            rail.Wire(root);
            var dock = chromeGo.AddComponent<InspectorDock>();
            dock.Wire(root, selection);
            var topBar = chromeGo.AddComponent<TopBar>();
            topBar.Wire(root, dock);
            var tooltip = chromeGo.AddComponent<HexTooltip>();
            tooltip.Wire(selection);
            var legend = chromeGo.AddComponent<LegendPanel>();
            legend.Wire(rail);
            var strip = chromeGo.AddComponent<TimelineStrip>();
            strip.Wire(root);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
