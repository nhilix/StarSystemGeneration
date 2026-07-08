using StarGen.Core.Model;

namespace StarGen.Core.Rng;

/// <summary>Stateless roll source: every draw is a pure hash (spec §8).</summary>
public readonly struct RollContext
{
    private readonly ulong _masterSeed;
    public HexCoordinate Coordinate { get; }

    public RollContext(ulong masterSeed, HexCoordinate coordinate)
    {
        _masterSeed = masterSeed;
        Coordinate = coordinate;
    }

    public double NextDouble(RollChannel channel, int index = 0, int subIndex = 0)
    {
        ulong coord = ((ulong)(uint)Coordinate.Q << 32) | (uint)Coordinate.R;
        ulong idx = ((ulong)(uint)index << 32) | (uint)subIndex;
        ulong h = StableHash.Mix(_masterSeed, coord, (ulong)channel, idx);
        return (h >> 11) * (1.0 / (1UL << 53)); // top 53 bits -> [0,1)
    }

    public int NextInt(RollChannel channel, int minInclusive, int maxExclusive,
                       int index = 0, int subIndex = 0) =>
        minInclusive + (int)(NextDouble(channel, index, subIndex) * (maxExclusive - minInclusive));
}
