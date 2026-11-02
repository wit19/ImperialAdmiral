// M6h live gate (zero fixtures, SHIPPED ui.json): Thomas/Tillmann 02.11 officer
// popup findings must not silently regress. Pins, for every language file:
//   1. exactly ONE order branch per officer tree (regular: "Befehl/Order
//      m\u00f6glich?/possible?" - movement order; elite: "Vollstreckungs-
//      befehl/Executive Order" - attack order),
//   2. bug2: the regular order branch NEVER orders an attack, the elite order
//      branch NEVER orders movement (no move+attack in one command),
//   3. bug1: no ability re-explanation ("deiner Karte"/"on your card"),
//   4. bug3: the Wachzone definition rides EXACTLY the two guard trees,
//   5. bug5: no "Spieler"/"player" subject in any officer popup text,
//   6. bug6: no "you decide" claim, no imperative sentences (options are
//      questions + infinitives - the decision stays with the human reader),
//   7. bug4: no approach/retreat advice verbs ("zubewegen"), no "movement
//      points given" misphrasing; the printed Order range is the FIXED 2,
//   8. De carries the printed German keywords (Befehl/Vollstreckungsbefehl),
//      never the bare English token; {0} is the only placeholder.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

public static class OfficerTextGate
{
    static int fails = 0, checks = 0;
    static void Ok(bool cond, string what)
    {
        checks++;
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) { fails++; Environment.ExitCode = 1; }
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    // printed German keywords (Languages/De card asset DG004/DG006 ability lane)
    const string Befehl = "Befehl";
    const string ExecDe = "Vollstreckungsbefehl";
    const string BefehlMoeglich = "Befehl m\u00f6glich?";       // "Befehl moeglich?"
    const string ExecMoeglich = "Vollstreckungsbefehl m\u00f6glich?";
    const string KeinBefehl = "Kein Befehl?";
    const string ZweiFelder = "bis zu 2 Felder";
    const string ZiehtSich = "zieht sich";
    const string GreiftWelches = "greift welches";
    const string Wachzone = "Wachzone";
    const string OrdHeadDe = "<b>? Angriffspunkt verf\u00fcgbar?</b>";
    const string WzDe = "<i><color=#9FC6FF>Wachzone: der Bereich der Mission";
    const string WzEn = "<i><color=#9FC6FF>Guard zone: the area of the mission";

    static readonly string[] RegularKeys =
    {
        "apTreeOfficerAggressive", "apTreeOfficerGuard", "apTreeOfficerBlocker",
        "apTreeOfficerSupport", "apTreeOfficerAmbush",
    };
    static readonly string[] EliteKeys =
    {
        "apTreeOfficerEliteAggressive", "apTreeOfficerEliteGuard",
        "apTreeOfficerEliteBlocker", "apTreeOfficerEliteSupport",
        "apTreeOfficerEliteAmbush",
    };

    public static int Run()
    {
        var langRoot = Path.Combine(App, "Assets", "Resources", "Languages");
        var langs = Directory.GetDirectories(langRoot);
        Array.Sort(langs);

        foreach (var dir in langs)
        {
            var file = Path.Combine(dir, "ui.json");
            if (!File.Exists(file)) { Ok(false, "ui.json exists: " + Path.GetFileName(dir)); continue; }
            var flat = new Dictionary<string, string>();
            foreach (var kv in Json.AsDict(Json.Parse(File.ReadAllText(file))))
            {
                if (kv.Key != "uiTacticalAI") continue;
                var sub = kv.Value as Dictionary<string, object>;
                if (sub == null) continue;
                foreach (var s in sub)
                    flat[s.Key] = s.Value as string;
            }
            var tag = Path.GetFileName(dir);
            var de = tag == "De";

            foreach (var k in RegularKeys)
                CheckTree(flat, tag, de, k, true);
            foreach (var k in EliteKeys)
                CheckTree(flat, tag, de, k, false);

            // shared note rendered in every officer popup (bug5 subject law)
            string lane;
            flat.TryGetValue("ordAbilitiesLane", out lane);
            Ok(!string.IsNullOrEmpty(lane) && lane.IndexOf("pieler") < 0 &&
               lane.ToLowerInvariant().IndexOf("player") < 0,
               tag + "/ordAbilitiesLane has no player/Spieler subject");
            // De reference pin: grammatical, subject = das Imperium
            if (de)
                Ok(lane != null && lane.StartsWith(
                       "\u00dcber gedruckte Sonderf\u00e4higkeiten entscheidet das Imperium:"),
                   tag + "/ordAbilitiesLane De subject is 'das Imperium'");
        }

        Console.WriteLine(fails == 0
            ? "OfficerTextGate: ALL PASS (checks=" + checks + ")"
            : "OfficerTextGate: FAILURES=" + fails + " (checks=" + checks + ")");
        return fails;
    }

