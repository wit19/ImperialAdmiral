// P33-H2 (findings_Player-33 §1, Fix A, card t_db6814bb): the HERO START-ANCHOR
// vectors. hero_deploy_anchor.json replays the round-1 entry channel over the
// REAL feed path (WorldEventFeed.Append validates kind+payload fail-loud):
//   - reader guards: empty-hero and section-less HeroDeployed payloads fail
//     CLOSED inside Core (presence is not position),
//   - the deploy-fakt rule label (explain hero ... / dump hero ... suffix),
//     emitted ONLY on the deploy-anchored class so every pre-P33 dump stays
//     byte-identical (same emission law as the S-C transit lines),
//   - the HOTH12 reconstruction (p33_hoth12_four_starts): four start heroes,
//     one blue field -> four anchors, ring + contest + threat zone in ROUND 1
//     without a single hero activation (the round-1 blindness of Player-33),
//   - spread/collapse/retract of the entry SET, the fenster-fakt flip when a
//     window fact overwrites, and the freshness law (a stale start anchor is
//     never reused as a position).
// expect blocks are recorded once via --record and reviewed (record
// discipline); every expect_hand check is hand-pinned ground truth - the law
// IS the expectation. Canon accounting: this file is the only fixture it
// reads; the pre-existing deploy_ledger.json streams shift ONLY where the
// P33-H2 vector registration/label laws are live (reviewed re-pins, see the
// handoff), every other fixture byte-identical by the emission law above.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecHeroDeployAnchor()
        {
            var v = LoadJson("hero_deploy_anchor.json");
            var cases = Json.AsList(v["cases"]);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);

                // THE REAL feed path: Append validates kind + payload fail-loud.
                var feed = new WorldEventFeed("vector|" + name);
                foreach (var eraw in Json.AsList(c["events"]))
                {
                    var e = Json.AsDict(eraw);
                    var payload = e.ContainsKey("payload")
                        ? new Dictionary<string, object>(Json.AsDict(e["payload"]))
                        : new Dictionary<string, object>();
                    feed.Append(Json.AsStr(e["kind"]), (int)Json.AsLong(e["round"]), payload);
                }

                // deterministic replay law (both projections, same as every feed section)
                var view1 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view1.Canon(), view2.Canon());

                var space1 = SectionSpace.Project(feed.Events, null, ReadSeeds(c), ReadEdges(c));
                var space2 = SectionSpace.Project(feed.Events, null, ReadSeeds(c), ReadEdges(c));
                Check(name + ".replay_dump", space1.Dump(), space2.Dump());

                var crit = new List<string>();
                if (c.ContainsKey("critical") && c["critical"] != null)
                    foreach (var s in Json.AsList(c["critical"])) crit.Add(Json.AsStr(s));

                var explain = new List<string>(space1.ExplainTrail());
                var zbS3 = SectionConsumers.PlanZones(space1, view1, SkillProfile.For("S3"));
                var zbS3C = SectionConsumers.PlanZones(space1, view1, SkillProfile.For("S3"), crit);
                var zbS2 = SectionConsumers.PlanZones(space1, view1, SkillProfile.For("S2"), crit);

                var got = new Dictionary<string, object>
                {
                    { "dump", space1.Dump() },
                    { "canon", view1.Canon() },
                    { "explain", new List<object>(explain.ToArray()) },
                    { "zones_s3", zbS3 != null ? zbS3.Dump() : "NULL" },
                    { "zones_s3_crit", zbS3C != null ? zbS3C.Dump() : "NULL" },
                    { "zones_s2", zbS2 != null ? zbS2.Dump() : "NULL" },
                };

                if (Record)
                {
                    recorded[name] = got;
                    // An EXISTING expect block is still compared in record
                    // runs, so a regression can never hide behind --record.
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        foreach (var k in new[] { "dump", "canon", "explain",
                                                  "zones_s3", "zones_s3_crit", "zones_s2" })
                            Check(name + ".recorded." + k, exp0[k], got[k]);
                    }
                    continue;
                }

                if (c.ContainsKey("expect"))
                {
                    var exp0 = Json.AsDict(c["expect"]);
                    foreach (var k in new[] { "dump", "canon", "explain",
                                              "zones_s3", "zones_s3_crit", "zones_s2" })
                        Check(name + ".expect." + k, exp0[k], got[k]);
                }

                // ---- hand-pinned laws (never recorded): the law is the truth
                if (c.ContainsKey("expect_hand"))
                {
                    var eh = Json.AsDict(c["expect_hand"]);
                    Func<string, bool> has = k => eh.ContainsKey(k) && eh[k] != null;

                    if (has("heroes"))
                        foreach (var hraw in Json.AsList(eh["heroes"]))
                        {
                            var h = Json.AsDict(hraw);
                            var hn = Json.AsStr(h["name"]) ?? "";
                            var sec = space1.HeroInSection(hn) ?? "";
                            var expSec = Json.AsStr(h["section"]) ?? "";
                            Check(name + ".anchor[" + hn + "].section", expSec, sec);
                            int expRound = h.ContainsKey("round") ? (int)Json.AsLong(h["round"]) : -1;
                            int gotRound = sec.Length == 0 ? -1 : space1.HeroSectionRound(hn);
                            Check(name + ".anchor[" + hn + "].round", (long)expRound, (long)gotRound);
                            if (h.ContainsKey("rule") && h["rule"] != null)
                            {
                                bool found = false;
                                foreach (var l in explain)
                                    if (l.StartsWith("explain hero " + hn + " ", StringComparison.Ordinal)
                                        && l.EndsWith("rule=" + Json.AsStr(h["rule"]), StringComparison.Ordinal))
                                    { found = true; break; }
                                Check(name + ".anchor[" + hn + "].rule", true, found);
                            }
                        }

                    if (has("explain_must"))
                        foreach (var s in Json.AsList(eh["explain_must"]))
                        {
                            bool found = false;
                            foreach (var l in explain) if (l == Json.AsStr(s)) { found = true; break; }
                            Check(name + ".explain_must[" + Json.AsStr(s) + "]", true, found);
                        }
                    if (has("explain_must_not"))
                        foreach (var s in Json.AsList(eh["explain_must_not"]))
                        {
                            bool found = false;
                            foreach (var l in explain) if (l.Contains(Json.AsStr(s))) { found = true; break; }
                            Check(name + ".explain_must_not[" + Json.AsStr(s) + "]", false, found);
                        }
                    if (has("dump_must_not"))
                        foreach (var s in Json.AsList(eh["dump_must_not"]))
                            Check(name + ".dump_must_not[" + Json.AsStr(s) + "]", false,
                                  space1.Dump().Contains(Json.AsStr(s)));
                    if (has("canon_must"))
                        foreach (var s in Json.AsList(eh["canon_must"]))
                            Check(name + ".canon_must[" + Json.AsStr(s) + "]", true,
                                  view1.Canon().Contains(Json.AsStr(s)));
                    if (has("canon_must_not"))
                        foreach (var s in Json.AsList(eh["canon_must_not"]))
                            Check(name + ".canon_must_not[" + Json.AsStr(s) + "]", false,
                                  view1.Canon().Contains(Json.AsStr(s)));
                    if (has("nodes"))
                        foreach (var s in Json.AsList(eh["nodes"]))
                            Check(name + ".node[" + Json.AsStr(s) + "]", true,
                                  space1.Graph.HasSection(Json.AsStr(s)));

                    // ring/contest/threat law over the consumer rail
                    // (optional-key law: an absent key means NO CHECK, never
                    // an implicit empty expectation)
                    if (has("rings"))
                    {
                        var ringsExp = Json.AsList(eh["rings"]).ConvertAll(Json.AsStr);
                        var ringsGot = zbS3 != null ? zbS3.RingZones : new List<string>();
                        Check(name + ".rings.count", (long)ringsExp.Count, (long)ringsGot.Count);
                        for (int i = 0; i < Math.Min(ringsExp.Count, ringsGot.Count); i++)
                            Check(name + ".rings[" + i + "]", ringsExp[i], ringsGot[i]);
                    }
                    if (has("contest"))
                        Check(name + ".contest", Json.AsStr(eh["contest"]),
                              zbS3 != null ? zbS3.ContestZone : "NULL");
                    if (has("threat_s3"))
                        Check(name + ".threat_s3", Json.AsStr(eh["threat_s3"]),
                              zbS3 != null ? zbS3.ThreatZone : "NULL");
                    if (has("contest_with_crit"))
                        Check(name + ".contest_with_crit",
                              Json.AsStr(eh["contest_with_crit"]),
                              zbS3C != null ? zbS3C.ContestZone : "NULL");
                    // S2 gate: the threat ladder does not exist below S3 -
                    // Null-Law, bit-identical to a world without this feature.
                    if (has("gate_s2") && Json.AsBool(eh["gate_s2"]))
                    {
                        Check(name + ".gate_s2_null", null, zbS2 != null ? zbS2.ThreatZone : "NULL");
                        Check(name + ".gate_s2_no_line", false,
                              zbS2 != null && zbS2.Dump().Contains("threat"));
                    }
                }
            }

            if (Record)
            {
                var path = Path.Combine(FIX, "hero_deploy_anchor_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
