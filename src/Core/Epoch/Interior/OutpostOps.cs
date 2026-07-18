using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>The frontier gate — Stage 3's anti-clustering guarantee
/// (domain-hex-expansion design §4, "The frontier gate"). A read-only
/// predicate over the port registry that decides which outposts are
/// candidacy-eligible for graduation into a real starport. It does NOT
/// promote anything (that is T3.2's administrative-promotion project); it only
/// answers "is this outpost far enough from EVERY existing port to become one
/// itself." Named apart from <see cref="GraduationOps"/> on purpose: that is
/// FACTION graduation (schisms/coups), this is OUTPOST graduation eligibility.
///
/// <para>The gate mirrors <see cref="ColonyValuation.EncroachedPolities"/>'
/// overlap geometry — a graduated outpost becomes a tier-1 port, so the
/// distance it must clear is the newcomer's own tier-1 service radius plus the
/// incumbent's service radius plus the incumbent's astro bonus — tightened from
/// a scored penalty into a HARD gate, plus a configured margin
/// (<see cref="ExpansionKnobs.GraduationMarginHexes"/>). Because the threshold
/// scales with the very service radii that define domains, a promotion can
/// never place two ports within each other's reach, at ANY config — the
/// anti-goal is structurally impossible.</para>
///
/// <para>An outpost inside some port's domain (closer than the threshold) is
/// INTERIOR and never graduates — permanently subordinate density, correct and
/// intended (design §4, "Interior outposts never graduate"). The outpost's own
/// parent port is an existing port too, and an outpost sits within its parent's
/// reach by construction, so a near-parent outpost falls out as interior with
/// no special-casing: only outposts pushed to the fringe (far from the parent
/// AND every other port) read as frontier.</para>
///
/// <para>Pure and deterministic (P6): ports scanned in id order, no rolls, no
/// state mutation.</para></summary>
public static class OutpostOps
{
    /// <summary>True iff this outpost is frontier / candidacy-eligible: not
    /// already graduated, and at distance &gt; G from EVERY entered port.</summary>
    public static bool IsFrontier(SimState state, Outpost outpost)
        => FrontierStatus(state, outpost).IsFrontier;

    /// <summary>The outpost's standing against the frontier gate, for T3.2's
    /// candidacy scoring and the T3.3 REPL "interior vs frontier (dist vs G)"
    /// display. Reports the BINDING port — the one leaving the smallest gap
    /// (min of distance − G across every entered port), i.e. the port that most
    /// nearly (or actually does) disqualify the outpost — with its distance,
    /// its threshold G, and the resulting slack. Frontier iff even that binding
    /// port leaves a positive gap. A graduated outpost is never a candidate; an
    /// outpost with no entered port anywhere is vacuously frontier (nothing to
    /// clash with).</summary>
    public static FrontierStanding FrontierStatus(SimState state, Outpost outpost)
    {
        // a graduated outpost is history, no longer a candidate (design §4).
        if (outpost.Graduated) return new FrontierStanding(false, 0, 0, 0);

        var cfg = state.Config;
        // the newcomer is a tier-1 port: its own tier-1 service radius is half
        // the spacing, exactly EncroachedPolities' `newRadius`.
        int newRadius = PortDomains.ServiceRadius(cfg, 1);
        int bindingDist = 0, bindingG = 0, minSlack = int.MaxValue;
        bool anyPort = false;
        foreach (var p in state.Ports)                    // id order (P6)
        {
            // mirror EncroachedPolities' Entered guard (with the same bounds
            // safety SettleOps uses): only a real, entered port projects a
            // domain the gate must clear. The parent port is NOT exempted —
            // it counts like any other, so a near-parent outpost stays interior.
            if (p.OwnerActorId < 0 || p.OwnerActorId >= state.Actors.Count
                || !state.Actors[p.OwnerActorId].Entered) continue;
            anyPort = true;
            // G = newcomer tier-1 radius + incumbent radius + incumbent astro
            // bonus + margin (design §4 / ledger decision #2). Integer hex
            // arithmetic throughout, matching EncroachedPolities exactly.
            int g = newRadius + PortDomains.ServiceRadius(cfg, p.Tier)
                    + TechOps.AstroRadiusBonus(state, p.OwnerActorId)
                    + cfg.Expansion.GraduationMarginHexes;
            int dist = HexGrid.Distance(p.Hex, outpost.Hex);
            int slack = dist - g;
            if (slack < minSlack)
            { minSlack = slack; bindingDist = dist; bindingG = g; }
        }
        // no entered port anywhere → nothing to overlap → vacuously frontier.
        if (!anyPort) return new FrontierStanding(true, 0, 0, int.MaxValue);
        // frontier iff dist > G for EVERY port ⇔ min(dist − G) > 0.
        return new FrontierStanding(minSlack > 0, bindingDist, bindingG, minSlack);
    }
}

/// <summary>An outpost's standing against the frontier gate (design §4),
/// described by the BINDING port — the entered port leaving the smallest gap.
/// <paramref name="IsFrontier"/> is the candidacy verdict; the remaining
/// fields drive the REPL "interior vs frontier" display and T3.2's scoring.
/// For a graduated outpost every field is zero and <c>IsFrontier</c> is false;
/// for an outpost with no entered port anywhere <c>IsFrontier</c> is true and
/// <c>Slack</c> is <see cref="int.MaxValue"/>.</summary>
/// <param name="IsFrontier">Candidacy-eligible: not graduated and clear of
/// every port's domain plus margin.</param>
/// <param name="PortDistance">Hex distance to the binding port.</param>
/// <param name="Threshold">The binding port's gate distance G.</param>
/// <param name="Slack">PortDistance − Threshold; &gt; 0 iff frontier.</param>
public readonly record struct FrontierStanding(
    bool IsFrontier, int PortDistance, int Threshold, int Slack);
