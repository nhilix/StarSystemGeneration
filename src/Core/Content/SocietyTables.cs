using System;
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>First-draft society content — original archetypes (spec §2).</summary>
public static class SocietyTables
{
    public static readonly WeightedTable<string> Government = new(
        ("council rule", 20),
        ("free assembly", 15),
        ("charter company", 15),
        ("steward dynasty", 12),
        ("autonomous collective", 10),
        ("faith communion", 10),
        ("warlord compact", 8),
        ("no rule", 8),
        ("machine regency", 2));

    public static readonly WeightedTable<OrderTier> Order = new(
        (OrderTier.Lawless, 10), (OrderTier.Loose, 25), (OrderTier.Orderly, 35),
        (OrderTier.Strict, 20), (OrderTier.Regimented, 10));

    public static readonly WeightedTable<PortTier> Port = new(
        (PortTier.None, 30), (PortTier.Field, 30), (PortTier.Station, 25),
        (PortTier.Orbital, 12), (PortTier.Nexus, 3));

    public static Func<PortTier, double> PortModifier(int populationTier) => port =>
        (populationTier, port) switch
        {
            ( >= 7, PortTier.Nexus) => 4.0,
            ( >= 7, PortTier.Orbital) => 2.0,
            ( >= 7, PortTier.None) => 0.1,
            ( <= 2, PortTier.Orbital) => 0.2,
            ( <= 2, PortTier.Nexus) => 0.0,
            _ => 1.0,
        };
}
