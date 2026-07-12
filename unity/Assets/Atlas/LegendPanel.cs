using System;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>The per-lens legend (K3, the K2 eyeball ask): when a lens
    /// is active, its vocabulary — glyph shapes, color ramps, lane stroke
    /// states — surfaces in a compact card. Entries come from Core's
    /// LegendQuery (the same constants the layers draw with); glyph rows
    /// name AtlasGlyph cells by enum member (Enum.TryParse — the drift
    /// test rides EditMode).</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class LegendPanel : MonoBehaviour
    {
        [SerializeField] private LensRail rail;

        private Texture2D _glyphAtlas;

        public void Wire(LensRail lensRail) => rail = lensRail;

        private void OnEnable()
        {
            _glyphAtlas = Resources.Load<Texture2D>("AtlasGlyphs");
            if (rail != null) rail.LensChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (rail != null) rail.LensChanged -= Refresh;
        }

        private void Refresh()
        {
            var legend = GetComponent<AtlasChrome>().Legend;
            if (legend == null || rail == null) return;
            legend.Clear();
            var entries = LegendQuery.For(rail.ActiveLegendKey,
                                          rail.PriceGood);
            if (entries.Count == 0)
            {
                legend.style.display = DisplayStyle.None;
                return;
            }
            legend.style.display = DisplayStyle.Flex;
            var title = new Label(rail.ActiveLegendKey.ToUpperInvariant());
            title.AddToClassList("ssg-legend__title");
            legend.Add(title);
            foreach (var entry in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("ssg-legend__row");
                row.Add(Swatch(entry));
                var label = new Label(entry.Label);
                label.AddToClassList("ssg-legend__label");
                row.Add(label);
                legend.Add(row);
            }
        }

        private VisualElement Swatch(LegendEntry entry)
        {
            var color = new Color32(entry.Color.R, entry.Color.G,
                                    entry.Color.B, 255);
            if (entry.Swatch == LegendSwatch.Glyph && _glyphAtlas != null
                && entry.GlyphKey != null
                && Enum.TryParse<AtlasGlyph>(entry.GlyphKey, out var glyph))
            {
                // sprite-sheet crop: size = grid ×100%, position = cell
                // fraction of the remaining travel
                var el = new VisualElement();
                el.AddToClassList("ssg-legend__glyph");
                el.style.backgroundImage = _glyphAtlas;
                el.style.unityBackgroundImageTintColor = (Color)color;
                int index = (int)glyph;
                int col = index % AtlasGlyphs.Columns;
                int row = index / AtlasGlyphs.Columns;
                el.style.backgroundSize = new BackgroundSize(
                    Length.Percent(AtlasGlyphs.Columns * 100),
                    Length.Percent(AtlasGlyphs.Rows * 100));
                el.style.backgroundPositionX = new BackgroundPosition(
                    BackgroundPositionKeyword.Left,
                    Length.Percent(100f * col / (AtlasGlyphs.Columns - 1)));
                el.style.backgroundPositionY = new BackgroundPosition(
                    BackgroundPositionKeyword.Top,
                    Length.Percent(100f * row / (AtlasGlyphs.Rows - 1)));
                return el;
            }

            var box = new VisualElement();
            box.AddToClassList("ssg-legend__swatch");
            if (entry.Swatch == LegendSwatch.Stroke)
                box.AddToClassList("ssg-legend__swatch--stroke");
            if (entry.Swatch == LegendSwatch.Ring)
            {
                box.AddToClassList("ssg-legend__swatch--ring");
                box.style.borderTopColor = (Color)color;
                box.style.borderBottomColor = (Color)color;
                box.style.borderLeftColor = (Color)color;
                box.style.borderRightColor = (Color)color;
            }
            else
                box.style.backgroundColor = (Color)color;
            return box;
        }
    }
}
