using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 2 builder: ordered seeding passes, then the epoch sim (spec §5, §7).</summary>
public static class SkeletonBuilder
{
    public static GalaxySkeleton Build(GalaxyConfig config)
    {
        var skeleton = new GalaxySkeleton(config);
        PassDensitySummary(skeleton);
        // PASSES (later tasks append here, in order):
        // PassStellarPopulation(skeleton);
        // PassResourceAnchors(skeleton);
        // PassHomeworlds(skeleton);
        // EpochSim.Run(skeleton);
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
    private static void MarkChokepoints(GalaxySkeleton s)
    {
        var config = s.Config;
        int w = config.CellsX, h = config.CellsY, n = w * h;
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
}
