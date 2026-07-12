using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;

namespace StarGen.Inspector;

public sealed class Repl
{
    private ulong _seed = 42;
    private int _spiralIndex;
    private GalaxyContext? _galaxy;
    private Core.Epoch.SimState? _sim;

    public void Run()
    {
        Console.WriteLine("StarGen inspector — 'help' for commands.");
        Show();
        while (true)
        {
            Console.Write("> ");
            var parts = (Console.ReadLine() ?? "quit")
                .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0].ToLowerInvariant())
            {
                case "quit" or "exit": return;
                case "help":
                    Console.WriteLine("seed <n> | galaxy <seed> [radiusCells] | goto <q> <r> | next | prev | reroll");
                    Console.WriteLine("find <criterion> | stats <n> | map [layer] | cell <q> <r>");
                    Console.WriteLine("epoch <seed> [epochs] [radiusCells] — run the seven-phase frame, print the phase/event trace");
                    Console.WriteLine("estep [n] [years] — step the loaded sim n more epochs (default 1); years overrides the");
                    Console.WriteLine("   integration step (fine tick: estep 25 1 plays a generation year by year)");
                    Console.WriteLine("emap [domains|lanes|traffic|price [good]|tech|war|tension] — political / lane / traffic / price / tech / war maps");
                    Console.WriteLine("polity [id] — the interior panel: form, legitimacy, reign, factions, tech, charters");
                    Console.WriteLine("characters [polityId] — the sparse living roster · bio <charId> — a life from the log (P8)");
                    Console.WriteLine("tech — per-polity domain tiers + progress · corps — the corporation registry");
                    Console.WriteLine("relations [polityId] — per-pair warmth/tension with live sources, bonds, claims");
                    Console.WriteLine("wars — every war ever declared · war <id> — one war's fronts, sieges, commanders, chronicle");
                    Console.WriteLine("market <portId> — one market's prices, inventory, black book, people, industry");
                    Console.WriteLine("fleet [id] — the fleet registry, or one fleet's composition + vectors + supply");
                    Console.WriteLine("designs [actorId] — ship design lineages (chassis cell, mark, grade)");
                    Console.WriteLine("fleetpost <fleetId> <posted|escort|patrol|blockade|reserve> [targetId] — debug posture override");
                    Console.WriteLine("equarantine <laneId> — issue the owner's QuarantineAct by hand (the player's verb, same rules)");
                    Console.WriteLine("elanes — every lane with gate tiers, owners, liveness, saturation");
                    Console.WriteLine("eprojects [actorId] — in-flight projects (one funder, or all); `eprojects all` adds completed/cancelled");
                    Console.WriteLine("eplan <actorId> — the actor's standing plan; `*` marks entries already in flight");
                    Console.WriteLine("chronicle [actorId|deep] — the era-annotated event log; one biography; or the deep-time strata only");
                    Console.WriteLine("chronicle place <q> <r> — everything that happened at one hex · eras — the detected eras");
                    Console.WriteLine("poi [id] — the anchored points of interest (battlefields, ruins, memorials, precursor sites)");
                    Console.WriteLine("belief <x> [y] — what polity x believes (vs truth) · news [id] — pulses in transit / one journey");
                    Console.WriteLine("stances [id] — reputation per audience · emap plague — the contagion layer");
                    Console.WriteLine("threads — the world in motion: loaded tensions, half-won wars, old thrones,");
                    Console.WriteLine("   leveraged corporations, burning plagues, unanswered offers (the handoff surface)");
                    Console.WriteLine("watch <seed> [radius] [epochs] [frameMs] — the whole story as one in-place animation:");
                    Console.WriteLine("   cosmic gas → life + precursor waves → political domains, every sim step a frame");
                    Console.WriteLine("gwatch [cosmic|life] [layer] [every N] — one genesis clock, in-place animated");
                    Console.WriteLine("   cosmic layers: gas | stars | metals · life layers: life | bio | waves");
                    Console.WriteLine("ewatch [n] [layer] — step n epochs, the emap layer animating in place");
                    Console.WriteLine("features — the cosmic feature registry (mergers, globulars, nebulae, AGN epochs)");
                    Console.WriteLine("precursors [waveId] — the precursor registry, or one wave's arc + typed sites");
                    Console.WriteLine("esave <path> | eload <path> — the layer-sectioned world-state artifact");
                    Console.WriteLine("edsave <base> <delta> | edload <base> <delta> — the delta boundary: a save is the base");
                    Console.WriteLine("   artifact + what the live game changed + the log's continuation");
                    Console.WriteLine("knobs [filter] — every calibration dial: name, live value, doc (see docs/TUNING.md)");
                    Console.WriteLine("goods — the 17-good catalog, grade bands, demand profiles");
                    Console.WriteLine("infra [q r] — the facility catalog + potentials/siting for sample cells (or a galaxy cell)");
                    Console.WriteLine("map layers: density | lean | gas | metal | age | minerals | bio | emergence | features");
                    Console.WriteLine("find criteria: overlay | <overlay-id> | settled | sapient");
                    break;
                case "seed" when parts.Length == 2 && ulong.TryParse(parts[1], out var s):
                    _seed = s;
                    _galaxy = null;
                    Console.WriteLine("seed set — back to flatspace (galaxy cleared)");
                    Show(); break;
                case "galaxy" when parts.Length >= 2 && ulong.TryParse(parts[1], out var gseed):
                {
                    int size = parts.Length >= 3 && int.TryParse(parts[2], out var sz) ? sz : 21;
                    var config = new GalaxyConfig { MasterSeed = gseed, GalaxyRadiusCells = size };
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var skeleton = SkeletonBuilder.Build(config);
                    sw.Stop();
                    _seed = gseed;
                    _galaxy = new GalaxyContext(config) { Skeleton = skeleton };
                    int nonVoid = 0, chokepoints = 0, homeworlds = 0;
                    foreach (var c in skeleton.Cells)
                    {
                        if (c.IsChokepoint) chokepoints++;
                        foreach (var a in c.Anchors)
                            if (a.Type == AnchorType.Homeworld) homeworlds++;
                        if (!c.IsVoid) nonVoid++;
                    }
                    Console.WriteLine($"galaxy built in {sw.ElapsedMilliseconds} ms: {skeleton.Cells.Count} cells "
                        + $"({nonVoid} non-void), {chokepoints} chokepoints, "
                        + $"{homeworlds} homeworld anchors, {skeleton.Species.Count} species");
                    break;
                }
                case "cell" when parts.Length == 3 && _galaxy?.Skeleton is { } sk
                        && int.TryParse(parts[1], out var qcx) && int.TryParse(parts[2], out var qcy):
                {
                    var cellCoord = new HexCoordinate(qcx, qcy);
                    if (!sk.TryGetCell(cellCoord, out var cell))
                    { Console.WriteLine("cell out of range"); break; }
                    Console.WriteLine($"cell [{qcx},{qcy}] density {cell.MeanDensity:F2}"
                        + (cell.IsVoid ? " VOID" : "") + (cell.IsChokepoint ? " CHOKEPOINT" : "")
                        + $" · {cell.Lean} · metallicity {cell.Metallicity:F2}");
                    foreach (var a in cell.Anchors)
                        Console.WriteLine($"  anchor: {a.Type} at [{a.Hex.Q:D4}-{a.Hex.R:D4}]"
                            + (a.SpeciesId >= 0 ? $" (species {sk.Species[a.SpeciesId].Name})" : ""));
                    Console.WriteLine(GalaxyMapView.CellZoom(_galaxy, cellCoord));
                    break;
                }
                case "epoch" when parts.Length >= 2 && ulong.TryParse(parts[1], out var eseed):
                {
                    var econfig = new Core.Epoch.EpochSimConfig { MasterSeed = eseed };
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var epochs))
                        econfig.Sim.EpochCount = epochs;
                    int eradius = parts.Length >= 4 && int.TryParse(parts[3], out var er) ? er : 21;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var eskeleton = SkeletonBuilder.Build(
                        new GalaxyConfig { MasterSeed = eseed, GalaxyRadiusCells = eradius });
                    var estate = Core.Epoch.EpochGenesis.Seed(eskeleton, econfig);
                    new Core.Epoch.EpochEngine().Run(estate);
                    sw.Stop();
                    _sim = estate;
                    _seed = eseed;
                    _galaxy = new GalaxyContext(eskeleton.Config) { Skeleton = eskeleton };
                    Console.WriteLine(Core.Epoch.SimTraceView.Render(estate));
                    Console.WriteLine($"stepped in {sw.ElapsedMilliseconds} ms");
                    break;
                }
                case "emap" or "estep" or "market" or "fleet"
                    or "designs" or "fleetpost" or "polity" or "characters"
                    or "bio" or "tech" or "corps" or "relations" or "wars"
                    or "war" when _sim == null:
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "polity" when parts.Length >= 2 && int.TryParse(parts[1], out var pid):
                    Console.WriteLine(InteriorView.RenderPolity(_sim!, pid));
                    break;
                case "polity":
                    foreach (var a in _sim!.Actors)
                        if (a.Kind == Core.Epoch.ActorKind.Polity && a.Entered)
                            Console.WriteLine(InteriorView.RenderPolity(_sim!, a.Id));
                    break;
                case "characters" when parts.Length >= 2 && int.TryParse(parts[1], out var cpid):
                    Console.WriteLine(InteriorView.RenderCharacters(_sim!, cpid));
                    break;
                case "characters":
                    Console.WriteLine(InteriorView.RenderCharacters(_sim!));
                    break;
                case "bio" when parts.Length >= 2 && int.TryParse(parts[1], out var cid):
                    Console.WriteLine(InteriorView.RenderBiography(_sim!, cid));
                    break;
                case "bio":
                    Console.WriteLine("bio <characterId> — see 'characters' for ids");
                    break;
                case "tech":
                    Console.WriteLine(InteriorView.RenderTech(_sim!));
                    break;
                case "relations" when parts.Length >= 2 && int.TryParse(parts[1], out var rpid):
                    Console.WriteLine(InterpolityView.RenderRelations(_sim!, rpid));
                    break;
                case "relations":
                    Console.WriteLine(InterpolityView.RenderRelations(_sim!));
                    break;
                case "wars":
                    Console.WriteLine(InterpolityView.RenderWars(_sim!));
                    break;
                case "war" when parts.Length >= 2 && int.TryParse(parts[1], out var wid):
                    Console.WriteLine(InterpolityView.RenderWar(_sim!, wid));
                    break;
                case "war":
                    Console.WriteLine("usage: war <id> (see `wars`)");
                    break;
                case "corps":
                    Console.WriteLine(InteriorView.RenderCorporations(_sim!));
                    break;
                case "fleet" when parts.Length >= 2 && int.TryParse(parts[1], out var fid):
                    Console.WriteLine(FleetView.RenderOne(_sim!, fid));
                    break;
                case "fleet":
                    Console.WriteLine(FleetView.RenderAll(_sim!));
                    break;
                case "designs":
                {
                    int actor = parts.Length >= 2 && int.TryParse(parts[1], out var da)
                        ? da : -1;
                    Console.WriteLine(FleetView.RenderDesigns(_sim!, actor));
                    break;
                }
                case "fleetpost" when parts.Length >= 3
                        && int.TryParse(parts[1], out var pfid):
                {
                    if (pfid < 0 || pfid >= _sim!.Fleets.Count)
                    { Console.WriteLine($"no fleet #{pfid}"); break; }
                    Core.Epoch.FleetPosture? posture = parts[2].ToLowerInvariant() switch
                    {
                        "posted" => Core.Epoch.FleetPosture.Posted,
                        "escort" => Core.Epoch.FleetPosture.Escort,
                        "patrol" => Core.Epoch.FleetPosture.Patrol,
                        "blockade" => Core.Epoch.FleetPosture.Blockade,
                        "reserve" => Core.Epoch.FleetPosture.Reserve,
                        _ => null,
                    };
                    if (posture == null)
                    { Console.WriteLine($"unknown posture '{parts[2]}'"); break; }
                    var fl = _sim.Fleets[pfid];
                    fl.Posture = posture.Value;
                    fl.TargetId = parts.Length >= 4 && int.TryParse(parts[3], out var tid)
                        ? tid : -1;
                    Console.WriteLine($"fleet #{pfid} -> {posture}"
                        + (fl.TargetId >= 0 ? $" (target {fl.TargetId})" : "")
                        + " — note: the posture manager reassigns freight/escort"
                        + " hulls next Allocation; blockade severs the port's lanes"
                        + " next market step");
                    break;
                }
                case "fleetpost":
                    Console.WriteLine("usage: fleetpost <fleetId> <posted|escort|patrol|blockade|reserve> [targetId]");
                    break;
                case "equarantine" when parts.Length >= 2 && _sim != null
                        && int.TryParse(parts[1], out var qLane):
                {
                    // the player's hand on a polity verb (slice J eyeball):
                    // issue the same QuarantineAct the AI issues, resolved
                    // by the same rule — the lane owner closes it
                    if (qLane < 0 || qLane >= _sim.Lanes.Count)
                    { Console.WriteLine($"no lane #{qLane}"); break; }
                    int owner = _sim.Ports[_sim.Lanes[qLane].PortAId].OwnerActorId;
                    bool held = Core.Epoch.PlagueOps.Quarantine(_sim,
                        new Core.Epoch.QuarantineAct(owner, qLane));
                    Console.WriteLine(held
                        ? FormattableString.Invariant(
                            $"lane #{qLane} quarantined by #{owner} until ")
                          + FormattableString.Invariant(
                            $"y{_sim.Lanes[qLane].QuarantinedUntil} — freight, ")
                          + "migration, and contagion stop next step"
                        : "no effect (already held longer, or ownership refused)");
                    break;
                }
                case "equarantine":
                    Console.WriteLine("usage: equarantine <laneId> — issue the owner's QuarantineAct by hand (see `emap lanes`)");
                    break;
                case "elanes" when _sim != null:
                {
                    if (_sim.Lanes.Count == 0)
                    { Console.WriteLine("no lanes yet"); break; }
                    foreach (var lane in _sim.Lanes)
                    {
                        var a = _sim.Ports[lane.PortAId];
                        var b = _sim.Ports[lane.PortBId];
                        int dist = Core.Galaxy.HexGrid.Distance(a.Hex, b.Hex);
                        var gA = lane.GateAId >= 0
                            ? _sim.Facilities[lane.GateAId] : null;
                        var gB = lane.GateBId >= 0
                            ? _sim.Facilities[lane.GateBId] : null;
                        bool live = Core.Epoch.LaneMath.IsLive(_sim, lane);
                        string OwnerOf(Core.Epoch.Facility? g) => g == null
                            ? "—" : _sim.Actors[g.OwnerActorId].Name;
                        Console.WriteLine(FormattableString.Invariant(
                            $"#{lane.Id,3} {a.Id,3}<->{b.Id,-3} {dist,3}hx ")
                            + $"T{gA?.Tier ?? 0}/{gB?.Tier ?? 0} "
                            + (live ? "live" : "DEAD")
                            + (lane.QuarantinedUntil >= _sim.WorldYear
                                ? " quarantined" : "")
                            + (lane.SaturatedYears > 0
                                ? FormattableString.Invariant(
                                    $" sat {lane.SaturatedYears}y") : "")
                            + $"  gates: {OwnerOf(gA)} / {OwnerOf(gB)}");
                    }
                    break;
                }
                case "elanes":
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "eprojects" when _sim != null:
                {
                    bool includeAll = parts.Length >= 2
                        && string.Equals(parts[1], "all", StringComparison.OrdinalIgnoreCase);
                    int funder = -1;
                    for (int i = 1; i < parts.Length; i++)
                        if (int.TryParse(parts[i], out var pf)) { funder = pf; break; }
                    RenderProjects(_sim, funder, includeAll);
                    break;
                }
                case "eprojects":
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "eplan" when parts.Length >= 2 && _sim != null
                        && int.TryParse(parts[1], out var planActorId):
                    RenderPlan(_sim, planActorId);
                    break;
                case "eplan" when _sim != null:
                    Console.WriteLine("usage: eplan <actorId> (see `polity` for ids)");
                    break;
                case "eplan":
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "emap":
                {
                    string layer = parts.Length >= 2 ? parts[1] : "domains";
                    var good = Core.Substrate.GoodId.Provisions;
                    if (layer == "price" && parts.Length >= 3
                        && !TryParseGood(parts[2], out good))
                    { Console.WriteLine($"unknown good '{parts[2]}' — see `goods`"); break; }
                    Console.WriteLine(EpochMapView.Render(_sim!, layer, good));
                    break;
                }
                case "estep":
                {
                    int n = parts.Length >= 2 && int.TryParse(parts[1], out var en)
                        ? Math.Max(1, en) : 1;
                    // the fine-tick variant (slice J): the SAME machine, a
                    // smaller integration step — estep 20 5 steps 20 times
                    // at 5 world-years each (play-tick resumability)
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var ey)
                        && ey >= 1)
                    {
                        _sim!.Config.Sim.YearsPerEpoch = ey;
                        Console.WriteLine(FormattableString.Invariant(
                            $"integration step set to {ey} world-year")
                            + (ey == 1 ? "" : "s")
                            + FormattableString.Invariant(
                            $" (generation stays {_sim.Config.Sim.GenerationYears})"));
                    }
                    var engine = new Core.Epoch.EpochEngine();
                    int traceFrom = _sim!.Trace.Count;
                    for (int i = 0; i < n; i++) engine.Step(_sim);
                    for (int i = traceFrom; i < _sim.Trace.Count; i++)
                    {
                        var t = _sim.Trace[i];
                        Console.WriteLine(FormattableString.Invariant(
                            $"  e{t.Epoch} {t.Phase,-10} {t.Note}"));
                    }
                    Console.WriteLine(FormattableString.Invariant(
                        $"now at epoch {_sim.EpochIndex} (y{_sim.WorldYear})"));
                    break;
                }
                case "market" when parts.Length == 2 && int.TryParse(parts[1], out var mp):
                    Console.WriteLine(MarketView.Render(_sim!, mp));
                    break;
                case "market":
                    Console.WriteLine("usage: market <portId>");
                    break;
                case "lanecut":
                    Console.WriteLine("superseded (slice H): interdiction is real now — "
                        + "`fleetpost <fleetId> blockade <portId>` stations a squadron and "
                        + "severs every lane at that port's approaches");
                    break;
                case "chronicle" or "eras" or "poi" or "belief" or "news"
                    or "stances" or "threads" when _sim == null:
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "belief" when parts.Length >= 2 && int.TryParse(parts[1], out var bObs):
                    Console.WriteLine(NarrativeView.RenderBeliefs(_sim!, bObs,
                        parts.Length >= 3 && int.TryParse(parts[2], out var bSub)
                            ? bSub : -1));
                    break;
                case "belief":
                    Console.WriteLine("usage: belief <observerId> [subjectId] — what a polity believes vs the truth");
                    break;
                case "news" when parts.Length >= 2 && int.TryParse(parts[1], out var pulse):
                    Console.WriteLine(NarrativeView.RenderNews(_sim!, pulse));
                    break;
                case "news":
                    Console.WriteLine(NarrativeView.RenderNews(_sim!));
                    break;
                case "stances" when parts.Length >= 2 && int.TryParse(parts[1], out var sObs):
                    Console.WriteLine(NarrativeView.RenderStances(_sim!, sObs));
                    break;
                case "stances":
                    Console.WriteLine(NarrativeView.RenderStances(_sim!));
                    break;
                case "eras":
                    Console.WriteLine(NarrativeView.RenderEras(_sim!));
                    break;
                case "threads":
                    Console.WriteLine(NarrativeView.RenderThreads(_sim!));
                    break;
                case "poi" when parts.Length >= 2 && int.TryParse(parts[1], out var poiId):
                    Console.WriteLine(NarrativeView.RenderPoi(_sim!, poiId));
                    break;
                case "poi":
                    Console.WriteLine(NarrativeView.RenderPois(_sim!));
                    break;
                case "chronicle" when parts.Length >= 4 && parts[1] == "place"
                        && int.TryParse(parts[2], out var cq)
                        && int.TryParse(parts[3], out var cr):
                    // the per-place view: everything that happened HERE —
                    // the archaeology surface (chronicle-and-poi.md §Indexes)
                    Console.Write(NarrativeView.RenderChronicle(_sim!,
                        _sim!.Log.AtPlace(new Core.Model.HexCoordinate(cq, cr))));
                    break;
                case "chronicle":
                {
                    bool deepOnly = parts.Length >= 2 && parts[1] == "deep";
                    int filter = !deepOnly && parts.Length >= 2
                        && int.TryParse(parts[1], out var cf) ? cf : -1;
                    if (deepOnly)
                    {
                        int shown = 0;
                        foreach (var e in _sim!.Log.Events)
                        {
                            if (e.Stratum is not (Core.Epoch.ClockStratum.Cosmic
                                or Core.Epoch.ClockStratum.Evolutionary)) continue;
                            Console.WriteLine("  " + Core.Epoch.SimTraceView.Describe(e));
                            shown++;
                        }
                        if (shown == 0) Console.WriteLine("  (no events)");
                        break;
                    }
                    var events = filter >= 0 ? _sim!.Log.ForActor(filter) : _sim!.Log.Events;
                    Console.Write(NarrativeView.RenderChronicle(_sim!, events));
                    break;
                }
                case "watch" when parts.Length >= 2 && ulong.TryParse(parts[1], out var wseed):
                {
                    // the whole story as one in-place animation: cosmic gas,
                    // then life + precursor waves, then political domains —
                    // every simulation step is a frame over the same map
                    int wradius = parts.Length >= 3 && int.TryParse(parts[2], out var wrr) ? wrr : 12;
                    int wepochs = parts.Length >= 4 && int.TryParse(parts[3], out var wee) ? wee : 40;
                    int frameMs = parts.Length >= 5 && int.TryParse(parts[4], out var wms) ? wms : 30;
                    var wconfig = new GalaxyConfig { MasterSeed = wseed, GalaxyRadiusCells = wradius };
                    var animator = new FrameAnimator(frameMs);
                    int gEvery = animator.InPlace ? 1 : 40;   // pipes get samples
                    try
                    {
                        var skeleton = SkeletonBuilder.Build(wconfig,
                            f =>
                            {
                                if ((f.Step + 1) % gEvery == 0 || f.Step == f.StepCount - 1)
                                    animator.Frame(GenesisWatchView.CosmicFrameText(f, "gas"));
                            },
                            f =>
                            {
                                if ((f.Step + 1) % gEvery == 0 || f.Step == f.StepCount - 1)
                                    animator.Frame(GenesisWatchView.EvoFrameText(f, "life"));
                            });
                        var wecfg = new Core.Epoch.EpochSimConfig { MasterSeed = wseed };
                        wecfg.Sim.EpochCount = wepochs;
                        var westate = Core.Epoch.EpochGenesis.Seed(skeleton, wecfg);
                        var wengine = new Core.Epoch.EpochEngine();
                        int eEvery = animator.InPlace ? 1 : 10;
                        for (int i = 0; i < wepochs; i++)
                        {
                            wengine.Step(westate);
                            // no header line: the emap legend already names the
                            // domains, and extra chrome only risks wrap drift
                            if ((i + 1) % eEvery == 0 || i == wepochs - 1)
                                animator.Frame(EpochMapView.Render(westate, "domains",
                                    Core.Substrate.GoodId.Provisions));
                        }
                        animator.Done();
                        _sim = westate;
                        _seed = wseed;
                        _galaxy = new GalaxyContext(wconfig) { Skeleton = skeleton };
                        Console.WriteLine($"watch complete — sim loaded at epoch {westate.EpochIndex} "
                            + "(emap/market/fleet/chronicle ready); the watched run is "
                            + "byte-identical to an unwatched one");
                    }
                    finally { animator.Done(); }
                    break;
                }
                case "watch":
                    Console.WriteLine("usage: watch <seed> [radiusCells=12] [epochs=40] [frameMs=30]");
                    break;
                case "gwatch":
                {
                    if (_galaxy == null)
                    { Console.WriteLine("build a galaxy first (galaxy <seed>)"); break; }
                    string clock = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "cosmic";
                    bool life = clock is "life" or "evo" or "evolution";
                    string wlayer = parts.Length >= 3 ? parts[2].ToLowerInvariant()
                        : life ? "life" : "gas";
                    var ganimator = new FrameAnimator(frameDelayMs: 30);
                    int every = parts.Length >= 4 && int.TryParse(parts[3], out var ev)
                        ? Math.Max(1, ev) : (ganimator.InPlace ? 1 : (life ? 25 : 10));
                    var wconfig = _galaxy.Config;
                    Console.WriteLine($"replaying genesis for seed {wconfig.MasterSeed} — "
                        + $"{(life ? "evolutionary" : "cosmic")} clock, layer {wlayer}, "
                        + $"every {every} steps (observation never changes the run)");
                    try
                    {
                        var wsw = System.Diagnostics.Stopwatch.StartNew();
                        var watched = SkeletonBuilder.Build(wconfig,
                            life ? null : f =>
                            {
                                if ((f.Step + 1) % every == 0 || f.Step == f.StepCount - 1)
                                    ganimator.Frame(GenesisWatchView.CosmicFrameText(f, wlayer));
                            },
                            life ? f =>
                            {
                                if ((f.Step + 1) % every == 0 || f.Step == f.StepCount - 1)
                                    ganimator.Frame(GenesisWatchView.EvoFrameText(f, wlayer));
                            } : null);
                        wsw.Stop();
                        ganimator.Done();
                        _galaxy = new GalaxyContext(wconfig) { Skeleton = watched };
                        Console.WriteLine($"genesis complete in {wsw.ElapsedMilliseconds} ms — "
                            + "byte-identical to the unwatched build");
                    }
                    finally { ganimator.Done(); }
                    break;
                }
                case "ewatch" when _sim == null:
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "ewatch":
                {
                    int n = parts.Length >= 2 && int.TryParse(parts[1], out var wn)
                        ? Math.Max(1, wn) : 5;
                    string wlayer = parts.Length >= 3 ? parts[2] : "domains";
                    var eanimator = new FrameAnimator(frameDelayMs: 30);
                    var wengine = new Core.Epoch.EpochEngine();
                    try
                    {
                        for (int i = 0; i < n; i++)
                        {
                            wengine.Step(_sim!);
                            eanimator.Frame(EpochMapView.Render(_sim!, wlayer,
                                Core.Substrate.GoodId.Provisions));
                        }
                    }
                    finally { eanimator.Done(); }
                    break;
                }
                case "features" when _galaxy?.Skeleton is { } fsk:
                {
                    if (fsk.Features.Count == 0)
                    { Console.WriteLine("  (no features — build a galaxy first?)"); break; }
                    foreach (var feat in fsk.Features)
                        Console.WriteLine(FormattableString.Invariant(
                            $"  #{feat.Id,-3} {feat.Type,-16} {feat.Name,-12} {Core.Epoch.SimTraceView.YearLabel((long)(feat.DateGyr * 1e9)),-9} {feat.Cells.Count} ")
                            + (feat.Cells.Count == 1 ? "cell" : "cells"));
                    break;
                }
                case "features":
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
                    break;
                case "precursors" when _galaxy?.Skeleton is { } psk
                        && parts.Length >= 2 && int.TryParse(parts[1], out var waveId):
                {
                    if (waveId < 0 || waveId >= psk.PrecursorWaves.Count)
                    { Console.WriteLine($"no wave #{waveId}"); break; }
                    var w = psk.PrecursorWaves[waveId];
                    Console.WriteLine(FormattableString.Invariant(
                        $"  the {w.Name} civilization — {w.Class} wave, vigor {w.Vigor:F2}"));
                    Console.WriteLine(FormattableString.Invariant(
                        $"  rose {Core.Epoch.SimTraceView.YearLabel(w.RoseYear)}, fell {Core.Epoch.SimTraceView.YearLabel(w.FellYear)} — {w.EndCause}"));
                    Console.WriteLine(FormattableString.Invariant(
                        $"  capital ({w.CapitalHex.Q},{w.CapitalHex.R}) · {w.Cells.Count} cells · {w.Lanes.Count} lanes")
                        + (w.DescendantOriginId >= 0
                            ? FormattableString.Invariant(
                                $" · machine descendant: origin #{w.DescendantOriginId}") : ""));
                    foreach (var site in w.Sites)
                        Console.WriteLine(FormattableString.Invariant(
                            $"    {site.Type,-20} at ({site.Hex.Q},{site.Hex.R})")
                            + (site.Dormant ? "  [DORMANT]" : "")
                            + (site.OtherWaveId >= 0
                                ? FormattableString.Invariant(
                                    $"  (with wave #{site.OtherWaveId})") : ""));
                    break;
                }
                case "precursors" when _galaxy?.Skeleton is { } psk2:
                {
                    if (psk2.PrecursorWaves.Count == 0)
                    { Console.WriteLine("  (no precursor waves)"); break; }
                    foreach (var w in psk2.PrecursorWaves)
                        Console.WriteLine(FormattableString.Invariant(
                            $"  #{w.Id,-3} {w.Name,-12} {w.Class,-7} rose {Core.Epoch.SimTraceView.YearLabel(w.RoseYear),-9} fell {Core.Epoch.SimTraceView.YearLabel(w.FellYear),-9} {w.EndCause,-16} {w.Cells.Count,3} cells {w.Sites.Count,3} sites")
                            + (w.DescendantOriginId >= 0 ? "  → machine descendant" : ""));
                    Console.WriteLine("  (precursors <id> for one wave's arc + sites)");
                    break;
                }
                case "precursors":
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
                    break;
                case "esave" when parts.Length == 2 && _sim != null:
                    try
                    {
                        System.IO.File.WriteAllText(parts[1],
                            Core.Epoch.ArtifactSerializer.ToText(_sim));
                        Console.WriteLine($"artifact saved to {parts[1]}");
                    }
                    catch (System.IO.IOException ex) { Console.WriteLine($"cannot save: {ex.Message}"); }
                    catch (UnauthorizedAccessException ex) { Console.WriteLine($"cannot save: {ex.Message}"); }
                    break;
                case "esave":
                    Console.WriteLine("run a sim first (epoch <seed>), then: esave <path>");
                    break;
                case "edsave" when parts.Length == 3 && _sim != null:
                    // the delta boundary (handoff.md): a save = the base
                    // artifact + what the live game changed + the log's
                    // continuation — genesis strata never re-record
                    try
                    {
                        string baseText = System.IO.File.ReadAllText(parts[1]);
                        string delta = Core.Epoch.DeltaSerializer.Diff(
                            baseText, Core.Epoch.ArtifactSerializer.ToText(_sim));
                        System.IO.File.WriteAllText(parts[2], delta);
                        Console.WriteLine(FormattableString.Invariant(
                            $"delta saved to {parts[2]} ({delta.Length:N0} chars ")
                            + FormattableString.Invariant(
                            $"against a {baseText.Length:N0}-char base)"));
                    }
                    catch (System.IO.IOException ex) { Console.WriteLine($"cannot save: {ex.Message}"); }
                    catch (UnauthorizedAccessException ex) { Console.WriteLine($"cannot save: {ex.Message}"); }
                    break;
                case "edsave":
                    Console.WriteLine("usage: edsave <basePath> <deltaPath> (needs a loaded sim)");
                    break;
                case "edload" when parts.Length == 3:
                    try
                    {
                        string baseText = System.IO.File.ReadAllText(parts[1]);
                        string full = Core.Epoch.DeltaSerializer.Apply(
                            baseText, System.IO.File.ReadAllText(parts[2]));
                        using (var reader = new System.IO.StringReader(full))
                        {
                            var loaded = Core.Epoch.ArtifactSerializer.Load(reader);
                            _sim = loaded;
                            _seed = loaded.Skeleton.Config.MasterSeed;
                            _galaxy = new GalaxyContext(loaded.Skeleton.Config)
                            { Skeleton = loaded.Skeleton };
                            Console.WriteLine($"base + delta loaded: seed {_seed}, "
                                + $"epoch {loaded.EpochIndex} (y{loaded.WorldYear}), "
                                + $"{loaded.Log.Events.Count} events");
                        }
                    }
                    catch (System.IO.InvalidDataException ex) { Console.WriteLine($"refused: {ex.Message}"); }
                    catch (System.IO.IOException) { Console.WriteLine("file not found"); }
                    catch (UnauthorizedAccessException ex) { Console.WriteLine($"cannot load: {ex.Message}"); }
                    break;
                case "edload":
                    Console.WriteLine("usage: edload <basePath> <deltaPath>");
                    break;
                case "eload" when parts.Length == 2:
                    try
                    {
                        using (var reader = new System.IO.StreamReader(parts[1]))
                        {
                            var loaded = Core.Epoch.ArtifactSerializer.Load(reader);
                            _sim = loaded;
                            _seed = loaded.Skeleton.Config.MasterSeed;
                            _galaxy = new GalaxyContext(loaded.Skeleton.Config)
                            { Skeleton = loaded.Skeleton };
                            Console.WriteLine($"artifact loaded: seed {_seed}, "
                                + $"epoch {loaded.EpochIndex} (y{loaded.WorldYear}), "
                                + $"{loaded.Ports.Count} ports, {loaded.Lanes.Count} lanes, "
                                + $"{loaded.Log.Events.Count} events");
                        }
                    }
                    catch (System.IO.InvalidDataException ex) { Console.WriteLine($"refused: {ex.Message}"); }
                    catch (System.IO.IOException) { Console.WriteLine("file not found"); }
                    catch (UnauthorizedAccessException ex) { Console.WriteLine($"cannot load: {ex.Message}"); }
                    break;
                case "goods":
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderGoods());
                    break;
                case "knobs":
                {
                    var config = _sim?.Config ?? new Core.Epoch.EpochSimConfig();
                    string filter = parts.Length >= 2 ? parts[1] : "";
                    int shown = 0;
                    foreach (var knob in Core.Epoch.KnobRegistry.All)
                    {
                        if (filter.Length > 0 && knob.Name.IndexOf(filter,
                                StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Console.WriteLine(FormattableString.Invariant(
                            $"  {knob.Name,-42} {knob.Get(config),10:0.####}  {knob.Doc}"));
                        shown++;
                    }
                    Console.WriteLine(shown == 0
                        ? $"no knobs match '{filter}'"
                        : $"{shown} knobs" + (_sim == null
                            ? " (defaults — no sim loaded)"
                            : " (live values of the loaded sim)"));
                    break;
                }
                case "infra" when parts.Length == 3 && _galaxy?.Skeleton is { } isk
                        && int.TryParse(parts[1], out var iq) && int.TryParse(parts[2], out var ir):
                {
                    if (!isk.TryGetCell(new HexCoordinate(iq, ir), out var icell))
                    { Console.WriteLine("cell out of range"); break; }
                    var fields = new Core.Substrate.CellFields(
                        icell.MeanDensity, icell.Lean, icell.Metallicity,
                        icell.Anchors.Any(a => a.Type == AnchorType.MineralRich),
                        icell.Anchors.Any(a => a.Type == AnchorType.PrecursorSite));
                    // connectivity/port context are epoch-sim state — neutral
                    // wilds here; workforce from the homeworld anchor if present
                    var site = new Core.Substrate.CellSite(fields, Connectivity: 0.3,
                        IsPortHeart: false, PortTier: 0,
                        DevelopmentTier: 0, IsChokepoint: icell.IsChokepoint);
                    var homeAnchor = icell.Anchors.FirstOrDefault(a => a.Type == AnchorType.Homeworld);
                    var workforce = homeAnchor != null && homeAnchor.SpeciesId >= 0
                        ? isk.Species[homeAnchor.SpeciesId].Embodiment
                        : Embodiment.TerranAnalog;
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderSite(
                        FormattableString.Invariant($"cell [{iq},{ir}]"), fields, site, workforce));
                    break;
                }
                case "infra" when parts.Length == 3:
                    Console.WriteLine("no galaxy loaded (or coords unparseable) — build one with: galaxy <seed>");
                    break;
                case "infra":
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderInfra());
                    break;
                case "goto" when parts.Length == 3
                        && int.TryParse(parts[1], out var gx) && int.TryParse(parts[2], out var gy):
                    _spiralIndex = GalaxyEnumerator.SpiralIndexOf(new HexCoordinate(gx, gy)); Show(); break;
                case "next": Step(+1); Show(); break;
                case "prev": Step(-1); Show(); break;
                case "reroll":
                    _seed = (ulong)Guid.NewGuid().GetHashCode() * 2654435761UL;
                    Console.WriteLine($"seed = {_seed}"); Show(); break;
                case "find" when parts.Length == 2: Find(parts[1]); break;
                case "stats" when parts.Length == 2 && int.TryParse(parts[1], out var n):
                    Console.WriteLine(StatsReport.Build(_galaxy ?? GalaxyContext.Flatspace(_seed), _spiralIndex, n));
                    break;
                case "map" when _galaxy?.Skeleton == null:
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
                    break;
                case "map":
                    Console.WriteLine(GalaxyMapView.CellMap(_galaxy!.Skeleton!,
                        parts.Length >= 2 ? parts[1] : "density"));
                    break;
                default:
                    Console.WriteLine("unrecognized — try 'help'"); break;
            }
        }
    }

    /// <summary>`eprojects` (Task 13): the in-flight-project table — the
    /// honest ETA under CURRENT starvation, not the naive one. Owner and
    /// funder can differ (corp-built gates on a host polity's port); the
    /// filter is by funder, since that's whose treasury is drawn.</summary>
    private static void RenderProjects(Core.Epoch.SimState sim, int funderFilter, bool includeAll)
    {
        var rows = new System.Collections.Generic.List<Core.Epoch.Project>();
        foreach (var p in sim.Projects)
        {
            if (!includeAll && !p.InFlight) continue;
            if (funderFilter >= 0 && p.FunderActorId != funderFilter) continue;
            rows.Add(p);
        }
        if (rows.Count == 0)
        {
            Console.WriteLine("no projects"
                + (includeAll ? "" : " in flight")
                + (funderFilter >= 0 ? $" for actor #{funderFilter}" : ""));
            return;
        }
        Console.WriteLine("  id  kind                  owner            port  pri      fed%  delivered/req yrs   eta");
        foreach (var p in rows)
        {
            string owner = p.OwnerActorId >= 0 && p.OwnerActorId < sim.Actors.Count
                ? sim.Actors[p.OwnerActorId].Name : "—";
            string status = p.Completed ? " (done)" : p.Cancelled ? " (cancelled)" : "";
            string eta = p.Completed || p.Cancelled ? "—"
                : FormattableString.Invariant($"y{sim.WorldYear + (int)Math.Ceiling(
                    (p.YearsRequired - p.YearsDelivered) / Math.Max(p.LastFedFraction, 0.05))}");
            Console.WriteLine(FormattableString.Invariant(
                $"  #{p.Id,-3} {p.Kind,-21} {owner,-16} #{p.PortId,-4} {p.Priority,-8} ")
                + FormattableString.Invariant(
                $"{p.LastFedFraction * 100,4:0}%  {p.YearsDelivered,6:0.0}/{p.YearsRequired,-6:0.0} ")
                + eta + status);
        }
    }

    /// <summary>`eplan` (Task 13): the actor's standing plan — `*` marks an
    /// entry already broken ground on (same kind+port+type still in flight),
    /// so the reader can see what the plan still owes versus what's live.</summary>
    private static void RenderPlan(Core.Epoch.SimState sim, int actorId)
    {
        if (actorId < 0 || actorId >= sim.Actors.Count)
        { Console.WriteLine($"no actor #{actorId}"); return; }
        if (sim.Actors[actorId].Policies is not Core.Epoch.PolityPolicies policies)
        { Console.WriteLine($"actor #{actorId} ({sim.Actors[actorId].Name}) has no standing plan"); return; }
        var plan = policies.Plan;
        if (plan.Entries.Count == 0)
        { Console.WriteLine($"actor #{actorId} ({sim.Actors[actorId].Name}) has no plan entries"); return; }
        Console.WriteLine("   #  kind        pri      start  type/design               port");
        for (int i = 0; i < plan.Entries.Count; i++)
        {
            var e = plan.Entries[i];
            bool inFlight = false;
            var matchKind = e.Kind switch
            {
                Core.Epoch.PlanEntryKind.Facility => Core.Epoch.ProjectKind.FacilityConstruction,
                Core.Epoch.PlanEntryKind.PortRaise => Core.Epoch.ProjectKind.PortRaise,
                Core.Epoch.PlanEntryKind.HullBatch => Core.Epoch.ProjectKind.HullBatch,
                _ => (Core.Epoch.ProjectKind)(-1),
            };
            foreach (var p in sim.Projects)
                if (p.InFlight && p.Kind == matchKind && p.PortId == e.PortId
                    && p.TypeId == e.TypeId)
                { inFlight = true; break; }
            string typeDesign = e.Kind switch
            {
                Core.Epoch.PlanEntryKind.Facility when e.TypeId >= 0 =>
                    ((Core.Substrate.InfraTypeId)e.TypeId).ToString(),
                Core.Epoch.PlanEntryKind.HullBatch when e.TypeId >= 0
                        && e.TypeId < sim.Designs.Count =>
                    $"{sim.Designs[e.TypeId].Name} Mk {sim.Designs[e.TypeId].Mark} x{e.Count}",
                Core.Epoch.PlanEntryKind.PortRaise => "(port raise)",
                _ => "—",
            };
            Console.WriteLine(FormattableString.Invariant(
                $"  {(inFlight ? "*" : " ")}{i,2}  {e.Kind,-11} {e.Priority,-8} y{e.StartYear,-6} ")
                + FormattableString.Invariant($"{typeDesign,-25} ")
                + FormattableString.Invariant($"#{e.PortId}"));
        }
    }

    private static bool TryParseGood(string text, out Core.Substrate.GoodId good)
    {
        if (int.TryParse(text, out int id)
            && id >= 0 && id < Core.Substrate.Goods.All.Count)
        { good = (Core.Substrate.GoodId)id; return true; }
        foreach (var def in Core.Substrate.Goods.All)
            if (string.Equals(def.Name.Replace(" ", ""), text,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.Id.ToString(), text,
                    StringComparison.OrdinalIgnoreCase))
            { good = def.Id; return true; }
        good = Core.Substrate.GoodId.Provisions;
        return false;
    }

    private void Step(int dir) => _spiralIndex = Math.Max(0, _spiralIndex + dir);

    private HexResult Gen(HexCoordinate c) =>
        Generator.Generate(_galaxy ?? GalaxyContext.Flatspace(_seed), c);

    private void Show() =>
        Console.WriteLine(SystemFormatter.Format(Gen(GalaxyEnumerator.SpiralAt(_spiralIndex))));

    private void Find(string criterion)
    {
        for (int i = 0; i < 50_000; i++)
        {
            Step(+1);
            var system = Gen(GalaxyEnumerator.SpiralAt(_spiralIndex)).System;
            if (system != null && Matches(system, criterion)) { Show(); return; }
        }
        Console.WriteLine($"no match for '{criterion}' within 50,000 hexes");
    }

    private static bool Matches(StarSystem s, string criterion) => criterion switch
    {
        "overlay" => s.OverlayId != null,
        "settled" => AnyBody(s, b => b.Settlement != Settlement.None),
        "sapient" => AnyBody(s, b => b.Biosphere == Biosphere.Sapient),
        _ => s.OverlayId == criterion,
    };

    private static bool AnyBody(StarSystem s, Func<Body, bool> pred)
    {
        foreach (var star in s.Stars)
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                if (pred(slot.Body)) return true;
                foreach (var sat in slot.Body.Satellites)
                    if (pred(sat)) return true;
            }
        return false;
    }
}
