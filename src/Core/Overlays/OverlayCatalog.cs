using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Model;

namespace StarGen.Core.Overlays;

/// <summary>Illustrative first-draft catalog (spec §6) — additive, pure data.</summary>
public static class OverlayCatalog
{
    public static readonly IReadOnlyList<OverlayDefinition> All = new List<OverlayDefinition>
    {
        new("precursor_ruins", 3,
            isEligible: s => Worlds(s).Any(),
            apply: (ctx, s) =>
            {
                Worlds(s).First().Tags.Add("precursor ruins");
                s.Tags.Add("notable: precursor ruins");
            }),

        new("unstable_star", 2,
            isEligible: s => s.Stars[0].Age != StarAge.Mature,
            apply: (ctx, s) =>
            {
                s.Tags.Add("stellar instability");
                foreach (var b in AllBodies(s)) b.Tags.Add("hazard: stellar instability");
            }),

        new("derelict_fleet", 2,
            isEligible: s => s.Stars.SelectMany(st => st.Slots).Any(sl => sl.Body == null),
            apply: (ctx, s) =>
            {
                var slot = s.Stars.SelectMany(st => st.Slots).First(sl => sl.Body == null);
                slot.Body = new Body { Kind = BodyKind.Wreckage, Size = 0 };
                slot.Body.Tags.Add("derelict fleet");
            }),

        new("anomalous_signal", 3,
            isEligible: _ => true,
            apply: (ctx, s) => s.Tags.Add("anomalous signal")),
    };

    private static IEnumerable<Body> Worlds(StarSystem s) => AllBodies(s)
        .Where(b => b.Kind == BodyKind.RockyWorld || b.Kind == BodyKind.IceWorld);

    private static IEnumerable<Body> AllBodies(StarSystem s) => s.Stars
        .SelectMany(st => st.Slots)
        .Where(sl => sl.Body != null)
        .SelectMany(sl => sl.Body!.Satellites.Prepend(sl.Body!));
}
