namespace StarGen.Core.Rng;

/// <summary>Deterministic mixing for stateless roll derivation (spec §8).</summary>
public static class StableHash
{
    public static ulong Mix(ulong a, ulong b, ulong c, ulong d)
    {
        ulong h = SplitMix64(a);
        h = SplitMix64(h ^ b);
        h = SplitMix64(h ^ c);
        h = SplitMix64(h ^ d);
        return h;
    }

    private static ulong SplitMix64(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
