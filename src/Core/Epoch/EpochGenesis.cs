using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Seeds the epoch sim from the emergence schedule (slice F): one
/// polity actor per current-era sapient origin, entering in spaceflight-date
/// order with the dates projected onto the emergence window — staggered
/// entry is causal residue of the evolutionary clock, not a roll (channel
/// 40's stub retired). The deep-time chronicle seeds the event log so one
/// history reads bottom-to-top. Homeworlds are simply the first ports: the
/// port itself is founded at entry, by the Interior phase.</summary>
public static class EpochGenesis
{
    /// <summary>Entry-grade bonus per point of origin biosphere richness —
    /// maturation quality shows in the starting kit.</summary>
    private const double RichnessGradeBonus = 0.05;
    /// <summary>Entry-grade bonus at the window's end — the late-emerger
    /// contact bonus: latecomers matured under a sky full of foreign
    /// traffic (elevated starting Astrogation/Industrial); behind, not
    /// hopeless (life-and-precursors.md §Starting conditions).</summary>
    private const double ContactGradeBonus = 0.10;

    public static SimState Seed(GalaxySkeleton skeleton, EpochSimConfig config)
    {
        var state = new SimState(config, skeleton);
        // one culture per species (id == species id) until a split mechanic
        // lands — the slow identity layer's registry
        foreach (var sp in skeleton.Species)
            state.Cultures.Add(new Culture(sp.Id, sp.Name, sp.Id));

        // the deep-time chronicle is the log's bottom stratum
        foreach (var e in skeleton.DeepTimeEvents)
            state.Log.Append(e.WorldYear, e.Stratum, e.Type, e.Actors,
                e.Location, e.Magnitude, e.Valence, e.Visibility, e.Payload);

        // current-era origins: species ids follow origin-id order
        // (PassSpecies); entry order follows the spaceflight dates
        var current = new List<SapientOrigin>();
        var speciesByOrigin = new Dictionary<int, int>();
        foreach (var origin in skeleton.Origins)
        {
            if (origin.Era != OriginEra.Current) continue;
            speciesByOrigin[origin.Id] = current.Count;
            current.Add(origin);
        }
        current.Sort((a, b) => a.SpaceflightYear != b.SpaceflightYear
            ? a.SpaceflightYear.CompareTo(b.SpaceflightYear)
            : a.Id.CompareTo(b.Id));

        long minDate = current.Count > 0 ? current[0].SpaceflightYear : 0;
        long maxDate = current.Count > 0
            ? current[current.Count - 1].SpaceflightYear : 0;
        int window = config.Genesis.EmergenceWindowYears;

        foreach (var origin in current)
        {
            int id = state.Actors.Count;
            // dates project onto the window preserving order and relative
            // spacing — honest narrative compression (frame/time.md)
            double fraction = maxDate > minDate
                ? (origin.SpaceflightYear - minDate) / (double)(maxDate - minDate)
                : 0.0;
            // the entry date is stored as the world-year it is (P7, slice MC):
            // genesis reads no clock, so the schedule cannot be denominated in
            // whatever step happened to be configured when Seed ran
            int entryYear = (int)Math.Round(fraction * window);

            int speciesId = speciesByOrigin[origin.Id];
            state.Actors.Add(new Actor(id, ActorKind.Polity,
                skeleton.Species[speciesId].Name, origin.Hex, entryYear,
                new GenesisController(config)));
            state.Polities.Add(new PolityRecord(id, speciesId)
            {
                EntryGradeBonus = RichnessGradeBonus * origin.Richness
                                  + ContactGradeBonus * fraction,
            });
        }
        return state;
    }
}
