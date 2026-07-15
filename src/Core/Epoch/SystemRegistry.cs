using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The commit trigger behind SimState.SettledSystems (locality
/// slice §1). The hex-tier generator is a pure function of (GalaxyConfig,
/// hex), so freezing its result the first time anything touches the hex is
/// deterministic regardless of trigger order — the commit only needs to be
/// idempotent (memoize-once).</summary>
public static class SystemRegistry
{
    /// <summary>Freeze a hex's system into state the first time it is
    /// touched; return the frozen system (null for an empty reach — still
    /// recorded as settled). Idempotent: later calls return the memoized
    /// value, never regenerate.</summary>
    public static StarSystem? Commit(SimState state, HexCoordinate hex)
    {
        if (state.SettledSystems.TryGetValue(hex, out var existing))
            return existing;
        var context = new GalaxyContext(state.Skeleton.Config)
        { Skeleton = state.Skeleton };
        var system = Generator.Generate(context, hex).System;
        state.SettledSystems[hex] = system;
        return system;
    }

    public static bool IsSettled(SimState state, HexCoordinate hex) =>
        state.SettledSystems.ContainsKey(hex);
}
