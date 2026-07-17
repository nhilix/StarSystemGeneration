using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice L2 task 1 — segments settle at a real body address at
/// creation (locality slice §3). PopulationSiting.Assign resolves the
/// arrival address to the settled port body, committing the hex as a
/// side effect.</summary>
public class PopulationSitingTests
{
    [Fact]
    public void Assign_SettlesAtThePortBody_AndCommitsTheHex()
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));

        var body = PopulationSiting.Assign(state, port.Id);

        Assert.True(SystemRegistry.IsSettled(state, port.Hex));
        Assert.Equal(BodySiting.PortBody(state.SettledSystems[port.Hex]), body);
    }

    [Fact]
    public void HomeworldSegments_HaveARealBody_AfterGenesis()
    {
        var (_, state) = EpochTestKit.Seeded();
        new InteriorPhase().Run(state);
        int withBody = 0;
        foreach (var s in state.Segments)
            if (!s.Body.IsNone) withBody++;
        Assert.True(withBody > 0,
            "expected at least one seeded segment to carry a real body ref");
    }
}
