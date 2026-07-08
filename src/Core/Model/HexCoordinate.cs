using System;

namespace StarGen.Core.Model;

/// <summary>Axial hex coordinate (flat-top orientation). Two ints, so equality,
/// hashing, and RollContext's ulong packing behave exactly as before.</summary>
public readonly struct HexCoordinate : IEquatable<HexCoordinate>
{
    public int Q { get; }
    public int R { get; }
    public HexCoordinate(int q, int r) { Q = q; R = r; }

    public bool Equals(HexCoordinate other) => Q == other.Q && R == other.R;
    public override bool Equals(object? obj) => obj is HexCoordinate h && Equals(h);
    public override int GetHashCode() => (Q * 397) ^ R;
    public override string ToString() => $"({Q},{R})";
}
