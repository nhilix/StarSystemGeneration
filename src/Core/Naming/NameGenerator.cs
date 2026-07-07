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

        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            foreach (var slot in system.Stars[starIndex].Slots)
            {
                if (slot.Body == null || !slot.Body.Satellites.Prepend(slot.Body).Any(b => b.IsInhabited))
                    continue;
                string roman = Romans[slot.Index % Romans.Length];
                // Star-scope the name so bodies at the same slot index under different
                // stars in the same system don't collide (e.g. "Veshara III" twice).
                slot.Body.Name = starIndex == 0
                    ? $"{system.GivenName} {roman}"
                    : $"{system.GivenName} {(char)('A' + starIndex)}-{roman}";
                for (int s = 0; s < slot.Body.Satellites.Count; s++)
                    if (slot.Body.Satellites[s].IsInhabited)
                        slot.Body.Satellites[s].Name = $"{slot.Body.Name}-{(char)('a' + s)}";
            }
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
