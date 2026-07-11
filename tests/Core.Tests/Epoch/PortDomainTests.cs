using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class PortDomainTests
{
    private static GalaxySkeleton SmallSkeleton(ulong seed = 7, int radius = 6) =>
        SkeletonBuilder.BuildShape(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void ServiceRadius_StepsPerTier()
    {
        var cfg = new EpochSimConfig();
        int b = cfg.Infrastructure.ServiceRadiusBaseHexes;
        int p = cfg.Infrastructure.ServiceRadiusPerTierHexes;
        Assert.Equal(b, PortDomains.ServiceRadius(cfg, 1));
        Assert.Equal(b + p, PortDomains.ServiceRadius(cfg, 2));
        Assert.Equal(b + 2 * p, PortDomains.ServiceRadius(cfg, 3));
    }

    [Fact]
    public void Services_InsideRadius_NotBeyond()
    {
        var sk = SmallSkeleton();
        var cfg = new EpochSimConfig();
        var nonVoid = sk.Cells.First(c => !c.IsVoid);
        var port = new Port(0, 0, HexGrid.CellCenter(nonVoid.Coord), tier: 1, foundedYear: 0);
        Assert.True(PortDomains.Services(sk, cfg, port, port.Hex));
        Assert.False(PortDomains.Services(sk, cfg, port,
            new HexCoordinate(port.Hex.Q + PortDomains.ServiceRadius(cfg, 1) + 1, port.Hex.R)));
    }

    [Fact]
    public void VoidCells_AreNeverServiced_TheWildsStayDark()
    {
        var sk = SmallSkeleton();
        var cfg = new EpochSimConfig();
        var voidCell = sk.Cells.FirstOrDefault(c => c.IsVoid);
        Assert.NotNull(voidCell);                          // seed 7 radius 6 carries voids
        var port = new Port(0, 0, HexGrid.CellCenter(voidCell!.Coord), tier: 3, foundedYear: 0);
        Assert.False(PortDomains.Services(sk, cfg, port, port.Hex));
    }

    [Fact]
    public void Services_OutsideTheGalaxyFootprint_IsFalse()
    {
        var sk = SmallSkeleton();
        var cfg = new EpochSimConfig();
        var nonVoid = sk.Cells.First(c => !c.IsVoid);
        var port = new Port(0, 0, HexGrid.CellCenter(nonVoid.Coord), tier: 3, foundedYear: 0);
        // far outside any cell of the small footprint
        Assert.False(PortDomains.Services(sk, cfg, port, new HexCoordinate(10_000, 10_000)));
    }

    [Fact]
    public void OwnersAt_DistinctAscending_AndContested()
    {
        var sk = SmallSkeleton();
        var cfg = new EpochSimConfig();
        var hex = HexGrid.CellCenter(sk.Cells.First(c => !c.IsVoid).Coord);
        var ports = new List<Port> {
            new Port(0, 5, hex, 2, 0),
            new Port(1, 5, hex, 1, 0),      // same owner twice → one entry
            new Port(2, 3, hex, 1, 0),
        };
        var owners = new List<int>();
        PortDomains.OwnersAt(sk, cfg, ports, hex, owners);
        Assert.Equal(new[] { 3, 5 }, owners);
        Assert.True(PortDomains.IsContested(sk, cfg, ports, hex));

        var solo = new List<Port> { new Port(0, 5, hex, 2, 0) };
        PortDomains.OwnersAt(sk, cfg, solo, hex, owners);   // reused list is cleared
        Assert.Equal(new[] { 5 }, owners);
        Assert.False(PortDomains.IsContested(sk, cfg, solo, hex));
    }

    [Fact]
    public void LaneMath_ReachStepsWithGateTier()
    {
        // reach lives in the gate, not the port (lane-economics spec §2);
        // capacity/speed from gate tiers are covered by LaneGateTests
        var cfg = new EpochSimConfig();
        Assert.Equal(cfg.Infrastructure.GateReachTier1Hexes, LaneMath.ReachHexes(cfg, 1));
        Assert.Equal(cfg.Infrastructure.GateReachTier2Hexes, LaneMath.ReachHexes(cfg, 2));
        Assert.Equal(cfg.Infrastructure.GateReachTier3Hexes, LaneMath.ReachHexes(cfg, 3));
        Assert.True(LaneMath.ReachHexes(cfg, 3) > LaneMath.ReachHexes(cfg, 1));
    }
}
