using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 14 regression: a fleet whose home port has fallen
/// into foreign hands (a captured home the fleet was never re-homed off)
/// victuals across a currency boundary. <see cref="FleetOps.SupplyFleets"/>'s
/// upkeep draw settles its sellers in the port's LOCAL currency
/// (<see cref="BookOps.LiftAsks"/>) but charges the buying polity's own-currency
/// military pool — the old code did that raw 1:1, silently re-denominating the
/// payment and leaking per-currency conservation exactly at the absorption
/// events the acceptance sweep exercises at scale. The fix converts and records
/// the transfer; this test is its acceptance bar.</summary>
public class FleetSupplyCurrencyTests
{
    private readonly struct Snap
    {
        public readonly double[] Supply, Fiat, Steady, ConvIn, ConvOut;
        public Snap(SimState s)
        {
            Supply = SupplyOps.WalkNative(s);
            int n = s.Currencies.Count;
            Fiat = new double[n]; Steady = new double[n];
            ConvIn = new double[n]; ConvOut = new double[n];
            for (int i = 0; i < n; i++)
            {
                Fiat[i] = s.Currencies[i].CumulativeFiatIssued;
                Steady[i] = s.Currencies[i].CumulativeSteadyIssuance;
                ConvIn[i] = s.Currencies[i].CumulativeConvertedIn;
                ConvOut[i] = s.Currencies[i].CumulativeConvertedOut;
            }
        }
    }

    [Fact]
    public void FleetVictualing_AtAForeignPort_ConservesPerCurrency()
    {
        // a real seeded world with currencies wired at genesis; one step emerges
        // the starter polities, each with its own currency and a home fleet
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Step(state);

        // polity A owns a home port P with a starter fleet; polity B is another
        // polity with a DIFFERENT wired currency (B need not be entered — it just
        // supplies the foreign-currency market the fleet ends up victualing in)
        PolityRecord? a = null, b = null;
        Port? port = null;
        FleetRecord? fleet = null;
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered || pr.CurrencyId < 0) continue;
            foreach (var p in state.Ports)
            {
                if (p.OwnerActorId != pr.ActorId) continue;
                var f = FleetOps.HomeFleet(state, pr.ActorId, p);
                if (f.TotalHulls > 0) { a = pr; port = p; fleet = f; break; }
            }
            if (a != null) break;
        }
        Assert.NotNull(a);
        Assert.NotNull(port);
        // B: any other polity record; mint it its own currency if it has none
        // yet (a small seeded galaxy may have emerged only A so far — we only
        // need a second, distinct currency for the foreign market)
        foreach (var pr in state.Polities)
            if (pr.ActorId != a!.ActorId) { b = pr; break; }
        Assert.NotNull(b);
        if (b!.CurrencyId < 0) state.FoundCurrency(b.ActorId);
        Assert.NotEqual(a!.CurrencyId, b.CurrencyId);
        fleet!.Posture = FleetPosture.Reserve;

        // make the two currencies genuinely diverge so a raw 1:1 carry leaks
        state.CurrencyOf(a!.CurrencyId).NumeraireRate = 1.0;
        state.CurrencyOf(b!.CurrencyId).NumeraireRate = 0.4;

        // the home port falls into B's hands (a capture the fleet outlived):
        // the fleet's HomePortId still points here, so A victuals in B's market
        fleet.HomePortId = port!.Id;
        port.OwnerActorId = b.ActorId;
        EpochTestKit.Stock(state, port.Id, (int)GoodId.Fuel, 500, 0.5,
            ownerActorId: b.ActorId);
        EpochTestKit.Stock(state, port.Id, (int)GoodId.Armaments, 500, 0.5,
            ownerActorId: b.ActorId);
        EpochTestKit.Stock(state, port.Id, (int)GoodId.ShipComponents, 500, 0.5,
            ownerActorId: b.ActorId);
        a.MilitaryPoints = 5000;

        var before = new Snap(state);
        int lost = FleetOps.SupplyFleets(state, a);
        var after = new Snap(state);

        // the fleet actually paid across the boundary (else the test proves
        // nothing): B's currency converted-in counter moved
        Assert.True(after.ConvOut[a.CurrencyId] > before.ConvOut[a.CurrencyId],
            "the fleet should have converted out of its own currency to pay");

        // per-currency conservation: each currency's native supply delta is fully
        // explained by its mints and its net conversions — no leak
        for (int i = 0; i < state.Currencies.Count; i++)
        {
            double residual = (after.Supply[i] - before.Supply[i])
                - (after.Fiat[i] - before.Fiat[i])
                - (after.Steady[i] - before.Steady[i])
                - (after.ConvIn[i] - before.ConvIn[i])
                + (after.ConvOut[i] - before.ConvOut[i]);
            double scale = System.Math.Max(1.0, System.Math.Abs(after.Supply[i]));
            Assert.True(System.Math.Abs(residual) <= 1.3e-9 * scale,
                $"currency {i}: fleet victualing leaked residual {residual:G6} "
                + $"on supply {after.Supply[i]:G6}");
        }
    }
}
