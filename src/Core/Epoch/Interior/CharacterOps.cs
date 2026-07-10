using System;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Mints, ages, kills, and succeeds characters (characters.md).
/// Generation is deterministic on demand from (institution, culture,
/// species, seed); aging is species-real; death triggers the government
/// form's succession rule. Runs inside the Interior phase.</summary>
public static class CharacterOps
{
    /// <summary>Species-real lifespans in world-years by embodiment
    /// (structural catalog): human-analog decades, lithic centuries, hive
    /// continuity and machine minds effectively ageless (they deprecate or
    /// drift instead — see the hazard).</summary>
    public static double Lifespan(Embodiment embodiment) => embodiment switch
    {
        Embodiment.Lithic => 400,
        Embodiment.Cryophilic => 120,
        Embodiment.Aquatic => 90,
        Embodiment.Hive => 10_000,
        Embodiment.Machine => 10_000,
        _ => 80,
    };

    /// <summary>Per-world-year death hazard at an age: zero through the
    /// prime, rising quadratically past 55% of lifespan (reaching the shape
    /// coefficient at the full span). Hive minds never die; machine minds
    /// carry a flat deprecation hazard instead.</summary>
    public static double AgeHazardPerYear(EpochSimConfig config,
                                          Embodiment embodiment, double age)
    {
        if (embodiment == Embodiment.Hive) return 0.0;
        if (embodiment == Embodiment.Machine)
            return config.Character.MachineDeprecationPerYear;
        double span = Lifespan(embodiment);
        double past = age / span - 0.55;
        if (past <= 0) return 0.0005;   // the young die rarely, never never
        double t = past / 0.45;
        return config.Character.MortalityShapePerYear * t * t;
    }

