// M13d live gate (zero fixtures): the EMPIRE12 playtest (De, 28.10.) logged
// "LoadUILanguage()::Found missing UI translation [De]" for 20 keys on EVERY
// screen load. DataStore.LoadUILanguage warns exactly when an EN string field
// has no non-empty counterpart in the active language file - so this gate
// pins the shipped-assets contract the same way: EVERY key of the shipped
// En/ui.json must exist and be non-empty in EVERY shipped language ui.json
// (missing count must be 0 for all 11), the 20 playtest keys are additionally
// listed explicitly, and the German reference lines from the ticket are
// pinned verbatim (T.: De is the shipped reference translation).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

public static class UiCompletenessGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) { fails++; Environment.ExitCode = 1; }
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    // The exact 20 keys the EMPIRE12 Player.log named (1 x uiSetup, 19 x uiSettings).
    static readonly Dictionary<string, string[]> TicketKeys = new Dictionary<string, string[]>
    {
        { "uiSetup", new[] { "appMissionFormat" } },
        { "uiSettings", new[] {
            "enemyGroupsColor", "colorRegular", "colorElite", "colorVillain",
            "audioHeader", "uiHeader", "graphicsHeader", "mapperHeader",
            "reset", "pressKeyMessage",
            "mapActivateImperialsLabel", "mapToggleCamViewLabel", "mapToggleMapVisibilityLabel",
            "mapNavForwardLabel", "mapNavBackLabel", "mapNavLeftLabel", "mapNavRightLabel",
            "mapNavCWLabel", "mapNavCCWLabel" } },
    };

    // German Fachsprache reference lines (ticket wording: Vor/Zurück,
    // im Uhrzeigersinn; key-mapper context for reset/pressKeyMessage).
    static readonly Dictionary<string, string> DeReference = new Dictionary<string, string>
    {
        { "uiSetup.appMissionFormat", "APP-MISSIONSFORMAT:" },
        { "uiSettings.mapperHeader", "TASTENBELEGUNG" },
        { "uiSettings.mapNavForwardLabel", "KARTE VOR" },
        { "uiSettings.mapNavBackLabel", "KARTE ZURÜCK" },
        { "uiSettings.mapNavCWLabel", "KAMERA IM UHRZEIGERSINN DREHEN" },
        { "uiSettings.mapNavCCWLabel", "KAMERA GEGEN DEN UHRZEIGERSINN DREHEN" },
        { "uiSettings.reset", "STANDARDBELEGUNG" },
        { "uiSettings.pressKeyMessage", "TASTE DRÜCKEN..." },
    };

    public static int Run()
    {
        var langRoot = Path.Combine(App, "Assets", "Resources", "Languages");
        var langs = Directory.GetDirectories(langRoot);
        Array.Sort(langs);
        Ok(langs.Length == 11, "11 shipped language dirs (found " + langs.Length + ")");

        var enFile = Path.Combine(langRoot, "En", "ui.json");
        var enFlat = Flat(Json.AsDict(Json.Parse(File.ReadAllText(enFile))), "");
        Ok(enFlat.Count > 0, "En/ui.json parses flat (" + enFlat.Count + " leaf keys)");

        foreach (var dir in langs)
        {
            var file = Path.Combine(dir, "ui.json");
            if (!File.Exists(file)) { Ok(false, "ui.json exists: " + Path.GetFileName(dir)); continue; }
            var flat = Flat(Json.AsDict(Json.Parse(File.ReadAllText(file))), "");

            // 1) loader mirror: every EN leaf key present + non-empty
            var missing = new List<string>();
            foreach (var kv in enFlat)
            {
                string v;
                if (!flat.TryGetValue(kv.Key, out v) || string.IsNullOrEmpty(v)) missing.Add(kv.Key);
            }
            Ok(missing.Count == 0, Path.GetFileName(dir)
                + " completeness vs En: missing=" + missing.Count
                + (missing.Count > 0 ? " first=" + missing[0] : ""));

            // 2) the 20 EMPIRE12 ticket keys, explicitly
            int ticketMissing = 0;
            foreach (var sec in TicketKeys)
                foreach (var k in sec.Value)
                {
                    string v;
                    if (!flat.TryGetValue(sec.Key + "." + k, out v) || string.IsNullOrEmpty(v)) ticketMissing++;
                }
            Ok(ticketMissing == 0, Path.GetFileName(dir)
                + " EMPIRE12 ticket keys 20/20 present (missing=" + ticketMissing + ")");
        }

        // 3) DE reference lines verbatim
        var de = Json.AsDict(Json.Parse(File.ReadAllText(Path.Combine(langRoot, "De", "ui.json"))));
        var deFlat = Flat(de, "");
        foreach (var kv in DeReference)
        {
            string v;
            deFlat.TryGetValue(kv.Key, out v);
            Ok(v == kv.Value, "De/" + kv.Key + " == \"" + kv.Value + "\" (actual: " + (v ?? "<null>") + ")");
        }

        // 4) uiTacticalAI structure parity vs De (P53, EN EliteHunter birth
        // defect): key presence (check 1) cannot see a multi-line value that
        // lost ROWS inside an otherwise present key - the shipped EN
        // apTreeOfficerEliteHunter lost its header, its Executive Order
        // choice line and its Movement branch (8 De rows shipped as 5, fleet
        // law copied the 5 into nine locales; a support ticket found it).
        // Fail-closed law: for EVERY uiTacticalAI key the De value carries,
        // each locale must match De's row count (multi-line values) and De's
        // {N} placeholder multiset (every value). De is the born-complete
        // reference; a translation that drops a row is an incomplete build.
        foreach (var dir in langs)
        {
            var file = Path.Combine(dir, "ui.json");
            if (!File.Exists(file)) continue;
            var flat = Flat(Json.AsDict(Json.Parse(File.ReadAllText(file))), "");
            int rowMiss = 0, phMiss = 0; string first = "";
            foreach (var kv in deFlat)
            {
                if (!kv.Key.StartsWith("uiTacticalAI.")) continue;
                string v;
                if (!flat.TryGetValue(kv.Key, out v)) v = "";
                if (kv.Value.Contains("\n")
                    && v.Split('\n').Length != kv.Value.Split('\n').Length)
                { rowMiss++; if (first.Length == 0) first = kv.Key; }
                if (Placeholders(v) != Placeholders(kv.Value))
                { phMiss++; if (first.Length == 0) first = kv.Key; }
            }
            var tag = Path.GetFileName(dir);
            Ok(rowMiss == 0, tag + " uiTacticalAI row parity vs De (miss=" + rowMiss
               + (rowMiss > 0 ? " first=" + first : "") + ")");
            Ok(phMiss == 0, tag + " uiTacticalAI placeholder parity vs De (miss=" + phMiss
               + (phMiss > 0 ? " first=" + first : "") + ")");
        }

        Console.WriteLine(fails == 0 ? "UiCompletenessGate: ALL PASS" : "UiCompletenessGate: FAILURES=" + fails);
        return fails;
    }

    // sorted {N} placeholder multiset, "|"-joined (parity across locales)
    static string Placeholders(string v)
    {
        var found = new List<string>();
        var i = 0;
        while (i < v.Length)
        {
            var a = v.IndexOf('{', i);
            if (a < 0) break;
            var b = v.IndexOf('}', a);
            if (b < 0) break;
            found.Add(v.Substring(a, b - a + 1));
            i = b + 1;
        }
        found.Sort();
        return string.Join("|", found.ToArray());
    }

    // flatten "section.key" -> string (ui.json leaves are strings)
    static Dictionary<string, string> Flat(Dictionary<string, object> d, string prefix)
    {
        var outp = new Dictionary<string, string>();
        foreach (var kv in d)
        {
            var path = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            var sub = kv.Value as Dictionary<string, object>;
            if (sub != null)
                foreach (var inner in Flat(sub, path)) outp[inner.Key] = inner.Value;
            else
                outp[path] = kv.Value as string;
        }
        return outp;
    }
}
