// P37d duty briefing scrub gate (card t_ffc705af, concept 9.3 + 7
// Abhaengkeits-Gesetz). Mission list = the dutyAnchors artifact (NOT a global
// scrub - 9.3): the gate first re-reads the duty-7 from the shipped mining
// table, then counts, per duty mission and per SHIPPED LANGUAGE, the sentences
// the Core DutyScrub law removes from the briefing channels the runtime
// actually shows (missionInfo + eventText + ShowTextBox/ChangeMissionInfo
// text + prompt texts; languages without a mission injection read the EN
// canonical - the same text the player sees). Counts are compared against the
// hand-pinned INVENTAR below (measured 08.11. over all 138 canonical files +
// every shipped injection). Fail-closed:
//   - inventory mismatch            -> FAIL, the divergent sentence is printed
//   - QA-BLOCKED list non-empty     -> FAIL (concept 7: a legacy sentence that
//     cannot be scrubbed blocks the MISSION for review, never the duty block -
//     here that means: the gate says WHICH mission/language, duty stays on)
//   - pattern leak outside the      -> FAIL (other missions must stay
//     inventory                     byte-identical, 9.3; global corpus scan
//                                     over every canonical + injection file)
//   - duty missions with no law     -> their briefing must be legacy-free
//     (already REF-clean), oracle cross-scan pinned at zero
// The scrub law itself is Core DutyScrub (the same implementation the Bridge
// calls and the harness mirrors) - second compile context here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

