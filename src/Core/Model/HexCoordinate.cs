using System;

namespace StarGen.Core.Model;

public readonly struct HexCoordinate : IEquatable<HexCoordinate>
{
    public int X { get; }
    public int Y { get; }
    public HexCoordinate(int x, int y) { X = x; Y = y; }

    public bool Equals(HexCoordinate other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is HexCoordinate h && Equals(h);
    public override int GetHashCode() => (X * 397) ^ Y;
    public override string ToString() => $"({X},{Y})";
}
