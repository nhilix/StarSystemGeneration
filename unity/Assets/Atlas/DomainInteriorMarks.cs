using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Model;

namespace StarGen.AtlasView
{
    /// <summary>A worked (satellite) hex of a domain — a hex the parent port
    /// farms with facilities, sitting away from the port hex. The subordinate
    /// "inhabited" read AC1.3 draws under the port marks; owner-tinted, no
    /// identity of its own beyond its domain.</summary>
    public readonly struct WorkedHexMark
    {
        public readonly int ParentPortId;
        public readonly HexCoordinate Hex;
        public readonly int OwnerActorId;
        public readonly int FacilityCount;

        public WorkedHexMark(int parentPortId, HexCoordinate hex,
                             int ownerActorId, int facilityCount)
        {
            ParentPortId = parentPortId;
            Hex = hex;
            OwnerActorId = ownerActorId;
            FacilityCount = facilityCount;
        }
    }

    /// <summary>One live outpost as the atlas draws it (AC1.4) — a named mark
    /// in the port-mark family, subordinate to a real port. Carries exactly
    /// what the mark, its hover tooltip and its selection need; the panel reads
    /// the full <see cref="DomainInteriorQuery"/> card for the deep detail.</summary>
    public readonly struct OutpostMark
    {
        public readonly int OutpostId;
        public readonly int ParentPortId;
        public readonly HexCoordinate Hex;
        public readonly int OwnerActorId;
        public readonly string Name;
        public readonly DomainCandidacyKind Candidacy;

        public OutpostMark(int outpostId, int parentPortId, HexCoordinate hex,
                           int ownerActorId, string name,
                           DomainCandidacyKind candidacy)
        {
            OutpostId = outpostId;
            ParentPortId = parentPortId;
            Hex = hex;
            OwnerActorId = ownerActorId;
            Name = name;
            Candidacy = candidacy;
        }
    }

    /// <summary>Both interior mark sets for a domain (or the whole galaxy):
    /// worked-satellite hexes and outposts. Parallel to how PortLens.Markers
    /// hands the port layer its dots — the layers only translate these into
    /// billboards, so the derivation stays pure and xUnit/EditMode-coverable.</summary>
    public readonly struct DomainMarkSet
    {
        public readonly IReadOnlyList<WorkedHexMark> Worked;
        public readonly IReadOnlyList<OutpostMark> Outposts;

        public DomainMarkSet(IReadOnlyList<WorkedHexMark> worked,
                             IReadOnlyList<OutpostMark> outposts)
        {
            Worked = worked;
            Outposts = outposts;
        }
    }

    /// <summary>The domain-interior marks the atlas draws over the flat domain
    /// glow (Slice AC): worked-satellite hexes (AC1.3) and outpost marks
    /// (AC1.4), both derived from the single read-only
    /// <see cref="DomainInteriorQuery"/> so the map and the REPL never drift.
    /// Pure over a loaded model — no rolls, no mutation. Graduated outposts are
    /// omitted: a graduated outpost has become a real port, so its hex already
    /// carries a port dot (a second mark there would only camouflage it). An
    /// outpost hex is drawn as an outpost, never also as a worked glyph, so
    /// every inhabited hex reads with exactly one mark.</summary>
    public static class DomainInteriorMarks
    {
        /// <summary>The interior marks for one port's domain (or empty when the
        /// port has no live outposts and no off-port workings).</summary>
        public static DomainMarkSet ForPort(AtlasReadModel model,
                                            EyeContext eye, int portId)
        {
            var worked = new List<WorkedHexMark>();
            var outposts = new List<OutpostMark>();
            Collect(model, eye, portId, worked, outposts);
            return new DomainMarkSet(worked, outposts);
        }

        /// <summary>Every live domain's interior marks, ports enumerated in id
        /// order (P6) via PortLens so the atlas iteration matches the port
        /// layer's. MaxPorts guards the shader's registry cap parity.</summary>
        public static DomainMarkSet Build(AtlasReadModel model, EyeContext eye)
        {
            var worked = new List<WorkedHexMark>();
            var outposts = new List<OutpostMark>();
            var ports = model.State.Ports;
            int count = System.Math.Min(ports.Count, 512);   // DomainField MaxPorts
            for (int i = 0; i < count; i++)
                Collect(model, eye, ports[i].Id, worked, outposts);
            return new DomainMarkSet(worked, outposts);
        }

        private static void Collect(AtlasReadModel model, EyeContext eye,
            int portId, List<WorkedHexMark> worked, List<OutpostMark> outposts)
        {
            var card = DomainInteriorQuery.Card(model, eye, portId);
            if (card == null) return;

            // Outpost hexes take the outpost mark; a worked glyph there would
            // double up. Track them so the worked pass can skip them.
            var outpostHexes = new HashSet<HexCoordinate>();
            foreach (var o in card.Outposts)
            {
                if (o.Graduated) continue;   // it is a port now — the port dot speaks
                outpostHexes.Add(o.Hex);
                outposts.Add(new OutpostMark(o.Id, card.PortId, o.Hex,
                    card.OwnerActorId, o.Name, o.Candidacy.Kind));
            }
            foreach (var sat in card.SatelliteHexes)
            {
                if (outpostHexes.Contains(sat.Hex)) continue;
                worked.Add(new WorkedHexMark(card.PortId, sat.Hex,
                    card.OwnerActorId, sat.Facilities.Count));
            }
        }

        /// <summary>The short candidacy read for an outpost tooltip (design
        /// §4/§6): the atlas's four-word gloss over the query's branch.</summary>
        public static string CandidacyText(DomainCandidacyKind kind) => kind switch
        {
            DomainCandidacyKind.Graduated => "graduated",
            DomainCandidacyKind.Frontier => "frontier — eligible",
            DomainCandidacyKind.FrontierNoPort => "frontier — eligible",
            _ => "interior — subordinate",
        };
    }
}
