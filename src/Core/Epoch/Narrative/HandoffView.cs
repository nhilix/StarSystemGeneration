using System.Collections.Generic;
using static System.FormattableString;

namespace StarGen.Core.Epoch;

/// <summary>One surfaced open thread — a reason the world is in motion at
/// the moment of handoff. SubjectId/SubjectId2 identify the subject in the
/// kind's own registry (K3, view-only): war→war id · tension→polity A/B
/// actor ids · succession→polity actor id (claimed thrones: holder,
/// claimed) · corporation→corp id, host actor id · plague→plague id ·
/// quarantine→lane id · offer→offerer actor id, other actor id.</summary>
public sealed record OpenThread(string Kind, string Text,
                                int SubjectId = -1, int SubjectId2 = -1);

/// <summary>The world-state handoff's open-threads surface (narrative/
/// handoff.md §Contents): the final epoch is not tidied — loaded tensions,
/// pending successions, half-won wars, and leveraged corporations hand the
/// player a world in motion, not a museum. A computed view over the loaded
/// state, recomputable at any moment; artifact finalization compiles
/// nothing (chronicle-and-poi.md).</summary>
public static class HandoffView
{
    /// <summary>Every open thread, in a fixed kind order then registry
    /// order (P6) — deterministic for a given state.</summary>
    public static List<OpenThread> OpenThreads(SimState state)
    {
        var threads = new List<OpenThread>();
        HalfWonWars(state, threads);
        LoadedTensions(state, threads);
        PendingSuccessions(state, threads);
        LeveragedCorporations(state, threads);
        BurningPlagues(state, threads);
        StandingQuarantines(state, threads);
        UnansweredOffers(state, threads);
        return threads;
    }

    /// <summary>Active wars, with how far each has burned.</summary>
    private static void HalfWonWars(SimState state, List<OpenThread> threads)
    {
        foreach (var war in state.Wars)                   // id order (P6)
        {
            if (!war.Active) continue;
            int taken = 0;
            foreach (var o in war.Objectives)
                if (o.Status == ObjectiveStatus.Taken) taken++;
            threads.Add(new OpenThread("war",
                Invariant($"{war.Name} still burns: ")
                + Invariant($"{state.Actors[war.AttackerId].Name} against ")
                + Invariant($"{state.Actors[war.DefenderId].Name}, ")
                + Invariant($"{taken}/{war.Objectives.Count} objectives taken, ")
                + Invariant($"exhaustion {war.AttackerExhaustion:0.00} vs ")
                + Invariant($"{war.DefenderExhaustion:0.00}"),
                war.Id));
        }
    }

    /// <summary>Pairs at the brink: tension over the war floor, a viable
    /// casus belli on the menu, and no war declared yet.</summary>
    private static void LoadedTensions(SimState state, List<OpenThread> threads)
    {
        foreach (var rel in state.Relations)              // creation order (P6)
        {
            if (!RelationsOps.BothLive(state, rel)) continue;
            if (rel.Tension < state.Config.War.WarTensionFloor) continue;
            if (WarOps.ActiveWarBetween(state, rel.PolityAId,
                                        rel.PolityBId) != null) continue;
            int causes = WarOps.Menu(state, rel.PolityAId, rel.PolityBId).Count
                       + WarOps.Menu(state, rel.PolityBId, rel.PolityAId).Count;
            if (causes == 0) continue;
            threads.Add(new OpenThread("tension",
                Invariant($"{state.Actors[rel.PolityAId].Name} and ")
                + Invariant($"{state.Actors[rel.PolityBId].Name} stand at the brink: ")
                + Invariant($"tension {rel.Tension:0.00}, {causes} declared ")
                + (causes == 1 ? "cause" : "causes") + " on the table",
                rel.PolityAId, rel.PolityBId));
        }
    }

