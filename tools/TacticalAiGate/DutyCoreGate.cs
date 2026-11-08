// P37c duty-core gate (card t_e80246ed): the Escort-Quota LAW re-executed in
// the SECOND compile context (this project + stub.cs, not the harness) over
// the SHIPPED hand-pinned fixture Assets/Tests/Fixtures/tacai/duty_core.json,
// plus the fairness source scan. If Core/DutyQuota.cs ever drifts from the
// pinned law (fmix64 order, k-band [1..ceil(n/2)], §9.2 floor, tripod law,
// degraded/matrix candidate selection, Explain disclosure) this gate fails
// even when someone regenerates or deletes the harness section.
//   1. Golden rerun: hoth10 3 rounds (round + combat + dutyOf), one_group,
//      six_groups x12 (band 1<=K<=3<6 + full round equality), no_duty
//      (Null-Law), shadow matrices via PrimeDoc (restore after).
//   2. Purity: two computes with equal inputs are equal (no statics/IO/RNG).
//   3. Fairness scan (concept §1b Fairness-Linie + G1/G3): DutyQuota.cs may
//      not reference Board/Cell/UnityEngine/pathfinding/Distance math — the
//      quota sees session key + rolled patterns + printed mobility + mined
//      anchor NAMES only; the Drohungsdefinition is PRINTED text (hop D),
//      never evaluated here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

