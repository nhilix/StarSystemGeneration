using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 1: the <see cref="Bank"/> record and its 1:1
/// founding alongside a <see cref="Currency"/>. <see cref="SimState.
/// FoundCurrency"/> is the single chokepoint every currency mints through
/// (currency-and-FX design); this pins that a Bank is founded there too, kept
/// dense-parallel to <see cref="SimState.Currencies"/>, and resolvable by
/// <see cref="SimState.BankOf"/> — the substrate later reserve-dynamics tasks
/// build on, with no behavior of its own yet.</summary>
public class BankFoundingTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static PolityRecord AddPolity(SimState state, int id)
    {
        state.Actors.Add(new Actor(id, ActorKind.Polity, $"P{id}",
            new HexCoordinate(id, id), entryEpoch: 0,
            new GenesisController(state.Config)) { Entered = true });
        var pr = new PolityRecord(id, 0);
        state.Polities.Add(pr);
        return pr;
    }

    [Fact]
    public void FoundCurrency_FoundsABank_KeyedToTheNewCurrency_AtZeroReserve()
    {
        var state = NewState();
        AddPolity(state, 0);

        var currency = state.FoundCurrency(0);

        // dense-parallel: exactly one Bank per Currency, same id
        Assert.Equal(state.Currencies.Count, state.Banks.Count);
        var bank = state.Banks[currency.Id];
        Assert.Equal(currency.Id, bank.CurrencyId);
        Assert.Equal(0.0, bank.Reserve);
        Assert.Equal(0.0, bank.CumulativeSpreadIntake);
        Assert.Equal(0.0, bank.CumulativeReserveFunded);
    }

    [Fact]
    public void BankOf_ResolvesTheSameObject_AsTheDenseRegistryEntry()
    {
        var state = NewState();
        AddPolity(state, 0);
        AddPolity(state, 1);

        var curA = state.FoundCurrency(0);
        var curB = state.FoundCurrency(1);

        Assert.Same(state.Banks[curA.Id], state.BankOf(curA.Id));
        Assert.Same(state.Banks[curB.Id], state.BankOf(curB.Id));
        Assert.NotSame(state.BankOf(curA.Id), state.BankOf(curB.Id));
    }

    [Fact]
    public void FoundCurrency_KeepsBanksDenseParallel_AcrossMultipleFoundings()
    {
        var state = NewState();
        for (int i = 0; i < 5; i++)
        {
            AddPolity(state, i);
            state.FoundCurrency(i);
        }

        Assert.Equal(5, state.Currencies.Count);
        Assert.Equal(5, state.Banks.Count);
        for (int i = 0; i < 5; i++)
            Assert.Equal(i, state.Banks[i].CurrencyId);
    }
}
