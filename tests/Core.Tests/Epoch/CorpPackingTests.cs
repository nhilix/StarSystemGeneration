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

        // the plan schedules the founding build; savings carry it
        Cycle();
        Assert.Equal(1, Funded());

        // the war chest staggers the NEXT build through the schedule —
        // one plan entry per pick, never the whole portfolio at once
        Cycle();
        Assert.Equal(2, Funded());

        // a broke corp with no income packs nothing new
        corp.Withdraw(state, corp.Credits, 0);   // drain the wallet to zero
        corp.LastIncomePerYear = 0;
        int before = Funded();
        Cycle();
        Assert.Equal(before, Funded());
    }
}
