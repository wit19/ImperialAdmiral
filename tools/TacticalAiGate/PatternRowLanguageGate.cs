// Player-19 language gate (Thomas 03.11). RED law: the doctrine weights a
// shipped group rolls MUST be language-independent. The bug this gate kills:
// tactical_pattern_weights.json matched roles by nameMatch regexes over
// ENGLISH card names while the live catalog hands the LOCALIZED name to the
// matcher - in the German build every nameMatch row fell through to the
// default row (Rote Garde rolled "Vorrücken" with aggressive:55 although the
// Royal Guard row carries guard:55). Fix: ids-lists pin the shipped roster;
// this gate verifies it and keeps it verified for every future row edit.
//
// Checks per shipped card (CardData = canonical EN roster):
//   1. effective weights in EVERY language store == effective weights in EN
//      (first-match semantics, exactly the Core's WeightsFor).
//   2. a nameMatch row that hits an EN roster card must pin that card's id
//      in its ids list (a new roster member is only covered once pinned -
//      the regex alone is NOT accepted as language-proof).
//   3. the catch-all row (no id/ids/nameMatch) must be the LAST row.
// Rows that hit no shipped card (import-only flavour rows) are reported
// INFO and stay legal - custom imports are matched by name, in any language
// they arrive in, and the ids law cannot pin what does not exist.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TacticalAI.Core;

