using System;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Atlas
{
    /// <summary>The single Core↔Unity seam (atlas spec §4): owns config, skeleton,
    /// and generation; views touch Core only through it (plus HexGrid math).</summary>
    public sealed class GalaxyService
    {
        private readonly GalaxyConfig _config;
        private GalaxyContext? _context;

        public GalaxyService(ulong seed, int radiusCells) =>
            _config = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radiusCells };

        public long BuildMilliseconds { get; private set; }

        public GalaxyContext Context => _context
            ?? throw new InvalidOperationException("call Build() first");

        public GalaxySkeleton Skeleton => Context.Skeleton!;

        public void Build()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var skeleton = SkeletonBuilder.Build(_config);
            timer.Stop();
            BuildMilliseconds = timer.ElapsedMilliseconds;
            _context = new GalaxyContext(_config) { Skeleton = skeleton };
        }

        public HexResult Generate(HexCoordinate hex) => Generator.Generate(Context, hex);

        public bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell) =>
            Skeleton.TryGetCell(cellCoord, out cell);

        public HexState StateOf(HexCoordinate hex)
        {
            if (!DensityField.InGalaxy(_config, hex)) return HexState.Void;
            var cell = Skeleton.CellForHex(hex);
            foreach (var anchor in cell.Anchors)
                if (anchor.Hex.Equals(hex)) return HexState.Anchored;
            var system = Generate(hex).System;
            if (system == null) return HexState.Empty;
            foreach (var star in system.Stars)
                foreach (var slot in star.Slots)
                {
                    if (slot.Body == null) continue;
                    if (slot.Body.Settlement != Settlement.None) return HexState.Settled;
                    foreach (var satellite in slot.Body.Satellites)
                        if (satellite.Settlement != Settlement.None) return HexState.Settled;
                }
            return HexState.System;
        }

        public string CellSummary(RegionCell cell)
        {
            var s = Skeleton;
            var sb = new StringBuilder();
            string owner = cell.OwnerPolityId >= 0 ? s.Polities[cell.OwnerPolityId].Name : "unclaimed";
            sb.AppendLine($"cell ({cell.Q},{cell.R})  density {cell.MeanDensity:F2}"
                + (cell.IsVoid ? "  VOID" : "") + (cell.IsChokepoint ? "  CHOKEPOINT" : ""));
            sb.AppendLine($"{cell.Lean} · metallicity {cell.Metallicity:F2}");
            sb.AppendLine($"owner: {owner} · dev {cell.DevelopmentTier}"
                + (cell.WarScarred ? " · war-scarred" : ""));
            foreach (var anchor in cell.Anchors)
                sb.AppendLine($"anchor: {anchor.Type} at {Core.Naming.Designation.For(anchor.Hex)}"
                    + (anchor.SpeciesId >= 0 ? $" ({s.Species[anchor.SpeciesId].Name})" : ""));
            foreach (var e in s.Events)
                if (e.Q == cell.Q && e.R == cell.R)
                    sb.AppendLine($"epoch {e.Epoch}: {e.Type} by {s.Polities[e.ActorPolityId].Name}"
                        + (e.TargetPolityId >= 0 ? $" vs {s.Polities[e.TargetPolityId].Name}" : ""));
            return sb.ToString();
        }
    }
}
