using StarGen.Unity;
using UnityEditor;
using UnityEngine;

namespace StarGen.UnityEditorTools
{
    /// <summary>
    /// Edit-mode driver for the integration spike: creates (or reuses) the
    /// GalaxyMap object and runs GalaxyMapSpike.BuildAndPaint without play mode.
    /// Invoked by hand from the menu or programmatically via ExecuteMenuItem.
    /// </summary>
    public static class StarGenSpikeMenu
    {
        [MenuItem("StarGen/Run Galaxy Spike")]
        public static void RunGalaxySpike()
        {
            var go = GameObject.Find("GalaxyMap");
            if (go == null)
            {
                go = new GameObject("GalaxyMap");
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<GalaxyMapSpike>();
                Undo.RegisterCreatedObjectUndo(go, "Create GalaxyMap");
            }
            go.GetComponent<GalaxyMapSpike>().BuildAndPaint();
            Selection.activeGameObject = go;
        }
    }
}
