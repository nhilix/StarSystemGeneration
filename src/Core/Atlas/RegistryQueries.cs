using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One calibration dial (`knobs` parity): name, live value of
/// the loaded sim's config, doc line.</summary>
public sealed record KnobRow(string Name, double Value, string Doc);

/// <summary>One good of the closed catalog (`goods`).</summary>
public sealed record GoodRow(GoodId Id, string Name, GoodTier Tier,
                             int RecipeCount);

/// <summary>What a find hit is.</summary>
public enum FindKind { Actor, Character, Corporation, War, Poi, Port }

/// <summary>One topbar-search hit; JumpHex set where the subject has an
/// address.</summary>
public sealed record FindHit(FindKind Kind, int Id, string Name,
                             HexCoordinate? JumpHex);

/// <summary>The world at a glance — registry counts for the drawer's
/// stats face.</summary>
public sealed record WorldStats(long WorldYear, int EpochIndex, int Ports,
    int Lanes, int PolitiesEntered, int ActiveWars, int Fleets,
    int Characters, int Corporations, int Pois, int Events,
    int ShipmentsInTransit, int ProjectsInFlight);

/// <summary>K3: the registry drawer behind the topbar search — the
/// ledger-workspace idea inside the panel system (`find`/`stats`/`goods`/
/// `knobs`).</summary>
public static class RegistryQueries
{
    /// <summary>Every calibration dial with its live value (`knobs`
    /// parity: substring filter, case-insensitive).</summary>
    public static List<KnobRow> Knobs(AtlasReadModel model, EyeContext eye,
                                      string filter = "")
    {
        var config = model.State.Config;
        var rows = new List<KnobRow>();
        foreach (var knob in KnobRegistry.All)
        {
            if (filter.Length > 0 && knob.Name.IndexOf(filter,
                    StringComparison.OrdinalIgnoreCase) < 0) continue;
            rows.Add(new KnobRow(knob.Name, knob.Get(config), knob.Doc));
        }
        return rows;
    }

    /// <summary>The closed 17-good vocabulary (`goods`).</summary>
    public static List<GoodRow> GoodsCatalog(AtlasReadModel model,
                                             EyeContext eye)
    {
        var rows = new List<GoodRow>();
        foreach (var def in Goods.All)
            rows.Add(new GoodRow(def.Id, def.Name, def.Tier,
                                 def.Recipes.Count));
        return rows;
    }

    /// <summary>Case-insensitive name search across the registries —
    /// actors, characters, corporations, wars, POIs (by type name), and
    /// ports by id. Fixed kind order, id order within (P6).</summary>
    public static List<FindHit> Find(AtlasReadModel model, EyeContext eye,
                                     string query)
    {
        var state = model.State;
        var hits = new List<FindHit>();
        if (string.IsNullOrWhiteSpace(query)) return hits;
        bool Match(string name) =>
            name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        foreach (var a in state.Actors)
            if (Match(a.Name))
                hits.Add(new FindHit(FindKind.Actor, a.Id, a.Name, a.Seat));
        foreach (var c in state.Characters)
            if (c.Alive && Match(c.Name))
                hits.Add(new FindHit(FindKind.Character, c.Id, c.Name,
                    c.PolityId >= 0 && c.PolityId < state.Actors.Count
                        ? state.Actors[c.PolityId].Seat : null));
        foreach (var corp in state.Corporations)
            if (Match(corp.Name))
                hits.Add(new FindHit(FindKind.Corporation, corp.Id,
                    corp.Name,
                    corp.HomePortId >= 0 && corp.HomePortId < state.Ports.Count
                        ? state.Ports[corp.HomePortId].Hex : null));
        foreach (var war in state.Wars)
            if (Match(war.Name))
                hits.Add(new FindHit(FindKind.War, war.Id, war.Name,
                    state.Actors[war.AttackerId].Seat));
        foreach (var poi in state.Pois)
        {
            string name = PoiPanel.TypeName(poi);
            if (Match(name))
                hits.Add(new FindHit(FindKind.Poi, poi.Id, name, poi.Hex));
        }
        if (int.TryParse(query.TrimStart('#'), out int portId)
            && portId >= 0 && portId < state.Ports.Count)
            hits.Add(new FindHit(FindKind.Port, portId,
                FormattableString.Invariant($"port #{portId}"),
                state.Ports[portId].Hex));
        return hits;
    }

    /// <summary>The registries counted — the drawer's stats face.</summary>
    public static WorldStats Stats(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        int entered = 0;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) entered++;
        int wars = 0;
        foreach (var w in state.Wars) if (w.Active) wars++;
        int fleets = 0;
        foreach (var f in state.Fleets) if (f.TotalHulls > 0) fleets++;
        int living = 0;
        foreach (var c in state.Characters) if (c.Alive) living++;
        int corps = 0;
        foreach (var c in state.Corporations) if (c.Active) corps++;
        int inFlight = 0;
        foreach (var p in state.Projects) if (p.InFlight) inFlight++;
        return new WorldStats(state.WorldYear, state.EpochIndex,
            state.Ports.Count, state.Lanes.Count, entered, wars, fleets,
            living, corps, state.Pois.Count, state.Log.Events.Count,
            state.Shipments.Count, inFlight);
    }
}
