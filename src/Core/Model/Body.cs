using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Body
{
    public BodyKind Kind { get; set; }
    public int Size { get; set; }
    public Atmosphere Atmosphere { get; set; }
    public int Hydrographics { get; set; }      // 0-100 surface coverage %
    public Biosphere Biosphere { get; set; }
    public Settlement Settlement { get; set; }
    public Society? Society { get; set; }
    public List<Body> Satellites { get; } = new();
    public List<string> Tags { get; } = new();
    public string? Name { get; set; }

    /// <summary>Society exists when settled or natively sapient (spec §5).</summary>
    public bool IsInhabited => Settlement != Settlement.None || Biosphere == Biosphere.Sapient;
}
