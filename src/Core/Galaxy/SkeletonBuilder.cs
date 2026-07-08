using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Tables;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 2 builder: ordered seeding passes, then the epoch sim (spec §5, §7).</summary>
public static class SkeletonBuilder
{
    public static GalaxySkeleton Build(GalaxyConfig config)
    {
        var skeleton = new GalaxySkeleton(config);
        PassDensitySummary(skeleton);
        // PASSES (later tasks append here, in order):
        PassStellarPopulation(skeleton);
        PassResourceAnchors(skeleton);
        PassHomeworlds(skeleton);
        EpochSim.Run(skeleton);
        return skeleton;
    }

    internal static void PassDensitySummary(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            double sum = 0; int n = 0;
            for (int hx = cell.Cx * 8; hx < cell.Cx * 8 + 8; hx += 2)
                for (int hy = cell.Cy * 10; hy < cell.Cy * 10 + 10; hy += 2)
                {
                    sum += DensityField.At(config, new HexCoordinate(hx, hy));
                    n++;
                }
            cell.MeanDensity = sum / n;
            cell.IsVoid = cell.MeanDensity < config.TraversabilityThreshold;
        }
        MarkChokepoints(s);
    }

    /// <summary>Articulation points of the traversability graph (spec §5 pass 1).</summary>
#warning HEXMIGRATION: chokepoint graph walks the placeholder square grid; the articulation-point pass moves onto real hex-cell adjacency in its own task.
    private static void MarkChokepoints(GalaxySkeleton s)
    {
        int w = s.GridSize, h = s.GridSize, n = w * h;
        int[] disc = new int[n], low = new int[n], parent = new int[n];
        bool[] visited = new bool[n], articulation = new bool[n];
        for (int i = 0; i < n; i++) parent[i] = -1;
        int timer = 0;

        IEnumerable<int> Neighbors(int idx)
        {
            int cx = idx % w, cy = idx / w;
            if (cx > 0 && !s.Cells[idx - 1].IsVoid) yield return idx - 1;
            if (cx < w - 1 && !s.Cells[idx + 1].IsVoid) yield return idx + 1;
            if (cy > 0 && !s.Cells[idx - w].IsVoid) yield return idx - w;
            if (cy < h - 1 && !s.Cells[idx + w].IsVoid) yield return idx + w;
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
            double hx = cell.Cx * 8 + 4, hy = cell.Cy * 10 + 5;
            double stellar = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseStellarLattice, hx, hy, 2, 0.02);
            cell.Lean = stellar < 0.12 ? StellarLean.RemnantGraveyard
                      : stellar < 0.40 ? StellarLean.OldDim
                      : stellar > 0.72 ? StellarLean.YoungBright
                      : StellarLean.Balanced;
            cell.Metallicity = ValueNoise.Sample(config.MasterSeed,
                RollChannel.NoiseMetalLattice, hx, hy, 2, 0.015);
        }
    }

    /// <summary>Spec §5 pass 3: strategic anchors. Closed vocabulary, one per hex.</summary>
    internal static void PassResourceAnchors(GalaxySkeleton s)
    {
        var config = s.Config;
        foreach (var cell in s.Cells)
        {
            var ctx = new RollContext(config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));

            if (!cell.IsVoid)
            {
                double mineralChance = 0.10 + 0.25 * cell.Metallicity;
                if (ctx.NextDouble(RollChannel.AnchorKind, 0) < mineralChance)
                    cell.Anchors.Add(new Anchor
                    {
                        Type = AnchorType.MineralRich,
                        Hex = PickAnchorHex(s, cell, 0),
                    });
            }

            // Precursor sites roll everywhere — a site deep in a void is a story (spec §5).
            double precursorChance = 0.02 + (cell.Lean == StellarLean.RemnantGraveyard ? 0.02 : 0.0);
            if (ctx.NextDouble(RollChannel.AnchorKind, 1) < precursorChance)
                cell.Anchors.Add(new Anchor
                {
                    Type = AnchorType.PrecursorSite,
                    Hex = PickAnchorHex(s, cell, 1),
                });
        }
    }

    /// <summary>Deterministic in-cell hex pick with forward-probe collision handling.</summary>
    internal static HexCoordinate PickAnchorHex(GalaxySkeleton s, RegionCell cell, int drawIndex)
    {
        var ctx = new RollContext(s.Config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));
        int local = ctx.NextInt(RollChannel.AnchorPlacement, 0, 80, drawIndex);
        for (int probe = 0; probe < 80; probe++)
        {
            int slot = (local + probe) % 80;
            var hex = new HexCoordinate(cell.Cx * 8 + slot % 8, cell.Cy * 10 + slot / 8);
            bool taken = false;
            foreach (var a in cell.Anchors)
                if (a.Hex.Equals(hex)) { taken = true; break; }
            if (!taken) return hex;
        }
        // Unreachable: a cell never carries 80 anchors.
        return new HexCoordinate(cell.Cx * 8, cell.Cy * 10);
    }

    /// <summary>Spec §5 pass 4 + §6: homeworlds, species profiles, founding polities.</summary>
#warning HEXMIGRATION: homeworld target/spacing sized off the placeholder square grid (cell count, GridSize); the hex-lattice-native capacity model lands with the homeworld-placement rewrite.
    internal static void PassHomeworlds(GalaxySkeleton s)
    {
        var config = s.Config;
        int target = Math.Max(2, (int)Math.Round(
            config.HomeworldRatePerCell * s.Cells.Length));
        int minSpacing = Math.Max(2, s.GridSize / (2 * target) + 2);

        var candidates = s.Cells.Where(c => !c.IsVoid)
            .Select(c => (cell: c,
                order: new RollContext(config.MasterSeed, new HexCoordinate(c.Cx, c.Cy))
                    .NextDouble(RollChannel.HomeworldPlacement)))
            .OrderBy(t => t.order).ThenBy(t => t.cell.LinearIndex(config))
            .Select(t => t.cell);

        foreach (var cell in candidates)
        {
            if (s.Polities.Count >= target) break;
            bool tooClose = s.Polities.Any(p =>
                Math.Max(Math.Abs(p.CapitalCx - cell.Cx), Math.Abs(p.CapitalCy - cell.Cy)) < minSpacing);
            if (tooClose) continue;

            int id = s.Polities.Count;
            var species = RollSpecies(s, cell, id);
            s.Species.Add(species);
            s.Polities.Add(new Polity
            {
                Id = id, Name = species.Name, SpeciesId = id,
                CapitalCx = cell.Cx, CapitalCy = cell.Cy,
            });
            cell.Anchors.Add(new Anchor
            {
                Type = AnchorType.Homeworld, Hex = PickAnchorHex(s, cell, 2), SpeciesId = id,
            });
            cell.OwnerPolityId = id;
            cell.DevelopmentTier = 2;
        }
    }

    private static SpeciesProfile RollSpecies(GalaxySkeleton s, RegionCell cell, int id)
    {
        var config = s.Config;
        var ctx = new RollContext(config.MasterSeed, new HexCoordinate(cell.Cx, cell.Cy));
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

#warning HEXMIGRATION: precursor-neighborhood scan walks the placeholder square grid; replaced with real hex adjacency in its own task.
    private static bool NeighborhoodHasPrecursor(GalaxySkeleton s, RegionCell cell)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int cx = cell.Cx + dx, cy = cell.Cy + dy;
                if (cx < 0 || cy < 0 || cx >= s.GridSize || cy >= s.GridSize) continue;
                if (s.CellAt(cx, cy).Anchors.Any(a => a.Type == AnchorType.PrecursorSite)) return true;
            }
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
