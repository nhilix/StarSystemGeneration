using System;
using System.Collections.Generic;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Graduation — the unified origin story for new institutions
/// (polity/factions-and-government.md §Graduation, frame/actors.md): when a
/// faction's strength × grievance beats the polity's legitimacy ×
/// enforcement, it stops pressuring and becomes something. Regional and
/// cultural interests schism into new polities; throne-seekers coup; the
/// economic basis charters (task 7); failures are crushed into revolt.
/// Runs at the end of the Interior phase on the recomputed scalars.</summary>
public static class GraduationOps
{
    /// <summary>One epoch of graduations: at most one attempt per polity
    /// (its loudest faction past the threshold). Returns counts for the
    /// phase note.</summary>
    public static (int Schisms, int Coups, int Revolts) Step(SimState state)
    {
        int schisms = 0, coups = 0, revolts = 0;
        int polities = state.Polities.Count;   // schisms append mid-loop;
                                               // newborn states sit this epoch out
        for (int i = 0; i < polities; i++)
        {
            var pr = state.Polities[i];
            var interior = pr.Interior;
            if (interior == null || !state.Actors[pr.ActorId].Entered) continue;

            // the loudest faction past the polity's grip attempts
            var knobs = state.Config.Faction;
            double grip = interior.Legitimacy * interior.Enforcement
                          * knobs.GraduationGripFactor;
            Faction? challenger = null;
            double loudest = grip;
            foreach (var faction in state.Factions)           // id order (P6)
            {
                if (!faction.Active || faction.PolityId != pr.ActorId) continue;
                if (faction.Basis == FactionBasis.Corporate) continue; // task 7
                double pressure = faction.Strength * faction.Grievance;
                if (pressure > loudest) { loudest = pressure; challenger = faction; }
            }
            if (challenger == null) continue;

            double p = loudest / (loudest + Math.Max(1e-9, grip));
            bool success = EpochRolls.NextDouble(state.Config.MasterSeed,
                RollChannel.Graduation, state.EpochIndex, challenger.Id) < p;
            if (success)
                switch (challenger.Basis)
                {
                    case FactionBasis.Regional:
                    case FactionBasis.Cultural:
                        if (TrySchism(state, pr, challenger)) schisms++;
                        else { Revolt(state, pr, challenger); revolts++; }
                        break;
                    default:
                        Coup(state, pr, challenger);
                        coups++;
                        break;
                }
            else { Revolt(state, pr, challenger); revolts++; }
        }
        return (schisms, coups, revolts);
    }

    // ---- schism ----

    /// <summary>Domains secede as a new polity: culture, ideology, and name
    /// from its own segments; ports, people, facilities, fleets, and a
    /// population share of every treasury go with them (all conserved). The
    /// faction's war chest becomes the new state's founding treasury and its
    /// leader takes the seat. False when no domain would secede (or all
    /// would) — those become revolts.</summary>
    private static bool TrySchism(SimState state, PolityRecord old, Faction faction)
    {
        var seceding = SecedingPorts(state, old, faction);
        int ownPorts = 0;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == old.ActorId) ownPorts++;
        if (seceding.Count == 0 || seceding.Count == ownPorts) return false;

        var species = state.Skeleton.Species[old.SpeciesId];
        int newId = state.Actors.Count;

        // the culture: a cultural schism IS its minority culture; a regional
        // one splits the founding culture — the registry's waited-for split
        int cultureId;
        if (faction.Basis == FactionBasis.Cultural) cultureId = faction.ContextId;
        else
        {
            cultureId = state.Cultures.Count;
            state.Cultures.Add(new Culture(cultureId,
                SyllableName(state, newId), old.SpeciesId));
            foreach (var s in state.Segments)
                if (s.Size > 0 && seceding.Contains(s.PortId))
                    s.CultureId = cultureId;
        }
        string name = state.Cultures[cultureId].Name;

        // seat at the biggest seceding harbor
        int seatPort = -1;
        double seatPop = -1;
        foreach (int portId in seceding)
        {
            double pop = 0;
            foreach (var s in state.Segments)
                if (s.PortId == portId) pop += s.Size;
            if (pop > seatPop) { seatPop = pop; seatPort = portId; }
        }
        var seat = state.Ports[seatPort].Hex;

        var actor = new Actor(newId, ActorKind.Polity, name, seat,
                              state.EpochIndex, new GenesisController(state.Config))
        { Entered = true };
        state.Actors.Add(actor);
        var young = new PolityRecord(newId, old.SpeciesId)
        {
            EntryGradeBonus = old.EntryGradeBonus,   // shared industrial heritage
        };
        state.Polities.Add(young);

