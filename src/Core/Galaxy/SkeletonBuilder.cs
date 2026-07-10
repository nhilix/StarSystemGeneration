using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Tables;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 2 builder: the ordered natural-raster seeding passes
/// (spec §5) — shape, stellar leans, anchors, homeworlds. No history: the
/// epoch sim (StarGen.Core.Epoch.EpochGenesis) seeds its polities from the
/// homeworld anchors this leaves behind. The seeding passes survive until
/// slice F derives their outputs causally.</summary>
public static class SkeletonBuilder
{
    public static GalaxySkeleton Build(GalaxyConfig config)
    {
        var skeleton = BuildShape(config);
        PassStellarPopulation(skeleton);
        PassResourceAnchors(skeleton);
        PassHomeworlds(skeleton);
        return skeleton;
    }

    /// <summary>Skeleton with cell densities/void/chokepoint marks only — no anchors,
    /// homeworlds, or history. The cheap path behind the atlas setup live preview;
    /// PassDensitySummary here is the same pass Build runs, so a preview's density
    /// layer is pixel-identical to the same config's full build (setup-knobs spec §4.1).</summary>
    public static GalaxySkeleton BuildShape(GalaxyConfig config)
    {
        var skeleton = new GalaxySkeleton(config);
        PassDensitySummary(skeleton);
        return skeleton;
    }

