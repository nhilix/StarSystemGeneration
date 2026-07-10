using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 1 — contact and the per-pair relations gauges
/// (interpolity/relations.md): polities meet when reach overlaps, warmth and
/// tension drift toward source-computed targets, standing claims hold
/// tension until they resolve, and the relations layer round-trips.</summary>
public class RelationsTests
{
    // radius 12 so several polities enter and expand into each other —
    // smaller test galaxies never make cross-owner contact
    private static SimState Run(int epochs = 24, ulong seed = 42,
                                int radius = 12)
    {
        var state = EpochTestKit.Seeded(seed, radius).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void Contact_FiresOncePerPair_WithinReach()
    {
        var state = Run();
        Assert.True(state.Relations.Count > 0);   // seed 42 r10 neighbors meet
        int contactEvents = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.FirstContact) contactEvents++;
        Assert.Equal(state.Relations.Count, contactEvents);
        // one relation per unordered pair, keyed ascending
        var seen = new System.Collections.Generic.HashSet<(int, int)>();
        foreach (var r in state.Relations)
        {
            Assert.True(r.PolityAId < r.PolityBId);
            Assert.True(seen.Add((r.PolityAId, r.PolityBId)));
        }
    }

    [Fact]
    public void Contact_RequiresReach()
    {
        var state = Run();
        // every related pair really has ports within contact reach
        foreach (var r in state.Relations)
        {
            int minDist = int.MaxValue;
            foreach (var pa in state.Ports)
            {
                if (pa.OwnerActorId != r.PolityAId) continue;
                foreach (var pb in state.Ports)
                {
                    if (pb.OwnerActorId != r.PolityBId) continue;
                    int d = HexGrid.Distance(pa.Hex, pb.Hex);
                    if (d < minDist) minDist = d;
                }
            }
            Assert.True(minDist <= state.Config.Relations.ContactReachHexes,
                $"pair ({r.PolityAId},{r.PolityBId}) met at distance {minDist}");
        }
    }

    [Fact]
    public void Gauges_StayInBounds()
    {
        var state = Run();
        foreach (var r in state.Relations)
        {
            Assert.InRange(r.Warmth, 0.0, 1.0);
            Assert.InRange(r.Tension, 0.0, 1.0);
        }
    }

    [Fact]
    public void Strangeness_ZeroForKin_PositiveForAliens()
    {
        var state = Run(epochs: 1);
        Assert.True(state.Polities.Count >= 2);
        var a = state.Polities[0];
        Assert.Equal(0.0, RelationsOps.Strangeness(state, a, a));
        // different species with different embodiment or traits read strange
        PolityRecord? alien = null;
        foreach (var p in state.Polities)
            if (p.SpeciesId != a.SpeciesId) { alien = p; break; }
        if (alien != null)
            Assert.True(RelationsOps.Strangeness(state, a, alien) > 0.0);
    }

    [Fact]
    public void StandingClaim_HoldsTension_UntilReleased()
    {
        var state = Run();
        Assert.True(state.Relations.Count > 0);
        var rel = state.Relations[0];
        double before = RelationsOps.TensionTarget(state, rel, overlapPairs: 0);
        rel.Claims.Add(new RelationClaim(ClaimType.LostTerritory,
            rel.PolityAId, 0, state.WorldYear));
        rel.Claims.Add(new RelationClaim(ClaimType.Succession,
            rel.PolityBId, 0, state.WorldYear));
        double loaded = RelationsOps.TensionTarget(state, rel, overlapPairs: 0);
        Assert.True(loaded > before, "live claims must load the target");
        foreach (var c in rel.Claims) c.Released = true;
        double released = RelationsOps.TensionTarget(state, rel, overlapPairs: 0);
        Assert.Equal(before, released, 12);
    }

    [Fact]
    public void Tension_RelaxesSlowerThanItRises()
    {
        var cfg = new EpochSimConfig();
        Assert.True(cfg.Relations.TensionRisePerYear
                    > cfg.Relations.TensionRelaxPerYear);
    }

