using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class AtlasNavigatorTests
    {
        [Test]
        public void DrillDown_AndBack_WalksTheLadder()
        {
            var nav = new AtlasNavigator();
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
            nav.EnterGalaxy();
            Assert.AreEqual(AtlasScreen.Galaxy, nav.Screen);
            nav.DrillToCell(new HexCoordinate(2, -1));
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.AreEqual(new HexCoordinate(2, -1), nav.SelectedCell);
            nav.SelectHex(new HexCoordinate(23, -12));
            Assert.AreEqual(new HexCoordinate(23, -12), nav.SelectedHex);
            nav.Back();   // clears hex selection first
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            nav.Back();
            Assert.AreEqual(AtlasScreen.Galaxy, nav.Screen);
            Assert.IsNull(nav.SelectedCell);
            nav.Back();
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
            nav.Back();   // no-op at the root
            Assert.AreEqual(AtlasScreen.Setup, nav.Screen);
        }

        [Test]
        public void IllegalTransitions_Throw()
        {
            var nav = new AtlasNavigator();
            Assert.Throws<System.InvalidOperationException>(() => nav.DrillToCell(new HexCoordinate(0, 0)));
            Assert.Throws<System.InvalidOperationException>(() => nav.SelectHex(new HexCoordinate(0, 0)));
        }

        [Test]
        public void EveryMutation_FiresChangedOnce()
        {
            var nav = new AtlasNavigator();
            int fired = 0;
            nav.Changed += () => fired++;
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 1));
            nav.SelectHex(new HexCoordinate(16, 1));
            nav.ClearHexSelection();
            nav.Back();
            nav.Reset();
            Assert.AreEqual(6, fired);
        }

        [Test]
        public void DrillToCell_FromCell_SwitchesCell_AndClearsHex()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.DrillToCell(new HexCoordinate(0, 1));   // breadcrumb-style sibling jump
            Assert.AreEqual(new HexCoordinate(0, 1), nav.SelectedCell);
            Assert.IsNull(nav.SelectedHex);
        }

        [Test]
        public void EnterSystem_FromCellWithHex_LandsOnSystemScreen()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            Assert.AreEqual(AtlasScreen.System, nav.Screen);
            Assert.AreEqual(new HexCoordinate(11, -5), nav.SelectedHex);
            Assert.AreEqual(new HexCoordinate(1, 0), nav.SelectedCell);
        }

        [Test]
        public void EnterSystem_Throws_OffCellOrWithoutHex()
        {
            var nav = new AtlasNavigator();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());
            nav.EnterGalaxy();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());
            nav.DrillToCell(new HexCoordinate(1, 0));
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());   // no hex
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            Assert.Throws<System.InvalidOperationException>(() => nav.EnterSystem());   // already there
        }

        [Test]
        public void Back_FromSystem_ReturnsToCell_KeepingHexSelection()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            nav.Back();
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.AreEqual(new HexCoordinate(11, -5), nav.SelectedHex);   // panel context survives
            nav.Back();   // now clears the hex, per existing behavior
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
        }

        [Test]
        public void DrillToCell_FromSystem_ClearsHex_AndLandsOnCell()
        {
            var nav = new AtlasNavigator();
            nav.EnterGalaxy();
            nav.DrillToCell(new HexCoordinate(1, 0));
            nav.SelectHex(new HexCoordinate(11, -5));
            nav.EnterSystem();
            nav.DrillToCell(new HexCoordinate(1, 0));   // breadcrumb "Cell" crumb path
            Assert.AreEqual(AtlasScreen.Cell, nav.Screen);
            Assert.IsNull(nav.SelectedHex);
            Assert.AreEqual(new HexCoordinate(1, 0), nav.SelectedCell);
        }
    }
}
