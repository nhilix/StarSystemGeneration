using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 3 — the top rungs (interpolity/relations.md
/// §Federation, §Vassalage): fusion births a NEW polity and retires the
/// parents whole (P4), vassalage binds tribute + the foreign-policy lock,
/// and the two exits fire on their clocks.</summary>
public class FederationTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int epochs)
    {
        state.Config.Sim.EpochCount = state.EpochIndex + epochs;
        new EpochEngine().Run(state);
    }

    // Numeraire-weighted money total: with real FX rates live, summing NATIVE
    // amounts across currencies is no longer conserved (a recorded conversion
    // changes the native sum), but the numeraire VALUE is — ConvertCurrency scales
    // by rateA/rateB, so amount·rateA is invariant. This is the cross-currency
    // conservation check for a single operation (federation/absorption converts
    // every mover into the survivor's currency, preserving numeraire value).
    private static double TotalCredits(SimState state)
    {
        double sum = 0;
        foreach (var p in state.Polities)
            sum += p.Credits * state.NumeraireRateOf(p.CurrencyId);
        foreach (var c in state.Corporations)
            sum += c.Credits;   // already numeraire (wallet total)
        foreach (var s in state.Segments)
            sum += s.Wealth * state.NumeraireRateOf(state.LocalCurrencySafe(s.PortId));
        foreach (var f in state.Factions)
            sum += f.Wealth * state.NumeraireRateOf(PolityCurrency(state, f.PolityId));
        // the conversion spread federation/absorption/vassalage settlements
        // pay is sequestered OUT of circulation into Bank.Reserve
        // (MetricsOps.cs authoritative residual balances Supply + Reserve) —
        // omitting it here reads as a false leak exactly equal to the skim,
        // in numeraire terms
        foreach (var bank in state.Banks)
            sum += bank.Reserve * state.NumeraireRateOf(bank.CurrencyId);
        return sum;
    }

    private static int PolityCurrency(SimState state, int actorId)
    {
        foreach (var p in state.Polities)
            if (p.ActorId == actorId) return p.CurrencyId;
        return -1;
    }

    private static (int Built, int Wrecked, int Scrapped) HullLedger(SimState state)
    {
        int built = 0, wrecked = 0, scrapped = 0;
        foreach (var p in state.Polities)
        {
            built += p.HullsBuilt;
            wrecked += p.HullsWrecked;
            scrapped += p.HullsScrapped;
        }
        foreach (var c in state.Corporations)
        {
            built += c.HullsBuilt;
            wrecked += c.HullsWrecked;
            scrapped += c.HullsScrapped;
        }
        return (built, wrecked, scrapped);
    }

    [Fact]
    public void Federate_FusesANewPolity_AndConserves()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        double creditsBefore = TotalCredits(state);
        var ledgerBefore = HullLedger(state);
        int portsA = 0, portsB = 0;
        foreach (var p in state.Ports)
        {
            if (p.OwnerActorId == rel.PolityAId) portsA++;
            if (p.OwnerActorId == rel.PolityBId) portsB++;
        }
        Assert.True(portsA > 0 && portsB > 0);

        int newId = FederationOps.Federate(state, rel);

        // the fused polity is NEW and both parents left the stage
        Assert.True(newId >= 0);
        Assert.True(state.Actors[newId].Entered);
        Assert.False(state.Actors[rel.PolityAId].Entered);
        Assert.True(state.Actors[rel.PolityAId].Retired);
        Assert.True(state.Actors[rel.PolityBId].Retired);
        Assert.Null(state.PolityOf(rel.PolityAId).Interior);
        // every port moved; nobody's segments went anywhere
        int newPorts = 0;
        foreach (var p in state.Ports)
        {
            Assert.NotEqual(rel.PolityAId, p.OwnerActorId);
            Assert.NotEqual(rel.PolityBId, p.OwnerActorId);
            if (p.OwnerActorId == newId) newPorts++;
        }
        Assert.Equal(portsA + portsB, newPorts);
        // books and hulls conserve through the fusion (P4)
        Assert.Equal(creditsBefore, TotalCredits(state), 6);
        Assert.Equal(ledgerBefore, HullLedger(state));
        // the interior seats fresh with founding legitimacy high
        var young = state.PolityOf(newId);
        Assert.NotNull(young.Interior);
        Assert.True(young.Interior!.Legitimacy >= 0.7,
            "treaty-built federations start legitimate");
        // fresh designs registered so the yards keep building
        Assert.NotNull(DesignRegistry.Current(state, newId,
            ShipRole.Freight, ShipSize.Medium));
    }

    [Fact]
    public void FederationGate_RequiresSustainedWarmAlliance()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        // no alliance: closed
        rel.Rung = TreatyRung.None;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        // fresh alliance: still closed
        rel.Rung = TreatyRung.DefenseAlliance;
        rel.RungYear = state.WorldYear;
        rel.Warmth = 0.95;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        // sustained + warm + aligned + open + cohesive: open
        rel.RungYear = state.WorldYear
            - state.Config.Relations.FederationAllianceEpochs
              * state.Config.Sim.GenerationYears;
        var a = state.PolityOf(rel.PolityAId);
        var b = state.PolityOf(rel.PolityBId);
        for (int ax = 0; ax < 4; ax++)
            b.Interior!.OfficialIdeology[ax] = a.Interior!.OfficialIdeology[ax];
        a.Interior!.Cohesion = 0.8;
        b.Interior!.Cohesion = 0.8;
        // openness is composed — force it through the official line
        a.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;
        b.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;
        bool holds = FederationOps.FederationGateHolds(state, rel);
        // openness also carries species/ruler terms — accept either, but a
        // cold pair must never pass
        rel.Warmth = 0.1;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
        rel.Warmth = 0.95;
        Assert.True(holds || Temperament.Compose(state, a).Openness
                        < state.Config.Relations.FederationOpennessFloor
                    || Temperament.Compose(state, b).Openness
                        < state.Config.Relations.FederationOpennessFloor);
    }

    /// <summary>Slice CU-4 T4 — the fusion true gate carries a monetary-
    /// credibility discount, mirroring the overlap discount, aggregated
    /// with <c>min</c> (design §3a): a credible ally pair fuses at a lower
    /// warmth bar; a debtor partner drags the term to ~0, so it earns none.</summary>
    [Fact]
    public void FederationGate_CredibilityDiscount_LowersBarForCrediblePairOnly()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        var a = state.PolityOf(rel.PolityAId);
        var b = state.PolityOf(rel.PolityBId);
        rel.Rung = TreatyRung.DefenseAlliance;
        rel.RungYear = state.WorldYear
            - state.Config.Relations.FederationAllianceEpochs
              * state.Config.Sim.GenerationYears;
        for (int ax = 0; ax < 4; ax++)
            b.Interior!.OfficialIdeology[ax] = a.Interior!.OfficialIdeology[ax];
        a.Interior!.Cohesion = 0.8;
        b.Interior!.Cohesion = 0.8;
        a.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;
        b.Interior.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.05;

        // the plain (undiscounted-by-credibility) gate this exact pair sees —
        // computed the same way FederationGateHolds does, so the test stays
        // correct regardless of this pair's actual border overlap
        double plainGate = RelationsOps.TreatyGate(state.Config, TreatyRung.Federation)
            - state.Config.Relations.FederationOverlapDiscount
              * RelationsOps.OverlapShare(state, rel.PolityAId, rel.PolityBId);
        rel.Warmth = plainGate - 0.05;   // just below the plain gate

        // knob pinned to 0: behavior identical to pre-CU-4 — fails regardless
        // of credibility, since the term is exactly 0 (pin explicitly; the
        // shipped default is now live at 0.20)
        state.BankOf(a.CurrencyId).Reserve = 100.0;
        state.BankOf(a.CurrencyId).ClaimOnState = 0.0;
        state.BankOf(b.CurrencyId).Reserve = 100.0;
        state.BankOf(b.CurrencyId).ClaimOnState = 0.0;
        state.Config.Relations.FederationCredibilityDiscount = 0.0;
        Assert.False(FederationOps.FederationGateHolds(state, rel));

        // both partners credible (BackedShare 1.0 each) + knob live: the
        // min-aggregated discount opens the gate at this same warmth
        state.Config.Relations.FederationCredibilityDiscount = 0.25;
        Assert.True(FederationOps.FederationGateHolds(state, rel));

        // a debtor partner (Reserve 0, ClaimOnState > 0 → BackedShare 0) drags
        // the pair's min to 0: no discount, the gate stays shut (min rule)
        state.BankOf(b.CurrencyId).Reserve = 0.0;
        state.BankOf(b.CurrencyId).ClaimOnState = 500.0;
        Assert.False(FederationOps.FederationGateHolds(state, rel));
    }

    [Fact]
    public void Vassalage_Binds_TributeFlows_TableCloses()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        Assert.Equal(vassal, rel.VassalPolityId);
        Assert.Equal(overlord, FederationOps.OverlordOf(state, vassal));
        Assert.True(FederationOps.HasVassals(state, overlord));

        // tribute: a conserved vassal→overlord receipts share. Histories
        // can carry their own vassal pairs — silence them (zero receipts)
        // so only the pair this test bound moves money
        var vr = state.PolityOf(vassal);
        var or = state.PolityOf(overlord);
        foreach (var p in state.Polities)
            if (p.ActorId != vassal) p.Receipts = 0;
        vr.Receipts = 100;
        double vc = vr.Credits, oc = or.Credits;
        int paid = FederationOps.PayTribute(state);
        Assert.True(paid >= 1, "the bound vassal must pay");
        double share = state.Config.Relations.VassalTributeShare;
        // the vassal pays the tribute out of its OWN currency (no conversion on
        // the debit); the overlord banks it converted into its own currency when
        // the two currencies differ (currency-and-FX design, "Conversion
        // mechanics" — a bilateral transfer routed through Deposit).
        double tribute = 100 * share;
        double landed = state.ConvertCurrency(tribute, vr.CurrencyId, or.CurrencyId);
        // Deposit (slice CU-2) skims the conversion spread off the top into
        // the destination currency's Bank.Reserve before crediting the net —
        // ConvertCurrency alone is the pre-skim gross, so a real cross-
        // currency landing is net of the spread (SimState.SettleConversion).
        if (vr.CurrencyId != or.CurrencyId)
            landed *= 1 - state.Config.Economy.ConversionSpread;
        Assert.Equal(vc - tribute, vr.Credits, 9);
        Assert.Equal(oc + landed, or.Credits, 9);
        Assert.Equal(100 * (1 - share), vr.Receipts, 9);

        // the foreign-policy lock: the vassal's treaties resolve to nothing
        var offer = new TreatyAct(vassal, overlord, 1, TreatyVerb.Offer);
        Assert.Equal(RelationsOps.TreatyOutcome.NoEffect,
                     RelationsOps.ResolveTreaty(state, offer));
    }

    [Fact]
    public void ChosenVassalage_NeedsRealWeakness()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        // a peer (similar strength) is refused
        var act = new VassalageAct(rel.PolityAId, rel.PolityBId, IsDemand: false);
        double a = FleetOps.WarStrength(state, rel.PolityAId);
        double b = FleetOps.WarStrength(state, rel.PolityBId);
        double ratio = b <= 0 ? 1.0 : a / b;
        rel.Warmth = 0.6;
        bool bound = FederationOps.TryBindVassal(state, act);
        Assert.Equal(ratio <= state.Config.Relations.VassalStrengthRatio, bound);
    }

    [Fact]
    public void Absorption_CompletesLongWarmVassalage()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        rel.VassalSinceYear = state.WorldYear
            - state.Config.Relations.VassalAbsorptionEpochs
              * state.Config.Sim.GenerationYears;
        rel.Warmth = 0.9;
        state.PolityOf(overlord).Interior!.Cohesion = 0.7;
        double creditsBefore = TotalCredits(state);
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(1, absorbed);
        Assert.Equal(0, seceded);
        Assert.True(state.Actors[vassal].Retired);
        Assert.Equal(-1, rel.VassalPolityId);
        foreach (var p in state.Ports)
            Assert.NotEqual(vassal, p.OwnerActorId);
        Assert.Equal(creditsBefore, TotalCredits(state), 6);
    }

    /// <summary>Slice CU-4 T6 — the absorption gate carries the credibility
    /// GAP (overlord − vassal, floored at 0): a monetarily weak vassal under
    /// a credible overlord completes annexation at a warmth just below the
    /// plain bar, once the discount knob is live.</summary>
    [Fact]
    public void Absorption_CredibilityGapDiscount_LowersBarForWeakVassalUnderCredibleOverlord()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        rel.VassalSinceYear = state.WorldYear
            - state.Config.Relations.VassalAbsorptionEpochs
              * state.Config.Sim.GenerationYears;
        state.PolityOf(overlord).Interior!.Cohesion = 0.7;
        // just below the plain warmth bar — fails without the discount
        rel.Warmth = state.Config.Relations.VassalAbsorptionWarmth - 0.05;

        var overlordPr = state.PolityOf(overlord);
        var vassalPr = state.PolityOf(vassal);
        // overlord: pure saver (BackedShare 1.0); vassal: pure debtor (0.0) —
        // the credibility gap is maximal (1.0)
        state.BankOf(overlordPr.CurrencyId).Reserve = 100.0;
        state.BankOf(overlordPr.CurrencyId).ClaimOnState = 0.0;
        state.BankOf(vassalPr.CurrencyId).Reserve = 0.0;
        state.BankOf(vassalPr.CurrencyId).ClaimOnState = 500.0;

        // knob pinned to 0: behavior identical to pre-CU-4 — the plain bar
        // isn't met, so no absorption fires (pin explicitly; the shipped
        // default is now live at 0.20)
        state.Config.Relations.VassalAbsorptionCredibilityDiscount = 0.0;
        var (absorbedAtZero, _) = FederationOps.VassalExits(state);
        Assert.Equal(0, absorbedAtZero);
        Assert.Equal(vassal, rel.VassalPolityId);   // still bound

        // knob live: the discount opens the gate at this same warmth
        state.Config.Relations.VassalAbsorptionCredibilityDiscount = 0.5;
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(1, absorbed);
        Assert.Equal(0, seceded);
        Assert.True(state.Actors[vassal].Retired);
        Assert.Equal(-1, rel.VassalPolityId);
    }

    /// <summary>A vassal MORE credible than its overlord earns no discount
    /// (design §4's <c>max(0, …)</c> floor) — never a penalty that could
    /// block an otherwise-qualifying absorption.</summary>
    [Fact]
    public void Absorption_VassalMoreCredibleThanOverlord_GetsNoDiscount()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        rel.VassalSinceYear = state.WorldYear
            - state.Config.Relations.VassalAbsorptionEpochs
              * state.Config.Sim.GenerationYears;
        state.PolityOf(overlord).Interior!.Cohesion = 0.7;
        rel.Warmth = state.Config.Relations.VassalAbsorptionWarmth - 0.05;

        var overlordPr = state.PolityOf(overlord);
        var vassalPr = state.PolityOf(vassal);
        // reversed: overlord is the debtor (0.0), vassal is the saver (1.0) —
        // the gap (overlord − vassal) is negative, floored to 0 by max(0, …)
        state.BankOf(overlordPr.CurrencyId).Reserve = 0.0;
        state.BankOf(overlordPr.CurrencyId).ClaimOnState = 500.0;
        state.BankOf(vassalPr.CurrencyId).Reserve = 100.0;
        state.BankOf(vassalPr.CurrencyId).ClaimOnState = 0.0;

        state.Config.Relations.VassalAbsorptionCredibilityDiscount = 0.5;
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(0, absorbed);
        Assert.Equal(0, seceded);
        Assert.Equal(vassal, rel.VassalPolityId);   // still bound — no discount
    }

    [Fact]
    public void Secession_FiresOnOverlordWeakness_AndLeavesAGrudge()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int vassal = rel.PolityAId, overlord = rel.PolityBId;
        FederationOps.Bind(state, rel, vassal);
        state.PolityOf(overlord).Interior!.Cohesion = 0.2;
        var (absorbed, seceded) = FederationOps.VassalExits(state);
        Assert.Equal(0, absorbed);
        Assert.Equal(1, seceded);
        Assert.Equal(-1, rel.VassalPolityId);
        Assert.True(state.Actors[vassal].Entered);   // free again
        bool grudge = false;
        foreach (var c in rel.Claims)
            if (!c.Released && c.Type == ClaimType.LostTerritory
                && c.HolderPolityId == overlord) grudge = true;
        Assert.True(grudge, "the lost bond persists as a standing claim");
    }

    [Fact]
    public void RetiredActors_AndBondClocks_RoundTrip()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.DefenseAlliance;
        rel.RungYear = 175;
        FederationOps.Bind(state, state.Relations.Count > 1
            ? state.Relations[1] : rel, rel.PolityAId);
        state.Actors[rel.PolityBId].Retired = true;
        state.Actors[rel.PolityBId].Entered = false;

        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));

        Assert.True(loaded.Actors[rel.PolityBId].Retired);
        Assert.False(loaded.Actors[rel.PolityBId].Entered);
        for (int i = 0; i < state.Relations.Count; i++)
        {
            Assert.Equal(state.Relations[i].RungYear,
                         loaded.Relations[i].RungYear);
            Assert.Equal(state.Relations[i].VassalPolityId,
                         loaded.Relations[i].VassalPolityId);
            Assert.Equal(state.Relations[i].VassalSinceYear,
                         loaded.Relations[i].VassalSinceYear);
        }
        Assert.Equal(ArtifactSerializer.ToText(state),
                     ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void RetiredPolity_NeverReenters_AndSimContinues()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        FederationOps.Federate(state, rel);
        int actorCount = state.Actors.Count;
        Continue(state, 3);
        Assert.False(state.Actors[rel.PolityAId].Entered);
        Assert.False(state.Actors[rel.PolityBId].Entered);
        // no phantom homeworld re-founding for the retired
        foreach (var p in state.Ports)
        {
            Assert.NotEqual(rel.PolityAId, p.OwnerActorId);
            Assert.NotEqual(rel.PolityBId, p.OwnerActorId);
        }
        Assert.True(state.Actors.Count >= actorCount);
    }
}