    [Fact]
    public void KinClaim_RaisesWhenKinLiveUnderForeignRule()
    {
        var state = Run();
        // manufacture kin: move a big segment of polity 0's founding culture
        // under a related foreign polity's port
        Assert.True(state.Relations.Count > 0);
        var rel = state.Relations[0];
        int holder = rel.PolityAId;
        int other = rel.PolityBId;
        int kinCulture = state.PolityOf(holder).Interior!.FoundingCultureId;
        Port? foreignPort = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == other) { foreignPort = p; break; }
        Assert.NotNull(foreignPort);
        var kin = new PopulationSegment(state.Segments.Count, foreignPort!.Id,
            state.PolityOf(holder).SpeciesId, kinCulture, 2.0);
        state.Segments.Add(kin);

        new EpochEngine().Step(state);
        Assert.True(rel.HasLiveClaim(ClaimType.CulturalKin, holder, kinCulture));

        // the kin gone, the claim releases and tension may cool
        kin.Size = 0.0;
        new EpochEngine().Step(state);
        Assert.False(rel.HasLiveClaim(ClaimType.CulturalKin, holder, kinCulture));
    }

    [Fact]
    public void DiplomaticPostures_WrittenFromRelations()
    {
        var state = Run();
        Assert.True(state.Relations.Count > 0);
        bool anyPosture = false;
        foreach (var rel in state.Relations)
        {
            var actor = state.Actors[rel.PolityAId];
            if (actor.Policies is PolityPolicies pp
                && pp.DiplomaticPostures.ContainsKey(rel.PolityBId))
                anyPosture = true;
        }
        Assert.True(anyPosture,
            "Intent should write a stance for every met polity");
    }

    [Fact]
    public void RelationsLayer_RoundTrips()
    {
        var built = Run();
        Assert.True(built.Relations.Count > 0);
        // force claim variety whatever the run produced
        built.Relations[0].Claims.Add(new RelationClaim(
            ClaimType.LostTerritory, built.Relations[0].PolityBId, 7, 250)
        { Released = true, ReleasedYear = 300 });
        built.Relations[0].DynasticTies = 2;
        built.Relations[0].OfferedRung = TreatyRung.TradePact;
        built.Relations[0].OfferedById = built.Relations[0].PolityAId;
        built.Relations[0].OfferEpoch = 9;

        var loaded = ArtifactSerializer.Load(
            new StringReader(ArtifactSerializer.ToText(built)));

        Assert.Equal(built.Relations.Count, loaded.Relations.Count);
        for (int i = 0; i < built.Relations.Count; i++)
        {
            var b = built.Relations[i];
            var l = loaded.Relations[i];
            Assert.Equal(b.PolityAId, l.PolityAId);
            Assert.Equal(b.PolityBId, l.PolityBId);
            Assert.Equal(b.MetEpoch, l.MetEpoch);
            Assert.Equal(b.Warmth, l.Warmth);
            Assert.Equal(b.Tension, l.Tension);
            Assert.Equal(b.Rung, l.Rung);
            Assert.Equal(b.OfferedRung, l.OfferedRung);
            Assert.Equal(b.OfferedById, l.OfferedById);
            Assert.Equal(b.OfferEpoch, l.OfferEpoch);
            Assert.Equal(b.DynasticTies, l.DynasticTies);
            Assert.Equal(b.VassalPolityId, l.VassalPolityId);
            Assert.Equal(b.Claims.Count, l.Claims.Count);
            for (int c = 0; c < b.Claims.Count; c++)
            {
                Assert.Equal(b.Claims[c].Type, l.Claims[c].Type);
                Assert.Equal(b.Claims[c].HolderPolityId, l.Claims[c].HolderPolityId);
                Assert.Equal(b.Claims[c].SubjectId, l.Claims[c].SubjectId);
                Assert.Equal(b.Claims[c].RaisedYear, l.Claims[c].RaisedYear);
                Assert.Equal(b.Claims[c].Released, l.Claims[c].Released);
                Assert.Equal(b.Claims[c].ReleasedYear, l.Claims[c].ReleasedYear);
            }
        }
        // and the byte-identity gate holds over the new layer
        Assert.Equal(ArtifactSerializer.ToText(built),
                     ArtifactSerializer.ToText(loaded));
    }
}