        // people decide the split fraction; every ledger moves by it (P4)
        double totalPop = 0, secededPop = 0;
        foreach (var s in state.Segments)
        {
            if (s.Size <= 0
                || state.Ports[s.PortId].OwnerActorId != old.ActorId) continue;
            totalPop += s.Size;
            if (seceding.Contains(s.PortId)) secededPop += s.Size;
        }
        double share = totalPop > 0 ? secededPop / totalPop : 0.5;
        young.Credits = old.Credits * share;
        old.Credits -= young.Credits;
        young.ExpansionPoints = old.ExpansionPoints * share;
        old.ExpansionPoints -= young.ExpansionPoints;
        young.DevelopmentPoints = old.DevelopmentPoints * share;
        old.DevelopmentPoints -= young.DevelopmentPoints;
        young.MilitaryPoints = old.MilitaryPoints * share;
        old.MilitaryPoints -= young.MilitaryPoints;
        for (int g = 0; g < old.ReserveQty.Length; g++)
        {
            young.ReserveQty[g] = old.ReserveQty[g] * share;
            old.ReserveQty[g] -= young.ReserveQty[g];
            young.ReserveGrade[g] = old.ReserveGrade[g];
        }
        // the movement's war chest founds the treasury (conserved flow)
        young.Credits += faction.Wealth;
        faction.Wealth = 0;

        foreach (var port in state.Ports)
            if (seceding.Contains(port.Id)) port.OwnerActorId = newId;
        foreach (var facility in state.Facilities)
            if (facility.OwnerActorId == old.ActorId
                && seceding.Contains(MarketEngine.AttachedMarketIndex(state, facility)))
                facility.OwnerActorId = newId;
        int hullsMoved = 0;
        foreach (var fleet in state.Fleets)
        {
            if (fleet.OwnerActorId != old.ActorId
                || !seceding.Contains(fleet.HomePortId)) continue;
            fleet.OwnerActorId = newId;
            hullsMoved += fleet.TotalHulls;
            if (fleet.CommanderId >= 0)
                state.Characters[fleet.CommanderId].PolityId = newId;
        }
        old.HullsBuilt -= hullsMoved;   // the ledger follows the hulls (P4)
        young.HullsBuilt += hullsMoved;
        DesignRegistry.RegisterEntryDesigns(state, newId, species.Militancy);

        // the interior: popular line of its own segments, form reseated,
        // the faction's leader on the new throne
        var interior = new PolityInterior { FoundingCultureId = cultureId };
        double sizeSum = 0;
        Span<double> popular = stackalloc double[4];
        foreach (var s in state.Segments)
        {
            if (s.Size <= 0 || !seceding.Contains(s.PortId)) continue;
            sizeSum += s.Size;
            for (int ax = 0; ax < 4; ax++) popular[ax] += s.Ideology[ax] * s.Size;
        }
        for (int ax = 0; ax < 4; ax++)
            interior.OfficialIdeology[ax] =
                sizeSum > 0 ? popular[ax] / sizeSum : 0.5;
        interior.FormId = GovernmentForms.SeatFor(species,
                                                  interior.OfficialIdeology);
        young.Interior = interior;
        SeatCourt(state, young, faction);

