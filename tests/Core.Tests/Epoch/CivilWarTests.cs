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
    private static SimState Run(int epochs = 24)
    {
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

    private static double Credits(SimState state)
    {
        double sum = 0;
        foreach (var p in state.Polities) sum += p.Credits;
        foreach (var c in state.Corporations) sum += c.Credits;
        foreach (var s in state.Segments) sum += s.Wealth;
        foreach (var f in state.Factions) sum += f.Wealth;
        return sum;
    }
}
