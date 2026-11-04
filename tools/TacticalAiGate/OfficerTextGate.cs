// M6h live gate (zero fixtures, SHIPPED ui.json): Thomas/Tillmann 02.11 officer
// popup findings must not silently regress. Pins, for every language file:
//   1. exactly ONE order branch per REGULAR officer tree ("Befehl/Order
//      m\u00f6glich?/possible?" - movement order),
//   1b. M6n / C1-SIGNATUR Thomas 02.11 (physical DG006 card): the elite tree
//      asks ONE Vollstreckungsbefehl/Executive Order receiver question per
//      activation offering Bewegung/movement OR Angriff/attack, plus exactly
//      one answer branch each; the line must carry the exclusive "oder" and
//      the explicit "nur eines von beiden, nie beides"-note; an "und"/"and"
//      in that line is the forbidden reading (mutation self-test: a
//      "Bewegung und Angriff" probe must turn this gate RED),
//   2. bug2: the regular order branch NEVER orders an attack; in the elite
//      tree the movement answer NEVER orders an attack and the attack
//      answer NEVER orders movement (no move+attack in one command),
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
    const string ExecChoiceDe = "Vollstreckungsbefehl \u2192";  // C1 receiver question
    const string ExecChoiceEn = "Executive Order \u2192";
    const string MvBranchDe = "Bewegung \u2192";
    const string MvBranchEn = "Movement \u2192";
    const string AtkBranchDe = "Angriff \u2192";
    const string AtkBranchEn = "Attack \u2192";
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

            // O-C (card t_a2d88c50): the supporter keep ersatzzweig. Regular
            // keep trees ride the SAME regular-tree laws (one move-only order
            // branch, headers, keywords) PLUS the keep laws; elite keep trees
            // drop the Angriff receiver branch (emergency privilege) so they
            // run their own shape check; apTreeOfficerNotfall wraps the
            // proven OfficerEmergency why-line for the popup tree head.
            foreach (var k in RegularKeys)
            {
                var kk = KeepOf(k);
                CheckTree(flat, tag, de, kk, true);
                string kv;
                if (flat.TryGetValue(kk, out kv) && !string.IsNullOrEmpty(kv))
                    CheckKeepLaws(tag + "/" + kk, de, kv);
            }
            foreach (var k in EliteKeys)
                CheckEliteKeepTree(flat, tag, de, KeepOf(k), k);
            CheckNotfallKey(flat, tag, de);

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
        bool treeOk = true;
        if (regular)
        {
            // one order branch (movement order), named per printed keyword
            int orderBranches = 0;
            string order = null;
            foreach (var ln in lines)
                if (ln.Contains(de ? BefehlMoeglich : "Order possible?"))
                { orderBranches++; order = ln; }
            Ok(orderBranches == 1, name + " exactly one order branch (" + orderBranches + ")");
            if (orderBranches != 1) treeOk = false;
            else
            {
                // bug2: regular = move only, never an attack in the order
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
        }
        else
        {
            // M6n / C1-SIGNATUR (Thomas 02.11., physische DG006): eine
            // Empfaenger-Frage pro Aktivierung - Bewegung ODER Angriff,
            // eines pro Befehl, NIE beides; je genau ein Antwortzweig.
            int choices = 0, moves = 0, attacks = 0;
            string choice = null, mv = null, atk = null;
            foreach (var ln in lines)
            {
                if (ln.Contains(de ? ExecChoiceDe : ExecChoiceEn)) { choices++; choice = ln; }
                if (ln.Contains(de ? MvBranchDe : MvBranchEn)) { moves++; mv = ln; }
                if (ln.Contains(de ? AtkBranchDe : AtkBranchEn)) { attacks++; atk = ln; }
            }
            Ok(choices == 1 && moves == 1 && attacks == 1,
               name + " one choice + one Bewegung + one Angriff branch (" +
               choices + "/" + moves + "/" + attacks + ")");
            if (choices != 1 || moves != 1 || attacks != 1) treeOk = false;
            else
            {
                // C1 exclusive offering: BOTH channels named, joined by the
                // exclusive "oder"/"or" - a conjunctive "und"/"and" in this
                // line IS the forbidden both-in-one-command reading
                // (mutation self-test 02.11.: "Bewegung und Angriff" = RED).
                var low = choice.ToLowerInvariant();
                Ok(choice.StartsWith("<color") &&
                   low.Contains(de ? "bewegung" : "movement") &&
                   low.Contains(de ? "angriff" : "attack") &&
                   choice.Contains(de ? " oder " : " or ") &&
                   low.IndexOf(de ? " und " : " and ") < 0,
                   name + " choice line: Bewegung ODER Angriff, exclusive oder (C1)");
                Ok(choice.Contains(de ? "nur eines von beiden" : "only one of the two") &&
                   choice.Contains(de ? "nie beides" : "never both") &&
                   choice.Contains(de ? "Angriffspunkt" : "attack point") &&
                   low.Contains(de ? "verbrauch" : "spends"),
                   name + " choice line: one per command, never both, AP cost (C1)");
                // Bewegung answer - printed move form, no attack inside
                Ok(mv.Contains(de ? ZweiFelder : "up to 2 spaces") &&
                   mv.Contains(de ? ZiehtSich : "repositions") &&
                   mv.Contains(de ? "verbrauch" : "spends"),
                   name + " movement answer: move up to the printed 2 + AP cost");
                Ok(!mv.Contains(GreiftWelches) &&
                   mv.ToLowerInvariant().IndexOf("attacks which") < 0 &&
                   mv.ToLowerInvariant().IndexOf("it attacks") < 0,
                   name + " movement answer orders NO attack (C1 exclusive)");
                // Angriff answer - the M6h attack order, no movement inside
                Ok(atk.Contains(de ? GreiftWelches : "attacks which"),
                   name + " attack answer orders an attack");
                Ok(!atk.Contains(ZiehtSich) && !atk.Contains(ZweiFelder) &&
                   atk.ToLowerInvariant().IndexOf("reposition") < 0 &&
                   atk.ToLowerInvariant().IndexOf("up to 2 spaces") < 0,
                   name + " attack answer orders NO movement (C1 exclusive)");
                // the no-command fallback names the printed executive order
                Ok(v.Contains(de ? "Kein Vollstreckungsbefehl?" : "No Executive Order?"),
                   name + " fallback branch names the printed executive order");
            }
        }
        if (!treeOk) return;
        CheckSharedLaws(name, de, key, v, regular);
    }

    // Shared bug-law tail of CheckTree - law subset that guards EVERY officer
    // popup tree shipped under uiTacticalAI: the ten legacy trees AND the O-C
    // supporter keep trees (card t_a2d88c50).
    static void CheckSharedLaws(string name, bool de, string key, string v, bool regular)
    {
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
            var first = v.Split('\n')[0];
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
            Ok(v.Contains(regular ? BefehlMoeglich : ExecChoiceDe),
               name + " De branch names the printed German keyword");
            var stripped = v.Replace(ExecDe, "");
            Ok(stripped.IndexOf("Order") < 0,
               name + " De carries no bare English 'Order' token");
        }
        else if (!regular)
        {
            Ok(v.Contains(ExecChoiceEn),
               name + " EN elite names the printed Executive Order");
        }
    }

    // ---- O-C supporter keep trees (card t_a2d88c50) -----------------------
    // apTreeOfficer<Pattern> -> apTreeOfficerKeep<Pattern>, elite pair stays
    // adjacent (apTreeOfficerElite<Pattern> -> apTreeOfficerEliteKeep<Pattern>).
    static string KeepOf(string legacyKey)
    {
        return legacyKey.StartsWith("apTreeOfficerElite")
            ? legacyKey.Replace("apTreeOfficerElite", "apTreeOfficerEliteKeep")
            : legacyKey.Replace("apTreeOfficer", "apTreeOfficerKeep");
    }

    // Laws that ONLY the supporter ersatzzweig carries (both regular and
    // elite keep trees): the frame, the E conditions as table text, the
    // removal of every self-combat instruction, and the GRUPPEN-LEINE clause
    // in the exact words of the O-B engine law (Baum says never "drift weg",
    // the engine never pulls farther from the anchor without an emergency -
    // the O-B check cases are explainable from the tree).
    static void CheckKeepLaws(string name, bool de, string v)
    {
        Ok(v.Contains(de ? "Kein Notfall? \u2192" : "No emergency? \u2192"),
           name + " keep frame branch present (O-C)");
        Ok(v.Contains(de ? "er steht unter Feuer" : "he is under fire") &&
           v.Contains(de ? "ein Held steht direkt an ihm" : "a hero stands directly beside him") &&
           v.Contains(de ? "letzte lebende imperiale Gruppe" : "last surviving Imperial group"),
           name + " names the emergency conditions as table text (O-C)");
        string[] banned = de
            ? new[] { "\u2192 selbst angreifen", "\u2192 es angreifen", "\u2192 ihn angreifen",
                      "Fokusziel angreifen" }
            : new[] { "\u2192 attack it", "\u2192 attack yourself", "attack the focus target" };
        foreach (var b in banned)
            Ok(v.IndexOf(b) < 0, name + " keep tree carries no self-attack: " + b);
        var core = de
            ? "nie weiter vom n\u00e4chsten imperialen Verb\u00fcndeten weg als er gerade entfernt ist"
            : "farther from your nearest Imperial ally than it is right now";
        int n = 0, i = 0;
        while ((i = v.IndexOf(core, i)) >= 0) { n++; i += core.Length; }
        Ok(n == 3, name + " group leash on the keep branch AND both movement tails (" + n + "/3)");
    }

    // The elite keep tree drops the Angriff receiver branch entirely (M6n's
    // Bewegung ODER Angriff question is an emergency privilege) and rewrites
    // the exec question to movement-only - so it runs its own shape check
    // instead of the legacy elite C1 law.
    static void CheckEliteKeepTree(Dictionary<string, string> flat, string tag, bool de,
                                   string key, string legacyKey)
    {
        string v, legacy;
        flat.TryGetValue(key, out v);
        flat.TryGetValue(legacyKey, out legacy);
        var name = tag + "/" + key;
        Ok(!string.IsNullOrEmpty(v), name + " present");
        if (string.IsNullOrEmpty(v)) return;
        int choices = 0, moves = 0, attacks = 0;
        string choice = null, mv = null;
        foreach (var ln in v.Split('\n'))
        {
            if (ln.Contains(de ? ExecChoiceDe : ExecChoiceEn)) { choices++; choice = ln; }
            if (ln.Contains(de ? MvBranchDe : MvBranchEn)) { moves++; mv = ln; }
            if (ln.Contains(de ? AtkBranchDe : AtkBranchEn)) attacks++;
        }
        Ok(choices == 1 && moves == 1 && attacks == 0,
           name + " exec question + Bewegung answer, NO Angriff branch (" +
           choices + "/" + moves + "/" + attacks + ")");
        if (choices != 1 || moves != 1 || attacks != 0) return;
        Ok(choice.Contains(de ? "nur Bewegung, nie ein Angriff" : "movement only, never an attack") &&
           choice.Contains(de ? "verbrauch" : "spends"),
           name + " exec question without an emergency: movement only + AP cost (O-C)");
        string legacyMv = null;
        foreach (var ln in (legacy ?? "").Split('\n'))
            if (ln.Contains(de ? MvBranchDe : MvBranchEn)) legacyMv = ln;
        Ok(legacyMv != null && mv == legacyMv,
           name + " Bewegung answer byte-identical to the legacy tree (O-C)");
        CheckKeepLaws(name, de, v);
        CheckSharedLaws(name, de, key, v, false);
    }

    // The why-line wrapper for a PROVEN emergency (EmergencyFor result). It
    // rides the tree head, EXPLAIN-independent - the color opener keeps it out
    // of the popup's colon-prefix bold whitelist by design.
    static void CheckNotfallKey(Dictionary<string, string> flat, string tag, bool de)
    {
        string v;
        flat.TryGetValue("apTreeOfficerNotfall", out v);
        var name = tag + "/apTreeOfficerNotfall";
        Ok(!string.IsNullOrEmpty(v), name + " present");
        if (string.IsNullOrEmpty(v)) return;
        Ok(v.StartsWith("<color"), name + " opens with a color tag (bold-whitelist law)");
        Ok(v.Replace("{0}", "").IndexOf('{') < 0, name + " only the {0} placeholder");
        Ok(v.Contains(de ? "Selbstverteidigung ist erlaubt." : "Self-defense is allowed."),
           name + " names the self-defense permission");
    }
}
