using StarGen.Core.Model;

namespace StarGen.Core.Generation;

public sealed class HexResult
{
    public HexCoordinate Coordinate { get; }
    public StarSystem? System { get; }
    public bool IsEmpty => System == null;

    public HexResult(HexCoordinate coordinate, StarSystem? system)
    {
        Coordinate = coordinate;
        System = system;
    }
}
