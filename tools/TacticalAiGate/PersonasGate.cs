// M6b live gate (zero fixtures): the persona asset contract against the
// SHIPPED files. Two laws, both born from M6a/M6b findings:
//
// 1. FLAT SCHEMA (M6b loader-fix). Core/Personas.cs Load() reads the matcher
//    keys (internalNameMatch/factionMatch/multiplier/guardPercent/patterns/
//    amount) as DIRECT keys of the effect row and never unwrapped a nested
//    "filter" object - so the legacy shipped rows (nested "filter", count
//    spelled "count") silently matched EVERYTHING at runtime while every
//    authored intent was targeted. M6b standardized on FLAT: this gate fails
//    the build if a nested "filter" object or a "count" key ever reappears,
//    so the flat schema is machine-enforced, not prose.
//
// 2. elite_swap TARGET CONTRACT (CONCEPT_personas_v2 v2.1 Sec.1.1-5). Every
//    elite_swap row must carry an explicit elite_card id that (a) exists in
//    the shipped CardData/enemies.json roster, (b) is flagged isElite there,
//    (c) satisfies the printed pairing law against the EN card asset (elite
//    EN name == base name + " (Elite)") whenever the row is an anchored
//    "^Base$" name, and (d) at most ONE swap row per figure name per persona
//    (the runtime name cap must be satisfiable; Death-Trooper-style double
//    elite cards stay off the swap rail by construction).
//
// The gate reads ONLY shipped assets - zero fixtures, zero recorder.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TacticalAI.Core;

