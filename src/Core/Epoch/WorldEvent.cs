using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Which clock layer emitted an event (frame/time.md: the four clocks).</summary>
public enum ClockStratum { Cosmic, Evolutionary, Generational, Play }

/// <summary>The design's eight type families (narrative/chronicle-and-poi.md),
/// exactly and in this order.</summary>
public enum EventFamily
{
    Cosmic, Evolutionary, Economic, Political, Military, Diplomatic, Corporate,
    Character,
}

/// <summary>Public emits a news pulse; regional spreads by contact; secret emits
/// nothing (the substrate future spy systems need).</summary>
public enum EventVisibility { Public, Regional, Secret }

/// <summary>Concrete event types. Values are STABLE (never renumber or reuse a
/// shipped value) and live in 100-blocks per family — see
/// <see cref="WorldEventTypes.FamilyOf"/>. Later slices append.</summary>
public enum WorldEventType
{
    // 0–99 cosmic · 100–199 evolutionary · 200–299 economic · 300–399 political
    // 400–499 military · 500–599 diplomatic · 600–699 corporate · 700–799 character
    DwarfGalaxyMerged = 0,
    AgnIgnited = 1,
    GlobularFormed = 2,
    FirstLife = 100,
    SapienceEmerged = 101,
    SpaceflightReached = 102,
    PrecursorWaveRose = 103,
    PrecursorWaveFell = 104,
    PrecursorContact = 105,
    LaneOpened = 200,
    PortTierRaised = 201,
    FamineStruck = 202,
    FacilityBuilt = 203,
    LoanIssued = 204,
    LoanDefaulted = 205,
    MigrationWave = 206,
    TechAdvanced = 207,
    PrecursorSiteCharted = 208,
    PlagueOutbreak = 209,
    PlagueBurnedOut = 210,
    PolityEmerged = 300,
    PortEstablished = 301,
    SchismDeclared = 302,
    CoupStruck = 303,
    FactionFormed = 304,
    FactionDissolved = 305,
    RevoltCrushed = 306,
    GovernmentReformed = 307,
    EmergenceSuppressed = 308,
    NativesIntegrated = 309,
    RuinsFallSilent = 310,
    CapitalRuined = 311,
    MemorialRaised = 312,
    QuarantineImposed = 313,
    ShipClassLaunched = 400,
    FleetAttrition = 401,
    ConvoyDispatched = 402,
    WarDeclared = 403,
    BorderIncident = 404,
    BattleFought = 405,
    SiegeBegun = 406,
    PortCaptured = 407,
    BattlefieldMarked = 408,
    FirstContact = 500,
    ClaimRaised = 501,
    ClaimReleased = 502,
    TreatySigned = 503,
    TreatyBroken = 504,
    FederationFormed = 505,
    VassalageBound = 506,
    VassalAbsorbed = 507,
    VassalSeceded = 508,
    DynasticInstrument = 509,
    PeaceSettled = 510,
    CorporationChartered = 600,
    PirateBandFormed = 601,
    CorporationNationalized = 602,
    CorporationBankrupt = 603,
    NicheDied = 604,
    RulerAscended = 700,
    CharacterDied = 701,
    SuccessionCrisis = 702,
    NotableEmerged = 703,
}

public static class WorldEventTypes
{
    /// <summary>Family from the type's stable 100-block. Throws on values
    /// outside the eight blocks — a mis-numbered type must fail loudly, not
    /// map to a phantom family.</summary>
    public static EventFamily FamilyOf(WorldEventType type)
    {
        int block = (int)type / 100;
        if ((int)type < 0 || block > (int)EventFamily.Character)
            throw new System.ArgumentOutOfRangeException(nameof(type), type,
                "event type outside the eight stable family blocks");
        return (EventFamily)block;
    }
}

/// <summary>Type-specific payload root; one derived record per event type.</summary>
public abstract record EventPayload;

