// M14e section (CONCEPT_standort_ledger_v2, Thomas 07.11): the deploy-ledger
// vectors. Three families:
//   1) the hero-entry TABLE reader (synthetic tables, hand-pinned - pure
//      data law, no seed anywhere),
//   2) the C4 FRIEND LEASH inside DeploymentAnchors.Choose - pinned as law
//      RELATIONS (leash rides the tie only, tiers keep Primat, no-leash
//      stays seed-chain) so no TieKey has to be recomputed by hand; the
//      absolute seed-chain winner lands in deploy_ledger_record.json for
//      review (record discipline, same file law as section_space_record),
//   3) the HeroDeployed entry-anchor vectors through the REAL feed path
//      (Append validates): single field anchors the hero and the entry
//      section CONFIRMS a graph edge the control case (no entry) can only
//      hypothesize - THAT is the precision gain the concept promised;
//      multi-field missions keep the honest set, a collapsing set anchors,
//      and a mission without a field changes NOTHING (Null-Law).
// Canon accounting: no pre-existing fixture touched; WorldEventVector has
// no HeroDeployed case, so every vector canon stays byte-identical by
// construction - the dp/canon record files prove it for the new streams.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDeployLedger()
        {
            var v = LoadJson("deploy_ledger.json");
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);

            // ------------------------------------------------ 1) hero-entry reader
            foreach (var raw in Json.AsList(v["reader_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                object table = c["table"];                    // null => no table
                var rows = DeploymentAnchors.HeroEntriesForMission(table, Json.AsStr(c["missionFile"]));
                var got = rows.Select(r => r.Guid + "@" + r.SectionGuid).ToList();
                var exp = Json.AsList(c["expect"]).Select(Json.AsStr).ToList();
                Check("reader[" + name + "].count", (long)exp.Count, (long)got.Count);
                for (int i = 0; i < Math.Min(exp.Count, got.Count); i++)
                    Check("reader[" + name + "][" + i + "]", exp[i], got[i]);
            }

            // ------------------------------------------------------ 2) C4 leash
            foreach (var raw in Json.AsList(v["leash_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var seed = (ulong)Json.AsLong(c["seed"]);
                int round = (int)Json.AsLong(c["round"]);
                var anchors = new List<DeployAnchor>();
                if (c["anchors"] != null)
                    foreach (var araw in Json.AsList(c["anchors"]))
                    {
                        var a = Json.AsDict(araw);
                        anchors.Add(new DeployAnchor
                        {
                            EntityGuid = Json.AsStr(a["guid"]),
                            SectionGuid = Json.AsStr(a["section"]),
                            X = (double)Json.AsLong(a["x"]),
                            Y = (double)Json.AsLong(a["y"]),
                        });
                    }
                Func<List<DeployCandidate>> mkCands = () =>
                    Json.AsList(c["cands"]).Select(x =>
                    {
                        var cd = Json.AsDict(x);
                        return new DeployCandidate
                        {
                            Guid = Json.AsStr(cd["guid"]),
                            Name = Json.AsStr(cd["name"]),
                            SectionGuid = Json.AsStr(cd["section"]),
                            X = (double)Json.AsLong(cd["x"]),
                            Y = (double)Json.AsLong(cd["y"]),
                        };
                    }).ToList();
                var own = c["ownSections"] == null ? null
                    : Json.AsList(c["ownSections"]).Select(Json.AsStr).ToList();

                var pick = DeploymentAnchors.Choose(mkCands(), anchors, seed, round, own);
                var legacy = DeploymentAnchors.Choose(mkCands(), anchors, seed, round, null);
                var got = new Dictionary<string, object>
                {
                    { "pick", pick == null ? null : pick.Candidate.Guid },
                    { "no_leash", legacy == null ? null : legacy.Candidate.Guid },
                };
                recorded["leash|" + name] = got;
                // neutrality law (hand check, never recorded): a leash that
                // hits BOTH or NEITHER candidate must not move the pick -
                // the seed chain stays in charge, bit-identical to no-leash.
                if (c.ContainsKey("expect") && Json.AsDict(c["expect"]).ContainsKey("pick_equals_no_leash")
                    && Json.AsBool(Json.AsDict(c["expect"])["pick_equals_no_leash"]))
                    Check("leash[" + name + "].neutral", got["no_leash"], got["pick"]);
                if (c.ContainsKey("expect"))
                {
                    var exp = Json.AsDict(c["expect"]);
                    if (exp.ContainsKey("pick"))
                        Check("leash[" + name + "].pick", exp["pick"], got["pick"]);
                    if (exp.ContainsKey("no_leash"))
                        Check("leash[" + name + "].no_leash", exp["no_leash"], got["no_leash"]);
                }
            }

            // ------------------------------------------------ 3) HeroDeployed space
            foreach (var raw in Json.AsList(v["space_cases"]))
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
                var view1 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view1.Canon(), view2.Canon());

                var seeds = ReadSeeds(c);
                var space1 = SectionSpace.Project(feed.Events, null, seeds, null);
                var space2 = SectionSpace.Project(feed.Events, null, seeds, null);
                Check(name + ".replay_dump", space1.Dump(), space2.Dump());

                var gotd = new Dictionary<string, object>
                {
                    { "dump", space1.Dump() },
                    { "canon", view1.Canon() },
                };

                // hand-pinned law checks (never recorded): the spatial claims
                var hero = c.ContainsKey("hero") ? Json.AsStr(c["hero"]) : "";
                var hs = space1.HeroInSection(hero ?? "");
                var hr = space1.HeroSectionRound(hero ?? "");
                if (c.ContainsKey("expect_hand"))
                {
                    var eh = Json.AsDict(c["expect_hand"]);
                    Func<string, bool> has = k => eh.ContainsKey(k) && eh[k] != null;
                    Check(name + ".hero_section", Json.AsStr(eh["hero_section"]) ?? "", hs);
                    int expRound = has("hero_round") ? (int)Json.AsLong(eh["hero_round"]) : -1;
                    Check(name + ".hero_round", (long)expRound, (long)(hs.Length == 0 ? -1 : hr));
                    if (has("node_a"))
                        Check(name + ".node_a", Json.AsBool(eh["node_a"]),
                              space1.Graph.HasSection(Json.AsStr(c["secA"])));
                    if (has("node_b"))
                        Check(name + ".node_b", Json.AsBool(eh["node_b"]),
                              space1.Graph.HasSection(Json.AsStr(c["secB"])));
                    if (has("edge_kind_ab"))
                        Check(name + ".edge_kind_ab", Json.AsStr(eh["edge_kind_ab"]) ?? "",
                              space1.Graph.EdgeKind(Json.AsStr(c["secA"]), Json.AsStr(c["secB"])) ?? "");
                    if (has("spawn_candidates"))
                    {
                        var grp = Json.AsStr(c["group"]);
                        int rn = has("spawn_round") ? (int)Json.AsLong(eh["spawn_round"]) : 1;
                        var bel = space1.BeliefOf(grp);
                        var mask = bel == null ? null : bel.MaskOf(0);
                        var cand = mask == null
                            ? new List<string>()
                            : new List<string>(mask.Candidates(space1.Graph, rn, 0));
                        Check(name + ".spawn_candidates",
                              string.Join(",", Json.AsList(eh["spawn_candidates"]).Select(Json.AsStr)),
                              string.Join(",", cand));
                    }
                }

                if (Record) recorded["space|" + name] = gotd;
            }

            if (Record)
            {
                var path = Path.Combine(FIX, "deploy_ledger_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
