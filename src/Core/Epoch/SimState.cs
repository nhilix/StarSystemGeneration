using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>An event emitted mid-step, awaiting Chronicle finalization (which
/// assigns id and world-year and appends to the log).</summary>
public sealed record StagedEvent(
    ClockStratum Stratum, WorldEventType Type, IReadOnlyList<int> Actors,
    HexCoordinate Location, double Magnitude, double Valence,
    EventVisibility Visibility, EventPayload? Payload);

/// <summary>One line of the phase execution trace — the REPL's step readout.</summary>
public sealed record PhaseTraceEntry(int Epoch, string Phase, string Note);

/// <summary>One actor's Intent-phase output, held for Resolution this step.</summary>
public sealed record ActorDecision(int ActorId, ControllerDecision Decision);

/// <summary>The generational sim-state container the seven phases step:
/// sparse hex-addressed registries over the natural raster
/// (space-and-travel.md) — there is no per-cell political state. Iteration
/// order is fixed everywhere: registries by id, cells by spiral index (P6).</summary>
public sealed class SimState
{
    public EpochSimConfig Config { get; }
    /// <summary>The natural raster — nature's fields, no political meaning.</summary>
    public GalaxySkeleton Skeleton { get; }
    public int EpochIndex { get; set; }
    public int WorldYear { get; set; }
    /// <summary>Running total of credits minted by bounded sovereign issuance
    /// (the second declared mint, monetary-equilibrium design §5) — a level
    /// across the whole sim, never reset per epoch, mirroring how the endowed
    /// PolityEmerged count accumulates. The conservation residual subtracts it.</summary>
    public double CumulativeFiatIssued { get; set; }
    /// <summary>Running total of credits minted by the always-on steady issuance
    /// channel (the third declared mint, Part B) — a level across the whole sim,
    /// never reset per epoch, mirroring CumulativeFiatIssued. The conservation
    /// residual subtracts it too.</summary>
    public double CumulativeSteadyIssuance { get; set; }
    /// <summary>Actor registry in id order — the fixed iteration order.</summary>
    public List<Actor> Actors { get; } = new List<Actor>();
    /// <summary>Polity-specific state beside the actor substrate, actor-id order.</summary>
    public List<PolityRecord> Polities { get; } = new List<PolityRecord>();
    /// <summary>The currencies in circulation (slice CU-1) — one per living
    /// polity, id order (P6). Retired currencies stay as history. Empty until
    /// genesis wiring mints them (a later task).</summary>
    public List<Currency> Currencies { get; } = new List<Currency>();
    /// <summary>The keystone registry: political geography derives from it.</summary>
    public List<Port> Ports { get; } = new List<Port>();
    public List<Lane> Lanes { get; } = new List<Lane>();
    public List<Facility> Facilities { get; } = new List<Facility>();
    /// <summary>Frozen hex-tier systems, keyed by hex (locality slice §1) —
    /// the first time construction/population touches a hex the generator is
    /// called once and its result memoized here. In-memory only: the bodies
    /// re-derive from the pure generator on load (the hex tier is never
    /// persisted), only the settled-hex SET is serialized. A committed empty
    /// reach stores a null system but stays a key (still "settled").
    /// Iterate SORTED for any output (P6).</summary>
    public Dictionary<HexCoordinate, StarSystem?> SettledSystems { get; }
        = new Dictionary<HexCoordinate, StarSystem?>();
    /// <summary>Depletable per-body resource stocks (body-resource-stock
    /// design), keyed (hex, body). Unlike SettledSystems this is REAL mutable
    /// state — rolled once when a Mine/ExcavationSite claims a body, then
    /// decremented as it extracts — so it is genuinely serialized, not
    /// re-derived. Iterate SORTED (q, r, star, slot) for any output (P6).</summary>
    public Dictionary<(HexCoordinate Hex, BodyRef Body), Stock> BodyResources
    { get; } = new Dictionary<(HexCoordinate, BodyRef), Stock>();
    /// <summary>Per-polity ship designs — lineage entries in id order;
    /// improved marks append, never edit (fleets/ships-and-fleets.md).</summary>
    public List<ShipDesign> Designs { get; } = new List<ShipDesign>();
    public List<FleetRecord> Fleets { get; } = new List<FleetRecord>();
    /// <summary>Losses conserve into wreckage at real hexes — salvage
    /// sites; the narrative layer compiles them in I (P4).</summary>
    public List<WreckageRecord> Wreckage { get; } = new List<WreckageRecord>();
    public List<PopulationSegment> Segments { get; } = new List<PopulationSegment>();
    /// <summary>One market per port, parallel to Ports (market id = port id).</summary>
    public List<Market> Markets { get; } = new List<Market>();
    /// <summary>The slow identity layer's registry. Seeded one per species;
    /// schisms and native emergences mint new entries (separation-drift
    /// splits and blending remain undone — slice J acceptance).</summary>
    public List<Culture> Cultures { get; } = new List<Culture>();
    public List<Loan> Loans { get; } = new List<Loan>();
    /// <summary>Sparse by construction (characters.md): role occupants and
    /// notables only, own id space, minted on demand deterministically.</summary>
    public List<Character> Characters { get; } = new List<Character>();
    public List<Dynasty> Dynasties { get; } = new List<Dynasty>();
    /// <summary>Interest blocs inside polities — pressure without a
    /// controller slot until graduation (frame/actors.md). Dead factions
    /// stay as history.</summary>
    public List<Faction> Factions { get; } = new List<Faction>();
    /// <summary>Emergent economic institutions (economy/corporations.md) —
    /// actors of Kind.Corporation with conserved books. Dead corps stay as
    /// history.</summary>
    public List<Corporation> Corporations { get; } = new List<Corporation>();
    /// <summary>Relations state per pair of polities that have met
    /// (interpolity/relations.md) — creation order (contact scans pairs
    /// ascending, P6). The pressure gauge war reads.</summary>
    public List<PolityRelation> Relations { get; } = new List<PolityRelation>();
    /// <summary>Wars declared and fought (interpolity/war.md) — id order
    /// (P6); ended wars stay as history.</summary>
    public List<War> Wars { get; } = new List<War>();
    /// <summary>Public events' word in transit (perception-and-news.md):
    /// emitted at Chronicle, delivered by Perception when age covers the
    /// news delay — id order (P6); expired pulses stay as history.</summary>
    public List<NewsPulse> Pulses { get; } = new List<NewsPulse>();
    /// <summary>Anchored points of interest compiled from residue every
    /// Chronicle (chronicle-and-poi.md) — id order (P6); depleted POIs
    /// stay as history.</summary>
    public List<PoiRecord> Pois { get; } = new List<PoiRecord>();
    /// <summary>Contagions riding the lanes (slice I) — id order (P6);
    /// burned-out plagues stay as history.</summary>
    public List<Plague> Plagues { get; } = new List<Plague>();
    /// <summary>In-flight work: every duration in the world is a project
    /// here (spec 2026-07-11 time-and-logistics §1) — id order (P6);
    /// completed and cancelled projects stay as history.</summary>
    public List<Project> Projects { get; } = new List<Project>();
    /// <summary>Goods in transit (spec §4b) — id order (P6). In-flight
    /// only: arrivals and losses leave the registry (freight is ambient,
    /// not history); NextShipmentId keeps identity stable across it.</summary>
    public List<Shipment> Shipments { get; } = new List<Shipment>();
    public int NextShipmentId { get; set; }
    /// <summary>The open order book (contract-economy spec §1) — id order
    /// (P6). Live orders only: fills and cancels leave the registry (the
    /// book is ambient, not history); NextOrderId keeps identity stable.</summary>
    public List<MarketOrder> Orders { get; } = new List<MarketOrder>();
    public int NextOrderId { get; set; }
    /// <summary>Open and in-transit courier contracts (spec §1) — id order
    /// (P6). Live only: delivered/lost/expired retire from the registry;
    /// NextCourierId keeps identity stable.</summary>
    public List<CourierContract> Couriers { get; } = new List<CourierContract>();
    public int NextCourierId { get; set; }
    /// <summary>The sim-health series the engine's always-on probe feeds —
    /// in-memory only, never serialized (sim-health spec §1).</summary>
    public MetricSeries Health { get; } = new MetricSeries();
    public EventLog Log { get; } = new EventLog();
    public List<PhaseTraceEntry> Trace { get; } = new List<PhaseTraceEntry>();
    /// <summary>Events emitted this step, finalized by Chronicle.</summary>
    public List<StagedEvent> Staged { get; } = new List<StagedEvent>();
    /// <summary>This step's Intent output in actor-id order, consumed by Resolution.</summary>
    public List<ActorDecision> Decisions { get; } = new List<ActorDecision>();

    public SimState(EpochSimConfig config, GalaxySkeleton skeleton)
    {
        Config = config;
        Skeleton = skeleton;
    }

    /// <summary>The conserved credit book behind any earning actor — a
    /// polity's record or a corporation's (slice G). Production and payouts
    /// move money through this, never caring who is earning (P4).</summary>
    public ICreditLedger LedgerOf(int actorId)
    {
        if (actorId < Polities.Count && Polities[actorId].ActorId == actorId)
            return Polities[actorId];
        foreach (var p in Polities)
            if (p.ActorId == actorId) return p;
        foreach (var c in Corporations)
            if (c.ActorId == actorId) return c;
        throw new KeyNotFoundException($"no credit ledger for actor {actorId}");
    }

    /// <summary>The relation between two polities, or null before contact.
    /// Order-insensitive (relations key the smaller actor id first).</summary>
    public PolityRelation? RelationOf(int polityA, int polityB)
    {
        int a = polityA < polityB ? polityA : polityB;
        int b = polityA < polityB ? polityB : polityA;
        foreach (var r in Relations)
            if (r.PolityAId == a && r.PolityBId == b) return r;
        return null;
    }

    /// <summary>The currency record for an id (registry is id-ordered and
    /// dense once genesis mints them; a scan covers any later interleaving).</summary>
    public Currency CurrencyOf(int currencyId)
    {
        if (currencyId >= 0 && currencyId < Currencies.Count
            && Currencies[currencyId].Id == currencyId)
            return Currencies[currencyId];
        foreach (var cur in Currencies)
            if (cur.Id == currencyId) return cur;
        throw new KeyNotFoundException($"no currency {currencyId}");
    }

    /// <summary>The numeraire rate of a currency id, defaulting to the dormant 1:1
    /// rate (1.0) for the pre-genesis sentinel (id &lt; 0) or any id not yet in the
    /// registry — the same "no rate exists, treat as 1:1" convention
    /// <see cref="ConvertCurrency"/> applies to an unwired id. Lets a numeraire
    /// read (a corporation's wallet total) stay well-defined in the dormant
    /// single-currency world before genesis mints the currency table, without the
    /// throwing <see cref="CurrencyOf"/> lookup. In a live post-genesis run every
    /// held currency is registered, so this is byte-identical to the direct rate.</summary>
    public double NumeraireRateOf(int currencyId)
    {
        if (currencyId >= 0 && currencyId < Currencies.Count
            && Currencies[currencyId].Id == currencyId)
            return Currencies[currencyId].NumeraireRate;
        foreach (var cur in Currencies)
            if (cur.Id == currencyId) return cur.NumeraireRate;
        return 1.0;
    }

    /// <summary>Mint a brand-new currency for a freshly founded polity and assign
    /// it as that polity's own (slice CU-1 genesis). The single chokepoint every
    /// polity-creation path routes through — entry (<c>InteriorPhase</c>),
    /// graduation splits (<c>GraduationOps.FoundSplinter</c>, so schisms and civil
    /// wars alike), and federation fusion (<c>FederationOps.Federate</c>). Registry
    /// id = list index (dense, id order P6); the currency starts at
    /// <see cref="Currency.NumeraireRate"/> = 1.0 and is recomputed next epoch by
    /// <see cref="FxOps"/>. Named after the polity for the REPL/inspection surface;
    /// the id, not the name, is the key. Must run before anything converts money
    /// against the new polity's currency (e.g. before a splinter's
    /// <see cref="GraduationOps.SeedTreasury"/>).</summary>
    public Currency FoundCurrency(int polityId)
    {
        var pr = PolityOf(polityId);
        int id = Currencies.Count;
        var currency = new Currency(id, Actors[polityId].Name, polityId);
        Currencies.Add(currency);
        pr.CurrencyId = id;
        return currency;
    }

    /// <summary>The one shared FX primitive (currency-and-FX design): value of
    /// <paramref name="amount"/> of <paramref name="fromCurrencyId"/> expressed
    /// in <paramref name="toCurrencyId"/>, via the numeraire ratio. A conversion
    /// is a transfer between two currencies' supplies, not a mint — the
    /// per-currency conserved-transfer counters
    /// (<see cref="Currency.CumulativeConvertedIn"/>/<c>Out</c>) are wired by
    /// the conversion-integration and conservation tasks; this slice keeps the
    /// primitive pure arithmetic so the ledger draw-down can use it.</summary>
    public double ConvertCurrency(double amount, int fromCurrencyId, int toCurrencyId)
    {
        if (fromCurrencyId == toCurrencyId) return amount;
        // pre-genesis sentinel on either side (a not-yet-minted or a loaded-but-
        // not-yet-deserialized currency, task 10): no rate exists, so a transfer
        // touching it is dormant 1:1 — the single-currency era, byte-identical to
        // the old raw path. RecordConversion likewise no-ops on a negative id, so
        // the pair stays consistent. Once BOTH sides are real, FX applies.
        if (fromCurrencyId < 0 || toCurrencyId < 0) return amount;
        var from = CurrencyOf(fromCurrencyId);
        var to = CurrencyOf(toCurrencyId);
        return amount * from.NumeraireRate / to.NumeraireRate;
    }

    /// <summary>Record a cross-currency transfer between two currencies' supplies
    /// (design "Conservation &amp; determinism": a conversion is a transfer, never
    /// a mint) — <paramref name="outAmount"/> of <paramref name="fromCurrencyId"/>
    /// leaves circulation and <paramref name="inAmount"/> of
    /// <paramref name="toCurrencyId"/> enters, tracked by the paired
    /// <see cref="Currency.CumulativeConvertedOut"/>/<c>In</c> counters so the
    /// per-currency residual nets it out. Amounts are each in their OWN currency's
    /// units (the conversion direction, not the arithmetic direction, decides which
    /// side is out vs in). A no-op when the currencies match or either side is
    /// unwired (id &lt; 0, i.e. pre-genesis) — the single-currency world records
    /// nothing.</summary>
    public void RecordConversion(int fromCurrencyId, double outAmount,
                                 int toCurrencyId, double inAmount)
    {
        if (fromCurrencyId == toCurrencyId
            || fromCurrencyId < 0 || toCurrencyId < 0) return;
        CurrencyOf(fromCurrencyId).CumulativeConvertedOut += outAmount;
        CurrencyOf(toCurrencyId).CumulativeConvertedIn += inAmount;
    }

    /// <summary>The currency a port's market is denominated in — the port-owning
    /// polity's <see cref="PolityRecord.CurrencyId"/>. Every price, bid, ask, and
    /// escrow at that port is in this currency. −1 before genesis wires currencies
    /// (the dormant single-currency world), which every conversion site treats as
    /// "no conversion".</summary>
    public int LocalCurrencyOf(int portId) =>
        PolityOf(Ports[portId].OwnerActorId).CurrencyId;

    /// <summary>The currency a port's market is denominated in, or −1 when the
    /// port sits unowned (no live-polity owner) or the id is out of range — the
    /// non-throwing counterpart to <see cref="LocalCurrencyOf"/>. The
    /// ownership-change and migration conversion sites use this so a transfer
    /// touching an unowned port degrades to the dormant 1:1 path (which
    /// <see cref="ConvertCurrency"/>/<see cref="RecordConversion"/> both treat as
    /// a no-op) rather than throwing — matching how the conservation walk
    /// (<c>SupplyOps</c>) resolves the same wealth to −1.</summary>
    public int LocalCurrencySafe(int portId)
    {
        if (portId < 0 || portId >= Ports.Count) return -1;
        int owner = Ports[portId].OwnerActorId;
        if (owner < 0) return -1;
        if (owner < Polities.Count && Polities[owner].ActorId == owner)
            return Polities[owner].CurrencyId;
        foreach (var p in Polities)
            if (p.ActorId == owner) return p.CurrencyId;
        return -1;
    }

    /// <summary>Credit <paramref name="amount"/> of a market's local currency
    /// (<paramref name="localCurrencyId"/>) to an earner, converting into the
    /// earner's own currency where it differs (a polity) or banking the local
    /// currency unconverted (a corporation), and recording the transfer. Returns
    /// the amount banked in the earner ledger's own denomination, for a
    /// Receipts mirror. Pre-genesis (<paramref name="localCurrencyId"/> &lt; 0)
    /// this is the old raw single-currency credit — byte-identical, and it keeps a
    /// corporation's empty <see cref="Corporation.Holdings"/> off the (nonexistent)
    /// currency table.</summary>
    public double CreditLocal(int earnerActorId, double amount, int localCurrencyId)
    {
        // Deposit handles every case, including the pre-genesis sentinel
        // (localCurrencyId < 0): a polity banks it raw same-currency, a corporation
        // banks it into the matching wallet bucket with the dormant 1:1 numeraire
        // rate (SimState.NumeraireRateOf). Genesis now wires a currency to every
        // polity before any market/courier/corp activity, so a live run only ever
        // passes a real (>= 0) id here; the sentinel path survives for pre-genesis
        // unit states. The transitional raw-Credits bridge is gone (task 7).
        return LedgerOf(earnerActorId).Deposit(this, amount, localCurrencyId);
    }

    /// <summary>Debit enough of a payer's ledger to provide <paramref name="amount"/>
    /// of a market's local currency (<paramref name="localCurrencyId"/>), converting
    /// out of the payer's own currency (a polity, which may go negative) or drawing
    /// the wallet down (a corporation, which caps at what it holds), and recording
    /// the transfer. Returns the amount actually provided in local-currency terms.
    /// Pre-genesis (<paramref name="localCurrencyId"/> &lt; 0) this is the old raw
    /// single-currency debit — byte-identical.</summary>
    public double DebitLocal(int payerActorId, double amount, int localCurrencyId)
    {
        // Withdraw handles every case, including the pre-genesis sentinel
        // (localCurrencyId < 0): a polity pays raw same-currency (and may go
        // negative), a corporation draws its wallet down with the dormant 1:1 rate.
        // See CreditLocal above — a live run only ever passes a real id.
        return LedgerOf(payerActorId).Withdraw(this, amount, localCurrencyId);
    }

    /// <summary>Force-convert every port-resolved money holder at a port whose
    /// owner is changing — resident <see cref="PopulationSegment.Wealth"/> AND any
    /// resting buy-order escrow (<see cref="MarketOrder.EscrowCredits"/>) or courier
    /// fee escrow (<see cref="Courier.FeeEscrow"/>) denominated in that port's
    /// market currency — from the old owner's currency into the new owner's,
    /// recording each transfer. <c>SupplyOps</c> resolves ALL of these by the port's
    /// current owner-currency, so a bare owner swap re-denominates them 1:1 and
    /// leaks per-currency conservation; this is the shared conversion the three
    /// ownership-change seams (federation/absorption, war capture, secession) apply
    /// at the moment of transfer (currency-and-FX design, "Data model" /
    /// "Conservation &amp; determinism"). A same-currency change, or an unwired side
    /// (id &lt; 0), is a no-op via <see cref="ConvertCurrency"/>/<see cref="RecordConversion"/>.</summary>
    public void ConvertPortHoldings(int portId, int fromCurrencyId, int toCurrencyId)
    {
        if (fromCurrencyId == toCurrencyId) return;
        foreach (var s in Segments)                           // id order (P6)
            if (s.PortId == portId && s.Wealth != 0)
            {
                double c = ConvertCurrency(s.Wealth, fromCurrencyId, toCurrencyId);
                RecordConversion(fromCurrencyId, s.Wealth, toCurrencyId, c);
                s.Wealth = c;
            }
        foreach (var o in Orders)                             // id order (P6)
            if (o.PortId == portId && o.EscrowCredits != 0)
            {
                double c = ConvertCurrency(o.EscrowCredits, fromCurrencyId, toCurrencyId);
                RecordConversion(fromCurrencyId, o.EscrowCredits, toCurrencyId, c);
                o.EscrowCredits = c;
            }
        foreach (var cr in Couriers)                          // id order (P6)
            if ((cr.Status == CourierStatus.Open
                 || cr.Status == CourierStatus.InTransit)
                && cr.OriginPortId == portId && cr.FeeEscrow != 0)
            {
                double c = ConvertCurrency(cr.FeeEscrow, fromCurrencyId, toCurrencyId);
                RecordConversion(fromCurrencyId, cr.FeeEscrow, toCurrencyId, c);
                cr.FeeEscrow = c;
            }
    }

    /// <summary>The corporation record behind an actor id, or null.</summary>
    public Corporation? CorporationOf(int actorId)
    {
        foreach (var c in Corporations)
            if (c.ActorId == actorId) return c;
        return null;
    }

    /// <summary>The polity record for an actor id (registry is actor-id ordered
    /// and dense over polity actors seeded at genesis).</summary>
    public PolityRecord PolityOf(int actorId)
    {
        // polity actors are seeded first and densely, so id == index today;
        // fall back to a scan if later slices interleave other actor kinds
        if (actorId < Polities.Count && Polities[actorId].ActorId == actorId)
            return Polities[actorId];
        foreach (var p in Polities)
            if (p.ActorId == actorId) return p;
        throw new KeyNotFoundException($"no polity record for actor {actorId}");
    }
}
