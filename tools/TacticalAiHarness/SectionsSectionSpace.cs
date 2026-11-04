// S-B section-space module: section_space.json pins SectionGraph, SectionMask,
// the speed track, the disjunctive laws and the two duty-fact probes
// (Wachzone, 3c) on synthetic section missions - same shape as Sections-
// DoorFeed/SectionFeed: events go through the REAL feed path (WorldEventFeed.
// Append fails loud on unknown kinds/keys), the SectionSpace projection and
// the SecLedger vector are compared against hand-reviewed expect blocks
// (recorded once via --record, law comments below), and the Grundgesetz /
// disjunctive laws that ARE the ground truth are hand-pinned directly.
//
// Canon accounting: NO pre-existing fixture is touched by this module. The
// only vector whose canon moves is S-A's sa_inert_inside_hero_window (a
// sec: FACT anchor now replaces the at:STORM hypothesis inside an open
// window - the data-existence upgrade S-A's own comment announced for the
// hop shipping the consumer). Every other vector keeps its byte-identical
// canon; this section proves it by re-projecting those event streams too.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSectionSpace()
        {
            var v = LoadJson("section_space.json");
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

                // ---- SecLedger vector (byte-identity law per case) --------
                var view1 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view1.Canon(), view2.Canon());
                Check(name + ".replay_hash", view1.HistoryHash, view2.HistoryHash);

                // ---- SectionSpace projection (deterministic + incremental) --
                var seeds = ReadSeeds(c);
                var edges = ReadEdges(c);
                var space1 = SectionSpace.Project(feed.Events, null, seeds, edges);
                var space2 = SectionSpace.Project(feed.Events, null, seeds, edges);
                Check(name + ".replay_dump", space1.Dump(), space2.Dump());

                // Incremental law (vector parity): split replay equals one-shot.
                if (c.ContainsKey("split"))
                {
                    int cut = (int)Json.AsLong(c["split"]);
                    var evs = new List<WorldEvent>(feed.Events);
                    var pre = SectionSpace.Project(evs.GetRange(0, cut), null, seeds, null);
                    var post = SectionSpace.Project(evs.GetRange(cut, evs.Count - cut), pre, seeds, edges);
                    Check(name + ".incremental_dump", space1.Dump(), post.Dump());
                }

                var got = new Dictionary<string, object>
                {
                    { "dump", space1.Dump() },
                    { "canon", view1.Canon() },
                    { "wachzone", WachzoneLines(space1, c) },
                    { "threec", ThreeCLines(space1) },
                };

                // Hole-law per case (hand check, never recorded): the named
                // substring must appear in NEITHER dump nor canon (a section-
                // space leak into an old-kind vector is the bug class).
                if (c.ContainsKey("forbid"))
                {
                    var f = Json.AsStr(c["forbid"]);
                    Check(name + ".forbid_dump", false, space1.Dump().Contains(f + " "));
                    Check(name + ".forbid_canon", false, view1.Canon().Contains(f + ":"));
                }

                if (Record)
                {
                    recorded[name] = got;
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        for (var k = 0; k < ExpectKeys.Length; k++)
                        {
                            var key = ExpectKeys[k];
                            if (exp0.ContainsKey(key)) Check(name + ".recorded." + key, exp0[key], got[key]);
                        }
                    }
                    continue;
                }

                if (!c.ContainsKey("expect"))
                {
                    Check(name + ".expect", "present", "missing - run with --record");
                    continue;
                }
                var exp = Json.AsDict(c["expect"]);
                foreach (var key in ExpectKeys)
                    if (exp.ContainsKey(key)) Check(name + "." + key, exp[key], got[key]);
            }

            // ---- hand-pinned laws (ground truth IS the expectation) --------
            // Grundgesetz 1: an EMPTY graph passes everything through and
            // knows nothing - DistanceLowerBound(-1) reads "no knowledge",
            // never "blocked" (concept 8 law as code).
            var empty = new SectionGraph();
            empty.AddSection("s-x");
            empty.AddSection("s-y");
            Check("law.unknown_edge_passable", true, empty.IsPassable("s-x", "s-y"));
            Check("law.no_route_is_no_knowledge", (long)SectionGraph.NoKnowledge,
                  (long)empty.DistanceLowerBound("s-x", "s-y"));
            // Grundgesetz 2: a CLOSED door is a state, not a blockade: the
            // lower bound still counts it, passability is untouched.
            var dg = new SectionGraph();
            dg.AddDoorEdge("s-a", "s-b", "d-1");
            dg.NoteDoorState("d-1", false);
            Check("law.closed_door_state", SectionGraph.DoorClosed, dg.DoorStateOf("s-a", "s-b"));
            Check("law.closed_door_still_passable", true, dg.IsPassable("s-a", "s-b"));
            Check("law.closed_door_still_bounded", 1L, (long)dg.DistanceLowerBound("s-a", "s-b"));
            // Grundgesetz 3: a section outside a mask is UNKNOWN. There is no
            // "closed" answer to give - the constant itself does not exist.
            var mg = new SectionGraph();
            mg.AddSection("s-here");
            var mm = new SectionMask();
            mm.AddObservation(1, "deploy", new[] { "s-here" }, "fact");
            Check("law.outside_mask_is_unknown", SectionMask.StatusUnknown,
                  mm.Status(mg, "s-there", 1, 1));
            Check("law.inside_mask_is_inside", SectionMask.StatusInside,
                  mm.Status(mg, "s-here", 1, 1));
            // Full-integer law: exact seed dominance is the exact rational 1,
            // a 3-way candidate set is exactly 1/3 (no double rounding).
            Check("law.dom_exact_seed", "1", mm.Dominance(mg, 1, 1).ToString());
            var mg3 = new SectionGraph();
            mg3.AddSection("s-1"); mg3.AddSection("s-2"); mg3.AddSection("s-3");
            var m3 = new SectionMask();
            m3.AddObservation(1, "deploy", new[] { "s-1", "s-2", "s-3" }, "fact");
            Check("law.dom_three_way", "1/3", m3.Dominance(mg3, 1, 1).ToString());
            // Observation law: alternatives UNION within one observation;
            // disjoint observations degrade to the union (honest diffuseness,
            // never a fabricated narrowing of lower-bound anchors).
            var ig = new SectionGraph();
            ig.AddSection("s-a"); ig.AddSection("s-c");
            var im = new SectionMask();
            im.AddObservation(1, "deploy", new[] { "s-a", "s-c" }, "fact");
            im.AddObservation(2, "hit", new[] { "s-c" }, "fact");
            Check("law.observation_intersection", "s-c", string.Join(",",
                  new List<string>(im.Candidates(ig, 2, 0)).ToArray()));
            var dm = new SectionMask();
            dm.AddObservation(1, "deploy", new[] { "s-a" }, "fact");
            dm.AddObservation(2, "hit", new[] { "s-c" }, "fact");
            var dc = dm.Candidates(ig, 1, 0);
            Check("law.degrade_to_union", "s-a,s-c", string.Join(",", new List<string>(dc).ToArray()));
            Check("law.degrade_flag", true, dm.DegradedToUnion);
            // Grade law: one fact supporter upgrades a candidate; a candidate
            // only ever hypothesis-supported reads hypothesis (fairness law:
            // a hypothesis must never present as a fact).
            var gm = new SectionMask();
            gm.AddObservation(1, "deploy", new[] { "s-a" }, "hypothesis");
            gm.AddObservation(2, "hit", new[] { "s-c" }, "fact");
            Check("law.grade_hypothesis_only", "hypothesis", gm.CandidateGrade(ig, "s-a", 2, 0));
            Check("law.grade_fact_wins", "fact", gm.CandidateGrade(ig, "s-c", 2, 0));
            // Disjunctive law: the anonymous victim (-1) re-grounds EVERY
            // figure as a group disjunctive and NEVER names one; a named
            // hit touches exactly the named figure. (Fixture case
            // ss_disjunctive_anon_vs_named pins the dump shape; these pin
            // the invariant directly.)
            var gb = new GroupSectionBelief("GUARD", 2);
            gb.SeedDeploy(new[] { "s-a" }, 1);
            var bg = new SectionGraph();
            bg.AddSection("s-a"); // the belief's section must exist for belief math
            var beforeFig1 = gb.MaskOf(1).ObservationCount;
            gb.NoteHit(bg, 0, 1, 1); // named hit on figure 0
            Check("law.named_hit_untouched_other", (long)beforeFig1, (long)gb.MaskOf(1).ObservationCount);
            gb.NoteHit(bg, GroupSectionBelief.AnonymousVictim, 1, 1);
            Check("law.anon_flag", true, gb.AnonymousVictimObserved);
            Check("law.anon_regrounded_all", 1L, (long)CountSources(gb.MaskOf(0), "hit-group"));
            Check("law.anon_regrounded_other", 1L, (long)CountSources(gb.MaskOf(1), "hit-group"));
            // Speed floor: before ANY observation the growth speed is 1 -
            // never 0 (a belief must still stretch), never guessed higher.
            var sp = SectionSpace.Project(new List<WorldEvent>());
            Check("law.speed_floor", 1L, (long)sp.SectionsPerWindow);
            // HeroWounded without an imperial window attributes NOTHING
            // (never from absence) - pinned via the empty-window belief.
            var wb = new GroupSectionBelief("G", 1);
            wb.SeedDeploy(new[] { "s-a" }, 1);
            Check("law.wounded_starts_clean", false, wb.WoundedDisjunctiveObserved);
            // HeroWounded during an OPEN HERO window attributes to NO
            // imperial group: a wound in the hero's own activation is mission
            // environment, not the last group's doing (naming it would invent
            // causation - fairness law against false attacker claims).
            var wf = new WorldEventFeed("law|wounded_hero_window");
            wf.Append("MissionStart", 1, new Dictionary<string, object>
            {
                { "objective", "Probe." }, { "priorityTarget", "REBEL" },
                { "roundLimit", 6L }, { "mission", "LAW" }
            });
            wf.Append("Spawn", 1, new Dictionary<string, object>
            { { "group", "GUARD" }, { "size", 1L }, { "section", "s-a" } });
            wf.Append("ActivationStart", 2, new Dictionary<string, object> { { "group", "GUARD" } });
            wf.Append("HeroActivationStart", 2, new Dictionary<string, object> { { "hero", "LEIA" } });
            wf.Append("HeroWounded", 2, new Dictionary<string, object> { { "hero", "LEIA" } });
            var wspace = SectionSpace.Project(wf.Events);
            var wbelief = wspace.BeliefOf("GUARD");
            Check("law.wounded_not_in_hero_window", false,
                  wbelief != null && wbelief.WoundedDisjunctiveObserved);

            if (Record)
            {
                var path = Path.Combine(FIX, "section_space_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }

        static readonly string[] ExpectKeys = { "dump", "canon", "wachzone", "threec" };

        static Dictionary<string, string[]> ReadSeeds(Dictionary<string, object> c)
        {
            var seeds = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (!c.ContainsKey("deploySeeds")) return seeds;
            foreach (var kv in Json.AsDict(c["deploySeeds"]))
            {
                var l = new List<string>();
                foreach (var s in Json.AsList(kv.Value)) l.Add(Json.AsStr(s));
                seeds[kv.Key] = l.ToArray();
            }
            return seeds;
        }

        static List<string[]> ReadEdges(Dictionary<string, object> c)
        {
            var rows = new List<string[]>();
            if (!c.ContainsKey("doorEdges")) return rows;
            foreach (var raw in Json.AsList(c["doorEdges"]))
            {
                var row = Json.AsList(raw);
                if (row.Count < 3) continue;
                rows.Add(new[] { Json.AsStr(row[0]), Json.AsStr(row[1]), Json.AsStr(row[2]) });
            }
            return rows;
        }

        // Case-level probe configuration: "critical": [sections], "guards":
        // [groups]. The Wachzone lines the S-D consumer would read.
        static List<object> WachzoneLines(SectionSpace space, Dictionary<string, object> c)
        {
            var lines = new List<object>();
            if (!c.ContainsKey("critical") || !c.ContainsKey("guards")) return lines;
            var crit = new List<string>();
            foreach (var s in Json.AsList(c["critical"])) crit.Add(Json.AsStr(s));
            var guards = new List<string>();
            foreach (var s in Json.AsList(c["guards"])) guards.Add(Json.AsStr(s));
            foreach (var f in space.WachzoneFacts(crit, guards))
                lines.Add(f.Hero + "|" + f.Section + "|" + f.GuardGroup + "|" + f.Round);
            return lines;
        }

        static List<object> ThreeCLines(SectionSpace space)
        {
            var lines = new List<object>();
            foreach (var f in space.ThreeCFacts())
                lines.Add(f.Group + "|" + f.Section + "|" + f.Hero + "|" + f.Round);
            return lines;
        }

        static int CountSources(SectionMask m, string source)
        {
            int n = 0;
            var dump = m.Dump(new SectionGraph(), 0, 1);
            foreach (var line in dump.Split('\n'))
                if (line.StartsWith("obs ", StringComparison.Ordinal)
                    && line.Contains(" src=" + source + " ")) n++;
            return n;
        }
    }
}
