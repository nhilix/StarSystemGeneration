using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The incremental POI compiler (chronicle-and-poi.md §The POI
/// compiler): runs inside Chronicle every epoch, converting that epoch's
/// qualifying residue into anchored POIs immediately — battlefields from
/// wreckage concentrations, ruins from dead cities, fallen capitals from
/// annihilation settlements, memorials from famines and atrocities, and
/// precursor sites charted as expansion reaches them. One live anchor per
/// hex by magnitude; overflow stays place-history. Artifact finalization
/// compiles nothing — the map is always current.</summary>
public static class PoiCompiler
{
    /// <summary>One epoch's compilation. Returns the formation events for
    /// Chronicle to finalize (they join the log and may pulse).</summary>
    public static List<StagedEvent> Compile(SimState state)
    {
        var events = new List<StagedEvent>();
        CompileBattlefields(state, events);
        CompileRuins(state, events);
        CompileFallenCapitals(state, events);
        CompileMemorials(state, events);
        ChartPrecursorSites(state, events);
        Decay(state);
        return events;
    }

    /// <summary>The live anchor at a hex, or null — one per hex (P1).</summary>
    public static PoiRecord? LiveAt(SimState state, HexCoordinate hex)
    {
        foreach (var poi in state.Pois)                   // id order (P6)
            if (!poi.Depleted && poi.Hex.Equals(hex)) return poi;
        return null;
    }

    /// <summary>Claim a hex for a new POI: an existing smaller anchor is
    /// superseded (kept as history); an equal-or-bigger one wins and the
    /// candidate stays place-history annotation (returns null).</summary>
    private static PoiRecord? Anchor(SimState state, PoiType type,
        HexCoordinate hex, double magnitude, int subjectId = -1, int detail = 0)
    {
        var standing = LiveAt(state, hex);
        if (standing != null)
        {
            if (standing.Magnitude >= magnitude) return null;
            standing.Depleted = true;                     // superseded
        }
        var poi = new PoiRecord(state.Pois.Count, type, hex, magnitude,
                                state.WorldYear, subjectId, detail);
        state.Pois.Add(poi);
        return poi;
    }

    /// <summary>Major battles leave wreckage fields: hexes whose wreckage
    /// crosses the hull floor anchor a battlefield with salvage value; the
    /// field grows while wars grind over the same ground.</summary>
    private static void CompileBattlefields(SimState state,
                                            List<StagedEvent> events)
    {
        var knobs = state.Config.Poi;
        // hulls per hex in first-wreck order (P6)
        var hexes = new List<HexCoordinate>();
        var hulls = new Dictionary<HexCoordinate, int>();
        foreach (var wr in state.Wreckage)                // id order (P6)
        {
            if (!hulls.TryGetValue(wr.Hex, out int sum)) hexes.Add(wr.Hex);
            hulls[wr.Hex] = sum + wr.Hulls;
        }
        foreach (var hex in hexes)
        {
            int total = hulls[hex];
            if (total < knobs.BattlefieldHullFloor) continue;
            var standing = LiveAt(state, hex);
            if (standing != null && standing.Type == PoiType.Battlefield)
            {
                // the field grows; already-salvaged hulls stay drawn
                if (total > standing.Magnitude) standing.Magnitude = total;
                continue;
            }
            var poi = Anchor(state, PoiType.Battlefield, hex, total);
            if (poi == null) continue;
            AddParticipants(state, poi, hex);
            AddSources(state, poi, hex, WorldEventType.BattleFought,
                       WorldEventType.FleetAttrition);
            events.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.BattlefieldMarked, poi.ParticipantActorIds,
                hex, Magnitude: total, Valence: -0.4, EventVisibility.Public,
                new BattlefieldMarkedPayload(poi.Id, total)));
        }
    }

    /// <summary>Owners of the hulls lying at a hex, ascending actor id.</summary>
    private static void AddParticipants(SimState state, PoiRecord poi,
                                        HexCoordinate hex)
    {
        var owners = new SortedSet<int>();
        foreach (var wr in state.Wreckage)
            if (wr.Hex.Equals(hex))
                owners.Add(state.Designs[wr.DesignId].OwnerActorId);
        poi.ParticipantActorIds.AddRange(owners);
    }

    /// <summary>This epoch's qualifying log events at the hex — the source
    /// trail (debris with a date and factions you can look up).</summary>
    private static void AddSources(SimState state, PoiRecord poi,
        HexCoordinate hex, params WorldEventType[] types)
    {
        var log = state.Log.Events;
        for (int i = log.Count - 1; i >= 0; i--)
        {
            var e = log[i];
            if (e.WorldYear < state.WorldYear) break;
            if (!e.Location.Equals(hex)) continue;
            foreach (var t in types)
                if (e.Type == t) { poi.SourceEventIds.Add(e.Id); break; }
        }
        poi.SourceEventIds.Reverse();                     // log order
    }

    /// <summary>Dead cities: a standing port whose people are gone (and
    /// stayed gone past the grace window) anchors ruins — suppressed
    /// settlement, a piracy shadow, salvage in the walls.</summary>
    private static void CompileRuins(SimState state, List<StagedEvent> events)
    {
        var knobs = state.Config.Poi;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (port.FoundedYear + knobs.RuinsDeadEpochs * years
                > state.WorldYear) continue;
            if (Population(state, port.Id) >= 0.01) continue;
            var standing = LiveAt(state, port.Hex);
            if (standing != null && standing.Type == PoiType.Ruins
                && standing.SubjectId == port.Id) continue;
            var poi = Anchor(state, PoiType.Ruins, port.Hex, port.Tier,
                             port.Id);
            if (poi == null) continue;
            poi.ParticipantActorIds.Add(port.OwnerActorId);
            events.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.RuinsFallSilent, new[] { port.OwnerActorId },
                port.Hex, Magnitude: port.Tier, Valence: -0.5,
                EventVisibility.Regional,
                new RuinsFallSilentPayload(poi.Id, port.Id)));
        }
    }

    private static double Population(SimState state, int portId)
    {
        double total = 0;
        foreach (var s in state.Segments)
            if (s.PortId == portId) total += s.Size;
        return total;
    }

    /// <summary>A war of annihilation carried this epoch: the loser's seat
    /// anchors a ruined metropolis — the cultural claim anchor irredentism
    /// and pilgrimage read (the H claims machinery already carries the
    /// grudge; the POI is its physical address).</summary>
    private static void CompileFallenCapitals(SimState state,
                                              List<StagedEvent> events)
    {
        var log = state.Log.Events;
        for (int i = log.Count - 1; i >= 0; i--)
        {
            var e = log[i];
            if (e.WorldYear < state.WorldYear) break;
            if (e.Type != WorldEventType.PeaceSettled
                || e.Payload is not PeaceSettledPayload ps
                || (WarOutcome)ps.Outcome != WarOutcome.Annexed
                || ps.WinnerId < 0) continue;
            var war = state.Wars[ps.WarId];
            int loser = war.AttackerId == ps.WinnerId
                ? war.DefenderId : war.AttackerId;
            var seat = state.Actors[loser].Seat;
            var poi = Anchor(state, PoiType.RuinedCapital, seat,
                             magnitude: 5.0, subjectId: loser);
            if (poi == null) continue;
            poi.ParticipantActorIds.Add(System.Math.Min(loser, ps.WinnerId));
            poi.ParticipantActorIds.Add(System.Math.Max(loser, ps.WinnerId));
            poi.SourceEventIds.Add(e.Id);
            events.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.CapitalRuined, new[] { loser, ps.WinnerId },
                seat, Magnitude: 5.0, Valence: -0.8, EventVisibility.Public,
                new CapitalRuinedPayload(poi.Id, loser,
                                         state.Actors[loser].Name)));
        }
    }

    /// <summary>Deep famines and suppressed emergences anchor memorial
    /// sites — stance and culture memory with a hex address.</summary>
    private static void CompileMemorials(SimState state,
                                         List<StagedEvent> events)
    {
        var knobs = state.Config.Poi;
        var log = state.Log.Events;
        for (int i = log.Count - 1; i >= 0; i--)
        {
            var e = log[i];
            if (e.WorldYear < state.WorldYear) break;
            int cause;
            double magnitude;
            if (e.Type == WorldEventType.FamineStruck
                && e.Magnitude >= knobs.MemorialShortfallFloor)
            { cause = 0; magnitude = 2.0; }
            else if (e.Type == WorldEventType.EmergenceSuppressed)
            { cause = 1; magnitude = 3.0; }
            else continue;
            var poi = Anchor(state, PoiType.Memorial, e.Location, magnitude,
                             detail: cause);
            if (poi == null) continue;
            poi.ParticipantActorIds.AddRange(e.Actors);
            poi.SourceEventIds.Add(e.Id);
            events.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.MemorialRaised, e.Actors, e.Location,
                magnitude, Valence: -0.6, EventVisibility.Regional,
                new MemorialRaisedPayload(poi.Id, cause)));
        }
    }

    /// <summary>Precursor sites surface as POIs when expansion reaches
    /// them: a registry entry becomes a charted, anchored place the first
    /// time a port sits within survey reach (the same mechanism, deeper
    /// stratum). Dormant remnants keep their flag — encounter content.</summary>
    private static void ChartPrecursorSites(SimState state,
                                            List<StagedEvent> events)
    {
        var knobs = state.Config.Poi;
        foreach (var wave in state.Skeleton.PrecursorWaves)   // id order (P6)
            foreach (var site in wave.Sites)
            {
                if (Charted(state, site.Hex)) continue;
                bool inReach = false;
                foreach (var port in state.Ports)
                    if (HexGrid.Distance(port.Hex, site.Hex)
                        <= knobs.SurveyReachHexes
                        && state.Actors[port.OwnerActorId].Entered)
                    { inReach = true; break; }
                if (!inReach) continue;
                var poi = Anchor(state, PoiType.PrecursorSite, site.Hex,
                    PrecursorMagnitude(site.Type), wave.Id, (int)site.Type);
                if (poi == null) continue;
                poi.Dormant = site.Dormant;
                events.Add(new StagedEvent(ClockStratum.Generational,
                    WorldEventType.PrecursorSiteCharted, new int[0], site.Hex,
                    poi.Magnitude, Valence: site.Dormant ? -0.2 : 0.4,
                    EventVisibility.Regional,
                    new PrecursorSiteChartedPayload(poi.Id, (int)site.Type,
                        site.Dormant, wave.Name)));
            }
    }

    /// <summary>A precursor hex already charted (live or dug out) never
    /// re-charts.</summary>
    private static bool Charted(SimState state, HexCoordinate hex)
    {
        foreach (var poi in state.Pois)
            if (poi.Type == PoiType.PrecursorSite && poi.Hex.Equals(hex))
                return true;
        return false;
    }

    private static double PrecursorMagnitude(PrecursorSiteType type)
        => type switch
        {
            PrecursorSiteType.Megastructure => 6.0,
            PrecursorSiteType.Capital => 5.0,
            PrecursorSiteType.EngineeredBiosphere => 3.0,
            PrecursorSiteType.Battlefield => 3.0,
            _ => 2.0,
        };

    /// <summary>POIs decay as their effects are consumed: salvaged-out
    /// fields fade (the largest persist as permanent archaeology) and
    /// repopulated ruins come back to life.</summary>
    private static void Decay(SimState state)
    {
        var knobs = state.Config.Poi;
        foreach (var poi in state.Pois)                   // id order (P6)
        {
            if (poi.Depleted) continue;
            if (poi.Type == PoiType.Battlefield
                && poi.SalvageRemaining <= 0
                && poi.Magnitude < knobs.PermanentMagnitude)
                poi.Depleted = true;
            else if (poi.Type == PoiType.Ruins
                     && Population(state, poi.SubjectId) >= 0.01)
                poi.Depleted = true;
        }
    }
}