    /// <summary>Deterministic on-demand generation from (institution,
    /// culture, species, seed): a culture-flavored name, an age set by the
    /// role, a personality of ideology position + four scalars.</summary>
    public static Character Mint(SimState state, int polityId,
                                 CharacterRole role, int institutionId,
                                 double mintAgeFraction)
    {
        var pr = state.PolityOf(polityId);
        var species = state.Skeleton.Species[pr.SpeciesId];
        int cultureId = pr.Interior?.FoundingCultureId ?? pr.SpeciesId;
        int id = state.Characters.Count;
        ulong seed = state.Config.MasterSeed;

        // name in the culture's syllable flavor (keyed by culture, not step:
        // the same person minted at any epoch bears the same name)
        int syllables = 2 + (EpochRolls.NextDouble(seed, RollChannel.CharacterName,
            cultureId, id, 100) < 0.4 ? 1 : 0);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.CharacterName, cultureId, id, i));
        name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);

        double Trait(int ordinal) => EpochRolls.NextDouble(seed,
            RollChannel.CharacterTraits, state.EpochIndex, id, ordinal);
        double span = Lifespan(species.Embodiment);
        long age = (long)Math.Round(
            mintAgeFraction * span * (0.85 + 0.3 * Trait(8)));
        var character = new Character(id, name, pr.SpeciesId, cultureId,
                                      polityId, state.WorldYear - age)
        {
            Role = role,
            InstitutionId = institutionId,
            Boldness = Trait(4),
            Zeal = Trait(5),
            Competence = Trait(6),
            Ambition = Trait(7),
        };
        // ideology position: the society's official line, personally skewed
        var interior = pr.Interior;
        for (int ax = 0; ax < 4; ax++)
        {
            double baseline = interior?.OfficialIdeology[ax] ?? 0.5;
            character.IdeologyPosition[ax] =
                Clamp01(baseline + (Trait(ax) - 0.5) * 0.3);
        }
        state.Characters.Add(character);
        return character;
    }

    /// <summary>Seat a polity's court at entry: the ruler (chronicled — every
    /// reign gets its anchor), an heir for lineage forms (with the founding
    /// dynasty), a marshal where the form keeps one.</summary>
    public static void SeatLeadership(SimState state, PolityRecord pr)
    {
        var interior = pr.Interior;
        if (interior == null) return;
        var knobs = state.Config.Character;
        var ruler = Mint(state, pr.ActorId, CharacterRole.Ruler, pr.ActorId,
                         knobs.RulerMintAgeFraction);
        interior.RulerCharacterId = ruler.Id;
        var form = GovernmentForms.Get(interior.FormId);
        if (form.Succession is SuccessionRule.Dynastic
            or SuccessionRule.RareDesignation)
        {
            var dynasty = new Dynasty(state.Dynasties.Count, ruler.Name,
                                      ruler.Id, pr.ActorId);
            state.Dynasties.Add(dynasty);
            ruler.DynastyId = dynasty.Id;
            var heir = Mint(state, pr.ActorId, CharacterRole.Heir, pr.ActorId,
                            knobs.HeirMintAgeFraction);
            heir.DynastyId = dynasty.Id;
        }
        if (interior.FormId is not (GovernmentFormId.HiveUnity
            or GovernmentFormId.MachineConsensus))
            Mint(state, pr.ActorId, CharacterRole.Marshal, pr.ActorId,
                 knobs.RulerMintAgeFraction);
        ruler.Renown += knobs.RenownAscension;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.RulerAscended, new[] { pr.ActorId },
            state.Actors[pr.ActorId].Seat, Magnitude: 1.0, Valence: 0.5,
            EventVisibility.Public,
            new RulerAscendedPayload(ruler.Id, ruler.Name, pr.ActorId,
                                     ruler.DynastyId)));
    }

    /// <summary>One epoch of lives: dynastic prestige accrues, death checks
    /// run (age curve + assassination for rulers), successions resolve per
    /// form, vacant court and commander slots refill. Returns
    /// (deaths, successions, crises) for the phase note.</summary>
    public static (int Deaths, int Successions, int Crises) Step(SimState state)
    {
        var knobs = state.Config.Character;
        int years = state.Config.Sim.YearsPerEpoch;
        int deaths = 0, successions = 0, crises = 0;

        // reigning dynasties accrue prestige
        foreach (var pr in state.Polities)
        {
            var ruler = RulerOf(state, pr);
            if (ruler is { Alive: true, DynastyId: >= 0 })
                state.Dynasties[ruler.DynastyId].Prestige +=
                    knobs.DynastyPrestigePerReignYear * years;
        }

        // death checks in id order (P6); successions resolve inline so a
        // throne never sits empty across an epoch
        int living = state.Characters.Count;   // successors mint mid-loop;
                                               // newborns skip this epoch
        for (int i = 0; i < living; i++)
        {
            var c = state.Characters[i];
            if (!c.Alive) continue;
            var species = state.Skeleton.Species[c.SpeciesId];
            double age = state.WorldYear - c.BirthYear;
            double hazard = AgeHazardPerYear(state.Config, species.Embodiment, age);
            if (c.Role == CharacterRole.Ruler)
            {
                var interior = state.PolityOf(c.PolityId).Interior;
                double unpopularity = 1.0 - (interior?.Legitimacy ?? 0.5);
                hazard += knobs.AssassinationBasePerYear
                          * c.Ambition * unpopularity;
            }
            double epochDeath = 1.0 - Math.Pow(1.0 - Math.Min(1.0, hazard), years);
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    RollChannel.CharacterDeath, state.EpochIndex, c.Id)
                >= epochDeath) continue;

            c.Alive = false;
            c.DeathYear = state.WorldYear;
            deaths++;
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.CharacterDied, new[] { c.PolityId },
                state.Actors[c.PolityId].Seat, Magnitude: c.Renown,
                Valence: -0.5,
                c.Role == CharacterRole.Ruler
                    ? EventVisibility.Public : EventVisibility.Regional,
                new CharacterDiedPayload(c.Id, c.Name, (int)c.Role,
                                         (long)age)));
            if (c.Role == CharacterRole.Commander)
                foreach (var fleet in state.Fleets)
                    if (fleet.CommanderId == c.Id) fleet.CommanderId = -1;
            if (c.Role == CharacterRole.Ruler)
            {
                successions++;
                if (Succeed(state, c)) crises++;
            }
        }

        RefillCourts(state);
        FillCommanderSlots(state);
        return (deaths, successions, crises);
    }

    /// <summary>The form's succession rule (characters.md §Lifespan and
    /// succession). Returns true when the succession was a crisis.</summary>
    private static bool Succeed(SimState state, Character deadRuler)
    {
        var pr = state.PolityOf(deadRuler.PolityId);
        var interior = pr.Interior!;
        var knobs = state.Config.Character;
        var form = GovernmentForms.Get(interior.FormId);
        bool crisis = false;
        Character? successor = null;

        if (form.Succession is SuccessionRule.Dynastic
            or SuccessionRule.RareDesignation)
        {
            foreach (var c in state.Characters)   // id order (P6)
                if (c.Alive && c.PolityId == pr.ActorId
                    && c.Role == CharacterRole.Heir)
                { successor = c; break; }
            if (successor != null)
            {
                successor.Role = CharacterRole.Ruler;
                successor.InstitutionId = pr.ActorId;
                if (successor.DynastyId < 0)
                    successor.DynastyId = deadRuler.DynastyId;
            }
            else
            {
                // no heir stands: a crisis, and a new house takes the seat
                crisis = true;
                interior.Legitimacy = Math.Max(0.0,
                    interior.Legitimacy - knobs.CrisisLegitimacyHit);
                state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                    WorldEventType.SuccessionCrisis, new[] { pr.ActorId },
                    state.Actors[pr.ActorId].Seat, Magnitude: 1.0,
                    Valence: -1.0, EventVisibility.Public,
                    new SuccessionCrisisPayload(deadRuler.Id, deadRuler.Name,
                                                pr.ActorId)));
                successor = Mint(state, pr.ActorId, CharacterRole.Ruler,
                                 pr.ActorId, knobs.RulerMintAgeFraction);
                var house = new Dynasty(state.Dynasties.Count, successor.Name,
                                        successor.Id, pr.ActorId);
                state.Dynasties.Add(house);
                successor.DynastyId = house.Id;
            }
        }
        else
        {
            // committee, election, boardroom, doctrine — the institution
            // names its own; machine minds fork a replacement silently
            successor = Mint(state, pr.ActorId, CharacterRole.Ruler,
                             pr.ActorId, knobs.RulerMintAgeFraction);
        }

        interior.RulerCharacterId = successor.Id;
        successor.Renown += knobs.RenownAscension;
        if (form.Succession != SuccessionRule.NoneForked)
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.RulerAscended, new[] { pr.ActorId },
                state.Actors[pr.ActorId].Seat, Magnitude: 1.0, Valence: 0.5,
                EventVisibility.Public,
                new RulerAscendedPayload(successor.Id, successor.Name,
                                         pr.ActorId, successor.DynastyId)));
        return crisis;
    }

    /// <summary>Vacant heir and marshal seats refill — the court stays a
    /// court (rulers refill through succession, never here).</summary>
    private static void RefillCourts(SimState state)
    {
        var knobs = state.Config.Character;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            var interior = pr.Interior;
            if (interior == null || !state.Actors[pr.ActorId].Entered) continue;
            var form = GovernmentForms.Get(interior.FormId);
            bool wantsHeir = form.Succession is SuccessionRule.Dynastic
                or SuccessionRule.RareDesignation;
            bool wantsMarshal = interior.FormId is not (GovernmentFormId.HiveUnity
                or GovernmentFormId.MachineConsensus);
            bool hasHeir = false, hasMarshal = false;
            foreach (var c in state.Characters)
            {
                if (!c.Alive || c.PolityId != pr.ActorId) continue;
                if (c.Role == CharacterRole.Heir) hasHeir = true;
                if (c.Role == CharacterRole.Marshal) hasMarshal = true;
            }
            if (wantsHeir && !hasHeir)
            {
                var heir = Mint(state, pr.ActorId, CharacterRole.Heir,
                                pr.ActorId, knobs.HeirMintAgeFraction);
                var ruler = RulerOf(state, pr);
                if (ruler != null) heir.DynastyId = ruler.DynastyId;
            }
            if (wantsMarshal && !hasMarshal)
                Mint(state, pr.ActorId, CharacterRole.Marshal, pr.ActorId,
                     knobs.RulerMintAgeFraction);
        }
    }

    /// <summary>Notable fleets take commanders (frame/actors.md): warship
    /// content or an expedition under way fills the slot E left vacant.</summary>
    private static void FillCommanderSlots(SimState state)
    {
        var knobs = state.Config.Character;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.CommanderId >= 0 || fleet.TotalHulls == 0) continue;
            var owner = state.Actors[fleet.OwnerActorId];
            if (owner.Kind != ActorKind.Polity || !owner.Entered) continue;
            if (state.PolityOf(fleet.OwnerActorId).Interior == null) continue;
            bool notable = fleet.Posture == FleetPosture.Expedition;
            if (!notable)
                foreach (var g in fleet.Hulls)
                {
                    var role = state.Designs[g.DesignId].Role;
                    if (role == ShipRole.Escort || role == ShipRole.Line)
                    { notable = true; break; }
                }
            if (!notable) continue;
            var commander = Mint(state, fleet.OwnerActorId,
                CharacterRole.Commander, fleet.Id, knobs.RulerMintAgeFraction);
            fleet.CommanderId = commander.Id;
        }
    }

    /// <summary>Threshold events mint notables, capped per polity
    /// (characters.md §Notables). Null when the cap holds.</summary>
    public static Character? MintNotable(SimState state, int polityId,
                                         NotableType type,
                                         Model.HexCoordinate hex)
    {
        var pr = state.PolityOf(polityId);
        if (pr.Interior == null) return null;
        var knobs = state.Config.Character;
        if (NotableCount(state, polityId) >= knobs.MaxNotablesPerPolity)
            return null;
        var notable = Mint(state, polityId, CharacterRole.Notable, -1,
                           knobs.RulerMintAgeFraction);
        notable.Notable = type;
        notable.Renown += knobs.RenownNotable;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.NotableEmerged, new[] { polityId }, hex,
            Magnitude: 1.0, Valence: 0.5, EventVisibility.Regional,
            new NotableEmergedPayload(notable.Id, notable.Name, (int)type)));
        return notable;
    }

    /// <summary>Living notable-typed characters of a polity, whatever role
    /// they hold now — the sparsity cap's count (prophets leading factions
    /// and magnates in boardrooms count; deposed nobodies do not).</summary>
    public static int NotableCount(SimState state, int polityId)
    {
        int notables = 0;
        foreach (var c in state.Characters)
            if (c.Alive && c.PolityId == polityId
                && c.Notable != NotableType.None) notables++;
        return notables;
    }

    private static Character? RulerOf(SimState state, PolityRecord pr)
    {
        int id = pr.Interior?.RulerCharacterId ?? -1;
        return id >= 0 && id < state.Characters.Count
            ? state.Characters[id] : null;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
