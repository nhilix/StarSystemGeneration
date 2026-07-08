using System.Linq;
using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using UnityEngine.UIElements;

namespace StarGen.Atlas.Tests
{
    public class SystemPanelBuilderTests
    {
        private static GalaxyService Built()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            service.Build();
            return service;
        }

        private static string AllText(VisualElement root) =>
            string.Join("\n", root.Query<Label>().ToList().Select(l => l.text));

        private static HexResult TrinaryResult() =>
            new HexResult(new HexCoordinate(0, 0), TestSystems.BuildTrinary());

        private static int TintedCount(VisualElement root) =>
            root.Query<Label>().ToList()
                .Count(l => l.style.backgroundColor == new StyleColor(SystemPanel.HighlightBg));

        [Test]
        public void HomeworldSystem_PanelShowsNameSocietyAndOrbits()
        {
            var service = Built();
            Anchor? homeworld = null;
            foreach (var cell in service.Skeleton.Cells)
                foreach (var anchor in cell.Anchors)
                    if (anchor.Type == AnchorType.Homeworld) { homeworld = anchor; break; }
            Assert.IsNotNull(homeworld, "fixture galaxy must contain a homeworld");
            var result = service.Generate(homeworld!.Hex);
            var panel = SystemPanelBuilder.Build(result);
            var text = AllText(panel.Root);
            StringAssert.Contains(result.System!.GivenName, text);
            StringAssert.Contains(result.System.Designation, text);
            StringAssert.Contains("pop tier", text);
            StringAssert.Contains("Star A", text);
        }

        [Test]
        public void EmptyHex_PanelShowsDesignationAndDensity()
        {
            var service = Built();
            HexCoordinate? empty = null;
            foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 12))
                if (service.StateOf(hex) == HexState.Empty) { empty = hex; break; }
            Assert.IsNotNull(empty, "fixture galaxy must contain an empty in-galaxy hex");
            var result = service.Generate(empty!.Value);
            var panel = SystemPanelBuilder.Build(result, density: 0.42);
            var text = AllText(panel.Root);
            StringAssert.Contains("no system", text);
            StringAssert.Contains("0.42", text);
            StringAssert.Contains(Core.Naming.Designation.For(empty.Value), text);
        }

        [Test]
        public void Rows_RegisterForStarsBodiesAndMoons_NotEmptySlots()
        {
            var panel = SystemPanelBuilder.Build(TrinaryResult());
            Assert.IsTrue(panel.HasRow(new BodyRef(0, -1, -1)));   // primary header
            Assert.IsTrue(panel.HasRow(new BodyRef(1, -1, -1)));   // companion header
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 1, -1)));    // rocky world
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 2, -1)));    // belt
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 3, 0)));     // first moon
            Assert.IsTrue(panel.HasRow(new BodyRef(0, 3, 1)));     // second moon
            Assert.IsTrue(panel.HasRow(new BodyRef(1, 1, -1)));    // companion's ice world
            Assert.IsFalse(panel.HasRow(new BodyRef(0, 0, -1)));   // empty slot
            Assert.IsFalse(panel.HasRow(new BodyRef(0, 7, -1)));   // empty slot
        }

        [Test]
        public void OpenSystemButton_PresentOnlyWithCallback()
        {
            bool clicked = false;
            var withButton = SystemPanelBuilder.Build(TrinaryResult(),
                onOpenSystem: () => clicked = true);
            var button = withButton.Root.Q<Button>();
            Assert.IsNotNull(button);
            Assert.AreEqual("Open system", button.text);

            var withoutButton = SystemPanelBuilder.Build(TrinaryResult());
            Assert.IsNull(withoutButton.Root.Q<Button>());
            Assert.IsFalse(clicked);
        }

        [Test]
        public void Highlight_TintsExactlyOneRow_MovesAndClears()
        {
            var panel = SystemPanelBuilder.Build(TrinaryResult());
            Assert.AreEqual(0, TintedCount(panel.Root));
            panel.Highlight(new BodyRef(0, 1, -1));
            Assert.AreEqual(1, TintedCount(panel.Root));
            panel.Highlight(new BodyRef(0, 3, 0));
            Assert.AreEqual(1, TintedCount(panel.Root));   // previous row cleared
            panel.Highlight(null);
            Assert.AreEqual(0, TintedCount(panel.Root));
        }
    }
}