    /// <summary>Old thrones and claimed thrones: rulers deep into their
    /// species' span, and standing foreign succession claims.</summary>
    private static void PendingSuccessions(SimState state,
                                           List<OpenThread> threads)
    {
        foreach (var pr in state.Polities)                // actor-id order
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered || actor.Retired
                || pr.Interior is not { RulerCharacterId: >= 0 } interior)
                continue;
            var ruler = state.Characters[interior.RulerCharacterId];
            if (!ruler.Alive) continue;
            int species = pr.SpeciesId;
            if (species < 0 || species >= state.Skeleton.Species.Count)
                continue;
            double span = CharacterOps.Lifespan(
                state.Skeleton.Species[species].Embodiment);
            double age = state.WorldYear - ruler.BirthYear;
            if (age / span >= 0.75)
                threads.Add(new OpenThread("succession",
                    Invariant($"an old throne: {ruler.Name} of {actor.Name} is ")
                    + Invariant($"{age:0} (a span is {span:0}) — the succession looms"),
                    pr.ActorId));
        }
        foreach (var rel in state.Relations)              // creation order (P6)
        {
            if (!RelationsOps.BothLive(state, rel)) continue;
            foreach (var claim in rel.Claims)
                if (!claim.Released && claim.Type == ClaimType.Succession)
                    threads.Add(new OpenThread("succession",
                        Invariant($"a claimed throne: {state.Actors[claim.HolderPolityId].Name}")
                        + " holds a live succession claim on "
                        + Invariant($"{state.Actors[rel.OtherOf(claim.HolderPolityId)].Name}"),
                        claim.HolderPolityId, rel.OtherOf(claim.HolderPolityId)));
        }
    }

    /// <summary>Hosted corporations whose books rival the state's — inside
    /// half the nationalization line counts as leverage.</summary>
    private static void LeveragedCorporations(SimState state,
                                              List<OpenThread> threads)
    {
        double factor = state.Config.Corporate.NationalizeWealthFactor * 0.5;
        foreach (var corp in state.Corporations)          // id order (P6)
        {
            if (!corp.Active || corp.HostPolityId < 0) continue;
            var host = state.PolityOf(corp.HostPolityId);
            if (host.Credits <= 0 || corp.Credits < host.Credits * factor)
                continue;
            threads.Add(new OpenThread("corporation",
                Invariant($"{corp.Name} out-banks its host: {corp.Credits:0} credits ")
                + Invariant($"against {state.Actors[corp.HostPolityId].Name}'s ")
                + Invariant($"{host.Credits:0} — nationalization pressure builds"),
                corp.Id, corp.HostPolityId));
        }
    }

    private static void BurningPlagues(SimState state, List<OpenThread> threads)
    {
        foreach (var plague in state.Plagues)             // id order (P6)
        {
            if (!plague.Active) continue;
            threads.Add(new OpenThread("plague",
                Invariant($"the {plague.Name} still burns: {plague.InfectedSince.Count} ")
                + (plague.InfectedSince.Count == 1 ? "port" : "ports")
                + Invariant($" infected, {plague.TotalDeaths:0.00} dead so far"),
                plague.Id));
        }
    }

    private static void StandingQuarantines(SimState state,
                                            List<OpenThread> threads)
    {
        foreach (var lane in state.Lanes)                 // id order (P6)
            if (lane.QuarantinedUntil > state.WorldYear)
                threads.Add(new OpenThread("quarantine",
                    Invariant($"lane #{lane.Id} (port {lane.PortAId} — port ")
                    + Invariant($"{lane.PortBId}) sits closed until ")
                    + Invariant($"y{lane.QuarantinedUntil}"),
                    lane.Id));
    }

    private static void UnansweredOffers(SimState state,
                                         List<OpenThread> threads)
    {
        foreach (var rel in state.Relations)              // creation order (P6)
        {
            if (!RelationsOps.BothLive(state, rel)) continue;
            if (rel.OfferedRung == TreatyRung.None || rel.OfferedById < 0)
                continue;
            threads.Add(new OpenThread("offer",
                Invariant($"an unanswered offer: {state.Actors[rel.OfferedById].Name} ")
                + Invariant($"holds out {rel.OfferedRung} to ")
                + Invariant($"{state.Actors[rel.OtherOf(rel.OfferedById)].Name}"),
                rel.OfferedById, rel.OtherOf(rel.OfferedById)));
        }
    }
}
