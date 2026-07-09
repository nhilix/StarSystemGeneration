using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>A discrete act, resolved the same step by the Resolution phase
/// (frame/controller-contract.md). One record per contract entry; ids refer to
/// registries later slices land (wars, corporations, lanes, goods, factions,
/// roles, characters). New mechanics may extend these — never add decision
/// points outside Intent.</summary>
public abstract record Act(int ActorId);

// --- Polity acts ---

public sealed record FoundColonyAct(int ActorId, HexCoordinate Target) : Act(ActorId);
// convoy composition attaches in Slice E (fleets)

public sealed record DeclareWarAct(
    int ActorId, int TargetPolityId, string CasusBelli,
    IReadOnlyList<string> Objectives, string Demand) : Act(ActorId);

public enum TreatyVerb { Offer, Accept, Break }

public sealed record TreatyAct(
    int ActorId, int TargetPolityId, int Rung, TreatyVerb Verb) : Act(ActorId);

public sealed record SanctionAct(int ActorId, int TargetPolityId) : Act(ActorId);

public sealed record SettlementResponseAct(int ActorId, int WarId, bool Accept) : Act(ActorId);

public sealed record NationalizeAct(int ActorId, int CorporationId) : Act(ActorId);

public sealed record CharterAct(int ActorId, int CorporationId, bool Grant) : Act(ActorId);

public enum DynasticInstrument { Marriage, Wardship }

public sealed record DynasticInstrumentAct(
    int ActorId, int TargetPolityId, DynasticInstrument Instrument) : Act(ActorId);

public sealed record VassalageAct(int ActorId, int TargetPolityId, bool IsDemand) : Act(ActorId);

/// <summary>Self-imposed lane closure.</summary>
public sealed record QuarantineAct(int ActorId, int LaneId) : Act(ActorId);

/// <summary>Post procurement contract (good, destination, premium — escrowed).
/// Shared by polities and corporations.</summary>
public sealed record ProcurementContractAct(
    int ActorId, int GoodId, int DestinationPortId, double Premium) : Act(ActorId);

// --- Corporation acts ---

public sealed record CharterApplicationAct(int ActorId, int PolityId) : Act(ActorId);

public sealed record MajorAcquisitionAct(int ActorId, int AssetId, bool Abandon) : Act(ActorId);

public sealed record RelocateHeadquartersAct(int ActorId, HexCoordinate Target) : Act(ActorId);

// --- Character personal acts ---

public sealed record PatronizeFactionAct(int ActorId, int FactionId) : Act(ActorId);

public sealed record DefectAct(int ActorId, int ToInstitutionId) : Act(ActorId);

public sealed record RoleResponseAct(int ActorId, int RoleId, bool Accept) : Act(ActorId);

public sealed record MarryAct(int ActorId, int TargetCharacterId) : Act(ActorId);

public sealed record LeadExpeditionAct(int ActorId, HexCoordinate Destination) : Act(ActorId);
