using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 9 — civil wars (factions-and-government.md
/// §Graduation: "contested → civil war, the war machinery, against a
/// provisional polity"): loyalist domains rally to the deposed ruler
/// through the schism-splinter flow, the throne is fought for, and
/// submission settlements merge the loser back whole.</summary>
public class CivilWarTests
{
    private static SimState Run(int epochs = 32)
    {
        // 32 epochs (was 24): the contract economy's works BUY their goods,
        // so early build-out is slower and the 4-port polity these tests
        // stage against arrives a few generations later (slice CE)
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    /// <summary>A polity with several ports and a living ruler, plus that
    /// ruler pulled off the throne — the eruption's inputs.</summary>
    private static (PolityRecord Usurper, Character Deposed) Stage(
        SimState state)
    {
        PolityRecord? pick = null;
        foreach (var pr in state.Polities)
        {
            if (pr.Interior == null
                || !state.Actors[pr.ActorId].Entered) continue;
            int ports = 0;
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ports++;
            if (ports >= 4 && pr.Interior.RulerCharacterId >= 0
                && state.Characters[pr.Interior.RulerCharacterId].Alive)
            { pick = pr; break; }
        }
        Assert.NotNull(pick);
        var deposed = state.Characters[pick!.Interior!.RulerCharacterId];
        deposed.Role = CharacterRole.Notable;
        deposed.InstitutionId = -1;
        return (pick, deposed);
    }

    [Fact]
    public void ContestedCoup_ErutpsIntoACivilWar()
    {
        var state = Run();
        var (usurper, deposed) = Stage(state);
        double creditsBefore = Credits(state);

        var war = CivilWarOps.Erupt(state, usurper, deposed,
            new[] { 0.5, 0.5, 0.5, 0.5 }, GovernmentFormId.Autocracy, 0.6);

        Assert.NotNull(war);
        Assert.Equal(CasusBelli.CivilWar, war!.Cause);
        Assert.Equal(WarDemand.Submission, war.Demand);
        Assert.Contains("Civil War", war.Name);
        // the provisional polity exists, ruled by the deposed
        var provisional = state.PolityOf(war.AttackerId);
        Assert.NotNull(provisional.Interior);
        Assert.Equal(deposed.Id, provisional.Interior!.RulerCharacterId);
        Assert.Equal(CharacterRole.Ruler, deposed.Role);
        Assert.Contains(state.Ports,
            p => p.OwnerActorId == war.AttackerId);
        // brothers' wars drag in no allies
        Assert.Empty(war.AttackerAllies);
        Assert.Empty(war.DefenderAllies);
        // the split conserved the books
        Assert.Equal(creditsBefore, Credits(state), 6);
        // the palace is an objective
        Assert.Contains(war.Objectives,
            o => o.Type == WarObjectiveType.CapturePort);
    }

    [Fact]
    public void FailedRestoration_MergesTheLoyalistsBack()
    {
        var state = Run();
        var (usurper, deposed) = Stage(state);
        var war = CivilWarOps.Erupt(state, usurper, deposed,
            new[] { 0.5, 0.5, 0.5, 0.5 }, GovernmentFormId.Autocracy, 0.6)!;
        int provisional = war.AttackerId;
        war.AttackerExhaustion = 1.0;   // the restoration fails

        WarResolution.Terminate(state, null);

        Assert.False(war.Active);
        Assert.True(state.Actors[provisional].Retired);
        foreach (var p in state.Ports)
            Assert.NotEqual(provisional, p.OwnerActorId);
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.PeaceSettled
            && e.Payload is PeaceSettledPayload pay
            && pay.Outcome == (int)WarOutcome.Submission);
    }

    [Fact]
    public void SuccessfulRestoration_AbsorbsTheUsurperState()
    {
        var state = Run();
        var (usurper, deposed) = Stage(state);
        var war = CivilWarOps.Erupt(state, usurper, deposed,
            new[] { 0.5, 0.5, 0.5, 0.5 }, GovernmentFormId.Autocracy, 0.6)!;
        foreach (var o in war.Objectives) o.Status = ObjectiveStatus.Taken;

        WarResolution.Terminate(state, null);

        Assert.False(war.Active);
        Assert.True(state.Actors[usurper.ActorId].Retired);
        Assert.True(state.Actors[war.AttackerId].Entered);
    }

    [Fact]
    public void OnePortRealms_CannotSplit()
    {
        var state = Run();
        // a realm with a single port keeps its coup uncontested in effect
        PolityRecord? small = null;
        foreach (var pr in state.Polities)
        {
            if (pr.Interior == null
                || !state.Actors[pr.ActorId].Entered) continue;
            int ports = 0;
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ports++;
            if (ports == 1) { small = pr; break; }
        }
        if (small == null) return;   // this seed grew everyone
        var ruler = state.Characters.FirstOrDefault(c => c.Alive);
        Assert.NotNull(ruler);
        Assert.Null(CivilWarOps.Erupt(state, small, ruler!,
            new[] { 0.5, 0.5, 0.5, 0.5 }, GovernmentFormId.Autocracy, 0.6));
    }

    // Numeraire-weighted money total (currency-and-FX design): a contested coup
    // founds the loyalist provisional through the schism splinter flow, minting it
    // a brand-new currency and force-converting the seceding ports' treasury,
    // pools, and resident segment wealth into it — a recorded transfer that changes
    // the NATIVE sum across currencies but preserves the numeraire VALUE (a
    // conversion scales by rateA/rateB, so amount·rate is invariant).
    private static double Credits(SimState state)
    {
        double sum = 0;
        foreach (var p in state.Polities)
            sum += p.Credits * state.NumeraireRateOf(p.CurrencyId);
        foreach (var c in state.Corporations)
            sum += c.Credits;   // already numeraire (wallet total)
        foreach (var s in state.Segments)
            sum += s.Wealth * state.NumeraireRateOf(state.LocalCurrencySafe(s.PortId));
        foreach (var f in state.Factions)
        {
            int cur = -1;
            foreach (var p in state.Polities)
                if (p.ActorId == f.PolityId) { cur = p.CurrencyId; break; }
            sum += f.Wealth * state.NumeraireRateOf(cur);
        }
        // the conversion spread the schism's force-conversions pay is
        // sequestered OUT of circulation into Bank.Reserve (MetricsOps.cs
        // authoritative residual balances Supply + Reserve) — omitting it
        // here reads as a false leak exactly equal to the skim, in
        // numeraire terms
        foreach (var bank in state.Banks)
            sum += bank.Reserve * state.NumeraireRateOf(bank.CurrencyId);
        return sum;
    }
}
