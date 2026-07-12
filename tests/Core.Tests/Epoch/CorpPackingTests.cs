using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Stage 2 carried residue: corporations pack their builds
/// against income like polities do — a fat balance sheet no longer breaks
/// ground on the whole portfolio at once; the portfolio staggers as the
/// trailing income rate carries each next build.</summary>
public class CorpPackingTests
{
    [Fact]
    public void CorpBuilds_PackAgainstIncome_NotJustCredits()
    {
        var state = EpochTestKit.Seeded().State;
        var host = state.Actors[0];
        host.Entered = true;
        var port = new Port(0, host.Id, host.Seat, tier: 3, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        var actor = new Actor(state.Actors.Count, ActorKind.Corporation,
            "Hollow Combine", host.Seat, 0, new CorporateController())
        { Entered = true };
        state.Actors.Add(actor);
        var corp = new Corporation(0, actor.Id, "Hollow Combine",
            hostPolityId: host.Id, CorporateNiche.Extraction,
            homePortId: 0, foundedYear: 0)
        { Credits = 1e6, LastIncomePerYear = 0 };
        state.Corporations.Add(corp);

        int Funded()
        {
            int n = 0;
            foreach (var p in state.Projects)
                if (p.InFlight && p.FunderActorId == corp.ActorId) n++;
            return n;
        }

        CorporationOps.Operate(state);
        Assert.Equal(1, Funded());     // the founding build bootstraps

        CorporationOps.Operate(state);
        Assert.Equal(1, Funded());     // no income yet: the second waits

        corp.LastIncomePerYear = 1e6;  // a booming book carries more work
        CorporationOps.Operate(state);
        Assert.Equal(2, Funded());
    }
}
