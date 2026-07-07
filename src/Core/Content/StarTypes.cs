using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

public sealed class StarTypeDef
{
    public string Id { get; }
    public string DisplayName { get; }
    public int MinSlots { get; }
    public int MaxSlots { get; }   // inclusive
    public int HabStart { get; }   // slot index; -1 = no habitable band
    public int HabEnd { get; }     // inclusive

    public StarTypeDef(string id, string displayName, int minSlots, int maxSlots,
                       int habStart, int habEnd)
    {
        Id = id; DisplayName = displayName;
        MinSlots = minSlots; MaxSlots = maxSlots;
        HabStart = habStart; HabEnd = habEnd;
    }
}

/// <summary>First-draft star content — tunable data, original terminology.</summary>
public static class StarTypes
{
    public static readonly WeightedTable<StarTypeDef> Table = new(
        (new StarTypeDef("ember_dwarf",    "ember dwarf",        3,  6, 1, 1), 30),
        (new StarTypeDef("amber_dwarf",    "amber dwarf",        4,  8, 2, 3), 25),
        (new StarTypeDef("gold_main",      "gold main-sequence", 5, 10, 3, 4), 20),
        (new StarTypeDef("white_blaze",    "white blaze",        6, 11, 5, 6), 10),
        (new StarTypeDef("blue_titan",     "blue titan",         6, 12, 8, 9),  4),
        (new StarTypeDef("ashen_remnant",  "ashen remnant",      2,  5, -1, -1), 8),
        (new StarTypeDef("collapsed_core", "collapsed core",     1,  4, -1, -1), 3));

    public static readonly WeightedTable<StarArrangement> Arrangement = new(
        (StarArrangement.Single, 70),
        (StarArrangement.Binary, 25),
        (StarArrangement.Trinary, 5));

    public static readonly WeightedTable<StarAge> Age = new(
        (StarAge.Young, 20),
        (StarAge.Mature, 55),
        (StarAge.Old, 25));
}
