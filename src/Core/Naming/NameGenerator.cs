using System.Globalization;
using System.Linq;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Naming;

public static class NameGenerator
{
    private static readonly string[] Romans =
        { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV" };

    public static void AssignNames(RollContext ctx, StarSystem system)
    {
        bool inhabited = system.Stars.SelectMany(s => s.Slots)
            .Where(sl => sl.Body != null)
            .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!))
            .Any(b => b.IsInhabited);
        if (!inhabited) return;

        EnsureNamed(ctx, system);

        foreach (var slot in system.Stars.SelectMany(s => s.Slots))
        {
            if (slot.Body == null || !slot.Body.Satellites.Prepend(slot.Body).Any(b => b.IsInhabited))
                continue;
            slot.Body.Name = $"{system.GivenName} {Romans[slot.Index % Romans.Length]}";
            for (int s = 0; s < slot.Body.Satellites.Count; s++)
                if (slot.Body.Satellites[s].IsInhabited)
                    slot.Body.Satellites[s].Name = $"{slot.Body.Name}-{(char)('a' + s)}";
        }
    }

    /// <summary>Names a system if unnamed — also used when an overlay marks it notable.</summary>
    public static void EnsureNamed(RollContext ctx, StarSystem system)
    {
        if (system.GivenName != null) return;
        int syllables = ctx.NextInt(RollChannel.NameLength, 2, 4);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(ctx.NextDouble(RollChannel.NameSyllable, 0, i));
        system.GivenName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