public static class DutyCoreGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    public static void Run()
    {
        var fixPath = Path.Combine(App, "Assets", "Tests", "Fixtures", "tacai", "duty_core.json");
        if (!File.Exists(fixPath))
        {
            Console.WriteLine("FAIL duty_core.json missing");
            Environment.ExitCode = 1;
            return;
        }
        var v = Json.AsDict(Json.Parse(File.ReadAllText(fixPath)));

        // Shipped pattern-weight table: NO dutyModifiers key => degraded.
        var pwPath = Path.Combine(App, "Assets", "Resources", "Languages", "En",
                                  "tactical_pattern_weights.json");
        PatternWeights.PrimeDoc(Json.AsDict(Json.Parse(File.ReadAllText(pwPath))));
        var shipped = PatternWeights.Load();
        Ok(shipped != null && shipped.DutyMods.Count == 0,
           "dutyCore.shipped_table_degraded (dutyModifiers absent)");

        var groupsOf = new Func<object, List<DutyGroup>>(raw =>
            Json.AsList(raw).Select(x =>
            {
                var d = Json.AsDict(x);
                return new DutyGroup
                {
                    CardId = Json.AsStr(d["cardId"]),
                    Pattern = Json.AsStr(Json.Get(d, "pattern", "")),
                    Mobile = Json.AsBool(Json.Get(d, "mobile", true)),
                };
            }).ToList());

        // ---- 1. golden rerun ------------------------------------------------
        var h = Json.AsDict(v["hoth10"]);
        var hGroups = groupsOf(h["groups"]);
        foreach (var raw in Json.AsList(h["rounds"]))
            CheckCase("hoth10", sk: Json.AsStr(h["sessionKey"]), e: Json.AsDict(raw),
                      groups: hGroups, table: shipped, target: Json.AsStr(h["targetName"]),
                      hasDuty: true, tripodNever: false);

        var og = Json.AsDict(v["one_group"]);
        CheckCase("one", sk: Json.AsStr(og["sessionKey"]), e: Json.AsDict(og),
                  groups: groupsOf(og["groups"]), table: shipped,
                  target: Json.AsStr(Json.Get(Json.AsDict(og["expect"]), "TargetName", "")),
                  hasDuty: true, tripodNever: false);

        var sg = Json.AsDict(v["six_groups"]);
        var sgGroups = groupsOf(sg["groups"]);
        int sixOk = 0, sixBand = 0, sixN = 0;
        foreach (var raw in Json.AsList(sg["rounds"]))
        {
            var c = Json.AsDict(raw);
            var e = Json.AsDict(c["expect"]);
            var r = DutyQuota.Compute(Json.AsStr(sg["sessionKey"]), (int)Json.AsLong(e["RoundNo"]),
                                      sgGroups, shipped,
                                      Json.AsStr(Json.Get(e, "TargetName", "")), true);
            sixN++;
            if (SameRound(r, e) && DutyOfSig(r, sgGroups) == Json.AsStr(c["dutyOf"])) sixOk++;
            if (r.K >= 1 && r.K <= 3 && r.K < 6) sixBand++;
        }
        Ok(sixN == 12 && sixOk == 12, "dutyCore.six golden x12 (round+dutyOf) ok=" + sixOk);
        Ok(sixBand == 12, "dutyCore.six band 1<=k<=3<6 every round ok=" + sixBand + "/12");

        var nd = Json.AsDict(v["no_duty"]);
        CheckCase("no_duty", sk: Json.AsStr(nd["sessionKey"]), e: Json.AsDict(nd),
                  groups: groupsOf(nd["groups"]), table: shipped,
                  target: Json.AsStr(Json.Get(Json.AsDict(nd["expect"]), "TargetName", "")),
                  hasDuty: false, tripodNever: false);

        // shadow: PrimeDoc synthetic tables, RESTORE the shipped doc after.
        var shippedDoc = Json.AsDict(Json.Parse(File.ReadAllText(pwPath)));
        var sh = Json.AsDict(v["shadow"]);
        var shGroups = groupsOf(sh["groups"]);
        try
        {
            foreach (var key in new[] { "matrix_positive", "matrix_nonpositive" })
            {
                var block = Json.AsDict(sh[key]);
                PatternWeights.PrimeDoc(new Dictionary<string, object>(shippedDoc)
                {
                    ["dutyModifiers"] = block["dutyModifiers"],
                });
                var t = PatternWeights.Load();
                foreach (var raw in Json.AsList(block["rounds"]))
                    CheckCase("shadow." + key, sk: Json.AsStr(sh["sessionKey"]), e: Json.AsDict(raw),
                              groups: shGroups, table: t,
                              target: Json.AsStr(Json.Get(Json.AsDict(Json.AsDict(raw)["expect"]), "TargetName", "")),
                              hasDuty: true, tripodNever: true);
            }
        }
        finally
        {
            PatternWeights.PrimeDoc(shippedDoc);
            PatternWeights.Load();
        }

        // ---- 2. purity: pure function, equal inputs => equal outputs -------
        var a = DutyQuota.Compute(Json.AsStr(h["sessionKey"]), 1, groupsOf(h["groups"]), shipped,
                                  Json.AsStr(h["targetName"]), true);
        var b = DutyQuota.Compute(Json.AsStr(h["sessionKey"]), 1, groupsOf(h["groups"]), shipped,
                                  Json.AsStr(h["targetName"]), true);
        Ok(a.Explain == b.Explain && string.Join(",", a.Escorts) == string.Join(",", b.Escorts)
           && a.Mode == b.Mode, "dutyCore.purity two computes equal");

        // ---- 3. fairness source scan (concept §1b Fairness-Linie) ----------
        var srcPath = Path.Combine(App, "Assets", "Scripts", "TacticalAI", "Core", "DutyQuota.cs");
        var src = File.ReadAllText(srcPath);
        foreach (var banned in new[] { "UnityEngine", "Board", "Cell", "Distance",
                                       "pathfind", "Pathfind", "System.Random", "Guid.NewGuid" })
            Ok(!src.Contains(banned), "dutyCore.fairness DutyQuota.cs free of '" + banned + "'");

        Console.WriteLine("DutyCoreGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL"));
        if (fails != 0) Environment.ExitCode = 1;
    }

    static void CheckCase(string tag, string sk, Dictionary<string, object> e,
                          List<DutyGroup> groups, PatternWeightTable table, string target,
                          bool hasDuty, bool tripodNever)
    {
        var ex = Json.AsDict(e["expect"]);
        var r = DutyQuota.Compute(sk, (int)Json.AsLong(ex["RoundNo"]), groups, table, target, hasDuty);
        var roundOk = SameRound(r, ex);
        var dutyOk = e.ContainsKey("dutyOf") && DutyOfSig(r, groups) == Json.AsStr(e["dutyOf"]);
        var combatOk = !e.ContainsKey("combat") ||
            string.Join(",", r.Combat()) == string.Join(",", Json.AsList(e["combat"]).Select(Json.AsStr));
        var tripodOk = !tripodNever || r.DutyOf("DG007") == DutyQuota.DutyNone;
        Ok(roundOk && dutyOk && combatOk && tripodOk,
           "dutyCore." + tag + ".r" + ex["RoundNo"] +
           " (round=" + roundOk + " dutyOf=" + dutyOk + " combat=" + combatOk +
           (tripodNever ? " tripod=" + tripodOk : "") + ")");
    }

    static bool SameRound(DutyRound r, Dictionary<string, object> ex) =>
        (int)Json.AsLong(ex["RoundNo"]) == r.RoundNo &&
        (int)Json.AsLong(ex["Cap"]) == r.Cap &&
        (int)Json.AsLong(ex["K"]) == r.K &&
        Json.AsStr(ex["Mode"]) == r.Mode &&
        string.Join(",", Json.AsList(ex["Ranking"]).Select(Json.AsStr)) == string.Join(",", r.Ranking) &&
        string.Join(",", Json.AsList(ex["Escorts"]).Select(Json.AsStr)) == string.Join(",", r.Escorts) &&
        Json.AsStr(ex["Explain"]) == r.Explain &&
        Json.AsStr(Json.Get(ex, "TargetName", "")) == r.TargetName;

    static string DutyOfSig(DutyRound r, List<DutyGroup> groups) =>
        string.Join(",", groups.Select(g => g.CardId).Distinct()
                      .OrderBy(x => x, StringComparer.Ordinal)
                      .Select(id => id + "=" + r.DutyOf(id)));
}
