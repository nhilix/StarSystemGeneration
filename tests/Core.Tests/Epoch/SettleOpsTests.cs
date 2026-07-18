using System.Linq;
using Xunit;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The settle election (domain-hex-expansion design §3, Stage 2):
/// sustained unmet labor at a worked satellite hex draws an eligible segment
/// to settle it, founding an outpost. The trigger is world-time (facility
/// maturity + a per-domain cadence, never step counts); the payment is
/// conserved (segment Wealth → construction wages); the segment keeps its
/// administering port.</summary>
public class SettleOpsTests
{
    // --- a controlled one-port domain with a matured, under-labored working ---
    private static (SimState state, Port port, PopulationSegment seg, Facility work)
        Domain(double habitatWealth = 100.0, long facilityAge = 100,
               double portHexDistance = 3, double segSize = 0.5)
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        a0.Entered = true;
        var portHex = a0.Seat;
        var port = new Port(0, a0.Id, portHex, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        state.WorldYear = 500;

        // the home segment lives at the port hex and can bankroll a habitat.
        // Its size is small on purpose: a distant commute from a thin labor
        // pool under-supplies even a single mine's LaborRequired, so the
        // satellite hex reads as sustained-under-labored.
        var seg = new PopulationSegment(0, port.Id, 0, 0, segSize)
        { Hex = portHex, Wealth = habitatWealth };
        state.Segments.Add(seg);

        // a worked satellite hex a few hops out: a mature mine attached to the
        // port. From the port hex the commute weight is weak, so its weighted
        // workforce falls short of LaborRequired — under-labored.
        var satHex = new HexCoordinate(portHex.Q + (int)portHexDistance, portHex.R);
        var work = new Facility(state.Facilities.Count, (int)InfraTypeId.Mine, 1,
            satHex, a0.Id, builtYear: 0)
        { CommissionedYear = state.WorldYear - facilityAge };
        state.Facilities.Add(work);
        return (state, port, seg, work);
    }

    [Fact]
    public void SustainedUnderLabor_SettlesASegment_AndFoundsAnOutpost()
    {
        var (state, port, seg, work) = Domain();

        int founded = SettleOps.Step(state);

        Assert.Equal(1, founded);
        Assert.Single(state.Outposts);
        var o = state.Outposts[0];
        Assert.Equal(work.Hex, o.Hex);
        Assert.Equal(port.Id, o.ParentPortId);
        Assert.Equal(state.WorldYear, o.FoundingYear);
        Assert.False(o.Graduated);
        Assert.False(string.IsNullOrWhiteSpace(o.Name));
        // the segment relocated to the satellite hex but stays administered by
        // the parent port
        Assert.Equal(work.Hex, seg.Hex);
        Assert.Equal(port.Id, seg.PortId);
        // its body resolves within the satellite hex's now-committed system
        Assert.True(SystemRegistry.IsSettled(state, work.Hex));
        Assert.Equal(BodySiting.PortBody(state.SettledSystems[work.Hex]), seg.Body);
    }

    [Fact]
    public void YoungFacility_DoesNotTrigger_TheMaturityGateBlocksIt()
    {
        // a brief spike: the working is only a few years old, below
        // SettleMaturityYears — not yet "sustained", so no one settles.
        var (state, _, _, _) = Domain(facilityAge: 5);
        Assert.True(5 < state.Config.Expansion.SettleMaturityYears);

        int founded = SettleOps.Step(state);

        Assert.Equal(0, founded);
        Assert.Empty(state.Outposts);
    }

    [Fact]
    public void FinerClock_SettlesNoFaster_OverTheSameWorldSpan()
    {
        // world-time property: the maturity gate reads facility age in
        // world-years, so the SAME facility age produces the SAME verdict
        // whatever the clock's YearsPerEpoch. A 1-year-resolution world and a
        // 25-year one both refuse a 40y-old working under a 50y maturity gate
        // and both accept a 60y-old one.
        foreach (int years in new[] { 1, 25 })
        {
            var (tooYoung, _, _, _) = Domain(facilityAge: 40);
            tooYoung.Config.Sim.YearsPerEpoch = years;
            Assert.Equal(0, SettleOps.Step(tooYoung));

            var (mature, _, _, _) = Domain(facilityAge: 60);
            mature.Config.Sim.YearsPerEpoch = years;
            Assert.Equal(1, SettleOps.Step(mature));
        }
    }

    [Fact]
    public void SettlePayment_ConservesDomainWealth()
    {
        var (state, port, seg, _) = Domain(habitatWealth: 100.0);
        double before = state.Segments.Where(s => s.PortId == port.Id)
                                      .Sum(s => s.Wealth);

        SettleOps.Step(state);

        double after = state.Segments.Where(s => s.PortId == port.Id)
                                     .Sum(s => s.Wealth);
        // money moved WHERE it lands, never how much exists: the domain's
        // total household wealth is unchanged to FP epsilon.
        Assert.Equal(before, after, 9);
    }

