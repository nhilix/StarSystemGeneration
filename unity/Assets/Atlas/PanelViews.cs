using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Substrate;
using UnityEngine.UIElements;
using static StarGen.AtlasView.DockKit;

namespace StarGen.AtlasView
{
    /// <summary>Typed panel bodies (K3): each builder is a view over one
    /// T1 query — the REPL's panels made visual. No derivations here;
    /// Core owns every number (the parity contract).</summary>
    public static class PanelViews
    {
        public static (string Title, VisualElement Body) Build(
            PanelRequest request, PanelContext ctx)
        {
            var body = new VisualElement();
            switch (request.Type)
            {
                case PanelType.Hex: return Hex(request, ctx, body);
                case PanelType.Polity: return Polity(request, ctx, body);
                case PanelType.Market: return Market(request, ctx, body);
                case PanelType.Project: return Project(request, ctx, body);
                case PanelType.Shipment: return Shipment(request, ctx, body);
                case PanelType.Fleet: return Fleet(request, ctx, body);
                case PanelType.Designs: return Designs(request, ctx, body);
                case PanelType.Wars: return Wars(ctx, body);
                case PanelType.War: return War(request, ctx, body);
                case PanelType.Relations: return Relations(request, ctx, body);
                case PanelType.Character: return Character(request, ctx, body);
                case PanelType.Corporations: return Corporations(request, ctx, body);
                case PanelType.Poi: return Poi(request, ctx, body);
                case PanelType.Beliefs: return Beliefs(request, ctx, body);
                case PanelType.News: return News(request, ctx, body);
                case PanelType.Stances: return Stances(request, ctx, body);
                case PanelType.Chronicle: return Chronicle(request, ctx, body);
                case PanelType.ChroniclePlace: return ChroniclePlace(request, ctx, body);
                case PanelType.Eras: return Eras(ctx, body);
                case PanelType.Threads: return Threads(ctx, body);
                case PanelType.Contracts: return Contracts(ctx, body);
                case PanelType.Find: return Find(request, ctx, body);
                case PanelType.Goods: return Goods(ctx, body);
                case PanelType.Knobs: return Knobs(request, ctx, body);
                case PanelType.Stats: return Stats(ctx, body);
                case PanelType.Facility: return Facility(request, ctx, body);
                case PanelType.System: return SystemCard(request, ctx, body);
                default: return (null, null);
            }
        }

        // ---- the opening screen ----

        private static (string, VisualElement) Threads(PanelContext ctx,
                                                       VisualElement body)
        {
            var rows = HandoffQueries.ThreadRows(ctx.Model, ctx.Eye);
            Line(body, Inv($"the world in motion — {rows.Count} open ")
                + (rows.Count == 1 ? "thread" : "threads")
                + Inv($" at y{ctx.Model.State.WorldYear}"), dim: true);
            if (rows.Count == 0)
                Line(body, "(a tidied museum — nothing is in motion; this"
                    + " should worry you more than a war)");
            foreach (var thread in rows)
            {
                var captured = thread;
                var row = Row(body, () =>
                {
                    if (captured.JumpHex != null)
                        ctx.JumpTo(captured.JumpHex.Value);
                });
                Tag(row, thread.Kind, thread.Kind switch
                {
                    "war" => "bad",
                    "plague" => "bad",
                    "tension" => "warn",
                    "quarantine" => "warn",
                    _ => null,
                });
                var text = Line(row, thread.Text);
                text.style.flexShrink = 1f;
            }
            return ("OPEN THREADS", body);
        }

