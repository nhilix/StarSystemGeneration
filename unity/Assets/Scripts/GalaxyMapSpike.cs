using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.Unity
{
    /// <summary>
    /// Integration spike: builds a galaxy skeleton and paints its cell grid into a
    /// texture — the ASCII atlas as pixels. Runs from Start() in play mode; the
    /// editor menu item (StarGen > Run Galaxy Spike) drives the same logic in edit
    /// mode via BuildAndPaint().
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GalaxyMapSpike : MonoBehaviour
    {
        public enum MapLayer { Density, Polity }

        [SerializeField] private long masterSeed = 42;   // Unity can't serialize ulong
        [SerializeField] private int sizeSectors = 4;
        [SerializeField] private MapLayer layer = MapLayer.Polity;
        [SerializeField] private float worldSize = 8f;   // sprite width in world units

        private void Start() => BuildAndPaint();

        public void BuildAndPaint()
        {
            var config = new GalaxyConfig
            {
                MasterSeed = (ulong)masterSeed,
                SizeSectors = sizeSectors,
            };

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var skeleton = SkeletonBuilder.Build(config);
            timer.Stop();

            int living = 0;
            foreach (var polity in skeleton.Polities)
                if (!polity.Extinct) living++;
            Debug.Log($"StarGen Core loaded: built {config.CellsX}x{config.CellsY} cell galaxy " +
                      $"in {timer.ElapsedMilliseconds} ms — {living} living polities, " +
                      $"{skeleton.Events.Count} events.");

            var texture = Paint(skeleton);
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: texture.width / worldSize);
            GetComponent<SpriteRenderer>().sprite = sprite;
        }

        private Texture2D Paint(GalaxySkeleton skeleton)
        {
            var config = skeleton.Config;
            var texture = new Texture2D(config.CellsX, config.CellsY, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // crisp cells, no smearing
            };

            foreach (var cell in skeleton.Cells)
            {
                Color color = layer switch
                {
                    MapLayer.Polity => PolityColor(skeleton, cell),
                    _ => new Color((float)cell.MeanDensity,
                                   (float)cell.MeanDensity,
                                   (float)cell.MeanDensity),
                };
                // Texture v runs bottom-up; cell cy runs top-down — flip so the map
                // matches the inspector's ASCII orientation.
                texture.SetPixel(cell.Cx, config.CellsY - 1 - cell.Cy, color);
            }
            texture.Apply();
            return texture;
        }

        private static Color PolityColor(GalaxySkeleton skeleton, RegionCell cell)
        {
            if (cell.IsVoid) return Color.black;
            foreach (var polity in skeleton.Polities)
                if (!polity.Extinct && polity.CapitalCx == cell.Cx && polity.CapitalCy == cell.Cy)
                    return Color.white;                          // capitals pop
            if (cell.OwnerPolityId < 0)
                return new Color(0.16f, 0.16f, 0.16f);           // unclaimed wilds
            // Stable hue per polity id, brightness by development.
            float hue = (cell.OwnerPolityId * 0.6180339887f) % 1f;   // golden-ratio spacing
            float value = 0.55f + 0.09f * Mathf.Min(5, cell.DevelopmentTier);
            return Color.HSVToRGB(hue, 0.75f, value);
        }
    }
}
