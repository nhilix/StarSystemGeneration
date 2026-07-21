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
    public void Assign_WithHex_SettlesAtAnArbitraryDomainHex_NotThePort()
    {
        // domain-hex-expansion §3: the settle election resolves a resident's
        // body within a SATELLITE hex's committed system, not the port's.
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        var satHex = new StarGen.Core.Model.HexCoordinate(a0.Seat.Q + 3, a0.Seat.R);

        var body = PopulationSiting.Assign(state, port.Id, satHex);

        Assert.True(SystemRegistry.IsSettled(state, satHex));
        Assert.Equal(BodySiting.PortBody(state.SettledSystems[satHex]), body);
        // the port hex is a different system — the overload did not resolve there
        Assert.NotEqual(satHex, port.Hex);
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
