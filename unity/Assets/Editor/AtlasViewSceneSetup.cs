using System.IO;
using System.Linq;
using StarGen.AtlasView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Builds (or rebuilds) the Atlas scene from scratch —
    /// idempotent: every run starts from a fresh empty scene. Perspective
    /// 2.5D camera + the layer stack (starfield, domain field, nature
    /// field, lattice, lanes, ports) + SimHost + AtlasRoot + provisional
    /// HUD.</summary>
    public static class AtlasViewSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Atlas.unity";
        private static readonly Color CameraBackground = new Color32(0x0A, 0x0E, 0x17, 255);

        [MenuItem("StarGen/Setup Atlas Scene")]
        public static void SetupScene() => Build();

        /// <summary>Batchmode twin, for -executeMethod
        /// StarGen.AtlasView.EditorTools.AtlasViewSceneSetup.RunFromCli.</summary>
        public static void RunFromCli() => Build();

        private static void Build()
        {
            // Never silently discard someone's open scene (batchmode always
            // proceeds; the dialog only appears for a human).
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                    NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            var cam = cameraGo.AddComponent<Camera>();
            cam.backgroundColor = CameraBackground;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.tag = "MainCamera";
            var rig = cameraGo.AddComponent<CameraRig>();
            rig.Configure(cam);

            var stars = new GameObject("Starfield").AddComponent<StarfieldLayer>();
            var domains = new GameObject("DomainField").AddComponent<DomainFieldLayer>();
            var nature = new GameObject("NatureField").AddComponent<NatureFieldLayer>();
            var lattice = new GameObject("Lattice").AddComponent<LatticeLayer>();
            var lanes = new GameObject("LaneLayer").AddComponent<LaneLayer>();
            var ports = new GameObject("PortLayer").AddComponent<PortLayer>();

            var atlasGo = new GameObject("Atlas");
            var host = atlasGo.AddComponent<SimHost>();
            var root = atlasGo.AddComponent<AtlasRoot>();
            root.Wire(host, stars, domains, nature, lattice, lanes, ports, rig);
            var hud = atlasGo.AddComponent<AtlasHud>();
            hud.Wire(root);

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

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
