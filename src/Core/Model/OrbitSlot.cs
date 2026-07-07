namespace StarGen.Core.Model;

public sealed class OrbitSlot
{
    public int Index { get; set; }
    public OrbitBand Band { get; set; }
    public Body? Body { get; set; }
}