/// <summary>An infalling dwarf galaxy arrives — the biggest source of
/// seed-to-seed structural variety (genesis/cosmic-genesis.md §Features).</summary>
public sealed record DwarfGalaxyMergedPayload(
    int FeatureId, string Name, double Mass) : EventPayload;

/// <summary>An AGN accretion epoch: a sterilization/enrichment wave over an
/// inner radius — the evolutionary clock reads it directly.</summary>
public sealed record AgnIgnitedPayload(
    int FeatureId, int WaveRadiusCells) : EventPayload;

/// <summary>An ancient compact metal-poor cluster forms in the earliest
/// steps — rare exotic terrain with its own hex-tier star-table bias.</summary>
public sealed record GlobularFormedPayload(int FeatureId, string Name) : EventPayload;

/// <summary>The galaxy's first abiogenesis — life exists (one event per
/// history; everything after is spread and evolution).</summary>
public sealed record FirstLifePayload : EventPayload;

/// <summary>A sapient origin registers on the emergence schedule
/// (life-and-precursors.md §The step loop).</summary>
public sealed record SapienceEmergedPayload(int OriginId) : EventPayload;

/// <summary>An origin reaches spaceflight in deep time — a precursor wave
/// begins (current-era origins emerge through the epoch sim instead).</summary>
public sealed record SpaceflightReachedPayload(int OriginId) : EventPayload;

/// <summary>A precursor wave plants its capital and begins its arc.</summary>
public sealed record PrecursorWaveRosePayload(
    int WaveId, string Name, int VigorClass) : EventPayload;

/// <summary>A wave's cause-typed ending — its residue signature is in the
/// precursor registry (life-and-precursors.md §The coarse civ-arc sim).</summary>
public sealed record PrecursorWaveFellPayload(
    int WaveId, string Name, int EndCause, int ExtentCells) : EventPayload;

/// <summary>Two live waves met: war, absorption, or partition — borders
/// that predate all current life.</summary>
public sealed record PrecursorContactPayload(
    int WaveAId, int WaveBId, int Resolution) : EventPayload;

public sealed record PolityEmergedPayload(string PolityName) : EventPayload;

/// <summary>Claiming space is building a port (space-and-travel.md).</summary>
public sealed record PortEstablishedPayload(string PolityName, int PortId) : EventPayload;

/// <summary>Paired port infrastructure: the lane links two port registry ids.</summary>
public sealed record LaneOpenedPayload(int PortAId, int PortBId) : EventPayload;

public sealed record PortTierRaisedPayload(int PortId, int NewTier) : EventPayload;

/// <summary>Unmet subsistence at a port market — the famine consequence
/// (economy/markets.md §Clearing). Shortfall is the unmet fraction [0,1].</summary>
public sealed record FamineStruckPayload(int PortId, double Shortfall) : EventPayload;

public sealed record FacilityBuiltPayload(int FacilityId, int TypeId, int Tier) : EventPayload;

public sealed record LoanIssuedPayload(
    int LoanId, int LenderActorId, int BorrowerActorId, double Principal) : EventPayload;

/// <summary>Unpayable obligation: reputation damage, relations hit, collateral
/// seizure (economy/markets.md §Credit).</summary>
public sealed record LoanDefaultedPayload(
    int LoanId, int LenderActorId, int BorrowerActorId) : EventPayload;

/// <summary>A refugee exodus — the fast desperate migration variant
/// (population-and-identity.md §Migration).</summary>
public sealed record MigrationWavePayload(
    int FromPortId, int ToPortId, double Size) : EventPayload;

/// <summary>An improved mark joins a design lineage — visible cultural
/// history (fleets/ships-and-fleets.md §Chassis grid).</summary>
public sealed record ShipClassLaunchedPayload(
    int DesignId, string Name, int Mark) : EventPayload;

/// <summary>Hulls wrecked by failed supply — losses conserve into wreckage
/// at the hex where they died (fleets/ships-and-fleets.md §Attrition).</summary>
public sealed record FleetAttritionPayload(
    int FleetId, int HullsLost) : EventPayload;