public static class DutyScrubGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) { fails++; Environment.ExitCode = 1; }
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";
    static readonly string[] Langs =
        { "Br", "De", "En", "Es", "Fr", "Hu", "It", "Nl", "No", "Pl", "Ru" };

    // golden duty-7 (DutyMineGate owns the count law; re-read here so this
    // gate's mission list provably comes from the dutyAnchors artifact)
    static readonly string[] DutyGolden =
        { "CORE23.json", "CORE27.json", "CORE28.json", "HOTH10.json",
          "HOTH12.json", "OTHER9.json", "OTHER31.json" };

    // INVENTAR 08.11. (card t_ffc705af): legacy duty sentences per visible
    // language. Two unique sentences exist in the whole corpus:
    //   HOTH10 "...Imperial Officers and Stormtroopers will usher the
    //           prisoners towards the cell..." - briefing box + mission info
    //           (Nl: mission-info translation lacks the sentence => 1)
    //   OTHER9 "...Imperial Officers and Stormtroopers can bind Wookiees when
    //           instructed..." - mission info + eventText
    // Br/Hu/It/No read the EN canonical where these missions have no
    // injection (Br only injects the CORE missions).
    static readonly Dictionary<string, Dictionary<string, int>> Inventory =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
    {
        { "HOTH10.json", Count2("Nl:1") },
        { "OTHER9.json", Count2("") },
        { "CORE23.json", Count2("all0") },
        { "CORE27.json", Count2("all0") },
        { "CORE28.json", Count2("all0") },
        { "HOTH12.json", Count2("all0") },
        { "OTHER31.json", Count2("all0") },
    };
    static Dictionary<string, int> Count2(string special)
    {
        var d = Langs.ToDictionary(l => l, l => 2);
        if (special == "Nl:1") d["Nl"] = 1;
        if (special == "all0") foreach (var k in d.Keys.ToList()) d[k] = 0;
        return d;
    }

    // file-level golden for the corpus leak scan (canonical + injections):
    // hits of a law may ONLY occur in its own mission's files.
    static readonly Dictionary<string, Dictionary<string, int>> FileGolden =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
    {
        { "HOTH10.json", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { { "HOTH10.json", 2 }, { "HOTH10_DE.json", 2 }, { "HOTH10_ES.json", 2 },
              { "HOTH10_FR.json", 2 }, { "HOTH10_NL.json", 1 }, { "HOTH10_PL.json", 2 },
              { "HOTH10_RU.json", 2 } } },
        { "OTHER9.json", new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { { "OTHER9.json", 2 }, { "OTHER9_DE.json", 2 }, { "OTHER9_ES.json", 2 },
              { "OTHER9_FR.json", 2 }, { "OTHER9_NL.json", 2 }, { "OTHER9_PL.json", 2 },
              { "OTHER9_RU.json", 2 } } },
    };

    public static void Run()
    {
        var artPath = Path.Combine(App, "Assets", "Resources", "Languages", "En",
                                   "tactical_mission_critical.json");
        if (!File.Exists(artPath))
        {
            Ok(false, "duty artifact exists");
            Console.WriteLine("DutyScrubGate: FAILURES=" + fails);
            return;
        }
        var art = Json.AsDict(Json.Parse(File.ReadAllText(artPath)));

        // ---- mission list from the dutyAnchors artifact (9.3) --------------
        var dutySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in Json.AsList(Json.Get(art, "missions", new List<object>())))
        {
            var m = Json.AsDict(raw);
            var da = Json.AsDict(Json.Get(m, "dutyAnchors", new Dictionary<string, object>()));
            if (Json.AsStr(Json.Get(da, "class", "")) == "duty")
                dutySet.Add(Json.AsStr(Json.Get(m, "file", "")));
        }
        Ok(dutySet.Count == 7 && DutyGolden.All(dutySet.Contains),
            "duty mission list from artifact == golden duty-7 (" +
            string.Join(",", dutySet.OrderBy(x => x, StringComparer.Ordinal)) + ")");

        // mission file paths by name
        var byName = Directory.GetFiles(
                Path.Combine(App, "Assets", "Resources", "SagaMissions"), "*.json",
                SearchOption.AllDirectories)
            .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ---- per mission x language: counted scrub vs inventory -------------
        var blocked = new List<string>();
        foreach (var mission in DutyGolden)
        {
            foreach (var lang in Langs)
            {
                var texts = ChannelTexts(mission, lang, byName);
                int removed = 0;
                var oracle = new List<string>();
                foreach (var t in texts)
                {
                    int n;
                    DutyScrub.Scrub(mission, t, out n);
                    removed += n;
                    oracle.AddRange(DutyScrub.LegacySentences(mission, t));
                }
                var expected = Inventory[mission][lang];
                // fail-open blank-box and Null-Law are pinned separately;
                // here: counted == inventory == oracle (one law, three views)
                bool ok = removed == expected && oracle.Count == expected;
                Ok(ok, "scrub " + mission + " [" + lang + "] removed=" + removed +
                       " oracle=" + oracle.Count + " inventory=" + expected);
                if (!ok)
                    blocked.Add(mission + "|" + lang + " removed=" + removed +
                                " expected=" + expected +
                                (oracle.Count > 0 ? " first=<" + oracle[0] + ">" : ""));
            }
        }
        // QA-BLOCKED list (concept 7): mission stays review-flagged, the duty
        // block itself is NEVER dropped for that - the gate just fails closed.
        Ok(blocked.Count == 0, "QA-BLOCKED list empty (found " + blocked.Count + ")");
        foreach (var b in blocked) Console.WriteLine("QA-BLOCKED " + b);

        // ---- REF-clean duty missions: oracle cross-scan with BOTH laws ----
        foreach (var mission in new[] { "CORE23.json", "CORE27.json", "CORE28.json",
                                        "HOTH12.json", "OTHER31.json" })
        {
            int cross = 0;
            foreach (var lang in Langs)
                foreach (var t in ChannelTexts(mission, lang, byName))
                    foreach (var lawFile in FileGolden.Keys)
                        cross += DutyScrub.LegacySentences(lawFile, t).Count;
            Ok(cross == 0, "REF-clean (no law, zero cross-law hits) " + mission + " hits=" + cross);
            Ok(!DutyScrub.HasPatterns(mission), "no scrub law shipped for " + mission +
               " (briefing already REF-clean - nothing to remove)");
        }

        // ---- corpus leak scan: patterns hit ONLY their own inventory -------
        var injRoot = Path.Combine(App, "Assets", "Resources", "Languages");
        foreach (var lawFile in FileGolden.Keys)
        {
            var golden = FileGolden[lawFile];
            var hits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var leak = new List<string>();
            foreach (var kv in byName)
                ScanFile(lawFile, kv.Value, hits, leak);
            foreach (var dir in Directory.GetDirectories(injRoot))
            {
                var missDir = Path.Combine(dir, "Missions");
                if (!Directory.Exists(missDir)) continue;
                foreach (var f in Directory.GetFiles(missDir, "*.json", SearchOption.AllDirectories))
                    ScanFile(lawFile, f, hits, leak);
            }
            int total = 0;
            bool match = leak.Count == 0;
            foreach (var kv in golden)
            {
                int got; hits.TryGetValue(kv.Key, out got);
                total += got;
                if (got != kv.Value) match = false;
            }
            Ok(match && total == golden.Values.Sum(),
               "law " + lawFile + " hits only its inventory files (total=" + total +
               " expected=" + golden.Values.Sum() + " leaks=" + leak.Count + ")");
            foreach (var l in leak) Console.WriteLine("LEAK " + lawFile + " -> " + l);
        }

        // ---- runtime mission scoping (Null-Law of the keyed table) ---------
        var sample = "{-} Imperial Officers and Stormtroopers will usher the prisoners towards the cell.";
        foreach (var other in new[] { "EMPIRE3.json", "HOTH12.json", "OTHER31.json", null, "" })
        {
            int n;
            var r = DutyScrub.Scrub(other, sample, out n);
            Ok(n == 0 && r == sample,
               "foreign/empty mission key never scrubs (key=" + (other ?? "<null>") + ")");
        }
        // blank-box law: a text that is ONLY the duty sentence survives whole
        int nb;
        var keep = DutyScrub.Scrub("HOTH10.json", sample, out nb);
        Ok(keep == sample, "blank-box law: duty-only briefing ships untouched");
        // the real briefing shape DOES lose exactly the one sentence
        var brief = "Die Türen stehen offen.\r\n\r\n{-} Imperiale Offiziere und Sturmtruppen werden die Gefangenen in die Zelle treiben.\r\n{-} Die Rebellen gewinnen am Ende von Runde 6.";
        int n2;
        var scrubbed = DutyScrub.Scrub("HOTH10.json", brief, out n2);
        Ok(n2 == 1 && !scrubbed.Contains("Sturmtruppen") &&
           scrubbed.Contains("Türen") && scrubbed.Contains("Runde 6"),
           "briefing shape: exactly the duty sentence removed, neighbors verbatim");

        Console.WriteLine(fails == 0 ? "DutyScrubGate: ALL PASS" : "DutyScrubGate: FAILURES=" + fails);
    }

    // text channels the runtime passes through the scrub seams for one
    // (mission, language): the mission injection if one ships, else the EN
    // canonical (the text the player sees - same law the inventory measured)
    static List<string> ChannelTexts(string missionFile, string lang,
                                     Dictionary<string, string> byName)
    {
        var outp = new List<string>();
        string path = null;
        if (lang != "En")
        {
            var baseName = Path.GetFileNameWithoutExtension(missionFile);
            foreach (var cand in byName.Values)
            {
                var inj = Path.Combine(App, "Assets", "Resources", "Languages", lang,
                                       "Missions", Path.GetFileName(Path.GetDirectoryName(cand)),
                                       baseName + "_" + lang.ToUpperInvariant() + ".json");
                if (File.Exists(inj)) { path = inj; break; }
            }
        }
        string canon;
        if (path == null) path = byName.TryGetValue(missionFile, out canon) ? canon : null;
        if (path == null || !File.Exists(path)) return outp;
        object doc;
        try { doc = Json.Parse(File.ReadAllText(path)); }
        catch { return outp; }
        if (doc == null) return outp;
        var d = Json.AsDict(doc);
        var mp = Json.AsDict(Json.Get(d, "missionProperties", new Dictionary<string, object>()));
        var mi = Json.AsStr(Json.Get(mp, "missionInfo", ""));
        if (!string.IsNullOrEmpty(mi)) outp.Add(mi);
        foreach (var grp in new[] { "events", "globalEvents" })
            foreach (var evRaw in Json.AsList(Json.Get(d, grp, new List<object>())))
            {
                if (!(evRaw is Dictionary<string, object>)) continue;
                var ev = Json.AsDict(evRaw);
                var et = Json.AsStr(Json.Get(ev, "eventText", ""));
                if (!string.IsNullOrEmpty(et)) outp.Add(et);
                foreach (var eaRaw in Json.AsList(Json.Get(ev, "eventActions", new List<object>())))
                {
                    if (!(eaRaw is Dictionary<string, object>)) continue;
                    var ea = Json.AsDict(eaRaw);
                    foreach (var k in new[] { "tbText", "theText", "questionText", "failText" })
                    {
                        var t = Json.AsStr(Json.Get(ea, k, ""));
                        if (!string.IsNullOrEmpty(t)) outp.Add(t);
                    }
                    foreach (var itRaw in Json.AsList(Json.Get(ea, "inputList", new List<object>())))
                    {
                        if (!(itRaw is Dictionary<string, object>)) continue;
                        var t = Json.AsStr(Json.Get(Json.AsDict(itRaw), "theText", ""));
                        if (!string.IsNullOrEmpty(t)) outp.Add(t);
                    }
                }
            }
        return outp;
    }

    // leak scan one file: channel texts when parseable, whole raw text
    // otherwise (conservative: more scanned, never less)
    static void ScanFile(string lawFile, string path,
                        Dictionary<string, int> hits, List<string> leak)
    {
        var name = Path.GetFileName(path);
        List<string> texts;
        var raw = File.ReadAllText(path);
        try
        {
            var doc = Json.AsDict(Json.Parse(raw));
            texts = new List<string>();
            var mp = Json.AsDict(Json.Get(doc, "missionProperties", new Dictionary<string, object>()));
            var mi = Json.AsStr(Json.Get(mp, "missionInfo", ""));
            if (!string.IsNullOrEmpty(mi)) texts.Add(mi);
            foreach (var grp in new[] { "events", "globalEvents" })
                foreach (var evRaw in Json.AsList(Json.Get(doc, grp, new List<object>())))
                {
                    if (!(evRaw is Dictionary<string, object>)) continue;
                    var ev = Json.AsDict(evRaw);
                    var et = Json.AsStr(Json.Get(ev, "eventText", ""));
                    if (!string.IsNullOrEmpty(et)) texts.Add(et);
                    foreach (var eaRaw in Json.AsList(Json.Get(ev, "eventActions", new List<object>())))
                    {
                        if (!(eaRaw is Dictionary<string, object>)) continue;
                        var ea = Json.AsDict(eaRaw);
                        foreach (var k in new[] { "tbText", "theText" })
                        {
                            var t = Json.AsStr(Json.Get(ea, k, ""));
                            if (!string.IsNullOrEmpty(t)) texts.Add(t);
                        }
                    }
                }
        }
        catch { texts = new List<string> { raw }; }   // broken JSON (6 known Nl
                                                      // files, non-duty) - scan raw
        var mine = new HashSet<string>(FileGolden[lawFile].Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var t in texts)
            foreach (var s in DutyScrub.LegacySentences(lawFile, t))
            {
                if (mine.Contains(name))
                    hits[name] = (hits.TryGetValue(name, out int g) ? g : 0) + 1;
                else
                    leak.Add(name + " <" + s.Substring(0, Math.Min(70, s.Length)) + ">");
            }
    }
}
