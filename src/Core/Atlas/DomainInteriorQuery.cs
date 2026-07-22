using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One satellite facility of a port's domain (domain-hex-expansion
/// design §6) — a working the port sells into, sitting away from the port hex.
/// Carries exactly what the `domain` view prints per facility: the resolved
/// catalog name, tier, active-vs-under-construction, condition, and the claimed
/// body (or None). Selection is data; the string shape stays with the
/// caller.</summary>
public sealed record DomainFacilityRow(
    int Id, string TypeName, int Tier, bool Active, double Condition, BodyRef Body);

/// <summary>A satellite hex of the domain and its facilities, hex sorted by
/// (Q then R), facilities within by id (P6 iteration order preserved).</summary>
public sealed record DomainSatelliteHex(
    HexCoordinate Hex, IReadOnlyList<DomainFacilityRow> Facilities);

/// <summary>One resident population segment of an outpost. The species-name
/// lookup is left to the caller (the query selects the id); size and SoL ride
/// along for the row.</summary>
public sealed record DomainResident(
    int SegmentId, int SpeciesId, double Size, double SoL);

/// <summary>Which candidacy state an outpost is in (design §4/§6) — the query
/// owns the branch selection that <c>DomainView.CandidacyLine</c> used to hold,
/// so the REPL and a future Unity renderer read ONE derivation. The caller maps
/// the kind to display text.
/// <list type="bullet">
/// <item><see cref="Graduated"/> — promoted into a real starport; history, no
/// longer a candidate. <see cref="DomainCandidacy.GraduatedPortId"/> resolves
/// the port it became (−1 if unresolved).</item>
/// <item><see cref="FrontierNoPort"/> — not graduated, no entered port anywhere
/// to clash with: vacuously frontier (Standing.Slack == int.MaxValue).</item>
/// <item><see cref="Frontier"/> — not graduated, clear of every port core by G:
/// candidacy-eligible. Read <see cref="DomainCandidacy.Standing"/>.</item>
/// <item><see cref="Interior"/> — not graduated, inside G of the binding port:
/// permanently subordinate. Read <see cref="DomainCandidacy.Standing"/>.</item>
/// </list></summary>
public enum DomainCandidacyKind { Graduated, FrontierNoPort, Frontier, Interior }

/// <summary>An outpost's candidacy standing, resolved by the query. For
/// <see cref="DomainCandidacyKind.Graduated"/> read <see cref="GraduatedPortId"/>
/// (the port at the outpost's hex, or −1 if none resolved); for
/// <see cref="DomainCandidacyKind.Frontier"/> / <see cref="DomainCandidacyKind.Interior"/>
/// read <see cref="Standing"/>.</summary>
public sealed record DomainCandidacy(
    DomainCandidacyKind Kind, int GraduatedPortId, FrontierStanding Standing);

/// <summary>One outpost of the domain (design §6) and everything the `domain`
/// view prints for it: identity, founding, graduated flag, resolved candidacy,
/// and resident segments in id order.</summary>
public sealed record DomainOutpostCard(
    int Id, string Name, HexCoordinate Hex, long FoundingYear, bool Graduated,
    DomainCandidacy Candidacy, IReadOnlyList<DomainResident> Residents);

/// <summary>The read-model for a port's domain interior — the single derivation
/// both the REPL `domain &lt;port&gt;` view and a future Unity domain layer read
/// (K3 parity rule: REPL and atlas never drift). Satellite workings, outposts
/// with candidacy + residents, and the selected domain events (formatting is the
/// caller's: <see cref="Events"/> are raw <see cref="WorldEvent"/>s in log
/// order).</summary>
public sealed record DomainInteriorCard(
    int PortId, int Tier, HexCoordinate Hex, int OwnerActorId, string OwnerName,
    int FoundedYear,
    IReadOnlyList<DomainSatelliteHex> SatelliteHexes,
    IReadOnlyList<DomainOutpostCard> Outposts,
    IReadOnlyList<WorldEvent> Events);

