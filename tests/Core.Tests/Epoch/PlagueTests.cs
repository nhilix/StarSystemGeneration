using System.IO;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 9 — plagues: outbreaks where people crowd, spread
/// riding the posted-traffic lanes, mortality that shrinks segments and
/// never touches a credit, machine immunity, burnout into an immunity
/// window, and the QuarantineAct finally resolved — closed lanes stop
/// freight, migration, and contagion together.</summary>
public class PlagueTests
{
    /// <summary>Two connected ports with people at both; the lane is
    /// saturated with posted freight (contagion at full odds).</summary>
    private static (SimState State, Port A, Port B) ConnectedFixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int sp = state.PolityOf(0).SpeciesId;
        state.Skeleton.Species[sp].Embodiment =
            StarGen.Core.Galaxy.Embodiment.TerranAnalog;
        state.Segments.Add(new PopulationSegment(0, 0, sp, sp, 3.0)
        { Wealth = 300 });
        state.Segments.Add(new PopulationSegment(1, 1, sp, sp, 3.0)
        { Wealth = 300 });
        EpochTestKit.PostFreight(state, a0.Id, laneId: 0, hulls: 8);
        state.WorldYear = 100;
        return (state, pa, pb);
    }

    private static Plague Infect(SimState state, int portId)
    {
        var plague = new Plague(state.Plagues.Count, "Test Fever", portId,
                                state.WorldYear);
        plague.InfectedSince.Add(portId, state.WorldYear);
        state.Plagues.Add(plague);
        return plague;
    }

    [Fact]
    public void CrowdedPorts_Outbreak_WhenTheGateRolls()
    {
        var (state, pa, _) = ConnectedFixture();
        state.Config.Plague.OutbreakChancePerYear = 10.0;   // force the gate
        var (outbreaks, _, _) = PlagueOps.Step(state);
        Assert.True(outbreaks >= 1);
        var plague = state.Plagues[0];
        Assert.True(plague.Active);
        Assert.True(plague.Name.Length > 0, "strains have names");
        Assert.Contains(state.Staged,
            e => e.Type == WorldEventType.PlagueOutbreak);
        Assert.True(plague.Infects(pa.Id) || plague.Infects(1));
    }

    [Fact]
    public void Contagion_RidesTheBusyLane()
    {
        var (state, pa, pb) = ConnectedFixture();
        var plague = Infect(state, pa.Id);
        // saturated traffic: SpreadChancePerYear × years ≥ 1 — certain
        PlagueOps.Step(state);
        Assert.True(plague.Infects(pb.Id),
            "a saturated lane carries the plague within the epoch");
    }

    [Fact]
    public void Quarantine_StopsContagion_AsSurelyAsFreight()
    {
        var (state, pa, pb) = ConnectedFixture();
        var plague = Infect(state, pa.Id);
        Assert.True(PlagueOps.Quarantine(state,
            new QuarantineAct(state.Actors[0].Id, 0)));
        Assert.Contains(0, FleetOps.SeveredLaneIds(state));
        Assert.Contains(state.Staged,
            e => e.Type == WorldEventType.QuarantineImposed);
        PlagueOps.Step(state);
        Assert.False(plague.Infects(pb.Id), "the closed lane carries nothing");
        // a foreign sovereign may not close someone else's lane
        state.Actors[1].Entered = true;
        Assert.False(PlagueOps.Quarantine(state, new QuarantineAct(1, 0)));
    }

    [Fact]
    public void Mortality_ShrinksSegments_NeverTouchesACredit()
    {
        var (state, pa, _) = ConnectedFixture();
        // a machine diaspora lives alongside the organics
        var machineSpecies = state.Skeleton.Species[state.PolityOf(1).SpeciesId];
        machineSpecies.Embodiment = StarGen.Core.Galaxy.Embodiment.Machine;
        var machines = new PopulationSegment(2, pa.Id, machineSpecies.Id,
            machineSpecies.Id, 2.0)
        { Wealth = 100 };
        state.Segments.Add(machines);
        var plague = Infect(state, pa.Id);
        double organicSize = state.Segments[0].Size;
        double wealthBefore = state.Segments.Sum(s => s.Wealth);

        PlagueOps.Step(state);
        Assert.True(state.Segments[0].Size < organicSize, "the toll is real");
        Assert.Equal(2.0, machines.Size, 9);   // machine minds don't sicken
        Assert.Equal(wealthBefore, state.Segments.Sum(s => s.Wealth), 9);
        Assert.True(plague.TotalDeaths > 0);
    }

    [Fact]
    public void Plagues_BurnOut_IntoImmunity_NeverSterilize()
    {
        var (state, pa, pb) = ConnectedFixture();
        var plague = Infect(state, pa.Id);
        plague.InfectedSince[pa.Id] = 100;
        plague.InfectedSince.Add(pb.Id, 100);
        state.WorldYear = 200;                 // long past the burnout window
        PlagueOps.Step(state);
        Assert.False(plague.Active);
        Assert.True(plague.EndedYear >= 0);
        Assert.True(plague.ImmuneUntil.ContainsKey(pa.Id));
        Assert.Contains(state.Staged,
            e => e.Type == WorldEventType.PlagueBurnedOut);
        Assert.True(state.Segments[0].Size > 0, "burnout, not sterilization");
    }

    [Fact]
    public void TheController_QuarantinesItsDoorstep_EndToEnd()
    {
        var (state, pa, pb) = ConnectedFixture();
        Infect(state, pb.Id);                  // plague at the far port
        new PerceptionPhase().Run(state);
        var frontier = state.Actors[0].Perception!.PlagueFrontier;
        var candidate = Assert.Single(frontier);
        Assert.Equal(pa.Id, candidate.OwnPortId);
        Assert.Equal(pb.Id, candidate.InfectedPortId);
        new IntentPhase().Run(state);
        new ResolutionPhase().Run(state);
        Assert.True(state.Lanes[0].QuarantinedUntil >= state.WorldYear,
            "the typed act finally learned its consequence");
    }

    [Fact]
    public void Plagues_AndQuarantines_SurviveTheRoundTrip()
    {
        var (state, pa, _) = ConnectedFixture();
        var plague = Infect(state, pa.Id);
        plague.ImmuneUntil.Add(1, 250);
        plague.TotalDeaths = 1.25;
        state.Lanes[0].QuarantinedUntil = 130;
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nPLAGUE|0|", text);
        var loaded = ArtifactSerializer.Load(new StringReader(text));
        Assert.Single(loaded.Plagues);
        Assert.Equal(plague.InfectedSince, loaded.Plagues[0].InfectedSince);
        Assert.Equal(plague.ImmuneUntil, loaded.Plagues[0].ImmuneUntil);
        Assert.Equal(130, loaded.Lanes[0].QuarantinedUntil);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
