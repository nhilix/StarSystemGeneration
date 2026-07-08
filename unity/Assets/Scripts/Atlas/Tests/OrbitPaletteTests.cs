using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Content;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class OrbitPaletteTests
    {
        [Test]
        public void EveryStarType_HasANonFallbackColor()
        {
            foreach (var (def, _) in StarTypes.Table.Entries)
                Assert.AreNotEqual(OrbitPalette.Fallback, OrbitPalette.StarColor(def.Id), def.Id);
        }

        [Test]
        public void EveryBodyKind_HasANonFallbackColor()
        {
            foreach (BodyKind kind in System.Enum.GetValues(typeof(BodyKind)))
                Assert.AreNotEqual(OrbitPalette.Fallback, OrbitPalette.BodyColor(kind),
                    kind.ToString());
        }

        [Test]
        public void UnknownStarType_FallsBack() =>
            Assert.AreEqual(OrbitPalette.Fallback, OrbitPalette.StarColor("mystery_type"));
    }
}
