using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class GalaxyServiceTests
    {
        private static GalaxyService Built()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            service.Build();
            return service;
        }

        [Test]
        public void Build_ProducesSkeletonAndContext_AndTimesIt()
        {
            var service = Built();
            Assert.AreEqual(37, service.Skeleton.Cells.Count);   // radius 3 = 3*3*4+1
            Assert.IsNotNull(service.Context.Skeleton);
            Assert.GreaterOrEqual(service.BuildMilliseconds, 0);
        }

        [Test]
        public void Skeleton_BeforeBuild_Throws()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            Assert.Throws<System.InvalidOperationException>(() => _ = service.Skeleton);
        }

        [Test]
        public void Generate_IsDeterministic_AndStateOfIsConsistent()
        {
            var service = Built();
            var center = new HexCoordinate(0, 0);
            Assert.AreEqual(service.Generate(center).IsEmpty, service.Generate(center).IsEmpty);
            // far outside the radius-3 galaxy: Void
            Assert.AreEqual(HexState.Void, service.StateOf(new HexCoordinate(400, 0)));
            // every homeworld anchor hex reports Anchored
            foreach (var cell in service.Skeleton.Cells)
                foreach (var anchor in cell.Anchors)
                    Assert.AreEqual(HexState.Anchored, service.StateOf(anchor.Hex));
        }

        [Test]
        public void CellSummary_NamesOwnerAndLean()
        {
            var service = Built();
            var polity = service.Skeleton.Polities[0];
            var capital = service.Skeleton.CellAt(polity.CapitalCoord);
            var summary = service.CellSummary(capital);
            StringAssert.Contains(polity.Name, summary);
            StringAssert.Contains(capital.Lean.ToString(), summary);
        }

        [Test]
        public void ConfigCtor_Builds37Cells()
        {
            var service = new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });
            service.Build();
            Assert.AreEqual(37, service.Skeleton.Cells.Count);
            Assert.IsFalse(service.IsShapeOnly);
        }

        [Test]
        public void BuildShapeOnly_SetsFlag_AndMatchesCellCount()
        {
            var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 };
            var preview = new GalaxyService(config);
            preview.BuildShapeOnly();
            var full = new GalaxyService(config);
            full.Build();
            Assert.IsTrue(preview.IsShapeOnly);
            Assert.AreEqual(full.Skeleton.Cells.Count, preview.Skeleton.Cells.Count);
        }
    }
}
