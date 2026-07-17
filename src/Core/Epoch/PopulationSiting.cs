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
    public static BodyRef Assign(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count) return BodyRef.None;
        var system = SystemRegistry.Commit(state, state.Ports[portId].Hex);
        return BodySiting.PortBody(system);
    }
}
