using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE (C11): corporations run a STANDING PLAN — Perception
/// offers the investment pick, the CorporateController packs it against
/// income + horizon-spread savings, and Operate executes the due entries
/// mechanically (Move 1). A fat balance sheet staggers through scheduled
/// builds instead of breaking ground on the whole portfolio at once; a
/// broke corp with no income plans nothing.</summary>
public class CorpPackingTests
{
    [Fact]
    public void CorpBuilds_FollowTheStandingPlan()
    {
        var state = EpochTestKit.Seeded().State;
        var host = state.Actors[0];
        host.Entered = true;
        var port = new Port(0, host.Id, host.Seat, tier: 3, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        var actor = new Actor(state.Actors.Count, ActorKind.Corporation,
            "Hollow Combine", host.Seat, 0,
            new CorporateController(state.Config))
        { Entered = true };
        state.Actors.Add(actor);
        var corp = new Corporation(0, actor.Id, "Hollow Combine",
            hostPolityId: host.Id, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0)
        { LastIncomePerYear = 0 };
        state.Corporations.Add(corp);
        corp.Deposit(state, 1e6, 0);   // wallet is the corp's whole balance now

        int Funded()
        {
            int n = 0;
            foreach (var p in state.Projects)
                if (p.InFlight && p.FunderActorId == corp.ActorId) n++;
            return n;
        }

        // the Move-1 cycle: perceive → decide (the plan) → execute
        void Cycle()
        {
            new PerceptionPhase().Run(state);
            actor.Policies = actor.Controller
                .Decide(actor.Perception!).Policies;
            CorporationOps.Operate(state);
        }

        // no plan yet (Operate before any Intent): nothing breaks ground
        CorporationOps.Operate(state);
        Assert.Equal(0, Funded());

        // DX Stage 1 (domain hex-expansion §2): the corp now runs the SAME
        // hex-granular domain scan as a polity, so Perception offers its top
        // domain candidates (best hexes across the home-port domain), not a
        // single synthesized pick. The greedy packer schedules the whole
        // AFFORDABLE slate against income + savings — identical discipline to
        // the polity scheduler (Planner.BuildCorpPlan). The wallet here is
        // deliberately huge to isolate packing; staggering under a realistic
        // budget is the per-year timeline's job, not a one-pick-per-cycle cap.
        Cycle();
        int offered = actor.Perception!.ConstructionCandidates.Count;
        Assert.True(offered >= 1, "the domain scan offers real candidates");
        Assert.Equal(offered, Funded());   // the affordable slate breaks ground

        // a broke corp with no income packs nothing new — capacity collapses to
        // zero, so no candidate fits the schedule (the real invariant)
        corp.Withdraw(state, corp.Credits, 0);   // drain the wallet to zero
        corp.LastIncomePerYear = 0;
        int before = Funded();
        Cycle();
        Assert.Equal(before, Funded());
    }
}
