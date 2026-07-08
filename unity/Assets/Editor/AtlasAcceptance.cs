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
            // Scoped to the HUD row: Foldout headers elsewhere in the tree are
            // also Toggles, so an unscoped query would index the wrong controls.
            var toggles = UiRoot().Q("layer-toggle-row").Query<Toggle>().ToList();
            toggles[index].value = true;
            Debug.Log($"[Acceptance] layer -> {toggles[index].label}");
        }

        [MenuItem("StarGen/Acceptance/Back")]
        public static void Back() => Controller().Navigator.Back();

        [MenuItem("StarGen/Acceptance/Open System")]
        public static void OpenSystem()
        {
            var controller = Controller();
            controller.Navigator.EnterSystem();
            Debug.Log($"[Acceptance] entered system screen: hex={controller.Navigator.SelectedHex}");
        }

        [MenuItem("StarGen/Acceptance/Select Binary Or Trinary Hex")]
        public static void SelectMultiStar()
        {
            var controller = Controller();
            var cellCoord = controller.Navigator.SelectedCell
                ?? throw new System.InvalidOperationException("not in cell view");
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cellCoord), HexGrid.CellRadius))
            {
                var system = controller.Service!.Generate(hex).System;
                if (system != null && system.Stars.Count > 1)
                {
                    controller.Navigator.SelectHex(hex);
                    Debug.Log($"[Acceptance] selected {system.Arrangement} system "
                        + $"{system.Designation} at {hex}");
                    return;
                }
            }
            Debug.LogWarning($"[Acceptance] no multi-star system in cell {cellCoord}");
        }

        [MenuItem("StarGen/Acceptance/Dump System Layout")]
        public static void DumpSystemLayout()
        {
            var controller = Controller();
            var hex = controller.Navigator.SelectedHex
                ?? throw new System.InvalidOperationException("no hex selected");
            var system = controller.Service!.Generate(hex).System
                ?? throw new System.InvalidOperationException("selected hex has no system");
            var layout = OrbitLayout.Compute(system);
            Debug.Log($"[Acceptance] layout: stars={layout.Stars.Count} rings={layout.Rings.Count}"
                + $" bodies={layout.Bodies.Count} picks={layout.Picks.Count} bounds={layout.Bounds}");
        }
    }
}
