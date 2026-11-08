// P37a duty gate (card t_1f4d7286): the SHIPPED dutyAnchors mining result in
// BOTH artifacts (Languages/En/tactical_mission_critical.json and
// Data/taktische_mission_critical.json) must hold the concept's golden
// vector, fail-closed - docs/CONCEPT_missions_duty.md 7 (Rev 3.3/3.4):
//   1. counts: duty=7, borderline=7, ruhe=124 - exact file sets, no drift.
//   2. golden variable@threshold pairs for the 7 duty missions.
//   3. data-exists (law 2 of MissionCriticalGate, same discipline): every
//      targetField/markerToken GUID of a duty row exists in that mission's
//      mapEntities; the Data rows re-resolve position + live section.
//   4. Pfad-Doppelpfad (concept W7): the two loaders (Languages block, Data
//      dutyAnchors section) read the SAME (file, entityGuid, role) set.
//   5. the anchor total is the measured golden (28 rows) - deviation FAILS,
//      silent adaptation is forbidden.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

public static class DutyMineGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    // golden vector (docs/CONCEPT_missions_duty.md 7 + card t_1f4d7286)
    static readonly Dictionary<string, string> DutyGolden = new Dictionary<string, string>
    {
        { "CORE23.json", "Imperial Recruits@16" },
        { "CORE27.json", "Imperials escape@1" },
        { "CORE28.json", "Caches destroyed@5" },
        { "HOTH10.json", "Prisoners claimed@4" },
        { "HOTH12.json", "Draylen executed@1" },
        { "OTHER9.json", "Wookiees Bound@6" },
        { "OTHER31.json", "Rebels Lose@1" },
    };
    static readonly HashSet<string> BorderlineGolden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CORE10.json", "CORE16.json", "EMPIRE2.json", "EMPIRE4.json",
        "HOTH15.json", "OTHER6.json", "OTHER7.json",
    };
    const int RuheGolden = 124;
    const int AnchorRowsGolden = 28;

    public static void Run()
    {
        var langPath = Path.Combine(App, "Assets", "Resources", "Languages", "En",
                                    "tactical_mission_critical.json");
        var dataPath = Path.Combine(App, "Assets", "Resources", "Data",
                                    "taktische_mission_critical.json");
        if (!File.Exists(langPath) || !File.Exists(dataPath))
        {
            Console.WriteLine("FAIL duty artifact missing");
            Environment.ExitCode = 1;
            Console.WriteLine("DutyMineGate: 1 FAIL");
            return;
        }
        var lang = Json.AsDict(Json.Parse(File.ReadAllText(langPath)));
        var data = Json.AsDict(Json.Parse(File.ReadAllText(dataPath)));
        var rows = Json.AsList(Json.Get(lang, "missions", new List<object>()))
                        .Select(Json.AsDict).ToList();
        Ok(rows.Count == 138, "critical table covers 138 missions (" + rows.Count + ")");

        // mission files by name (data-exists ground truth)
        var missionFiles = Directory.GetFiles(
            Path.Combine(App, "Assets", "Resources", "SagaMissions"), "*.json",
            SearchOption.AllDirectories);
        var byName = missionFiles.GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                                 .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int duty = 0, borderline = 0, none = 0, anchorRowsLang = 0;
        var langSet = new HashSet<string>();      // file|guid|role
        foreach (var row in rows)
        {
            var file = Json.AsStr(Json.Get(row, "file", ""));
            var da = Json.AsDict(Json.Get(row, "dutyAnchors", null));
            if (da == null)
            { Console.WriteLine("FAIL " + file + ": no dutyAnchors block"); fails++; continue; }
            var cls = Json.AsStr(Json.Get(da, "class", ""));
            if (cls == "duty")
            {
                duty++;
                Ok(DutyGolden.ContainsKey(file), "duty file in golden set: " + file);
                var duties = Json.AsList(Json.Get(da, "duties", new List<object>()))
                                 .Select(Json.AsDict).ToList();
                if (duties.Count != 1)
                { Console.WriteLine("FAIL " + file + ": " + duties.Count + " duties, expected 1"); fails++; }
                foreach (var d in duties)
                {
                    var v = Json.AsStr(Json.Get(d, "variable", ""));
                    var thr = Json.AsLong(Json.Get(d, "threshold", -1));
                    string golden;
                    DutyGolden.TryGetValue(file, out golden);
                    Ok(golden == v + "@" + thr,
                       "golden pair " + file + ": " + v + " @" + thr + " (golden " + golden + ")");
                    if (!byName.ContainsKey(file))
                    { Console.WriteLine("FAIL " + file + ": duty mission file not shipped"); fails++; continue; }
                    var m = Json.AsDict(Json.Parse(File.ReadAllText(byName[file])));
                    var ents = Json.AsList(Json.Get(m, "mapEntities", new List<object>()))
                                   .Select(Json.AsDict)
                                   .ToDictionary(e => Json.AsStr(Json.Get(e, "GUID", "")),
                                                 e => e, StringComparer.OrdinalIgnoreCase);
                    foreach (var key in new[] { "targetFields", "markerTokens" })
                    {
                        var role = key == "targetFields" ? "targetField" : "markerToken";
                        foreach (var a in Json.AsList(Json.Get(d, key, new List<object>()))
                                            .Select(Json.AsDict))
                        {
                            var guid = Json.AsStr(Json.Get(a, "entity", ""));
                            Ok(ents.ContainsKey(guid),
                               "data-exists " + file + ": " + role + " " +
                               Json.AsStr(Json.Get(a, "name", "")) + " GUID resolves");
                            langSet.Add(file + "|" + guid.ToLowerInvariant() + "|" + role);
                            anchorRowsLang++;
                        }
                    }
                }
            }
            else if (cls == "borderline")
            {
                borderline++;
                Ok(BorderlineGolden.Contains(file), "borderline file in golden set: " + file);
                var duties = Json.AsList(Json.Get(da, "duties", new List<object>()));
                Ok(duties.Count == 0, "borderline carries no duty: " + file);
            }
            else if (cls == "none") none++;
            else { Console.WriteLine("FAIL " + file + ": duty class '" + cls + "' unknown"); fails++; }
        }

        // 1. counts, fail-closed on the golden vector
        Ok(duty == 7, "duty count == 7 (found " + duty + ")");
        Ok(borderline == 7, "borderline count == 7 (found " + borderline + ")");
        Ok(none == RuheGolden, "ruhe count == " + RuheGolden + " (found " + none + ")");
        Ok(duty + borderline + none == 138, "classes cover every mission (138)");

        // 4./3. Data path: dutyAnchors section re-resolves and equals the
        //        Languages set (Pfad-Doppelpfad law W7)
        var dutySec = Json.AsDict(Json.Get(data, "dutyAnchors", null));
        if (dutySec == null)
        { Console.WriteLine("FAIL Data table has no dutyAnchors section"); fails++; }
        else
        {
            var drows = Json.AsList(Json.Get(dutySec, "rows", new List<object>()));
            var dcols = Json.AsList(Json.Get(dutySec, "columns", new List<object>()))
                            .Select(Json.AsStr).ToList();
            Ok(string.Join(",", dcols) == "missionFile,entityGuid,entityPosition,sectionGuid,role",
               "dutyAnchors columns header");
            // header row + array rows (same table law as the DP rows)
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < dcols.Count; i++) idx[dcols[i]] = i;
            Func<List<object>, string, string> col = (r, name) =>
                idx.ContainsKey(name) && idx[name] < r.Count
                    ? Json.AsStr(r[idx[name]]) ?? "" : "";
            var dataSet = new HashSet<string>();
            foreach (var raw in drows)
            {
                var r = Json.AsList(raw);
                var file = col(r, "missionFile");
                var guid = col(r, "entityGuid");
                var pos = col(r, "entityPosition");
                var sec = col(r, "sectionGuid");
                var role = col(r, "role");
                Ok(role == "targetField" || role == "markerToken",
                   "role in {targetField,markerToken}: " + file + " " + role);
                if (!byName.ContainsKey(file))
                { Console.WriteLine("FAIL Data duty row for unshipped file " + file); fails++; continue; }
                var m = Json.AsDict(Json.Parse(File.ReadAllText(byName[file])));
                var e = Json.AsList(Json.Get(m, "mapEntities", new List<object>()))
                            .Select(Json.AsDict)
                            .FirstOrDefault(x => string.Equals(Json.AsStr(Json.Get(x, "GUID", "")),
                                                               guid, StringComparison.OrdinalIgnoreCase));
                Ok(e != null, "data-exists (Data path) " + file + ": anchor " + guid + " resolves");
                if (e != null)
                {
                    Ok(Json.AsStr(Json.Get(e, "entityPosition", "")) == pos,
                       "authored position agrees " + file + " " + guid);
                    Ok(string.Equals(Json.AsStr(Json.Get(e, "mapSectionOwner", "")), sec,
                                     StringComparison.OrdinalIgnoreCase),
                       "live section agrees " + file + " " + guid);
                }
                dataSet.Add(file + "|" + guid.ToLowerInvariant() + "|" + role);
            }
            Ok(drows.Count == anchorRowsLang && dataSet.SetEquals(langSet),
               "Pfad-Doppelpfad: both artifacts read the same anchor set (" +
               anchorRowsLang + "/" + drows.Count + ")");
        }

        // 5. measured golden anchor total
        Ok(anchorRowsLang == AnchorRowsGolden,
           "anchor rows == " + AnchorRowsGolden + " (Languages " + anchorRowsLang + ")");

        if (fails > 0) Environment.ExitCode = 1;
        Console.WriteLine("DutyMineGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL") +
                          " | duty=" + duty + " borderline=" + borderline +
                          " ruhe=" + none + " anchors=" + anchorRowsLang);
    }
}
