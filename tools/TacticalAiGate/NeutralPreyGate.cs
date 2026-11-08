// P38 (card t_11ebb843): the NEUTRALE-BEUTE census gate. It re-derives the
// bystander classification of EVERY shipped mission over the REAL production
// law (the TryBuildObjectEvents feed-entry grammar mirrored here over the
// shipped mission files + the CORE WorldEventVector polarity ladder + the
// Core NeutralPrey class) and verifies docs/CENSUS_beute_objekte.md line by
// line, fail-closed: every feed row must be listed in the census, the
// census must list no ghost, and the classification must match EXACTLY.
//
// Modes:
//   dotnet run -- ... census            -> print the current derivation table
//   dotnet run -- ... verify            -> assert the census document (default)
//
// The extensional golden of the census (the card's Pflicht-Zuordnung):
//   DRIN  (neutral bystander): Protester x9 (EMPIRE3), Refugee
//          Tassi/Camsir/Dumudu/Sheedro (OTHER11)
//   RAUS  (not neutral): Checkpoint R/G/B, Siege Wall, Weak Point, Support,
//          Beacon, Cell, Engine, Data Core, Barricade, Window, Security
//          Station, every Door/Terminal, the Warship highlight; the HOTH2
//          "Refugee 1-8" never enter the FEED at all (their printed sentence
//          is "A refugee can be attacked" - not the attack-sentence grammar;
//          the census proves this numerically, the card demanded the check).
//   Duty anchors are NOT feed objects (no parseable attack sentence on the
//   anchor entities) — the census counts 0 bystander rows among the P37
//   duty-anchor missions' escort targets by construction of the feed gate.
//
// Producer-fidelity notes (measured against WorldEventsUnity.TryBuildObjectEvents):
//   * CanonText == theText for the shipped files (no sourceText key exists
//     in any SagaMissions/*.json - scanned); the replay uses theText.
//   * Wired event names resolve through the eventGUID of each button action
//     (measured: shipped buttons carry the ALL-ZERO eventGUID; the replay
//     resolves normally - an unresolvable GUID yields no name, same law).
//   * objective = missionProperties.startingObjective (the MissionStart
//     baseline); objectiveForEmpire = false (the replay assumes the REBEL
//     campaign; EMPIRE3 is P1-classified, the flag cannot move it - pinned
//     below). P2 verdicts on Empire-campaign missions could flip jagt/
//     schuetzt, the BYSTANDER flag never depends on the objective.
//   * failImperial/failRebel are dead flags and are never read (P33-H4c law).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TacticalAI.Core;

public static class NeutralPreyGate
{
    static int fails = 0;
    static int passes = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (cond) passes++; else fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";
    static readonly string Missions = Path.Combine(App, "Assets/Resources/SagaMissions");
    static readonly string Census = Path.Combine(App, "../docs/CENSUS_beute_objekte.md");

    // ---------------- producer mirror (WorldEventsUnity P33-H4c/P38) --------
    static readonly string[] AttackWords = { "can attack", "angreifen" };
    static readonly string[] HealthKeys = { "health:", "gesundheit:" };
    static readonly string[] DefenseKeys = { "defense:", "verteidigung:" };
    static readonly string[] DefenseNoneWords = { "none", "keine" };
    static readonly string[] LoseRebels = { "rebels lose", "rebel lose", "rebellen verlieren" };
    static readonly string[] LoseEmpire = { "empire lose", "imperial lose", "imperials lose",
                                            "imperium verliert", "imperium verlieren" };
    static readonly string[] WinRebels = { "rebels win", "rebel win", "rebellen gewinn" };
    static readonly string[] WinEmpire = { "empire win", "imperial win", "imperials win", "imperium gewinn" };
    static readonly string[] ClaimWords = { "claim", "beanspruch", "anspruch" };
    static readonly string[] ClaimEmpireMarks = { "empire", "imperium" };
    static readonly string[] ClaimRebelMarks = { "rebel", "rebellen" };
    const int CM2_MissionManagement = 27;
    const int CM6_ModifyVariable = 31;

