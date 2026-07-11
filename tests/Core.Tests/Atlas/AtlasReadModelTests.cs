using StarGen.Core.Atlas;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The atlas read model — the single Eye-parameterized query
/// surface the Unity presentation consumes (unity-atlas-design.md).</summary>
public class AtlasReadModelTests
{
    [Fact]
    public void GodEyeCarriesWorldYearAndScope()
    {
        var eye = EyeContext.God(1000);
        Assert.True(eye.IsGod);
        Assert.Equal(1000, eye.WorldYear);
    }

    [Fact]
    public void ControllerEyeIsAReservedSeamCarryingItsActor()
    {
        var eye = EyeContext.Controller(actorId: 3, worldYear: 500);
        Assert.False(eye.IsGod);
        Assert.Equal(3, eye.ActorId);
        Assert.Equal(500, eye.WorldYear);
    }

    [Fact]
    public void ReadModelExposesTheRasterInSkeletonOrder()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        Assert.Same(skeleton.Cells[0], model.Cells[0]);
        Assert.Equal(skeleton.Cells.Count, model.Cells.Count);
    }
}