        faction.Active = false;   // graduated
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.SchismDeclared, new[] { old.ActorId, newId },
            seat, Magnitude: seceding.Count, Valence: -0.8,
            EventVisibility.Public,
            new SchismDeclaredPayload(faction.Id, faction.Name, old.ActorId,
                                      newId, name, seceding.Count)));
        return true;
    }

    /// <summary>Which domains walk: frontier ports for a regional schism,
    /// its culture's majority ports for a cultural one.</summary>
    private static HashSet<int> SecedingPorts(SimState state, PolityRecord pr,
                                              Faction faction)
    {
        var seceding = new HashSet<int>();
        if (faction.Basis == FactionBasis.Regional)
        {
            var seat = state.Actors[pr.ActorId].Seat;
            int frontierAt = (int)(state.Config.Faction.FrontierDistanceFraction
                * state.Config.Expansion.ColonizationReachHexes);
            foreach (var port in state.Ports)
                if (port.OwnerActorId == pr.ActorId
                    && HexGrid.Distance(seat, port.Hex) > frontierAt)
                    seceding.Add(port.Id);
        }
        else
        {
            foreach (var port in state.Ports)
            {
                if (port.OwnerActorId != pr.ActorId) continue;
                double total = 0, ours = 0;
                foreach (var s in state.Segments)
                {
                    if (s.Size <= 0 || s.PortId != port.Id) continue;
                    total += s.Size;
                    if (s.CultureId == faction.ContextId) ours += s.Size;
                }
                if (total > 0 && ours / total > 0.5) seceding.Add(port.Id);
            }
        }
        return seceding;
    }

    /// <summary>The graduated faction's leader takes the seat; lineage forms
    /// found a house; the court fills per form.</summary>
    private static void SeatCourt(SimState state, PolityRecord pr,
                                  Faction faction)
    {
        var interior = pr.Interior!;
        var knobs = state.Config.Character;
        var leader = state.Characters[faction.LeaderCharacterId];
        leader.PolityId = pr.ActorId;
        leader.Role = CharacterRole.Ruler;
        leader.InstitutionId = pr.ActorId;
        leader.Renown += knobs.RenownAscension;
        interior.RulerCharacterId = leader.Id;
        var form = GovernmentForms.Get(interior.FormId);
        if (form.Succession is SuccessionRule.Dynastic
            or SuccessionRule.RareDesignation)
        {
            var house = new Dynasty(state.Dynasties.Count, leader.Name,
                                    leader.Id, pr.ActorId);
            state.Dynasties.Add(house);
            leader.DynastyId = house.Id;
        }
        // heir and marshal follow through the court refill next epoch
    }

    // ---- coup ----

    /// <summary>Leadership replaced, ideology lurches, the form may reseat;
    /// a contested coup records the civil war the war machinery (H) will
    /// fight — until then the strong side simply holds the palace.</summary>
    private static void Coup(SimState state, PolityRecord pr, Faction faction)
    {
        var interior = pr.Interior!;
        var knobs = state.Config.Faction;
        var leader = state.Characters[faction.LeaderCharacterId];

        if (interior.RulerCharacterId >= 0)
        {
            var deposed = state.Characters[interior.RulerCharacterId];
            if (deposed.Alive)
            {
                deposed.Role = CharacterRole.Notable;   // lives on, disgraced
                deposed.InstitutionId = -1;
            }
        }
        leader.Role = CharacterRole.Ruler;
        leader.InstitutionId = pr.ActorId;
        leader.Renown += state.Config.Character.RenownAscension;
        interior.RulerCharacterId = leader.Id;

        var target = faction.IdeologyTarget ?? leader.IdeologyPosition;
        for (int ax = 0; ax < 4; ax++)
            interior.OfficialIdeology[ax] +=
                (target[ax] - interior.OfficialIdeology[ax])
                * knobs.CoupIdeologyLurch;
        interior.Legitimacy = Math.Max(0.0,
            interior.Legitimacy - knobs.CoupLegitimacyHit);

        var species = state.Skeleton.Species[pr.SpeciesId];
        var oldForm = interior.FormId;
        var newForm = GovernmentForms.SeatFor(species, interior.OfficialIdeology);
        bool contested = EpochRolls.NextDouble(state.Config.MasterSeed,
            RollChannel.Graduation, state.EpochIndex, faction.Id, 1)
            < faction.Militancy * 0.5;

        if (newForm != oldForm)
        {
            interior.FormId = newForm;
            var form = GovernmentForms.Get(newForm);
            if (form.Succession is SuccessionRule.Dynastic
                or SuccessionRule.RareDesignation && leader.DynastyId < 0)
            {
                var house = new Dynasty(state.Dynasties.Count, leader.Name,
                                        leader.Id, pr.ActorId);
                state.Dynasties.Add(house);
                leader.DynastyId = house.Id;
            }
        }

        // the movement's chest funds the new regime (conserved flow)
        pr.Credits += faction.Wealth;
        faction.Wealth = 0;
        faction.Active = false;

        var seat = state.Actors[pr.ActorId].Seat;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.CoupStruck, new[] { pr.ActorId }, seat,
            Magnitude: 1.0, Valence: -0.6, EventVisibility.Public,
            new CoupStruckPayload(leader.Id, leader.Name, faction.Id,
                                  faction.Name, pr.ActorId, contested)));
        if (newForm != oldForm)
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.GovernmentReformed, new[] { pr.ActorId }, seat,
                Magnitude: 1.0, Valence: 0.0, EventVisibility.Public,
                new GovernmentReformedPayload(pr.ActorId, (int)oldForm,
                                              (int)newForm)));
    }

    // ---- revolt ----

    /// <summary>A crushed graduation: the leader is martyred, the movement
    /// halved, the state bruised — and the grievance largely kept, which is
    /// how repression compounds (§Graduation).</summary>
    private static void Revolt(SimState state, PolityRecord pr, Faction faction)
    {
        var interior = pr.Interior!;
        var knobs = state.Config.Faction;
        interior.Legitimacy = Math.Max(0.0,
            interior.Legitimacy - knobs.RevoltLegitimacyHit);
        faction.Strength *= 0.5;
        faction.Grievance *= knobs.RevoltGrievanceKeep;

        var martyr = state.Characters[faction.LeaderCharacterId];
        if (martyr.Alive)
        {
            martyr.Alive = false;
            martyr.DeathYear = state.WorldYear;
        }
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.RevoltCrushed, new[] { pr.ActorId },
            state.Actors[pr.ActorId].Seat, Magnitude: faction.Strength,
            Valence: -0.9, EventVisibility.Public,
            new RevoltCrushedPayload(martyr.Id, martyr.Name, faction.Id,
                                     faction.Name, pr.ActorId)));
    }

    private static string SyllableName(SimState state, int key)
    {
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed, RollChannel.Graduation,
            key, -1, 100) < 0.4 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.Graduation, key, -1, 10 + i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word);
    }
}
