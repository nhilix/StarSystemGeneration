using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Society
{
    public int PopulationTier { get; set; }     // 0-9
    public string Government { get; set; } = "";
    public OrderTier Order { get; set; }
    public PortTier Port { get; set; }
    public List<string> PointsOfInterest { get; } = new();
}
