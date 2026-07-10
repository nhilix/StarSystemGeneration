using System.Text;
using static System.FormattableString;

namespace StarGen.Core.Epoch;

/// <summary>Deterministic text rendering of a stepped sim — the phase trace
/// plus the chronicle. The REPL prints it; the determinism gate byte-compares
/// it. Every interpolation renders invariant-culture (negative coordinates
/// would otherwise pick up culture negative signs); fixed iteration order; no
/// timing or environment.</summary>
public static class SimTraceView
{
    public static string Render(SimState state)
    {
        var sb = new StringBuilder();
        var sim = state.Config.Sim;
        sb.AppendLine(Invariant($"epoch frame — seed {state.Config.MasterSeed} · ")
            + Invariant($"{state.EpochIndex} epochs stepped × {sim.YearsPerEpoch}y = ")
            + Invariant($"{state.WorldYear} world-years"));

        sb.AppendLine(Invariant($"actors: {state.Actors.Count}"));
        foreach (var a in state.Actors)
        {
            int ports = 0, topTier = 0;
            foreach (var p in state.Ports)
                if (p.OwnerActorId == a.Id)
                {
                    ports++;
                    if (p.Tier > topTier) topTier = p.Tier;
                }
            sb.AppendLine(Invariant($"  #{a.Id} {a.Name} ({a.Kind}) — seat ")
                + Invariant($"({a.Seat.Q},{a.Seat.R}), enters epoch {a.EntryEpoch} ")
                + Invariant($"(y{a.EntryEpoch * sim.YearsPerEpoch})")
                + (a.Entered
                    ? Invariant($" — {ports} ") + (ports == 1 ? "port" : "ports")
                      + Invariant($", top tier {topTier}")
                    : " [not yet entered]"));
        }
        sb.AppendLine(Invariant($"registries: {state.Ports.Count} ports · ")
            + Invariant($"{state.Lanes.Count} lanes · {state.Segments.Count} segments"));

        int lastEpoch = -1;
        foreach (var t in state.Trace)
        {
            if (t.Epoch != lastEpoch)
            {
                lastEpoch = t.Epoch;
                int y0 = t.Epoch * sim.YearsPerEpoch;
                sb.AppendLine();
                sb.AppendLine(Invariant($"epoch {t.Epoch} · y{y0}–y{y0 + sim.YearsPerEpoch}"));
            }
            sb.AppendLine(Invariant($"  {t.Phase,-10} {t.Note}"));
        }

        sb.AppendLine();
        sb.AppendLine(Invariant($"chronicle ({state.Log.Events.Count} events)"));
        foreach (var e in state.Log.Events)
            sb.AppendLine("  " + Describe(e));
        return sb.ToString();
    }