    static string S(Dictionary<string, object> d, string k)
    {
        object v;
        if (d == null || !d.TryGetValue(k, out v)) return "";
        return v == null ? "" : v.ToString();
    }
    static bool B(Dictionary<string, object> d, string k)
    {
        object v;
        if (d == null || !d.TryGetValue(k, out v)) return false;
        if (v is bool bb) return bb;
        var s = (v ?? "").ToString();
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
    static long L(Dictionary<string, object> d, string k)
    {
        object v;
        if (d == null || !d.TryGetValue(k, out v)) return 0;
        if (v is long ll) return ll;
        if (v is double dd) return (long)dd;
        long r; return long.TryParse((v ?? "").ToString(), out r) ? r : 0;
    }
    static Dictionary<string, object> D(object o) { var d = o as Dictionary<string, object>; return d ?? new Dictionary<string, object>(); }
    static List<object> A(object o) { var l = o as List<object>; return l ?? new List<object>(); }

    static bool TryParseAttackText(string text, out long health, out long defense, out bool hasDef)
    {
        health = -1; defense = -1; hasDef = false;
        if (string.IsNullOrEmpty(text)) return false;
        var t = text.ToLowerInvariant();
        int at = -1;
        foreach (var w in AttackWords)
        {
            int i = t.IndexOf(w, StringComparison.Ordinal);
            if (i >= 0 && (at < 0 || i < at)) at = i;
        }
        if (at < 0) return false;
        int winEnd = Math.Min(t.Length, at + 160);
        var win = t.Substring(at, winEnd - at);
        bool hp = false;
        foreach (var k in HealthKeys)
            if (win.IndexOf(k, StringComparison.Ordinal) >= 0)
            { hp = TryParsePrintedInt(win, k, out health); break; }
        if (!hp) return false;
        long d;
        foreach (var k in DefenseKeys)
            if (win.IndexOf(k, StringComparison.Ordinal) >= 0)
            {
                if (TryParsePrintedDefense(win, k, out d)) { defense = d; hasDef = true; }
                break;
            }
        return true;
    }
    static bool TryParsePrintedInt(string win, string key, out long value)
    {
        value = -1;
        int i = win.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return false;
        i += key.Length;
        while (i < win.Length && (win[i] == ' ' || win[i] == '\t')) i++;
        if (i >= win.Length || win[i] < '0' || win[i] > '9') return false;
        int j = i;
        while (j < win.Length && win[j] >= '0' && win[j] <= '9') j++;
        return long.TryParse(win.Substring(i, j - i), NumberStyles.None,
                             CultureInfo.InvariantCulture, out value);
    }
    static bool TryParsePrintedDefense(string win, string key, out long value)
    {
        value = -1;
        int i = win.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return false;
        i += key.Length;
        while (i < win.Length && (win[i] == ' ' || win[i] == '\t')) i++;
        if (i >= win.Length) return false;
        if (win[i] >= '0' && win[i] <= '9')
        {
            int j = i;
            while (j < win.Length && win[j] >= '0' && win[j] <= '9') j++;
            return long.TryParse(win.Substring(i, j - i), NumberStyles.None,
                                 CultureInfo.InvariantCulture, out value);
        }
        foreach (var nw in DefenseNoneWords)
            if (win.Length >= i + nw.Length && string.CompareOrdinal(win, i, nw, 0, nw.Length) == 0)
            { value = 0; return true; }
        return false;
    }

    static string OutcomeSideOfText(string text)
    {
        var t = (text ?? "").ToLowerInvariant();
        if (t.Length == 0) return "";
        foreach (var s in LoseRebels) if (t.Contains(s)) return "rebels";
        foreach (var s in LoseEmpire) if (t.Contains(s)) return "empire";
        foreach (var s in WinRebels) if (t.Contains(s)) return "empire";
        foreach (var s in WinEmpire) if (t.Contains(s)) return "rebels";
        bool claim = false;
        foreach (var s in ClaimWords) if (t.Contains(s)) { claim = true; break; }
        if (claim)
        {
            foreach (var s in ClaimEmpireMarks) if (t.Contains(s)) return "rebels";
            foreach (var s in ClaimRebelMarks) if (t.Contains(s)) return "empire";
        }
        return "";
    }
    static bool HasEndAction(Dictionary<string, object> ev)
    {
        foreach (var a in A(ev == null ? null : (object)(ev.TryGetValue("eventActions", out var v) ? v : null)))
        {
            var ad = D(a);
            if (L(ad, "eventActionType") == CM2_MissionManagement && B(ad, "endMission")) return true;
        }
        return false;
    }
    static string OutcomeSideOneHop(List<Dictionary<string, object>> all, List<Dictionary<string, object>> wired)
    {
        var setters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in wired)
            foreach (var a in A(e.TryGetValue("eventActions", out var v) ? v : null))
            {
                var ad = D(a);
                if (L(ad, "eventActionType") != CM6_ModifyVariable) continue;
                foreach (var t in A(ad.TryGetValue("triggerList", out var tv) ? tv : null))
                {
                    var td = D(t);
                    var g = S(td, "triggerGUID");
                    if (g.Length == 0 || g == "00000000-0000-0000-0000-000000000000") continue;
                    if (L(td, "setValue") < 1 && L(td, "modifyValue") < 1) continue;
                    setters.Add(g);
                }
            }
        if (setters.Count == 0) return "";
        foreach (var e in all)
        {
            if (!HasEndAction(e)) continue;
            foreach (var tb in A(e.TryGetValue("additionalTriggers", out var av) ? av : null))
            {
                var td = D(tb);
                var g = S(td, "triggerGUID");
                if (g.Length == 0 || g == "00000000-0000-0000-0000-000000000000") continue;
                if (L(td, "triggerValue") < 1) continue;
                if (!setters.Contains(g)) continue;
                var side = OutcomeSideOfText(S(e, "name") + ". " + S(e, "eventText"));
                if (side != "") return side;
            }
        }
        return "";
    }

