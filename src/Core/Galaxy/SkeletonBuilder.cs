using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Tables;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 2 builder: the natural raster, causally (slice F). The
/// cosmic sim (StarGen.Core.Genesis.CosmicSim) runs the formation history
/// and its residue *is* the density/lean/metallicity structure — the old
/// painted passes 1–2 are gone. Anchors and homeworlds still seed from the
/// derived fields (passes 3–4, retired by the evolutionary integration).
/// The epoch sim (StarGen.Core.Epoch.EpochGenesis) seeds its polities from
/// the homeworld anchors this leaves behind.</summary>
public static class SkeletonBuilder
{
    public static GalaxySkeleton Build(GalaxyConfig config,
        Action<Genesis.CosmicFrame>? cosmicObserver = null,
        Action<Genesis.EvoFrame>? evoObserver = null)
    {
        var skeleton = BuildShape(config, cosmicObserver);
        Genesis.EvolutionSim.Run(skeleton, evoObserver);
        PassSpecies(skeleton);
        PassDerivedAnchors(skeleton);
        return skeleton;
    }

    /// <summary>Skeleton with the simulated present-day fields and
    /// void/chokepoint marks — no anchors, homeworlds, or polity history.
    /// The path behind the atlas setup preview; it runs the same cosmic sim
    /// Build runs, so a preview's density layer is pixel-identical to the
    /// same config's full build (setup-knobs spec §4.1).</summary>
    public static GalaxySkeleton BuildShape(GalaxyConfig config,
        Action<Genesis.CosmicFrame>? cosmicObserver = null)
    {
        var skeleton = new GalaxySkeleton(config);
        var cosmic = Genesis.CosmicSim.Run(skeleton, cosmicObserver);
        Genesis.CosmicResidue.Compress(cosmic);
        MarkChokepoints(skeleton);
        return skeleton;
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

    /// <summary>Species profiles, one per current-era sapient origin
    /// (spec §6 vocabulary over slice F's causal inputs): machine species
    /// descend from precursor capitals; organic embodiments fit their
    /// origin cell's simulated character. Species id == its origin's index
    /// among current-era origins, in origin-id order.</summary>
    internal static void PassSpecies(GalaxySkeleton s)
    {
        foreach (var origin in s.Origins)
        {
            if (origin.Era != OriginEra.Current) continue;
            int id = s.Species.Count;
            s.Species.Add(DeriveSpecies(s, origin, id));
        }
    }

    /// <summary>Anchors derive from the genesis registries (slice F —
    /// passes 3–4's rolled paint retired): homeworlds at current-era origin
    /// hexes, precursor-site anchors at wave site hexes, mineral anchors
    /// rolled against the *simulated* mineral richness (ore geography traces
    /// to actual ancient supernovae). One anchor per hex; homeworlds claim
    /// first, sites next, minerals probe around both.</summary>
    internal static void PassDerivedAnchors(GalaxySkeleton s)
    {
        var config = s.Config;

        int speciesId = 0;
        foreach (var origin in s.Origins)
        {
            if (origin.Era != OriginEra.Current) continue;
            s.CellAt(origin.CellCoord).Anchors.Add(new Anchor
            {
                Type = AnchorType.Homeworld, Hex = origin.Hex, SpeciesId = speciesId++,
            });
        }

        // wave sites → anchors (deduped by hex; multiplier samples them)
        foreach (var wave in s.PrecursorWaves)
            foreach (var site in wave.Sites)
            {
                if (site.Type == PrecursorSiteType.SterilizationScar) continue;
                var cell = s.CellForHex(site.Hex);
                bool taken = false;
                foreach (var a in cell.Anchors)
                    if (a.Hex.Equals(site.Hex)) { taken = true; break; }
                if (taken) continue;
                if (config.PrecursorAnchorMultiplier < 1.0)
                {
                    var gate = new RollContext(config.MasterSeed, site.Hex);
                    if (gate.NextDouble(RollChannel.AnchorKind, 1)
                        >= config.PrecursorAnchorMultiplier) continue;
                }
                cell.Anchors.Add(new Anchor
                { Type = AnchorType.PrecursorSite, Hex = site.Hex });
            }

        foreach (var cell in s.Cells)
        {
            if (cell.IsVoid) continue;
            var ctx = new RollContext(config.MasterSeed, cell.Coord);
            double mineralChance = (0.05 + 0.30 * cell.MineralRichness)
                * config.MineralAnchorMultiplier;
            if (ctx.NextDouble(RollChannel.AnchorKind, 0) < mineralChance)
                cell.Anchors.Add(new Anchor
                {
                    Type = AnchorType.MineralRich,
                    Hex = PickAnchorHex(s, cell, 0),
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

    /// <summary>Species from origin context (spec §6 vocabulary, causal
    /// inputs): machine embodiment comes only from precursor descent; the
    /// organic table probes for a fit against the origin cell's simulated
    /// character. Temperament axes stay seeded rolls keyed to the origin
    /// cell (the species-profile seed); catastrophe-scarred origins lean
    /// harder (militancy floor rises with setbacks).</summary>
    /// <remarks>Public since slice H: native emergences mint their species
    /// with the same derivation, whenever their date fires.</remarks>
    public static SpeciesProfile DeriveSpecies(GalaxySkeleton s,
        SapientOrigin origin, int id)
    {
        var cell = s.CellAt(origin.CellCoord);
        var ctx = new RollContext(s.Config.MasterSeed, cell.Coord);

        Embodiment embodiment;
        if (origin.DescendantOfWaveId >= 0)
            embodiment = Embodiment.Machine;   // homeworld is the old capital
        else
        {
            var embodimentTable = new WeightedTable<Embodiment>(
                (Embodiment.TerranAnalog, 44), (Embodiment.Aquatic, 16),
                (Embodiment.Cryophilic, 13), (Embodiment.Lithic, 16),
                (Embodiment.Hive, 11));
            embodiment = Embodiment.TerranAnalog;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                embodiment = embodimentTable.Pick(
                    ctx.NextDouble(RollChannel.SpeciesEmbodiment, attempt));
                if (FitsCell(cell, embodiment)) break;
            }
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
        if (origin.Setbacks > 0)   // a scarred cradle breeds vigilance
            species.Militancy = Math.Max(species.Militancy,
                Math.Min(0.35 + 0.1 * origin.Setbacks, 0.65));
        return species;
    }

    private static bool FitsCell(RegionCell cell, Embodiment e) => e switch
    {
        Embodiment.Cryophilic => cell.Lean == StellarLean.OldDim,
        Embodiment.Aquatic or Embodiment.TerranAnalog =>
            cell.Lean == StellarLean.Balanced || cell.Lean == StellarLean.YoungBright,
        Embodiment.Lithic => cell.Metallicity > 0.4,
        _ => true,
    };

    private static string SpeciesName(RollContext ctx, int id)
    {
        int syllables = 2 + (ctx.NextDouble(RollChannel.NameLength, 1000 + id) < 0.4 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(ctx.NextDouble(RollChannel.NameSyllable, 1000 + id, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
