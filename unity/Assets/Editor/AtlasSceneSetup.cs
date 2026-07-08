using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas.EditorTools
{
    /// <summary>Builds (or rebuilds) the Atlas scene from scratch (atlas spec §7):
    /// camera + GalaxyView + CellView + AtlasUI (UIDocument) + AtlasController, all
    /// wired via serialized fields. Idempotent — every run starts from a fresh empty
    /// scene, so re-running never leaves duplicate objects behind.</summary>
    public static class AtlasSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Atlas.unity";
        private const string PanelSettingsFolder = "Assets/Atlas";
        private const string PanelSettingsPath = PanelSettingsFolder + "/PanelSettings.asset";
        private const string FallbackThemePath = PanelSettingsFolder + "/AtlasTheme.tss";

        private static readonly Color CameraBackground = new Color32(0x0A, 0x0A, 0x0E, 255);

        [MenuItem("StarGen/Setup Atlas Scene")]
        public static void SetupScene() => Build();

        /// <summary>Batchmode twin of the menu item, for
        /// -executeMethod StarGen.Atlas.EditorTools.AtlasSceneSetup.RunFromCli.</summary>
        public static void RunFromCli() => Build();

        private static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            var cam = cameraGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = CameraBackground;
            cam.cullingMask = ~0;
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var galaxyGo = new GameObject("GalaxyView");
            var galaxyView = galaxyGo.AddComponent<GalaxyView>();

            var cellGo = new GameObject("CellView");
            var cellView = cellGo.AddComponent<CellView>();

            var uiGo = new GameObject("AtlasUI");
            var uiDocument = uiGo.AddComponent<UIDocument>();
            uiDocument.panelSettings = LoadOrCreatePanelSettings();
            var atlasUi = uiGo.AddComponent<AtlasUI>();

            var atlasGo = new GameObject("Atlas");
            var controller = atlasGo.AddComponent<AtlasController>();
            AssignControllerRefs(controller, galaxyView, cellView, atlasUi, cam);

            EditorSceneManager.MarkSceneDirty(scene);

            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (sceneDir != null && !AssetDatabase.IsValidFolder(sceneDir))
                Directory.CreateDirectory(sceneDir);

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError($"AtlasSceneSetup: failed to save scene to {ScenePath}");
                return;
            }

            AssetDatabase.Refresh();
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"AtlasSceneSetup: scene constructed at {ScenePath}");
        }

        private static void AssignControllerRefs(
            AtlasController controller, GalaxyView galaxyView, CellView cellView, AtlasUI ui, Camera cam)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("galaxyView").objectReferenceValue = galaxyView;
            so.FindProperty("cellView").objectReferenceValue = cellView;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PanelSettings LoadOrCreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(PanelSettingsFolder))
                AssetDatabase.CreateFolder("Assets", "Atlas");

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = LoadOrCreateThemeStyleSheet();

            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return panelSettings;
        }

        private static ThemeStyleSheet LoadOrCreateThemeStyleSheet()
        {
            var themeGuids = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (themeGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(themeGuids[0]);
                var found = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (found != null) return found;
            }

            var existingFallback = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(FallbackThemePath);
            if (existingFallback != null) return existingFallback;

            var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            AssetDatabase.CreateAsset(theme, FallbackThemePath);
            Debug.Log("AtlasSceneSetup: no project ThemeStyleSheet asset found; created an "
                + $"empty fallback at {FallbackThemePath}. AtlasUI carries its entire design "
                + "via inline styles, so the empty theme only satisfies UIDocument's "
                + "PanelSettings requirement.");
            return theme;
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