    public static string Describe(WorldEvent e)
    {
        string what = e.Payload switch
        {
            DwarfGalaxyMergedPayload p =>
                $"the {p.Name} Merger begins — a dwarf galaxy falls in",
            AgnIgnitedPayload p =>
                Invariant($"the galactic nucleus ignites — a sterilizing wave ")
                + Invariant($"sweeps the inner {p.WaveRadiusCells} rings"),
            GlobularFormedPayload p =>
                $"the {p.Name} Cluster condenses, ancient and metal-poor",
            FirstLifePayload => "first life stirs in the galaxy",
            SapienceEmergedPayload p =>
                Invariant($"a sapient species awakens (origin #{p.OriginId})"),
            SpaceflightReachedPayload p =>
                Invariant($"origin #{p.OriginId} reaches spaceflight"),
            PrecursorWaveRosePayload p =>
                $"the {p.Name} civilization rises "
                + $"({((Galaxy.VigorClass)p.VigorClass).ToString().ToLowerInvariant()} wave)",
            PrecursorWaveFellPayload p =>
                $"the {p.Name} civilization "
                + (Galaxy.WaveEndCause)p.EndCause switch
                {
                    Galaxy.WaveEndCause.War => "shatters in war",
                    Galaxy.WaveEndCause.CascadeCollapse => "collapses into silence",
                    Galaxy.WaveEndCause.Transcendence => "transcends, leaving empty halls",
                    Galaxy.WaveEndCause.Plague => "dies of plague",
                    _ => "is absorbed",
                }
                + Invariant($" ({p.ExtentCells} cells of ruins)"),
            PrecursorContactPayload p =>
                Invariant($"precursor civilizations #{p.WaveAId} and #{p.WaveBId} meet — ")
                + p.Resolution switch
                { 0 => "war", 1 => "absorption", _ => "an ancient border is drawn" },
            PolityEmergedPayload p => $"{p.PolityName} enters the galactic stage",
            PortEstablishedPayload p =>
                Invariant($"{p.PolityName} establishes a port (#{p.PortId})"),
            LaneOpenedPayload p =>
                Invariant($"a lane opens between ports #{p.PortAId} and #{p.PortBId}"),
            PortTierRaisedPayload p =>
                Invariant($"port #{p.PortId} rises to tier {p.NewTier}"),
            FamineStruckPayload p =>
                Invariant($"famine grips port #{p.PortId} ")
                + Invariant($"({(int)System.Math.Round(p.Shortfall * 100)}% short)"),
            FacilityBuiltPayload p => Invariant($"a ")
                + Substrate.Infrastructure.Get((Substrate.InfraTypeId)p.TypeId)
                    .Name.ToLowerInvariant()
                + Invariant($" rises (facility #{p.FacilityId})"),
            LoanIssuedPayload p =>
                Invariant($"polity #{p.LenderActorId} lends ")
                + Invariant($"{(int)System.Math.Round(p.Principal)} credits ")
                + Invariant($"to polity #{p.BorrowerActorId}"),
            LoanDefaultedPayload p =>
                Invariant($"polity #{p.BorrowerActorId} defaults on its debt ")
                + Invariant($"to polity #{p.LenderActorId}"),
            MigrationWavePayload p =>
                Invariant($"refugees flee port #{p.FromPortId} ")
                + Invariant($"for port #{p.ToPortId}"),
            ShipClassLaunchedPayload p =>
                Invariant($"the {p.Name} Mk {p.Mark} class launches ")
                + Invariant($"(design #{p.DesignId})"),
            FleetAttritionPayload p =>
                Invariant($"fleet #{p.FleetId} loses {p.HullsLost} ")
                + (p.HullsLost == 1 ? "hull" : "hulls") + " to failed supply",
            ConvoyDispatchedPayload p =>
                Invariant($"a convoy (fleet #{p.FleetId}) departs port ")
                + Invariant($"#{p.FromPortId} for ({p.TargetQ},{p.TargetR})"),
            CorporationCharteredPayload p =>
                (CorporateNiche)p.Niche == CorporateNiche.Cartel
                    ? $"the {p.Name} opens its black books — chartered by no one"
                    : Invariant($"polity #{p.HostPolityId} charters the {p.Name} (")
                      + ((CorporateNiche)p.Niche).ToString().ToLowerInvariant()
                      + ")",
            PirateBandFormedPayload p =>
                $"the {p.Name} raise the black flag over an unguarded lane",
            CorporationNationalizedPayload p =>
                Invariant($"polity #{p.PolityId} seizes the {p.Name} — ")
                + "a scandal on every lane",
            CorporationBankruptPayload p =>
                $"the {p.Name} collapses under its debts",
            NicheDiedPayload p =>
                $"the {p.Name} winds down, its niche gone",
            TechAdvancedPayload p =>
                Invariant($"polity #{p.PolityId} masters ")
                + ((TechDomain)p.Domain).ToString().ToLowerInvariant()
                + Invariant($" tier {p.NewTier}"),
            SchismDeclaredPayload p =>
                Invariant($"the {p.FactionName} leads {p.Ports} ")
                + (p.Ports == 1 ? "domain" : "domains")
                + Invariant($" out of polity #{p.OldPolityId} — ")
                + Invariant($"{p.NewPolityName} declares itself (polity #{p.NewPolityId})"),
            CoupStruckPayload p =>
                Invariant($"{p.CharacterName} of the {p.FactionName} seizes ")
                + Invariant($"power in polity #{p.PolityId}")
                + (p.Contested ? " — loyalists refuse the palace" : ""),
            RevoltCrushedPayload p =>
                Invariant($"the {p.FactionName} rises and is crushed in ")
                + Invariant($"polity #{p.PolityId}; {p.MartyrName} is martyred"),
            GovernmentReformedPayload p =>
                Invariant($"polity #{p.PolityId} is remade: ")
                + GovernmentForms.Get((GovernmentFormId)p.OldForm).Name
                + " gives way to "
                + GovernmentForms.Get((GovernmentFormId)p.NewForm).Name,
            FactionFormedPayload p =>
                Invariant($"the {p.Name} coalesces in polity #{p.PolityId} (")
                + ((FactionBasis)p.Basis).ToString().ToLowerInvariant()
                + " interest)",
            FactionDissolvedPayload p =>
                $"the {p.Name} disbands, its moment passed",
            RulerAscendedPayload p =>
                Invariant($"{p.CharacterName} takes the seat of polity ")
                + Invariant($"#{p.PolityId}")
                + (p.DynastyId >= 0
                    ? Invariant($" (house #{p.DynastyId})") : ""),
            CharacterDiedPayload p =>
                p.CharacterName + (CharacterRole)p.Role switch
                {
                    CharacterRole.Ruler => ", the ruler,",
                    CharacterRole.Heir => ", the heir,",
                    CharacterRole.Marshal => ", the marshal,",
                    CharacterRole.Commander => ", the commander,",
                    CharacterRole.FactionLeader => ", the faction leader,",
                    CharacterRole.Executive => ", the executive,",
                    _ => "",
                }
                + Invariant($" dies at {p.AgeYears}"),
            SuccessionCrisisPayload p =>
                Invariant($"the death of {p.DeadRulerName} leaves polity ")
                + Invariant($"#{p.PolityId} without a clear successor"),
            NotableEmergedPayload p =>
                p.CharacterName + (NotableType)p.NotableType switch
                {
                    NotableType.Founder => " is hailed as a founder",
                    NotableType.WarHero => " returns a war hero",
                    NotableType.Prophet => " speaks and crowds gather",
                    NotableType.PirateLord => " is whispered of on every lane",
                    NotableType.Magnate => " corners the market",
                    NotableType.Explorer => " returns from the ruins famous",
                    _ => " becomes notable",
                },
            FirstContactPayload p =>
                $"{p.PolityAName} and {p.PolityBName} make first contact",
            ClaimRaisedPayload p =>
                Invariant($"polity #{p.HolderPolityId} raises a ")
                + ClaimName((ClaimType)p.ClaimType)
                + Invariant($" claim against polity #{p.AgainstPolityId}"),
            ClaimReleasedPayload p =>
                Invariant($"polity #{p.HolderPolityId} lets its ")
                + ClaimName((ClaimType)p.ClaimType)
                + Invariant($" claim against polity #{p.AgainstPolityId} rest"),
            TreatySignedPayload p =>
                $"{p.PolityAName} and {p.PolityBName} sign a "
                + RungName((TreatyRung)p.Rung),
            TreatyBrokenPayload p =>
                $"{p.BreakerName} tears up its "
                + RungName((TreatyRung)p.Rung)
                + $" with {p.OtherName} — the galaxy hears",
            FederationFormedPayload p =>
                $"{p.ParentAName} and {p.ParentBName} fuse: the "
                + Invariant($"{p.NewPolityName} Federation is born ")
                + Invariant($"(polity #{p.NewPolityId})"),
            VassalageBoundPayload p =>
                $"{p.VassalName} kneels to {p.OverlordName} — "
                + "tribute for protection",
            VassalAbsorbedPayload p =>
                $"{p.OverlordName} quietly absorbs {p.VassalName}; "
                + "the old flag comes down without a shot",
            VassalSecededPayload p =>
                $"{p.VassalName} declares independence from a weakened "
                + p.OverlordName,
            WarDeclaredPayload p =>
                $"{p.AttackerName} declares {p.WarName} on {p.DefenderName} ("
                + CauseName((CasusBelli)p.Cause) + ")",
            BorderIncidentPayload p => p.Loaded
                ? $"a patrol clash between {p.PolityAName} and "
                  + $"{p.PolityBName} — fleets go to alert"
                : $"a border incident between {p.PolityAName} and "
                  + $"{p.PolityBName} fizzles into demands and apologies",
            BattleFoughtPayload p =>
                "battle in " + p.WarName
                + (p.AttackerCommanderName.Length > 0
                    ? $" — {p.AttackerCommanderName} leads the assault" : "")
                + (BattleOutcome)p.Outcome switch
                {
                    BattleOutcome.DecisiveAttacker => "; the defense breaks",
                    BattleOutcome.DecisiveDefender =>
                        (p.DefenderCommanderName.Length > 0
                            ? $"; {p.DefenderCommanderName} holds the line"
                            : "; the assault is repelled"),
                    BattleOutcome.Attrition => "; both lines bleed",
                    _ => "; neither line yields",
                }
                + Invariant($" ({p.AttackerLosses}+{p.DefenderLosses} hulls lost)"),
            SiegeBegunPayload p =>
                Invariant($"{p.AttackerName} lays siege to port #{p.PortId} (")
                + p.WarName + ")",
            PortCapturedPayload p =>
                Invariant($"port #{p.PortId} falls to {p.AttackerName}; ")
                + $"its people wake under a new flag ({p.WarName})",
            DynasticInstrumentPayload p =>
                (DynasticInstrument)p.Instrument == DynasticInstrument.Marriage
                    ? $"the houses of {p.FromName} and {p.ToName} marry"
                    : $"{p.FromName} sends a ward to the court of {p.ToName}",
            _ => e.Type.ToString(),
        };
        string family = e.Family.ToString().ToLowerInvariant();
        string vis = e.Visibility.ToString().ToLowerInvariant();
        return Invariant($"{YearLabel(e.WorldYear),-9} {family,-12} {what} ")
            + Invariant($"at ({e.Location.Q},{e.Location.R}) [{vis}]");
    }