    // Build the feed rows of ONE mission exactly like the producer would.
    static List<FeedWorldView.MissionObjectFact> BuildRows(string path)
    {
        var root = D(TacticalAI.Core.Json.Parse(File.ReadAllText(path)));
        var all = new List<Dictionary<string, object>>();
        foreach (var o in A(root.TryGetValue("globalEvents", out var gv) ? gv : null)) all.Add(D(o));
        foreach (var s in A(root.TryGetValue("mapSections", out var sv) ? sv : null))
            foreach (var o in A(D(s).TryGetValue("missionEvents", out var mv) ? mv : null)) all.Add(D(o));
        var evByGuid = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach (var e in all)
        {
            var g = S(e, "GUID");
            if (g.Length > 0 && !evByGuid.ContainsKey(g)) evByGuid[g] = e;
        }
        var rows = new List<FeedWorldView.MissionObjectFact>();
        foreach (var o in A(root.TryGetValue("mapEntities", out var ents) ? ents : null))
        {
            var en = D(o);
            var ep = D(en.TryGetValue("entityProperties", out var epv) ? epv : null);
            long health, defense; bool hasDef;
            if (!TryParseAttackText(S(ep, "theText"), out health, out defense, out hasDef)) continue;
            var name = S(en, "name").Trim();
            if (name.Length == 0) continue;
            var f = new FeedWorldView.MissionObjectFact
            {
                Guid = S(en, "GUID"),
                Name = name,
                Health = health,
                Defense = hasDef ? defense : -1,
                EntityType = S(en, "entityType").Length > 0 ? EntityTypeOf(L(en, "entityType")) : "",
            };
            var sec = S(en, "mapSectionOwner");
            if (sec.Length > 0 && sec != "00000000-0000-0000-0000-000000000000") f.Section = sec;
            var btns = new List<string>(); var names = new List<string>();
            var wired = new List<Dictionary<string, object>>();
            string outcome = "";
            foreach (var bo in A(ep.TryGetValue("buttonActions", out var bv) ? bv : null))
            {
                var b = D(bo);
                var bt = S(b, "buttonText").Trim().ToLowerInvariant();
                if (bt.Length > 0 && !btns.Contains(bt)) btns.Add(bt);
                var g = S(b, "eventGUID");
                if (g.Length == 0 || g == "00000000-0000-0000-0000-000000000000") continue;
                Dictionary<string, object> ev;
                if (!evByGuid.TryGetValue(g, out ev)) continue;
                var evn = S(ev, "name").Trim().ToLowerInvariant();
                if (evn.Length > 0 && !names.Contains(evn)) names.Add(evn);
                if (!wired.Contains(ev)) wired.Add(ev);
                if (outcome == "" && HasEndAction(ev))
                    outcome = OutcomeSideOfText(S(ev, "name") + ". " + S(ev, "eventText"));
            }
            if (outcome == "" && wired.Count > 0) outcome = OutcomeSideOneHop(all, wired);
            if (btns.Count > 0) { btns.Sort(StringComparer.Ordinal); f.Buttons = string.Join(";", btns.ToArray()); }
            if (names.Count > 0) { names.Sort(StringComparer.Ordinal); f.EventNames = string.Join(";", names.ToArray()); }
            if (outcome != "") f.OutcomeSide = outcome;
            rows.Add(f);
        }
        return rows;
    }

    // Saga.EntityType { Tile, Terminal, Crate, DeploymentPoint, Token, Highlight, Door }
    static readonly string[] EntityTypeNames =
        { "tile", "terminal", "crate", "deploymentpoint", "token", "highlight", "door" };
    static string EntityTypeOf(long v)
    {
        if (v < 0 || v >= EntityTypeNames.Length) return "unknown" + v;
        return EntityTypeNames[v];
    }

    // The REAL Core law (WorldEventVector ladder + NeutralPrey bystander).
    static void Derive(List<FeedWorldView.MissionObjectFact> rows, string objective)
    {
        foreach (var o in rows)
            WorldEventVector.DeriveObjectPolarity(o, objective, false);
    }

