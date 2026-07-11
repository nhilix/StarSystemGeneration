using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>What one actor believes about one other polity — the compressed
/// snapshot behind the stale-able RelationBrief fields (perception-and-news.md
/// §Compressed beliefs). The snapshot IS the stale value: it refreshes when
/// the news delay allows and freezes between refreshes; no truth history is
/// kept. Serialized (belief layer) — LoadThenContinue must not re-survey.</summary>
public sealed class PolityBelief
{
    public int SubjectId { get; }
    /// <summary>World-year the snapshot was taken.</summary>
    public long HeardYear { get; set; }
    /// <summary>The subject's headline war weight, as heard.</summary>
    public double Strength { get; set; }
    /// <summary>Subject + everyone bound to defend it, as heard.</summary>
    public double DefensiveStrength { get; set; }
    /// <summary>The casus-belli menu against the subject, as heard — a
    /// declaration can arm on stale facts (Resolution re-grounds on truth).</summary>
    public List<CasusBelliOption> Menu { get; } = new List<CasusBelliOption>();
    /// <summary>War-target enumeration against the subject, as heard.</summary>
    public List<WarObjectiveSpec> ObjectiveCandidates { get; }
        = new List<WarObjectiveSpec>();

    public PolityBelief(int subjectId)
    {
        SubjectId = subjectId;
    }
}

/// <summary>What a belligerent believes about a war it is in — the front
/// reports arrive with the news, so a distant loser doesn't yet know it is
/// losing and wars run past their rational end (P3).</summary>
public sealed class WarBelief
{
    public int WarId { get; }
    public long HeardYear { get; set; }
    public double OwnSideExhaustion { get; set; }
    public double OwnSideStrengthShare { get; set; }
    public int ObjectivesTaken { get; set; }

    public WarBelief(int warId)
    {
        WarId = warId;
    }
}

/// <summary>What a host polity believes a chartered corporation is worth —
/// the books are wherever the headquarters is.</summary>
public sealed class CorpBelief
{
    public int CorpId { get; }
    public long HeardYear { get; set; }
    public double Credits { get; set; }

    public CorpBelief(int corpId)
    {
        CorpId = corpId;
    }
}

/// <summary>One actor's compressed believed world (P3): belief snapshots per
/// subject, in sorted-key order everywhere (P6). Lives on the Actor and
/// serializes with it — the PerceptionView built FROM it stays transient.</summary>
public sealed class BeliefState
{
    /// <summary>Belief per known polity, subject-id order.</summary>
    public SortedList<int, PolityBelief> Polities { get; }
        = new SortedList<int, PolityBelief>();
    /// <summary>Belief per war this actor is (or was) in, war-id order.</summary>
    public SortedList<int, WarBelief> Wars { get; }
        = new SortedList<int, WarBelief>();
    /// <summary>Belief per hosted corporation, corp-id order.</summary>
    public SortedList<int, CorpBelief> Corporations { get; }
        = new SortedList<int, CorpBelief>();
}
