using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>The frontier gate — Stage 3's anti-adjacency guarantee
/// (domain-hex-expansion design §4, "The frontier gate"). A read-only
/// predicate over the port registry that decides which outposts are
/// candidacy-eligible for graduation into a real starport. It does NOT
/// promote anything (that is T3.2's administrative-promotion project); it only
/// answers "is this outpost far enough from EVERY existing port core to become
/// one itself." Named apart from <see cref="GraduationOps"/> on purpose: that is
/// FACTION graduation (schisms/coups), this is OUTPOST graduation eligibility.
///
/// <para>Graduation is <b>densification, not a reach leap</b>: an outpost
/// graduates <em>inside</em> its parent's domain — a genuine second centre that
/// is not stacked on another port — becoming the domain's densifying second
/// core. So the gate is deliberately NOT
/// <see cref="ColonyValuation.EncroachedPolities"/>' domain-exclusion geometry
/// (the sum of both ports' radii, which demands the newcomer stay outside every
/// existing domain — impossible for an in-domain outpost), NOR any
/// radius-derived spacing at all. It is the <b>literal anti-adjacency</b>
/// spacing: an outpost must sit at least G hexes from every port core, where
/// <c>G = 1 + GraduationMarginHexes</c>
/// (<see cref="ExpansionKnobs.GraduationMarginHexes"/>, default margin 1 →
/// <b>G = 2</b>). G is uniform across EVERY port — parent, our own other ports,
/// foreign ports alike — never scaled by the incumbent's tier. "Never adjacent"
/// read literally is "not in a touching hex" ⇔ <c>dist ≥ 2</c>: a promotion can
/// never place two port cores in touching hexes, at ANY config — the anti-goal
/// (no two ports adjacent) is structurally impossible. Two earlier domain-scale
/// formulations of G blocked graduation entirely (outposts form 1–3 hexes from
/// their parent, so any domain-scale threshold was unreachable); the literal
/// G = 2 is the reconciliation (ledger decision #2 FINAL, 2026-07-20).</para>
///
/// <para>An outpost within <c>G</c> of any port core is INTERIOR and never
/// graduates — permanently subordinate density, correct and intended (design §4,
/// "Interior outposts never graduate"). The parent port is an existing port too
/// and falls out with no special-casing: a stacked-on-a-port outpost (dist 1)
/// reads interior; a second-centre outpost that has cleared its parent by
/// G ≥ 2 hexes graduates while still sitting inside the parent's larger service
/// radius (densification). GraduationMarginHexes is the knob: raise it to make
/// graduation rarer (a wider dead gap around every port).</para>
///
/// <para>Foreign-domain graduation is ALLOWED: the gate excludes no domain, so
/// an outpost may sit inside a foreign polity's larger domain as long as it
/// clears G from that domain's core. The diplomacy is PRICED by the
/// encroachment-tension bump (wired in the promotion), not FORBIDDEN by the
/// gate.</para>
///
/// <para>Pure and deterministic (P6): ports scanned in id order, no rolls, no
/// state mutation.</para></summary>
public static class OutpostOps
{
    /// <summary>True iff this outpost is frontier / candidacy-eligible: not
    /// already graduated, and at distance &gt;= G from EVERY entered port
    /// core.</summary>
    public static bool IsFrontier(SimState state, Outpost outpost)
        => FrontierStatus(state, outpost).IsFrontier;

    /// <summary>The outpost's standing against the frontier gate, for T3.2's
    /// candidacy scoring and the T3.3 REPL "interior vs frontier (dist vs G)"
    /// display. Reports the BINDING port — the NEAREST entered port core, the
    /// one that most nearly (or actually does) disqualify the outpost — with its
    /// distance, the uniform threshold G, and the resulting slack
    /// (<c>distance − G</c>). Frontier iff even that binding port leaves a
    /// non-negative gap. A graduated outpost is never a candidate; an outpost
    /// with no entered port anywhere is vacuously frontier (nothing to clash
    /// with).</summary>
    public static FrontierStanding FrontierStatus(SimState state, Outpost outpost)
    {
        // a graduated outpost is history, no longer a candidate (design §4).
        if (outpost.Graduated) return new FrontierStanding(false, 0, 0, 0);

        var cfg = state.Config;
        // G = the LITERAL anti-adjacency spacing: 1 + margin (default margin 1
        // → G = 2), UNIFORM across every port (design §4 / ledger decision #2
        // FINAL, 2026-07-20). NOT radius-derived — the two earlier domain-scale
        // formulations (ServiceRadius(1)+margin, and the EncroachedPolities
        // overlap sum) both tied G to domain scale and blocked graduation
        // entirely: instrumentation settled that outposts form 1–3 hexes from
        // their parent (0 at dist ≥ 4), so any domain-scale G is unreachable.
        // "Never adjacent" is the whole anti-goal, read literally: not in a
        // touching hex ⇔ dist ≥ 2. A stacked-on-a-port outpost (dist 1) stays
        // interior/subordinate; a genuine second-centre outpost (dist ≥ G)
        // graduates; real neighbours (~9 hexes away) are never crowded. The
        // parent falls out naturally, no special-case.
        int g = 1 + cfg.Expansion.GraduationMarginHexes;
        int bindingDist = 0, minSlack = int.MaxValue;
        bool anyPort = false;
        foreach (var p in state.Ports)                    // id order (P6)
        {
            // mirror EncroachedPolities' Entered guard (with the same bounds
            // safety SettleOps uses): only a real, entered port projects a core
            // the gate must clear. The parent port is NOT exempted — it counts
            // like any other, so a near-parent outpost stays interior; foreign
            // ports count too (the gate excludes no domain).
            if (p.OwnerActorId < 0 || p.OwnerActorId >= state.Actors.Count
                || !state.Actors[p.OwnerActorId].Entered) continue;
            anyPort = true;
            int dist = HexGrid.Distance(p.Hex, outpost.Hex);
            int slack = dist - g;
            if (slack < minSlack) { minSlack = slack; bindingDist = dist; }
        }
        // no entered port anywhere → nothing to clash with → vacuously frontier.
        if (!anyPort) return new FrontierStanding(true, 0, 0, int.MaxValue);
        // frontier iff dist >= G for EVERY port ⇔ min(dist − G) >= 0.
        return new FrontierStanding(minSlack >= 0, bindingDist, g, minSlack);
    }
}

/// <summary>An outpost's standing against the frontier gate (design §4),
/// described by the BINDING port — the nearest entered port core.
/// <paramref name="IsFrontier"/> is the candidacy verdict; the remaining
/// fields drive the REPL "interior vs frontier" display and T3.2's scoring.
/// For a graduated outpost every field is zero and <c>IsFrontier</c> is false;
/// for an outpost with no entered port anywhere <c>IsFrontier</c> is true and
/// <c>Slack</c> is <see cref="int.MaxValue"/>.</summary>
/// <param name="IsFrontier">Candidacy-eligible: not graduated and clear of
/// every port core by the anti-adjacency spacing G.</param>
/// <param name="PortDistance">Hex distance to the binding (nearest) port core.</param>
/// <param name="Threshold">The uniform gate distance G = 1 + margin (default 2).</param>
/// <param name="Slack">PortDistance − Threshold; &gt;= 0 iff frontier.</param>
public readonly record struct FrontierStanding(
    bool IsFrontier, int PortDistance, int Threshold, int Slack);
