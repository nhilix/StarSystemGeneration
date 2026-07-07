using System;
using System.Collections.Generic;

namespace StarGen.Core.Tables;

/// <summary>
/// Immutable weighted lookup: content is data, cross-influence is a call-site
/// modifier multiplying weights at draw time (spec §5; plan resolution #1).
/// </summary>
public sealed class WeightedTable<T>
{
    private readonly (T Item, double Weight)[] _entries;

    public IReadOnlyList<(T Item, double Weight)> Entries => _entries;

    public WeightedTable(params (T Item, double Weight)[] entries)
    {
        if (entries.Length == 0)
            throw new ArgumentException("Weighted table requires at least one entry.");
        double total = 0;
        foreach (var (_, weight) in entries)
        {
            if (weight < 0) throw new ArgumentException("Weights must be non-negative.");
            total += weight;
        }
        if (total <= 0) throw new ArgumentException("Total weight must be positive.");
        _entries = entries;
    }

    public T Pick(double roll01) => Pick(roll01, null);

    public T Pick(double roll01, Func<T, double>? modifier)
    {
        double total = 0;
        foreach (var (item, weight) in _entries)
            total += weight * (modifier?.Invoke(item) ?? 1.0);
        if (total <= 0)
            throw new InvalidOperationException("All weights are zero after applying modifier.");

        double target = roll01 * total, acc = 0;
        foreach (var (item, weight) in _entries)
        {
            acc += weight * (modifier?.Invoke(item) ?? 1.0);
            if (target < acc) return item;
        }
        return _entries[_entries.Length - 1].Item; // roll01 == upper edge
    }
}
