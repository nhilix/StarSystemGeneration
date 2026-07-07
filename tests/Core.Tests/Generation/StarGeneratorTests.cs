using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class StarGeneratorTests
{
    private static List<StarSystem> Sample(ulong seed, int count)
    {
        var systems = new List<StarSystem>();
        for (int x = 0; systems.Count < count && x < count * 4; x++)
        {
            var r = Generator.Generate(seed, new HexCoordinate(x % 100, x / 100));
            if (r.System != null) systems.Add(r.System);
        }
        return systems;
    }

    [Fact]
    public void EverySystem_HasPrimaryWithSlots()
    {
        foreach (var s in Sample(3, 300))
        {
            Assert.NotEmpty(s.Stars);
            Assert.Null(s.Stars[0].CompanionSlotIndex);
            Assert.NotEmpty(s.Stars[0].Slots);
            Assert.False(string.IsNullOrEmpty(s.Stars[0].TypeName));
        }
    }

    [Fact]
    public void StarCount_MatchesArrangement()
    {
        foreach (var s in Sample(3, 300))
        {
            int expected = s.Arrangement switch
            {
                StarArrangement.Single => 1,
                StarArrangement.Binary => 2,
                _ => 3,
            };
            Assert.Equal(expected, s.Stars.Count);
        }
    }

    [Fact]
    public void Bands_AreOrderedInnerHabitableOuter()
    {
        foreach (var star in Sample(3, 300).SelectMany(s => s.Stars))
        {
            OrbitBand last = OrbitBand.Inner;
            foreach (var slot in star.Slots)
            {
                Assert.True(slot.Band >= last, "bands must never regress inner<-hab<-outer");
                last = slot.Band;
            }
        }
    }

    [Fact]
    public void Companions_OccupyValidPrimarySlot_AndDontNest()
    {
        foreach (var s in Sample(5, 300))
        {
            var primary = s.Stars[0];
            foreach (var companion in s.Stars.Skip(1))
            {
                Assert.NotNull(companion.CompanionSlotIndex);
                Assert.InRange(companion.CompanionSlotIndex!.Value, 0, primary.Slots.Count - 1);
                Assert.True(companion.Slots.Count <= 3);
            }
        }
    }

    [Fact]
    public void AllArrangements_Occur()
    {
        var arrangements = Sample(3, 500).Select(s => s.Arrangement).Distinct().ToList();
        Assert.Contains(StarArrangement.Single, arrangements);
        Assert.Contains(StarArrangement.Binary, arrangements);
    }
}
