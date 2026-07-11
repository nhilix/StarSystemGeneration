using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The domains lens — port-derived territory glows with organic
/// borders and visible contested overlap (space-and-travel.md §P1). Every
/// value derives from the port registry at query time; nothing stored.
/// Fixture builds ports by hand (the ColonyViabilityTests pattern): actors
/// haven't entered at seed time.</summary>
public class DomainLensTests
{
    /// <summary>Seeded raster with one hand-planted port per polity, far
    /// apart, plus the coordinates involved.</summary>
    private static (AtlasReadModel Model, SimState State, Port Port) WithPort()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = FirstLiveCenter(state);
        var port = new Port(0, state.Actors[0].Id, hex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        return (new AtlasReadModel(state), state, port);
    }

    private static HexCoordinate FirstLiveCenter(SimState state)
    {
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) return HexGrid.CellCenter(cell.Coord);
        throw new System.InvalidOperationException("no live cell");
    }

    [Fact]
    public void APortHexBelongsToItsOwner()
    {
        var (model, state, port) = WithPort();
        var owners = new List<int>();
        DomainLens.OwnersAt(model, EyeContext.God(state.WorldYear), port.Hex, owners);
        Assert.Equal(new[] { port.OwnerActorId }, owners);
    }

    [Fact]
    public void TheWildsHaveNoOwner()
    {
        var (model, state, _) = WithPort();
        RegionCell? far = null;
        foreach (var c in model.Cells)
            if (c.IsVoid) { far = c; break; }
        Assert.NotNull(far);
        var owners = new List<int> { 99 };   // stale content must be cleared
        DomainLens.OwnersAt(model, EyeContext.God(state.WorldYear),
                            HexGrid.CellCenter(far!.Coord), owners);
        Assert.Empty(owners);
    }

    [Fact]
    public void CellShadesRunParallelAndWildsAreTransparent()
    {
        var (model, state, _) = WithPort();
        var eye = EyeContext.God(state.WorldYear);
        var shades = DomainLens.CellShades(model, eye);
        Assert.Equal(model.Cells.Count, shades.Count);
        var owners = new List<int>();
        for (int i = 0; i < model.Cells.Count; i++)
        {
            DomainLens.OwnersAt(model, eye,
                                HexGrid.CellCenter(model.Cells[i].Coord), owners);
            if (owners.Count == 0)
                Assert.Equal(0, shades[i].A);
            else
                Assert.True(shades[i].A > 0);
        }
    }

    [Fact]
    public void ASingleOwnerCellGlowsInTheOwnersColor()
    {
        var (model, state, port) = WithPort();
        var eye = EyeContext.God(state.WorldYear);
        var shades = DomainLens.CellShades(model, eye);
        int cellIndex = IndexOfCell(model, HexGrid.CellOf(port.Hex));
        var own = AtlasPalette.OwnerColor(port.OwnerActorId);
        Assert.Equal((own.R, own.G, own.B),
                     (shades[cellIndex].R, shades[cellIndex].G, shades[cellIndex].B));
        Assert.True(shades[cellIndex].A > 0);
    }

    [Fact]
    public void ContestedOverlapReadsAsItsOwnShade()
    {
        var (model, state, anchor) = WithPort();
        int rival = state.Actors[1].Id;
        Assert.NotEqual(anchor.OwnerActorId, rival);
        state.Ports.Add(new Port(1, rival, anchor.Hex, tier: 1, foundedYear: 0));

        var eye = EyeContext.God(state.WorldYear);
        var owners = new List<int>();
        DomainLens.OwnersAt(model, eye, anchor.Hex, owners);
        Assert.Equal(2, owners.Count);

        var shades = DomainLens.CellShades(model, eye);
        var contested = shades[IndexOfCell(model, HexGrid.CellOf(anchor.Hex))];
        Assert.True(contested.A > 0);
        var anchorOwn = AtlasPalette.OwnerColor(anchor.OwnerActorId);
        var rivalOwn = AtlasPalette.OwnerColor(rival);
        Assert.NotEqual((anchorOwn.R, anchorOwn.G, anchorOwn.B),
                        (contested.R, contested.G, contested.B));
        Assert.NotEqual((rivalOwn.R, rivalOwn.G, rivalOwn.B),
                        (contested.R, contested.G, contested.B));
    }

    private static int IndexOfCell(AtlasReadModel model, HexCoordinate cellCoord)
    {
        for (int i = 0; i < model.Cells.Count; i++)
            if (model.Cells[i].Coord.Equals(cellCoord)) return i;
        throw new System.InvalidOperationException("cell not in raster");
    }

    [Fact]
    public void HexShadesResolveServiceRadiiAtHexResolution()
    {
        var (model, state, port) = WithPort();
        var eye = EyeContext.God(state.WorldYear);
        int radius = PortDomains.ServiceRadius(state.Config, port.Tier);
        // A LIVE cell beyond the radius — remoteness must go dark on its
        // own, not through the void gate.
        HexCoordinate? outside = null;
        foreach (var cell in model.Cells)
        {
            if (cell.IsVoid) continue;
            var center = HexGrid.CellCenter(cell.Coord);
            if (HexGrid.Distance(port.Hex, center) > radius) { outside = center; break; }
        }
        Assert.NotNull(outside);
        var shades = DomainLens.HexShades(model, eye,
                                          new[] { port.Hex, outside!.Value });
        Assert.Equal(2, shades.Count);
        Assert.True(shades[0].A > 0);
        Assert.Equal(0, shades[1].A);
    }

    [Fact]
    public void TheReadModelIndexesCellsByCoord()
    {
        var (model, _, _) = WithPort();
        for (int i = 0; i < model.Cells.Count; i++)
        {
            Assert.True(model.TryIndexOfCell(model.Cells[i].Coord, out int at));
            Assert.Equal(i, at);
        }
        Assert.False(model.TryIndexOfCell(new HexCoordinate(9999, 9999), out _));
    }
}
