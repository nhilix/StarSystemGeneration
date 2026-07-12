using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.MenuView.EditorTools
{
    /// <summary>Builds the main-menu scene, its PanelSettings, and the
    /// scanline tile programmatically — no hand-authored GUID plumbing and
    /// no binaries in git. Menu: SSG → UI → Create Main Menu Scene.</summary>
    public static class MainMenuSceneBuilder
    {
        private const string UiDir = "Assets/UI/MainMenu";
        private const string ScanlinePath = UiDir + "/scanline.png";
        private const string PanelSettingsPath = UiDir + "/MainMenuPanelSettings.asset";
        private const string UxmlPath = UiDir + "/MainMenu.uxml";
        private const string ThemePath = "Assets/UI/Themes/SSG-Ice.tss";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("SSG/UI/Create Main Menu Scene")]
        public static void Build()
        {
            EnsureScanlineTexture();
            var panel = EnsurePanelSettings();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.tag = "MainCamera";

            var uiGo = new GameObject("MainMenuUI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = tree;
            uiGo.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[SSG] Main menu scene saved to {ScenePath} — enter play mode to eyeball.");
        }

        private static void EnsureScanlineTexture()
        {
            bool created = !File.Exists(ScanlinePath);
            if (created)
            {
                var tex = new Texture2D(1, 3, TextureFormat.RGBA32, false);
                tex.SetPixels(new[]
                {
                    new Color(0f, 0f, 0f, 0.30f), // the dark line of the 1×3 tile
                    new Color(0f, 0f, 0f, 0f),
                    new Color(0f, 0f, 0f, 0f),
                });
                tex.Apply();
                File.WriteAllBytes(ScanlinePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(ScanlinePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(ScanlinePath);
            // Default, not the project's Sprite default: a tiling texture
            // (alphaIsTransparency dilation garbles a 1×3 tile)
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            // On a fresh checkout MainMenu.uss imports BEFORE this texture
            // exists, so the compiled stylesheet holds a broken url() and
            // UI Toolkit tiles its missing-image placeholder (the yellow
            // screen-door of the K3 eyeball). Reimporting rebinds it.
            if (created)
                AssetDatabase.ImportAsset(UiDir + "/MainMenu.uss",
                                          ImportAssetOptions.ForceUpdate);
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return panel;
        }
    }
}
