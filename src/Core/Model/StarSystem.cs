using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class StarSystem
{
    public string Designation { get; }
    public string? GivenName { get; set; }
    public StarArrangement Arrangement { get; set; }
    public List<Star> Stars { get; } = new();
    public string? OverlayId { get; set; }
    public List<string> Tags { get; } = new();

    public StarSystem(string designation) => Designation = designation;
}
