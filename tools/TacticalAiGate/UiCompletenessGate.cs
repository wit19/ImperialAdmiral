// M13d live gate (zero fixtures): the EMPIRE12 playtest (De, 28.10.) logged
// "LoadUILanguage()::Found missing UI translation [De]" for 20 keys on EVERY
// screen load. DataStore.LoadUILanguage warns exactly when an EN string field
// has no non-empty counterpart in the active language file - so this gate
// pins the shipped-assets contract the same way: EVERY key of the shipped
// En/ui.json must exist and be non-empty in EVERY shipped language ui.json
// (missing count must be 0 for all 11), the 20 playtest keys are additionally
// listed explicitly, and the German reference lines from the ticket are
// pinned verbatim (Thomas: De is the shipped reference translation).
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

    // German Fachsprache reference lines (ticket wording: Vor/Zurueck,
    // im Uhrzeigersinn; key-mapper context for reset/pressKeyMessage).
    static readonly Dictionary<string, string> DeReference = new Dictionary<string, string>
    {
        { "uiSetup.appMissionFormat", "APP-MISSIONSFORMAT:" },
        { "uiSettings.mapperHeader", "TASTENBELEGUNG" },
        { "uiSettings.mapNavForwardLabel", "KARTE VOR" },
        { "uiSettings.mapNavBackLabel", "KARTE ZURUECK" },
        { "uiSettings.mapNavCWLabel", "KAMERA IM UHRZEIGERSINN DREHEN" },
        { "uiSettings.mapNavCCWLabel", "KAMERA GEGEN DEN UHRZEIGERSINN DREHEN" },
        { "uiSettings.reset", "STANDARDBELEGUNG" },
        { "uiSettings.pressKeyMessage", "TASTE DRUECKEN..." },
    };

    // M14f end-state pins (card t_68945694): the FOUR doctrine strings this
    // hop owns - apTreeHunter + ordExplainFokuszone, De + En - verbatim. The
    // card's mutation probe ("ein Satzzeichen im neuen Merksatz entfernen ->
    // Harness/Gate MUSS failen") needs a prose pin: the structure gates
    // parse, count keys and placeholders but NEVER read the sentence itself
    // (M14d QA probe A proved a punctuation edit sails through rc=0). The
    // law rides with the pin: any future text hop on these two keys updates
    // the shipped strings AND this reference in the same commit - a prose
    // edit that skips the pin goes RED, never silent. De carries the shipped
    // reference translation (Thomas), En is the source lane the other nine
    // languages mirror verbatim (M13c convention) - pinning En covers the
    // mirror chain too. The literals carry the ui.json newlines as \n
    // escapes and the umlauts exactly as shipped.
    static readonly Dictionary<string, string> HunterLawLines = new Dictionary<string, string>
    {
        { "De.apTreeHunter",
            "<i><color=#9FC6FF>Fokuszone: der Bereich, auf den das aktuelle Missionsziel der Rebellen zeigt – dort jagt der Jäger.</color></i>\n<b>? Angriffspunkt verfügbar?</b>\n<color=#FFD76A>    Held IN der Fokuszone in Reichweite und freier Sichtlinie? → ihn angreifen (mehrere Zone-Helden? → der mit dem höheren Bedrohungswert; EXPLAIN zeigt ihn).</color>\n<color=#FFD76A>    Kein Zone-Held greifbar? → das FOKUSZIEL angreifen — auch wenn es außerhalb der Zone steht oder gerade auf dem Weg dorthin ist (EXPLAIN zeigt, warum).</color>\n<color=#FFD76A>    Kein Angriff möglich? → dieser Punkt wird Bewegung: bis zu {0} Felder in die Fokuszone bzw. auf den Zone-Helden zu.</color>\n<b>? Angriffspunkt(e) verbraucht?</b>\n<color=#FFD76A>    Dann ist dieser Punkt Bewegung: bis zu {0} Felder in die Fokuszone bzw. auf den Zone-Helden zu. Merksatz der Jäger: ein Held AUßERHALB der Fokuszone, der nicht das FOKUSZIEL ist, ist nicht euer Ziel – ignoriert ihn, selbst wenn er im Weg steht. Nur wer EUCH angreift, darf in dieser Runde euren Rückschuss ernten; verfolgt wird er nicht.</color>" },
        { "De.ordExplainFokuszone",
            "Fokuszone: {0} arbeitet am Missionsziel — {1} (Jäger, ab Stufe 3) folgt in die Zone und bleibt bei ihren Helden; ein Held außerhalb wird angegriffen, wenn er das Fokusziel ist, sonst ignoriert; wer {1} selbst angreift, darf mit einem Rückschuss rechnen." },
        { "En.apTreeHunter",
            "<i><color=#9FC6FF>Focus zone: the area the rebels' current mission objective points at – that is where the hunter hunts.</color></i>\n<b>? Attack point available?</b>\n<color=#FFD76A>    Hero INSIDE the focus zone in range and clear line of sight? → attack him (several zone heroes? → the one with the higher threat value; EXPLAIN shows it).</color>\n<color=#FFD76A>    No zone hero within reach? → attack the FOCUS TARGET — even when it stands outside the zone or is on its way there (EXPLAIN shows why).</color>\n<color=#FFD76A>    No attack possible? → this point becomes movement: move up to {0} spaces toward the focus zone resp. the zone hero.</color>\n<b>? Attack point(s) spent?</b>\n<color=#FFD76A>    Then this point is movement: move up to {0} spaces toward the focus zone resp. the zone hero. The hunter's creed: a hero OUTSIDE the focus zone who is not your focus target is not your target – ignore him, even when he blocks your path. Only whoever attacks YOU may reap your shot back this round; nobody is pursued for it.</color>" },
        { "En.ordExplainFokuszone",
            "Focus zone: {0} works the mission objective – {1} (hunter, tier 3+) follows into the zone and sticks to its heroes; a hero outside is attacked only when he is the focus target, otherwise ignored; whoever attacks {1} may expect one shot back." },
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

        // 3b) M14f hunter doctrine strings verbatim (prose pin - the
        // HunterLawLines comment carries the law this rides).
        foreach (var kv in HunterLawLines)
        {
            var dot = kv.Key.IndexOf('.');
            var loc = kv.Key.Substring(0, dot);
            var key = kv.Key.Substring(dot + 1);
            var sec = Json.AsDict(Json.Parse(File.ReadAllText(
                Path.Combine(langRoot, loc, "ui.json"))));
            var flatSec = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var leaf in Json.AsDict(sec["uiTacticalAI"]))
                if (leaf.Value is string) flatSec[leaf.Key] = (string)leaf.Value;
            string hv;
            flatSec.TryGetValue(key, out hv);
            Ok(hv == kv.Value, loc + "/" + key + " == M14f end-state string verbatim"
                + (hv == kv.Value ? "" : " (actual: " + (hv ?? "<null>") + ")"));
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

        Console.WriteLine(fails == 0 ? "UiCompletenessGate: ALL PASS" : "UiCompletenessGate: FAILURES=" + fails);
        return fails;
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
