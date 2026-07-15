using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyRefTests
{
    [Fact]
    public void None_IsNegativeOne_AndReportsIsNone()
    {
        Assert.Equal(-1, BodyRef.None.StarIndex);
        Assert.Equal(-1, BodyRef.None.SlotIndex);
        Assert.True(BodyRef.None.IsNone);
    }

    [Fact]
    public void RealAddress_IsNotNone_AndComparesByValue()
    {
        var a = new BodyRef(0, 2);
        var b = new BodyRef(0, 2);
        Assert.False(a.IsNone);
        Assert.Equal(a, b);
        Assert.NotEqual(a, new BodyRef(1, 2));
    }
}
