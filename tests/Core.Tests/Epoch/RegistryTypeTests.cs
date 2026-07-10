using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class RegistryTypeTests
{
    [Fact]
    public void Lane_RequiresOrderedPortIds()
    {
        Assert.Throws<System.ArgumentException>(() => new Lane(0, 2, 1, 0));
        Assert.Throws<System.ArgumentException>(() => new Lane(0, 1, 1, 0));
        var lane = new Lane(0, 1, 2, 100);
        Assert.Equal((1, 2), (lane.PortAId, lane.PortBId));
        Assert.Equal(100, lane.BuiltYear);
    }

    [Fact]
    public void Port_RoundTripsIdentityAndGrowthAxis()
    {
        var hex = new HexCoordinate(-7, 12);
        var port = new Port(3, ownerActorId: 5, hex, tier: 1, foundedYear: 250);
        Assert.Equal((3, 5, hex, 1, 250),
                     (port.Id, port.OwnerActorId, port.Hex, port.Tier, port.FoundedYear));
        port.Tier = 2;                     // investment raises the port
        Assert.Equal(2, port.Tier);
    }

    [Fact]
    public void Facility_StartsAtFullCondition()
    {
        var f = new Facility(0, typeId: 4, tier: 1, new HexCoordinate(1, 1),
                             ownerActorId: 2, builtYear: 75);
        Assert.Equal(1.0, f.Condition);
    }
}
