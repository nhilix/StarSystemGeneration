using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>Hand-built-skeleton routing tests. GalaxySkeleton's constructor
/// enumerates the cell lattice from config; tests then hand-set cell state.</summary>
public class FlowRoutingTests
{
    private static GalaxySkeleton Blank()
    {
        // Radius 3 lattice; all cells non-void by default for routing clarity.
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        return s;
    }

    [Fact]
    public void Route_FindsShortestPath_AndIncludesEndpoints()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(3, 0));
        var path = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(path);
        Assert.Equal(from.SpiralIndex, path![0].SpiralIndex);
        Assert.Equal(target.SpiralIndex, path[^1].SpiralIndex);
        Assert.Equal(4, path.Count);   // 0,0 → 1,0 → 2,0 → 3,0 (BFS shortest = 3 steps)
    }

    [Fact]
    public void Route_DetoursAroundContestedCells()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(2, 0));
        var direct = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0))!;
        int directLen = direct.Count;
        s.CellAt(new HexCoordinate(1, 0)).Contested = true;   // block the straight line
        var detour = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(detour);
        Assert.True(detour!.Count >= directLen, "blocked direct hop forces an equal-or-longer path");
        Assert.DoesNotContain(detour, c => c.Contested);
    }

    [Fact]
    public void Route_BelligerentTerritoryBlocks_NeutralDoesNot()
    {
        var s = Blank();
        s.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 1 });
        // Wall of enemy cells across the middle column q=1 (all r).
        foreach (var c in s.Cells.Where(c => c.Q == 1)) c.OwnerPolityId = 1;
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(3, -1));
        var blocked = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        // The q=1 wall spans the whole lattice: no path for a belligerent of polity 1.
        Assert.Null(blocked);
        // A third polity not at war with 1 routes straight through.
        var neutral = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 2));
        Assert.NotNull(neutral);
    }

    [Fact]
    public void Route_TargetOnBlockedCell_IsStillReachable_EndpointExempt()
    {
        var s = Blank();
        var from = s.CellAt(new HexCoordinate(0, 0));
        var target = s.CellAt(new HexCoordinate(1, 0));
        target.Contested = true;   // endpoint exemption: target check precedes passable check
        var path = Economy.Route(s, from, c => c.Coord.Equals(target.Coord), Economy.Passable(s, 0));
        Assert.NotNull(path);
    }

    [Fact]
    public void Route_IsDeterministic()
    {
        var s1 = Blank(); var s2 = Blank();
        var p1 = Economy.Route(s1, s1.Cells[0], c => c.SpiralIndex == 30, Economy.Passable(s1, 0));
        var p2 = Economy.Route(s2, s2.Cells[0], c => c.SpiralIndex == 30, Economy.Passable(s2, 0));
        Assert.Equal(p1!.Select(c => c.SpiralIndex), p2!.Select(c => c.SpiralIndex));
    }
}
