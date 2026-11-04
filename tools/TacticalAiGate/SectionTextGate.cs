// S-D live gate (card t_c52dd34b, zero fixtures, SHIPPED ui.json only): the
// section-space decision lines. Thomas' language law: every new player-facing
// decision line lives in ui.json in ALL 11 shipped languages; Core carries
// only machine markers and identifiers. This gate pins
//   1) the exact 8-key contract in section uiTacticalAI for all 11 languages
//      (existence + non-empty, the exact set - UiCompletenessGate checks the
//      general superset, this pins THESE keys),
//   2) the placeholder contract per key against the bridge's TF() arity:
//      ordExplainWachzone/ordExplainThreeC = {0}{1}{2}, ordExplainVergeltung
//      = {0}{1}, ordExplainClamp / both ordFocusBasis* = NO placeholders,
//      ordFocusWachzone/ordExplainVergeltung-focus = exactly {0} - a stray or
//      missing {n} would Format-crash or ship a literal '{0}' to the player,
//   3) De is a real translation: every new explain/focus value differs from
//      its EN counterpart (an EN copy-paste in De is a silent language break).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TacticalAI.Core;

public static class SectionTextGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) { fails++; Environment.ExitCode = 1; }
    }

    static readonly string LangRoot =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Resources/Languages";

    static readonly string[] Locales =
        { "Br", "De", "En", "Es", "Fr", "Hu", "It", "Nl", "No", "Pl", "Ru" };

    // key -> expected placeholder indices (bridge TF call sites, S-D wiring)
    static readonly Dictionary<string, int[]> Contract = new Dictionary<string, int[]>
    {
        { "ordExplainWachzone",      new[] { 0, 1, 2 } },
        { "ordExplainThreeC",        new[] { 0, 1, 2 } },
        { "ordExplainVergeltung",    new[] { 0, 1 } },
        { "ordExplainClamp",         new int[0] },
        { "ordFocusWachzone",        new[] { 0 } },
        { "ordFocusVergeltung",      new[] { 0 } },
        { "ordFocusBasisWachzone",   new int[0] },
        { "ordFocusBasisVergeltung", new int[0] },
        // S-E (card t_9dd56737): supporter mask-trust arity contract, taken
        // from the Bridge TF call sites (TrySelectTarget tail + ExplainFor).
        { "ordFocusMaskenvertrauen",        new[] { 0 } },
        { "ordFocusBasisMaskenvertrauen",   new int[0] },
        { "ordExplainSupportMaske",         new[] { 0, 1 } },
        { "ordExplainSupportHalten",        new[] { 0 } },
    };

    public static void Run()
    {
        Console.WriteLine("-- SectionTextGate (S-D uiTacticalAI decision lines, 11 languages) --");
        var de = Section("De");
        var en = Section("En");
        foreach (var loc in Locales)
        {
            var sec = loc == "De" ? de : loc == "En" ? en : Section(loc);
            Ok(sec != null, "ui.json readable + uiTacticalAI present: " + loc);
            if (sec == null) continue;
            foreach (var kv in Contract)
            {
                string val;
                if (!sec.TryGetValue(kv.Key, out val) || string.IsNullOrEmpty(val))
                {
                    Ok(false, loc + " has key " + kv.Key);
                    continue;
                }
                Ok(true, loc + " has key " + kv.Key);
                var found = new List<int>();
                foreach (Match m in Regex.Matches(val, @"\{(\d+)\}"))
                    found.Add(int.Parse(m.Groups[1].Value));
                found.Sort();
                var want = new List<int>(kv.Value); want.Sort();
                var same = found.Count == want.Count;
                if (same) for (int i = 0; i < found.Count; i++) if (found[i] != want[i]) same = false;
                Ok(same, loc + " placeholders of " + kv.Key + " = {" +
                         string.Join(",", found.ConvertAll(x => x.ToString()).ToArray()) +
                         "} (contract {" +
                         string.Join(",", want.ConvertAll(x => x.ToString()).ToArray()) + "})");
            }
            if (loc != "En")
                foreach (var kv in Contract)
                {
                    string dv, ev;
                    if (de == null || en == null) break;
                    de.TryGetValue(kv.Key, out dv);
                    en.TryGetValue(kv.Key, out ev);
                    if (!string.IsNullOrEmpty(dv) && dv == ev && kv.Key.StartsWith("ordExplain"))
                        Ok(false, "De is a real translation, not the EN copy: " + kv.Key);
                }
        }
        Ok(fails == 0, "SectionTextGate ALL PASS");
        Console.WriteLine();
    }

    static Dictionary<string, string> Section(string loc)
    {
        try
        {
            var path = Path.Combine(LangRoot, loc, "ui.json");
            var root = Json.AsDict(Json.Parse(File.ReadAllText(path)));
            var sec = Json.AsDict(root["uiTacticalAI"]);
            var flat = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in sec)
                if (kv.Value is string) flat[kv.Key] = (string)kv.Value;
            return flat;
        }
        catch { return null; }
    }
}
