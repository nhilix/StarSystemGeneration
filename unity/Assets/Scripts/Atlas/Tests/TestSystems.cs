using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    /// <summary>Hand-built model fixtures for orbit-diagram tests: fully specified
    /// so geometry assertions don't depend on the generator.</summary>
    public static class TestSystems
    {
        public static Body MakeBody(BodyKind kind, int size, Settlement settlement = Settlement.None)
            => new Body { Kind = kind, Size = size, Settlement = settlement };

        public static Star MakeStar(string typeId, int slotCount, int habStart, int habEnd,
                                    int? companionSlot = null)
        {
            var star = new Star { TypeId = typeId, TypeName = typeId, CompanionSlotIndex = companionSlot };
            for (int i = 0; i < slotCount; i++)
                star.Slots.Add(new OrbitSlot
                {
                    Index = i,
                    Band = i >= habStart && i <= habEnd ? OrbitBand.Habitable
                         : i < habStart ? OrbitBand.Inner : OrbitBand.Outer,
                });
            return star;
        }

        /// <summary>gold_main primary (8 slots, hab 3–4): rocky@1, belt@2, settled
        /// colony with two moons@3, gas giant@5; ember_dwarf companion (3 slots,
        /// hab 1, ice world@1) at primary slot 6; collapsed_core companion (1 slot,
        /// no hab band, empty) at primary slot 4. Primary slots 0 and 7 empty.</summary>
        public static StarSystem BuildTrinary()
        {
            var system = new StarSystem("SGC 2048-2048") { Arrangement = StarArrangement.Trinary };

            var primary = MakeStar("gold_main", 8, 3, 4);
            primary.Slots[1].Body = MakeBody(BodyKind.RockyWorld, 5);
            primary.Slots[2].Body = MakeBody(BodyKind.PlanetoidBelt, 0);
            var colony = MakeBody(BodyKind.RockyWorld, 7, Settlement.Colony);
            colony.Hydrographics = 60;
            colony.Satellites.Add(MakeBody(BodyKind.RockyWorld, 1));
            colony.Satellites.Add(MakeBody(BodyKind.IceWorld, 1));
            primary.Slots[3].Body = colony;
            primary.Slots[5].Body = MakeBody(BodyKind.GasGiant, 9);
            system.Stars.Add(primary);

            var emberCompanion = MakeStar("ember_dwarf", 3, 1, 1, companionSlot: 6);
            emberCompanion.Slots[1].Body = MakeBody(BodyKind.IceWorld, 3);
            system.Stars.Add(emberCompanion);

            system.Stars.Add(MakeStar("collapsed_core", 1, 99, 99, companionSlot: 4));
            return system;
        }
    }
}
