using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Where a new segment settles within its administering port's
/// domain (locality slice §3). The arrival address is the settled port body
/// — the finest address that is cheap and always committed (the port's hex
/// is frozen the moment the port exists). A port-domain can hold several
/// segments across different bodies as colonies and outposts found their
/// own cores. Intra-domain relocation over time is deferred (design
/// boundary); only the arrival address gets finer here.</summary>
public static class PopulationSiting
{
    /// <summary>Resolve the arrival address for a resident of the port's own
    /// hex — the homeworld/colony default. Delegates to the hex overload with
    /// the port's hex.</summary>
    public static BodyRef Assign(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count) return BodyRef.None;
        return Assign(state, portId, state.Ports[portId].Hex);
    }

    /// <summary>Resolve a resident's body within an ARBITRARY domain hex's
    /// committed system, not only the port's (domain-hex-expansion design §3,
    /// "The settle election"). The satellite hex's system is committed (frozen
    /// idempotently, like the port hex) and the resolved body is that system's
    /// port body — the finest cheap always-committed address there. The
    /// <paramref name="portId"/> stays the administering domain; only the
    /// settled hex changes.</summary>
    public static BodyRef Assign(SimState state, int portId, HexCoordinate hex)
    {
        if (portId < 0 || portId >= state.Ports.Count) return BodyRef.None;
        var system = SystemRegistry.Commit(state, hex);
        return BodySiting.PortBody(system);
    }
}
