using System.Collections.Generic;
using static System.FormattableString;

namespace StarGen.Core.Epoch;

/// <summary>One detected era: a contiguous run of epochs sharing a dominant
/// event signature, named from its participants. A recomputable annotation
/// over the log — never sim state (chronicle-and-poi.md §Chronicle views).</summary>
public sealed record Era(
    int StartEpoch, int EndEpoch, long StartYear, long EndYear,
    EraKind Kind, string Name);

public enum EraKind { Quiet, Expansion, Treaty, Upheaval, War }

/// <summary>Era detection (P8): cluster epochs by dominant event signature
/// (expansion-heavy, war-dense, treaty-dense, upheaval), merge consecutive
/// runs, and name each era from the polities that made it. Deterministic:
/// fixed weights, fixed tie order, names from registry ids.</summary>
public static class EraDetector
{
    private const int WarWeight = 3, UpheavalWeight = 2, TreatyWeight = 2,
        ExpansionWeight = 1;
    /// <summary>Weighted score an epoch needs before it stops reading as
    /// quiet years.</summary>
    private const int SignatureFloor = 3;

    public static List<Era> Detect(SimState state)
    {
        var eras = new List<Era>();
        // era buckets are generations, not integration steps (P7):
        // a fine-tick continuation clusters into the same calendar
        int years = state.Config.Sim.GenerationYears;
        if (years <= 0) return eras;
        int epochs = (state.WorldYear + years - 1) / years;
        if (epochs <= 0) return eras;

        // per-epoch weighted signature from the generational stream
        var scores = new int[epochs, 4];   // war, upheaval, treaty, expansion
        foreach (var e in state.Log.Events)
        {
            if (e.Stratum != ClockStratum.Generational || e.WorldYear < 0)
                continue;
            int epoch = (int)(e.WorldYear / years);
            if (epoch >= epochs) epoch = epochs - 1;
            int bucket = BucketOf(e.Type);
            if (bucket >= 0) scores[epoch, bucket] += WeightOf(bucket);
        }

        var kinds = new EraKind[epochs];
        for (int i = 0; i < epochs; i++)
        {
            int best = -1, bestScore = SignatureFloor - 1;
            for (int b = 0; b < 4; b++)      // war > upheaval > treaty > exp
                if (scores[i, b] > bestScore) { bestScore = scores[i, b]; best = b; }
            kinds[i] = best switch
            {
                0 => EraKind.War,
                1 => EraKind.Upheaval,
                2 => EraKind.Treaty,
                3 => EraKind.Expansion,
                _ => EraKind.Quiet,
            };
        }

        // merge consecutive runs and name each from its participants
        var seen = new Dictionary<string, int>();
        int start = 0;
        for (int i = 1; i <= epochs; i++)
        {
            if (i < epochs && kinds[i] == kinds[start]) continue;
            string name = NameEra(state, kinds[start], start, i - 1, years);
            if (seen.TryGetValue(name, out int nth))
            {
                seen[name] = nth + 1;
                name += Invariant($" {Numeral(nth + 1)}");
            }
            else seen[name] = 1;
            eras.Add(new Era(start, i - 1, (long)start * years,
                             (long)i * years, kinds[start], name));
            start = i;
        }
        return eras;
    }

    /// <summary>The era annotation for one epoch, or null outside every era.</summary>
    public static Era? EraOf(IReadOnlyList<Era> eras, int epoch)
    {
        foreach (var era in eras)
            if (epoch >= era.StartEpoch && epoch <= era.EndEpoch) return era;
        return null;
    }

    private static int BucketOf(WorldEventType type) => type switch
    {
        WorldEventType.WarDeclared or WorldEventType.BattleFought
            or WorldEventType.SiegeBegun or WorldEventType.PortCaptured => 0,
        WorldEventType.SchismDeclared or WorldEventType.CoupStruck
            or WorldEventType.RevoltCrushed or WorldEventType.GovernmentReformed
            or WorldEventType.EmergenceSuppressed => 1,
        WorldEventType.TreatySigned or WorldEventType.FederationFormed
            or WorldEventType.VassalageBound or WorldEventType.DynasticInstrument
            or WorldEventType.PeaceSettled => 2,
        WorldEventType.PortEstablished or WorldEventType.LaneOpened
            or WorldEventType.PolityEmerged => 3,
        _ => -1,
    };

    private static int WeightOf(int bucket) => bucket switch
    {
        0 => WarWeight, 1 => UpheavalWeight, 2 => TreatyWeight,
        _ => ExpansionWeight,
    };

    /// <summary>Names come from the participants: the two loudest polities
    /// of a war era title its wars; the busiest founder titles an
    /// expansion. Quiet stretches earn "the Long Peace" only when long.</summary>
    private static string NameEra(SimState state, EraKind kind, int startEpoch,
                                  int endEpoch, int years)
    {
        var (first, second) = LoudestParticipants(state, kind, startEpoch,
                                                  endEpoch, years);
        return kind switch
        {
            EraKind.War => first != null && second != null
                ? $"The {first}–{second} Wars"
                : first != null ? $"The {first} Wars" : "The Burning Years",
            EraKind.Treaty => first != null && second != null
                ? $"The Concord of {first} and {second}"
                : "The Great Concord",
            EraKind.Expansion => first != null
                ? $"The {first} Expansion" : "The Great Expansion",
            EraKind.Upheaval => "The Age of Upheaval",
            _ => endEpoch - startEpoch + 1 >= 4
                ? "The Long Peace" : "The Quiet Years",
        };
    }

    private static (string? First, string? Second) LoudestParticipants(
        SimState state, EraKind kind, int startEpoch, int endEpoch, int years)
    {
        int bucket = kind switch
        {
            EraKind.War => 0, EraKind.Upheaval => 1, EraKind.Treaty => 2,
            EraKind.Expansion => 3, _ => -1,
        };
        if (bucket < 0) return (null, null);
        var counts = new SortedList<int, int>();
        foreach (var e in state.Log.Events)
        {
            if (e.Stratum != ClockStratum.Generational || e.WorldYear < 0)
                continue;
            int epoch = (int)(e.WorldYear / years);
            if (epoch < startEpoch || epoch > endEpoch
                || BucketOf(e.Type) != bucket) continue;
            foreach (var id in e.Actors)
            {
                if (id < 0 || id >= state.Actors.Count
                    || state.Actors[id].Kind != ActorKind.Polity) continue;
                counts.TryGetValue(id, out int c);
                counts[id] = c + 1;
            }
        }
        int firstId = -1, secondId = -1, firstCount = 0, secondCount = 0;
        for (int i = 0; i < counts.Count; i++)   // ascending id: ties → lower
        {
            int id = counts.Keys[i], c = counts.Values[i];
            if (c > firstCount)
            {
                secondId = firstId; secondCount = firstCount;
                firstId = id; firstCount = c;
            }
            else if (c > secondCount) { secondId = id; secondCount = c; }
        }
        return (firstId >= 0 ? state.Actors[firstId].Name : null,
                secondId >= 0 ? state.Actors[secondId].Name : null);
    }

    private static string Numeral(int n) => n switch
    {
        2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII",
        8 => "VIII", 9 => "IX", 10 => "X", _ => Invariant($"{n}"),
    };
}