/// <summary>A convoy departs under the Expedition posture — colony convoys
/// make founding physical (fleets doc §Postures, space-and-travel.md).</summary>
public sealed record ConvoyDispatchedPayload(
    int FleetId, int FromPortId, int TargetQ, int TargetR) : EventPayload;

/// <summary>An interest coalesces into a faction
/// (factions-and-government.md §Faction formation).</summary>
public sealed record FactionFormedPayload(
    int FactionId, string Name, int Basis, int PolityId) : EventPayload;

/// <summary>A spent interest disbands; its war chest returns to the people.</summary>
public sealed record FactionDissolvedPayload(
    int FactionId, string Name) : EventPayload;

/// <summary>A persistent niche incorporates through the charter graduation
/// (economy/corporations.md §Founding). HostPolityId −1 = the outlaw path.</summary>
public sealed record CorporationCharteredPayload(
    int CorpId, string Name, int HostPolityId, int Niche) : EventPayload;

/// <summary>The raiding niche's institution — chartered by no one.</summary>
public sealed record PirateBandFormedPayload(
    int CorpId, string Name) : EventPayload;

/// <summary>The state seizes a de facto power: assets to the treasury,
/// scandal to the news (corporations.md §Influence).</summary>
public sealed record CorporationNationalizedPayload(
    int CorpId, string Name, int PolityId) : EventPayload;

/// <summary>Default cascade → dissolution (corporations.md §Death).</summary>
public sealed record CorporationBankruptPayload(
    int CorpId, string Name) : EventPayload;

/// <summary>The deposit exhausts, the lane closes, or the margin
/// evaporates — the corporation follows its niche into history.</summary>
public sealed record NicheDiedPayload(
    int CorpId, string Name, int Niche) : EventPayload;

/// <summary>Two polities meet — reach overlapped (interpolity/relations.md
/// §Contact). The relation seeds at its first-stance targets.</summary>
public sealed record FirstContactPayload(
    int PolityAId, int PolityBId, string PolityAName, string PolityBName)
    : EventPayload;

/// <summary>A standing claim raises — a tension source that persists until
/// its cause resolves (§Relations state per pair).</summary>
public sealed record ClaimRaisedPayload(
    int HolderPolityId, int AgainstPolityId, int ClaimType, int SubjectId)
    : EventPayload;

/// <summary>A standing claim's cause resolved; the grudge may now cool.</summary>
public sealed record ClaimReleasedPayload(
    int HolderPolityId, int AgainstPolityId, int ClaimType, int SubjectId)
    : EventPayload;

/// <summary>Mutual consent seals a treaty rung (interpolity/relations.md
/// §Relations state per pair). Rung is the TreatyRung reached.</summary>
public sealed record TreatySignedPayload(
    int PolityAId, int PolityBId, string PolityAName, string PolityBName,
    int Rung) : EventPayload;

/// <summary>A rung breaks — a reputation event the galaxy hears; warmth
/// crashes with it.</summary>
public sealed record TreatyBrokenPayload(
    int BreakerPolityId, int OtherPolityId, string BreakerName,
    string OtherName, int Rung) : EventPayload;

/// <summary>Two allies fuse into a NEW polity (interpolity/relations.md
/// §Federation) — it plays subsequent epochs as itself.</summary>
public sealed record FederationFormedPayload(
    int NewPolityId, string NewPolityName, int ParentAId, int ParentBId,
    string ParentAName, string ParentBName) : EventPayload;

/// <summary>The asymmetric rung binds: tribute, defensive obligation,
/// foreign-policy lock (§Vassalage).</summary>
public sealed record VassalageBoundPayload(
    int OverlordPolityId, int VassalPolityId, string OverlordName,
    string VassalName) : EventPayload;

/// <summary>Long stable vassalage completes as peaceful annexation.</summary>
public sealed record VassalAbsorbedPayload(
    int OverlordPolityId, int VassalPolityId, string OverlordName,
    string VassalName) : EventPayload;

