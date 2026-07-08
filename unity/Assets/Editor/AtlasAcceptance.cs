using System.Linq;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.UnityEditorTools
{
    /// <summary>
    /// Acceptance driver for the atlas (plan Task 9): menu items the MCP bridge can
    /// trigger to drive the running app through its real code paths in play mode.
    /// Map clicks are the only input we can't fake, so cell/hex selection goes
    /// through AtlasController.Navigator — everything downstream (Render, panels,
    /// camera fit) is the production path.
    /// </summary>
    public static class AtlasAcceptance
    {
        private static AtlasController Controller() =>
            Object.FindFirstObjectByType<AtlasController>()
            ?? throw new System.InvalidOperationException("no AtlasController in scene");

        private static VisualElement UiRoot() =>
            Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        [MenuItem("StarGen/Acceptance/Generate Seed 42")]
        public static void Generate()
        {
            var root = UiRoot();
            root.Query<TextField>().First().value = "42";
            root.Query<IntegerField>().First().value = 21;
            var button = root.Query<Button>().Where(b => b.text == "Generate").First();
            using (var evt = new NavigationSubmitEvent { target = button })
                button.SendEvent(evt);
            Debug.Log($"[Acceptance] generated: screen={Controller().Navigator.Screen}, " +
                      $"cells={Controller().Service?.Skeleton.Cells.Count}, " +
                      $"buildMs={Controller().Service?.BuildMilliseconds}");
        }

        [MenuItem("StarGen/Acceptance/Drill To First Capital")]
        public static void DrillCapital()
        {
            var controller = Controller();
            var polity = controller.Service!.Skeleton.Polities.First(p => !p.Extinct);
            controller.Navigator.DrillToCell(polity.CapitalCoord);
            Debug.Log($"[Acceptance] drilled to capital of {polity.Name} at {polity.CapitalCoord}");
        }

        [MenuItem("StarGen/Acceptance/Select Settled Hex")]
        public static void SelectSettled() => SelectByState(HexState.Settled);

        [MenuItem("StarGen/Acceptance/Select Anchored Hex")]
        public static void SelectAnchored() => SelectByState(HexState.Anchored);

        [MenuItem("StarGen/Acceptance/Select Empty Hex")]
        public static void SelectEmpty() => SelectByState(HexState.Empty);

        private static void SelectByState(HexState wanted)
        {
            var controller = Controller();
            var cellCoord = controller.Navigator.SelectedCell
                ?? throw new System.InvalidOperationException("not in cell view");
            var center = HexGrid.CellCenter(cellCoord);
            foreach (var hex in HexGrid.Spiral(center, HexGrid.CellRadius))
                if (controller.Service!.StateOf(hex) == wanted)
                {
                    controller.Navigator.SelectHex(hex);
                    Debug.Log($"[Acceptance] selected {wanted} hex {hex}");
                    return;
                }
            Debug.LogWarning($"[Acceptance] no {wanted} hex in cell {cellCoord}");
        }

        [MenuItem("StarGen/Acceptance/Set Layer Density")]
        public static void LayerDensity() => SetLayer(0);
        [MenuItem("StarGen/Acceptance/Set Layer Polity")]
        public static void LayerPolity() => SetLayer(1);
        [MenuItem("StarGen/Acceptance/Set Layer Zone")]
        public static void LayerZone() => SetLayer(2);
        [MenuItem("StarGen/Acceptance/Set Layer Dev")]
        public static void LayerDev() => SetLayer(3);
        [MenuItem("StarGen/Acceptance/Set Layer Lean")]
        public static void LayerLean() => SetLayer(4);

        private static void SetLayer(int index)
        {
            var toggles = UiRoot().Query<Toggle>().ToList();
            toggles[index].value = true;
            Debug.Log($"[Acceptance] layer -> {toggles[index].label}");
        }

        [MenuItem("StarGen/Acceptance/Back")]
        public static void Back() => Controller().Navigator.Back();
    }
}
