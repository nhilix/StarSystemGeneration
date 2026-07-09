using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Stateless roll source for the epoch frame: every draw is a pure
/// hash keyed (step, actor id, channel) — P6's determinism discipline at the
/// generational clock (play speed swaps step for tick, same keying).</summary>
public static class EpochRolls
{
    public static double NextDouble(ulong masterSeed, RollChannel channel,
                                    int step, int actorId, int subIndex = 0)
    {
        ulong idx = ((ulong)(uint)actorId << 32) | (uint)subIndex;
        ulong h = StableHash.Mix(masterSeed, (ulong)channel, (uint)step, idx);
        return (h >> 11) * (1.0 / (1UL << 53)); // top 53 bits -> [0,1)
    }

    public static int NextInt(ulong masterSeed, RollChannel channel,
                              int step, int actorId, int minInclusive, int maxExclusive,
                              int subIndex = 0) =>
        minInclusive + (int)(NextDouble(masterSeed, channel, step, actorId, subIndex)
                             * (maxExclusive - minInclusive));
}
