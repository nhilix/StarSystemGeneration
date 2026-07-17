using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Natives and late emergence (interpolity/relations.md §Natives):
/// pre-spaceflight origins carry emergence dates projected onto the native
/// window; in free space a new polity is born, inside claimed space the
/// host's native policy resolves it — client vassal (uplift), autonomous
/// member with a cultural faction (integration), or a suppressed emergence
/// that hands every rival a standing liberation casus belli and the natives
/// a graduation path. Late emergence is a story generator, not an edge case.</summary>
public static class NativeOps
{
    /// <summary>An uplift-born client polity awaiting its vassal bond —
    /// bound after the entry loop founds it (same Interior phase).</summary>
    public readonly struct PendingClient
    {
        public readonly int PolityId;
        public readonly int HostId;

        public PendingClient(int polityId, int hostId)
        {
            PolityId = polityId;
            HostId = hostId;
        }
    }

    /// <summary>Resolve every pre-spaceflight origin whose projected date
    /// has fired. Runs at the top of the Interior phase, before the entry
    /// loop, so free and uplift births enter this same epoch.</summary>
    public static (int Births, int Integrated, int Suppressed,
        List<PendingClient>? Clients) Step(SimState state)
    {
        var natives = new List<SapientOrigin>();
        foreach (var origin in state.Skeleton.Origins)        // id order (P6)
            if (origin.Era == OriginEra.PreSpaceflight) natives.Add(origin);
        if (natives.Count == 0) return (0, 0, 0, null);
        natives.Sort((a, b) => a.SpaceflightYear != b.SpaceflightYear
            ? a.SpaceflightYear.CompareTo(b.SpaceflightYear)
            : a.Id.CompareTo(b.Id));

        var genesis = state.Config.Genesis;
        int births = 0, integrated = 0, suppressed = 0;
        List<PendingClient>? clients = null;
        for (int rank = 0; rank < natives.Count; rank++)
        {
            var origin = natives[rank];
            if (origin.ResolvedEpoch >= 0) continue;
            // dates project onto (window, native window], order and
            // spacing preserved — the same honest compression entries use
            double baseYear = genesis.EmergenceWindowYears
                + (rank + 1.0) / (natives.Count + 1.0)
                  * (genesis.NativeWindowYears - genesis.EmergenceWindowYears);
            // dates quantize to the generation calendar, whatever the
            // integration step (P7, slice J)
            int gen = state.Config.Sim.GenerationYears;
            long fireYear = (long)(baseYear / gen) * gen;

            int host = HostOf(state, origin.Hex);
            var policy = host >= 0
                ? (state.Actors[host].Policies as PolityPolicies
                   ?? PolityPolicies.Default).NativePolicy
                : NativePolicy.Protectorate;
            if (host >= 0)
            {
                // uplift accelerates (Life-tech-gated); the reserve delays
                if (policy == NativePolicy.Uplift
                    && state.PolityOf(host).TechTier[(int)TechDomain.Life] >= 2)
                    fireYear -= (long)genesis.UpliftAccelerationEpochs * gen;
                else if (policy == NativePolicy.Protectorate)
                    fireYear += (long)genesis.ProtectorateDelayEpochs * gen;
            }
            if (state.WorldYear < fireYear) continue;

            origin.ResolvedEpoch = state.EpochIndex;
            int speciesId = state.Skeleton.Species.Count;
            state.Skeleton.Species.Add(
                SkeletonBuilder.DeriveSpecies(state.Skeleton, origin, speciesId));
            var species = state.Skeleton.Species[speciesId];

            if (host < 0 || (policy == NativePolicy.Uplift
                && state.PolityOf(host).TechTier[(int)TechDomain.Life] >= 2))
            {
                // a new polity is born with its homeworld domain; uplift
                // births are clients, bound once the entry loop seats them
                int actorId = state.Actors.Count;
                // born now, so the entry date is now: this used to hand the
                // gate an EpochIndex, which the gate re-inflated by
                // GenerationYears — at any fine clock that lands in the future
                // and the native-born polity never entered at all (P7, slice MC)
                state.Actors.Add(new Actor(actorId, ActorKind.Polity,
                    species.Name, origin.Hex, state.WorldYear,
                    new GenesisController(state.Config)));
                state.Polities.Add(new PolityRecord(actorId, speciesId)
                {
                    // latecomers matured under a sky full of foreign
                    // traffic — the full contact bonus, behind not hopeless
                    EntryGradeBonus = 0.10 + 0.05 * origin.Richness,
                });
                state.Cultures.Add(new Culture(state.Cultures.Count,
                    species.Name, speciesId));
                births++;
                if (host >= 0)
                    (clients ??= new List<PendingClient>())
                        .Add(new PendingClient(actorId, host));
            }
            else
            {
                // inside claimed space the host's policy resolves it: the
                // native people join as segments under the covering port
                int cultureId = state.Cultures.Count;
                state.Cultures.Add(new Culture(cultureId, species.Name,
                                               speciesId));
                int port = CoveringPort(state, origin.Hex, host);
                var segment = new PopulationSegment(state.Segments.Count,
                    port, speciesId, cultureId,
                    genesis.NativePopulationSize);
                var tilt = GovernmentForms.SpeciesIdeologyTilt(species);
                for (int ax = 0; ax < 4; ax++) segment.Ideology[ax] = tilt[ax];
                segment.Body = PopulationSiting.Assign(state, port);
                state.Segments.Add(segment);

                if (policy == NativePolicy.Integrate
                    || policy == NativePolicy.Uplift)
                {
                    // an uplift ambition without the Life tech welcomes
                    // them as members instead — never a cage
                    integrated++;
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational,
                        WorldEventType.NativesIntegrated,
                        new[] { host }, origin.Hex, Magnitude: segment.Size,
                        Valence: 0.5, EventVisibility.Public,
                        new NativesIntegratedPayload(origin.Id, host,
                            state.Actors[host].Name, species.Name)));
                    // the cultural faction the design promises coalesces
                    // from the minority segment through FactionOps
                }
                else
                {
                    // exploitation, or a protectorate turned cage: the
                    // suppressed emergence — every rival gains a standing
                    // liberation casus belli, the natives a graduation path
                    suppressed++;
                    segment.SoL = 0.1;   // the worst press in the galaxy
                    foreach (var relation in state.Relations)
                    {
                        if (!relation.Involves(host)
                            || !RelationsOps.BothLive(state, relation))
                            continue;
                        int rival = relation.OtherOf(host);
                        if (!relation.HasLiveClaim(ClaimType.Liberation,
                                rival, cultureId))
                            relation.Claims.Add(new RelationClaim(
                                ClaimType.Liberation, rival, cultureId,
                                state.WorldYear));
                    }
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational,
                        WorldEventType.EmergenceSuppressed,
                        new[] { host }, origin.Hex, Magnitude: segment.Size,
                        Valence: -0.9, EventVisibility.Public,
                        new EmergenceSuppressedPayload(origin.Id, host,
                            state.Actors[host].Name, species.Name,
                            (int)policy)));
                }
            }
        }
        return (births, integrated, suppressed, clients);
    }

    /// <summary>Bind uplift-born clients to their hosts — after the entry
    /// loop has founded them (contact is definitional: they were born
    /// inside the domain).</summary>
    public static void BindClients(SimState state,
                                   List<PendingClient>? clients)
    {
        if (clients == null) return;
        foreach (var client in clients)
        {
            if (!state.Actors[client.PolityId].Entered
                || !state.Actors[client.HostId].Entered) continue;
            var relation = state.RelationOf(client.PolityId, client.HostId);
            if (relation == null)
            {
                int a = Math.Min(client.PolityId, client.HostId);
                int b = Math.Max(client.PolityId, client.HostId);
                relation = new PolityRelation(a, b, state.WorldYear);
                relation.Warmth = 0.5;   // raised, not met
                state.Relations.Add(relation);
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.FirstContact,
                    new[] { a, b }, state.Actors[client.PolityId].Seat,
                    Magnitude: 1.0, Valence: 0.3, EventVisibility.Public,
                    new FirstContactPayload(a, b, state.Actors[a].Name,
                                            state.Actors[b].Name)));
            }
            // a host that is itself a vassal can't take clients — no nested
            // overlord chains (review fix 6); the young polity stays free
            if (relation.VassalPolityId < 0
                && FederationOps.OverlordOf(state, client.HostId) < 0)
                FederationOps.Bind(state, relation, client.PolityId);
        }
    }

    /// <summary>The polity whose domain covers a hex (nearest covering
    /// port's owner), or −1 for free space.</summary>
    public static int HostOf(SimState state, HexCoordinate hex)
    {
        int port = NearestCoveringPort(state, hex, ownerFilter: -1);
        return port >= 0 ? state.Ports[port].OwnerActorId : -1;
    }

    private static int CoveringPort(SimState state, HexCoordinate hex,
                                    int owner) =>
        NearestCoveringPort(state, hex, owner);

    private static int NearestCoveringPort(SimState state, HexCoordinate hex,
                                           int ownerFilter)
    {
        var cfg = state.Config;
        int best = -1, bestDist = int.MaxValue;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (ownerFilter >= 0 && port.OwnerActorId != ownerFilter) continue;
            if (!state.Actors[port.OwnerActorId].Entered) continue;
            int dist = HexGrid.Distance(port.Hex, hex);
            if (dist > PortDomains.ServiceRadius(cfg, port.Tier)
                       + TechOps.AstroRadiusBonus(state, port.OwnerActorId))
                continue;
            if (dist < bestDist) { bestDist = dist; best = port.Id; }
        }
        return best;
    }
}
