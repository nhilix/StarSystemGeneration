namespace StarGen.Core.Epoch;

/// <summary>The four tech domains (economy/technology.md): per-polity tier
/// ladders gating ceilings and regions, not flat multipliers. Stable ids.</summary>
public enum TechDomain
{
    Industrial = 0,  // recipe variants, facility tiers, grade ceilings
    Military = 1,    // armament/warship stat regions, fortifications (H)
    Astrogation = 2, // port service radius, inter-port range, endurance
    Life = 3,        // medicine grade, agri productivity, growth
}

/// <summary>The provided interface (technology.md §Provided interface):
/// Ceiling(polity, domain) and Region(polity, domain), consumed by the Grade
/// system, recipe gating, ship design sheets, and the port growth axes.
/// Non-polity actors (corporations) read as the era-standard tier 2.</summary>
public static class Tech
{
    public const int EraStandardTier = 2;

    /// <summary>A polity's tier in a domain — the qualitative ladder rung.</summary>
    public static int Tier(SimState state, int actorId, TechDomain domain)
    {
        foreach (var pr in state.Polities)
            if (pr.ActorId == actorId) return pr.TechTier[(int)domain];
        return EraStandardTier;
    }

    /// <summary>The grade ceiling the domain's tier buys (Grades.TechCeiling
    /// is the qualitative ladder the Grade system requires).</summary>
    public static double Ceiling(SimState state, int actorId, TechDomain domain)
        => Substrate.Grades.TechCeiling(Tier(state, actorId, domain));

    /// <summary>The stat region for design sheets — the tier itself (the
    /// sheet derivation consumes it as its tech input).</summary>
    public static int Region(SimState state, int actorId, TechDomain domain)
        => Tier(state, actorId, domain);

    /// <summary>Progress needed to leave a tier: geometric investment
    /// thresholds (technology.md §Advancement).</summary>
    public static double Threshold(EpochSimConfig config, int fromTier) =>
        config.Tech.BaseThreshold
        * System.Math.Pow(config.Tech.ThresholdGrowth, fromTier - 1);
}
