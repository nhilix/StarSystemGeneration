namespace StarGen.Core.Atlas;

/// <summary>The observer context every atlas query is parameterized by
/// (unity-atlas-design.md): god reads truth registries; a controller eye
/// reads belief snapshots, news-delayed log, and fog beyond reach — through
/// the same query API. Views never know which eye is looking.</summary>
public readonly struct EyeContext
{
    /// <summary>-1 under the god eye; otherwise the observing actor.</summary>
    public int ActorId { get; }
    public long WorldYear { get; }
    public bool IsGod => ActorId < 0;

    private EyeContext(int actorId, long worldYear)
    {
        ActorId = actorId;
        WorldYear = worldYear;
    }

    public static EyeContext God(long worldYear) => new(-1, worldYear);

    /// <summary>RESERVED SEAM (play tier): the controller eye exists so no
    /// query surface bakes in god scope, but K-slice lenses answer it with
    /// god-equivalent truth until the belief-backed reads land.</summary>
    public static EyeContext Controller(int actorId, long worldYear) =>
        new(actorId, worldYear);
}
