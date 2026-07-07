using System.Linq;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Naming;

public class NamingTests
{
    [Fact]
    public void OnlyInhabitedSystems_GetGivenNames()
    {
        int named = 0, checkedSystems = 0;
        for (int x = 0; x < 2000; x++)
        {
            var r = Generator.Generate(31, new HexCoordinate(x % 100, x / 100));
            if (r.System == null) continue;
            checkedSystems++;
            bool inhabited = r.System.Stars.SelectMany(s => s.Slots)
                .Where(sl => sl.Body != null)
                .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!))
                .Any(b => b.IsInhabited);
            bool notable = r.System.OverlayId != null;
            if (r.System.GivenName != null) named++;
            // Named if inhabited OR notable (via overlay)
            Assert.Equal(inhabited || notable, r.System.GivenName != null);
        }
        Assert.True(named > 0 && named < checkedSystems, "names must be neither universal nor absent");
    }

    [Fact]
    public void GivenNames_AreDeterministic_AndPresentable()
    {
        for (int x = 0; x < 500; x++)
        {
            var a = Generator.Generate(31, new HexCoordinate(x, 2)).System;
            var b = Generator.Generate(31, new HexCoordinate(x, 2)).System;
            Assert.Equal(a?.GivenName, b?.GivenName);
            if (a?.GivenName is string name)
            {
                Assert.True(char.IsUpper(name[0]));
                Assert.InRange(name.Length, 3, 16);
            }
        }
    }

    [Fact]
    public void InhabitedBodies_GetDerivedNames()
    {
        for (int x = 0; x < 2000; x++)
        {
            var system = Generator.Generate(31, new HexCoordinate(x % 100, x / 100)).System;
            if (system?.GivenName == null) continue;
            foreach (var slot in system.Stars.SelectMany(s => s.Slots))
            {
                if (slot.Body == null) continue;
                if (slot.Body.IsInhabited)
                {
                    Assert.NotNull(slot.Body.Name);
                    Assert.StartsWith(system.GivenName, slot.Body.Name);
                }
            }
        }
    }
}