    class Row { public string Mission, Guid8, Name, EntityType, Pol, Basis; public bool Bystander; }

    static List<Row> DeriveAll()
    {
        var outp = new List<Row>();
        var files = Directory.GetFiles(Missions, "*.json", SearchOption.AllDirectories)
                             .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                             .ThenBy(p => p, StringComparer.Ordinal);
        foreach (var p in files)
        {
            var doc = D(TacticalAI.Core.Json.Parse(File.ReadAllText(p)));
            var mp = D(doc.TryGetValue("missionProperties", out var mpv) ? mpv : null);
            var objective = S(mp, "startingObjective");
            var rows = BuildRows(p);
            Derive(rows, objective);
            foreach (var f in rows)
                outp.Add(new Row
                {
                    Mission = Path.GetFileName(p),
                    Guid8 = f.Guid.Length >= 8 ? f.Guid.Substring(0, 8) : f.Guid,
                    Name = f.Name,
                    EntityType = f.EntityType,
                    Pol = f.Polaritaet, Basis = f.Basis,
                    Bystander = f.Bystander,
                });
        }
        return outp;
    }

    // ---------------- fixture generator (prey_core.json) -------------------
    // P38 (card t_11ebb843): mines the REAL EMPIRE3 mission file through the
    // producer mirror + the REAL Core laws and emits the harness fixture.
    // Feed rows carry the true GUIDs/sections (read, never hardcoded); the
    // quota/threat vector pins are the Core law's own output (golden-rerun
    // discipline, P37c) — an independent Python twin (tools/p38_prey_twin.py)
    // re-derives the dice and cross-checks the pins.
    static string J(string s)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (var c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\n') sb.Append("\\n");
            else if (c < 32) sb.Append(" ");
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    static void EmitRow(System.Text.StringBuilder sb, FeedWorldView.MissionObjectFact o, string indent)
    {
        var parts = new List<string>();
        parts.Add("\"entity\": " + J(o.Guid));
        parts.Add("\"name\": " + J(o.Name));
        if (o.Section.Length > 0) parts.Add("\"section\": " + J(o.Section));
        if (o.Position.Length > 0) parts.Add("\"position\": " + J(o.Position));
        parts.Add("\"health\": " + o.Health.ToString(CultureInfo.InvariantCulture));
        if (o.Defense >= 0) parts.Add("\"defense\": " + o.Defense.ToString(CultureInfo.InvariantCulture));
        if (o.Buttons.Length > 0) parts.Add("\"buttons\": " + J(o.Buttons));
        if (o.EventNames.Length > 0) parts.Add("\"eventNames\": " + J(o.EventNames));
        if (o.OutcomeSide.Length > 0) parts.Add("\"outcomeSide\": " + J(o.OutcomeSide));
        if (o.EntityType.Length > 0) parts.Add("\"entityType\": " + J(o.EntityType));
        sb.Append(indent).Append("{ ").Append(string.Join(", ", parts)).Append(" }");
    }

    static void GenFixture()
    {
        var missionPath = Path.Combine(Missions, "Empire", "EMPIRE3.json");
        var doc = D(TacticalAI.Core.Json.Parse(File.ReadAllText(missionPath)));
        var mp = D(doc.TryGetValue("missionProperties", out var mpv) ? mpv : null);
        var objective = S(mp, "startingObjective");
        var rows = BuildRows(missionPath);
        Derive(rows, objective);
        var prey = rows.Where(r => r.Bystander && r.Polaritaet == "jagt" && r.EntityType == "token")
                       .OrderBy(r => r.Guid, StringComparer.Ordinal).ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"_comment\": \"P38 (card t_11ebb843) NEUTRALE-BEUTE vectors. Feed rows mined from the REAL Empire/EMPIRE3.json through the producer mirror (gate 'gen' mode) - true GUIDs/sections, read not hardcoded. Quota/threat pins are the Core law's own output (golden rerun, P37c discipline); the independent Python twin tools/p38_prey_twin.py re-derives every dice value and the ranking. Hand-added synthesis: the unit-id cohort (patterns hunter/aggressive/etc. as fixture roles), the HeroDeployed threat events (fixture sections). Null-Law: prey-only=false fixtures keep P36.\",\n");

        // ---- em3 feed: the full EMPIRE3 object feed (prey + devices) -------
        sb.Append("  \"em3\": {\n");
        sb.Append("    \"missionFile\": ").Append(J("EMPIRE3.json")).Append(",\n");
        sb.Append("    \"objective\": ").Append(J(objective)).Append(",\n");
        sb.Append("    \"expectPrey\": ").Append(prey.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("    \"protesterGuids\": [");
        sb.Append(string.Join(", ", prey.Select(r => J(r.Guid)).ToArray()));
        sb.Append("],\n");
        sb.Append("    \"events\": [\n");
        for (int i = 0; i < rows.Count; i++)
        {
            sb.Append("      { \"kind\": \"MissionObject\", \"round\": 1, \"payload\": ");
            EmitRow(sb, rows[i], "        ");
            sb.Append(" }").Append(i == rows.Count - 1 ? "" : ",").Append('\n');
        }
        sb.Append("    ]\n  },\n");

        // ---- quota vectors: EMPIRE3-shaped cohorts over the real card ids --
        // Cohorts are synthetic unit-id rosters (the deployed figures are the
        // PLAYER's choice - not a mission-file fact); the card ids are the
        // REAL EMPIRE3 reserved-deployment cards (DG001 Stormtrooper,
        // DG047 Riot Trooper, DG049 Riot Trooper Elite - mined below).
        var cards = A(doc.TryGetValue("reservedDeploymentGroups", out var rgv) ? rgv : null)
            .Select(g => S(D(g), "cardID")).Where(c => c.Length > 0).Distinct().ToList();
        sb.Append("  \"cards_mined\": [").Append(string.Join(", ", cards.Select(J).ToArray())).Append("],\n");

        var cohorts = new[]
        {
            new { Name = "em3_n9",  Session = "saga|EMPIRE3",         Members = NineHunter("DG001", "DG047", "DG049") },
            new { Name = "em3_n2",  Session = "saga|EMPIRE3",         Members = TwoHunter() },
            new { Name = "no_chase", Session = "saga|EMPIRE3",        Members = NoChase() },
            new { Name = "salt",    Session = "solo|EMPIRE3",         Members = NineHunter("DG001", "DG047", "DG049") },
        };
        sb.Append("  \"quota_vectors\": [\n");
        var qv = new List<string>();
        foreach (var c in cohorts)
        {
            for (int round = 1; round <= 5; round++)
            {
                var hs = c.Members.Select(m => new PreyHunter { CardId = m.Id, Pattern = m.Pattern }).ToList();
                var pr = PreyQuota.Compute(c.Session, round, hs);
                qv.Add(c.Name + "|" + c.Session + "|" + round + "|" + (pr == null ? "" : pr.Explain));
                if (pr == null) continue;
            }
        }
        foreach (var line in qv)
            sb.Append("    ").Append(J(line)).Append(",\n");
        // trim trailing comma of last element
        sb.Length -= 2; sb.Append("\n  ],\n");

        // roster + sessions echoed for the harness rebuild
        sb.Append("  \"quota_cohorts\": [\n");
        foreach (var c in cohorts)
        {
            sb.Append("    { \"name\": ").Append(J(c.Name)).Append(", \"session\": ").Append(J(c.Session))
              .Append(", \"members\": [");
            sb.Append(string.Join(", ", c.Members.Select(m =>
                "{ \"id\": " + J(m.Id) + ", \"pattern\": " + J(m.Pattern) + " }").ToArray()));
            sb.Append("] },\n");
        }
        sb.Length -= 2; sb.Append("\n  ],\n");

        // ---- threat vectors: HeroDeployed fact anchors on fixture sections -
        var cases = new[]
        {
            new { Name = "unit_sec",  Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-a|1" } },
            new { Name = "prey_sec",  Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-b|1" } },
            new { Name = "none",      Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-c|1" } },
            new { Name = "multi_gap", Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-a|1", "HeroDeployed|Luke|sec-b|1" } },
            new { Name = "multi_back", Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-a|1", "HeroDeployed|Luke|sec-b|1",
                               "HeroDeployed|Luke|sec-a|2" } },
            new { Name = "two_heroes_prey", Unit = "sec-a", Prey = "sec-b",
                  Ev = new[] { "HeroDeployed|Luke|sec-a|1", "HeroDeployed|Han|sec-b|1" } },
            new { Name = "no_hero",   Unit = "sec-a", Prey = "sec-b", Ev = new string[0] },
        };
        sb.Append("  \"threat_vectors\": [\n");
        bool firstC = true;
        foreach (var c in cases)
        {
            var feed = new WorldEventFeed("vector|prey_threat|" + c.Name);
            foreach (var ev in c.Ev)
            {
                var p = ev.Split('|');
                // "dp" is the required identity key (P33-H4b: GUID, never a
                // coordinate). No graph exists in these vectors, so dp is
                // never consumed - a stable synthetic per section (the
                // harness replays the SAME synthesis; fixture plumbing,
                // not law).
                var payload = new Dictionary<string, object>
                { { "hero", p[1] }, { "section", p[2] }, { "dp", DpOf(p[2]) } };
                feed.Append(p[0], int.Parse(p[3], CultureInfo.InvariantCulture), payload);
            }
            var space = SectionSpace.Project(feed.Events, null, null, null, new SectionOptions { Refine = true });
            var got = PreyThreat.ThreatSectionOf(space, c.Unit, c.Prey) ?? "";
            sb.Append(firstC ? "    " : "    ,\n    ");
            firstC = false;
            sb.Append("{ \"name\": ").Append(J(c.Name))
              .Append(", \"unitSection\": ").Append(J(c.Unit))
              .Append(", \"preySection\": ").Append(J(c.Prey))
              .Append(", \"events\": [").Append(string.Join(", ", c.Ev.Select(J).ToArray()))
              .Append("], \"expect\": ").Append(J(got)).Append(" }");
        }
        sb.Append("\n  ],\n");

        // ---- lang inventory + anchor pins (P37d duty_popup discipline) -----
        // The 11 shipped ui.json must carry the three apPrey keys, non-empty;
        // De and En are the real translations (anchor fragments are hand-
        // picked REAL strings incl. the De umlaut 'Würfel'), the other nine
        // ship byte-exact EN copies (M6c fleet law). The anchors are pin
        // CONSTANTS here (hand-picked from the shipped keys), never read from
        // the asset - a silent text drift fails the harness loudly.
        var langNames = new[] { "Br", "De", "En", "Es", "Fr", "Hu", "It", "Nl", "No", "Pl", "Ru" };
        var preyKeys = new[] { "apPreyQuotaLine", "apPreyThreatLine", "apPreyThreatRel" };
        var deAnchors = new[] { "Objektjagd:", "Würfel", "Einheiten", "Bedrohung in Sektion", "derselben Sektion" };
        var enAnchors = new[] { "Prey hunt:", "seeded draw", "units chase", "Threat in section", "same section" };
        sb.Append("  \"lang\": {\n");
        sb.Append("    \"langs\": [").Append(string.Join(", ", langNames.Select(J)).ToArray()).Append("],\n");
        sb.Append("    \"keys\": [").Append(string.Join(", ", preyKeys.Select(J)).ToArray()).Append("],\n");
        sb.Append("    \"de_contains\": [").Append(string.Join(", ", deAnchors.Select(J)).ToArray()).Append("],\n");
        sb.Append("    \"en_contains\": [").Append(string.Join(", ", enAnchors.Select(J)).ToArray()).Append("]\n");
        sb.Append("  }\n}\n");

        var outPath = Path.Combine(App, "Assets/Tests/Fixtures/tacai/prey_core.json");
        File.WriteAllText(outPath, sb.ToString());
        Ok(true, "gen: wrote " + outPath + " (rows=" + rows.Count + " prey=" + prey.Count + ")");
    }

    class HunterSpec { public string Id; public string Pattern; }
    // Synthetic well-formed GUID per fixture section letter (see GenFixture
    // threat vectors - dp is only the required identity key, never consumed).
    static string DpOf(string section)
    {
        char c = section.Length > 0 ? section[section.Length - 1] : 'f';
        if (c < 'a' || c > 'f') c = 'f';
        var s = new string(c, 4);
        return s + s + "-" + s + "-4" + s.Substring(0, 3) + "-8" + s.Substring(0, 3)
             + "-" + s + s + s;
    }
    static List<HunterSpec> NineHunter(string a, string b, string c)
    {
        var l = new List<HunterSpec>();
        string[] patterns = { "hunter", "aggressive", "hunter", "hunter", "aggressive",
                              "aggressive", "hunter", "aggressive", "hunter" };
        string[] ids = { a + "#1", a + "#2", a + "#3", a + "#4", a + "#5",
                         b + "#1", b + "#2", c + "#1", c + "#2" };
        for (int i = 0; i < 9; i++) l.Add(new HunterSpec { Id = ids[i], Pattern = patterns[i] });
        return l;
    }
    static List<HunterSpec> TwoHunter() => new List<HunterSpec>
    {
        new HunterSpec { Id = "DG001#1", Pattern = "hunter" },
        new HunterSpec { Id = "DG047#1", Pattern = "aggressive" },
        new HunterSpec { Id = "DG049#1", Pattern = "guard" },   // not chase: filtered out
    };
    static List<HunterSpec> NoChase() => new List<HunterSpec>
    {
        new HunterSpec { Id = "DG006#1", Pattern = "guard" },
        new HunterSpec { Id = "DG006#2", Pattern = "support" },
    };

    public static void Run(string[] args)
    {
        bool censusMode = args != null && args.Contains("census");
        if (args != null && args.Contains("gen")) { GenFixture(); return; }
        var all = DeriveAll();

        // ---- golden invariants of the extensional assignment (card) --------
        int protest = all.Count(r => r.Mission == "EMPIRE3.json" && r.Name.StartsWith("Protester"));
        Ok(protest == 9, "census: EMPIRE3 Protester feed rows = 9 (found " + protest + ")");
        Ok(all.Count(r => r.Mission == "EMPIRE3.json" && r.Name.StartsWith("Protester")
                         && r.Bystander && r.Pol == "jagt" && r.EntityType == "token") == 9,
           "census: Protester x9 token+jagt+BYSTANDER (in scope)");
        var refu11 = all.Where(r => r.Mission == "OTHER11.json" && r.Name.StartsWith("Refugee")).ToList();
        Ok(refu11.Count == 4, "census: OTHER11 Refugee feed rows = 4 (found " + refu11.Count + ")");
        Ok(refu11.All(r => r.Bystander), "census: Refugee x4 BYSTANDER (person wiring 'Defeat'/'Reveal')");
        Ok(refu11.All(r => r.Pol == "neutral"), "census: Refugee x4 polaritaet neutral (P4 - no POLAR prey line, scope leg 1 fails)");
        Ok(!all.Any(r => r.Mission == "HOTH2.json" && r.Name.StartsWith("Refugee")),
           "census: HOTH2 Refugee 1-8 NOT in the feed (no attack-sentence grammar 'can be attacked')");
        // RAUS list (card): every named device + doors/terminals/highlight never bystander
        string[] rausBases = { "Barricade", "Beacon", "Cell", "Checkpoint", "Data Core", "Engine",
                               "Security Station", "Siege Wall", "Support", "Weak Point", "Window" };
        Ok(!all.Any(r => rausBases.Any(b => r.Name.StartsWith(b)) && r.Bystander),
           "census: no device row (Barricade..Window) is a bystander");
        Ok(!all.Any(r => (r.EntityType == "door" || r.EntityType == "terminal"
                          || r.EntityType == "highlight" || r.EntityType == "crate")
                         && r.Bystander),
           "census: no door/terminal/highlight/crate is a bystander (type leg locks them out)");
        var warship = all.Where(r => r.Name == "Warship").ToList();
        Ok(warship.Count >= 1 && warship.All(r => !r.Bystander),
           "census: Warship highlight never bystander (rows=" + warship.Count + ")");

        // ---- duty anchors never stand in the feed (card's numerical proof) --
        // The P37 duty-anchor objects (Gefangene, Cache, Wookiee ...) are NOT
        // attack-sentence objects: they do not even ENTER the feed (no
        // parseable attack sentence), so P38's class can never contain them.
        // Verified over the SHIPPED dutyAnchors artifact through the REAL
        // Core reader (DutyAnchors.ForMission, the P37 source of truth):
        // no feed row of ANY mission carries a duty anchor's name.
        {
            var artPath = Path.Combine(App, "Assets/Resources/Languages/En/tactical_mission_critical.json");
            var artDoc = TacticalAI.Core.Json.Parse(File.ReadAllText(artPath));
            int anchorTotal = 0, dutyMissionTotal = 0;
            var anchorNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var mRaw in A(((Dictionary<string, object>)artDoc).TryGetValue("missions", out var ms) ? ms : null))
            {
                var m = D(mRaw);
                var file = S(m, "file");
                if (file.Length == 0) continue;
                var duties = DutyAnchors.ForMission(artDoc, file);
                if (duties.Count == 0) continue;
                dutyMissionTotal++;
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var d in duties)
                {
                    foreach (var t in d.TargetFields) names.Add(t.Name);
                    foreach (var t in d.MarkerTokens) names.Add(t.Name);
                }
                anchorNames[file] = names;
                anchorTotal += names.Count;
            }
            Ok(dutyMissionTotal == 7, "census: duty missions from the shipped artifact via the Core reader = 7 (found " + dutyMissionTotal + ")");
            var bleed = all.Where(r => anchorNames.TryGetValue(r.Mission, out var ns) && ns.Contains(r.Name))
                           .Select(r => r.Mission + "/" + r.Name).ToList();
            Ok(bleed.Count == 0, "census: no duty anchor (" + anchorTotal + " names) stands in any feed (bleed=" + string.Join(",", bleed) + ")");
            var bleedB = all.Count(r => anchorNames.ContainsKey(r.Mission) && r.Bystander);
            Console.WriteLine("INFO census: bystander rows among the 7 duty missions = " + bleedB + " (duty branch untouched, no coupling)");
        }

        // ---- EMPIRE3 P2-flag pin: the objectiveForEmpire flag cannot move it -
        var e3 = BuildRows(Path.Combine(Missions, "Empire/EMPIRE3.json"));
        var mp3 = D(D(TacticalAI.Core.Json.Parse(File.ReadAllText(Path.Combine(Missions, "Empire/EMPIRE3.json"))))
                    .TryGetValue("missionProperties", out var m3) ? m3 : null);
        Derive(e3, S(mp3, "startingObjective"));
        bool allJagt = e3.All(r => r.Polaritaet == "jagt" && r.Basis == "P1");
        Ok(allJagt, "census: EMPIRE3 rows P1/jagt under the rebel-campaign flag (P1 buttons, flag-inert)");

        var lawFiles = new[] { "Core/NeutralPrey.cs", "Core/PreyQuota.cs" }
            .Select(f => Path.Combine(App, "Assets/Scripts/TacticalAI", f)).ToList();
        // ---- fairness scan: the law's STRING LAWS carry no mission names ----
        // (comments legitimately NAME the playtest missions as justification,
        // same line as DutyQuota.cs; the scan reads the literals only - no
        // literal may contain a mission id or an authored person.)
        var lit = new System.Text.RegularExpressions.Regex(
            @"""([^""\\\n]*)""");
        bool clean = lawFiles.All(f =>
        {
            foreach (System.Text.RegularExpressions.Match m in
                     lit.Matches(File.ReadAllText(f)))
            {
                var t = m.Groups[1].Value.ToUpperInvariant();
                if (t.Contains("EMPIRE3") || t.Contains("OTHER11") || t.Contains("HOTH2")
                    || t.Contains("PROTESTER") || t.Contains("REFUGEE") || t.Contains("TASSI")
                    || t.Contains("CAMSIR") || t.Contains("DUMUDU") || t.Contains("SHEEDRO"))
                    return false;
            }
            return true;
        });
        Ok(clean, "fairness: no law string-literal names a mission or an authored person");

        if (censusMode)
        {
            Console.WriteLine("== CENSUS TABLE (mission | entityType | polaritaet | basis | bystander | name) ==");
            foreach (var r in all)
                Console.WriteLine("| " + r.Mission + " | " + r.Name + " | " + r.Guid8 + " | "
                                  + r.EntityType + " | " + r.Pol + " | " + r.Basis + " | "
                                  + (r.Bystander ? "Ja" : "Nein") + " |");
            Console.WriteLine("== rows: " + all.Count + " ==");
            Console.WriteLine("NeutralPreyGate census printed (fails=" + fails + ")");
            if (fails > 0) Environment.ExitCode = 1;
            return;
        }

        // ---- verify the census document, fail-closed -----------------------
        if (!File.Exists(Census))
        {
            Ok(false, "census: docs/CENSUS_beute_objekte.md exists");
            Environment.ExitCode = 1;
            return;
        }
        var docRows = new Dictionary<string, string>(StringComparer.Ordinal);  // mission|guid8|name -> pol|basis|bystander|type
        foreach (var line in File.ReadAllLines(Census))
        {
            if (!line.StartsWith("| CENSUS ")) continue;
            var bits = line.Trim('|').Split('|');
            if (bits.Length < 7) { Ok(false, "census: malformed row: " + line); continue; }
            var mission = bits[0].Trim().Substring("CENSUS".Length).Trim();
            docRows[mission + "|" + bits[2].Trim() + "|" + bits[1].Trim()]
                = bits[3].Trim() + "|" + bits[4].Trim() + "|" + bits[5].Trim() + "|" + bits[6].Trim();
        }
        var liveRows = all.ToDictionary(r => r.Mission + "|" + r.Guid8 + "|" + r.Name,
                                        r => r.EntityType + "|" + r.Pol + "|" + r.Basis + "|" + (r.Bystander ? "Ja" : "Nein"));
        Ok(docRows.Count == liveRows.Count,
           "census: row count doc=" + docRows.Count + " live=" + liveRows.Count);
        int ghost = 0, drift = 0, missing = 0;
        foreach (var kv in docRows)
        {
            string live;
            if (!liveRows.TryGetValue(kv.Key, out live)) { ghost++; Console.WriteLine("  GHOST " + kv.Key); continue; }
            if (live != kv.Value) { drift++; Console.WriteLine("  DRIFT " + kv.Key + " doc=" + kv.Value + " live=" + live); }
        }
        foreach (var kv in liveRows)
            if (!docRows.ContainsKey(kv.Key)) { missing++; Console.WriteLine("  MISSING " + kv.Key); }
        Ok(ghost == 0, "census: doc lists no ghost row (ghosts=" + ghost + ")");
        Ok(missing == 0, "census: every live feed row is in the doc (missing=" + missing + ")");
        Ok(drift == 0, "census: doc classification matches the live derivation exactly (drifts=" + drift + ")");

        Console.WriteLine("NeutralPreyGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL") + " (checks=" + (passes + fails) + ")");
        if (fails > 0) Environment.ExitCode = 1;
    }
}
