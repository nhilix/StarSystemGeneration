using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the War panel (`wars`/`war &lt;id&gt;` parity —
/// InterpolityView: objective status, siege falls-at threshold, side
/// strength as a fraction of mustered, the four war-chronicle payloads)
/// and the Relations panel (`relations` parity: BothLive filter, the six
/// warmth/tension source terms, bond, claims).</summary>
public class WarRelationsPanelTests
{
    private static (AtlasReadModel Model, SimState State, War War)
        WithWar()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? a = null;
        foreach (var cell in state.Skeleton.Cells)
            if (!cell.IsVoid) { a = HexGrid.CellCenter(cell.Coord); break; }
        state.Ports.Add(new Port(0, state.Actors[1].Id, a!.Value, 2, 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        var war = new War(0, "the Test War", state.Actors[0].Id,
            state.Actors[1].Id, CasusBelli.ResourceSeizure, -1,
            WarDemand.CedeObjectives, state.WorldYear)
        {
            AttackerExhaustion = 0.3,
            DefenderExhaustion = 0.6,
            AttackerStrengthAtStart = 10.0,
        };
        war.Objectives.Add(new WarObjective(0,
            WarObjectiveType.CapturePort, targetId: 0)
        { SiegeYears = 3 });
        state.Wars.Add(war);
        return (new AtlasReadModel(state), state, war);
    }

    [Fact]
    public void TheWarListCountsTakenObjectives()
    {
        var (model, state, war) = WithWar();
        war.Objectives.Add(new WarObjective(1,
            WarObjectiveType.DestroyFleet, state.Actors[1].Id)
        { Status = ObjectiveStatus.Taken });
        var rows = WarPanel.Rows(model, EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows);
        Assert.Equal("the Test War", row.Name);
        Assert.True(row.Active);
        Assert.Equal(1, row.ObjectivesTaken);
        Assert.Equal(2, row.ObjectivesTotal);
        Assert.Equal(state.Actors[war.AttackerId].Name, row.AttackerName);
    }

    [Fact]
    public void AContestedSiegeCarriesItsFallsAtThreshold()
    {
        var (model, state, war) = WithWar();
        var card = WarPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        var front = Assert.Single(card.Objectives);
        Assert.Equal(WarObjectiveType.CapturePort, front.Type);
        Assert.Equal(ObjectiveStatus.Contested, front.Status);
        Assert.Equal(3, front.SiegeYears);
        Assert.Equal(WarConduct.SiegeThreshold(state, war, state.Ports[0]),
                     front.FallsAtYears);
    }

    [Fact]
    public void SideStrengthReadsAsAFractionOfMustered()
    {
        var (model, state, war) = WithWar();
        var card = WarPanel.Card(model, EyeContext.God(state.WorldYear), 0)!;
        Assert.Equal(0.3, card.Attacker.Exhaustion);
        Assert.Equal(WarOps.SideStrength(state, war, attacker: true) / 10.0,
                     card.Attacker.StrengthOfMustered);
        // the defender mustered nothing — no fraction to show
        Assert.Null(card.Defender.StrengthOfMustered);
    }

    [Fact]
    public void RelationsReadTheSourceTermsAndClaims()
    {
        var (_, state) = EpochTestKit.Seeded();
        var entered = new System.Collections.Generic.List<int>();
        foreach (var actor in state.Actors)
        {
            if (actor.Kind != ActorKind.Polity || entered.Count == 2) continue;
            actor.Entered = true;
            entered.Add(actor.Id);
        }
        var rel = new PolityRelation(entered[0], entered[1], 3)
        { Warmth = 0.4, Tension = 0.7, DynasticTies = 1 };
        for (int i = 0; i < 6; i++)
        {
            rel.LastWarmthTerms[i] = i * 0.1;
            rel.LastTensionTerms[i] = i * 0.05;
        }
        rel.Claims.Add(new RelationClaim(ClaimType.Succession, entered[0],
                                         subjectId: 7, raisedYear: 12));
        state.Relations.Add(rel);
        // a dead pair must NOT surface (BothLive parity)
        state.Relations.Add(new PolityRelation(entered[0],
            state.Actors[FindUnentered(state)].Id, 0));

        var model = new AtlasReadModel(state);
        var rows = RelationsPanel.Rows(model,
            EyeContext.God(state.WorldYear));
        var row = Assert.Single(rows);
        Assert.Equal(0.4, row.Warmth);
        Assert.Equal(0.7, row.Tension);
        Assert.Equal(0.2, row.WarmthTerms[2], 12);
        Assert.Equal(0.25, row.TensionTerms[5], 12);
        Assert.Equal(1, row.DynasticTies);
        var claim = Assert.Single(row.Claims);
        Assert.Equal(ClaimType.Succession, claim.Type);
        Assert.Equal(entered[0], claim.HolderPolityId);
    }

    [Fact]
    public void APolityFilterNarrowsTheRelationRows()
    {
        var (_, state) = EpochTestKit.Seeded();
        var entered = new System.Collections.Generic.List<int>();
        foreach (var actor in state.Actors)
        {
            if (actor.Kind != ActorKind.Polity || entered.Count == 3) continue;
            actor.Entered = true;
            entered.Add(actor.Id);
        }
        state.Relations.Add(new PolityRelation(entered[0], entered[1], 0));
        state.Relations.Add(new PolityRelation(entered[1], entered[2], 0));
        var model = new AtlasReadModel(state);
        Assert.Equal(2, RelationsPanel.Rows(model,
            EyeContext.God(state.WorldYear)).Count);
        var mine = RelationsPanel.Rows(model,
            EyeContext.God(state.WorldYear), entered[0]);
        var row = Assert.Single(mine);
        Assert.True(row.PolityAId == entered[0] || row.PolityBId == entered[0]);
    }

    private static int FindUnentered(SimState state)
    {
        foreach (var actor in state.Actors)
            if (actor.Kind == ActorKind.Polity && !actor.Entered)
                return actor.Id;
        throw new System.InvalidOperationException("all entered");
    }
}