    static void CheckTree(Dictionary<string, string> flat, string tag, bool de,
                          string key, bool regular)
    {
        string v;
        flat.TryGetValue(key, out v);
        var name = tag + "/" + key;
        Ok(!string.IsNullOrEmpty(v), name + " present");
        if (string.IsNullOrEmpty(v)) return;

        var lines = v.Split('\n');
        // one order branch, named per printed keyword of this language
        int orderBranches = 0;
        string order = null;
        foreach (var ln in lines)
        {
            bool hit = de
                ? (ln.Contains(regular ? BefehlMoeglich : ExecMoeglich))
                : (ln.Contains(regular ? "Order possible?" : "Executive Order possible?"));
            if (hit) { orderBranches++; order = ln; }
        }
        Ok(orderBranches == 1, name + " exactly one order branch (" + orderBranches + ")");
        if (orderBranches != 1) return;

        // bug2: regular = move only, elite = attack only, never both
        if (regular)
        {
            Ok(de ? order.Contains(ZweiFelder) && order.Contains(ZiehtSich)
                  : order.Contains("up to 2 spaces") && order.Contains("repositions"),
               name + " order branch = move up to the printed 2");
            Ok(!order.Contains(GreiftWelches) &&
               order.ToLowerInvariant().IndexOf("attacks which") < 0 &&
               order.ToLowerInvariant().IndexOf("it attacks") < 0,
               name + " order branch orders NO attack (bug2)");
            Ok(order.Contains(de ? "verbrauch" : "spends"),
               name + " order branch states the attack-point cost");
        }
        else
        {
            Ok(de ? order.Contains(GreiftWelches)
                  : order.ToLowerInvariant().Contains("attacks which"),
               name + " exec-order branch orders an attack");
            Ok(!order.Contains(ZiehtSich) &&
               order.ToLowerInvariant().IndexOf("reposition") < 0,
               name + " exec-order branch orders NO movement (bug2)");
            // the retired move-order route must not come back at all
            Ok(!v.Contains(de ? ZweiFelder : "up to 2 spaces"),
               name + " elite tree carries no plain move order (bug2)");
        }

        // shared bug laws over every line
        Ok(v.IndexOf("Spieler") < 0 && v.ToLowerInvariant().IndexOf("player") < 0,
           name + " no player/Spieler subject (bug5)");
        Ok(!v.Contains("deiner Karte") && !v.Contains("on your card"),
           name + " no ability re-explanation (bug1)");
        Ok(!v.Contains("entscheidest") && v.ToLowerInvariant().IndexOf("you decide") < 0,
           name + " no decide-claim (bug6)");
        Ok(!v.Contains("Bewege") && !v.Contains("Bleib ") && !v.Contains("Gehe ") &&
           v.IndexOf("Decide at the board") < 0,
           name + " no imperatives (bug6: questions + infinitives only)");
        Ok(!v.Contains("zubewegen"), name + " no approach advice (bug4)");
        Ok(!v.Contains("Bewegungspunkte") && v.ToLowerInvariant().IndexOf("movement points") < 0,
           name + " no 'movement points given' misphrasing (bug1/2)");
        // {0} is the only placeholder the renderer substitutes
        Ok(v.Replace("{0}", "").IndexOf('{') < 0, name + " only the {0} placeholder");
        // the AP question headers survive (tree stays readable for apRule)
        Ok(v.Contains(de ? OrdHeadDe : "<b>? Attack point available?</b>"),
           name + " keeps the attack-point question header");
        // bug3: Wachzone definition rides exactly the guard pair
        var guard = key.EndsWith("Guard");
        var def = v.StartsWith(de ? WzDe : WzEn);
        Ok(guard == def, name + " guard-zone definition placement (bug3)");
        if (def)
        {
            var first = lines[0];
            Ok(!first.Contains("Felder") && first.IndexOf(" Feld ") < 0 &&
               first.ToLowerInvariant().IndexOf("spaces") < 0,
               name + " guard-zone definition is data-free (bug3)");
            Ok(first.Contains(de ? "Wachen" : "guards"),
               name + " guard-zone definition is the canonical sentence");
        }
        if (de && !guard) Ok(v.IndexOf(Wachzone) < 0, name + " no Wachzone outside guard trees");

        // language-keyword law: De prints Befehl/Vollstreckungsbefehl, never bare Order
        if (de)
        {
            Ok(v.Contains(regular ? BefehlMoeglich : ExecMoeglich),
               name + " De branch names the printed German keyword");
            var stripped = v.Replace(ExecDe, "");
            Ok(stripped.IndexOf("Order") < 0,
               name + " De carries no bare English 'Order' token");
        }
        else if (!regular)
        {
            Ok(v.Contains("Executive Order possible?"),
               name + " EN elite names the printed Executive Order");
        }
    }
}
