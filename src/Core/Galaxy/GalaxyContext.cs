namespace StarGen.Core.Galaxy;

/// <summary>Generation handle: config + (later) skeleton. Flatspace = Phase 1 behavior (spec §8).</summary>
public sealed class GalaxyContext
{
    public GalaxyConfig Config { get; }
    public bool IsFlatspace { get; }

    public GalaxyContext(GalaxyConfig config)
    {
        Config = config;
        IsFlatspace = false;
    }

    private GalaxyContext(GalaxyConfig config, bool flatspace)
    {
        Config = config;
        IsFlatspace = flatspace;
    }

    public static GalaxyContext Flatspace(ulong masterSeed) =>
        new(new GalaxyConfig { MasterSeed = masterSeed }, flatspace: true);
}