/// <summary>Overlord weakness ends the bond — an independence bid that
/// carried (the fought variant is a war, H5+).</summary>
public sealed record VassalSecededPayload(
    int OverlordPolityId, int VassalPolityId, string OverlordName,
    string VassalName) : EventPayload;

/// <summary>An emergence inside claimed space, suppressed
/// (interpolity/relations.md §Natives): exploitation or a protectorate
/// turned cage — every rival gains a standing liberation casus belli.</summary>
public sealed record EmergenceSuppressedPayload(
    int OriginId, int HostPolityId, string HostName, string NativeName,
    int Policy) : EventPayload;

/// <summary>An emergence resolved as membership: the natives join the host
/// with rights per its ideology, an instant cultural minority.</summary>
public sealed record NativesIntegratedPayload(
    int OriginId, int HostPolityId, string HostName, string NativeName)
    : EventPayload;

/// <summary>Tension discharges through a casus belli (interpolity/war.md):
/// a declaration with objectives and a settlement demand.</summary>
public sealed record WarDeclaredPayload(
    int WarId, string WarName, int AttackerId, int DefenderId,
    string AttackerName, string DefenderName, int Cause, int Demand)
    : EventPayload;

/// <summary>A patrol clash or enforcement killing in contested-overlap
/// space (war.md §Causes: the spark). Loaded incidents prime the
/// BorderIncident casus belli; the rest fizzle into demands and apologies.</summary>
public sealed record BorderIncidentPayload(
    int PolityAId, int PolityBId, string PolityAName, string PolityBName,
    bool Loaded) : EventPayload;

/// <summary>An engagement at one objective (war.md §Conduct 2): fleet
/// vectors × fortification × supply × competence, seeded rolls. Both
/// commanders' biographies carry the day (EventLog.ForCharacter).</summary>
public sealed record BattleFoughtPayload(
    int WarId, string WarName, int ObjectiveType, int TargetId,
    int AttackerId, int DefenderId, int Outcome, int AttackerLosses,
    int DefenderLosses, int AttackerCommanderId, string AttackerCommanderName,
    int DefenderCommanderId, string DefenderCommanderName) : EventPayload;

/// <summary>A blockade turns to siege — the port's larder and fortress
/// tiers set the clock (war.md §Conduct 3).</summary>
public sealed record SiegeBegunPayload(
    int WarId, string WarName, int PortId, string AttackerName,
    string DefenderName) : EventPayload;

/// <summary>A fallen port transfers its domain with population segments
/// intact — conquest composition is automatic.</summary>
public sealed record PortCapturedPayload(
    int WarId, string WarName, int PortId, string AttackerName,
    string DefenderName) : EventPayload;

/// <summary>A marriage or wardship between dynastic thrones
/// (interpolity/relations.md §Dynastic instruments) — warmth this
/// generation, a succession claim two reigns later.</summary>
public sealed record DynasticInstrumentPayload(
    int FromPolityId, int ToPolityId, string FromName, string ToName,
    int Instrument) : EventPayload;

/// <summary>The settlement record (war.md §Termination): negotiated from
/// per-objective outcomes — cessions, reparations, vassalization,
/// independence, or white peace. WinnerId −1 = white peace.</summary>
public sealed record PeaceSettledPayload(
    int WarId, string WarName, int Outcome, int WinnerId,
    string AttackerName, string DefenderName, int PortsCeded,
    double Reparations) : EventPayload;

/// <summary>Wreckage crossed the battlefield floor: an anchored POI with
/// salvage value (chronicle-and-poi.md §The POI compiler, slice I).</summary>
public sealed record BattlefieldMarkedPayload(int PoiId, int Hulls) : EventPayload;

/// <summary>A port's people are gone and stayed gone — a dead city anchors
/// ruins (suppressed settlement, salvage in the walls).</summary>
public sealed record RuinsFallSilentPayload(int PoiId, int PortId) : EventPayload;

/// <summary>An annexed polity's seat becomes a ruined metropolis — the
/// cultural claim anchor irredentism and pilgrimage read.</summary>
public sealed record CapitalRuinedPayload(
    int PoiId, int PolityId, string PolityName) : EventPayload;

