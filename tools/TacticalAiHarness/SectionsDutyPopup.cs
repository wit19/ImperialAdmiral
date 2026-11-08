// P37d (card t_ffc705af) section: the Hop D popup canon. Two laws pinned:
//  (A) KEY INVENTORY - apDutyEscortTree + apDutyQuotaLine (declared in
//      UITacticalAI by P37c, VALUES ship here) exist and are non-empty in
//      every one of the 11 shipped ui.json; De and En carry real
//      translations (signed anchor fragments below); the other nine are
//      byte-exact EN copies (M6c fleet law - the language-dependency QA
//      lesson: the popup builds its rows from the KEYS, no hardcoded text).
//  (B) POPUP VECTOR - the card's GUETEN vector: on the REAL HOTH10 duty fact
//      (anchor read through the P37b fail-closed reader, name measured, never
//      hardcoded) an Escort-Aktivierung shows the duty rows in DE and EN, a
//      Gefechts-Aktivierung shows NONE. The quota law itself is P37c's
//      (duty_core.json - untouched here, this section only RENDERS). The
//      renderer is the exact RenderBlocks seam mirror (TF fill, missing-key
//      fail-closed guard, brace guard, line split) - the Unity popup cannot
//      run here, so the mirror is the law's executable reading and the Gate
//      re-runs it in its own compile context.
// Every expectation is HAND-PINNED (duty_popup.json); canon untouched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDutyPopup()
        {
            var v = LoadJson("duty_popup.json");
            var langs = Json.AsList(v["langs"]).Select(Json.AsStr).ToList();
            Check("langs inventory", 11L, (long)langs.Count);
            var keys = Json.AsList(v["keys"]).Select(Json.AsStr).ToList();

            // one parsed ui.json per language (section 'uiTacticalAI' only)
            var secs = new Dictionary<string, Dictionary<string, object>>();
            foreach (var l in langs)
            {
                var doc = Json.AsDict(LoadLangJson(l, "ui.json"));
                secs[l] = Json.AsDict(doc["uiTacticalAI"]);
            }

            // ------------------------------------------------ 1 presence 11/11
            foreach (var l in langs)
                foreach (var k in keys)
                {
                    object val;
                    var have = secs[l].TryGetValue(k, out val);
                    Check("present[" + k + "|" + l + "]", true,
                          have && !string.IsNullOrEmpty(Json.AsStr(val)));
                }

            // ------------------------------- 2 De/En real, 9 byte-exact EN copies
            var de = secs["De"].ToDictionary(kv => kv.Key, kv => Json.AsStr(kv.Value));
            var en = secs["En"].ToDictionary(kv => kv.Key, kv => Json.AsStr(kv.Value));
            var deBlob = de["apDutyEscortTree"] + "\n" + de["apDutyQuotaLine"];
            var enBlob = en["apDutyEscortTree"] + "\n" + en["apDutyQuotaLine"];
            foreach (var f in Json.AsList(v["de_contains"]).Select(Json.AsStr))
                Check("de_canon contains '" + f.Substring(0, Math.Min(40, f.Length)) + "'", true,
                      deBlob.Contains(f));
            foreach (var f in Json.AsList(v["en_contains"]).Select(Json.AsStr))
                Check("en_canon contains '" + f.Substring(0, Math.Min(40, f.Length)) + "'", true,
                      enBlob.Contains(f));
            foreach (var l in langs)
            {
                if (l == "De" || l == "En") continue;
                foreach (var k in keys)
                    Check("en_copy[" + k + "|" + l + "]", en[k], Json.AsStr(secs[l][k]));
            }

            // tree row count (the printed Wenn-Dann shape): De and En 8 rows
            foreach (var l in new[] { "De", "En" })
                Check("tree_rows[" + l + "]", Json.AsLong(v["tree_rows"]),
                      (long)en_de_rows(secs[l]));
            Check("tree_rows equal De==En shape", Json.AsLong(v["tree_rows"]),
                  (long)en_de_rows(secs["De"]));

            // ------------------------------ 3 the real HOTH10 escort/combat fact
            var h = Json.AsDict(v["hoth10"]);
            var artPath = RepoFile("Resources", "Languages", "En", "tactical_mission_critical.json");
            Check("artifact.exists", true, artPath != null);
            var art = Json.Parse(File.ReadAllText(artPath));
            var missionFile = Json.AsStr(h["missionFile"]);
            var missionPath = RepoFile("Resources", "SagaMissions", Json.AsStr(h["dir"]), missionFile);
            Check("hoth10.mission", true, missionPath != null);
            var duties = DutyAnchors.ForMission(art, missionFile, MissionEntityGuids(missionPath));
            Check("hoth10.duties", 1L, (long)duties.Count);
            var target = duties.SelectMany(d => d.TargetFields).Select(t => t.Name).FirstOrDefault()
                         ?? duties.SelectMany(d => d.MarkerTokens).Select(t => t.Name).FirstOrDefault()
                         ?? "";
            Check("hoth10.target read", "Cell", target);

            // P37c law, REUSED read-only on the shipped weight asset (degraded
            // path - no dutyModifiers key; duty_core.json owns that law).
            var pwPath = RepoFile("Resources", "Languages", "En", "tactical_pattern_weights.json");
            PatternWeights.PrimeDoc(Json.AsDict(Json.Parse(File.ReadAllText(pwPath))));
            var tShipped = PatternWeights.Load();
            var groups = DutyGroups(Json.AsList(h["groups"]));
            var round = (int)Json.AsLong(h["round"]);
            var r = DutyQuota.Compute(Json.AsStr(h["sessionKey"]), round, groups,
                                       tShipped, target, true);
            // n=2 => cap=ceil(2/2)=1: exactly one escort, exactly one combat,
            // every seed (band [1..1]) - structural pin, seed-stable by law.
            Check("hoth10.k1", 1L, (long)r.K);
            Check("hoth10.escorts", 1L, (long)r.Escorts.Count);
            Check("hoth10.combat", 1L, (long)CombatOf(r).Count);

            var exp = Json.AsDict(h["expect"]);

            // ---- Escort-Aktivierung: DE and EN show the duty rows ----------
            foreach (var l in new[] { "De", "En" })
            {
                var tree = Json.AsStr(secs[l]["apDutyEscortTree"]);
                var treeNm = Json.AsStr(secs[l]["apDutyEscortTreeNameless"]);
                var quota = Json.AsStr(secs[l]["apDutyQuotaLine"]);
                var rows = DutyPopupRows(tree, treeNm, quota, r.TargetName, r.Explain,
                                         escort: r.DutyOf(r.Escorts[0]) == DutyQuota.DutyEscort);
                Check("escort_rows[" + l + "]", Json.AsLong(exp["row_total"]), (long)rows.Count);
                var head = exp[l == "De" ? "de_head_contains" : "en_head_contains"];
                Check("escort_head[" + l + "] contains '" + Json.AsStr(head) + "'", true,
                      rows.Count > 0 && rows[0].Contains(Json.AsStr(head)));
                Check("quota_row[" + l + "] cites the Explain", true,
                      rows.Any(x => x.Contains(Json.AsStr(exp["quota_contains"]))));
                Check("no_brace_left[" + l + "]", false, rows.Any(x => x.Contains("{")));
                // determinism: a popup reopen renders identically
                var rows2 = DutyPopupRows(tree, treeNm, quota, r.TargetName, r.Explain,
                                          escort: r.DutyOf(r.Escorts[0]) == DutyQuota.DutyEscort);
                Check("reopen[" + l + "]", string.Join("\n", rows), string.Join("\n", rows2));
            }

            // ---- Gefechts-Aktivierung: NO duty rows (card vector law) ------
            var combatId = CombatOf(r)[0];
            foreach (var l in new[] { "De", "En" })
            {
                var tree = Json.AsStr(secs[l]["apDutyEscortTree"]);
                var treeNm = Json.AsStr(secs[l]["apDutyEscortTreeNameless"]);
                var quota = Json.AsStr(secs[l]["apDutyQuotaLine"]);
                var rows = DutyPopupRows(tree, treeNm, quota, r.TargetName, r.Explain,
                                         escort: r.DutyOf(combatId) == DutyQuota.DutyEscort);
                Check("combat_no_duty_rows[" + l + "]", 0L, (long)rows.Count);
            }

            // ---- Null-Law: missing key (pre-Hop-D asset state) => no rows --
            var nullRows = DutyPopupRows("", "", "", r.TargetName, r.Explain, escort: true);
            Check("null_law.missing_key_no_rows", 0L, (long)nullRows.Count);
            var noDutyRows = DutyPopupRows(Json.AsStr(secs["De"]["apDutyEscortTree"]),
                                           Json.AsStr(secs["De"]["apDutyEscortTreeNameless"]),
                                           Json.AsStr(secs["De"]["apDutyQuotaLine"]),
                                           "", "", escort: false);
            Check("null_law.no_duty_no_rows", 0L, (long)noDutyRows.Count);
        }

        // P39 MINOR-1b section (card t_a697bc1a): the NAMELESS escort shape.
        // The QA MINOR-1 claimed the anchor name is missing from officer
        // texts - MEASURED FALSE for the six anchored duty missions (the
        // escort popup names the anchor since P37d, duty_popup.json pins it
        // for HOTH10 DE/EN). The one REAL gap: CORE23 is the shipped
        // anchorless duty (duties=1, anchors=0) - its TargetName is "" and
        // the named key rendered "Missionsobjekt  —" with a dangling gap.
        // This section pins: (A) the CORE23 premise on the SHIPPED artifact
        // (duties/anchors/target measured, never hardcoded), (B) key
        // inventory 11/11 (De genuine with umlaut, 9 byte-exact EN copies),
        // (C) the seam mirror: empty TargetName -> nameless tree, named
        // TargetName -> named tree, missing nameless key -> nothing,
        // combat -> nothing. Rows: 8 tree + 1 quota = 9, like the named case.
        static void SecDutyNameless()
        {
            var v = LoadJson("duty_nameless.json");
            var langs = Json.AsList(v["langs"]).Select(Json.AsStr).ToList();
            Check("langs inventory", 11L, (long)langs.Count);

            var secs = new Dictionary<string, Dictionary<string, object>>();
            foreach (var l in langs)
                secs[l] = Json.AsDict(Json.AsDict(LoadLangJson(l, "ui.json"))["uiTacticalAI"]);

            // (B) inventory: present 11/11, De/En genuine, 9 byte-exact EN copies
            foreach (var l in langs)
            {
                object val;
                var have = secs[l].TryGetValue("apDutyEscortTreeNameless", out val);
                Check("present[apDutyEscortTreeNameless|" + l + "]", true,
                      have && !string.IsNullOrEmpty(Json.AsStr(val)));
            }
            var de = Json.AsStr(secs["De"]["apDutyEscortTreeNameless"]);
            var en = Json.AsStr(secs["En"]["apDutyEscortTreeNameless"]);
            Check("de_head contains '" + Json.AsStr(v["de_head_contains"]) + "'", true,
                  de.Contains(Json.AsStr(v["de_head_contains"])));
            Check("de carries the real umlaut word", true, de.Contains(Json.AsStr(v["de_umlaut_contains"])));
            Check("en_head contains '" + Json.AsStr(v["en_head_contains"]) + "'", true,
                  en.Contains(Json.AsStr(v["en_head_contains"])));
            foreach (var l in langs)
            {
                if (l == "De" || l == "En") continue;
                Check("en_copy[apDutyEscortTreeNameless|" + l + "]", en, Json.AsStr(secs[l]["apDutyEscortTreeNameless"]));
            }
            // same printed Wenn-Dann shape as the named tree (row law of the
            // popup section; only the head line lost its name slot)
            static int rowsOf(string t) => (t ?? "").Replace("\r", "").Split('\n').Count(x => x.Trim().Length > 0);
            foreach (var l in new[] { "De", "En" })
                Check("nameless_tree_rows[" + l + "] == named tree rows",
                      (long)rowsOf(Json.AsStr(secs[l]["apDutyEscortTree"])),
                      (long)rowsOf(Json.AsStr(secs[l]["apDutyEscortTreeNameless"])));

            // (A) the CORE23 premise on the SHIPPED artifact (bridge truth)
            var c23 = Json.AsDict(v["core23"]);
            var artPath = RepoFile("Resources", "Languages", "En", "tactical_mission_critical.json");
            Check("artifact.exists", true, artPath != null);
            var art = Json.Parse(File.ReadAllText(artPath));
            var missionFile = Json.AsStr(c23["missionFile"]);
            var missionPath = RepoFile("Resources", "SagaMissions", Json.AsStr(c23["dir"]), missionFile);
            Check("core23.mission", true, missionPath != null);
            var duties = DutyAnchors.ForMission(art, missionFile, MissionEntityGuids(missionPath));
            Check("core23.duties", Json.AsLong(c23["duties"]), (long)duties.Count);
            Check("core23.anchors", Json.AsLong(c23["anchors"]), (long)duties.Sum(d => d.AnchorCount));
            // the bridge target-name law verbatim: first TargetField name,
            // else first MarkerToken name, else "" - the gap in words.
            var target = duties.SelectMany(d => d.TargetFields).Select(t => t.Name).FirstOrDefault()
                         ?? duties.SelectMany(d => d.MarkerTokens).Select(t => t.Name).FirstOrDefault()
                         ?? "";
            Check("core23.target is EMPTY (the measured gap)", Json.AsStr(c23["target"]), target);

            // (C) the seam on a real Compute over the CORE23 shape (empty
            // target flows through DutyQuota.Compute - TargetName stays "")
            var pwPath = RepoFile("Resources", "Languages", "En", "tactical_pattern_weights.json");
            PatternWeights.PrimeDoc(Json.AsDict(Json.Parse(File.ReadAllText(pwPath))));
            var tShipped = PatternWeights.Load();
            var groups = DutyGroups(Json.AsList(v["groups"]));
            var r = DutyQuota.Compute(Json.AsStr(v["sessionKey"]), (int)Json.AsLong(v["round"]),
                                       groups, tShipped, target, true);
            Check("core23.round TargetName empty", "", r.TargetName);
            Check("core23.band k in [1..cap] cap<n", true, r.K >= 1 && r.K <= r.Cap && r.Cap < groups.Count);

            foreach (var l in new[] { "De", "En" })
            {
                var tree = Json.AsStr(secs[l]["apDutyEscortTree"]);
                var treeNm = Json.AsStr(secs[l]["apDutyEscortTreeNameless"]);
                var quota = Json.AsStr(secs[l]["apDutyQuotaLine"]);
                var rows = DutyPopupRows(tree, treeNm, quota, r.TargetName, r.Explain, escort: true);
                Check("nameless_rows[" + l + "]", Json.AsLong(v["row_total"]), (long)rows.Count);
                var head = Json.AsStr(v[l == "De" ? "de_head_contains" : "en_head_contains"]);
                Check("nameless_head[" + l + "] names no anchor", true,
                      rows.Count > 0 && rows[0].Contains(head));
                Check("nameless_quota_row[" + l + "] cites the Explain", true,
                      rows.Any(x => x.Contains(Json.AsStr(v["quota_contains"]))));
                Check("nameless_no_brace_left[" + l + "]", false, rows.Any(x => x.Contains("{")));
                var rows2 = DutyPopupRows(tree, treeNm, quota, r.TargetName, r.Explain, escort: true);
                Check("nameless_reopen[" + l + "]", string.Join("\n", rows), string.Join("\n", rows2));
                // control: a NAMED target still selects the NAMED key (bit law
                // of the P37d popup vectors - unchanged by the P39 seam)
                var named = DutyPopupRows(tree, treeNm, quota, "Cell", r.Explain, escort: true);
                Check("named_control[" + l + "]", true,
                      named.Count > 0 && named[0].Contains(Json.AsStr(v[l == "De" ? "named_de_head_contains" : "named_en_head_contains"])));
            }
            // fail-closed: missing nameless key (and missing quota key, the
            // P37d null_law reading) + empty target => no rows at all
            var deadRows = DutyPopupRows("", "", "", "", r.Explain, escort: true);
            Check("nameless_law.missing_key_no_rows", 0L, (long)deadRows.Count);
            // combat with the anchorless shape: still ZERO duty rows (P37d law)
            var combatRows = DutyPopupRows(Json.AsStr(secs["De"]["apDutyEscortTree"]),
                                           Json.AsStr(secs["De"]["apDutyEscortTreeNameless"]),
                                           Json.AsStr(secs["De"]["apDutyQuotaLine"]),
                                           r.TargetName, r.Explain, escort: false);
            Check("nameless_law.combat_no_duty_rows", 0L, (long)combatRows.Count);
        }

        // non-empty rendered row count of a language's tree value (split law
        // of RenderBlocks: \r dropped, empty rows skipped)
        static int en_de_rows(Dictionary<string, object> sec)
        {
            var t = Json.AsStr(sec["apDutyEscortTree"]) ?? "";
            return t.Replace("\r", "").Split('\n').Count(x => x.Trim().Length > 0);
        }

        // Exact RenderBlocks duty-seam mirror (Bridge lines: escort-gated
        // tree fill + escort-gated quota fill, TF brace-silent fill, missing
        // key -> nothing, leftover placeholder -> nothing). One law, one
        // reading; DutyScrubGate/DutyCoreGate re-run it in a second context.
        // P39 MINOR-1b (card t_a697bc1a): empty targetName selects the
        // NAMELESS tree key (anchorless CORE23 shape), a named target keeps
        // the named key - the exact seam of RenderBlocks.
        static List<string> DutyPopupRows(string treeVal, string treeNamelessVal,
                                          string quotaVal, string targetName,
                                          string explain, bool escort)
        {
            var outp = new List<string>();
            if (!escort) return outp;   // combat popups carry no duty rows
            var dt = string.IsNullOrEmpty(targetName)
                ? TFMirror(treeNamelessVal)
                : TFMirror(treeVal, targetName);
            if (!string.IsNullOrEmpty(dt) && dt.IndexOf('{') < 0)
                foreach (var dl in dt.Replace("\r", "").Split('\n'))
                    if (!string.IsNullOrEmpty(dl.Trim())) outp.Add(dl);
            if (!string.IsNullOrEmpty(explain))
            {
                var dq = TFMirror(quotaVal, explain);
                if (!string.IsNullOrEmpty(dq) && dq.IndexOf('{') < 0) outp.Add(dq);
            }
            return outp;
        }

        static string TFMirror(string fmt, params object[] args)
        {
            if (string.IsNullOrEmpty(fmt)) return "";
            try { return args.Length > 0 ? string.Format(fmt, args) : fmt; }
            catch (System.FormatException) { return fmt; }
        }
    }
}
