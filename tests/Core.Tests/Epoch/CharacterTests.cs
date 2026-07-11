using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 2: characters — sparse role occupants, deterministic
/// on-demand generation, species-real mortality, succession, dynasties,
/// notables, and the P8 biography (a life reconstructs from the log).</summary>
public class CharacterTests
{
    [Fact]
    public void Entry_SeatsACourt_RulerHeirWhereDynastic_Marshal()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var pr = state.Polities.First(p => p.Interior != null);
        var interior = pr.Interior!;
        Assert.True(interior.RulerCharacterId >= 0, "no ruler seated at entry");
        var ruler = state.Characters[interior.RulerCharacterId];
        Assert.Equal(CharacterRole.Ruler, ruler.Role);
        Assert.Equal(pr.ActorId, ruler.PolityId);
        Assert.False(string.IsNullOrEmpty(ruler.Name));
        Assert.True(ruler.BirthYear < state.WorldYear, "ruler born in the future");

        var form = GovernmentForms.Get(interior.FormId);
        bool dynastic = form.Succession is SuccessionRule.Dynastic
            or SuccessionRule.RareDesignation;
        bool hasHeir = state.Characters.Any(c => c.Alive
            && c.PolityId == pr.ActorId && c.Role == CharacterRole.Heir);
        Assert.Equal(dynastic, hasHeir);
        if (dynastic)
        {
            Assert.True(ruler.DynastyId >= 0, "dynastic ruler without a house");
            Assert.Equal(ruler.Id,
                state.Dynasties[ruler.DynastyId].FounderCharacterId);
        }
        // the first ascension chronicles — every reign has its anchor
        Assert.Contains(state.Log.Events, e =>
            e.Type == WorldEventType.RulerAscended
            && e.Payload is RulerAscendedPayload p && p.CharacterId == ruler.Id);
    }

    [Fact]
    public void FullRun_KeepsCharactersSparse_AndCourtsFilled()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        foreach (var pr in state.Polities)
        {
            if (pr.Interior == null) continue;
            var living = state.Characters
                .Where(c => c.Alive && c.PolityId == pr.ActorId).ToList();
            // "perhaps a dozen": court + commanders + notables + faction
            // leaders + corporate boardrooms (slice G task 7) at the top
            // end — and mergers (slice H) pool two realms' rosters
            Assert.InRange(living.Count, 1, 50);
            // the seat never sits empty
            var ruler = state.Characters[pr.Interior.RulerCharacterId];
            Assert.True(ruler.Alive, "a dead ruler holds the seat");
            Assert.Equal(CharacterRole.Ruler, ruler.Role);
        }
    }

    [Fact]
    public void Mortality_IsSpeciesReal()
    {
        var config = new EpochSimConfig();
        // hive minds never die; machines only deprecate; lithics outlive all
        Assert.Equal(0.0, CharacterOps.AgeHazardPerYear(config, Embodiment.Hive, 5000));
        Assert.Equal(config.Character.MachineDeprecationPerYear,
            CharacterOps.AgeHazardPerYear(config, Embodiment.Machine, 5000));
        double humanAt80 = CharacterOps.AgeHazardPerYear(
            config, Embodiment.TerranAnalog, 80);
        double lithicAt80 = CharacterOps.AgeHazardPerYear(
            config, Embodiment.Lithic, 80);
        Assert.Equal(config.Character.MortalityShapePerYear, humanAt80, 9);
        Assert.True(lithicAt80 < 0.001, "a lithic at 80 is a youth");
        // the curve rises with age
        Assert.True(CharacterOps.AgeHazardPerYear(config, Embodiment.TerranAnalog, 90)
                    > humanAt80);
    }

    [Fact]
    public void RulerDeath_TriggersSuccession_HeirAscends()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        // find a dynastic polity; make its ruler certain to die by config
        var pr = state.Polities.FirstOrDefault(p => p.Interior != null
            && GovernmentForms.Get(p.Interior.FormId).Succession
                is SuccessionRule.Dynastic or SuccessionRule.RareDesignation);
        if (pr == null) return;   // seed carries no dynastic form: nothing to test
        var oldRuler = state.Characters[pr.Interior!.RulerCharacterId];
        var heir = state.Characters.First(c => c.Alive
            && c.PolityId == pr.ActorId && c.Role == CharacterRole.Heir);
        state.Config.Character.MortalityShapePerYear = 500.0;   // everyone dies
        engine.Step(state);
        Assert.False(oldRuler.Alive);
        Assert.True(state.Characters[pr.Interior.RulerCharacterId].Alive);
        // the heir took the seat (they may have died the same epoch and been
        // succeeded again — the chain must at least run through them)
        Assert.True(heir.Role == CharacterRole.Ruler || !heir.Alive);
        Assert.Contains(state.Log.Events, e =>
            e.Type == WorldEventType.CharacterDied
            && e.Payload is CharacterDiedPayload p && p.CharacterId == oldRuler.Id);
    }

    [Fact]
    public void Commanders_FillNotableFleetSlots()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        var commanded = state.Fleets.Where(f => f.CommanderId >= 0).ToList();
        foreach (var fleet in commanded)
        {
            var c = state.Characters[fleet.CommanderId];
            Assert.Equal(CharacterRole.Commander, c.Role);
            Assert.Equal(fleet.Id, c.InstitutionId);
            Assert.Equal(fleet.OwnerActorId, c.PolityId);
            Assert.True(c.Alive, "a dead commander still holds a fleet");
        }
    }

    [Fact]
    public void Founders_MintOnColonies_CapHolds()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        // any polity with a colony port should have minted founders
        var colonies = state.Ports.Where(p =>
            !state.Actors[p.OwnerActorId].Seat.Equals(p.Hex)).ToList();
        if (colonies.Count > 0)
            Assert.Contains(state.Characters,
                c => c.Notable == NotableType.Founder);
        foreach (var pr in state.Polities)
        {
            if (pr.Interior == null) continue;
            // the cap governs notable-typed characters, whatever role they
            // hold; deposed nobodies (Notable role, no type) don't count.
            // The cap is a mint-time valve: a merger (slice H) legitimately
            // pools the parents' notables past it — no new ones mint until
            // deaths bring the union back under
            bool merged = state.Log.Events.Any(e =>
                (e.Type == WorldEventType.FederationFormed
                 && e.Payload is FederationFormedPayload f
                 && f.NewPolityId == pr.ActorId)
                || (e.Type == WorldEventType.VassalAbsorbed
                    && e.Payload is VassalAbsorbedPayload v
                    && v.OverlordPolityId == pr.ActorId));
            int notables = CharacterOps.NotableCount(state, pr.ActorId);
            if (!merged)
                Assert.True(notables <= state.Config.Character.MaxNotablesPerPolity,
                    $"polity {pr.ActorId} carries {notables} notables over the cap");
        }
    }

    [Fact]
    public void Biography_DerivesFromTheLog()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        // every character with any chronicle presence renders a coherent
        // life: events in year order, ascensions before deaths, prose clean
        int biographies = 0;
        foreach (var c in state.Characters)
        {
            var life = state.Log.ForCharacter(c.Id).ToList();
            if (life.Count == 0) continue;
            biographies++;
            long lastYear = long.MinValue;
            foreach (var e in life)
            {
                Assert.True(e.WorldYear >= lastYear, "biography out of order");
                lastYear = e.WorldYear;
                Assert.False(string.IsNullOrEmpty(SimTraceView.Describe(e)));
            }
            if (!c.Alive)
                // natural deaths chronicle as CharacterDied; martyrdom (a
                // crushed revolt) IS the death record
                Assert.Contains(life, e =>
                    e.Type is WorldEventType.CharacterDied
                    or WorldEventType.RevoltCrushed);
        }
        Assert.True(biographies > 0, "no character has any chronicle presence");
    }

    [Fact]
    public void CharactersAndDynasties_RoundTripThroughTheArtifact()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        Assert.True(state.Characters.Count > 0, "no characters after a run");
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Characters.Count, loaded.Characters.Count);
        Assert.Equal(state.Dynasties.Count, loaded.Dynasties.Count);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
