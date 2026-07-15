namespace StarGen.Core.Epoch;

/// <summary>A body address inside a hex's system: which star, which orbit
/// slot. None (-1,-1) is the deep-space station orbit — a port or facility
/// with no body to dock at (bodiless system, empty reach). The epoch layer
/// owns this type; Atlas reuses it (locality slice §1: Atlas depends on
/// Epoch, never the reverse).</summary>
public readonly record struct BodyRef(int StarIndex, int SlotIndex)
{
    public static readonly BodyRef None = new(-1, -1);
    public bool IsNone => StarIndex < 0 || SlotIndex < 0;
}
