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
            var service = new GalaxyService(42, 3);
            service.Build();
            return service;
        }

        private static string AllText(VisualElement root) =>
            string.Join("\n", root.Query<Label>().ToList().Select(l => l.text));

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
            var text = AllText(panel);
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
            var text = AllText(panel);
            StringAssert.Contains("no system", text);
            StringAssert.Contains("0.42", text);
            StringAssert.Contains(Core.Naming.Designation.For(empty.Value), text);
        }
    }
}