/// <summary>The domain-interior read-model query (Slice AC): the selection and
/// derivation the Inspector's <c>DomainView</c> once held privately, lifted into
/// Core.Atlas so the atlas presentation reads the SAME derivation as the REPL.
/// Pure over a loaded SimState — no rolls, no mutation, P6 iteration order
/// preserved (facilities/outposts/segments in id order, satellite hexes sorted
/// by Q then R, events in log order). Zero sim behaviour: it only reads.</summary>
public static class DomainInteriorQuery
{
    /// <summary>The domain interior for a port by id, or <c>null</c> when the id
    /// is out of range (the caller renders the "no port" message with the
    /// registry bound).</summary>
    public static DomainInteriorCard? Card(AtlasReadModel model, EyeContext eye,
                                           int portId)
    {
        var state = model.State;
        if (portId < 0 || portId >= state.Ports.Count) return null;
        var port = state.Ports[portId];
        var owner = state.Actors[port.OwnerActorId];

        // ---- satellite hexes: this port's facilities away from the port hex,
        // grouped by hex (design §6). Facilities scanned in id order (P6), hexes
        // then sorted by (Q then R), facilities within a hex by id.
        var byHex = new Dictionary<HexCoordinate, List<Facility>>();
        foreach (var f in state.Facilities)                 // id order (P6)
        {
            if (MarketEngine.AttachedMarketIndex(state, f) != portId) continue;
            if (f.Hex.Equals(port.Hex)) continue;            // the port hex itself
            if (!byHex.TryGetValue(f.Hex, out var list))
                byHex[f.Hex] = list = new List<Facility>();
            list.Add(f);
        }
        var hexKeys = new List<HexCoordinate>(byHex.Keys);
        hexKeys.Sort((a, b) => a.Q != b.Q ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
        var satellites = new List<DomainSatelliteHex>(hexKeys.Count);
        foreach (var hex in hexKeys)
        {
            var list = byHex[hex];
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            var rows = new List<DomainFacilityRow>(list.Count);
            foreach (var f in list)
            {
                var def = Infrastructure.Get((InfraTypeId)f.TypeId);
                rows.Add(new DomainFacilityRow(f.Id, def.Name, f.Tier,
                    MarketEngine.IsActive(state, f), f.Condition, f.Body));
            }
            satellites.Add(new DomainSatelliteHex(hex, rows));
        }

        // ---- outposts: settlements this port's domain founded (design §6).
        var outposts = new List<DomainOutpostCard>();
        foreach (var o in state.Outposts)                   // id order (P6)
        {
            if (o.ParentPortId != portId) continue;
            var candidacy = Candidacy(state, o);
            var residents = new List<DomainResident>();
            foreach (var s in state.Segments)               // id order (P6)
            {
                if (s.PortId != portId || !s.Hex.Equals(o.Hex)
                    || s.Size <= 0.001) continue;
                residents.Add(new DomainResident(s.Id, s.SpeciesId, s.Size, s.SoL));
            }
            outposts.Add(new DomainOutpostCard(o.Id, o.Name, o.Hex, o.FoundingYear,
                o.Graduated, candidacy, residents));
        }

        // ---- events: settles (OutpostFounded whose outpost is in this domain)
        // and graduations (PortEstablished at a graduated outpost's hex), in log
        // order (P6). SAME selection as DomainView's events section; the caller
        // formats each WorldEvent with SimTraceView.Describe.
        var events = new List<WorldEvent>();
        foreach (var e in state.Log.Events)                 // log order (P6)
        {
            if (e.Type == WorldEventType.OutpostFounded
                && e.Payload is OutpostFoundedPayload op
                && op.OutpostId >= 0 && op.OutpostId < state.Outposts.Count
                && state.Outposts[op.OutpostId].ParentPortId == portId)
            {
                events.Add(e);
                continue;
            }
            if (e.Type != WorldEventType.PortEstablished) continue;
            foreach (var o in state.Outposts)               // id order (P6)
                if (o.ParentPortId == portId && o.Graduated
                    && o.Hex.Equals(e.Location))
                {
                    events.Add(e);
                    break;
                }
        }

        return new DomainInteriorCard(portId, port.Tier, port.Hex,
            port.OwnerActorId, owner.Name, port.FoundedYear,
            satellites, outposts, events);
    }

    /// <summary>Resolve an outpost's candidacy (design §4/§6) — the branch
    /// selection that used to live in <c>DomainView.CandidacyLine</c>: a
    /// graduated outpost resolves to the port at its hex; a live one reads its
    /// standing straight off <see cref="OutpostOps.FrontierStatus"/>, with the
    /// no-entered-port case flagged so the caller can render "no entered port
    /// yet" without inspecting Slack.</summary>
    private static DomainCandidacy Candidacy(SimState state, Outpost o)
    {
        if (o.Graduated)
        {
            int graduatedPortId = -1;
            foreach (var p in state.Ports)                  // id order (P6)
                if (p.Hex.Equals(o.Hex)) { graduatedPortId = p.Id; break; }
            return new DomainCandidacy(DomainCandidacyKind.Graduated,
                graduatedPortId, default);
        }
        var standing = OutpostOps.FrontierStatus(state, o);
        // no entered port anywhere to clash with — vacuously frontier
        // (FrontierStatus's documented Slack == int.MaxValue case).
        if (standing.Slack == int.MaxValue)
            return new DomainCandidacy(DomainCandidacyKind.FrontierNoPort, -1,
                standing);
        return new DomainCandidacy(
            standing.IsFrontier ? DomainCandidacyKind.Frontier
                                : DomainCandidacyKind.Interior,
            -1, standing);
    }
}
