// P37c (card t_e80246ed) section: the Escort-Quota core law — the duty split
// of the imperial roster per round (concept docs/CONCEPT_missions_duty.md
// §1b G1-G4 + §9.2 + §8 W4). Every expectation is HAND-PINNED in
// duty_core.json (authored from the law via an independent Python parity
// model — the P37b duty_loader discipline, no recorder, no --record route).
// The escort/combat vectors walk the REAL production input seam: the shipped
// HOTH10 mission file through the P37b fail-closed DutyAnchors reader
// (same RepoFile seam + MissionEntityGuids truth as duty_loader.json), so
// the target name "Cell" is read, not hardcoded. The shadow cases Prime
// synthetic PatternWeight docs and RESTORE the shipped asset afterwards so
// no later section can observe synthetic state.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDutyCore()
        {
            var v = LoadJson("duty_core.json");

            // Pattern-weight table: the SHIPPED asset has no dutyModifiers
            // key => degraded path (base weights + quota ranking) — the law
            // the live game runs until hop B ships a data row.
            var pwPath = RepoFile("Resources", "Languages", "En", "tactical_pattern_weights.json");
            Check("pwAsset", true, pwPath != null);
            var shippedPw = Json.AsDict(Json.Parse(File.ReadAllText(pwPath)));
            PatternWeights.PrimeDoc(shippedPw);
            var tShipped = PatternWeights.Load();
            Check("pw.shipped_no_dutymods", 0L, (long)tShipped.DutyMods.Count);

            // ---------------- escort/combat vectors on the REAL HOTH10 fact
            var h = Json.AsDict(v["hoth10"]);
            var artPath = RepoFile("Resources", "Languages", "En", "tactical_mission_critical.json");
            Check("artifact.exists", true, artPath != null);
            var art = Json.Parse(File.ReadAllText(artPath));
            var missionFile = Json.AsStr(h["missionFile"]);
            var missionPath = RepoFile("Resources", "SagaMissions", Json.AsStr(h["dir"]), missionFile);
            Check("hoth10.mission", true, missionPath != null);
            var duties = DutyAnchors.ForMission(art, missionFile, MissionEntityGuids(missionPath));
            Check("hoth10.duties", 1L, (long)duties.Count);
            // Anchor target name (bridge law mirrored): first TargetField name
            // in file order, else first MarkerToken name. A NAME, never a
            // coordinate — the non-walkable HOTH10 "Cell" Highlight is VALID.
            var target = duties.SelectMany(d => d.TargetFields).Select(t => t.Name).FirstOrDefault()
                         ?? duties.SelectMany(d => d.MarkerTokens).Select(t => t.Name).FirstOrDefault()
                         ?? "";
            Check("hoth10.target", Json.AsStr(h["targetName"]), target);

            var sk = Json.AsStr(h["sessionKey"]);
            var groups = DutyGroups(Json.AsList(h["groups"]));
            var firstPin = Json.AsDict(Json.AsList(h["rounds"])[0]);
            var firstExpect = Json.AsDict(firstPin["expect"]);
            // Two full passes over all pinned rounds: 3-Runden-Seed-Stabilitaet
            // (pass 2 must equal the SAME pins — determinism by construction).
            for (int pass = 0; pass < 2; pass++)
                foreach (var raw in Json.AsList(h["rounds"]))
                {
                    var c = Json.AsDict(raw);
                    var e = Json.AsDict(c["expect"]);
                    var r = DutyQuota.Compute(sk, (int)Json.AsLong(e["RoundNo"]), groups,
                                              tShipped, target, true);
                    var tag = "hoth10.r" + e["RoundNo"] + (pass == 1 ? ".rerun" : "");
                    Check(tag + ".round", e, DutyDict(r));
                    Check(tag + ".combat", Names(c["combat"]),
                          r.Combat().Select(x => (object)x).ToList());
                    Check(tag + ".dutyOf", Json.AsStr(c["dutyOf"]), DutySig(r, groups));
                }

            // -------------------------------------------------- k bounds: n=1
            var og = Json.AsDict(v["one_group"]);
            var ogR = DutyQuota.Compute(Json.AsStr(og["sessionKey"]), (int)Json.AsLong(og["round"]),
                                        DutyGroups(Json.AsList(og["groups"])), tShipped,
                                        Json.AsStr(Json.Get(Json.AsDict(og["expect"]), "TargetName", "")), true);
            Check("one_group.k1", Json.AsDict(og["expect"]), DutyDict(ogR));
            Check("one_group.dutyOf", Json.AsStr(og["dutyOf"]),
                  DutySig(ogR, DutyGroups(Json.AsList(og["groups"]))));

            // ------------------------------------------- k bounds: n=6 x 12r
            var sg = Json.AsDict(v["six_groups"]);
            var sgGroups = DutyGroups(Json.AsList(sg["groups"]));
            int sixRounds = 0;
            foreach (var raw in Json.AsList(sg["rounds"]))
            {
                var c = Json.AsDict(raw);
                var e = Json.AsDict(c["expect"]);
                var r = DutyQuota.Compute(Json.AsStr(sg["sessionKey"]), (int)Json.AsLong(e["RoundNo"]),
                                          sgGroups, tShipped,
                                          Json.AsStr(Json.Get(e, "TargetName", "")), true);
                sixRounds++;
                Check("six.r" + e["RoundNo"] + ".round", e, DutyDict(r));
                Check("six.r" + e["RoundNo"] + ".dutyOf", Json.AsStr(c["dutyOf"]), DutySig(r, sgGroups));
                Check("six.r" + e["RoundNo"] + ".band", true, r.K >= 1 && r.K <= 3 && r.K < 6);
            }
            Check("six.rounds_pinned", 12L, (long)sixRounds);

            // --------------------------------- shadow cases (dutyModifiers)
            var sh = Json.AsDict(v["shadow"]);
            var shGroups = DutyGroups(Json.AsList(sh["groups"]));
            try
            {
                foreach (var key in new[] { "matrix_positive", "matrix_nonpositive" })
                {
                    var block = Json.AsDict(sh[key]);
                    var synth = new Dictionary<string, object>(shippedPw)
                    {
                        ["dutyModifiers"] = block["dutyModifiers"],
                    };
                    PatternWeights.PrimeDoc(synth);
                    var tSynth = PatternWeights.Load();
                    foreach (var raw in Json.AsList(block["rounds"]))
                    {
                        var c = Json.AsDict(raw);
                        var e = Json.AsDict(c["expect"]);
                        var r = DutyQuota.Compute(Json.AsStr(sh["sessionKey"]),
                                                  (int)Json.AsLong(e["RoundNo"]), shGroups, tSynth,
                                                  Json.AsStr(Json.Get(e, "TargetName", "")), true);
                        var tag = "shadow." + key + ".r" + e["RoundNo"];
                        Check(tag + ".round", e, DutyDict(r));
                        Check(tag + ".dutyOf", Json.AsStr(c["dutyOf"]), DutySig(r, shGroups));
                        // The immobile Stativ never escorts, in every round,
                        // in both matrix polarities (printed movement law).
                        Check(tag + ".tripod_never", "none", r.DutyOf("DG007"));
                    }
                }
            }
            finally
            {
                PatternWeights.PrimeDoc(shippedPw);   // restore: no synthetic state leaks
                PatternWeights.Load();
            }

            // ------------------------------------------- Null-Law (no duty)
            var ndc = Json.AsDict(v["no_duty"]);
            var ndGroups = DutyGroups(Json.AsList(ndc["groups"]));
            var nd = DutyQuota.Compute(Json.AsStr(ndc["sessionKey"]), (int)Json.AsLong(ndc["round"]),
                                       ndGroups, tShipped,
                                       Json.AsStr(Json.Get(Json.AsDict(ndc["expect"]), "TargetName", "")), false);
            Check("no_duty.round", Json.AsDict(ndc["expect"]), DutyDict(nd));
            Check("no_duty.dutyOf", Json.AsStr(ndc["dutyOf"]), DutySig(nd, ndGroups));
            Check("no_duty.combat", Names(ndc["combat"]), nd.Combat().Select(x => (object)x).ToList());

            // ---------------------------------- Objectives/Refs feed (§8 W4)
            var rf = Json.AsDict(v["refs_feed"]);
            var anchors = Json.AsList(rf["anchors"]).Select(x => Json.AsStr(x)).ToList();
            var views = new Dictionary<string, AppView>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(rf["views"]))
            {
                var vd = Json.AsDict(raw);
                views[Json.AsStr(vd["name"])] = RefsFeedView(vd);
            }
            foreach (var raw in Json.AsList(rf["expect"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var view = views[name];
                var base_ = Doctrine.SelectDoctrine(view, "aggressive");
                var fed = Doctrine.SelectDoctrine(view, "aggressive", null, anchors);
                Check("refs." + name + ".baseline", Names(c["baseline_refs"]),
                      base_.Refs.Select(x => (object)x).ToList());
                Check("refs." + name + ".fed", Names(c["fed_refs"]),
                      fed.Refs.Select(x => (object)x).ToList());
                // W4 "einspeisen, nicht ersetzen": the CARD TEXT (where the
                // {schutz}/protect placeholders render) is byte-identical —
                // the anchors ride the Refs chain, nothing else moves.
                Check("refs." + name + ".cardtext_identical", base_.CardText, fed.CardText);
                if (c.ContainsKey("rules"))
                    Check("refs." + name + ".rules", Names(c["rules"]),
                          fed.Rules.Select(x => (object)x).ToList());
            }
            var dp = Json.AsDict(rf["dedup_pin"]);
            var dpFed = Doctrine.SelectDoctrine(views[Json.AsStr(dp["name"])], "aggressive", null,
                Json.AsList(dp["anchors"]).Select(x => Json.AsStr(x)).ToList());
            Check("refs.dedup", Names(dp["fed_refs"]), dpFed.Refs.Select(x => (object)x).ToList());

            // --------------------------------------------- Meta-Law (pure fn)
            // Same inputs, freshly built argument objects: equal outputs —
            // the tactical-ON decision is reproducible, and the ONLY gate to
            // it is ActiveNow() at the call site (tactical OFF never calls).
            var againA = DutyQuota.Compute(sk, 1, DutyGroups(Json.AsList(h["groups"])), tShipped, target, true);
            var againB = DutyQuota.Compute(sk, 1, DutyGroups(Json.AsList(h["groups"])), tShipped, target, true);
            Check("meta_law.pure_repeat", DutyDict(againA), DutyDict(againB));
            Check("meta_law.equals_pin", (object)firstExpect, DutyDict(againA));
        }

        // ---- helpers (pure fixture plumbing, no law lives here) ----------

        static List<DutyGroup> DutyGroups(List<object> rows)
        {
            var outp = new List<DutyGroup>();
            foreach (var raw in rows)
            {
                var d = Json.AsDict(raw);
                outp.Add(new DutyGroup
                {
                    CardId = Json.AsStr(d["cardId"]),
                    Pattern = Json.AsStr(Json.Get(d, "pattern", "")),
                    Mobile = Json.AsBool(Json.Get(d, "mobile", true)),
                });
            }
            return outp;
        }

        static Dictionary<string, object> DutyDict(DutyRound r)
        {
            return new Dictionary<string, object>
            {
                { "RoundNo", (long)r.RoundNo },
                { "Cap", (long)r.Cap },
                { "K", (long)r.K },
                { "Mode", r.Mode },
                { "Ranking", r.Ranking.Select(x => (object)x).ToList() },
                { "Escorts", r.Escorts.Select(x => (object)x).ToList() },
                { "TargetName", r.TargetName },
                { "Explain", r.Explain },
            };
        }

        // Core-Marker disclosure: "id=escort|none" per group, Ordinal by id.
        static string DutySig(DutyRound r, IReadOnlyList<DutyGroup> groups)
        {
            return string.Join(",", groups.Select(g => g.CardId)
                .Distinct().OrderBy(x => x, StringComparer.Ordinal)
                .Select(id => id + "=" + r.DutyOf(id)));
        }

        static List<object> Names(object raw) =>
            Json.AsList(raw).Select(x => (object)Json.AsStr(x)).ToList();

        static AppView RefsFeedView(Dictionary<string, object> vd)
        {
            var objectives = new List<Dictionary<string, object>>();
            foreach (var raw in Json.AsList(Json.Get(vd, "objectives", new List<object>())))
            {
                var o = Json.AsDict(raw);
                objectives.Add(new Dictionary<string, object>
                {
                    { "id", Json.AsStr(o["id"]) },
                    { "zone", Json.AsStr(Json.Get(o, "zone", "")) },
                    { "done", Json.AsBool(Json.Get(o, "done", false)) },
                });
            }
            Dictionary<string, Dictionary<string, object>> contest = null;
            var cr = Json.Get(vd, "contest", null);
            if (cr != null)
            {
                var cd = Json.AsDict(cr);
                contest = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal)
                {
                    [Json.AsStr(vd["posture"])] = new Dictionary<string, object>
                    {
                        { "zone", Json.AsStr(Json.Get(cd, "zone", "")) },
                        { "door", Json.AsStr(Json.Get(cd, "door", "")) },
                        { "path", new List<string>() },
                        { "targets", Json.AsList(Json.Get(cd, "targets", new List<object>()))
                            .Select(x => Json.AsStr(x)).ToList() },
                    },
                };
            }
            return new AppView
            {
                RoundNo = 1,
                Posture = Json.AsStr(vd["posture"]),
                PostureReason = "harness",
                Counter = new Dictionary<string, object>
                {
                    { "victoryCounter", Json.AsStr(vd["counter"]) },
                    { "campaignMetaBonus", false },
                },
                Objectives = objectives,
                Contest = contest,
            };
        }
    }
}
