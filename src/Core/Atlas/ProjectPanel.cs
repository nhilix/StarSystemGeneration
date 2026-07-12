using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One per-year basket line of a project card.</summary>
public sealed record BasketLine(GoodId Good, string GoodName, double QtyPerYear);

/// <summary>The Project inspector's card (K3, NEW at T1): everything
/// `eprojects` prints, typed — kind, funder vs owner, priority, the
/// LastFedFraction starvation readout, and the HONEST eta under current
/// starvation, plus the per-year basket the site draws.</summary>
public sealed record ProjectCard(
    int Id, ProjectKind Kind, int OwnerActorId, string OwnerName,
    int FunderActorId, string FunderName, int PortId, HexCoordinate Hex,
    ProjectPriority Priority, double FedFraction,
    double YearsDelivered, double YearsRequired, double Progress,
    long? EtaYear, bool Completed, bool Cancelled,
    IReadOnlyList<BasketLine> Basket, double WagesPerYear);

/// <summary>K3: the works-lens site mark's panel query — `eprojects`
/// parity (Repl.RenderProjects): default in-flight only, filter by funder
/// (whose treasury is drawn), honest eta = WorldYear +
/// ceil(remaining / max(LastFedFraction, 0.05)).</summary>
public static class ProjectPanel
{
    /// <summary>The project table, id order (P6).</summary>
    public static List<ProjectCard> Cards(AtlasReadModel model, EyeContext eye,
        int funderActorId = -1, bool includeAll = false)
    {
        var state = model.State;
        var cards = new List<ProjectCard>();
        foreach (var p in state.Projects)                 // id order (P6)
        {
            if (!includeAll && !p.InFlight) continue;
            if (funderActorId >= 0 && p.FunderActorId != funderActorId)
                continue;
            cards.Add(CardOf(state, p));
        }
        return cards;
    }

    /// <summary>One project by id — the site-mark click target.</summary>
    public static ProjectCard? Card(AtlasReadModel model, EyeContext eye,
                                    int projectId)
    {
        var state = model.State;
        if (projectId < 0 || projectId >= state.Projects.Count) return null;
        return CardOf(state, state.Projects[projectId]);
    }

    private static ProjectCard CardOf(SimState state, Project p)
    {
        string owner = p.OwnerActorId >= 0 && p.OwnerActorId < state.Actors.Count
            ? state.Actors[p.OwnerActorId].Name : "—";
        string funder = p.FunderActorId >= 0 && p.FunderActorId < state.Actors.Count
            ? state.Actors[p.FunderActorId].Name : "—";
        // the honest eta under CURRENT starvation (eprojects parity)
        long? eta = p.Completed || p.Cancelled ? null
            : state.WorldYear + (long)Math.Ceiling(
                (p.YearsRequired - p.YearsDelivered)
                / Math.Max(p.LastFedFraction, 0.05));
        var basket = new List<BasketLine>();
        for (int g = 0; g < p.PerYearBasket.Length; g++)
            if (p.PerYearBasket[g] > 0)
                basket.Add(new BasketLine((GoodId)g,
                    Goods.Get((GoodId)g).Name, p.PerYearBasket[g]));
        return new ProjectCard(p.Id, p.Kind, p.OwnerActorId, owner,
            p.FunderActorId, funder, p.PortId, p.Hex, p.Priority,
            p.LastFedFraction, p.YearsDelivered, p.YearsRequired,
            p.Progress, eta, p.Completed, p.Cancelled, basket,
            p.WagesPerYear);
    }
}
