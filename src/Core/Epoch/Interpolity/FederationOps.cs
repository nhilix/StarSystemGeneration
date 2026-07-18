using System;
using System.Collections.Generic;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>The treaty ladder's top rungs (interpolity/relations.md
/// §Federation, §Vassalage): federation fuses a NEW polity from two
/// consenting allies; vassalage is the asymmetric bond — tribute, defensive
/// obligation, foreign-policy lock — chosen under threat or imposed by
/// settlement (H7), exited by absorption or secession. The merge plumbing
/// is shared: everything an actor owns moves whole (P4).</summary>
public static class FederationOps
{
    /// <summary>Warmth an overlord must at least hold toward a supplicant to
    /// take it in — protection has a market, not a charity (structural).</summary>
    private const double VassalConsentWarmth = 0.25;
    /// <summary>Founding legitimacy of a treaty-built federation — its
    /// segments chose membership (structural; conquest empires start lower).</summary>
    private const double FederationFoundingLegitimacy = 0.75;

    // ---- federation ----

    /// <summary>The merge gate, verified on truth at Resolution: sustained
    /// alliance + high warmth + ideology compatibility + openness + both
    /// cohesions healthy + neither bound in a vassal bond.</summary>
    public static bool FederationGateHolds(SimState state, PolityRelation rel)
    {
        var knobs = state.Config.Relations;
        if (rel.Rung != TreatyRung.DefenseAlliance) return false;
        if (rel.RungYear < 0 || state.WorldYear - rel.RungYear
            < knobs.FederationAllianceEpochs
              * state.Config.Sim.GenerationYears) return false;
        // entangled friendly borders lower the bar: interleaved domains
        // are a reason to fuse, not just a thing to tolerate
        double gate = RelationsOps.TreatyGate(state.Config,
                TreatyRung.Federation)
            - knobs.FederationOverlapDiscount
              * RelationsOps.OverlapShare(state, rel.PolityAId, rel.PolityBId);
        if (rel.Warmth < gate) return false;
        var a = state.PolityOf(rel.PolityAId);
        var b = state.PolityOf(rel.PolityBId);
        if (RelationsOps.IdeologyGap(a, b) > knobs.FederationIdeologyGapMax)
            return false;
        // pair-mean openness: one open partner can carry a warier one over
        // the line (both still consented through the offer/accept dance)
        if (0.5 * (Temperament.Compose(state, a).Openness
                   + Temperament.Compose(state, b).Openness)
            < knobs.FederationOpennessFloor) return false;
        if (a.Interior == null || b.Interior == null) return false;
        if (a.Interior.Cohesion < knobs.FederationCohesionFloor
            || b.Interior.Cohesion < knobs.FederationCohesionFloor) return false;
        if (OverlordOf(state, rel.PolityAId) >= 0 || HasVassals(state, rel.PolityAId)
            || OverlordOf(state, rel.PolityBId) >= 0
            || HasVassals(state, rel.PolityBId)) return false;
        return true;
    }

