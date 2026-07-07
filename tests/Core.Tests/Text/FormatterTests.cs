using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;
using Xunit;

namespace StarGen.Core.Tests.Text;

public class FormatterTests
{
    [Fact]
    public void Format_IsDeterministic_AcrossRegeneration()
    {
        for (int x = 0; x < 300; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            var a = SystemFormatter.Format(Generator.Generate(17, coord));
            var b = SystemFormatter.Format(Generator.Generate(17, coord));
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void Format_EmptyHex() =>
        Assert.Contains("empty",
            SystemFormatter.Format(new HexResult(new HexCoordinate(1, 2), null)));

    [Fact]
    public void Format_NonEmpty_ShowsDesignationStarsAndSlots()
    {
        for (int x = 0; x < 200; x++)
        {
            var r = Generator.Generate(17, new HexCoordinate(x, 5));
            if (r.System == null) continue;
            var text = SystemFormatter.Format(r);
            Assert.Contains(r.System.Designation, text);
            Assert.Contains(r.System.Stars[0].TypeName, text);
            return;
        }
        Assert.Fail("no system found");
    }
}
