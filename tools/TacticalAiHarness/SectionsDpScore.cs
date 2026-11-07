// SectionsDpScore.cs - P33-H4 (Thomas 08.11, CONCEPT_drohungsbudget Sec.2
// Stufe 3 + Sec.3, findings_Player-33 §3): the DP-Score vectors.
//
// The law under test (Core DeploymentAnchors.ChooseScored): the ranked DP
// choice for (Karte, DP) runs a stage ladder - ANKER-TIER (mined live anchor
// or DYNAMIC section anchor, DeployAnchor.HasPosition=false) -> intra-section
// anchor distance -> BATTLE-ZONE graph hop distance (claim beats no claim;
// Loch-Gesetz: unmeasured = no claim, never "far") -> FRIEND LEASH as a
// Vollrecht (after all spatial stages, before diversity) -> DP-DIVERSITAET
// (unused beats used, only inside a tie) -> the LEGACY seed chain,
// bit-identical. Determinism: no RNG, sequential challenge in caller order.
//
// VECTOR DISCIPLINE (tacai-harness-fixtures law): RELATIONS are hand-pilled
// (tier primacy, claim-over-no-claim, leash-over-fresh-over-seed, the
// Null-Law equality against the legacy OrderBy line); absolute winners where
// the SEED CHAIN decides (green-vs-green ties) come from --record and are
// reviewed once, then merged into the fixture's expect blocks - never
// hand-recomputed. The Null-Law block pins that with NO fact at all the
// scored path lands on the same winner as the legacy XOR line.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDpScore()
        {
            var v = LoadJson("dp_score.json");
            var recorded = new Dictionary<string, object>();

            // ---------------- score_cases: the stage ladder ----------------
            foreach (var craw in (List<object>)v["score_cases"])
            {
                var c = Json.AsDict(craw);
                var name = Json.AsStr(c["name"]);
                var path = "score|" + name;
                ulong seed = (ulong)Json.AsLong(c["seed"]);
                int round = (int)Json.AsLong(c["round"]);

                // graph: sections + confirmed edges (hop distance is what
                // stage 3 measures; doors would only add annotations).
                SectionGraph graph = null;
                if (c.ContainsKey("sections"))
                {
                    graph = new SectionGraph();
                    foreach (var s in Json.AsList(c["sections"]))
                        graph.AddSection(Json.AsStr(s));
                    if (c.ContainsKey("edges"))
                        foreach (var eraw in Json.AsList(c["edges"]))
                        {
                            var e = Json.AsList(eraw);
                            graph.ConfirmEdge(Json.AsStr(e[0]), Json.AsStr(e[1]));
                        }
                }

                var anchors = new List<DeployAnchor>();
                if (c.ContainsKey("anchors"))
                    foreach (var araw in Json.AsList(c["anchors"]))
                    {
                        var a = Json.AsDict(araw);
                        anchors.Add(new DeployAnchor
                        {
                            EntityGuid = Json.AsStr(a["guid"]),
                            SectionGuid = Json.AsStr(a["section"]),
                            X = Json.AsDouble(a["x"]),
                            Y = Json.AsDouble(a["y"]),
                            HasPosition = !a.ContainsKey("hasPosition") || Json.AsBool(a["hasPosition"]),
                        });
                    }

                var cands = new List<DeployCandidate>();
                foreach (var cdraw in Json.AsList(c["cands"]))
                {
                    var cd = Json.AsDict(cdraw);
                    cands.Add(new DeployCandidate
                    {
                        Guid = Json.AsStr(cd["guid"]),
                        Name = Json.AsStr(cd["name"]),
                        SectionGuid = Json.AsStr(cd["section"]),
                        X = Json.AsDouble(cd["x"]),
                        Y = Json.AsDouble(cd["y"]),
                    });
                }

                List<string> StrList(string key)
                {
                    if (!c.ContainsKey(key) || c[key] == null) return null;
                    var l = new List<string>();
                    foreach (var x in Json.AsList(c[key])) l.Add(Json.AsStr(x));
                    return l;
                }
                var zones = StrList("zones");
                var own = StrList("own");
                var used = StrList("used");

                List<DpRow> rows;
                var pick = DeploymentAnchors.ChooseScored(cands, anchors, graph, zones,
                                                          seed, round, own, used, out rows);
                var rowDumps = rows == null
                    ? new List<string>()
                    : rows.Select(r => r.Dump()).ToList();

                var rec = new Dictionary<string, object>
                {
                    { "winner", pick == null ? "" : pick.Candidate.Guid },
                    { "anchor", pick == null || pick.Anchor == null ? "" : pick.Anchor.EntityGuid },
                    { "dyn", pick == null || pick.Anchor == null ? false : !pick.Anchor.HasPosition },
                    { "rows", rowDumps.Cast<object>().ToList() },
                };
                recorded[path] = rec;

                if (!c.ContainsKey("expect")) continue;   // record-only case
                var eh = Json.AsDict(c["expect"]);

                // determinism: the same inputs must yield the same answer.
                List<DpRow> rows2;
                var pick2 = DeploymentAnchors.ChooseScored(cands, anchors, graph, zones,
                                                           seed, round, own, used, out rows2);
                Check(path + "|determinism",
                      pick == null ? "" : pick.Candidate.Guid,
                      pick2 == null ? "" : pick2.Candidate.Guid);

                if (eh.ContainsKey("winner"))
                    Check(path + "|winner",
                          pick == null ? "" : pick.Candidate.Guid, Json.AsStr(eh["winner"]));
                if (eh.ContainsKey("winner_in"))
                {
                    var set = Json.AsList(eh["winner_in"]).Select(Json.AsStr).ToList();
                    Check(path + "|winner_in", true,
                          pick != null && set.Contains(pick.Candidate.Guid));
                }
                if (eh.ContainsKey("winner_not"))
                    Check(path + "|winner_not", true,
                          pick != null && Json.AsStr(eh["winner_not"]) != pick.Candidate.Guid);
                if (eh.ContainsKey("anchor"))
                    Check(path + "|anchor",
                          pick == null || pick.Anchor == null ? "" : pick.Anchor.EntityGuid,
                          Json.AsStr(eh["anchor"]));
                if (eh.ContainsKey("dyn_anchor"))
                    Check(path + "|dyn_anchor", Json.AsBool(eh["dyn_anchor"]),
                          pick != null && pick.Anchor != null && !pick.Anchor.HasPosition);
                if (eh.ContainsKey("rows"))
                {
                    var want = (List<object>)eh["rows"];
                    Check(path + "|rows.count", (long)want.Count, (long)rowDumps.Count);
                    var sb = new StringBuilder();
                    for (int i = 0; i < want.Count && i < rowDumps.Count; i++)
                        if (Json.AsStr(want[i]) != rowDumps[i])
                        {
                            if (sb.Length > 0) sb.Append(" ;; ");
                            sb.Append("DIFF[" + i + "] want=" + Json.AsStr(want[i]) +
                                      " got=" + rowDumps[i]);
                        }
                    if (sb.Length == 0) sb.Append("ok");
                    Check(path + "|rows", "ok", sb.ToString());
                }

                // P34-C Vergleichszeilen-Vektoren (Thomas, card t_1787135b):
                // winnerScore vs the STRONGEST alternative plus the stage
                // that separated them - the same API the log line prints,
                // pinned by relation (never recorded arithmetic).
                if (eh.ContainsKey("cmp"))
                {
                    var cmpE = Json.AsDict(eh["cmp"]);
                    var alt = DeploymentAnchors.StrongestAlternative(
                        rows, pick == null ? null : pick.Score);
                    if (cmpE.ContainsKey("alt"))
                        Check(path + "|cmp.alt",
                              alt == null ? "" : alt.Candidate.Guid,
                              Json.AsStr(cmpE["alt"]));
                    if (cmpE.ContainsKey("alt_not_winner"))
                        Check(path + "|cmp.alt_not_winner", true,
                              pick != null && alt != null &&
                              alt.Candidate.Guid != pick.Candidate.Guid);
                    if (cmpE.ContainsKey("stage"))
                        Check(path + "|cmp.stage",
                              alt == null || pick == null ? ""
                                  : DeploymentAnchors.DecideStage(pick.Score, alt),
                              Json.AsStr(cmpE["stage"]));
                    if (cmpE.ContainsKey("winner_pairwise"))
                        Check(path + "|cmp.winner_pairwise", Json.AsBool(cmpE["winner_pairwise"]),
                              pick != null && alt != null &&
                              DeploymentAnchors.DecideScored(pick.Score, alt) < 0);
                }
            }

            // ---------------- null_law: no fact, no tactical claim ----------
            // The scored path WITHOUT any fact must land exactly on the
            // legacy XOR OrderBy line the bridge keeps verbatim (bit-compat
            // acceptance criterion), and the legacy Choose wrapper must agree
            // with the scored call on the pre-P33 input domain.
            foreach (var craw in (List<object>)v["null_law"])
            {
                var c = Json.AsDict(craw);
                var name = Json.AsStr(c["name"]);
                var path = "null_law|" + name;
                ulong seed = (ulong)Json.AsLong(c["seed"]);
                int round = (int)Json.AsLong(c["round"]);

                var cands = new List<DeployCandidate>();
                foreach (var cdraw in Json.AsList(c["cands"]))
                {
                    var cd = Json.AsDict(cdraw);
                    cands.Add(new DeployCandidate
                    {
                        Guid = Json.AsStr(cd["guid"]),
                        Name = Json.AsStr(cd["name"]),
                        SectionGuid = Json.AsStr(cd["section"]),
                        X = Json.AsDouble(cd["x"]),
                        Y = Json.AsDouble(cd["y"]),
                    });
                }

                // the bridge's legacy line, verbatim law over the candidates
                var legacy = cands
                    .OrderBy(x => DeploymentAnchors.OrdinalOf(x.Name ?? "") ^
                                  (seed ^ (ulong)Math.Max(1, round) * 0x2545F4914F6CDD1DUL))
                    .ThenBy(x => x.Name ?? "", StringComparer.Ordinal)
                    .ThenBy(x => x.Guid ?? "", StringComparer.Ordinal)
                    .First();

                List<DpRow> rows;
                var pick = DeploymentAnchors.ChooseScored(cands, new DeployAnchor[0],
                                                          null, null, seed, round,
                                                          null, null, out rows);
                Check(path + "|xor_line_bit_identical", legacy.Guid, pick.Candidate.Guid);
                var wrap = DeploymentAnchors.Choose(cands, null, seed, round);
                Check(path + "|wrapper_equals_scored", pick.Candidate.Guid, wrap.Candidate.Guid);
                Check(path + "|rows_inert", true,
                      rows.All(r => r.Tier == 1 && !r.HasAnchor && !r.ZoneKnown &&
                                    !r.Own && r.Fresh));
            }

            if (Record)
            {
                var path = System.IO.Path.Combine(FIX, "dp_score_record.json");
                System.IO.File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
