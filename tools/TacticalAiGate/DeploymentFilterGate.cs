// Player-25 M14b-C1 live gate: the deploy-point trait filter is language-
// neutral. DeploymentPointProps.IsFilteredOut builds its accepted-trait list
// from ENGLISH tokens ("Droid", "Trooper", ...), but card.traits carries the
// LOCALIZED lane after SetCardTranslations (DG013: EN "Droid" / DE "Droide" -
// the data truth in Languages/<lang>/DeploymentGroups/enemies.json). In DE
// that mismatch filtered EVERY DP out (findings_Player-25 C: the Suchdroide
// spawn self-forbade). The fix: SetCardTranslations caches the shipped EN
// lane on the FIRST pass (traitsEn ??= traits, before the localized
// overwrite), and IsFilteredOut compares EN-vs-EN when the cache exists.
// This gate runs the REAL DeploymentPointProps.cs (gate.csproj links it;
// card/enum stubs in stub.cs) against both hand-built cards and the SHIPPED
// DE/EN data bytes. Cards without an EN cache (imports, pre-cache saves)
// keep the old comparison - pinned too, so the fallback is law, not drift.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

public static class DeploymentFilterGate
{
    static int fails = 0;
    static void Eq(object expected, object actual, string what)
    {
        bool ok = Equals(expected, actual);
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what +
                          " | expected=" + expected + " actual=" + actual);
        if (!ok) { fails++; Environment.ExitCode = 1; }
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    // A droid card as the props law sees it: faction/size lanes neutral, so
    // every flip below is exactly the trait-filter law under test.
    static Saga.DeploymentCard Card(string[] traits, string[] traitsEn)
    {
        return new Saga.DeploymentCard
        {
            faction = "Imperial",
            miniSize = FigureSize.Medium1x2,
            traits = traits,
            traitsEn = traitsEn,
        };
    }

    // A DP accepting medium-size Imperial droids only (incMedium + incDroid).
    static Saga.DeploymentPointProps DroidDp()
    {
        return new Saga.DeploymentPointProps { incImperial = true, incMedium = true, incDroid = true };
    }

    // A DP accepting medium-size Imperial troopers only - droids must still
    // be filtered (the filter stays a filter; this hop moves no layout).
    static Saga.DeploymentPointProps TrooperDp()
    {
        return new Saga.DeploymentPointProps { incImperial = true, incMedium = true, incTrooper = true };
    }

    static List<Dictionary<string, object>> CardsOf(object root)
    {
        var outl = new List<Dictionary<string, object>>();
        if (root is List<object> list)
            foreach (var o in list) outl.Add(Json.AsDict(o));
        return outl;
    }

    static string[] TraitsOf(Dictionary<string, object> c)
    {
        return Json.AsList(Json.Get(c, "traits", new List<object>()))
                   .Select(x => Json.AsStr(x)).ToArray();
    }

    public static void Run()
    {
        Console.WriteLine("--- DeploymentFilterGate (P25-C1: EN-cache vs EN tokens, shipped DE/EN bytes) ---");

        // ------------------------------------------------- hand-built law cases
        // The fix: localized DE lane + EN cache on a droid DP -> NOT filtered.
        Eq(false, DroidDp().IsFilteredOut(Card(new[] { "Droide" }, new[] { "Droid" })),
           "DE lane 'Droide' + EN cache 'Droid' on droid DP passes (the fix)");
        // The fallback, pinned as law: no EN cache = pre-fix comparison.
        Eq(true, DroidDp().IsFilteredOut(Card(new[] { "Droide" }, null)),
           "DE lane without EN cache keeps old filtering (import/pre-cache fallback)");
        // Today's EN behavior, untouched.
        Eq(false, DroidDp().IsFilteredOut(Card(new[] { "Droid" }, null)),
           "EN lane 'Droid' on droid DP passes (EN games unmoved)");
        Eq(false, DroidDp().IsFilteredOut(Card(new[] { "Droid" }, new[] { "Droid" })),
           "EN lane + identical EN cache passes (EN games after cache pass)");
        // The filter stays a filter: EN-matched droid on a trooper-only DP.
        Eq(true, TrooperDp().IsFilteredOut(Card(new[] { "Droide" }, new[] { "Droid" })),
           "droid card stays filtered on trooper-only DP (no logic-layout change)");
        // Non-trait lanes are NOT language-blinded by this hop: faction/size
        // still filter even with a matching EN cache.
        Eq(true, DroidDp().IsFilteredOut(new Saga.DeploymentCard
             { faction = "Imperial", miniSize = FigureSize.Small1x1,
               traits = new[] { "Droide" }, traitsEn = new[] { "Droid" } }),
           "small size still filtered on medium-only DP despite EN cache");

        // ------------------------------------------- shipped data: DG013 truth
        var enPath = Path.Combine(App, "Assets/Resources/Languages/En/DeploymentGroups/enemies.json");
        var dePath = Path.Combine(App, "Assets/Resources/Languages/De/DeploymentGroups/enemies.json");
        // Shipped card/language JSON carries trailing commas (legacy quirk,
        // OfficerSupporterGate reads these same files the same way).
        Func<string, object> load = p => Json.Parse(System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(p), @",\s*([}\]])", "$1"));
        var en = CardsOf(load(enPath));
        var de = CardsOf(load(dePath));

        var enIds = new SortedSet<string>(en.Select(c => Json.AsStr(Json.Get(c, "id", ""))), StringComparer.Ordinal);
        var deIds = new SortedSet<string>(de.Select(c => Json.AsStr(Json.Get(c, "id", ""))), StringComparer.Ordinal);
        Eq(enIds.Count, deIds.Count, "DG overlay id parity: count En == De");
        Eq(true, enIds.SetEquals(deIds), "DG overlay id parity: id sets En == De (ID, not language)");

        var en13 = en.First(c => Json.AsStr(Json.Get(c, "id", "")) == "DG013");
        var de13 = de.First(c => Json.AsStr(Json.Get(c, "id", "")) == "DG013");
        var en13Traits = TraitsOf(en13);
        var de13Traits = TraitsOf(de13);
        Eq(true, en13Traits.Contains("Droid"), "DG013 shipped EN lane carries DP token 'Droid' | actual=" + string.Join(",", en13Traits));
        Eq(true, de13Traits.Contains("Droide"), "DG013 shipped DE lane carries 'Droide' (localization) | actual=" + string.Join(",", de13Traits));
        Eq(false, de13Traits.SequenceEqual(en13Traits), "DG013 DE lane != EN lane (the mismatch this hop bridges)");

        // End-to-end over the SHIPPED bytes: the real DE runtime card is
        // DE lane + cached EN lane -> the droid DP accepts it again.
        Eq(false, DroidDp().IsFilteredOut(Card(de13Traits, en13Traits)),
           "DG013 real DE bytes + EN cache pass the droid DP (Player-25 spawn law restored)");
        Eq(true, DroidDp().IsFilteredOut(Card(de13Traits, null)),
           "DG013 real DE bytes without cache still self-forbid (pre-fix state pinned)");

        Console.WriteLine(fails == 0 ? "DeploymentFilterGate: ALL PASS"
                                     : "DeploymentFilterGate: " + fails + " FAILURES");
    }
}