    /// <summary>Fuse the pair into a NEW polity: multi-species membership,
    /// population-weighted composition, fresh name, government form from the
    /// combined ideology — it plays subsequent epochs as itself. Both
    /// parents retire.</summary>
    public static int Federate(SimState state, PolityRelation rel)
    {
        var parentA = state.PolityOf(rel.PolityAId);
        var parentB = state.PolityOf(rel.PolityBId);
        int newId = state.Actors.Count;
        string name = SyllableName(state, newId);

        // population decides species, culture, and the official line
        double popA = RealmPopulation(state, rel.PolityAId);
        double popB = RealmPopulation(state, rel.PolityBId);
        var bySpecies = new Dictionary<int, double>();
        var byCulture = new Dictionary<int, double>();
        Span<double> ideology = stackalloc double[4];
        double popSum = 0;
        foreach (var s in state.Segments)
        {
            if (s.Size <= 0) continue;
            int owner = state.Ports[s.PortId].OwnerActorId;
            if (owner != rel.PolityAId && owner != rel.PolityBId) continue;
            popSum += s.Size;
            bySpecies.TryGetValue(s.SpeciesId, out double sp);
            bySpecies[s.SpeciesId] = sp + s.Size;
            byCulture.TryGetValue(s.CultureId, out double cu);
            byCulture[s.CultureId] = cu + s.Size;
            for (int ax = 0; ax < 4; ax++) ideology[ax] += s.Ideology[ax] * s.Size;
        }
        int species = DominantKey(bySpecies, parentA.SpeciesId);
        int culture = DominantKey(byCulture,
            parentA.Interior?.FoundingCultureId ?? parentA.SpeciesId);
        if (popSum > 0)
            for (int ax = 0; ax < 4; ax++) ideology[ax] /= popSum;

        // the capital: the union's biggest harbor (port-id order breaks ties)
        int seatPort = -1;
        double seatPop = -1;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != rel.PolityAId
                && port.OwnerActorId != rel.PolityBId) continue;
            double pop = 0;
            foreach (var s in state.Segments)
                if (s.PortId == port.Id) pop += s.Size;
            if (pop > seatPop) { seatPop = pop; seatPort = port.Id; }
        }
        var seat = seatPort >= 0 ? state.Ports[seatPort].Hex
            : state.Actors[rel.PolityAId].Seat;

        var actor = new Actor(newId, ActorKind.Polity, name, seat,
                              state.WorldYear, new GenesisController(state.Config))
        { Entered = true };
        state.Actors.Add(actor);
        var young = new PolityRecord(newId, species)
        {
            EntryGradeBonus = Math.Max(parentA.EntryGradeBonus,
                                       parentB.EntryGradeBonus),
        };
        for (int d = 0; d < 4; d++)
        {
            // a union of sciences: each domain at the better member's ladder
            young.TechTier[d] = Math.Max(parentA.TechTier[d], parentB.TechTier[d]);
            young.TechProgress[d] = Math.Max(parentA.TechProgress[d],
                                             parentB.TechProgress[d]);
        }
        state.Polities.Add(young);
        // the union mints a brand-new currency before either parent merges in
        // (slice CU-1 genesis): the two MergeInto calls below force-convert
        // each parent's balance into THIS fresh currency (two conversions into
        // a new currency, not one absorption into a pre-existing one), and both
        // parent currencies retire when their actors do.
        state.FoundCurrency(newId);

        // parent politics dissolve while their treasuries and ports are
        // still theirs (war chests return to their own segments)
        DissolveFactionsOf(state, rel.PolityAId);
        DissolveFactionsOf(state, rel.PolityBId);
        MergeInto(state, rel.PolityAId, newId);
        MergeInto(state, rel.PolityBId, newId);
        DesignRegistry.RegisterEntryDesigns(state, newId,
            state.Skeleton.Species[species].Militancy);

        var interior = new PolityInterior
        {
            FoundingCultureId = culture,
            // treaty-built federations are structurally stabler than
            // conquest empires: their segments chose membership
            Legitimacy = FederationFoundingLegitimacy,
        };
        for (int ax = 0; ax < 4; ax++)
            interior.OfficialIdeology[ax] = popSum > 0 ? ideology[ax] : 0.5;
        interior.FormId = GovernmentForms.SeatFor(
            state.Skeleton.Species[species], interior.OfficialIdeology);
        young.Interior = interior;
        CharacterOps.SeatLeadership(state, young);

        Retire(state, rel.PolityAId);
        Retire(state, rel.PolityBId);

        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FederationFormed,
            new[] { rel.PolityAId, rel.PolityBId, newId }, seat,
            Magnitude: popA + popB, Valence: 0.9, EventVisibility.Public,
            new FederationFormedPayload(newId, name, rel.PolityAId,
                rel.PolityBId, state.Actors[rel.PolityAId].Name,
                state.Actors[rel.PolityBId].Name)));
        return newId;
    }

    // ---- vassalage ----

    /// <summary>The polity's living overlord, or −1 for the free.</summary>
    public static int OverlordOf(SimState state, int polityId)
    {
        foreach (var rel in state.Relations)                  // creation order (P6)
            if (rel.VassalPolityId == polityId
                && state.Actors[rel.OtherOf(polityId)].Entered)
                return rel.OtherOf(polityId);
        return -1;
    }

    public static bool HasVassals(SimState state, int polityId)
    {
        foreach (var rel in state.Relations)
            if (rel.VassalPolityId >= 0 && rel.VassalPolityId != polityId
                && rel.Involves(polityId)
                && state.Actors[rel.VassalPolityId].Entered) return true;
        return false;
    }

    /// <summary>Resolve a chosen vassalage (the supplicant's act): verified
    /// on truth — the supplicant must be genuinely weaker, the protector
    /// willing (not cold), and neither already bound. Imposed vassalage
    /// arrives through war settlements (H7), not this act.</summary>
    public static bool TryBindVassal(SimState state, VassalageAct act)
    {
        if (act.IsDemand) return false;   // demands are settlement business
        int vassal = act.ActorId, overlord = act.TargetPolityId;
        if (vassal == overlord) return false;
        if (overlord >= state.Actors.Count
            || state.Actors[overlord].Kind != ActorKind.Polity
            || state.Actors[vassal].Kind != ActorKind.Polity
            || !state.Actors[overlord].Entered
            || !state.Actors[vassal].Entered) return false;
        var rel = state.RelationOf(vassal, overlord);
        if (rel == null || rel.VassalPolityId >= 0) return false;
        if (OverlordOf(state, vassal) >= 0 || OverlordOf(state, overlord) >= 0
            || HasVassals(state, vassal)) return false;
        double ratio = FleetOps.WarStrength(state, overlord) <= 0 ? 1.0
            : FleetOps.WarStrength(state, vassal)
              / FleetOps.WarStrength(state, overlord);
        if (ratio > state.Config.Relations.VassalStrengthRatio) return false;
        if (rel.Warmth < VassalConsentWarmth) return false;
        Bind(state, rel, vassal);
        return true;
    }

    /// <summary>Bind the bond (shared with H7's imposed settlements):
    /// tribute, defensive obligation, foreign-policy lock — the plain
    /// rungs dissolve into it.</summary>
    public static void Bind(SimState state, PolityRelation rel, int vassalId)
    {
        rel.VassalPolityId = vassalId;
        rel.VassalSinceYear = state.WorldYear;
        rel.Rung = TreatyRung.None;
        rel.RungYear = -1;
        rel.OfferedRung = TreatyRung.None;
        rel.OfferedById = -1;
        rel.OfferYear = -1;
        int overlord = rel.OtherOf(vassalId);
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.VassalageBound, new[] { overlord, vassalId },
            state.Actors[vassalId].Seat, Magnitude: 1.0, Valence: -0.2,
            EventVisibility.Public,
            new VassalageBoundPayload(overlord, vassalId,
                state.Actors[overlord].Name, state.Actors[vassalId].Name)));
    }

    /// <summary>Tribute: the vassal ships an income share up before it
    /// budgets — a conserved vassal→overlord flow Allocation runs first.</summary>
    public static int PayTribute(SimState state)
    {
        double share = state.Config.Relations.VassalTributeShare;
        int paid = 0;
        foreach (var rel in state.Relations)                  // creation order (P6)
        {
            if (rel.VassalPolityId < 0) continue;
            var vassal = state.PolityOf(rel.VassalPolityId);
            var overlord = state.PolityOf(rel.OtherOf(rel.VassalPolityId));
            if (!state.Actors[vassal.ActorId].Entered
                || !state.Actors[overlord.ActorId].Entered) continue;
            double tribute = Math.Max(0.0, vassal.Receipts) * share;
            if (tribute <= 0) continue;
            // the vassal's own currency leaves, converting into the
            // overlord's own on arrival (currency-and-FX design) — a no-op
            // conversion when they share one, or pre-genesis (both -1)
            vassal.Withdraw(state, tribute, vassal.CurrencyId);
            vassal.Receipts -= tribute;   // the budget base shrinks with it
            double banked = overlord.Deposit(state, tribute, vassal.CurrencyId);
            overlord.Receipts += banked;
            paid++;
        }
        return paid;
    }

    /// <summary>The two exits, checked mechanically each Interior phase:
    /// absorption (long stable bond + real warmth + a healthy overlord →
    /// peaceful annexation) and secession (overlord weakness → the bond
    /// dissolves; the fought variant arrives with H5's casus belli).</summary>
    public static (int Absorbed, int Seceded) VassalExits(SimState state)
    {
        var knobs = state.Config.Relations;
        int absorbed = 0, seceded = 0;
        int relations = state.Relations.Count;   // absorption appends nothing,
                                                 // but hold the scan stable
        for (int i = 0; i < relations; i++)
        {
            var rel = state.Relations[i];
            if (rel.VassalPolityId < 0) continue;
            int vassalId = rel.VassalPolityId;
            int overlordId = rel.OtherOf(vassalId);
            if (!state.Actors[vassalId].Entered
                || !state.Actors[overlordId].Entered) continue;
            var overlord = state.PolityOf(overlordId);
            if (overlord.Interior == null) continue;

            if (overlord.Interior.Cohesion < knobs.VassalSecessionCohesion)
            {
                // the overlord's grip slips: independence, negotiated —
                // and the lost bond becomes a standing grudge
                rel.VassalPolityId = -1;
                rel.VassalSinceYear = -1;
                seceded++;
                var seatPort = SeatPortOf(state, vassalId);
                if (seatPort >= 0)   // a portless vassal leaves no grudge target
                    rel.Claims.Add(new RelationClaim(ClaimType.LostTerritory,
                        overlordId, seatPort, state.WorldYear));
                state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                    WorldEventType.VassalSeceded, new[] { vassalId, overlordId },
                    state.Actors[vassalId].Seat, Magnitude: 1.0, Valence: 0.4,
                    EventVisibility.Public,
                    new VassalSecededPayload(overlordId, vassalId,
                        state.Actors[overlordId].Name,
                        state.Actors[vassalId].Name)));
                continue;
            }

            if (rel.VassalSinceYear >= 0
                && state.WorldYear - rel.VassalSinceYear
                   >= knobs.VassalAbsorptionEpochs
                      * state.Config.Sim.GenerationYears
                && rel.Warmth >= knobs.VassalAbsorptionWarmth)
            {
                // cultural drift completes: peaceful annexation
                DissolveFactionsOf(state, vassalId);
                MergeInto(state, vassalId, overlordId);
                Retire(state, vassalId);
                rel.VassalPolityId = -1;
                rel.VassalSinceYear = -1;
                absorbed++;
                state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                    WorldEventType.VassalAbsorbed, new[] { overlordId, vassalId },
                    state.Actors[vassalId].Seat, Magnitude: 1.0, Valence: 0.1,
                    EventVisibility.Public,
                    new VassalAbsorbedPayload(overlordId, vassalId,
                        state.Actors[overlordId].Name,
                        state.Actors[vassalId].Name)));
            }
        }
        return (absorbed, seceded);
    }

    // ---- the merge plumbing (shared by federation, absorption, H7 conquest) ----

    /// <summary>Move everything one polity owns to another, whole (P4):
    /// ports, facilities, fleets with their commanders and hull ledgers,
    /// treasuries and reserves, hosted corporations, characters; open loans
    /// reissue against the successor (debts between the two cancel).
    /// Segments stay at their ports — people are geography.</summary>
    public static void MergeInto(SimState state, int fromId, int intoId)
    {
        var from = state.PolityOf(fromId);
        var into = state.PolityOf(intoId);

        foreach (var port in state.Ports)
            if (port.OwnerActorId == fromId)
            {
                // the port's resident households (and any resting order/courier
                // escrow at its market) now hold and earn the survivor's currency:
                // force-convert every port-resolved holder at the frozen rate and
                // record the transfers, so a conquered/absorbed population's wealth
                // isn't silently re-denominated 1:1 the instant the owner changes
                // (currency-and-FX design, "Data model"). Factions follow their
                // PolityId (dissolved by the caller into same-currency segments, or
                // left attributed to the absorbed polity's own currency), handled
                // separately from these port-owner-resolved holders.
                port.OwnerActorId = intoId;
                state.ConvertPortHoldings(port.Id, from.CurrencyId, into.CurrencyId);
            }
        foreach (var facility in state.Facilities)
            if (facility.OwnerActorId == fromId) facility.OwnerActorId = intoId;
        // in-flight work follows the merge like its facilities do (owner AND
        // funder pass to the successor); a Mobilization is the parent's war
        // ramp, which the successor was never party to — it cancels (F2/F1)
        foreach (var p in state.Projects)                     // id order (P6)
        {
            if (!p.InFlight
                || (p.OwnerActorId != fromId && p.FunderActorId != fromId))
                continue;
            if (p.Kind == ProjectKind.Mobilization)
            {
                ProjectOps.Cancel(state, p);
                continue;
            }
            // a ColonyExpedition's in-flight purse (ColonyCost) is resolved LIVE
            // by SupplyOps through p.FunderActorId's currency; reassigning the
            // funder re-denominates that purse from the absorbed polity's currency
            // to the survivor's, so record the transfer at nominal parity (the
            // forced-conversion absorption stub — the purse is a fixed nominal, not
            // a converted value) before the funder changes, or it leaks.
            if (p.Kind == ProjectKind.ColonyExpedition
                && p.FunderActorId == fromId
                && from.CurrencyId != into.CurrencyId)
            {
                double purse = state.Config.Expansion.ColonyCost;
                state.RecordConversion(from.CurrencyId, purse,
                                       into.CurrencyId, purse);
            }
            if (p.OwnerActorId == fromId) p.OwnerActorId = intoId;
            if (p.FunderActorId == fromId) p.FunderActorId = intoId;
        }
        int hulls = 0;
        foreach (var fleet in state.Fleets)
        {
            if (fleet.OwnerActorId != fromId) continue;
            fleet.OwnerActorId = intoId;
            hulls += fleet.TotalHulls;
            // inherited war stations stand down: the successor was never
            // a party to the parent's wars, and a stranded blockade would
            // sever lanes forever (review fix 1)
            if (fleet.Posture is FleetPosture.Blockade
                or FleetPosture.Expedition)
            {
                fleet.Posture = FleetPosture.Reserve;
                fleet.TargetId = -1;
            }
        }
        // the absorbed treasury force-converts into the survivor's currency
        // (slice CU-1, currency-and-FX design): a transfer between the two
        // currencies' supplies, never a raw carry-over. This is the polity's OWN
        // money moving across the merge seam, so it is EXEMPT from the conversion
        // spread ("clipping is wrong") — DepositExempt converts + records at plain
        // rate with NO skim, exactly like the pools right below it (:429-442). An
        // insolvent parent (negative Credits) hands its debt over converted;
        // DepositExempt has no non-positive guard, so the debt is never swallowed.
        into.DepositExempt(state, from.Credits, from.CurrencyId);
        from.Credits = 0;
        into.Receipts += from.Receipts;
        from.Receipts = 0;
        // the investment pools are the absorbed polity's money in its OWN currency
        // too (SupplyOps sums them into Currency.Supply), so they force-convert into
        // the survivor's currency and record the transfer exactly like the treasury
        // above — a raw 1:1 carry-over would silently re-denominate a whole
        // treasury's worth of pooled budget at a polity's death (currency-and-FX
        // design, "Conservation & determinism"). ConvertCurrency is linear, so the
        // per-pool converts sum to the aggregate recorded out/in.
        double poolsOut = from.ExpansionPoints + from.DevelopmentPoints
            + from.MilitaryPoints + from.ReservePoints;
        double eIn = state.ConvertCurrency(from.ExpansionPoints, from.CurrencyId, into.CurrencyId);
        double dIn = state.ConvertCurrency(from.DevelopmentPoints, from.CurrencyId, into.CurrencyId);
        double mIn = state.ConvertCurrency(from.MilitaryPoints, from.CurrencyId, into.CurrencyId);
        double rIn = state.ConvertCurrency(from.ReservePoints, from.CurrencyId, into.CurrencyId);
        into.ExpansionPoints += eIn;
        from.ExpansionPoints = 0;
        into.DevelopmentPoints += dIn;
        from.DevelopmentPoints = 0;
        into.MilitaryPoints += mIn;
        from.MilitaryPoints = 0;
        into.ReservePoints += rIn;
        from.ReservePoints = 0;
        state.RecordConversion(from.CurrencyId, poolsOut,
                               into.CurrencyId, eIn + dIn + mIn + rIn);
        into.HullsBuilt += from.HullsBuilt;
        into.HullsWrecked += from.HullsWrecked;
        into.HullsScrapped += from.HullsScrapped;
        from.HullsBuilt = 0;
        from.HullsWrecked = 0;
        from.HullsScrapped = 0;
        // located stockpiles need no merge (spec §4b): stock is banked at
        // ports, and the ports changed owner above — the goods stay put
        foreach (var corp in state.Corporations)
            if (corp.HostPolityId == fromId) corp.HostPolityId = intoId;
        foreach (var character in state.Characters)
            if (character.PolityId == fromId) character.PolityId = intoId;
        // the absorbed bank's balance sheet consolidates into the survivor's
        // (slice CU-3, currency-consolidation design §3): its RESERVE pools into
        // the survivor's reserve and its CLAIM book transfers onto the survivor's
        // enlarged self. Placed after the treasury/pool re-denomination above and
        // before the loan reissue below — the same seam, the same frozen rate.
        // Guards (§3a): pre-genesis has no banks (BankOf(-1) would throw), and a
        // self-transfer between two polities that already share a currency would
        // read-then-zero the ONE shared bank — skip the block in both cases.
        if (from.CurrencyId >= 0 && into.CurrencyId >= 0
            && from.CurrencyId != into.CurrencyId)
        {
            var fromBank = state.BankOf(from.CurrencyId);
            var intoBank = state.BankOf(into.CurrencyId);
            // Reserve is MONEY — sequestered out of Supply but counted in the
            // per-currency residual (against Supply + Reserve). So it converts AND
            // records the transfer, exactly like the treasury (DepositExempt) and
            // pools above: EXEMPT (plain ConvertCurrency, never the skimming
            // SettleConversion) — this is re-denomination of the polity's own
            // monetary backing at the merge, not a market FX trade (§3b).
            double reserveIn = state.ConvertCurrency(fromBank.Reserve,
                                                     from.CurrencyId, into.CurrencyId);
            state.RecordConversion(from.CurrencyId, fromBank.Reserve,
                                   into.CurrencyId, reserveIn);
            intoBank.Reserve += reserveIn;
            fromBank.Reserve = 0;
            // ClaimOnState is NOT money — it never enters Supply and never appears
            // on the residual's balance side (Bank.cs; the MetricsOps LoanPrincipal
            // precedent). So it converts to REPRICE into the survivor's currency
            // but is NOT recorded — recording it would inject a phantom leak into
            // BOTH currencies' residuals. This mirrors the loan-principal reissue
            // right below, which likewise ConvertCurrency's the principal without a
            // RecordConversion (§3c — the slice's central correctness point).
            double claimIn = state.ConvertCurrency(fromBank.ClaimOnState,
                                                   from.CurrencyId, into.CurrencyId);
            intoBank.ClaimOnState += claimIn;
            fromBank.ClaimOnState = 0;
            // §3d: only the live balances move. The cumulative counters
            // (CumulativeSpreadIntake/ReserveFunded/LentToState/Retired) stay on
            // the drained husk — CumulativeRetired mirrors the retired currency's
            // CumulativeFiatRetired (which stays), and the rest are observability
            // of the ABSORBED polity's own activity; attributing them to the
            // survivor would be a false readout. The husk lingers in state.Banks
            // keyed to its retired currency, parallel to the retired Currency
            // record (§3e). No change to Retire.
        }
        int loans = state.Loans.Count;   // reissues append — scan the originals
        for (int i = 0; i < loans; i++)
        {
            var loan = state.Loans[i];
            if (loan.Closed) continue;
            bool lender = loan.LenderActorId == fromId;
            bool borrower = loan.BorrowerActorId == fromId;
            if (!lender && !borrower) continue;
            loan.Closed = true;
            int newLender = lender ? intoId : loan.LenderActorId;
            int newBorrower = borrower ? intoId : loan.BorrowerActorId;
            if (newLender == newBorrower) continue;   // internal debt cancels
            // A loan denominates in the LENDER's currency for a polity lender,
            // but in the BORROWER's currency for a corporation lender (design,
            // "Loans across currencies"; Phases.Borrow/ServiceLoans). Reprice the
            // principal into the survivor's currency whenever the side that
            // DENOMINATES the loan is the one being absorbed: a changed
            // polity-lender, OR a changed borrower under a corp lender. Otherwise
            // the denominating currency is untouched and the principal carries.
            // The old code repriced only on a lender change, silently
            // re-denominating a corp-lent (borrower-denominated) loan 1:1 when its
            // borrower was absorbed — the exact absorption-time leak this slice
            // exists to close.
            bool corpLender = state.CorporationOf(loan.LenderActorId) != null;
            bool denomChanged = (lender && !corpLender) || (borrower && corpLender);
            double principal = denomChanged
                ? state.ConvertCurrency(loan.Principal, from.CurrencyId,
                                        into.CurrencyId)
                : loan.Principal;
            // preserve the capitalization ceiling's fixed reference across the
            // reissue: scale OriginalPrincipal by the same conversion factor so the
            // loan keeps its real "2x runway". A bare reissue defaulted
            // OriginalPrincipal to the current (possibly already-capitalized)
            // principal, handing every absorbed loan a fresh ceiling at each
            // absorption — a pre-existing ME follow-up the serializer already
            // guards against on load (markets v4).
            double newOriginal = loan.Principal != 0
                ? loan.OriginalPrincipal * (principal / loan.Principal)
                : loan.OriginalPrincipal;
            state.Loans.Add(new Loan(state.Loans.Count, newLender, newBorrower,
                principal, loan.RatePerYear, loan.TermYears,
                loan.IssuedYear, originalPrincipal: newOriginal));
        }
    }

    /// <summary>An actor leaves the stage: entered no more, never re-enters,
    /// court dispersed, interior gone, bonds dissolved (the record stays
    /// as history).</summary>
    public static void Retire(SimState state, int polityId)
    {
        var actor = state.Actors[polityId];
        actor.Entered = false;
        actor.Retired = true;
        // vassal bonds die with either party — an orphaned vassal must
        // never stay diplomatically paralyzed to a ghost (review fix 2)
        foreach (var rel in state.Relations)                  // creation order (P6)
            if (rel.Involves(polityId) && rel.VassalPolityId >= 0)
            {
                rel.VassalPolityId = -1;
                rel.VassalSinceYear = -1;
            }
        var pr = state.PolityOf(polityId);
        // the polity's currency retires with it (slice CU-1): no new money
        // mints into a dead polity's currency, though the record lives on as
        // history (and any dangling foreign-held balances resolve through it).
        // The single death chokepoint — reached from federation fusion (both
        // parents), vassal absorption, and war submission/annexation — so this
        // one line retires every absorbed currency, the whole exhaustive list.
        if (pr.CurrencyId >= 0)
            state.CurrencyOf(pr.CurrencyId).Retired = true;
        if (pr.Interior is { } interior && interior.RulerCharacterId >= 0)
        {
            var ruler = state.Characters[interior.RulerCharacterId];
            if (ruler.Alive)
            {
                ruler.Role = CharacterRole.Notable;
                ruler.InstitutionId = -1;
            }
        }
        pr.Interior = null;
    }

    /// <summary>Dissolve a polity's active factions (their chests return to
    /// its own segments) — every merger's first act.</summary>
    public static void DissolveFactionsOf(SimState state, int polityId)
    {
        var pr = state.PolityOf(polityId);
        foreach (var faction in state.Factions)               // id order (P6)
            if (faction.Active && faction.PolityId == polityId)
                FactionOps.Dissolve(state, pr, faction);
    }

    private static double RealmPopulation(SimState state, int polityId)
    {
        double pop = 0;
        foreach (var s in state.Segments)
            if (s.Size > 0 && state.Ports[s.PortId].OwnerActorId == polityId)
                pop += s.Size;
        return pop;
    }

    private static int DominantKey(Dictionary<int, double> weights, int fallback)
    {
        int best = fallback;
        double bestWeight = -1;
        var keys = new List<int>(weights.Keys);
        keys.Sort();                                          // P6: id order
        foreach (var key in keys)
            if (weights[key] > bestWeight) { bestWeight = weights[key]; best = key; }
        return best;
    }

    /// <summary>The polity's seat port id (its homeworld harbor), or its
    /// biggest port when the seat hex holds none.</summary>
    private static int SeatPortOf(SimState state, int polityId)
    {
        var seat = state.Actors[polityId].Seat;
        int fallback = -1;
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != polityId) continue;
            if (port.Hex.Equals(seat)) return port.Id;
            if (fallback < 0) fallback = port.Id;
        }
        return fallback;
    }

    private static string SyllableName(SimState state, int key)
    {
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed,
            RollChannel.FederationSeed, key, -1, 100) < 0.4 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.FederationSeed, key, -1, 10 + i));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word);
    }
}