public static class PatternRowLanguageGate
{
    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    static int fails = 0;
    static void Eq(object expected, object actual, string what)
    {
        bool ok = Equals(expected, actual);
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what +
                          " | expected=" + expected + " actual=" + actual);
        if (!ok) fails++;
    }

    // The language stores carry trailing commas (hand-authored) - the same
    // sanitize the Unity loader tolerates, applied before MiniJson.
    static object ParseLenientRaw(string path)
    {
        var t = Regex.Replace(File.ReadAllText(path), @",\s*([}\]])", "$1");
        return Json.Parse(t);
    }
    static Dictionary<string, object> ParseLenient(string path)
        => Json.AsDict(ParseLenientRaw(path));

    static string JoinWeights(Dictionary<string, int> d)
    {
        if (d == null) return "-";
        var keys = new List<string>(d.Keys);
        keys.Sort(StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();
        foreach (var k in keys) sb.Append(k).Append(':').Append(d[k]).Append(';');
        return sb.ToString();
    }

    public static void Run()
    {
        Console.WriteLine("-- PatternRowLanguageGate (Player-19) --");

        // 1. load the SHIPPED table through the Core loader (identical
        //    row/order semantics as the live game).
        var assetPath = Path.Combine(App, "Assets", "Resources", "Languages",
                                     "En", "tactical_pattern_weights.json");
        PatternWeights.PrimeDoc(ParseLenient(assetPath));
        var t = PatternWeights.Load();
        Eq(true, t.IsActive, "table.active");

        // catch-all last
        int catchAll = -1, lastId = -1;
        for (int i = 0; i < t.Rows.Count; i++)
        {
            var r = t.Rows[i];
            bool catchRow = string.IsNullOrEmpty(r.IdMatch)
                            && string.IsNullOrEmpty(r.NameMatch)
                            && (r.Ids == null || r.Ids.Count == 0);
            if (catchRow) catchAll = i;
            if (!string.IsNullOrEmpty(r.IdMatch) || (r.Ids != null && r.Ids.Count > 0)) lastId = i;
        }
        Eq(t.Rows.Count - 1, catchAll, "catch_all_is_last_row");
        Eq(true, catchAll < 0 || lastId < catchAll, "id_rows_before_catch_all");

        // 2. canonical EN roster (CardData = what the harness pins)
        var roster = new List<KeyValuePair<string, string>>();
        foreach (var f in new[] { "enemies", "villains" })
        {
            var list = Json.AsList(ParseLenientRaw(Path.Combine(
                App, "Assets", "Resources", "CardData", f + ".json")));
            foreach (var raw in list)
            {
                var c = Json.AsDict(raw);
                roster.Add(new KeyValuePair<string, string>(
                    Json.AsStr(c["id"]), Json.AsStr(c["name"])));
            }
        }
        Eq(true, roster.Count >= 89, "roster.loaded(" + roster.Count + ")");

        // 3. language stores (top-level JSON arrays with hand-authored
        //    trailing commas - sanitize, then MiniJson list path)
        var langRoot = Path.Combine(App, "Assets", "Resources", "Languages");
        var locs = new[] { "Br", "De", "En", "Es", "Fr", "Hu", "It", "Nl", "No", "Pl", "Ru" };
        var stores = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var loc in locs)
        {
            var s = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in Json.AsList(ParseLenientRaw(Path.Combine(
                langRoot, loc, "DeploymentGroups", "enemies.json"))))
            {
                var c = Json.AsDict(item);
                s[Json.AsStr(c["id"])] = Json.AsStr(c["name"]);
            }
            stores[loc] = s;
        }
        {
            Eq(11, stores.Count, "stores.loaded");
            Eq("Rote Garde", stores["De"]["DG009"], "de_name_is_localized(DG009)");

            // 4. THE law: every card, every language, same weights as EN
            int mismatches = 0, checkedPairs = 0;
            foreach (var card in roster)
            {
                var enW = JoinWeights(t.WeightsFor(card.Key, card.Value));
                foreach (var kv in stores)
                {
                    string name;
                    if (!kv.Value.TryGetValue(card.Key, out name)) continue; // villains: not store-owned (EN-only source)
                    checkedPairs++;
                    var locW = JoinWeights(t.WeightsFor(card.Key, name));
                    if (locW != enW)
                    {
                        mismatches++;
                        Console.WriteLine("FAIL langrow[" + card.Key + "|" + kv.Key +
                                          "] | expected=" + enW + " actual=" + locW +
                                          " (localized name: " + name + ")");
                    }
                }
            }
            Eq(0, mismatches, "language_parity(" + checkedPairs + " pairs)");

            // 5. nameMatch rows must id-pin the shipped cards they claim
            int unpin = 0;
            for (int i = 0; i < t.Rows.Count; i++)
            {
                var r = t.Rows[i];
                if (string.IsNullOrEmpty(r.NameMatch)) continue;
                foreach (var card in roster)
                {
                    // does this row FIRST-match the card in EN semantics?
                    var first = t.WeightsFor(card.Key, card.Value);
                    if (!ReferenceEqualsSafe(first, r.Weights)) continue;
                    bool hitByName;
                    try { hitByName = Regex.IsMatch(card.Value ?? "", r.NameMatch, RegexOptions.IgnoreCase); }
                    catch (ArgumentException) { continue; }
                    if (!hitByName) continue;
                    bool pinned = r.Ids != null && r.Ids.Contains(card.Key);
                    if (!pinned)
                    {
                        unpin++;
                        Console.WriteLine("FAIL idpin[" + card.Key + "] row[" + i + ":" +
                                          r.NameMatch + "] claims this card by EN name but pins no id");
                    }
                }
            }
            Eq(0, unpin, "roster_rows_id_pinned");

            // 6. INFO: import-only rows (legal, reported for review)
            foreach (var r in t.Rows)
            {
                if (string.IsNullOrEmpty(r.NameMatch)) continue;
                int hits = 0;
                foreach (var card in roster)
                {
                    try { if (Regex.IsMatch(card.Value ?? "", r.NameMatch, RegexOptions.IgnoreCase)) hits++; }
                    catch (ArgumentException) { }
                }
                if (hits == 0)
                    Console.WriteLine("INFO import-only nameMatch row: " + r.NameMatch +
                                      " (no shipped card matches in EN - legal, name-only)");
            }
        }

        if (fails > 0) Environment.ExitCode = 1;
        Console.WriteLine("PatternRowLanguageGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL"));
    }

    // WeightsFor returns a FRESH copy per call - compare by content instead.
    static bool ReferenceEqualsSafe(Dictionary<string, int> a, Dictionary<string, int> b)
        => JoinWeights(a) == JoinWeights(b);
}
