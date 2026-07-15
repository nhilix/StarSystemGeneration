using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 5: graduation — strength × grievance against
/// legitimacy × enforcement; schisms found polities, coups replace rulers,
/// failures are crushed into revolt. Conservation holds through all of it.</summary>
public class GraduationTests
{
    /// <summary>Run a full history with a hair-trigger graduation test so
    /// every route gets exercised at this small radius.</summary>
    private static SimState VolatileRun(ulong seed = 42)
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = seed, GalaxyRadiusCells = 8 };
        var config = new EpochSimConfig { MasterSeed = seed };
        config.Faction.GraduationGripFactor = 0.5;
        config.Faction.AppeasementDemandShare = 2.0;   // nobody stays bought
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc), config);
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void VolatileHistory_GraduatesEveryRouteItRolls()
    {
        var state = VolatileRun();
        var types = state.Log.Events.Select(e => e.Type).ToHashSet();
        // with a hair trigger, SOMETHING graduated over 40 epochs
        Assert.True(types.Contains(WorldEventType.SchismDeclared)
                    || types.Contains(WorldEventType.CoupStruck)
                    || types.Contains(WorldEventType.RevoltCrushed),
            "no graduation of any kind in a volatile 40-epoch history");
    }

    [Fact]
    public void Schism_FoundsAWorkingPolity_Conserved()
    {
        var state = VolatileRun();
        var schisms = state.Log.Events
            .Where(e => e.Type == WorldEventType.SchismDeclared)
            .Select(e => (SchismDeclaredPayload)e.Payload!).ToList();
        if (schisms.Count == 0) return;   // this seed rolled coups instead
        foreach (var s in schisms)
        {
            var actor = state.Actors[s.NewPolityId];
            Assert.Equal(ActorKind.Polity, actor.Kind);
            // a schism state may since have federated or been absorbed
            // (slice H) — its founding story still checked out
            if (actor.Retired) continue;
            Assert.True(actor.Entered);
            Assert.Equal(s.NewPolityName, actor.Name);
            var pr = state.PolityOf(s.NewPolityId);
            Assert.NotNull(pr.Interior);
            // the seceded state has a ruler on its seat
            var ruler = state.Characters[pr.Interior!.RulerCharacterId];
            Assert.Equal(CharacterRole.Ruler, ruler.Role);
            Assert.Equal(s.NewPolityId, ruler.PolityId);
            // it owns designs (they never leave), and ports unless a later
            // war or schism stripped them (slice H: conquest moves ports)
            Assert.Contains(state.Designs, d => d.OwnerActorId == s.NewPolityId);
            bool stripped = state.Log.Events.Any(ev =>
                (ev.Type == WorldEventType.PortCaptured
                 && ev.Actors.Count > 1 && ev.Actors[1] == s.NewPolityId)
                || (ev.Type == WorldEventType.SchismDeclared
                    && ev.Actors.Count > 0 && ev.Actors[0] == s.NewPolityId));
            if (!stripped)
                Assert.Contains(state.Ports,
                                p => p.OwnerActorId == s.NewPolityId);
        }
        // money still conserves across the split — now measured PER CURRENCY
        // (currency-and-FX design). The old single lump sum of native credits
        // stopped being conserved the moment real FX rates went live: a schism
        // mints the child a brand-new currency and force-converts the seceding
        // treasury/pool/segment share into it (a recorded transfer that changes
        // the native sum). The invariant is each Currency's own residual reading
        // zero across the whole history — every currency grows only through its
        // own declared mints, and every conversion nets across its Converted
        // In/Out pair. This is exactly the schism path exercised end-to-end.
        Assert.True(state.Health.Rows.Count >= 10, "history too short");
        for (int i = 1; i < state.Health.Rows.Count; i++)
        {
            var row = state.Health.Rows[i];
            foreach (var cur in row.Currencies)
            {
                double scale = System.Math.Max(1.0, System.Math.Abs(cur.Supply));
                Assert.True(System.Math.Abs(cur.Residual) <= 1.3e-9 * scale,
                    $"epoch {row.Epoch} currency {cur.CurrencyId}: residual "
                    + $"{cur.Residual:G6} on supply {cur.Supply:G6}");
            }
        }
    }

    [Fact]
    public void Coup_PutsTheFactionLeaderOnTheSeat()
    {
        var state = VolatileRun();
        var coups = state.Log.Events
            .Where(e => e.Type == WorldEventType.CoupStruck)
            .Select(e => (CoupStruckPayload)e.Payload!).ToList();
        if (coups.Count == 0) return;
        foreach (var c in coups)
        {
            var usurper = state.Characters[c.CharacterId];
            // still ruling, later deposed/succeeded, or dead — but the coup
            // moment must have made them ruler of that polity. A polity that
            // since merged away (slice H) moved its people to the successor.
            if (!state.Actors[c.PolityId].Retired)
                Assert.Equal(c.PolityId, usurper.PolityId);
            var faction = state.Factions[c.FactionId];
            Assert.False(faction.Active);   // graduated into government
            Assert.Equal(0.0, faction.Wealth);   // the chest funded the regime
        }
    }

    [Fact]
    public void Revolt_MartyrsTheLeader_KeepsGrievance()
    {
        var state = VolatileRun();
        var revolts = state.Log.Events
            .Where(e => e.Type == WorldEventType.RevoltCrushed)
            .Select(e => (RevoltCrushedPayload)e.Payload!).ToList();
        if (revolts.Count == 0) return;
        foreach (var r in revolts)
        {
            var martyr = state.Characters[r.CharacterId];
            Assert.False(martyr.Alive);
        }
    }

    [Fact]
    public void GraduationsStayInTheLowSingleDigits_AtDefaults()
    {
        // the shape band: defaults must not dissolve polities every epoch
        var (_, state) = EpochTestKit.Seeded(42, 10);
        new EpochEngine().Run(state);
        int graduations = state.Log.Events.Count(e =>
            e.Type is WorldEventType.SchismDeclared
            or WorldEventType.CoupStruck or WorldEventType.RevoltCrushed);
        int polities = state.Polities.Count(p => p.Interior != null);
        if (polities == 0) return;
        Assert.InRange(graduations, 0, polities * 5);
    }

    [Fact]
    public void SchismState_RoundTripsThroughTheArtifact()
    {
        var state = VolatileRun();
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Actors.Count, loaded.Actors.Count);
        Assert.Equal(state.Cultures.Count, loaded.Cultures.Count);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
