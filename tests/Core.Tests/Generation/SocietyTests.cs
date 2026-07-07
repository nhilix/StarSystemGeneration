using System.Linq;
using StarGen.Core.Model;
using Xunit;
using static StarGen.Core.Tests.Generation.BodyGeneratorTests;

namespace StarGen.Core.Tests.Generation;

public class SocietyTests
{
    [Fact]
    public void Society_PresentExactlyWhenInhabited()
    {
        foreach (var body in SampleBodies(21, 2000))
        {
            foreach (var b in body.Satellites.Prepend(body))
            {
                if (b.IsInhabited) Assert.NotNull(b.Society);
                else Assert.Null(b.Society);
            }
        }
    }

    [Fact]
    public void PopulationTier_MatchesSettlementScale()
    {
        foreach (var b in SampleBodies(21, 2000).Where(b => b.Society != null))
        {
            var (min, max) = b.Settlement switch
            {
                Settlement.Outpost => (1, 3),
                Settlement.Colony => (3, 6),
                Settlement.MajorWorld => (6, 9),
                _ => (4, 9), // native sapient
            };
            Assert.InRange(b.Society!.PopulationTier, min, max);
        }
    }

    [Fact]
    public void Governments_Vary()
    {
        var governments = SampleBodies(21, 3000)
            .Where(b => b.Society != null)
            .Select(b => b.Society!.Government).Distinct().ToList();
        Assert.True(governments.Count >= 4, $"only {governments.Count} distinct governments in sample");
    }
}