    private static string CauseName(CasusBelli cause) => cause switch
    {
        CasusBelli.ResourceSeizure => "resource seizure",
        CasusBelli.ChokepointControl => "chokepoint control",
        CasusBelli.PunitiveInterdiction => "punitive response",
        CasusBelli.Crusade => "crusade",
        CasusBelli.Liberation => "liberation of kin",
        CasusBelli.Containment => "containment",
        CasusBelli.SuccessionClaim => "a claim of succession",
        CasusBelli.GrievanceDischarge => "old grievances",
        CasusBelli.VassalSecession => "independence",
        CasusBelli.BorderIncident => "a border incident",
        CasusBelli.CivilWar => "the throne itself",
        _ => "war",
    };

    private static string RungName(TreatyRung rung) => rung switch
    {
        TreatyRung.TradePact => "trade pact",
        TreatyRung.NonAggression => "non-aggression pact",
        TreatyRung.DefenseAlliance => "defense alliance",
        _ => "treaty",
    };

    private static string ClaimName(ClaimType type) => type switch
    {
        ClaimType.CulturalKin => "cultural-kin",
        ClaimType.LostTerritory => "lost-territory",
        ClaimType.Succession => "succession",
        ClaimType.Liberation => "liberation",
        _ => "standing",
    };

    /// <summary>World-year label at any zoom (P8): deep time reads in Gyr /
    /// Myr, the generational clock in plain years.</summary>
    public static string YearLabel(long worldYear) =>
        worldYear <= -1_000_000_000 ? Invariant($"{worldYear / 1e9:F2}Gy")
        : worldYear <= -1_000_000 ? Invariant($"{worldYear / 1e6:F0}My")
        : Invariant($"y{worldYear}");
}