        /// <summary>The courier job board (AC2.5, `econtracts` parity):
        /// open + in-transit contracts — route (by owner name, ports have
        /// none of their own), cargo, fee, and fulfiller once accepted.
        /// WAR priority gets the same red tag STALLED wears elsewhere — a
        /// war convoy jumping the queue is exactly that kind of fact.
        /// A row opens the DESTINATION port's Market (the ShipmentPanel
        /// row-click idiom) — where the delivery lands.</summary>
        private static (string, VisualElement) Contracts(PanelContext ctx,
                                                          VisualElement body)
        {
            var rows = ContractsPanel.Rows(ctx.Model, ctx.Eye);
            Line(body, Inv($"the courier board — {rows.Count} open ")
                + (rows.Count == 1 ? "contract" : "contracts"), dim: true);
            if (rows.Count == 0)
                Line(body, "(a quiet board — nothing posted)", dim: true);
            foreach (var c in rows)
            {
                var captured = c;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Market, captured.DestPortId)));
                if (captured.Priority == Core.Epoch.CourierPriority.War)
                    Tag(row, "WAR", "bad");
                var cargo = new List<string>();
                foreach (var line in captured.Cargo)
                {
                    if (cargo.Count >= 3) break;
                    cargo.Add(Inv($"{line.Qty:0.#} {line.GoodName}"));
                }
                string status = captured.Status == Core.Epoch.CourierStatus.Open
                    ? "OPEN"
                    : Inv($"in transit ({captured.FulfillerName})");
                var text = Line(row, Inv($"#{captured.Id} ")
                    + captured.OriginPortOwnerName + " → "
                    + captured.DestPortOwnerName + " · "
                    + string.Join(", ", cargo)
                    + Inv($" · fee {captured.FeeEscrow:0.0} · ") + status
                    + " (" + captured.PosterName + ")");
                text.style.flexShrink = 1f;
            }
            return ("CONTRACTS", body);
        }

        // ---- selection panels ----

        /// <summary>The system inside a hex (K5 eyeball wave): the orbit
        /// view's info panel — stars, every orbit row, and the epoch
        /// overlays as links. One SystemQuery, no derivations here.</summary>
        private static (string, VisualElement) SystemCard(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var info = StarGen.Core.Atlas.SystemQuery.At(ctx.Model, ctx.Eye,
                                                         request.Hex);
            if (!info.HasSystem)
            {
                Line(body, "empty reach — the wilds between systems",
                     dim: true);
                Kv(body, "hex", Inv($"({request.Hex.Q},{request.Hex.R})"));
                return ("EMPTY REACH", body);
            }
            if (info.GivenName != null) Line(body, "“" + info.GivenName + "”");
            Kv(body, "hex", Inv($"({request.Hex.Q},{request.Hex.R})"));
            Kv(body, "arrangement",
               info.Arrangement.ToString().ToLowerInvariant());
            if (info.OverlayId != null) Kv(body, "overlay", info.OverlayId, "acc");
            foreach (var tag in info.Tags) Tag(body, tag);

            Sect(body, "stars");
            foreach (var star in info.Stars)
                Kv(body, ((char)('A' + star.Index)).ToString(),
                   star.TypeName + " · "
                   + star.Age.ToString().ToLowerInvariant()
                   + (star.CompanionSlotIndex is int c
                       ? Inv($" · rides slot {c}") : ""));

            Sect(body, "orbits");
            if (info.Orbits.Count == 0)
                Line(body, "every slot rolled empty", dim: true);
            foreach (var orbit in info.Orbits)
            {
                string kind = orbit.Kind switch
                {
                    Core.Model.BodyKind.RockyWorld => "rocky world",
                    Core.Model.BodyKind.IceWorld => "ice world",
                    Core.Model.BodyKind.GasGiant => "gas giant",
                    Core.Model.BodyKind.PlanetoidBelt => "planetoid belt",
                    Core.Model.BodyKind.Wreckage => "wreckage field",
                    _ => orbit.Kind.ToString().ToLowerInvariant(),
                };
                string band = orbit.Band.ToString().ToLowerInvariant();
                string name = orbit.Name != null ? orbit.Name + " — " : "";
                string moons = orbit.SatelliteCount switch
                {
                    0 => "",
                    1 => " · 1 moon",
                    _ => Inv($" · {orbit.SatelliteCount} moons"),
                };
                string settled = orbit.Settlement != Core.Model.Settlement.None
                    ? " · " + orbit.Settlement.ToString().ToLowerInvariant()
                    : "";
                Kv(body,
                   Inv($"{(char)('A' + orbit.StarIndex)}{orbit.SlotIndex}·{band}"),
                   name + kind + Inv($" s{orbit.Size}") + moons + settled,
                   orbit.Settlement != Core.Model.Settlement.None ? "acc" : null);
            }

            if (info.PortId >= 0)
            {
                Sect(body, "port");
                int portId = info.PortId;
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Market, portId)));
                Line(row, Inv($"port #{info.PortId} · tier {info.PortTier} · ")
                    + info.PortOwnerName);
            }
            if (info.Facilities.Count > 0)
            {
                Sect(body, "facilities");
                foreach (var f in info.Facilities)
                {
                    var captured = f;
                    var row = Row(body, () => ctx.Open(
                        new PanelRequest(PanelType.Facility, captured.Id)));
                    Line(row, f.TypeName.ToLowerInvariant()
                        + Inv($" t{f.Tier} · {f.OwnerName}"));
                }
            }
            if (info.Sites.Count > 0)
            {
                Sect(body, "under construction");
                foreach (var s in info.Sites)
                {
                    var captured = s;
                    var row = Row(body, () => ctx.Open(
                        new PanelRequest(PanelType.Project, captured.ProjectId)));
                    Line(row, s.TypeName.ToLowerInvariant()
                        + Inv($" ({s.Progress:0%})"));
                }
            }
            return (info.Designation.ToUpperInvariant(), body);
        }

        private static (string, VisualElement) Hex(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var info = HexQuery.At(ctx.Model, ctx.Eye, request.Hex);
            var system = Row(body, () => ctx.Open(
                new PanelRequest(PanelType.System, hex: request.Hex)));
            Line(system, info.SystemSummary);
            if (info.OwnerNames.Count == 0) Line(body, "the wilds", dim: true);
            for (int i = 0; i < info.OwnerNames.Count; i++)
            {
                int actorId = info.OwnerActorIds[i];
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Polity, actorId)));
                Line(row, "domain of " + info.OwnerNames[i]);
            }
            foreach (var poi in info.LivePois)
            {
                var captured = poi;
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Poi, captured.Id)));
                Line(row, poi.TypeName + (poi.Dormant ? " · DORMANT" : ""));
            }
            Sect(body, "what happened here");
            var lines = ChronicleQueries.AtPlace(ctx.Model, ctx.Eye,
                                                 request.Hex);
            if (lines.Count == 0) Line(body, "(no events)", dim: true);
            foreach (var line in Tail(lines, 20))
                Line(body, line.Text, dim: true);
            return (Inv($"HEX ({request.Hex.Q},{request.Hex.R})"), body);
        }

        private static (string, VisualElement) Polity(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = PolityPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null) return ("POLITY", Missing(body, "no such polity"));
            Line(body, card.Name + (card.Entered ? "" : "  [not yet entered]"));
            Kv(body, "seat", Inv($"({card.Seat.Q},{card.Seat.R})"));
            if (card.FormName != null)
            {
                Kv(body, "form", card.FormName);
                Meter(body, "legitimacy", card.Legitimacy);
                Meter(body, "cohesion", card.Cohesion);
                Kv(body, "enforcement", Inv($"{card.Enforcement:0.00}"));
                Kv(body, "official line",
                    Inv($"authority {card.OfficialLine[0]:0.00} · communal {card.OfficialLine[1]:0.00}"));
                Kv(body, " ",
                    Inv($"open {card.OfficialLine[2]:0.00} · sacral {card.OfficialLine[3]:0.00}"));
            }
            if (card.Ruler != null)
            {
                var r = card.Ruler;
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Character, r.CharacterId)));
                Line(row, "ruler: " + r.Name
                    + (r.HouseName != null ? Inv($" of house {r.HouseName}") : "")
                    + Inv($" — since y{r.ReignFromYear}, age {r.Age}"));
            }
            foreach (var c in card.Court)
            {
                var captured = c;
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Character, captured.CharacterId)));
                Line(row, Inv($"{c.Role.ToString().ToLowerInvariant()}: {c.Name}, age {c.Age}"));
            }

            Sect(body, "treasury");
            // negative is a real state: development is deficit-financed
            // through downturns (AllocationPhase) — read as debt, not glitch
            Kv(body, "credits", Inv($"{card.Credits:0}")
                + (card.Credits < 0 ? "  (in deficit — credit-financed)" : ""),
               card.Credits < 0 ? "warn" : null);
            Kv(body, "reserve points", Inv($"{card.ReservePoints:0.0}"), "acc");

            Sect(body, "tech");
            foreach (var t in card.Tech)
                Kv(body, t.DomainName,
                   Inv($"t{t.Tier} ({t.ProgressFraction:00%})"));

            Sect(body, "factions");
            if (card.Factions.Count == 0)
                Line(body, "none organized", dim: true);
            foreach (var f in card.Factions)
            {
                Line(body, Inv($"{f.Name} ({f.Basis.ToString().ToLowerInvariant()}) — led by {f.LeaderName}"));
                Meter(body, "strength", f.Strength);
                Meter(body, "grievance", System.Math.Min(1, f.Grievance),
                      Inv($"{f.Grievance:0.00}"),
                      f.Grievance >= 0.7 ? "warn" : null);
                Line(body, Inv($"militancy {f.Militancy:0.00} · chest {f.Wealth:0}"), dim: true);
            }

            Sect(body, "charters");
            if (card.Charters.Count == 0) Line(body, "none", dim: true);
            foreach (var charter in card.Charters)
            {
                var captured = charter;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Corporations, captured.CorpId)));
                Line(row, Inv($"the {charter.Name} ({charter.Niche.ToString().ToLowerInvariant()}, {charter.Credits:0} credits)"));
            }

            Sect(body, "standing plan");
            if (card.Plan.Count == 0) Line(body, "no plan entries", dim: true);
            foreach (var e in card.Plan)
                Line(body, (e.InFlight ? "* " : "  ")
                    + Inv($"{e.Kind.ToString().ToLowerInvariant(),-9} {e.TypeDesign} — port #{e.PortId}, {PriorityName(e.Priority)}, y{e.StartYear}"));

            var links = new VisualElement
            { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            Link(links, "RELATIONS", () => ctx.Open(
                new PanelRequest(PanelType.Relations, card.ActorId)));
            Link(links, "BELIEFS", () => ctx.Open(
                new PanelRequest(PanelType.Beliefs, card.ActorId)));
            Link(links, "STANCES", () => ctx.Open(
                new PanelRequest(PanelType.Stances, card.ActorId)));
            Link(links, "CHRONICLE", () => ctx.Open(
                new PanelRequest(PanelType.Chronicle, card.ActorId)));
            body.Add(links);
            return (Inv($"POLITY #{card.ActorId}"), body);
        }

        private static (string, VisualElement) Market(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = MarketPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null) return ("MARKET", Missing(body, "no such port"));
            Line(body, Inv($"tier {card.Tier} port at ({card.Hex.Q},{card.Hex.R})"));
            var ownerRow = Row(body, () => ctx.Open(new PanelRequest(
                PanelType.Polity, card.OwnerActorId)));
            Line(ownerRow, Inv($"{card.OwnerName}'s domain — founded y{card.FoundedYear}"));

            // AC1.4: a selected outpost rides its parent port's economic panel
            // as a leading section — it trades through this market, it is not a
            // market of its own. Keyed by SubId; a plain port click (SubId −1)
            // renders nothing here.
            if (request.SubId >= 0)
                OutpostSection(body, ctx, request.Id, request.SubId);

            Sect(body, "the larder");
            Kv(body, "stock capacity / good",
               Inv($"{card.StockCapacity:0.#}"), "acc");
            bool anyStock = false;
            foreach (var g in card.Goods)
            {
                if (g.StockQty <= 0) continue;
                anyStock = true;
                Kv(body, g.GoodName,
                   Inv($"{g.StockQty:0.#} @ {g.StockGrade:0.00} · rots {g.StockDecayPerYear:0.###}/y"),
                   g.StockQty >= card.StockCapacity * 0.9 ? "warn" : null);
            }
            if (!anyStock) Line(body, "(nothing banked)", dim: true);

            Sect(body, "market");
            Line(body, "good · price · inv · grade · cleared · black book",
                 dim: true);
            foreach (var g in card.Goods)
            {
                string grade = g.Inventory > 0
                    ? Inv($"{g.GradeBand.ToString().ToLowerInvariant()} {g.Grade:0.00}")
                    : "-";
                string black = g.BlackBookDemand > 0
                    ? Inv($"{g.BlackBookDemand:0.#} @ {g.BlackBookPrice:0.00}")
                    : "-";
                Line(body, Inv($"{g.GoodName,-14} {g.Price,6:0.00} {g.Inventory,7:0.#} {grade,-11} {g.LastCleared,6:0.#}  {black}"));
            }

            // AC2.4: the resting book, `ebook` parity — reads the SAME
            // per-good Asks/Bids the "market" summary above rolls up (its
            // Inventory/Grade columns ARE this book's ask side), now at
            // order granularity: owner, qty, grade, limit vs reference.
            Sect(body, "order book");
            BookSection(body, card.Goods);

            Sect(body, "segments");
            if (card.Segments.Count == 0) Line(body, "(unpeopled)", dim: true);
            foreach (var s in card.Segments)
                Line(body, Inv($"{s.CultureName} — size {s.Size:0.00}, SoL {s.SoL:0.00}, wealth {s.Wealth:0.0}, subsistence {s.LastSubsistence:0.00}"));

            Sect(body, "facilities");
            if (card.Facilities.Count == 0) Line(body, "(none)", dim: true);
            foreach (var f in card.Facilities)
                Line(body, Inv($"#{f.Id} {f.TypeName} t{f.Tier} — ")
                    + (f.Active ? Inv($"condition {f.Condition:0.00}")
                                : "under construction"));

            Sect(body, "lanes to");
            if (card.Lanes.Count == 0) Line(body, "(none)", dim: true);
            foreach (var lane in card.Lanes)
            {
                var captured = lane;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Market, captured.OtherPortId)));
                if (lane.Cut) Tag(row, "CUT", "bad");
                Line(row, Inv($"port #{lane.OtherPortId} (lane #{lane.LaneId})"));
            }
            return (Inv($"MARKET #{card.PortId}"), body);
        }

        /// <summary>AC2.4 — the resting order book, per good: reference price,
        /// then every live ask (cheapest first) and bid (dearest first) with
        /// owner, qty, grade, and the limit's delta against the reference
        /// (above reference in "warn" red for an ask — pricier than the
        /// market; below reference in "warn" for a bid — lowballing it). A
        /// good with no resting orders is skipped, matching `ebook`'s
        /// default (unfiltered) view.</summary>
        private static void BookSection(VisualElement body,
            IReadOnlyList<MarketGoodRow> goods)
        {
            bool any = false;
            foreach (var g in goods)
            {
                if (g.Asks.Count == 0 && g.Bids.Count == 0) continue;
                any = true;
                Line(body, Inv($"{g.GoodName} — ref {g.Price:0.00}"));
                foreach (var o in g.Asks)
                    Kv(body, Inv($"  ask {o.Qty:0.#} @ {o.LimitPrice:0.00}"),
                       Inv($"grade {o.Grade:0.00} · {o.OwnerName}"),
                       o.RefDelta > 0 ? "warn" : null);
                foreach (var o in g.Bids)
                    Kv(body, Inv($"  bid {o.Qty:0.#} @ {o.LimitPrice:0.00}"),
                       Inv($"escrow {o.EscrowCredits:0.0} · {o.OwnerName}"),
                       o.RefDelta < 0 ? "warn" : null);
            }
            if (!any) Line(body, "(bare book — no resting orders)", dim: true);
        }

        /// <summary>AC1.4 — the selected outpost's own detail, rendered inside
        /// its parent port's Market panel (the outpost has no market of its
        /// own). Reads the SAME DomainInteriorQuery card the atlas marks read,
        /// so the panel and the map never drift.</summary>
        private static void OutpostSection(VisualElement body, PanelContext ctx,
                                           int portId, int outpostId)
        {
            var card = DomainInteriorQuery.Card(ctx.Model, ctx.Eye, portId);
            DomainOutpostCard outpost = null;
            if (card != null)
                foreach (var o in card.Outposts)
                    if (o.Id == outpostId) { outpost = o; break; }
            Sect(body, "selected outpost");
            if (outpost == null)
            {
                Line(body, "(outpost no longer in this domain)", dim: true);
                return;
            }
            var head = new VisualElement
            { style = { flexDirection = FlexDirection.Row } };
            if (outpost.Graduated) Tag(head, "GRADUATED", "good");
            Line(head, outpost.Name);
            body.Add(head);
            Kv(body, "at", Inv($"({outpost.Hex.Q},{outpost.Hex.R})"));
            Kv(body, "founded", Inv($"y{outpost.FoundingYear}"));
            bool frontier =
                outpost.Candidacy.Kind == DomainCandidacyKind.Frontier
                || outpost.Candidacy.Kind == DomainCandidacyKind.FrontierNoPort;
            Kv(body, "candidacy",
               DomainInteriorMarks.CandidacyText(outpost.Candidacy.Kind),
               frontier ? "acc" : null);
            if (outpost.Candidacy.Kind == DomainCandidacyKind.Graduated
                && outpost.Candidacy.GraduatedPortId >= 0)
            {
                int gpid = outpost.Candidacy.GraduatedPortId;
                var row = Row(body, () => ctx.Open(
                    new PanelRequest(PanelType.Market, gpid)));
                Line(row, Inv($"became port #{gpid}"));
            }
            if (outpost.Residents.Count == 0)
                Line(body, "(unpeopled — a claim, not yet a home)", dim: true);
            foreach (var r in outpost.Residents)
                Kv(body, SpeciesName(ctx, r.SpeciesId),
                   Inv($"size {r.Size:0.00}, SoL {r.SoL:0.00}"));
        }

        private static string SpeciesName(PanelContext ctx, int speciesId)
        {
            var species = ctx.Model.State.Skeleton.Species;
            return speciesId >= 0 && speciesId < species.Count
                ? species[speciesId].Name : Inv($"species #{speciesId}");
        }

        private static (string, VisualElement) Project(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = ProjectPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null)
                return ("PROJECT", Missing(body, "no such project"));
            Kv(body, "kind", card.Kind.ToString());
            Kv(body, "owner", card.OwnerName);
            Kv(body, "funder", card.FunderName,
               card.FunderActorId != card.OwnerActorId ? "acc" : null);
            Kv(body, "port", Inv($"#{card.PortId}"));
            Kv(body, "priority", PriorityName(card.Priority));
            Meter(body, "progress", card.Progress,
                  Inv($"{card.YearsDelivered:0.0}/{card.YearsRequired:0.0}"));
            Meter(body, "fed", card.FedFraction,
                  Inv($"{card.FedFraction * 100:0}%"),
                  card.FedFraction < 0.25 ? "bad"
                  : card.FedFraction < 0.75 ? "warn" : null);
            if (card.Completed) Kv(body, "status", "done", "good");
            else if (card.Cancelled) Kv(body, "status", "cancelled", "bad");
            else Kv(body, "honest eta", Inv($"y{card.EtaYear}"),
                    card.FedFraction < 0.5 ? "warn" : null);
            Sect(body, "per-year basket");
            if (card.Basket.Count == 0) Line(body, "(travel kind — none)", dim: true);
            foreach (var line in card.Basket)
                Kv(body, line.GoodName, Inv($"{line.QtyPerYear:0.##}/y"));
            Kv(body, "wages", Inv($"{card.WagesPerYear:0.#}/y"));
            return (Inv($"PROJECT #{card.Id}"), body);
        }

        private static (string, VisualElement) Facility(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = FacilityPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null)
                return ("FACILITY", Missing(body, "no such facility"));
            Line(body, Inv($"{card.TypeName} · tier {card.Tier}"));
            Kv(body, "family", card.Family.ToString().ToLowerInvariant());
            // a corp's panel subject is its registry id (OwnerCorpId),
            // never its actor id — the id spaces differ (review finding)
            bool corpOwner =
                card.OwnerKind == Core.Epoch.ActorKind.Corporation;
            var owner = Row(body, () => ctx.Open(new PanelRequest(
                corpOwner ? PanelType.Corporations : PanelType.Polity,
                corpOwner ? card.OwnerCorpId : card.OwnerActorId)));
            Line(owner, "owner: " + card.OwnerName
                + Inv($" ({card.OwnerKind.ToString().ToLowerInvariant()})"));
            Meter(body, "condition", card.Condition,
                  Inv($"{card.Condition:0.00}"),
                  card.Condition < 0.35 ? "bad"
                  : card.Condition < 0.7 ? "warn" : null);
            // Active ≡ Commissioned today (MarketEngine.IsActive) — no
            // third state exists
            if (!card.Commissioned)
                Kv(body, "status", "under construction", "warn");
            else Kv(body, "status", "active", "good");
            Kv(body, "built", Inv($"y{card.BuiltYear}"));
            Sect(body, "produces");
            if (card.Produces.Count == 0)
                Line(body, "(capability, not goods)", dim: true);
            foreach (var name in card.Produces) Line(body, name);
            if (card.MarketPortId >= 0)
            {
                var market = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Market, card.MarketPortId)));
                Line(market, Inv($"trades at port #{card.MarketPortId}"));
            }
            return (Inv($"FACILITY #{card.Id}"), body);
        }

        private static (string, VisualElement) Shipment(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = ShipmentPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null)
                return ("SHIPMENT", Missing(body, "no such shipment"));
            var purposeRow = new VisualElement();
            purposeRow.style.flexDirection = FlexDirection.Row;
            Tag(purposeRow, PurposeLabel(card.Purpose),
                card.Purpose == FreightPurpose.WarConvoy ? "bad" : null);
            body.Add(purposeRow);
            Kv(body, "channel", card.Channel.ToString().ToLowerInvariant());
            Kv(body, "owner", card.OwnerName);
            var route = Row(body, () => ctx.Open(new PanelRequest(
                PanelType.Market, card.DestPortId)));
            Line(route, Inv($"#{card.OriginPortId} → #{card.DestPortId} ")
                + (card.LaneCount == 0 ? "off-lane"
                   : Inv($"via {card.LaneCount} lane")
                     + (card.LaneCount == 1 ? "" : "s")));
            Meter(body, "sailed",
                  card.TotalYears > 0 ? card.SailedYears / card.TotalYears : 0,
                  Inv($"{card.SailedYears:0.0}/{card.TotalYears:0.0}"));
            if (card.Stalled)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                Tag(row, "STALLED", "bad");
                Line(row, "the current leg is closed", dim: true);
                body.Add(row);
            }
            else
                Kv(body, "live eta", Inv($"y{card.EtaYear}"), "acc");
            Sect(body, "cargo");
            foreach (var line in card.Cargo)
                Kv(body, line.GoodName,
                   Inv($"{line.Qty:0.#} @ {line.Grade:0.00}"));
            if (card.Rider != null)
            {
                Sect(body, "rider contract");
                Kv(body, "route",
                   card.Rider.OriginPortOwnerName + " → "
                   + card.Rider.DestPortOwnerName);
                Kv(body, "fee", Inv($"{card.Rider.FeeEscrow:0.0}"));
                Kv(body, "poster", card.Rider.PosterName);
                Link(body, Inv($"OPEN CONTRACTS BOARD (#{card.Rider.Id})"),
                    () => ctx.Open(new PanelRequest(PanelType.Contracts)));
            }
            return (Inv($"SHIPMENT #{card.Id}"), body);
        }

        /// <summary>The 4-way freight-purpose label (AC2.6) — same words
        /// `efreight` prints, Core.Atlas.FreightPurpose the shared type.</summary>
        private static string PurposeLabel(FreightPurpose purpose) => purpose switch
        {
            FreightPurpose.WarConvoy => "WAR CONVOY",
            FreightPurpose.Courier => "courier",
            FreightPurpose.SpreadRun => "spread run",
            _ => "state haul",
        };

        private static (string, VisualElement) Fleet(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = FleetPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null) return ("FLEET", Missing(body, "no such fleet"));
            var row = card.Row;
            Kv(body, "owner", row.OwnerName);
            Kv(body, "posture", row.Posture.ToString().ToLowerInvariant());
            Kv(body, "station", StationLabel(row));
            Kv(body, "readiness", Inv($"{row.Readiness:0.00}"));
            Kv(body, "home port", card.HomePortId >= 0
                ? Inv($"#{card.HomePortId}") : "none");
            // forward depot (AC2.7) — only a deployed (Blockade/Expedition)
            // fleet has one; FleetPanel.Card already gates it to -1 otherwise
            if (card.ForwardDepotPortId >= 0)
                Kv(body, "forward depot",
                   Inv($"port #{card.ForwardDepotPortId} ({card.ForwardDepotDistanceHexes} hexes)"));
            if (card.CommanderName != null)
            {
                var cmd = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Character, card.CommanderId)));
                Line(cmd, "commander: " + card.CommanderName);
            }
            else Kv(body, "commander", "(vacant slot)");
            Sect(body, "composition");
            if (card.Composition.Count == 0) Line(body, "(no hulls)", dim: true);
            foreach (var g in card.Composition)
                Line(body, Inv($"{g.Count}x {g.DesignName} Mk {g.Mark} ({g.Role}/{g.Size}, grade {g.Grade:0.00})"));
            Sect(body, "vectors (computed, never stored)");
            var v = card.Vectors;
            Kv(body, "strike / sustained",
               Inv($"{v.Strike:0.0} / {v.Sustained:0.0}"));
            Kv(body, "screening / tracking",
               Inv($"{v.Screening:0.0} / {v.Tracking:0.0}"));
            Kv(body, "detection / stealth",
               Inv($"{v.Detection:0.0} / {v.Stealth:0.00}"));
            Kv(body, "capacity", Inv($"{v.Capacity:0.0}"));
            Kv(body, "endurance floor",
               Inv($"{v.EnduranceFloor:0.0} (~{(int)card.EnduranceHexesOffLane} hexes off-lane)"));
            Kv(body, "upkeep", Inv($"{v.Upkeep:0.0}"));
            Link(body, "DESIGNS", () => ctx.Open(new PanelRequest(
                PanelType.Designs, row.OwnerActorId)));
            return (Inv($"FLEET #{row.Id}"), body);
        }

        private static (string, VisualElement) Designs(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var rows = FleetPanel.Designs(ctx.Model, ctx.Eye, request.Id);
            if (rows.Count == 0) Line(body, "no designs yet", dim: true);
            foreach (var d in rows)
                Line(body, Inv($"#{d.Id} {d.OwnerName,-14} {d.Name} Mk {d.Mark} ({d.Role}/{d.Size}) grade {d.ComponentGrade:0.00} t{d.TechTier} y{d.DesignedYear}"));
            return (request.Id >= 0 ? "DESIGNS (ONE LINEAGE)" : "DESIGNS", body);
        }

        private static (string, VisualElement) Wars(PanelContext ctx,
                                                    VisualElement body)
        {
            var rows = WarPanel.Rows(ctx.Model, ctx.Eye);
            int active = 0;
            foreach (var w in rows) if (w.Active) active++;
            Line(body, Inv($"wars: {active} burning (of {rows.Count} ever declared)"), dim: true);
            foreach (var w in rows)
            {
                var captured = w;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.War, captured.Id)));
                if (w.Active) Tag(row, "ACTIVE", "bad");
                Line(row, Inv($"#{w.Id} {w.Name} — {w.AttackerName} vs {w.DefenderName} · objectives {w.ObjectivesTaken}/{w.ObjectivesTotal}"));
            }
            return ("WARS", body);
        }

        private static (string, VisualElement) War(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = WarPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null) return ("WAR", Missing(body, "no such war"));
            Line(body, card.Name
                + (card.Active ? "" : Inv($" — ended y{card.EndedYear}")));
            Side(body, ctx, "attacker", card.Attacker);
            Side(body, ctx, "defender", card.Defender);
            Kv(body, "cause", card.Cause.ToString());
            Kv(body, "demand", card.Demand.ToString());
            Kv(body, "declared", Inv($"y{card.DeclaredYear}"));
            Sect(body, "fronts");
            foreach (var o in card.Objectives)
            {
                string status = o.Status switch
                {
                    Core.Epoch.ObjectiveStatus.Taken => "[x] ",
                    Core.Epoch.ObjectiveStatus.Abandoned => "[~] ",
                    _ => "[ ] ",
                };
                string front = o.Type switch
                {
                    Core.Epoch.WarObjectiveType.CapturePort =>
                        Inv($"capture port #{o.TargetId}")
                        // siege text only once the clock runs (REPL parity)
                        + (o.SiegeYears > 0 && o.FallsAtYears != null
                            ? Inv($" — under siege ({o.SiegeYears}y, falls at {o.FallsAtYears})")
                            : ""),
                    Core.Epoch.WarObjectiveType.BlockadeLane =>
                        Inv($"blockade lane #{o.TargetId}")
                        + (o.SiegeYears > 0 ? Inv($" — cut {o.SiegeYears}y") : ""),
                    _ => "break the enemy fleet",
                };
                Line(body, status + front);
            }
            if (card.FleetsOnStation.Count > 0) Sect(body, "on station");
            foreach (var f in card.FleetsOnStation)
            {
                var captured = f;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Fleet, captured.FleetId)));
                Line(row, Inv($"fleet #{f.FleetId}: {f.Hulls} hulls at ({f.Hex.Q},{f.Hex.R})")
                    + (f.CommanderName != null ? " under " + f.CommanderName : "")
                    // forward depot (AC2.7) — every station fleet here is
                    // already deployed, so DepotPortId is -1 only when the
                    // attacker holds no port at all
                    + (f.DepotPortId >= 0
                        ? Inv($" · depot #{f.DepotPortId} ({f.DepotDistanceHexes}h)")
                        : " · depot none"));
            }
            Sect(body, "chronicle");
            if (card.Chronicle.Count == 0) Line(body, "(quiet so far)", dim: true);
            foreach (var line in card.Chronicle) Line(body, line, dim: true);
            return (Inv($"WAR #{card.Id}"), body);
        }

        private static void Side(VisualElement body, PanelContext ctx,
                                 string label, WarSide side)
        {
            var row = Row(body, () => ctx.Open(new PanelRequest(
                PanelType.Polity, side.LeaderId)));
            Line(row, label + ": " + side.LeaderName
                + (side.AllyIds.Count > 0
                    ? Inv($" (+{side.AllyIds.Count} allies)") : ""));
            Meter(body, "exhaustion", side.Exhaustion, null,
                  side.Exhaustion >= 0.7 ? "bad" : null);
            if (side.StrengthOfMustered != null)
                Kv(body, "strength",
                   Inv($"{side.StrengthOfMustered:0%} of mustered"));
        }

        private static (string, VisualElement) Relations(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var rows = RelationsPanel.Rows(ctx.Model, ctx.Eye, request.Id);
            if (rows.Count == 0) Line(body, "no live relations", dim: true);
            foreach (var r in rows)
            {
                var captured = r;
                var head = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.Polity,
                    captured.PolityAId == request.Id
                        ? captured.PolityBId : captured.PolityAId)));
                if (r.WarId != null) Tag(head, "AT WAR", "bad");
                Line(head, Inv($"{r.PolityAName} <> {r.PolityBName}"));
                Meter(body, "warmth", r.Warmth);
                Meter(body, "tension", r.Tension, null,
                      r.Tension >= 0.6 ? "warn" : null);
                Line(body, Inv($"warmth: base {r.WarmthTerms[0]:0.00} · trade {r.WarmthTerms[1]:0.00} · treaty {r.WarmthTerms[2]:0.00} · dynastic {r.WarmthTerms[3]:0.00} · ideology {r.WarmthTerms[4]:0.00} · reputation {r.WarmthTerms[5]:0.00}"), dim: true);
                Line(body, Inv($"tension: overlap {r.TensionTerms[0]:0.00} · claims {r.TensionTerms[1]:0.00} · interdiction {r.TensionTerms[2]:0.00} · ideology×zeal {r.TensionTerms[3]:0.00} · agitation {r.TensionTerms[4]:0.00} · militancy {r.TensionTerms[5]:0.00}"), dim: true);
                string bond = r.VassalPolityId >= 0
                    ? Inv($"vassalage (#{r.VassalPolityId} kneels, since y{r.VassalSinceYear})")
                    : r.Rung.ToString()
                      + (r.RungYear >= 0 ? Inv($" since y{r.RungYear}") : "");
                Kv(body, "bond", bond);
                if (r.OfferedRung != Core.Epoch.TreatyRung.None)
                    Kv(body, "on the table",
                       Inv($"{r.OfferedRung} offered by #{r.OfferedById}"), "acc");
                foreach (var claim in r.Claims)
                    Line(body, Inv($"claim: {claim.HolderName} holds {claim.Type.ToString().ToLowerInvariant()} (raised y{claim.RaisedYear})"), dim: true);
            }
            return (request.Id >= 0
                ? Inv($"RELATIONS OF #{request.Id}") : "RELATIONS", body);
        }

        private static (string, VisualElement) Character(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var bio = CharacterPanel.Bio(ctx.Model, ctx.Eye, request.Id);
            if (bio == null)
                return ("CHARACTER", Missing(body, "no such character"));
            Line(body, bio.Name + " — " + bio.SpeciesName + ", "
                + (bio.Alive ? Inv($"age {bio.Age}")
                             : Inv($"y{bio.BirthYear}–y{bio.DeathYear}")));
            string role = bio.Role.ToString().ToLowerInvariant();
            if (bio.Notable != Core.Epoch.NotableType.None)
                role += ", " + bio.Notable.ToString().ToLowerInvariant();
            if (bio.HouseName != null) role += ", house " + bio.HouseName;
            var polity = Row(body, () => ctx.Open(new PanelRequest(
                PanelType.Polity, bio.PolityId)));
            Line(polity, role + Inv($" of polity #{bio.PolityId}"));
            Kv(body, "renown", Inv($"{bio.Renown:0.0}"));
            Meter(body, "boldness", bio.Boldness);
            Meter(body, "zeal", bio.Zeal);
            Meter(body, "competence", bio.Competence);
            Meter(body, "ambition", bio.Ambition);
            Sect(body, "a life from the log");
            if (bio.Chronicle.Count == 0)
                Line(body, "(no chronicle presence — a quiet life)", dim: true);
            foreach (var line in bio.Chronicle) Line(body, line, dim: true);
            return (Inv($"CHARACTER #{bio.Id}"), body);
        }

        private static (string, VisualElement) Corporations(
            PanelRequest request, PanelContext ctx, VisualElement body)
        {
            var rows = CorporationPanel.Rows(ctx.Model, ctx.Eye);
            if (rows.Count == 0) Line(body, "no corporations yet", dim: true);
            foreach (var corp in rows)
            {
                if (request.Id >= 0 && corp.Id != request.Id) continue;
                Line(body, Inv($"#{corp.Id} {corp.Name} ({corp.Niche.ToString().ToLowerInvariant()})"));
                if (!corp.Active)
                {
                    Line(body, Inv($"[dead, founded y{corp.FoundedYear}]"), dim: true);
                    continue;
                }
                if (corp.HostName != null)
                {
                    var captured = corp;
                    var host = Row(body, () => ctx.Open(new PanelRequest(
                        PanelType.Polity, captured.HostPolityId)));
                    Line(host, "chartered by " + corp.HostName);
                }
                else Line(body, "chartered nowhere", dim: true);
                Kv(body, "credits", Inv($"{corp.Credits:0}"));
                Kv(body, "assets",
                   Inv($"{corp.FacilityCount} facilities · {corp.Hulls} hulls"));
                if (corp.ExecutiveName != null)
                    Kv(body, "exec", corp.ExecutiveName);
                if (corp.FundedProjectIds.Count > 0)
                    Sect(body, "funds (its treasury feeds)");
                foreach (var projectId in corp.FundedProjectIds)
                {
                    var captured = projectId;
                    var row = Row(body, () => ctx.Open(new PanelRequest(
                        PanelType.Project, captured)));
                    Line(row, Inv($"project #{projectId}"));
                }
            }
            return (request.Id >= 0
                ? Inv($"CORPORATION #{request.Id}") : "CORPORATIONS", body);
        }

        private static (string, VisualElement) Poi(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var card = PoiPanel.Card(ctx.Model, ctx.Eye, request.Id);
            if (card == null) return ("POI", Missing(body, "no such POI"));
            var row = card.Row;
            Line(body, row.TypeName
                + (row.Depleted ? " [faded]" : "")
                + (row.Dormant ? " · DORMANT — something is still awake" : ""));
            Kv(body, "at", Inv($"({row.Hex.Q},{row.Hex.R})"));
            Kv(body, "since", Inv($"y{row.FoundedYear}"));
            Kv(body, "magnitude", Inv($"{row.Magnitude:0.#}"));
            if (row.Type == Core.Epoch.PoiType.Battlefield)
                Kv(body, "salvage",
                   Inv($"{row.SalvageRemaining:0.#} hulls ({row.HullsSalvaged} drawn)"));
            if (row.ParticipantActorIds.Count > 0)
            {
                Sect(body, "history of");
                foreach (var actorId in row.ParticipantActorIds)
                {
                    var captured = actorId;
                    var actorRow = Row(body, () => ctx.Open(new PanelRequest(
                        PanelType.Polity, captured)));
                    Line(actorRow,
                        ctx.Model.State.Actors[actorId].Name
                        + Inv($" (#{actorId})"));
                }
            }
            Sect(body, "compiled from");
            if (card.Chronicle.Count == 0) Line(body, "(no events)", dim: true);
            foreach (var line in card.Chronicle) Line(body, line, dim: true);
            Link(body, "EVERYTHING THAT HAPPENED HERE", () => ctx.Open(
                new PanelRequest(PanelType.ChroniclePlace, hex: row.Hex)));
            return (Inv($"POI #{row.Id}"), body);
        }

        private static (string, VisualElement) Beliefs(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var rows = BeliefPanel.Rows(ctx.Model, ctx.Eye, request.Id);
            if (rows.Count == 0)
                Line(body, "(no beliefs — nobody met yet)", dim: true);
            foreach (var b in rows)
                Line(body, Inv($"#{b.SubjectId} {b.SubjectName} — strength {b.BelievedStrength:0.#} (truth {b.TruthStrength:0.#}) · coalition {b.DefensiveStrength:0.#} · menu {b.MenuCount}")
                    + (b.StaleYears > 0 ? Inv($" · {b.StaleYears}y stale") : " · fresh"));
            var wars = BeliefPanel.WarRows(ctx.Model, ctx.Eye, request.Id);
            if (wars.Count > 0) Sect(body, "the fronts, as heard");
            foreach (var w in wars)
                Line(body, Inv($"{w.WarName}: believed exhaustion {w.BelievedExhaustion:0.00} (truth {w.TruthExhaustion:0.00}) · share {w.StrengthShare:0.00}")
                    + (w.StaleYears > 0
                        ? Inv($" · reports {w.StaleYears}y old") : " · fresh"));
            return (Inv($"BELIEFS OF #{request.Id}"), body);
        }

        private static (string, VisualElement) News(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            if (request.Id >= 0)
            {
                var card = NewsPanel.Journey(ctx.Model, ctx.Eye, request.Id);
                if (card == null) return ("PULSE", Missing(body, "no such pulse"));
                Line(body, card.EventText);
                Kv(body, "born", Inv($"y{card.EmitYear} at ({card.Origin.Q},{card.Origin.R})"));
                Sect(body, "the journey");
                if (card.Deliveries.Count == 0)
                    Line(body, "(still in transit — nobody has heard)", dim: true);
                foreach (var d in card.Deliveries)
                    Line(body, Inv($"y{d.Year}  reaches {d.ActorName} ({d.TransitYears}y in transit)"));
                return (Inv($"PULSE #{card.Id}"), body);
            }
            var rows = NewsPanel.LivePulses(ctx.Model, ctx.Eye);
            Line(body, Inv($"{rows.Count} pulses in transit"), dim: true);
            foreach (var p in rows)
            {
                var captured = p;
                var row = Row(body, () => ctx.Open(new PanelRequest(
                    PanelType.News, captured.Id)));
                var text = Line(row, Inv($"({p.AgeYears:0}y out, heard by {p.HeardByCount}/{p.EnteredCount}) ") + p.EventText);
                text.style.flexShrink = 1f;
            }
            return ("NEWS", body);
        }

        private static (string, VisualElement) Stances(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var rows = StancesPanel.Rows(ctx.Model, ctx.Eye, request.Id);
            if (rows.Count == 0)
                Line(body, "no stances yet — the galaxy has heard nothing",
                     dim: true);
            else
                Line(body, "news-arrived judgments; reputation is per audience",
                     dim: true);
            foreach (var s in rows)
                Line(body, Inv($"{s.Stance,6:+0.00;-0.00} — {s.ObserverName} toward {s.SubjectName}")
                    + (s.Verdict == StanceVerdict.Monster
                        ? " · a monster to this audience"
                        : s.Verdict == StanceVerdict.Hero
                            ? " · a hero to this audience" : ""));
            return (request.Id >= 0
                ? Inv($"STANCES OF #{request.Id}") : "STANCES", body);
        }

        private static (string, VisualElement) Chronicle(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var lines = request.Id >= 0
                ? ChronicleQueries.ForActor(ctx.Model, ctx.Eye, request.Id)
                : ChronicleQueries.Annotated(ctx.Model, ctx.Eye,
                    ctx.Model.State.Log.Events);
            AppendChronicle(body, lines);
            return (request.Id >= 0
                ? Inv($"CHRONICLE OF #{request.Id}") : "CHRONICLE", body);
        }

        private static (string, VisualElement) ChroniclePlace(
            PanelRequest request, PanelContext ctx, VisualElement body)
        {
            AppendChronicle(body, ChronicleQueries.AtPlace(
                ctx.Model, ctx.Eye, request.Hex));
            return (Inv($"HERE: ({request.Hex.Q},{request.Hex.R})"), body);
        }

        private static void AppendChronicle(VisualElement body,
            IReadOnlyList<ChronicleLine> lines)
        {
            if (lines.Count == 0) { Line(body, "(no events)", dim: true); return; }
            string era = null;
            int from = System.Math.Max(0, lines.Count - 120);
            if (from > 0)
                Line(body, Inv($"(…{from} earlier events omitted)"), dim: true);
            for (int i = from; i < lines.Count; i++)
            {
                if (lines[i].EraHeader != null && lines[i].EraHeader != era)
                {
                    era = lines[i].EraHeader;
                    Sect(body, "── " + era + " ──");
                }
                Line(body, lines[i].Text, dim: true);
            }
        }

        private static (string, VisualElement) Eras(PanelContext ctx,
                                                    VisualElement body)
        {
            var rows = EraQueries.Eras(ctx.Model, ctx.Eye);
            Line(body, Inv($"{rows.Count} detected over {ctx.Model.State.EpochIndex} epochs"), dim: true);
            foreach (var era in rows)
                Line(body, Inv($"{era.Name} — y{era.StartYear}–y{era.EndYear} ({era.Kind.ToString().ToLowerInvariant()})"));
            return ("ERAS", body);
        }

        // ---- the registry drawer ----

        private static (string, VisualElement) Find(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var hits = RegistryQueries.Find(ctx.Model, ctx.Eye,
                                            request.Text ?? "");
            if (hits.Count == 0) Line(body, "no matches", dim: true);
            foreach (var hit in hits)
            {
                var captured = hit;
                var row = Row(body, () =>
                {
                    if (captured.JumpHex != null)
                        ctx.JumpTo(captured.JumpHex.Value);
                    ctx.Open(PanelFor(captured));
                });
                Tag(row, hit.Kind.ToString().ToLowerInvariant());
                Line(row, hit.Name);
            }
            return (Inv($"FIND “{request.Text}”"), body);
        }

        private static PanelRequest PanelFor(FindHit hit) => hit.Kind switch
        {
            FindKind.Actor => new PanelRequest(PanelType.Polity, hit.Id),
            FindKind.Character => new PanelRequest(PanelType.Character, hit.Id),
            FindKind.Corporation => new PanelRequest(PanelType.Corporations, hit.Id),
            FindKind.War => new PanelRequest(PanelType.War, hit.Id),
            FindKind.Poi => new PanelRequest(PanelType.Poi, hit.Id),
            _ => new PanelRequest(PanelType.Market, hit.Id),
        };

        private static (string, VisualElement) Goods(PanelContext ctx,
                                                     VisualElement body)
        {
            foreach (var g in RegistryQueries.GoodsCatalog(ctx.Model, ctx.Eye))
                Line(body, Inv($"{g.Name,-16} {g.Tier.ToString().ToLowerInvariant(),-10} {g.RecipeCount} recipe")
                    + (g.RecipeCount == 1 ? "" : "s"));
            return ("GOODS", body);
        }

        private static (string, VisualElement) Knobs(PanelRequest request,
            PanelContext ctx, VisualElement body)
        {
            var rows = RegistryQueries.Knobs(ctx.Model, ctx.Eye,
                                             request.Text ?? "");
            Line(body, Inv($"{rows.Count} knobs (live values of the loaded sim)"), dim: true);
            foreach (var k in rows)
            {
                Kv(body, k.Name, Inv($"{k.Value:0.####}"));
                Line(body, k.Doc, dim: true);
            }
            return ("KNOBS", body);
        }

        private static (string, VisualElement) Stats(PanelContext ctx,
                                                     VisualElement body)
        {
            var s = RegistryQueries.Stats(ctx.Model, ctx.Eye);
            Kv(body, "world year", Inv($"y{s.WorldYear} (epoch {s.EpochIndex})"));
            Kv(body, "ports / lanes", Inv($"{s.Ports} / {s.Lanes}"));
            Kv(body, "polities entered", Inv($"{s.PolitiesEntered}"));
            Kv(body, "wars burning", Inv($"{s.ActiveWars}"),
               s.ActiveWars > 0 ? "bad" : null);
            Kv(body, "fleets (hulled)", Inv($"{s.Fleets}"));
            Kv(body, "living characters", Inv($"{s.Characters}"));
            Kv(body, "corporations", Inv($"{s.Corporations}"));
            Kv(body, "POIs", Inv($"{s.Pois}"));
            Kv(body, "shipments in transit", Inv($"{s.ShipmentsInTransit}"));
            Kv(body, "projects in flight", Inv($"{s.ProjectsInFlight}"));
            Kv(body, "chronicle events", Inv($"{s.Events}"));
            return ("WORLD STATS", body);
        }

        // ---- helpers ----

        private static VisualElement Missing(VisualElement body, string text)
        {
            Line(body, text, dim: true);
            return body;
        }

        private static string PriorityName(Core.Epoch.ProjectPriority p) =>
            p.ToString().ToLowerInvariant();

        private static string StationLabel(FleetRow row) => row.Station switch
        {
            StationKind.Lane => Inv($"lane #{row.StationId}"),
            StationKind.Port => Inv($"port #{row.StationId}"),
            StationKind.InTransit => "in transit",
            StationKind.Docked => Inv($"docked #{row.StationId}"),
            StationKind.Adrift => "adrift",
            _ => "unassigned",
        };

        private static IEnumerable<T> Tail<T>(IReadOnlyList<T> list, int n)
        {
            for (int i = System.Math.Max(0, list.Count - n);
                 i < list.Count; i++)
                yield return list[i];
        }
    }
}
