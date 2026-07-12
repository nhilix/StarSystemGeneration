using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Supply lands on the BOOK (contract-economy spec §2): facilities
/// post terrain-graded output as their owner's sell orders; processing
/// lifts input asks through C's recipes, paying the sellers; construction
/// lag and condition gate output; homeworld entry seeds starter industry.</summary>
public class MarketSupplyTests
{
    /// <summary>A minimal entered polity with one port+market+segment at its
    /// homeworld seat — direct fixture control for phase-level tests.</summary>
    private static (SimState State, Port Port) Fixture(double segmentSize = 3.0)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, port.Id, species, species,
                                                 segmentSize));
        state.PolityOf(actor.Id).Credits = 500.0;
        state.WorldYear = 100;
        return (state, port);
    }

    private static Facility Built(SimState state, InfraTypeId type,
                                  HexCoordinate hex, int owner,
                                  double condition = 1.0)
    {
        // backdated past every catalog construction time — active now
        var f = new Facility(state.Facilities.Count, (int)type, 1, hex, owner,
                             state.WorldYear - 10) { Condition = condition };
        state.Facilities.Add(f);
        return f;
    }

    [Fact]
    public void Extraction_DepositsTerrainGradedOutputIntoTheMarket()
    {
        var (state, port) = Fixture();
        Built(state, InfraTypeId.Mine, port.Hex, port.OwnerActorId);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Ore) > 0);
        // raw grade roots in terrain richness: 0.15 + 0.7×richness, richness ≤ 1
        Assert.InRange(BookOps.AskGrade(state, 0, (int)GoodId.Ore), 0.15, 0.85);
    }

    [Fact]
    public void Processing_ConsumesInputsAndDepositsGradedOutput()
    {
        var (state, port) = Fixture();
        Built(state, InfraTypeId.Refinery, port.Hex, port.OwnerActorId);
        EpochTestKit.Stock(state, 0, (int)GoodId.Ore, 1000.0, 0.6,
            ownerActorId: 1);
        EpochTestKit.Stock(state, 0, (int)GoodId.Volatiles, 1000.0, 0.6,
            ownerActorId: 1);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Alloys) > 0);
        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Ore) < 1000.0);
        Assert.True(BookOps.AskGrade(state, 0, (int)GoodId.Alloys) > 0);
        // grade capped by the owner's Industrial ceiling (slice G)
        Assert.True(BookOps.AskGrade(state, 0, (int)GoodId.Alloys)
                    <= Tech.Ceiling(state, port.OwnerActorId,
                                    TechDomain.Industrial));
    }

    [Fact]
    public void Processing_WithoutInputs_ProducesNothing()
    {
        var (state, port) = Fixture();
        Built(state, InfraTypeId.Foundry, port.Hex, port.OwnerActorId);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.Equal(0.0, BookOps.AskQty(state, 0, (int)GoodId.Machinery), 6);
    }

    [Fact]
    public void InputPurchases_PayTheInputSellers()
    {
        var (state, port) = Fixture();
        Built(state, InfraTypeId.Refinery, port.Hex, port.OwnerActorId);
        EpochTestKit.Stock(state, 0, (int)GoodId.Ore, 1000.0, 0.6,
            ownerActorId: 1);
        EpochTestKit.Stock(state, 0, (int)GoodId.Volatiles, 1000.0, 0.6,
            ownerActorId: 1);
        double buyerBefore = state.PolityOf(port.OwnerActorId).Credits;
        double sellerBefore = state.LedgerOf(1).Credits;
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        // input purchases pay the SELLERS at their asks (net of tax and
        // the labor share) — no anonymous pool (P4)
        double spent = buyerBefore - state.PolityOf(port.OwnerActorId).Credits;
        Assert.True(spent > 0);
        Assert.True(state.LedgerOf(1).Credits > sellerBefore,
            "the feedstock seller booked real revenue");
    }

    [Fact]
    public void UnderConstruction_ProducesNothing()
    {
        var (state, port) = Fixture();
        var candidate = new ConstructionCandidate((int)InfraTypeId.Mine,
            port.Hex, port.Id, Score: 1.0);
        ProjectOps.SpawnFacilityConstruction(state, port.OwnerActorId,
            port.OwnerActorId, candidate, ProjectPriority.Core, planOrder: 0);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.Equal(0.0, BookOps.AskQty(state, 0, (int)GoodId.Ore), 6);
    }

    [Fact]
    public void Condition_ScalesOutput()
    {
        var (a, portA) = Fixture();
        Built(a, InfraTypeId.Mine, portA.Hex, portA.OwnerActorId, condition: 1.0);
        var sa = new MarketStepScratch(a);
        MarketEngine.SupplyLands(a, sa);

        var (b, portB) = Fixture();
        Built(b, InfraTypeId.Mine, portB.Hex, portB.OwnerActorId, condition: 0.5);
        var sb = new MarketStepScratch(b);
        MarketEngine.SupplyLands(b, sb);

        Assert.Equal(BookOps.AskQty(a, 0, (int)GoodId.Ore) * 0.5,
                     BookOps.AskQty(b, 0, (int)GoodId.Ore), 10);
    }

    [Fact]
    public void Facility_AttachesToTheNearestSameOwnerPort()
    {
        var (state, port) = Fixture();
        // a second port of the same owner, far away
        var farHex = new HexCoordinate(port.Hex.Q + 40, port.Hex.R);
        var far = new Port(1, port.OwnerActorId, farHex, tier: 1, foundedYear: 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(1, state.Config.Economy));
        var nearHome = Built(state, InfraTypeId.Mine, port.Hex, port.OwnerActorId);
        var nearFar = Built(state, InfraTypeId.Mine, farHex, port.OwnerActorId);

        Assert.Equal(0, MarketEngine.AttachedMarketIndex(state, nearHome));
        Assert.Equal(1, MarketEngine.AttachedMarketIndex(state, nearFar));
    }

    [Fact]
    public void HomeworldEntry_SeedsStarterIndustryAndEndowment()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);

        foreach (var pr in state.Polities)
        {
            // retired polities (conquest, merger) lose their assets — the
            // seizure moves facility ownership with the territory
            if (!state.Actors[pr.ActorId].Entered
                || state.Actors[pr.ActorId].Retired) continue;
            // schedule entries only — schism states (slice G) inherit
            // existing industry, they don't found homeworlds
            if (!state.Log.Events.Any(e =>
                    e.Type == WorldEventType.PolityEmerged
                    && e.Actors.Contains(pr.ActorId))) continue;
            // a homeworld can be conquered while the polity survives in
            // exile — the starter industry moved with the territory
            if (!state.Ports.Any(p => p.OwnerActorId == pr.ActorId
                    && p.Hex.Equals(state.Actors[pr.ActorId].Seat))) continue;
            int starters = 0;
            foreach (var f in state.Facilities)
                if (f.OwnerActorId == pr.ActorId
                    && f.Hex.Equals(state.Actors[pr.ActorId].Seat))
                    starters++;
            Assert.True(starters >= 5,
                $"polity {pr.ActorId} has {starters} homeworld facilities");
        }
    }

    [Fact]
    public void Entry_MintsTheInitialCreditEndowmentOnce()
    {
        var (skeleton, seeded) = EpochTestKit.Seeded();
        var state = new SimState(seeded.Config, skeleton);
        var template = seeded.Actors[0];
        state.Actors.Add(new Actor(0, ActorKind.Polity, template.Name,
            template.Seat, entryEpoch: 0, new TrivialController()));
        state.Polities.Add(new PolityRecord(0, seeded.Polities[0].SpeciesId));

        new InteriorPhase().Run(state);

        Assert.Equal(state.Config.Economy.InitialCreditsPerPolity,
                     state.PolityOf(0).Credits);
    }
}
