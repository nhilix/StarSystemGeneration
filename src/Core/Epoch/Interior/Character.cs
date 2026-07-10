namespace StarGen.Core.Epoch;

/// <summary>The institutional role a character occupies (characters.md
/// §Sparsity: a character exists only when a role needs an occupant or an
/// event mints a notable). Stable ids.</summary>
public enum CharacterRole
{
    Notable = 0,       // no seat: carries chronicle color, patronizes factions
    Ruler = 1,
    Heir = 2,
    Marshal = 3,
    FactionLeader = 4, // slice G task 3
    Executive = 5,     // slice G task 7
    Commander = 6,     // fleet commander (fills FleetRecord.CommanderId)
}

/// <summary>What threshold event minted a notable. Stable ids.</summary>
public enum NotableType
{
    None = 0,
    Founder = 1,    // led a founding convoy
    WarHero = 2,    // great battle (slice H arms the trigger)
    Prophet = 3,    // sacral surge (slice G task 3)
    PirateLord = 4, // piracy concentration (slice G task 7)
    Magnate = 5,    // corporate boom (slice G task 7)
    Explorer = 6,   // ruin expedition (slice I)
}

/// <summary>An individual (characters.md): sparse by construction, minted
/// deterministically on demand, biographical by derivation (P8 — the event
/// log is the biography; nothing here restates it). Characters have their
/// own id space; events reference them through ICharacterPayload while the
/// institution rides the Actors list. Registry in SimState.Characters,
/// id order (P6).</summary>
public sealed class Character
{
    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; }
    public int CultureId { get; }
    /// <summary>Home polity (actor id) — the realm whose politics they color.</summary>
    public int PolityId { get; set; }
    public CharacterRole Role { get; set; }
    /// <summary>Role context: polity actor id for court roles, fleet id for
    /// commanders, faction id for leaders, corporation actor id for
    /// executives; −1 for unseated notables.</summary>
    public int InstitutionId { get; set; } = -1;
    public NotableType Notable { get; set; } = NotableType.None;
    public long BirthYear { get; }
    public bool Alive { get; set; } = true;
    /// <summary>World-year of death; meaningful only once !Alive.</summary>
    public long DeathYear { get; set; }
    /// <summary>Position in ideology space per <see cref="IdeologyAxis"/> —
    /// the ruler term of the temperament composition.</summary>
    public double[] IdeologyPosition { get; } = new double[4];
    /// <summary>Risk appetite [0,1].</summary>
    public double Boldness { get; set; }
    /// <summary>Ideology intensity [0,1].</summary>
    public double Zeal { get; set; }
    /// <summary>Execution quality [0,1].</summary>
    public double Competence { get; set; }
    /// <summary>Self over institution [0,1] — feeds the assassination hazard
    /// and (later) defection.</summary>
    public double Ambition { get; set; }
    /// <summary>Accrued from event participation; feeds ruler prestige,
    /// faction patron strength, and commander weight.</summary>
    public double Renown { get; set; }
    /// <summary>Lineage where forms care (dynastic autocracies, steward
    /// dynasties); −1 for the unlineaged.</summary>
    public int DynastyId { get; set; } = -1;

    public Character(int id, string name, int speciesId, int cultureId,
                     int polityId, long birthYear)
    {
        Id = id;
        Name = name;
        SpeciesId = speciesId;
        CultureId = cultureId;
        PolityId = polityId;
        BirthYear = birthYear;
    }
}

/// <summary>A lineage (characters.md §Dynasties): prestige accumulates
/// across reigns and feeds legitimacy. Registry in SimState.Dynasties,
/// id order (P6).</summary>
public sealed class Dynasty
{
    public int Id { get; }
    /// <summary>The house name — its founder's name.</summary>
    public string Name { get; }
    public int FounderCharacterId { get; }
    public int PolityId { get; }
    /// <summary>Accrues per reign-year; feeds the ruler legitimacy term.</summary>
    public double Prestige { get; set; }

    public Dynasty(int id, string name, int founderCharacterId, int polityId)
    {
        Id = id;
        Name = name;
        FounderCharacterId = founderCharacterId;
        PolityId = polityId;
    }
}
