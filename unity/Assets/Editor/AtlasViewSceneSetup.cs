using System.IO;
using System.Linq;
using StarGen.AtlasView;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StarGen.AtlasView.EditorTools
{
    /// <summary>Builds (or rebuilds) the Atlas scene from scratch — the
    /// PoC's idempotent pattern: every run starts from a fresh empty scene,
    /// so re-running never leaves duplicates. Camera + CameraRig + SimHost
    /// + MapSurface + LaneLayer + PortLayer + AtlasRoot + provisional HUD.</summary>
    public static class AtlasViewSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Atlas.unity";
        private static readonly Color CameraBackground = new Color32(0x0A, 0x0A, 0x0E, 255);

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
            cam.orthographic = true;
            cam.backgroundColor = CameraBackground;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var rig = cameraGo.AddComponent<CameraRig>();
            rig.Configure(cam);

            var mapGo = new GameObject("MapSurface");
            var map = mapGo.AddComponent<MapSurface>();

            var laneGo = new GameObject("LaneLayer");
            var lanes = laneGo.AddComponent<LaneLayer>();

            var portGo = new GameObject("PortLayer");
            var ports = portGo.AddComponent<PortLayer>();

            var atlasGo = new GameObject("Atlas");
            var host = atlasGo.AddComponent<SimHost>();
            var root = atlasGo.AddComponent<AtlasRoot>();
            root.Wire(host, map, lanes, ports, rig);
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
