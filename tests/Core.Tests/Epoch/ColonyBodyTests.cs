using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Colony founding decides claim-aware, real bodies for its opening
/// facilities and — the Slice L follow-up this task closes — never ships
/// equipment to a bodiless extraction dud (mirrors SpawnFacilityConstruction's
/// reject).</summary>
public class ColonyBodyTests
{
    [Fact]
    public void FoundColonyFacilities_AssignsRealBodies_AtABodyBearingHex()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var owner = state.Actors[0].Id;
        HexCoordinate? sited = null;
        foreach (var cell in skeleton.Cells)
        {
            var hex = HexGrid.CellCenter(cell.Coord);
            var sys = SystemRegistry.Commit(state, hex);
            if (sys != null)
                for (int s = 0; s < sys.Stars.Count && sited == null; s++)
                    foreach (var slot in sys.Stars[s].Slots)
                        if (slot.Body != null) { sited = hex; break; }
            if (sited != null) break;
        }
        Assert.NotNull(sited);
        int before = state.Facilities.Count;
        ProjectOps.FoundColonyFacilities(state, sited!.Value, owner, 100);
        Assert.True(state.Facilities.Count > before);
        for (int i = before; i < state.Facilities.Count; i++)
            Assert.False(state.Facilities[i].Body.IsNone,
                "a colony facility at a body-bearing hex must claim a body");
    }

    [Fact]
    public void FoundColonyFacilities_SkipsBodilessDud_WhenEveryEligibleBodyIsClaimed()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var owner = state.Actors[0].Id;
        // find a body-bearing hex and enumerate EVERY body in its system
        HexCoordinate? sited = null;
        var bodies = new List<BodyRef>();
        foreach (var cell in skeleton.Cells)
        {
            var hex = HexGrid.CellCenter(cell.Coord);
            var sys = SystemRegistry.Commit(state, hex);
            if (sys == null) continue;
            var found = new List<BodyRef>();
            for (int s = 0; s < sys.Stars.Count; s++)
                foreach (var slot in sys.Stars[s].Slots)
                    if (slot.Body != null)
                        found.Add(new BodyRef(s, slot.Index));
            if (found.Count > 0) { sited = hex; bodies = found; break; }
        }
        Assert.NotNull(sited);
        // pre-claim every body at the hex with dummy Facility rows: with every
        // substrate-appropriate candidate taken, PlaceFacilityBody resolves
        // None for the founding extraction industry AND the subsistence farm
        // (both founding types are extraction), so the claim scan in
        // PlaceFacilityBody finds nothing free.
        foreach (var b in bodies)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)InfraTypeId.Mine, tier: 1, sited!.Value, owner, 100)
            { Body = b });
        int before = state.Facilities.Count;
        ProjectOps.FoundColonyFacilities(state, sited!.Value, owner, 100);
        // the crux: no bodiless extraction dud is ever created
        for (int i = before; i < state.Facilities.Count; i++)
        {
            var f = state.Facilities[i];
            Assert.False(f.Body.IsNone
                && BodySiting.IsExtraction((InfraTypeId)f.TypeId),
                "colony founding must not create a bodiless extraction dud");
        }
        // and here nothing founds at all: both founding bodies resolved None
        Assert.Equal(before, state.Facilities.Count);
    }
}
