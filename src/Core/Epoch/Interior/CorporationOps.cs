using System;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The corporate lifecycle (economy/corporations.md): the niche
/// watcher raises merchant factions where profit persists unclaimed, the
/// charter graduation incorporates them (the host's charter policy gating),
/// operations run the portfolio, dividends feed host-polity elites, and
/// death leaves residue. Cartels and pirate bands are the outlaw cousins —
/// the same persistent-niche rule, chartered nowhere.</summary>
public static class CorporationOps
{
    /// <summary>Corp name suffix per niche (index = CorporateNiche).</summary>
    private static readonly string[] Suffix =
        { "", "Consortium", "Line", "Works", "Cartel", "Corsairs", "Salvors" };

    // ---- the niche watcher (Interior phase) ----

    /// <summary>Scan every entered polity for persistent profit niches
    /// without an incumbent. A detected niche raises (or feeds) the polity's
    /// merchant faction — Corporate basis, persistence counted; a raiding
    /// niche founds a pirate band outright (registry-level until H).</summary>
    public static int WatchNiches(SimState state)
    {
        var knobs = state.Config.Corporate;
        int found = 0;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            if (pr.Interior == null || !state.Actors[pr.ActorId].Entered)
                continue;
            var (niche, target) = DetectNiche(state, pr);

            // the merchant faction: one per polity, fed while a niche holds
            Faction? merchants = null;
            foreach (var f in state.Factions)
                if (f.Active && f.PolityId == pr.ActorId
                    && f.Basis == FactionBasis.Corporate)
                { merchants = f; break; }
            if (niche != CorporateNiche.None && niche != CorporateNiche.Raiding)
            {
                if (merchants == null)
                {
                    merchants = FactionOps.FoundFaction(state, pr,
                                                        FactionBasis.Corporate);
                    found++;
                }
                int years = state.Config.Sim.YearsPerEpoch;
                if (merchants.NicheType == (int)niche && merchants.ContextId == target)
                    merchants.NichePersistenceYears += years;
                else
                {
                    merchants.NicheType = (int)niche;
                    merchants.ContextId = target;
                    merchants.NichePersistenceYears = years;
                }
            }
            else if (merchants != null && merchants.NichePersistenceYears > 0)
                // the opportunity is closing
                merchants.NichePersistenceYears = Math.Max(0,
                    merchants.NichePersistenceYears
                    - state.Config.Sim.YearsPerEpoch);

            if (niche == CorporateNiche.Raiding
                && !BandExists(state, target))
                FoundPirateBand(state, pr, target);
        }
        return found;
    }

    /// <summary>The best unclaimed opportunity in a polity's space, first
    /// match in a fixed order (P6): unserved lane gradients, prohibited
    /// margins, unexploited deposits, industrial gaps, lawless rich lanes.</summary>
    private static (CorporateNiche, int) DetectNiche(SimState state,
                                                     PolityRecord pr)
    {
        var knobs = state.Config.Corporate;
        var eco = state.Config.Economy;

        // unserved profitable lanes → freight line. "Profitable" runs the
        // arbitrage's own math (net of freight, fuel, tax, labor share, on
        // real source inventory) — a raw price gap with nothing to ship or
        // no margin after costs is a mirage that starves its line
        foreach (var lane in state.Lanes)                     // id order (P6)
        {
            var srcPort = state.Ports[lane.PortAId];
            if (srcPort.OwnerActorId != pr.ActorId) continue;
            if (FleetOps.PostedCapacity(state, lane) > 0) continue;
            if (LaneCarriesProfit(state, lane, knobs.FreightNicheMargin))
                return (CorporateNiche.Freight, lane.Id);
        }
        // profitable prohibited niches → cartel
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != pr.ActorId) continue;
            var market = state.Markets[port.Id];
            for (int g = 0; g < market.BlackBookDemand.Length; g++)
                if (market.BlackBookDemand[g] * market.BlackBookPrice[g]
                    > knobs.CartelValueFloor)
                    return (CorporateNiche.Cartel, port.Id);
        }
        // unexploited deposits → extraction conglomerate
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != pr.ActorId) continue;
            var fields = MarketEngine.FieldsAt(state, port.Hex);
            double best = Math.Max(Potentials.Ore(fields),
                Math.Max(Potentials.Volatiles(fields), Potentials.Exotics(fields)));
            if (best < knobs.DepositNichePotential) continue;
            if (!HasFacilityKind(state, port, extraction: true))
                return (CorporateNiche.Extraction, port.Id);
        }
        // salvage in reach → salvage corporation: a battlefield's hulls or
        // a charted (non-dormant) precursor site is a persistent profit
        // niche (chronicle-and-poi.md §The POI compiler, slice I)
        foreach (var poi in state.Pois)                       // id order (P6)
        {
            if (poi.Depleted || SalvorExists(state, poi.Id)) continue;
            bool diggable = poi.Type == PoiType.Battlefield
                    && poi.SalvageRemaining >= state.Config.Poi.SalvageNicheHullFloor
                || (poi.Type == PoiType.PrecursorSite && !poi.Dormant);
            if (!diggable) continue;
            if (NearestOwnPort(state, pr.ActorId, poi.Hex,
                    state.Config.Poi.SalvageReachHexes) >= 0)
                return (CorporateNiche.Salvage, poi.Id);
        }
        // industrial gaps → fabricator combine
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != pr.ActorId) continue;
            var market = state.Markets[port.Id];
            for (int g = 0; g < market.Price.Length; g++)
            {
                if (Goods.Get((GoodId)g).Recipes.Count == 0) continue;
                if (market.Price[g] > knobs.FabricationPriceRatio
                        * Market.InitialPrice(eco, (GoodId)g)
                    && !PortProduces(state, port, (GoodId)g))
                    return (CorporateNiche.Fabrication, port.Id);
            }
        }
        // lawless rich lanes → pirate band (lawlessness × cargo value).
        // Lawlessness has two sources: an owner with no navy at all, or a
        // standing ruin at either lane mouth — a haven no navy roots out,
        // where even thinner cargo tempts (chronicle-and-poi.md
        // live-effects table, the slice J wire)
        bool navyless = true;
        foreach (var fleet in state.Fleets)
        {
            if (fleet.OwnerActorId != pr.ActorId) continue;
            foreach (var g in fleet.Hulls)
                if (ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                { navyless = false; break; }
            if (!navyless) break;
        }
        foreach (var lane in state.Lanes)
        {
            var src = state.Ports[lane.PortAId];
            if (src.OwnerActorId != pr.ActorId) continue;
            bool haven = InRuinsShadow(state, lane);
            if (!navyless && !haven) continue;
            double floor = knobs.RaidCapacityFloor
                * (haven ? state.Config.Poi.LawlessRaidFactor : 1.0);
            if (FleetOps.PostedCapacity(state, lane) >= floor)
                return (CorporateNiche.Raiding, lane.Id);
        }
        return (CorporateNiche.None, -1);
    }

    /// <summary>Does a standing (non-depleted) ruin cast lawlessness over
    /// this lane — Ruins or a RuinedCapital within LawlessnessReachHexes
    /// of either endpoint?</summary>
    private static bool InRuinsShadow(SimState state, Lane lane)
    {
        int reach = state.Config.Poi.LawlessnessReachHexes;
        var hexA = state.Ports[lane.PortAId].Hex;
        var hexB = state.Ports[lane.PortBId].Hex;
        foreach (var poi in state.Pois)                       // id order (P6)
        {
            if (poi.Depleted) continue;
            if (poi.Type != PoiType.Ruins
                && poi.Type != PoiType.RuinedCapital) continue;
            if (HexGrid.Distance(poi.Hex, hexA) <= reach
                || HexGrid.Distance(poi.Hex, hexB) <= reach)
                return true;
        }
        return false;
    }

    /// <summary>Would a hauler on this lane actually clear the freight-niche
    /// margin? Mirrors MarketEngine.Arbitrage's per-unit economics.</summary>
    private static bool LaneCarriesProfit(SimState state, Lane lane,
                                          double margin)
    {
        var eco = state.Config.Economy;
        var portA = state.Ports[lane.PortAId];
        var portB = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(portA.Hex, portB.Hex);
        for (int g = 0; g < Goods.All.Count; g++)
        {
            var (src, dst) = state.Markets[lane.PortAId].Price[g]
                             <= state.Markets[lane.PortBId].Price[g]
                ? (portA, portB) : (portB, portA);
            var mSrc = state.Markets[src.Id];
            if (mSrc.Inventory[g] <= 0) continue;
            double pSrc = mSrc.Price[g];
            double pDst = state.Markets[dst.Id].Price[g];
            double freight = eco.FreightCostPerUnitPerHex * dist;
            double fuel = eco.FuelPerUnitPerHex * dist
                          * mSrc.Price[(int)GoodId.Fuel];
            double dstTax = (state.Actors[dst.OwnerActorId].Policies
                as PolityPolicies ?? PolityPolicies.Default).TaxRate;
            double expectedNet = pDst * (1.0 - dstTax) * (1.0 - eco.LaborShare);
            if (expectedNet > (pSrc + freight + fuel) * (1.0 + margin))
                return true;
        }
        return false;
    }

    private static bool HasFacilityKind(SimState state, Port port, bool extraction)
    {
        foreach (var f in state.Facilities)
        {
            var type = (InfraTypeId)f.TypeId;
            bool isExtraction = type is InfraTypeId.Mine or InfraTypeId.Skimmer
                or InfraTypeId.ExcavationSite;
            if (isExtraction == extraction
                && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                return true;
        }
        return false;
    }

    private static bool PortProduces(SimState state, Port port, GoodId good)
    {
        foreach (var f in state.Facilities)
        {
            if (MarketEngine.AttachedMarketIndex(state, f) != port.Id) continue;
            foreach (var produced in
                     Infrastructure.Get((InfraTypeId)f.TypeId).Produces)
                if (produced == good) return true;
        }
        return false;
    }

    private static bool BandExists(SimState state, int laneId)
    {
        foreach (var corp in state.Corporations)
            if (corp.Active && corp.Niche == CorporateNiche.Raiding
                && corp.TargetId == laneId) return true;
        return false;
    }

    private static bool SalvorExists(SimState state, int poiId)
    {
        foreach (var corp in state.Corporations)
            if (corp.Active && corp.Niche == CorporateNiche.Salvage
                && corp.TargetId == poiId) return true;
        return false;
    }

    /// <summary>Nearest port an actor owns within reach of a hex; −1 none.</summary>
    private static int NearestOwnPort(SimState state, int actorId,
                                      Model.HexCoordinate hex, int reach)
    {
        int best = -1, bestDist = int.MaxValue;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != actorId) continue;
            int d = HexGrid.Distance(port.Hex, hex);
            if (d <= reach && d < bestDist) { best = port.Id; bestDist = d; }
        }
        return best;
    }

    // ---- founding (Interior phase) ----

    /// <summary>Charter graduations: a merchant faction whose niche has
    /// persisted incorporates — cartel niches skip the charter gate (they
    /// are chartered nowhere). Returns charters granted.</summary>
    public static int CharterCheck(SimState state)
    {
        var knobs = state.Config.Corporate;
        int chartered = 0;
        int factions = state.Factions.Count;   // founding mints no factions,
                                               // but stay index-stable anyway
        for (int i = 0; i < factions; i++)
        {
            var faction = state.Factions[i];
            if (!faction.Active || faction.Basis != FactionBasis.Corporate
                || faction.NichePersistenceYears
                   < knobs.CharterPersistenceEpochs
                     * state.Config.Sim.GenerationYears)
                continue;
            var pr = state.PolityOf(faction.PolityId);
            bool outlaw = (CorporateNiche)faction.NicheType == CorporateNiche.Cartel;
            // a salvage expedition needs a harbor: a host that lost every
            // port between detection and charter keeps waiting (review fix 4)
            if ((CorporateNiche)faction.NicheType == CorporateNiche.Salvage
                && NearestOwnPort(state, pr.ActorId,
                    state.Pois[faction.ContextId].Hex, int.MaxValue) < 0)
                continue;
            if (!outlaw)
            {
                double openness = (state.Actors[pr.ActorId].Policies
                    as PolityPolicies ?? PolityPolicies.Default).CharterOpenness;
                if (openness < knobs.CharterOpennessGate) continue;
                // an unfunded venture is a stillbirth: the merchants keep
                // collecting until the war chest can capitalize the charter
                // (cartels need no works — they charter on nerve alone)
                if (faction.Wealth < knobs.CharterCapitalFloor) continue;
            }
            Charter(state, pr, faction, outlaw);
            chartered++;
        }
        return chartered;
    }

    private static void Charter(SimState state, PolityRecord pr,
                                Faction faction, bool outlaw)
    {
        var niche = (CorporateNiche)faction.NicheType;
        int homePort = niche switch
        {
            CorporateNiche.Freight => state.Lanes[faction.ContextId].PortAId,
            // salvors base at the nearest own port to their field
            CorporateNiche.Salvage => NearestOwnPort(state, pr.ActorId,
                state.Pois[faction.ContextId].Hex, int.MaxValue),
            _ => faction.ContextId,
        };
        int actorId = state.Actors.Count;
        string name = CorpName(state, actorId, niche);
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, name,
            state.Ports[homePort].Hex, state.EpochIndex,
            new CorporateController())
        { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId, name,
            outlaw ? -1 : pr.ActorId, niche, homePort, state.WorldYear)
        {
            Credits = faction.Wealth,   // the war chest capitalizes it (P4)
            // the field (or site) the expedition works
            TargetId = niche == CorporateNiche.Salvage ? faction.ContextId : -1,
        };
        faction.Wealth = 0;
        faction.Active = false;         // the merchant faction incorporated
        state.Corporations.Add(corp);

        // the executive suite: the faction's leader moves to the boardroom
        var executive = state.Characters[faction.LeaderCharacterId];
        if (executive.Alive)
        {
            executive.Role = CharacterRole.Executive;
            executive.InstitutionId = corp.ActorId;
        }
        corp.ExecutiveCharacterId = executive.Id;

        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.CorporationChartered, new[] { pr.ActorId, actorId },
            state.Ports[homePort].Hex, Magnitude: corp.Credits, Valence: 0.5,
            outlaw ? EventVisibility.Regional : EventVisibility.Public,
            new CorporationCharteredPayload(corp.Id, name,
                outlaw ? -1 : pr.ActorId, (int)niche)));
        // a salvage charter is a ruin expedition setting out — it arms the
        // Explorer notable (characters.md §Notables, slice I)
        if (niche == CorporateNiche.Salvage)
            CharacterOps.MintNotable(state, pr.ActorId, NotableType.Explorer,
                                     state.Pois[corp.TargetId].Hex);
    }

    /// <summary>The other outlaw cousin: where the profitable niche is
    /// raiding, an institution with a haven and a fearsome name forms —
    /// registry-level until slice H gives it teeth. Its leader is the
    /// pirate-lord notable the chronicle names.</summary>
    private static void FoundPirateBand(SimState state, PolityRecord pr,
                                        int laneId)
    {
        int actorId = state.Actors.Count;
        string name = CorpName(state, actorId, CorporateNiche.Raiding);
        var lane = state.Lanes[laneId];
        var haven = state.Ports[lane.PortAId].Hex;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, name,
            haven, state.EpochIndex, new CorporateController())
        { Entered = true });
        var band = new Corporation(state.Corporations.Count, actorId, name,
            -1, CorporateNiche.Raiding, lane.PortAId, state.WorldYear)
        { TargetId = laneId };   // the haven is a port; the hunt is a lane
        state.Corporations.Add(band);
        var lord = CharacterOps.MintNotable(state, pr.ActorId,
            NotableType.PirateLord, haven);
        if (lord != null)
        {
            lord.Role = CharacterRole.Executive;
            lord.InstitutionId = band.ActorId;
            band.ExecutiveCharacterId = lord.Id;
        }
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PirateBandFormed, new[] { pr.ActorId, actorId },
            haven, Magnitude: 1.0, Valence: -0.6, EventVisibility.Regional,
            new PirateBandFormedPayload(band.Id, name)));
    }

    private static string CorpName(SimState state, int actorId,
                                   CorporateNiche niche)
    {
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed, RollChannel.CorpSeed,
            0, actorId, 100) < 0.4 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.CorpSeed, 0, actorId, i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word)
               + " " + Suffix[(int)niche];
    }

    // ---- operations (Allocation phase) ----

    /// <summary>One epoch of corporate life: upkeep, investment per founding
    /// character, fleet supply, dividends to host elites, lobby money to the
    /// aligned faction, and the deaths (bankruptcy / niche death). Returns
    /// active corporations for the phase note.</summary>
    public static int Operate(SimState state)
    {
        var knobs = state.Config.Corporate;
        int active = 0;
        int corporations = state.Corporations.Count;
        for (int i = 0; i < corporations; i++)
        {
            var corp = state.Corporations[i];
            if (!corp.Active) continue;
            active++;
            var policies = state.Actors[corp.ActorId].Policies
                as CorporationPolicies ?? CorporationPolicies.Default;

            // the boardroom refills like any court: a dead executive's
            // seat passes on (characters.md — role slots stay occupied)
            if (corp.ExecutiveCharacterId < 0
                || !state.Characters[corp.ExecutiveCharacterId].Alive)
            {
                int home = corp.HostPolityId >= 0 ? corp.HostPolityId
                    : state.Ports[corp.HomePortId].OwnerActorId;
                var successor = CharacterOps.Mint(state, home,
                    CharacterRole.Executive, corp.ActorId,
                    state.Config.Character.RulerMintAgeFraction);
                corp.ExecutiveCharacterId = successor.Id;
            }

            RunUpkeep(state, corp);
            switch (corp.Niche)
            {
                case CorporateNiche.Extraction:
                case CorporateNiche.Fabrication:
                    InvestFacilities(state, corp, policies);
                    break;
                case CorporateNiche.Freight:
                    InvestFleet(state, corp, policies);
                    break;
                case CorporateNiche.Cartel:
                    SkimBlackBooks(state, corp);
                    break;
                // raiding: registry-level until slice H arms it
            }
            SupplyFleet(state, corp);

            // dividends flow to host-polity elites — corporate influence IS
            // internal politics (they feed the corporate faction's wealth)
            if (corp.HostPolityId >= 0 && corp.Receipts > 0)
            {
                double dividend = corp.Receipts * policies.DividendRate;
                dividend = Math.Min(dividend, Math.Max(0, corp.Credits));
                if (dividend > 0)
                {
                    corp.Credits -= dividend;
                    ElitesOf(state, corp).Wealth += dividend;
                }
                double lobby = Math.Max(0, corp.Credits) * knobs.LobbyShare;
                if (lobby > 0)
                {
                    corp.Credits -= lobby;
                    ElitesOf(state, corp).Wealth += lobby;
                }
                if (corp.Receipts > knobs.MagnateReceipts
                    && corp.ExecutiveCharacterId >= 0
                    && state.Characters[corp.ExecutiveCharacterId]
                           is { Alive: true, Notable: NotableType.None } exec
                    && CharacterOps.NotableCount(state, corp.HostPolityId)
                       < state.Config.Character.MaxNotablesPerPolity)
                {
                    // a corporate boom mints its magnate — chronicled and
                    // renowned like every other notable (characters.md)
                    exec.Notable = NotableType.Magnate;
                    exec.Renown += state.Config.Character.RenownNotable;
                    state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                        WorldEventType.NotableEmerged,
                        new[] { corp.HostPolityId, corp.ActorId },
                        state.Ports[corp.HomePortId].Hex, Magnitude: 1.0,
                        Valence: 0.5, EventVisibility.Regional,
                        new NotableEmergedPayload(exec.Id, exec.Name,
                                                  (int)NotableType.Magnate)));
                }
            }

            // deaths: the balance sheet or the niche gives out — but a
            // founding gets its build-out grace before the lean clock
            // starts (facilities take epochs to fund, build, and spin up)
            if (corp.Credits < 0)
                Dissolve(state, corp, WorldEventType.CorporationBankrupt);
            else if (corp.Niche != CorporateNiche.Raiding
                     && (state.WorldYear - corp.FoundedYear)
                        / Math.Max(1, state.Config.Sim.GenerationYears)
                        > knobs.FoundingGraceEpochs)
            {
                corp.LeanYears = corp.Receipts < knobs.LeanReceiptsFloor
                    ? corp.LeanYears + state.Config.Sim.YearsPerEpoch : 0;
                if (corp.LeanYears >= knobs.NicheDeathEpochs
                        * state.Config.Sim.GenerationYears)
                    Dissolve(state, corp, WorldEventType.NicheDied);
            }
        }
        return active;
    }

    /// <summary>The dividend-fed elites: the host polity's corporate
    /// faction, raised the moment money starts flowing to it.</summary>
    private static Faction ElitesOf(SimState state, Corporation corp)
    {
        var pr = state.PolityOf(corp.HostPolityId);
        foreach (var f in state.Factions)
            if (f.Active && f.PolityId == pr.ActorId
                && f.Basis == FactionBasis.Corporate)
                return f;
        return FactionOps.FoundFaction(state, pr, FactionBasis.Corporate);
    }

    private static void RunUpkeep(SimState state, Corporation corp)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.OwnerActorId != corp.ActorId) continue;
            if (!MarketEngine.IsActive(state, f)) continue;
            int mIx = MarketEngine.AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
            var market = state.Markets[mIx];
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            double scale = Production.TierCostFactor(f.Tier) * years;
            double met = 1.0;
            foreach (var q in def.UpkeepPerYear)
            {
                double need = q.Quantity * scale;
                if (need <= 0) continue;
                double drawn = market.Draw((int)q.Good, need);
                market.LastCleared[(int)q.Good] += drawn;
                met = Math.Min(met, drawn / need);
            }
            double target = Math.Max(0.05, met);
            f.Condition = target > f.Condition
                ? Math.Min(target, f.Condition + eco.ConditionRecoveryPerYear * years)
                : Math.Max(target, f.Condition - eco.ConditionDecayPerYear * years);
        }
    }

    /// <summary>The facility a builder corp intends next — extraction reads
    /// its terrain, fabrication chases the widest price-over-founding gap.
    /// Shared by the demand pull and the investment step, so the goods the
    /// price signal hauls in are the goods that get built with.</summary>
    private static InfraTypeId PlannedFacility(SimState state, Corporation corp)
    {
        var port = state.Ports[corp.HomePortId];
        if (corp.Niche == CorporateNiche.Extraction)
        {
            var fields = MarketEngine.FieldsAt(state, port.Hex);
            var pick = InfraTypeId.Mine;
            double best = Potentials.Ore(fields);
            if (Potentials.Volatiles(fields) > best)
            { pick = InfraTypeId.Skimmer; best = Potentials.Volatiles(fields); }
            if (Potentials.Exotics(fields) > best)
                pick = InfraTypeId.ExcavationSite;
            return pick;
        }
        var market = state.Markets[port.Id];
        var type = InfraTypeId.Fabricator;
        double bestSignal = 0;
        foreach (var candidate in new[] { InfraTypeId.Refinery,
            InfraTypeId.Chemworks, InfraTypeId.Fabricator,
            InfraTypeId.Foundry, InfraTypeId.ExoticsLab,
            InfraTypeId.ComputeCore })
        {
            var def = Infrastructure.Get(candidate);
            double signal = 0;
            foreach (var good in def.Produces)
                signal = Math.Max(signal, market.Price[(int)good]
                    / Market.InitialPrice(state.Config.Economy, good));
            if (signal > bestSignal) { bestSignal = signal; type = candidate; }
        }
        return type;
    }

    /// <summary>True while a builder corp still wants and can fund another
    /// facility — the same gate the demand pull and the investment use.</summary>
    private static bool WantsFacility(SimState state, Corporation corp)
    {
        int owned = 0;
        foreach (var f in state.Facilities)
            if (f.OwnerActorId == corp.ActorId) owned++;
        return owned < state.Config.Corporate.MaxFacilities;
    }

    /// <summary>Corporate demand registers at the home market each Markets
    /// phase (the slice-E lesson, again: without a price signal nothing
    /// hauls the goods in, and every founding starves at an empty market —
    /// 42 of 47 corp deaths were exactly this stillbirth). Builders pull
    /// their planned build basket; freight lines pull hull components.</summary>
    public static void AddCorporateDemand(SimState state, MarketStepScratch scratch)
    {
        foreach (var corp in state.Corporations)              // id order (P6)
        {
            if (!corp.Active) continue;
            int home = corp.HomePortId;
            switch (corp.Niche)
            {
                case CorporateNiche.Extraction:
                case CorporateNiche.Fabrication:
                    if (!WantsFacility(state, corp)) break;
                    var def = Infrastructure.Get(PlannedFacility(state, corp));
                    foreach (var q in def.BuildCost)
                        scratch.Demand[home][(int)q.Good] += q.Quantity;
                    break;
                case CorporateNiche.Freight:
                    if (corp.Credits <= 0) break;
                    scratch.Demand[home][(int)GoodId.ShipComponents]
                        += state.Config.Corporate.FreightPullComponents;
                    break;
            }
        }
    }

    /// <summary>Salvage supply lands with everyone else's (Markets phase,
    /// after SupplyLands): salvors strip their field — battlefield hulls
    /// become alloys and components at declining grade; a precursor dig
    /// yields exotics and teaches the host polity (the full-consumption
    /// channel TechOps only hinted at). Goods are minted from wreckage the
    /// hull ledger already counted; credits arrive only when buyers pay at
    /// distribution — conserved end to end (P4). Returns working salvors.</summary>
    public static int SalvageLands(SimState state, MarketStepScratch scratch)
    {
        var knobs = state.Config.Poi;
        int years = state.Config.Sim.YearsPerEpoch;
        int working = 0;
        foreach (var corp in state.Corporations)              // id order (P6)
        {
            if (!corp.Active || corp.Niche != CorporateNiche.Salvage) continue;
            if (corp.TargetId < 0 || corp.TargetId >= state.Pois.Count) continue;
            var poi = state.Pois[corp.TargetId];
            if (poi.Depleted) continue;
            int mIx = corp.HomePortId;
            if (poi.Type == PoiType.Battlefield)
            {
                int hulls = (int)Math.Min(poi.SalvageRemaining, Math.Max(1.0,
                    Math.Round(knobs.SalvageHullsPerYear * years)));
                if (hulls <= 0) continue;
                // declining grade: the best pickings go first
                double grade = 0.3 + 0.4 * (poi.Magnitude > 0
                    ? poi.SalvageRemaining / poi.Magnitude : 0.0);
                poi.HullsSalvaged += hulls;
                DepositSalvage(state, scratch, mIx, corp.ActorId,
                    GoodId.Alloys, hulls * knobs.SalvageAlloysPerHull, grade);
                DepositSalvage(state, scratch, mIx, corp.ActorId,
                    GoodId.ShipComponents,
                    hulls * knobs.SalvageComponentsPerHull, grade);
                working++;
            }
            else if (poi.Type == PoiType.PrecursorSite && !poi.Dormant)
            {
                double dig = Math.Min(poi.Magnitude,
                                      knobs.DigMagnitudeDecayPerYear * years);
                if (dig <= 0) continue;
                poi.Magnitude -= dig;
                if (poi.Magnitude <= 0.25) poi.Depleted = true;   // dug out
                DepositSalvage(state, scratch, mIx, corp.ActorId,
                    GoodId.Exotics, knobs.DigExoticsPerYear * years,
                    grade: 0.9);
                if (corp.HostPolityId >= 0)
                {
                    var pr = state.PolityOf(corp.HostPolityId);
                    double lesson = knobs.DigResearchPerYear * years;
                    TechOps.Advance(state, pr, TechDomain.Astrogation, lesson);
                    TechOps.Advance(state, pr, TechDomain.Industrial,
                                    lesson * 0.5);
                }
                working++;
            }
        }
        return working;
    }

    /// <summary>Deposit recovered goods and register the supply so buyers'
    /// payments flow to the salvor at distribution (MarketEngine.Deposit's
    /// pattern — unsold salvage earns nothing).</summary>
    private static void DepositSalvage(SimState state, MarketStepScratch scratch,
        int mIx, int ownerActorId, GoodId good, double qty, double grade)
    {
        if (qty <= 0) return;
        var market = state.Markets[mIx];
        market.Deposit((int)good, qty, grade);
        scratch.Supplies.Add(new SupplyRecord(mIx, ownerActorId,
                                              qty * market.Price[(int)good]));
    }

    /// <summary>Conglomerates and combines build where their niche points:
    /// the build basket is drawn from the home market and paid at founding
    /// prices from corporate credits (construction wages recycle, P4).</summary>
    private static void InvestFacilities(SimState state, Corporation corp,
                                         CorporationPolicies policies)
    {
        if (!WantsFacility(state, corp)) return;
        var port = state.Ports[corp.HomePortId];
        var market = state.Markets[port.Id];
        var type = PlannedFacility(state, corp);
        var build = Infrastructure.Get(type);
        double value = 0;
        foreach (var q in build.BuildCost)
        {
            if (market.Inventory[(int)q.Good] < q.Quantity) return;
            value += q.Quantity * Market.InitialPrice(state.Config.Economy, q.Good);
        }
        if (corp.Credits * policies.Investment.Facilities < value) return;
        foreach (var q in build.BuildCost)
        {
            double drawn = market.Draw((int)q.Good, q.Quantity);
            market.LastCleared[(int)q.Good] += drawn;
        }
        corp.Credits -= value;
        MarketEngine.PayWages(state, port.Id, value);
        state.Facilities.Add(new Facility(state.Facilities.Count, (int)type,
            tier: 1, port.Hex, corp.ActorId, state.WorldYear));
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FacilityBuilt, new[] { corp.ActorId }, port.Hex,
            Magnitude: 1.0, Valence: 0.5, EventVisibility.Regional,
            new FacilityBuiltPayload(state.Facilities.Count - 1, (int)type, 1)));
    }

    /// <summary>Freight lines buy hulls (components at market price, wages
    /// recycle) and post them on the best gradient lane at home — corporate
    /// capacity thickens the same freight web everyone trades on. Hulls are
    /// sourced from whichever host-polity market actually stocks components
    /// (internal logistics at cost: a frontier line commissions its ships
    /// at the yard port and sails them over) — the home market alone was
    /// the stillbirth trap.</summary>
    private static void InvestFleet(SimState state, Corporation corp,
                                    CorporationPolicies policies)
    {
        int hostId = state.Ports[corp.HomePortId].OwnerActorId;
        Market? market = null;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != hostId) continue;
            var m = state.Markets[port.Id];
            if (market == null
                || m.Inventory[(int)GoodId.ShipComponents]
                   > market.Inventory[(int)GoodId.ShipComponents])
                market = m;
        }
        if (market == null) return;
        var design = DesignRegistry.Current(state, corp.ActorId,
                ShipRole.Freight, ShipSize.Medium)
            ?? DesignRegistry.Register(state, corp.ActorId,
                ShipRole.Freight, ShipSize.Medium, grade: 0.5);
        double perHull = DesignMath.ComponentsPerHull(state.Config.Fleet,
                                                      ShipSize.Medium);
        double price = market.Price[(int)GoodId.ShipComponents];
        double budget = Math.Max(0, corp.Credits) * policies.Investment.Fleet;
        int affordable = (int)Math.Min(
            budget / Math.Max(1e-9, perHull * price),
            market.Inventory[(int)GoodId.ShipComponents] / Math.Max(1e-9, perHull));
        if (affordable <= 0) return;

        // the best own-home lane gradient carries the new hulls
        int bestLane = -1;
        double bestGradient = 0;
        foreach (var lane in state.Lanes)
        {
            if (lane.PortAId != corp.HomePortId
                && lane.PortBId != corp.HomePortId) continue;
            var a = state.Markets[lane.PortAId];
            var b = state.Markets[lane.PortBId];
            for (int g = 0; g < a.Price.Length; g++)
            {
                double gradient = Math.Abs(a.Price[g] - b.Price[g]);
                if (gradient > bestGradient)
                { bestGradient = gradient; bestLane = lane.Id; }
            }
        }
        if (bestLane < 0) return;

        double components = market.Draw((int)GoodId.ShipComponents,
                                        affordable * perHull);
        int hulls = (int)(components / perHull);
        if (hulls <= 0) return;
        market.LastCleared[(int)GoodId.ShipComponents] += components;
        double cost = components * price;
        corp.Credits -= cost;
        MarketEngine.PayWages(state, market.PortId, cost);   // the yard's crews

        FleetRecord? fleet = null;
        foreach (var f in state.Fleets)
            if (f.OwnerActorId == corp.ActorId && f.TargetId == bestLane)
            { fleet = f; break; }
        if (fleet == null)
        {
            fleet = new FleetRecord(state.Fleets.Count, corp.ActorId,
                state.Ports[corp.HomePortId].Hex)
            {
                Posture = FleetPosture.Posted,
                TargetId = bestLane,
                HomePortId = corp.HomePortId,
            };
            state.Fleets.Add(fleet);
        }
        fleet.AddHulls(design.Id, hulls, market.InventoryGrade[(int)GoodId.ShipComponents]);
        corp.HullsBuilt += hulls;
    }

    /// <summary>Cartels sell what the law won't: the black-book margin at
    /// the home market is skimmed from buyer wealth — a conserved transfer,
    /// capped by what the buyers actually hold (the goods flow rides the
    /// black-book model; the cartel is who profits from it).</summary>
    private static void SkimBlackBooks(SimState state, Corporation corp)
    {
        var knobs = state.Config.Corporate;
        var market = state.Markets[state.Ports[corp.HomePortId].Id];
        double value = 0;
        for (int g = 0; g < market.BlackBookDemand.Length; g++)
            value += market.BlackBookDemand[g] * market.BlackBookPrice[g];
        if (value <= 0) return;
        double wealth = 0;
        foreach (var s in state.Segments)
            if (s.PortId == corp.HomePortId) wealth += Math.Max(0, s.Wealth);
        double take = Math.Min(value * knobs.CartelSkim, wealth * 0.05);
        if (take <= 0) return;
        foreach (var s in state.Segments)
            if (s.PortId == corp.HomePortId && s.Wealth > 0)
                s.Wealth -= take * s.Wealth / wealth;
        corp.Credits += take;
        corp.Receipts += take;
    }

    /// <summary>Corporate fleets buy their upkeep at the home market —
    /// unaffordable supply decays readiness, and starved hulls wreck like
    /// anyone's (the loss conserves into wreckage, P4).</summary>
    private static void SupplyFleet(SimState state, Corporation corp)
    {
        var knobs = state.Config.Fleet;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != corp.ActorId || fleet.TotalHulls == 0)
                continue;
            var market = state.Markets[state.Ports[corp.HomePortId].Id];
            double met = 1.0;
            foreach (var g in fleet.Hulls)
            {
                var design = state.Designs[g.DesignId];
                var sheet = DesignRegistry.SheetOf(state, design);
                double need = sheet[ShipStat.Upkeep] * g.Count
                              * knobs.UpkeepUnitsPerPointPerYear * years;
                double fuelNeed = need * knobs.UpkeepFuelShare;
                double partsNeed = need - fuelNeed;
                met = Math.Min(met, BuyDraw(state, corp, market,
                    (int)GoodId.Fuel, fuelNeed));
                met = Math.Min(met, BuyDraw(state, corp, market,
                    (int)GoodId.ShipComponents, partsNeed));
            }
            double target = Math.Max(0.0, met);
            fleet.Readiness = target > fleet.Readiness
                ? Math.Min(target, fleet.Readiness
                    + knobs.ReadinessRecoveryPerYear * years)
                : Math.Max(target, fleet.Readiness
                    - knobs.ReadinessDecayPerYear * years);
            if (fleet.Readiness < knobs.AttritionReadinessFloor)
            {
                int losses = (int)Math.Ceiling(fleet.TotalHulls
                    * knobs.AttritionHullLossPerYear * years);
                FleetOps.Wreck(state, fleet, losses);
            }
        }
    }

    private static double BuyDraw(SimState state, Corporation corp,
                                  Market market, int good, double need)
    {
        if (need <= 0) return 1.0;
        double price = Math.Max(1e-9, market.Price[good]);
        double affordable = Math.Max(0.0, corp.Credits) / price;
        double drawn = market.Draw(good, Math.Min(need, affordable));
        if (drawn > 0)
        {
            market.LastCleared[good] += drawn;
            double cost = drawn * price;
            corp.Credits -= cost;
            MarketEngine.PayWages(state, market.PortId, cost);
        }
        return drawn / need;
    }

    // ---- deaths and seizure ----

    /// <summary>Dissolution with residue (corporations.md §Death): assets
    /// abandon to whoever hosts them, hulls scrap, remaining credits settle
    /// on the home port's populations — a complete chronicle arc.</summary>
    private static void Dissolve(SimState state, Corporation corp,
                                 WorldEventType cause)
    {
        corp.Active = false;
        foreach (var f in state.Facilities)
            if (f.OwnerActorId == corp.ActorId)
            {
                // abandoned to the port's sovereign, worse for wear
                f.OwnerActorId = state.Ports[corp.HomePortId].OwnerActorId;
                f.Condition *= 0.5;
            }
        foreach (var fleet in state.Fleets)
        {
            if (fleet.OwnerActorId != corp.ActorId) continue;
            int hulls = fleet.TotalHulls;
            while (fleet.Hulls.Count > 0)
                fleet.RemoveHulls(fleet.Hulls[0].DesignId,
                                  fleet.Hulls[0].Count);
            corp.HullsScrapped += hulls;
        }
        // the books settle whole: surplus to the home port's populations,
        // debt onto the sovereign administering the collapse — a negative
        // balance is never wiped (that would mint the money the creditors
        // already received, P4)
        double credits = corp.Credits;
        corp.Credits = 0;
        if (credits < 0)
            state.PolityOf(state.Ports[corp.HomePortId].OwnerActorId)
                .Credits += credits;
        else if (credits > 0)
        {
            double total = 0;
            foreach (var s in state.Segments)
                if (s.PortId == corp.HomePortId) total += s.Size;
            if (total > 0)
            {
                foreach (var s in state.Segments)
                    if (s.PortId == corp.HomePortId)
                        s.Wealth += credits * s.Size / total;
            }
            else
                state.PolityOf(state.Ports[corp.HomePortId].OwnerActorId)
                    .Credits += credits;
        }
        if (corp.ExecutiveCharacterId >= 0)
        {
            var executive = state.Characters[corp.ExecutiveCharacterId];
            if (executive.Alive)
            {
                executive.Role = CharacterRole.Notable;
                executive.InstitutionId = -1;
            }
        }
        state.Staged.Add(new StagedEvent(ClockStratum.Generational, cause,
            new[] { corp.ActorId }, state.Ports[corp.HomePortId].Hex,
            Magnitude: 1.0, Valence: -0.5, EventVisibility.Public,
            cause == WorldEventType.NicheDied
                ? new NicheDiedPayload(corp.Id, corp.Name, (int)corp.Niche)
                : new CorporationBankruptPayload(corp.Id, corp.Name)));
    }

    /// <summary>The polity's counter-move to a de facto power (an Intent
    /// act): seize the assets, take the wealth, eat the scandal — the
    /// reputation fallout lands as a legitimacy hit until slice I's news.</summary>
    public static bool Nationalize(SimState state, int polityId, int corpId)
    {
        if (corpId < 0 || corpId >= state.Corporations.Count) return false;
        var corp = state.Corporations[corpId];
        if (!corp.Active || corp.HostPolityId != polityId) return false;
        var pr = state.PolityOf(polityId);
        foreach (var f in state.Facilities)
            if (f.OwnerActorId == corp.ActorId) f.OwnerActorId = polityId;
        int hullsMoved = 0;
        foreach (var fleet in state.Fleets)
            if (fleet.OwnerActorId == corp.ActorId)
            {
                fleet.OwnerActorId = polityId;
                hullsMoved += fleet.TotalHulls;
            }
        corp.HullsBuilt -= hullsMoved;
        pr.HullsBuilt += hullsMoved;
        pr.Credits += corp.Credits;   // assets AND liabilities seize (P4)
        corp.Credits = 0;
        corp.Active = false;
        if (pr.Interior != null)
            pr.Interior.Legitimacy = Math.Max(0.0, pr.Interior.Legitimacy
                - state.Config.Corporate.NationalizeLegitimacyHit);
        if (corp.ExecutiveCharacterId >= 0)
        {
            var executive = state.Characters[corp.ExecutiveCharacterId];
            if (executive.Alive)
            {
                executive.Role = CharacterRole.Notable;
                executive.InstitutionId = -1;
            }
        }
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.CorporationNationalized,
            new[] { polityId, corp.ActorId },
            state.Ports[corp.HomePortId].Hex, Magnitude: 1.0, Valence: -0.7,
            EventVisibility.Public,
            new CorporationNationalizedPayload(corp.Id, corp.Name, polityId)));
        return true;
    }
}