    internal static void PassDensitySummary(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            double sum = 0; int n = 0;
            int i = 0;
            foreach (var hex in HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius))
            {
                if (i++ % 2 != 0) continue;      // 46 of 91 hexes
                sum += DensityField.At(config, hex);
                n++;
            }
            cell.MeanDensity = sum / n;
            cell.IsVoid = cell.MeanDensity < config.TraversabilityThreshold;
        }
        MarkChokepoints(s);
    }

    /// <summary>Articulation points of the traversability graph (spec §5 pass 1).</summary>
    private static void MarkChokepoints(GalaxySkeleton s)
    {
        int n = s.Cells.Count;
        int[] disc = new int[n], low = new int[n], parent = new int[n];
        bool[] visited = new bool[n], articulation = new bool[n];
        for (int i = 0; i < n; i++) parent[i] = -1;
        int timer = 0;

        IEnumerable<int> Neighbors(int idx)
        {
            foreach (var neighborCoord in HexGrid.Neighbors(s.Cells[idx].Coord))
                if (s.TryGetCell(neighborCoord, out var neighbor) && !neighbor.IsVoid)
                    yield return neighbor.SpiralIndex;
        }

        for (int root = 0; root < n; root++)
        {
            if (visited[root] || s.Cells[root].IsVoid) continue;
            // Iterative DFS with explicit stack (recursion depth could hit thousands).
            var stack = new Stack<(int node, IEnumerator<int> it)>();
            visited[root] = true; disc[root] = low[root] = timer++;
            stack.Push((root, Neighbors(root).GetEnumerator()));
            int rootChildren = 0;

            while (stack.Count > 0)
            {
                var (node, it) = stack.Peek();
                if (it.MoveNext())
                {
                    int next = it.Current;
                    if (!visited[next])
                    {
                        parent[next] = node;
                        if (node == root) rootChildren++;
                        visited[next] = true; disc[next] = low[next] = timer++;
                        stack.Push((next, Neighbors(next).GetEnumerator()));
                    }
                    else if (next != parent[node] && disc[next] < low[node])
                        low[node] = disc[next];
                }
                else
                {
                    stack.Pop();
                    if (stack.Count > 0)
                    {
                        int p = stack.Peek().node;
                        if (low[node] < low[p]) low[p] = low[node];
                        if (p != root && low[node] >= disc[p]) articulation[p] = true;
                    }
                }
            }
            if (rootChildren > 1) articulation[root] = true;
        }

        for (int i = 0; i < n; i++) s.Cells[i].IsChokepoint = articulation[i];
    }

    /// <summary>Spec §5 pass 2: stellar-population & metallicity leans. Never paints
    /// body kinds — world character emerges via the star->band->body causality.</summary>
    internal static void PassStellarPopulation(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            var (wx, wy) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
            double stellar = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseStellarLattice, wx, wy, 2, 0.02);
            cell.Lean = stellar < 0.12 ? StellarLean.RemnantGraveyard
                      : stellar < 0.40 ? StellarLean.OldDim
                      : stellar > 0.72 ? StellarLean.YoungBright
                      : StellarLean.Balanced;
            cell.Metallicity = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseMetalLattice, wx, wy, 2, 0.015);
        }
    }

    /// <summary>Spec §5 pass 3: strategic anchors. Closed vocabulary, one per hex.</summary>
    internal static void PassResourceAnchors(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            var ctx = new RollContext(config.MasterSeed, cell.Coord);

            if (!cell.IsVoid)
            {
                double mineralChance = (0.10 + 0.25 * cell.Metallicity) * config.MineralAnchorMultiplier;
                if (ctx.NextDouble(RollChannel.AnchorKind, 0) < mineralChance)
                    cell.Anchors.Add(new Anchor
                    {
                        Type = AnchorType.MineralRich,
                        Hex = PickAnchorHex(s, cell, 0),
                    });
            }

            // Precursor sites roll everywhere — a site deep in a void is a story (spec §5).
            double precursorChance = (0.02 + (cell.Lean == StellarLean.RemnantGraveyard ? 0.02 : 0.0))
                                     * config.PrecursorAnchorMultiplier;
            if (ctx.NextDouble(RollChannel.AnchorKind, 1) < precursorChance)
                cell.Anchors.Add(new Anchor
                {
                    Type = AnchorType.PrecursorSite,
                    Hex = PickAnchorHex(s, cell, 1),
                });
        }
    }

    /// <summary>Deterministic in-cell hex pick over the cell's 91-hex spiral,
    /// with forward-probe collision handling (one anchor per hex).</summary>
    internal static HexCoordinate PickAnchorHex(GalaxySkeleton s, RegionCell cell, int drawIndex)
    {
        var ctx = new RollContext(s.Config.MasterSeed, cell.Coord);
        var members = new List<HexCoordinate>(
            HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius));
        int local = ctx.NextInt(RollChannel.AnchorPlacement, 0, members.Count, drawIndex);
        for (int probe = 0; probe < members.Count; probe++)
        {
            var hex = members[(local + probe) % members.Count];
            bool taken = false;
            foreach (var a in cell.Anchors)
                if (a.Hex.Equals(hex)) { taken = true; break; }
            if (!taken) return hex;
        }
        return members[0];   // unreachable: a cell never carries 91 anchors
    }

    /// <summary>Spec §5 pass 4 + §6: homeworld anchors + species profiles. The
    /// roll sequence is unchanged from the founding-polity era (anchor placement
    /// stays seed-stable); polity founding itself moved to the epoch sim, which
    /// reads these anchors (EpochGenesis).</summary>
    internal static void PassHomeworlds(GalaxySkeleton s)
    {
        var config = s.Config;
        int target = Math.Max(2, (int)Math.Round(config.HomeworldRatePerCell * s.Cells.Count));
        int minSpacing = Math.Max(2, config.GalaxyRadiusCells
            / Math.Max(1, (int)Math.Ceiling(Math.Sqrt(target))));

        var candidates = s.Cells.Where(c => !c.IsVoid)
            .Select(c => (cell: c,
                order: new RollContext(config.MasterSeed, c.Coord)
                    .NextDouble(RollChannel.HomeworldPlacement)))
            .OrderBy(t => t.order).ThenBy(t => t.cell.SpiralIndex)
            .Select(t => t.cell);

        var placed = new List<RegionCell>();
        foreach (var cell in candidates)
        {
            if (placed.Count >= target) break;
            bool tooClose = placed.Any(p =>
                HexGrid.Distance(p.Coord, cell.Coord) < minSpacing);
            if (tooClose) continue;

            int id = placed.Count;
            var species = RollSpecies(s, cell, id);
            s.Species.Add(species);
            cell.Anchors.Add(new Anchor
            {
                Type = AnchorType.Homeworld, Hex = PickAnchorHex(s, cell, 2), SpeciesId = id,
            });
            placed.Add(cell);
        }
    }

    private static SpeciesProfile RollSpecies(GalaxySkeleton s, RegionCell cell, int id)
    {
        var config = s.Config;
        var ctx = new RollContext(config.MasterSeed, cell.Coord);
        var embodimentTable = new WeightedTable<Embodiment>(
            (Embodiment.TerranAnalog, 40), (Embodiment.Aquatic, 15), (Embodiment.Cryophilic, 12),
            (Embodiment.Lithic, 15), (Embodiment.Hive, 10), (Embodiment.Machine, 8));

        Embodiment embodiment = Embodiment.TerranAnalog;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            embodiment = embodimentTable.Pick(ctx.NextDouble(RollChannel.SpeciesEmbodiment, attempt));
            if (FitsCell(s, cell, embodiment)) break;
        }

        double Axis(int i) => 0.15 + 0.7 * ctx.NextDouble(RollChannel.SpeciesTemperament, 0, i);
        var species = new SpeciesProfile
        {
            Id = id, Embodiment = embodiment,
            Expansionism = Axis(0), Cohesion = Axis(1), Militancy = Axis(2),
            Openness = Axis(3), Industry = Axis(4), Adaptability = Axis(5),
            Name = SpeciesName(ctx, id),
        };
        if (embodiment == Embodiment.Hive)
            species.Cohesion = Math.Max(species.Cohesion, 0.75);   // hive correlation (spec §6)
        return species;
    }

    private static bool FitsCell(GalaxySkeleton s, RegionCell cell, Embodiment e) => e switch
    {
        Embodiment.Cryophilic => cell.Lean == StellarLean.OldDim,
        Embodiment.Aquatic or Embodiment.TerranAnalog =>
            cell.Lean == StellarLean.Balanced || cell.Lean == StellarLean.YoungBright,
        Embodiment.Lithic => cell.Metallicity > 0.4,
        Embodiment.Machine => NeighborhoodHasPrecursor(s, cell),
        _ => true,
    };

    private static bool NeighborhoodHasPrecursor(GalaxySkeleton s, RegionCell cell)
    {
        if (cell.Anchors.Any(a => a.Type == AnchorType.PrecursorSite)) return true;
        foreach (var neighborCoord in HexGrid.Neighbors(cell.Coord))
            if (s.TryGetCell(neighborCoord, out var neighbor)
                && neighbor.Anchors.Any(a => a.Type == AnchorType.PrecursorSite))
                return true;
        return false;
    }

    private static string SpeciesName(RollContext ctx, int id)
    {
        int syllables = 2 + (ctx.NextDouble(RollChannel.NameLength, 1000 + id) < 0.4 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(ctx.NextDouble(RollChannel.NameSyllable, 1000 + id, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
