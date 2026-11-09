// M13c live gate (zero fixtures): the optional-deployment revival reads its
// strings ONLY from ui.json (sagaMainApp, all 11 shipped language files),
// so the key contract is pinned here against the shipped assets: every key
// present and non-empty in EVERY language, the announce template carries
// both {0} group and {1} DP placeholders in every language, and the pure
// substitution law of Core.OptionalDeploy reproduces the German playtest
// sentence exactly (Thomas: De is the shipped reference translation).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

public static class OptionalDeployGate
{
    static int fails = 0;
    static void Eq(object expected, object actual, string what)
    {
        bool ok = Equals(expected, actual);
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what + " | expected=" + expected + " actual=" + actual);
        if (!ok) fails++;
    }
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    static readonly string[] Keys = {
        "optionalDeployTitleUC", "optionalDeployPickGroupUC", "optionalDeployPickDpUC",
        "optionalDeployNoGroupsUC", "optionalDeployCostFormatUC", "optionalDeployDpFormatUC",
        "optionalDeployConfirmUC", "optionalTacticalAnnounceUC",
    };
    // templates that MUST keep their placeholders in every language
    static readonly Dictionary<string, int[]> Placeholders = new Dictionary<string, int[]>
    {
        { "optionalDeployCostFormatUC", new[] { 0, 1 } },
        { "optionalDeployDpFormatUC",   new[] { 0 } },
        { "optionalTacticalAnnounceUC", new[] { 0, 1 } },
    };

    public static int Run()
    {
        var langRoot = Path.Combine(App, "Assets", "Resources", "Languages");
        var langs = Directory.GetDirectories(langRoot);
        Array.Sort(langs);
        Ok(langs.Length == 11, "11 shipped language dirs (found " + langs.Length + ")");
        foreach (var dir in langs)
        {
            var file = Path.Combine(dir, "ui.json");
            if (!File.Exists(file)) { Ok(false, "ui.json exists: " + Path.GetFileName(dir)); continue; }
            var doc = Json.AsDict(Json.Parse(File.ReadAllText(file)));
            var sma = Json.AsDict(doc["sagaMainApp"]);
            foreach (var k in Keys)
            {
                string v = sma.ContainsKey(k) ? Json.AsStr(sma[k]) : null;
                Ok(!string.IsNullOrEmpty(v), Path.GetFileName(dir) + "/sagaMainApp." + k + " present");
                if (Placeholders.ContainsKey(k) && !string.IsNullOrEmpty(v))
                    foreach (var p in Placeholders[k])
                        Ok(v.Contains("{" + p + "}"), Path.GetFileName(dir) + "/" + k + " keeps {" + p + "}");
            }
        }

        // the DE reference strings render through the pinned substitution law
        var de = Json.AsDict(Json.Parse(File.ReadAllText(Path.Combine(langRoot, "De", "ui.json"))));
        var deSma = Json.AsDict(de["sagaMainApp"]);
        var announce = OptionalDeploy.AnnounceLine(Json.AsStr(deSma["optionalTacticalAnnounceUC"]),
                                                   "Gamorrean Guard", "A1");
        Ok(announce.StartsWith("Gamorrean Guard stellt sich bei A1 auf"),
           "DE announce line renders group + DP: " + announce);
        var dpLine = string.Format(Json.AsStr(deSma["optionalDeployDpFormatUC"]), "A1");
        Ok(dpLine == "Aufstellungspunkt: A1", "DE DP line: " + dpLine);

        // mode fork against the settings law itself (no fixture): every
        // legal badge/global combination the settings UI can produce
        Eq("manual", OptionalDeploy.Path(false, "overlord", ""), "global OFF -> manual");
        Eq("manual", OptionalDeploy.Path(true, "overlord", "off"), "badge OFF -> manual");
        Eq("manual", OptionalDeploy.Path(true, "overlord", "legacy"), "old campaign 'legacy' field -> manual");
        Eq("tactical", OptionalDeploy.Path(true, "overlord", "S1"), "badge S1 -> tactical");
        Eq("tactical", OptionalDeploy.Path(true, "reference", ""), "AUTO over global reference -> tactical");

        // ---- P41c rail-5 Massive rows (card t_a2e54ec9; spec 4-D3-B v1.3
        // rails census + 9.4 last bullet, controller note from P41b-QA). The
        // optional window is ONE of the five rails: its Active-spot deploys
        // resolve through the single tactical seam (DeploymentManager
        // :534 DoMultipleDeployment -> GetActiveDeploymentPoint(card) ->
        // bridge hard filter) and the announce re-roll names a DP that was
        // really used (SagaDeploymentPopup :878, card-supplied). The popup
        // itself must NEVER grow its own interior filter (single-seam law) -
        // and the human's explicit DP pick becomes a Specific-slot override
        // (:1136-1139), the human-authored placement lane: by spec design the
        // hard filter governs the AI rails, the human command channel stays
        // the human's (D6 doctrine: the AI proposes, the human acts).
        var rf5 = MassiveInteriorGate.CollectRailFacts();
        Ok(rf5.PopupReRoll == 1,
           "rail5 announce re-roll supplies the card to the seam exactly once (M13f one-DP truth kept)");
        Ok(rf5.PopupInteriorRefs == 0,
           "rail5 inherits the Massive hard filter through the seam only - no interior logic grew in the popup");
        Ok(rf5.Core4CoversDg073,
           "rail5 CORE4: DG073 (authored indoor) carries the D6 override - its window keeps the full pool");
        Ok(!rf5.Core4CoversDg014,
           "rail5 CORE4: DG014 has no authored row - its window drops the Banned greens");

        Console.WriteLine(fails == 0 ? "OptionalDeployGate: ALL PASS" : "OptionalDeployGate: FAILURES=" + fails);
        return fails;
    }
}
