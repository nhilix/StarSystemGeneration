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
        // extraction now roots in a specific body: decide it and roll the
        // depletable stock the same way groundbreaking does, so a Mine built
        // through this fixture actually has a rock to dig (body-resource-stock).
        f.Body = ProjectOps.PlaceFacilityBody(state, hex, type);
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

    /// <summary>Task 6b: a CORP-owned processing facility fronts its recipe
    /// inputs from its own wallet and has no overdraft — when the wallet cannot
    /// cover the full run the production is bounded to what it can pay, so the
    /// input sellers (settled in full inside LiftAsks) are never paid past the
    /// corp's holdings while its capped debit keeps the shortfall (which minted
    /// money before the fix). Single active currency (rate 1.0) so face value IS
    /// numeraire and conservation is exact.</summary>
    [Fact]
    public void CorpProcessing_CappedByWallet_ConservesCredits()
    {
        var (state, port) = Fixture();
        var cur = new Currency(0, "C0", foundingPolityId: 0)
        { NumeraireRate = 1.0 };
        state.Currencies.Add(cur);
        state.PolityOf(port.OwnerActorId).CurrencyId = 0;
        // a corp that owns the refinery, funded with a wallet far too thin to
        // buy the whole input run
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, "Refco",
            port.Hex, 0, new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(0, actorId, "Refco", port.OwnerActorId,
            CorporateNiche.Fabrication, port.Id, 0);
        state.Corporations.Add(corp);
        corp.Deposit(state, 8.0, 0);          // 8 C0 wallet
        Built(state, InfraTypeId.Refinery, port.Hex, actorId);
        EpochTestKit.Stock(state, 0, (int)GoodId.Ore, 1000.0, 0.6);
        EpochTestKit.Stock(state, 0, (int)GoodId.Volatiles, 1000.0, 0.6);

        double before = Total(state);
        MarketEngine.SupplyLands(state, new MarketStepScratch(state));
        double after = Total(state);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Alloys) > 0,
            "the corp should still produce something within its means");
        Assert.True(corp.Credits <= 0.5,
            "the input run should have drained the tiny wallet");
        Assert.True(corp.Credits >= -1e-9, "the corp must never overdraft");
        Assert.Equal(before, after, System.Math.Max(1.0, before) * 1e-9);
    }

    /// <summary>Face-value credit total across every holder the market step can
    /// move money between (single-currency, rate 1.0, so face IS numeraire).</summary>
    private static double Total(SimState state)
    {
        double t = 0;
        foreach (var pr in state.Polities)
            t += pr.Credits + pr.ExpansionPoints + pr.DevelopmentPoints
                 + pr.MilitaryPoints + pr.ReservePoints;
        foreach (var s in state.Segments) t += s.Wealth;
        foreach (var f in state.Factions) t += f.Wealth;
        foreach (var o in state.Orders) t += o.EscrowCredits;
        foreach (var c in state.Corporations) t += c.Credits;
        return t;
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
            // a homeworld's industry can also be seized piecemeal by creditors:
            // ServiceLoans transfers a defaulted borrower's facilities to the
            // lender (economy/markets.md §Credit). That's a legitimate history
            // outcome, not a seeding failure — this test asserts seeding, so
            // skip a polity whose seat industry was foreclosed. (Slice ME made
            // the credit loop breathe, so more borrowers reach — and survive —
            // default than in the pre-mechanism equilibrium this test froze.)
            if (state.Log.Events.Any(e => e.Type == WorldEventType.LoanDefaulted
                    && e.Payload is LoanDefaultedPayload d
                    && d.BorrowerActorId == pr.ActorId)) continue;
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
            template.Seat, entryYear: 0, new TrivialController()));
        state.Polities.Add(new PolityRecord(0, seeded.Polities[0].SpeciesId));

        new InteriorPhase().Run(state);

        Assert.Equal(state.Config.Economy.InitialCreditsPerPolity,
                     state.PolityOf(0).Credits);
    }

    [Fact]
    public void Extraction_DrawsFromTheBodyStock_CappedByWhatRemains()
    {
        var (state, port) = Fixture();
        var mine = Built(state, InfraTypeId.Mine, port.Hex, port.OwnerActorId);
        // shrink the body's stock to a tiny remainder: the mine can post no
        // more than what the rock has left this step, then it is dry
        state.BodyResources[(mine.Hex, mine.Body)] =
            new StarGen.Core.Substrate.Stock(GoodId.Ore, 3.0, 0.6);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.Equal(3.0, BookOps.AskQty(state, 0, (int)GoodId.Ore), 6);
        Assert.Equal(0.0,
            state.BodyResources[(mine.Hex, mine.Body)].Quantity, 9);
    }

    [Fact]
    public void Skimmer_ProducesFromItsGiant_WithoutRollingAStock()
    {
        var (state, port) = Fixture();
        // pre-seed the port hex's system with a gas giant so the Skimmer has a
        // real body to draw a renewable yield from
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.GasGiant, Size = 13 } });
        sys.Stars.Add(s0);
        state.SettledSystems[port.Hex] = sys;
        var skimmer = Built(state, InfraTypeId.Skimmer, port.Hex,
                            port.OwnerActorId);
        var scratch = new MarketStepScratch(state);

        MarketEngine.SupplyLands(state, scratch);

        Assert.True(BookOps.AskQty(state, 0, (int)GoodId.Volatiles) > 0);
        // renewable: no stock entry was ever created for a Skimmer
        Assert.False(state.BodyResources.ContainsKey((skimmer.Hex, skimmer.Body)));
    }
}
