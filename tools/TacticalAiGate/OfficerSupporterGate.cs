// O-A supporter belt gate (concept offizier_supporter v1 par.2, Thomas-
// Gesetz 04.11., card t_4acaa220). RED law: the supporter identity of the
// tactical AI is driven by the PRINTED command token, and the token belt
// (TacticalAIBridge.ToRecord + PrintedOfficerSupporter: DG004/DG005 plain,
// DG006 executive) must stay TRUE for the shipped card data. This gate
// verifies against the SHIPPED files (zero fixtures):
//   1. Across CardData pools + ALL 11 language DeploymentGroups overlays,
//      the set of cards whose ability lane prints one of the four command
//      token names ("{A} Order", "{A} Befehl", "{A} Executive Order",
//      "{A} Vollstreckungsbefehl") is EXACTLY {DG004, DG005, DG006}. A new
//      card starting to print the token goes RED here, not silently green
//      in a popup (the belt comment in ToRecord says "exactly these three"
//      - this gate is what keeps that sentence true).
//   2. The field-commander exception ids DG075/DG078 exist in the shipped
//      villain pool and print NO command token today - the exclusion in
//      Core DeploymentCard.FieldCommanderExceptions stays a wall against
//      data drift, not a live behaviour (shipped-name truth 04.11.:
//      DG075 = Kayn Somos, DG078 = General Sorin; the concept text has the
//      pair swapped - both ids exclude, behaviour identical).
//   3. Core round-trip: a record with the token keyword is a Supporter;
//      the same record under the ids DG075/DG078 is NOT (id wall, keyed
//      never by names); a tokenless record is NOT.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TacticalAI.Core;

public static class OfficerSupporterGate
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

    // Shipped card stores carry trailing commas (hand-authored) - the same
    // sanitize the Unity loader tolerates (PatternRowLanguageGate twin).
    static object ParseLenientRaw(string path)
    {
        var t = Regex.Replace(File.ReadAllText(path), @",\s*([}\]])", "$1");
        return Json.Parse(t);
    }

    static readonly HashSet<string> CommandTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "{A} Order", "{A} Befehl", "{A} Executive Order", "{A} Vollstreckungsbefehl",
    };

    static IEnumerable<string> CardFiles()
    {
        yield return Path.Combine(App, "Assets/Resources/CardData/enemies.json");
        yield return Path.Combine(App, "Assets/Resources/CardData/villains.json");
        foreach (var loc in new[] { "Br", "De", "En", "Es", "Fr", "Hu", "It", "Nl", "No", "Pl", "Ru" })
        {
            yield return Path.Combine(App, "Assets/Resources/Languages/" + loc + "/DeploymentGroups/enemies.json");
            yield return Path.Combine(App, "Assets/Resources/Languages/" + loc + "/DeploymentGroups/villains.json");
        }
    }

    static List<Dictionary<string, object>> CardsOf(object root)
    {
        var outl = new List<Dictionary<string, object>>();
        if (root is List<object> list)
            foreach (var o in list) outl.Add(Json.AsDict(o));
        else if (root is Dictionary<string, object> d && d.ContainsKey("cards"))
            foreach (var o in Json.AsList(d["cards"])) outl.Add(Json.AsDict(o));
        return outl;
    }

    static bool PrintsToken(Dictionary<string, object> c, out string lane)
    {
        lane = null;
        foreach (var a in Json.AsList(Json.Get(c, "abilities", new List<object>())))
        {
            var ad = Json.AsDict(a);
            var n = Json.AsStr(Json.Get(ad, "name", ""));
            if (CommandTokens.Contains(n)) { lane = n; return true; }
        }
        return false;
    }

    public static void Run()
    {
        Console.WriteLine("--- OfficerSupporterGate (printed command-token belt, shipped files) ---");

        // 1. the printing set over ALL shipped card stores == the belt trio.
        var printers = new SortedSet<string>(StringComparer.Ordinal);
        var offenders = new List<string>();
        var files = 0;
        foreach (var path in CardFiles())
        {
            if (!File.Exists(path)) { offenders.Add("MISSING " + path); continue; }
            files++;
            foreach (var c in CardsOf(ParseLenientRaw(path)))
            {
                string lane;
                if (PrintsToken(c, out lane))
                {
                    var id = Json.AsStr(Json.Get(c, "id", "?"));
                    printers.Add(id);
                }
            }
        }
        Eq(24, files, "24 shipped card stores scanned (2 pools + 11 langs x 2)");
        Eq("DG004,DG005,DG006", string.Join(",", printers),
           "printed command-token set over all shipped card data == the ToRecord belt trio");

        // 2. exception ids exist in the shipped villain pool, print nothing.
        var villains = CardsOf(ParseLenientRaw(
            Path.Combine(App, "Assets/Resources/CardData/villains.json")));
        string name75 = null, name78 = null;
        foreach (var c in villains)
        {
            var id = Json.AsStr(Json.Get(c, "id", ""));
            if (id == "DG075") name75 = Json.AsStr(Json.Get(c, "name", ""));
            if (id == "DG078") name78 = Json.AsStr(Json.Get(c, "name", ""));
        }
        Eq(true, name75 != null, "DG075 exists in shipped villains pool");
        Eq(true, name78 != null, "DG078 exists in shipped villains pool");
        // Shipped-name truth pin (the concept text swaps the pair; the ids
        // exclude either way, this row keeps the comment in DeploymentCard honest).
        Eq("Kayn Somos", name75, "shipped name of DG075");
        Eq("General Sorin", name78, "shipped name of DG078");
        Eq(false, printers.Contains("DG075"), "DG075 prints no command token today");
        Eq(false, printers.Contains("DG078"), "DG078 prints no command token today");

        // 3. Core round-trip of the supporter law (keyword + id wall).
        DeploymentCard From(string id, string kw)
        {
            var kws = new List<object>();
            if (kw != null) kws.Add(kw);
            return DeploymentCard.FromRecord(new Dictionary<string, object>
            {
                { "id", id }, { "name", "Probe" }, { "miniSize", "Small1x1" },
                { "speed", 4L }, { "health", 3L }, { "attackType", "Ranged" },
                { "keywords", kws },
            });
        }
        Eq(true, From("DG004", "OfficerOrder").Supporter, "token keyword -> Supporter");
        Eq(true, From("DG999", "OfficerExecOrder").Supporter, "import with token inherits (id-agnostic)");
        Eq(false, From("DG075", "OfficerOrder").Supporter, "id wall DG075 beats the token (Kayn Somos)");
        Eq(false, From("DG078", "OfficerExecOrder").Supporter, "id wall DG078 beats the token (General Sorin)");
        Eq(false, From("DG021", null).Supporter, "tokenless card is never a Supporter");
        // The exception set itself is the law the bridge helper mirrors.
        Eq("DG075,DG078", string.Join(",", new SortedSet<string>(DeploymentCard.FieldCommanderExceptions, StringComparer.Ordinal)),
           "field-commander exception set is exactly the two named-villain ids");

        foreach (var o in offenders) Console.WriteLine("FAIL " + o);
        fails += offenders.Count;
        Console.WriteLine(fails == 0 ? "OfficerSupporterGate: ALL PASS" : "OfficerSupporterGate: " + fails + " FAILURES");
        if (fails > 0) Environment.ExitCode = 1;
    }
}
