using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>A lightweight settlement record founded when a population segment
/// elects to settle a worked satellite hex within a port's domain
/// (domain-hex-expansion design §3, "The outpost record"). It exists for
/// fiction, the REPL, and metrics — it is <b>not an actor</b>: no treasury, no
/// market, no service radius, no controller. Its residents are still
/// administered by, and trade through, the parent port's market. Held in
/// <see cref="SimState.Outposts"/> in id order (P6); a new <c>outposts</c>
/// serializer layer carries it.</summary>
public sealed record Outpost(int Id, string Name, HexCoordinate Hex,
    int ParentPortId, long FoundingYear)
{
    /// <summary>Flipped once, at Stage-3 frontier graduation, when this outpost
    /// is promoted into a real starport — it then stays in the registry as
    /// history, no longer a candidate. Mutable by design (the only field that
    /// changes after founding).</summary>
    public bool Graduated { get; set; }
}
