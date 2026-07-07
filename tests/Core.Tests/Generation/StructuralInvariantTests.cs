using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;
using Xunit;

namespace StarGen.Core.Tests.Generation;

/// <summary>Spec §9 structural invariants over a large sample.</summary>
public class StructuralInvariantTests
{
    private const ulong Seed = 99;
    private const int Hexes = 5000;

    [Fact]
    public void AllInvariants_HoldOverLargeSample()
    {
        for (int x = 0; x < Hexes; x++)
        {
            var coord = new HexCoordinate(x % 100, x / 100);
            var result = Generator.Generate(Seed, coord);

            // determinism: full snapshot identical on regeneration
            Assert.Equal(SystemFormatter.Format(result),
                         SystemFormatter.Format(Generator.Generate(Seed, coord)));

            if (result.System == null) continue;
            var s = result.System;

            Assert.False(string.IsNullOrEmpty(s.Designation));

            foreach (var star in s.Stars.Skip(1))
                Assert.NotNull(star.CompanionSlotIndex); // exactly one primary

            // Companions truly occupy their slot (spec §5): unique, valid index
            // into the primary's slots, and that primary slot has no body of its own.
            var primary = s.Stars[0];
            var companionIndices = s.Stars.Skip(1)
                .Select(st => st.CompanionSlotIndex!.Value)
                .ToList();
            Assert.Equal(companionIndices.Count, companionIndices.Distinct().Count());
            foreach (var companionIndex in companionIndices)
            {
                Assert.InRange(companionIndex, 0, primary.Slots.Count - 1);
                var primarySlot = primary.Slots.Single(sl => sl.Index == companionIndex);
                Assert.Null(primarySlot.Body);
            }

            foreach (var body in s.Stars.SelectMany(st => st.Slots)
                                        .Where(sl => sl.Body != null)
                                        .Select(sl => sl.Body!))
            {
                // society present exactly when inhabited
                foreach (var b in body.Satellites.Prepend(body))
                    Assert.Equal(b.IsInhabited, b.Society != null);
                // no satellite recursion
                foreach (var sat in body.Satellites)
                    Assert.Empty(sat.Satellites);
                // wreckage only via overlay
                if (body.Kind == BodyKind.Wreckage)
                    Assert.Equal("derelict_fleet", s.OverlayId);
            }
        }
    }
}
