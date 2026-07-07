using StarGen.Core.Tables;

namespace StarGen.Core.Content;

public static class NameTables
{
    public static readonly WeightedTable<string> Syllables = new(
        ("ka", 3), ("ve", 3), ("sha", 2), ("ra", 3), ("tor", 2), ("mi", 3),
        ("zen", 2), ("al", 3), ("or", 2), ("du", 2), ("ny", 2), ("bel", 2),
        ("cas", 2), ("tha", 2), ("lus", 2), ("rin", 2), ("vo", 2), ("hai", 1),
        ("mar", 2), ("sel", 2), ("qua", 1), ("dre", 2), ("no", 2), ("li", 3));
}
