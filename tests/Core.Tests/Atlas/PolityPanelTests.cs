using System;
using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Polity panel — `polity`/`tech` parity (InteriorView:
/// form, legitimacy/cohesion/enforcement, official line as 1−ideology,
/// reign from the log, court, tech tiers + progress, factions, charters)
/// PLUS the T1/T2 additions: ReservePoints (the reserve treasury) and the
/// standing plan with `eplan`'s in-flight star.</summary>
public class PolityPanelTests
{
    private static readonly Lazy<SimState> Ran = new(() =>
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        return state;
    });

    private static PolityRecord FirstInteriorPolity(SimState state)
    {
        foreach (var pr in state.Polities)
            if (state.Actors[pr.ActorId].Entered && pr.Interior != null)
                return pr;
        throw new InvalidOperationException("no entered polity w/ interior");
    }

    [Fact]
    public void TheCardReadsInteriorTechAndTreasury()
    {
        var state = Ran.Value;
        var pr = FirstInteriorPolity(state);
        var card = PolityPanel.Card(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), pr.ActorId)!;
        var interior = pr.Interior!;
        Assert.Equal(state.Actors[pr.ActorId].Name, card.Name);
        Assert.Equal(GovernmentForms.Get(interior.FormId).Name, card.FormName);
        Assert.Equal(interior.Legitimacy, card.Legitimacy);
        Assert.Equal(interior.Cohesion, card.Cohesion);
        Assert.Equal(interior.Enforcement, card.Enforcement);
        for (int i = 0; i < 4; i++)   // the renderer's 1−raw display axis
            Assert.Equal(1 - interior.OfficialIdeology[i],
                         card.OfficialLine[i]);
        Assert.Equal(pr.Credits, card.Credits);
        Assert.Equal(pr.ReservePoints, card.ReservePoints);
        Assert.Equal(4, card.Tech.Count);
        Assert.Equal(new[] { "industrial", "military", "astrogation", "life" },
            new[] { card.Tech[0].DomainName, card.Tech[1].DomainName,
                    card.Tech[2].DomainName, card.Tech[3].DomainName });
        for (int d = 0; d < 4; d++)
        {
            Assert.Equal(pr.TechTier[d], card.Tech[d].Tier);
            Assert.Equal(pr.TechProgress[d]
                / Tech.Threshold(state.Config, pr.TechTier[d]),
                card.Tech[d].ProgressFraction, 12);
        }
    }

    [Fact]
    public void TheReignIsLogDerived_LikeTheRenderer()
    {
        var state = Ran.Value;
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered
                || pr.Interior is not { RulerCharacterId: >= 0 } interior)
                continue;
            var ruler = state.Characters[interior.RulerCharacterId];
            long expected = ruler.BirthYear;
            foreach (var e in state.Log.ForCharacter(ruler.Id))
                if (e.Type is WorldEventType.RulerAscended
                    or WorldEventType.CoupStruck)
                    expected = e.WorldYear;
            var card = PolityPanel.Card(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear), pr.ActorId)!;
            Assert.NotNull(card.Ruler);
            Assert.Equal(ruler.Name, card.Ruler!.Name);
            Assert.Equal(expected, card.Ruler.ReignFromYear);
            Assert.Equal(state.WorldYear - ruler.BirthYear, card.Ruler.Age);
            return;   // one ruled polity proves the derivation
        }
        throw new InvalidOperationException("no ruled polity in the run");
    }

    [Fact]
    public void ThePlanCarriesEplansInFlightStar()
    {
        var (_, state) = EpochTestKit.Seeded();
        var actor = state.Actors[state.Polities[0].ActorId];
        actor.Entered = true;
        var entries = new List<PlanEntry>
        {
            new(PlanEntryKind.Facility, ProjectPriority.Core, 5,
                (int)InfraTypeId.Depot, PortId: 0, default, Count: 0),
            new(PlanEntryKind.PortRaise, ProjectPriority.Growth, 9,
                -1, PortId: 0, default, Count: 0),
        };
        actor.Policies = PolityPolicies.Default with
        { Plan = new StandingPlan(entries) };
        // ground broken on the depot: same kind + port + type in flight
        state.Projects.Add(new Project(0, ProjectKind.FacilityConstruction,
            actor.Id, actor.Id, 0, default, 8, 5)
        { TypeId = (int)InfraTypeId.Depot });

        var card = PolityPanel.Card(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), actor.Id)!;
        Assert.Equal(2, card.Plan.Count);
        Assert.True(card.Plan[0].InFlight);
        Assert.Equal("Depot", card.Plan[0].TypeDesign);
        Assert.Equal(ProjectPriority.Core, card.Plan[0].Priority);
        Assert.Equal(5, card.Plan[0].StartYear);
        Assert.False(card.Plan[1].InFlight);
        Assert.Equal("(port raise)", card.Plan[1].TypeDesign);
    }

    [Fact]
    public void FactionsChartersAndTheCourtSurface()
    {
        var (_, state) = EpochTestKit.Seeded();
        var pr = state.Polities[0];
        var actor = state.Actors[pr.ActorId];
        actor.Entered = true;
        var leader = new Character(state.Characters.Count, "Vex", 0, 0,
                                   actor.Id, state.WorldYear - 40);
        state.Characters.Add(leader);
        var heir = new Character(state.Characters.Count, "Ada", 0, 0,
                                 actor.Id, state.WorldYear - 12)
        { Role = CharacterRole.Heir };
        state.Characters.Add(heir);
        state.Factions.Add(new Faction(0, "the Ledger", actor.Id,
            FactionBasis.Corporate, state.WorldYear)
        {
            Strength = 0.5, Grievance = 0.2, Militancy = 0.1,
            Wealth = 30, LeaderCharacterId = leader.Id,
        });
        var vex = new Corporation(0, 99, "Vex Combine",
            actor.Id, CorporateNiche.Freight, 0, state.WorldYear);
        state.Corporations.Add(vex);
        vex.Deposit(state, 500, 0);

        var card = PolityPanel.Card(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), actor.Id)!;
        var faction = Assert.Single(card.Factions);
        Assert.Equal("the Ledger", faction.Name);
        Assert.Equal("Vex", faction.LeaderName);
        Assert.Equal(0.5, faction.Strength);
        var charter = Assert.Single(card.Charters);
        Assert.Equal("Vex Combine", charter.Name);
        Assert.Equal(500.0, charter.Credits);
        var court = Assert.Single(card.Court);
        Assert.Equal(CharacterRole.Heir, court.Role);
        Assert.Equal("Ada", court.Name);
        Assert.Equal(12, court.Age);
    }

    [Fact]
    public void NoSuchPolityReturnsNull()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        Assert.Null(PolityPanel.Card(model,
            EyeContext.God(state.WorldYear), -1));
        Assert.Null(PolityPanel.Card(model,
            EyeContext.God(state.WorldYear), 9999));
    }
}