/// <summary>A deep famine or suppressed emergence anchors a memorial site.
/// Cause: 0 famine, 1 suppression.</summary>
public sealed record MemorialRaisedPayload(int PoiId, int Cause) : EventPayload;

/// <summary>Expansion reached a precursor site: the registry entry becomes
/// a charted, anchored place (dormant remnants keep their flag).</summary>
public sealed record PrecursorSiteChartedPayload(
    int PoiId, int SiteType, bool Dormant, string WaveName) : EventPayload;

/// <summary>Payloads that name an individual — the biography index key
/// (characters have their own id space; the event's Actors list carries the
/// institution). P8: a life reconstructs from the log by this interface.</summary>
public interface ICharacterPayload
{
    int CharacterId { get; }
}

/// <summary>A research threshold crossing climbs a domain's tier ladder
/// (economy/technology.md §Advancement).</summary>
public sealed record TechAdvancedPayload(
    int PolityId, int Domain, int NewTier) : EventPayload;

/// <summary>Domains secede as a new polity — a faction graduates
/// (factions-and-government.md §Graduation).</summary>
public sealed record SchismDeclaredPayload(
    int FactionId, string FactionName, int OldPolityId, int NewPolityId,
    string NewPolityName, int Ports) : EventPayload;

/// <summary>A throne-seeking faction replaces the leadership; ideology
/// lurches. Contested coups record the civil war slice H will fight.</summary>
public sealed record CoupStruckPayload(
    int CharacterId, string CharacterName, int FactionId, string FactionName,
    int PolityId, bool Contested) : EventPayload, ICharacterPayload;

/// <summary>A crushed graduation: martyrs, repression, compounding grievance.</summary>
public sealed record RevoltCrushedPayload(
    int CharacterId, string MartyrName, int FactionId, string FactionName,
    int PolityId) : EventPayload, ICharacterPayload;

/// <summary>The government form changes through a graduation event — a
/// chronicle landmark (§Government forms).</summary>
public sealed record GovernmentReformedPayload(
    int PolityId, int OldForm, int NewForm) : EventPayload;

/// <summary>A character takes a polity's seat — by succession, crisis
/// resolution, or founding (the first ruler ascends silently at entry;
/// only real transitions chronicle).</summary>
public sealed record RulerAscendedPayload(
    int CharacterId, string CharacterName, int PolityId, int DynastyId)
    : EventPayload, ICharacterPayload;

/// <summary>A role-holder or notable dies; the age is world-years lived.</summary>
public sealed record CharacterDiedPayload(
    int CharacterId, string CharacterName, int Role, long AgeYears)
    : EventPayload, ICharacterPayload;

/// <summary>A seat empties with no clear successor — resolved as politics
/// until the war machinery (H) can escalate it (characters.md §Succession).</summary>
public sealed record SuccessionCrisisPayload(
    int CharacterId, string DeadRulerName, int PolityId)
    : EventPayload, ICharacterPayload;

/// <summary>A threshold event mints a named notable (characters.md §Notables).</summary>
public sealed record NotableEmergedPayload(
    int CharacterId, string CharacterName, int NotableType)
    : EventPayload, ICharacterPayload;

/// <summary>One record of the single append-only stream — the event grammar v2
/// (narrative/chronicle-and-poi.md): one schema across all four clocks.
/// Actors are registry ids; the location is a physical hex address
/// (space-and-travel.md: no political fact without a physical carrier).
/// WorldYear is long: the deep-time strata write true world-years ("−6.2 Gyr"
/// is −6.2e9), far past int range (frame/time.md: timescale-aware from birth).</summary>
public sealed record WorldEvent(
    long Id,
    long WorldYear,
    ClockStratum Stratum,
    WorldEventType Type,
    IReadOnlyList<int> Actors,
    HexCoordinate Location,
    double Magnitude,
    double Valence,
    EventVisibility Visibility,
    EventPayload? Payload)
{
    public EventFamily Family => WorldEventTypes.FamilyOf(Type);
}
