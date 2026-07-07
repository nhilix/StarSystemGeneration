using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Overlays;

/// <summary>Curated exotic-phenomenon definition (spec §6). Pure data + functions.</summary>
public sealed class OverlayDefinition
{
    public string Id { get; }
    public double Weight { get; }
    public Func<StarSystem, bool> IsEligible { get; }
    public Action<RollContext, StarSystem> Apply { get; }

    public OverlayDefinition(string id, double weight,
                             Func<StarSystem, bool> isEligible,
                             Action<RollContext, StarSystem> apply)
    {
        Id = id; Weight = weight; IsEligible = isEligible; Apply = apply;
    }
}
