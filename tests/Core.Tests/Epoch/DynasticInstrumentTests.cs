using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 4 — dynastic instruments (interpolity/relations.md
/// §Dynastic instruments): marriages between lineage thrones buy warmth now
/// and lapse into succession claims two reigns later; claims die with the
/// line that pressed them.</summary>
public class DynasticInstrumentTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    /// <summary>A related pair forced onto dynastic thrones with fresh
    /// living rulers and houses.</summary>
    private static PolityRelation MakeDynasticPair(SimState state)
    {
        var rel = state.Relations[0];
        foreach (int id in new[] { rel.PolityAId, rel.PolityBId })
        {
            var pr = state.PolityOf(id);
            pr.Interior!.FormId = GovernmentFormId.Autocracy;
            var ruler = state.Characters[pr.Interior.RulerCharacterId];
            if (!ruler.Alive) ruler.Alive = true;
            if (ruler.DynastyId < 0)
            {
                var house = new Dynasty(state.Dynasties.Count, ruler.Name,
                                        ruler.Id, id);
                state.Dynasties.Add(house);
                ruler.DynastyId = house.Id;
            }
        }
        return rel;
    }

    [Fact]
    public void Instrument_BindsBetweenDynasticThrones_Only()
    {
        var state = Run();
        var rel = MakeDynasticPair(state);
        var act = new DynasticInstrumentAct(rel.PolityAId, rel.PolityBId,
                                            DynasticInstrument.Marriage);
        Assert.True(RelationsOps.ResolveDynasticInstrument(state, act));
        Assert.Equal(1, rel.DynasticTies);
        Assert.Equal(state.WorldYear, rel.LastTieYear);

        // a committee form can't marry anyone
        state.PolityOf(rel.PolityBId).Interior!.FormId =
            GovernmentFormId.Assembly;
        Assert.False(RelationsOps.ResolveDynasticInstrument(state, act));
        Assert.Equal(1, rel.DynasticTies);
    }

    [Fact]
    public void Ties_WarmTheTarget()
    {
        var state = Run();
        var rel = state.Relations[0];
        rel.DynasticTies = 0;
        double cold = RelationsOps.WarmthTarget(state, rel, 0);
        rel.DynasticTies = 2;
        double warm = RelationsOps.WarmthTarget(state, rel, 0);
        Assert.True(warm > cold, "instruments buy warmth this generation");
    }

    [Fact]
    public void LapsedTie_SeedsASuccessionClaim()
    {
        var state = Run();
        var rel = MakeDynasticPair(state);
        rel.DynasticTies = 1;
        rel.LastTieYear = state.WorldYear
            - state.Config.Relations.DynasticTieLapseYears;
        new EpochEngine().Step(state);
        Assert.Equal(0, rel.DynasticTies);
        Assert.Equal(-1, rel.LastTieYear);
        var claim = rel.Claims.FirstOrDefault(c =>
            c.Type == ClaimType.Succession && !c.Released);
        Assert.NotNull(claim);
        // the claim names a reigning house of its holder
        Assert.Equal(claim!.SubjectId,
            RelationsOps.RulingDynasty(state, claim.HolderPolityId));
    }

    [Fact]
    public void SuccessionClaim_DiesWithItsLine()
    {
        var state = Run();
        var rel = MakeDynasticPair(state);
        rel.Claims.Add(new RelationClaim(ClaimType.Succession, rel.PolityAId,
            999_999, state.WorldYear));   // a house nobody reigns by
        new EpochEngine().Step(state);
        Assert.DoesNotContain(rel.Claims, c =>
            c.Type == ClaimType.Succession && c.SubjectId == 999_999
            && !c.Released);
    }

    [Fact]
    public void TieClock_RoundTrips()
    {
        var state = Run();
        state.Relations[0].DynasticTies = 2;
        state.Relations[0].LastTieYear = 425;
        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));
        Assert.Equal(2, loaded.Relations[0].DynasticTies);
        Assert.Equal(425, loaded.Relations[0].LastTieYear);
        Assert.Equal(ArtifactSerializer.ToText(state),
                     ArtifactSerializer.ToText(loaded));
    }
}
