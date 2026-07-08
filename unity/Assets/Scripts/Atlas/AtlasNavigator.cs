using System;
using StarGen.Core.Model;

namespace StarGen.Atlas
{
    public enum AtlasScreen { Setup, Galaxy, Cell }

    /// <summary>Pure drill-down state (atlas spec §3): Setup → Galaxy → Cell (+ hex
    /// selection). No Unity types — fully edit-mode testable.</summary>
    public sealed class AtlasNavigator
    {
        public AtlasScreen Screen { get; private set; } = AtlasScreen.Setup;
        public HexCoordinate? SelectedCell { get; private set; }
        public HexCoordinate? SelectedHex { get; private set; }
        public event Action? Changed;

        public void EnterGalaxy()
        {
            Screen = AtlasScreen.Galaxy;
            SelectedCell = null;
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void DrillToCell(HexCoordinate cellCoord)
        {
            if (Screen != AtlasScreen.Galaxy && Screen != AtlasScreen.Cell)
                throw new InvalidOperationException($"cannot drill to a cell from {Screen}");
            Screen = AtlasScreen.Cell;
            SelectedCell = cellCoord;
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void SelectHex(HexCoordinate hex)
        {
            if (Screen != AtlasScreen.Cell)
                throw new InvalidOperationException($"cannot select a hex from {Screen}");
            SelectedHex = hex;
            Changed?.Invoke();
        }

        public void ClearHexSelection()
        {
            SelectedHex = null;
            Changed?.Invoke();
        }

        public void Back()
        {
            if (SelectedHex != null) { SelectedHex = null; }
            else if (Screen == AtlasScreen.Cell) { Screen = AtlasScreen.Galaxy; SelectedCell = null; }
            else if (Screen == AtlasScreen.Galaxy) { Screen = AtlasScreen.Setup; }
            else return;   // Setup: no-op, no event
            Changed?.Invoke();
        }

        public void Reset()
        {
            Screen = AtlasScreen.Setup;
            SelectedCell = null;
            SelectedHex = null;
            Changed?.Invoke();
        }
    }
}
