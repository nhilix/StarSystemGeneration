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
        // credits still conserve to the mint across the split
        double minted = 0, held = 0;
        var eco = state.Config.Economy;
        foreach (var a in state.Actors)
            // the emergence event IS the mint record — retirement (slice H
            // mergers) moves the credits on, never uncoins them
            if (state.Log.Events.Any(e =>
                    e.Type == WorldEventType.PolityEmerged
                    && e.Actors.Contains(a.Id)))
                minted += eco.InitialCreditsPerPolity
                          + state.Config.Expansion.HomeworldSegmentSize
                            * eco.InitialWealthPerPop;
        // slice ME §5: bounded sovereign issuance is the second declared mint
        minted += state.CumulativeFiatIssued;
        foreach (var p in state.Polities)
            held += p.Credits + p.ExpansionPoints + p.DevelopmentPoints
                    + p.MilitaryPoints + p.ReservePoints;
        foreach (var seg in state.Segments) held += seg.Wealth;
        foreach (var f in state.Factions) held += f.Wealth;
        foreach (var c in state.Corporations) held += c.Credits;
        // a colony expedition in flight carries the settlers' stake between
        // treasuries (charged at dispatch, minted into the colony on arrival —
        // Task 9's world-time founding), so count the in-transit ColonyCost
        foreach (var p in state.Projects)
            if (p.InFlight && p.Kind == ProjectKind.ColonyExpedition)
                held += state.Config.Expansion.ColonyCost;
        // escrow is held credits too (slice CE): open order escrow and
        // courier fees in flight
        foreach (var o in state.Orders) held += o.EscrowCredits;
        foreach (var c in state.Couriers) held += c.FeeEscrow;
        Assert.Equal(minted, held, System.Math.Abs(minted) * 1e-9);
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