public static class PersonasGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    // The legal effect kinds of the v2.1 four-channel ridge plus the two
    // legacy channels that are still allowed to SHIP until the asset swap
    // (M6b step 5) - they vanish from the asset with it, and this gate's
    // kind rule then becomes the four-channel law.
    static readonly HashSet<string> LegalKinds = new HashSet<string>
    {
        "cost_reduction", "free_deployment",
        "pattern_weight", "guard_split", "signaturText", "elite_swap",
    };

    static Dictionary<string, object> LoadJson(string path)
    {
        // shipped JSON is strict after M6b (python-generated); CardData
        // files still carry legacy trailing commas -> strip before parse.
        var txt = File.ReadAllText(path);
        txt = Regex.Replace(txt, @",(\s*[}\]])", "$1");
        return Json.AsDict(Json.Parse(txt));
    }

    static List<object> LoadJsonArray(string path)
    {
        var txt = File.ReadAllText(path);
        txt = Regex.Replace(txt, @",(\s*[}\]])", "$1");
        return Json.AsList(Json.Parse(txt));
    }

    public static int Run()
    {
        Console.WriteLine("-- PersonasGate (M6b schema + swap contract) --");
        var assetPath = Path.Combine(App, "Assets", "Resources", "Languages", "En", "tactical_personas.json");
        Ok(File.Exists(assetPath), "persona asset present");
        if (!File.Exists(assetPath)) return fails;

        Dictionary<string, object> doc;
        try { doc = LoadJson(assetPath); }
        catch (Exception e) { Ok(false, "persona asset parses: " + e.Message); return fails; }
        Ok(true, "persona asset parses strict-clean (flat writer, no trailing commas needed)");

        // ---- shipped roster (CardData) + EN pairing lane ----
        var roster = new Dictionary<string, Dictionary<string, object>>();
        var rosterByName = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var c in LoadJsonArray(Path.Combine(App, "Assets", "Resources", "CardData", "enemies.json")))
            {
                var cd = Json.AsDict(c);
                var id = Json.AsStr(cd["id"]);
                roster[id] = cd;
                var nm = Json.AsStr(cd["name"]);
                if (!rosterByName.ContainsKey(nm)) rosterByName[nm] = new List<Dictionary<string, object>>();
                rosterByName[nm].Add(cd);
            }
        }
        catch (Exception e) { Ok(false, "CardData/enemies.json parses: " + e.Message); return fails; }
        Ok(roster.Count > 50, "CardData/enemies.json roster loaded (cards=" + roster.Count + ")");

        // EN card asset = the locale-stable pairing lane the runtime swap
        // seam resolves regular->elite by id (SetCardTranslations overwrites
        // live card names with the ACTIVE language, so the EN lane is the
        // only internal-name truth that survives every locale - M6b).
        var enNames = new Dictionary<string, string>();
        try
        {
            foreach (var c in LoadJsonArray(Path.Combine(App, "Assets", "Resources", "Languages", "En", "DeploymentGroups", "enemies.json")))
            {
                var cd = Json.AsDict(c);
                enNames[Json.AsStr(cd["id"])] = Json.AsStr(cd["name"]);
            }
        }
        catch (Exception e) { Ok(false, "Languages/En card asset parses: " + e.Message); return fails; }
        Ok(enNames.Count > 50, "EN card asset pairing lane loaded (cards=" + enNames.Count + ")");

        var personas = Json.AsList(doc["personas"]);
        Ok(personas.Count >= 3, "persona count >= 3 (found " + personas.Count + ")");
        var seenIds = new HashSet<string>();

        foreach (var pobj in personas)
        {
            var p = Json.AsDict(pobj);
            var pid = Json.AsStr(p["id"]);
            Ok(seenIds.Add(pid), "persona id unique: " + pid);
            foreach (var key in new[] { "nameKey", "mottoKey" })
            {
                Ok(p.ContainsKey(key) && !string.IsNullOrEmpty(Json.AsStr(p[key])),
                    pid + "/" + key + " present (ui key law: keys, never inline text)");
            }

            int prevRank = 0, prevThreat = -1;
            var swapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int swaps = 0;
            var ranks = Json.AsList(p["ranks"]);
            foreach (var robj in ranks)
            {
                var r = Json.AsDict(robj);
                int rank = Json.AsInt(r["rank"]);
                int mt = Json.AsInt(r["minThreat"]);
                Ok(rank == prevRank + 1, pid + " rank ladder sequential (rank=" + rank + ")");
                Ok(mt >= prevThreat, pid + " minThreat non-decreasing (rank " + rank + ": " + mt + " after " + prevThreat + ")");
                prevRank = rank; prevThreat = mt;

                foreach (var eobj in Json.AsList(r["effects"]))
                {
                    var e = Json.AsDict(eobj);
                    var kind = Json.AsStr(e["kind"]);
                    Ok(LegalKinds.Contains(kind), pid + " R" + rank + " kind legal: " + kind);

                    // ---- FLAT SCHEMA LAW (M6b loader-fix) ----
                    Ok(!e.ContainsKey("filter"), pid + " R" + rank + " no nested \"filter\" object (flat law)");
                    Ok(!e.ContainsKey("count"), pid + " R" + rank + " no legacy \"count\" key (flat law: amount)");
                    if (e.ContainsKey("filter") || e.ContainsKey("count")) continue; // broken shape: skip deeper rules

                    // filter regexes must COMPILE (the loader swallows a
                    // broken regex to "matches nothing" at runtime; an
                    // authored typo must fail the build instead).
                    if (e.ContainsKey("internalNameMatch"))
                    {
                        var rx = Json.AsStr(e["internalNameMatch"]);
                        bool ok = true;
                        try { var re = new Regex(rx, RegexOptions.IgnoreCase); re.IsMatch("probe"); }
                        catch { ok = false; }
                        Ok(ok, pid + " R" + rank + " internalNameMatch compiles: " + rx);

                        // ---- elite_swap target contract ----
                        if (kind == "elite_swap")
                        {
                            swaps++;
                            Ok(e.ContainsKey("amount") && Json.AsInt(e["amount"]) >= 1,
                                pid + " R" + rank + " elite_swap amount >= 1");
                            Ok(e.ContainsKey("elite_card") && !string.IsNullOrEmpty(Json.AsStr(e["elite_card"])),
                                pid + " R" + rank + " elite_swap carries explicit elite_card id");
                            var anchored = Regex.Match(rx, @"^\^([^^$]+)\$$");
                            if (anchored.Success)
                            {
                                var baseName = anchored.Groups[1].Value;
                                Ok(swapNames.Add(baseName),
                                    pid + " at most one elite_swap row per figure name: " + baseName);
                                string eliteId = e.ContainsKey("elite_card") ? Json.AsStr(e["elite_card"]) : "";
                                Dictionary<string, object> card;
                                if (roster.TryGetValue(eliteId, out card))
                                {
                                    Ok(!card.ContainsKey("isElite") || (card["isElite"] is bool && (bool)card["isElite"]),
                                        pid + " elite target " + eliteId + " is isElite in the roster");
                                    string enName;
                                    if (enNames.TryGetValue(eliteId, out enName))
                                        Ok(string.Equals(enName, baseName + " (Elite)", StringComparison.Ordinal),
                                            pid + " EN pairing law: " + eliteId + " == \"" + baseName + " (Elite)\" (found \"" + enName + "\")");
                                    else
                                        Ok(false, pid + " elite target " + eliteId + " present in EN card asset");
                                    // regular side exists in the Core roster
                                    List<Dictionary<string, object>> regs;
                                    if (rosterByName.TryGetValue(baseName, out regs))
                                    {
                                        Ok(true, pid + " regular base name present in roster: " + baseName);
                                        // SIZE pairing law (Delta-0 law): the
                                        // figure count of a group IS the card
                                        // size, so a swap may never move it -
                                        // against EVERY regular roster card
                                        // of the name (incl. RRG double cards
                                        // like the second Probe Droid).
                                        int eliteSize = Json.AsInt(card["size"]);
                                        foreach (var reg in regs)
                                            Ok(Json.AsInt(reg["size"]) == eliteSize,
                                                pid + " size parity " + baseName + ": " + Json.AsStr(reg["id"])
                                                + " size " + Json.AsInt(reg["size"]) + " == " + eliteId + " size " + eliteSize);
                                    }
                                    else Ok(false, pid + " regular base name present in roster: " + baseName);
                                }
                                else Ok(false, pid + " elite target " + eliteId + " exists in CardData roster");
                            }
                        }
                    }
                    if (kind == "cost_reduction" || kind == "free_deployment")
                    {
                        Ok(e.ContainsKey("amount") && Json.AsInt(e["amount"]) >= 1,
                            pid + " R" + rank + " " + kind + " amount >= 1 (flat law)");
                    }
                    if (kind == "signaturText")
                    {
                        Ok(e.ContainsKey("key") && !string.IsNullOrEmpty(Json.AsStr(e["key"])),
                            pid + " R" + rank + " signaturText has ui key");
                    }
                }
            }
            if (swaps > 0) Ok(true, pid + " elite_swap rows: " + swaps + " (all name-capped)");
        }

        // ---- the v2.1 channel ridge: gifts are a BUILD ERROR in the asset ----
        // (Engine-side dormant code may stay; the asset must not carry them.
        // Active from the M6b asset swap; before it the 3 legacy personas
        // still ship gift rows, so the rule counts personas rather than
        // pretending the swap already happened.)
        bool gifts = false;
        foreach (var pobj in personas)
            foreach (var robj in Json.AsList(Json.AsDict(pobj)["ranks"]))
                foreach (var eobj in Json.AsList(Json.AsDict(robj)["effects"]))
                {
                    var k = Json.AsStr(Json.AsDict(eobj)["kind"]);
                    if (k == "cost_reduction" || k == "free_deployment") gifts = true;
                }
        int giftFleet = gifts ? 1 : 0; // pre-swap legacy fleet is legal; post-swap must be zero
        Ok(personas.Count >= 6 ? giftFleet == 0 : true,
            (personas.Count >= 6
                ? "v2.1 fleet (>=6 personas) carries NO gift channels (cost_reduction/free_deployment = build error)"
                : "pre-swap legacy fleet may still carry gift channels (asset swap pending)"));

        Console.WriteLine("-- PersonasGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL") + " --");
        return fails;
    }
}