    [Fact]
    public void SettlePayment_DrawsExactlyTheHabitatCost_FromTheFunder()
    {
        // one segment in the domain: it pays the habitat cost, then banks its
        // whole size-share of the wage back (it is the only payee), so it nets
        // exactly zero — but the cost really flowed through PayWages. Add a
        // second port household so the net draw is visible.
        var (state, port, seg, _) = Domain(habitatWealth: 100.0);
        // a second port household of equal size → the wage splits 50/50, and
        // the election ties on size, so lowest-id (seg) is the funder.
        var other = new PopulationSegment(state.Segments.Count, port.Id, 0, 0,
            seg.Size) { Hex = port.Hex, Wealth = 0.0 };
        state.Segments.Add(other);

        double cost = state.Config.Expansion.SettleHabitatCost;
        double segBefore = seg.Wealth;

        SettleOps.Step(state);

        // funder (size 3 of 6) paid `cost`, banked half back: net −cost/2.
        // the other household (size 3 of 6) banked the other half: +cost/2.
        Assert.Equal(segBefore - cost + cost * 0.5, seg.Wealth, 9);
        Assert.Equal(cost * 0.5, other.Wealth, 9);
    }

    [Fact]
    public void InsufficientWealth_MakesNoOneEligible_NoSettlement()
    {
        // the sole segment cannot fund a meaningful habitat → not eligible.
        var (state, _, _, _) = Domain(habitatWealth: 0.5);
        Assert.True(0.5 < state.Config.Expansion.SettleHabitatCost);

        Assert.Equal(0, SettleOps.Step(state));
        Assert.Empty(state.Outposts);
    }

    [Fact]
    public void CadenceGate_HoldsFire_UntilTheWindowElapses()
    {
        var (state, port, _, _) = Domain();
        Assert.Equal(1, SettleOps.Step(state));   // first outpost founds now

        // give the domain a second under-labored worked hex so ONLY the
        // cadence gate can be what blocks a second election this window.
        var far = new HexCoordinate(port.Hex.Q - 3, port.Hex.R);
        var work2 = new Facility(state.Facilities.Count, (int)InfraTypeId.Mine, 1,
            far, port.OwnerActorId, builtYear: 0)
        { CommissionedYear = state.WorldYear - 100 };
        state.Facilities.Add(work2);
        // add another thin funder still at the port hex (small, so `far` stays
        // under-labored once the cadence window opens)
        state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
            0, 0, 0.5) { Hex = port.Hex, Wealth = 100.0 });

        // same world-year: inside the cadence window → no second founding
        Assert.Equal(0, SettleOps.Step(state));
        Assert.Single(state.Outposts);

        // advance past the cadence window → the domain may settle again
        state.WorldYear += (int)state.Config.Expansion.SettleCadenceYears + 1;
        Assert.Equal(1, SettleOps.Step(state));
        Assert.Equal(2, state.Outposts.Count);
    }

    [Fact]
    public void AlreadyResidentHex_IsNotSettledTwice()
    {
        var (state, port, _, work) = Domain();
        SettleOps.Step(state);
        Assert.Single(state.Outposts);

        // a fresh funder at the port hex, cadence window elapsed — but the
        // only worked hex now has a resident, so nothing settles there again.
        state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
            0, 0, 3.0) { Hex = port.Hex, Wealth = 100.0 });
        state.WorldYear += (int)state.Config.Expansion.SettleCadenceYears + 1;

        Assert.Equal(0, SettleOps.Step(state));
        Assert.Single(state.Outposts);
    }

    [Fact]
    public void FullSeed42History_FoundsOutposts_PopFollowsWork()
    {
        // end-to-end: over a full history, satellite workings scattered by
        // Stage 1 draw residents on their own books — at least one outpost
        // founds, and its resident segment really sits at its hex.
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        Assert.NotEmpty(state.Outposts);
        var o = state.Outposts[0];
        Assert.Contains(state.Segments,
            s => s.PortId == o.ParentPortId && s.Hex.Equals(o.Hex) && s.Size > 0);
        // the founding surfaced as its own event type, never PortEstablished
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.OutpostFounded);
    }

    [Fact]
    public void Deterministic_SameConfig_SameOutpostName()
    {
        var (s1, _, _, _) = Domain();
        var (s2, _, _, _) = Domain();
        SettleOps.Step(s1);
        SettleOps.Step(s2);
        Assert.Equal(s1.Outposts[0].Name, s2.Outposts[0].Name);
    }
}
