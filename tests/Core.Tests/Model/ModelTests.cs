using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Model;

public class ModelTests
{
    [Fact]
    public void Body_IsInhabited_TracksSettlementOrSapience()
    {
        var body = new Body { Kind = BodyKind.RockyWorld };
        Assert.False(body.IsInhabited);
        body.Settlement = Settlement.Outpost;
        Assert.True(body.IsInhabited);                    // colony on a dead rock
        body.Settlement = Settlement.None;
        body.Biosphere = Biosphere.Sapient;
        Assert.True(body.IsInhabited);                    // native society
    }

    [Fact]
    public void StarSystem_InitializesCollections()
    {
        var system = new StarSystem("SGC 0001-0002");
        Assert.Equal("SGC 0001-0002", system.Designation);
        Assert.Empty(system.Stars);
        Assert.Empty(system.Tags);
        Assert.Null(system.GivenName);
        Assert.Null(system.OverlayId);
    }
}
