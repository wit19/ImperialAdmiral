// P37b (card t_1743a062) section: the duty-loader trio - objectiveCounter
// whitelist, DutyAnchors reader over the SHIPPED MissionCritical artifact
// (+ SHIPPED SagaMissions entity truth, same RepoFile seam as SecDpAnchors),
// and the PatternWeights dutyModifiers Null-Law. Hand-pinned route (like
// dp_anchors.json's rank cases): every expectation lives in
// duty_loader.json, authored from the artifact + DutyMineGate ground truth;
// Canon vectors untouched. The dutyModifiers cases Prime the Core cache with
// synthetic docs and RESTORE the shipped asset afterwards so no later
// section can observe synthetic state.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDutyLoader()
        {
            var v = LoadJson("duty_loader.json");

            // ------------------------------------------- counter whitelist
            foreach (var raw in Json.AsList(v["counter_accept"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var counter = Json.AsStr(c["counter"]);
                bool accepts = Json.AsBool(c["accepts"]);
                bool threw = false;
                try { var vc = new VictoryCounter(counter); Check("counter[" + name + "].type", counter, vc.CounterType); }
                catch (ArgumentException) { threw = true; }
                Check("counter[" + name + "].accepts", accepts, !threw);
            }

            // --------------------------------- board victoryCounter gate
            foreach (var raw in Json.AsList(v["board_counter"]))
            {
                var c = Json.AsDict(raw);
                var counter = Json.AsStr(c["counter"]);
                bool accepts = Json.AsBool(c["accepts"]);
                var doc = new Dictionary<string, object>(Json.AsDict(v["board_doc"]))
                {
                    ["victoryCounter"] = counter,
                };
                bool threw = false;
                try
                {
                    var b = Board.FromJson(doc);
                    Check("board_counter[" + counter + "].name", counter, b.VictoryCounterName);
                }
                catch (ArgumentException) { threw = true; }
                Check("board_counter[" + counter + "].accepts", accepts, !threw);
            }

            // -------------------------------- eliminate posture rule lines
            // Minimal AppView (doctrine layer sees AppView only - the same
            // boundary SelectDoctrine asserts). Rules are pattern- and
            // view-content-independent for the eliminate branch.
            foreach (var raw in Json.AsList(v["eliminate_rules"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var view = new AppView
                {
                    RoundNo = 1,
                    Posture = "eliminate",
                    PostureReason = "harness",
                    Counter = new Dictionary<string, object>
                    {
                        { "victoryCounter", Json.AsStr(c["counter"]) },
                        { "campaignMetaBonus", false },
                    },
                    Objectives = new List<Dictionary<string, object>>(),
                };
                var d = Doctrine.SelectDoctrine(view, "aggressive");
                Check("eliminate_rules[" + name + "].rules",
                      Json.AsList(c["rules"]).Select(x => (object)Json.AsStr(x)).ToList(),
                      d.Rules.Select(x => (object)x).ToList());
                Check("eliminate_rules[" + name + "].counter",
                      Json.AsStr(c["counter"]), d.Counter);
            }

            // ----------------------- duty reader over the SHIPPED artifact
            var artPath = RepoFile("Resources", "Languages", "En", "tactical_mission_critical.json");
            Check("artifact.exists", true, artPath != null);
            var art = Json.Parse(File.ReadAllText(artPath));
            long sumAnchors = 0;
            foreach (var raw in Json.AsList(v["duty_shipped"]))
            {
                var c = Json.AsDict(raw);
                var missionFile = Json.AsStr(c["missionFile"]);
                var missionPath = RepoFile("Resources", "SagaMissions", Json.AsStr(c["dir"]), missionFile);
                Check("duty_shipped[" + missionFile + "].missionFile", true, missionPath != null);
                var known = MissionEntityGuids(missionPath);
                var duties = DutyAnchors.ForMission(art, missionFile, known);
                Check("duty_shipped[" + missionFile + "].duties", Json.AsLong(c["duties"]), (long)duties.Count);
                Check("duty_shipped[" + missionFile + "].anchors", Json.AsLong(c["anchors"]),
                      (long)duties.Sum(d => d.AnchorCount));
                sumAnchors += duties.Sum(d => d.AnchorCount);
                // P39 MINOR-4 (card t_a697bc1a): the three law fields are read
                // by NAMED checks EVEN when the reader yields no duty row.
                // Before P39 they were gated behind duties.Count > 0, so a
                // missing duty line silently dropped three checks from the
                // harness total (46591->46588 in the QA mutation probe - an
                // alarm that never named itself). Now: a missing duty line
                // fails FOUR named checks and the check total is data-
                // invariant; the sum pin below can no longer mask a delta.
                var d0 = duties.Count > 0 ? duties[0] : null;
                Check("duty_shipped[" + missionFile + "].variable", Json.AsStr(c["variable"]),
                      d0 != null ? d0.Variable : "<no duty row read>");
                Check("duty_shipped[" + missionFile + "].threshold", Json.AsLong(c["threshold"]),
                      d0 != null ? d0.Threshold : long.MinValue);
                Check("duty_shipped[" + missionFile + "].law", Json.AsStr(c["law"]),
                      d0 != null ? d0.Law : "<no duty row read>");
                if (c.ContainsKey("tfNames"))
                    Check("duty_shipped[" + missionFile + "].tfNames",
                          Json.AsList(c["tfNames"]).Select(x => (object)Json.AsStr(x)).ToList(),
                          duties.SelectMany(d => d.TargetFields).Select(t => (object)t.Name).ToList());
                if (c.ContainsKey("mtNames"))
                    Check("duty_shipped[" + missionFile + "].mtNames",
                          Json.AsList(c["mtNames"]).Select(x => (object)Json.AsStr(x)).ToList(),
                          duties.SelectMany(d => d.MarkerTokens).Select(t => (object)t.Name).ToList());
                if (c.ContainsKey("detail"))
                    Check("duty_shipped[" + missionFile + "].detail",
                          Json.AsList(c["detail"]).Select(x => (object)DutyDetail(Json.AsDict(x))).ToList(),
                          duties.Select(d => (object)DutyDetail(d)).ToList());
            }
            Check("duty_shipped.sum_anchors", Json.AsLong(v["duty_shipped_sum_anchors"]), sumAnchors);

            foreach (var raw in Json.AsList(v["duty_no_duty_missions"]))
            {
                var c = Json.AsDict(raw);
                var missionFile = Json.AsStr(c["missionFile"]);
                var missionPath = RepoFile("Resources", "SagaMissions", Json.AsStr(c["dir"]), missionFile);
                Check("duty_none[" + missionFile + "].missionFile", true, missionPath != null);
                var duties = DutyAnchors.ForMission(art, missionFile, MissionEntityGuids(missionPath));
                Check("duty_none[" + missionFile + "].duties", 0L, (long)duties.Count);
            }

            // ------------------------------- fail-closed drops (synthetic)
            foreach (var raw in Json.AsList(v["duty_synth_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var missionFile = Json.AsStr(Json.Get(c, "missionFile", "SYNTH.json"));
                ICollection<string> known = null;
                object knownRaw;
                if (c.TryGetValue("known", out knownRaw) && knownRaw != null)
                    known = new HashSet<string>(Json.AsList(knownRaw).Select(x => Json.AsStr(x)), StringComparer.Ordinal);
                var duties = DutyAnchors.ForMission(Json.AsDict(c["doc"]), missionFile, known);
                Check("synth[" + name + "].duties",
                      Json.AsList(c["duties"]).Select(x => (object)DutySummary(Json.AsDict(x))).ToList(),
                      duties.Select(d => (object)DutySummary(d)).ToList());
            }

            // ----------------------- dutyModifiers Null-Law (pattern dice)
            var mc = Json.AsDict(v["modifier_cases"]);
            var pins = Json.AsDict(mc["pins"]);
            var pwPath = RepoFile("Resources", "Languages", "En", "tactical_pattern_weights.json");
            Check("modifier.pwAsset", true, pwPath != null);
            var shippedPw = Json.AsDict(Json.Parse(File.ReadAllText(pwPath)));
            try
            {
                // shipped asset: no key, empty DutyMods (Null-Law baseline).
                PatternWeights.PrimeDoc(shippedPw);
                var tShipped = PatternWeights.Load();
                Check("modifier.shipped_key_absent", Json.AsBool(mc["shipped_key_absent"]),
                      !shippedPw.ContainsKey("dutyModifiers"));
                Check("modifier.shipped_dutymods_empty", 0L, (long)tShipped.DutyMods.Count);

                foreach (var keyname in new[] { "baseline", "empty", "present" })
                {
                    var doc = Json.AsDict(mc[keyname + "_doc"]);
                    var pin = Json.AsDict(pins[keyname]);
                    PatternWeights.PrimeDoc(doc);
                    var t = PatternWeights.Load();
                    Check("modifier[" + keyname + "].dutymods",
                          Json.AsList(pin["dutymods"]).Select(x => (object)Json.AsStr(x)).ToList(),
                          ModsSig(t.DutyMods));
                    Check("modifier[" + keyname + "].posture_sig", Json.AsStr(pin["posture_sig"]),
                          string.Join(";", ModsSig(t.PostureMods)));
                    Check("modifier[" + keyname + "].rows", Json.AsLong(pin["rows"]), (long)t.Rows.Count);
                }
            }
            finally
            {
                PatternWeights.PrimeDoc(shippedPw);   // never leak synthetic prime
            }
        }

        // Entity truth of a shipped mission file: mapEntities GUIDs (same
        // ground truth DutyMineGate resolves its data-exists law against).
        static HashSet<string> MissionEntityGuids(string missionPath)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var md = Json.AsDict(Json.Parse(File.ReadAllText(missionPath)));
            foreach (var e in Json.AsList(Json.Get(md, "mapEntities", new List<object>())))
            {
                var ed = e as Dictionary<string, object>;
                if (ed == null) continue;
                var g = Json.AsStr(Json.Get(ed, "GUID", ""));
                if (!string.IsNullOrEmpty(g)) set.Add(g);
            }
            return set;
        }

        // Reader-side summary shape (counts + names, roles only by name).
        static Dictionary<string, object> DutySummary(DutyAnchor d)
        {
            return new Dictionary<string, object>
            {
                { "variable", d.Variable },
                { "threshold", d.Threshold },
                { "law", d.Law },
                { "tfCount", (long)d.TargetFields.Count },
                { "mtCount", (long)d.MarkerTokens.Count },
                { "tfNames", d.TargetFields.Select(t => (object)t.Name).ToList() },
                { "mtNames", d.MarkerTokens.Select(t => (object)t.Name).ToList() },
            };
        }

        // Fixture-side summary normalizer (missing names lists read empty).
        static Dictionary<string, object> DutySummary(Dictionary<string, object> d)
        {
            return new Dictionary<string, object>
            {
                { "variable", Json.AsStr(Json.Get(d, "variable", "")) },
                { "threshold", Json.AsLong(Json.Get(d, "threshold", 0L)) },
                { "law", Json.AsStr(Json.Get(d, "law", "")) },
                { "tfCount", Json.AsLong(Json.Get(d, "tfCount", 0L)) },
                { "mtCount", Json.AsLong(Json.Get(d, "mtCount", 0L)) },
                { "tfNames", Json.AsList(Json.Get(d, "tfNames", new List<object>())).Select(x => (object)Json.AsStr(x)).ToList() },
                { "mtNames", Json.AsList(Json.Get(d, "mtNames", new List<object>())).Select(x => (object)Json.AsStr(x)).ToList() },
            };
        }

        // Reader-side detail shape (full anchors, GUID identity included -
        // Loch-Gesetz: GUID + name + type, never a coordinate).
        static Dictionary<string, object> DutyDetail(DutyAnchor d)
        {
            return new Dictionary<string, object>
            {
                { "variable", d.Variable },
                { "threshold", d.Threshold },
                { "law", d.Law },
                { "targetFields", d.TargetFields.Select(t => (object)new Dictionary<string, object>
                    { { "entity", t.Entity }, { "name", t.Name }, { "entityType", t.EntityType } }).ToList() },
                { "markerTokens", d.MarkerTokens.Select(t => (object)new Dictionary<string, object>
                    { { "entity", t.Entity }, { "name", t.Name }, { "entityType", t.EntityType } }).ToList() },
            };
        }

        // Fixture-side detail normalizer (same keys, same defaults).
        static Dictionary<string, object> DutyDetail(Dictionary<string, object> d)
        {
            return new Dictionary<string, object>
            {
                { "variable", Json.AsStr(Json.Get(d, "variable", "")) },
                { "threshold", Json.AsLong(Json.Get(d, "threshold", 0L)) },
                { "law", Json.AsStr(Json.Get(d, "law", "")) },
                { "targetFields", Json.AsList(Json.Get(d, "targetFields", new List<object>())).Select(x =>
                    {
                        var td = Json.AsDict(x);
                        return (object)new Dictionary<string, object>
                        {
                            { "entity", Json.AsStr(Json.Get(td, "entity", "")) },
                            { "name", Json.AsStr(Json.Get(td, "name", "")) },
                            { "entityType", Json.AsStr(Json.Get(td, "entityType", "")) },
                        };
                    }).ToList() },
                { "markerTokens", Json.AsList(Json.Get(d, "markerTokens", new List<object>())).Select(x =>
                    {
                        var td = Json.AsDict(x);
                        return (object)new Dictionary<string, object>
                        {
                            { "entity", Json.AsStr(Json.Get(td, "entity", "")) },
                            { "name", Json.AsStr(Json.Get(td, "name", "")) },
                            { "entityType", Json.AsStr(Json.Get(td, "entityType", "")) },
                        };
                    }).ToList() },
            };
        }

        // "key|k1=v1,k2=v2" per entry, Ordinal-sorted (deterministic sig).
        static List<string> ModsSig(Dictionary<string, Dictionary<string, int>> mods)
        {
            var outSigs = new List<string>();
            foreach (var k in mods.Keys.OrderBy(x => x, StringComparer.Ordinal))
                outSigs.Add(k + "|" + string.Join(",", mods[k].OrderBy(e => e.Key, StringComparer.Ordinal)
                    .Select(e => e.Key + "=" + e.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            return outSigs;
        }
    }
}
