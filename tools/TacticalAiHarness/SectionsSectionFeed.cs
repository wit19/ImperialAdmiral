// S-A section/entity/join vectors (card t_00fda230): section_feed.json pins
// the vocabulary contract of the S-A kinds (SectionActivated, EntityInteract,
// PlayerSelection) on the REAL feed path - SectionsD-style direct WorldEvent
// construction would bypass the feed's fail-loud Validate(), so this section
// appends through WorldEventFeed.Append: an unknown kind, a missing identity
// key or a non-public payload key THROWS. expect blocks were recorded once
// via `--record` and reviewed (record discipline); the vocabulary laws below
// are hand-pinned ground truth (the law IS the expectation). Honest boundary
// pinned here too: the new kinds are audit-trail history - they move the
// history hash but leave NO trace in the projected view (WorldEventVector
// has no consumer case for them yet; inventing section beliefs from anchor
// ids before S-B ships would violate the no-estimation rule). The adapter's
// polling dedup + live-accessor reads + first-sight resync claims live in
// WorldEventsUnity.PollSections/PollEntities - Core cannot reproduce them.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSectionFeed()
        {
            var v = LoadJson("section_feed.json");
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

                var view1 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view1.Canon(), view2.Canon());
                Check(name + ".replay_hash", view1.HistoryHash, view2.HistoryHash);

                var got = new Dictionary<string, object>
                {
                    { "posture", view1.Posture },
                    { "objective", view1.Objective },
                    { "objective_actor", view1.ObjectiveActor },
                    { "round", (long)view1.Round },
                    { "threat", view1.ThreatBalance },
                    { "seq", (long)view1.Seq },
                    { "hash", view1.HistoryHash },
                    { "canon", view1.Canon() },
                };

                if (Record)
                {
                    recorded[name] = got;
                    // Record discipline (R-B): an EXISTING expect block is
                    // still compared in record runs, so a regression can
                    // never hide behind a --record invocation.
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        foreach (var k in new[] { "posture", "objective", "objective_actor", "round",
                                                  "threat", "seq", "hash", "canon" })
                            Check(name + ".recorded." + k, exp0[k], got[k]);
                    }
                    continue;
                }

                if (!c.ContainsKey("expect"))
                {
                    Check(name + ".expect", "present", "missing - run with --record");
                    continue;
                }
                var exp = Json.AsDict(c["expect"]);
                foreach (var k in new[] { "posture", "objective", "objective_actor", "round", "threat", "seq", "hash", "canon" })
                    Check(name + "." + k, exp[k], got[k]);
            }

            // ---- hand-pinned vocabulary laws (ground truth, not recorded) ----
            // S-A law 1: an identity-less event is a caller bug, on ALL three
            // new kinds (identity is the whole anchor story).
            Check("sec.requires_section_key", true,
                  Throws(() => new WorldEventFeed("t").Append("SectionActivated", 1,
                      new Dictionary<string, object> { { "name", "Comms" } })));
            Check("ent.requires_entity_key", true,
                  Throws(() => new WorldEventFeed("t").Append("EntityInteract", 1,
                      new Dictionary<string, object> { { "active", true } })));
            Check("sel.requires_name_key", true,
                  Throws(() => new WorldEventFeed("t").Append("PlayerSelection", 1,
                      new Dictionary<string, object>())));
            // S-A law 2: only the named kinds exist - lookalikes fail loud
            // (retires typos like "SectionActivate"/"EntityInteraction").
            Check("sec.unknown_kind_fails", true,
                  Throws(() => new WorldEventFeed("t").Append("SectionActivate", 1,
                      P("section", "s-1"))));
            // Leak guard covers the new kinds too: a position/coordinate key
            // cannot ride a section or entity event (Loch-Gesetz is enforced
            // by the SAME guard as every other kind - anchors only).
            Check("sec.leak_guard", true,
                  Throws(() => new WorldEventFeed("t").Append("SectionActivated", 1,
                      new Dictionary<string, object> { { "section", "s-1" }, { "position", "A1" } })));
            Check("ent.leak_guard", true,
                  Throws(() => new WorldEventFeed("t").Append("EntityInteract", 1,
                      new Dictionary<string, object> { { "entity", "e-1" }, { "zone_coord", "3|4" } })));
            // Positive controls: the valid shapes append cleanly (no throw) -
            // direction on EntityInteract is data ("active"), the section
            // anchor is optional (data-existence law: only when the entity's
            // own mapSectionOwner is set).
            Check("sec.valid_shape_appends", false,
                  Throws(() => new WorldEventFeed("t").Append("SectionActivated", 1,
                      new Dictionary<string, object> { { "section", "s-1" }, { "name", "Comms" } })));
            Check("ent.valid_shape_appends", false,
                  Throws(() => new WorldEventFeed("t").Append("EntityInteract", 1,
                      new Dictionary<string, object>
                      {
                          { "entity", "e-1" }, { "name", "Comms Terminal" },
                          { "section", "s-1" }, { "active", true },
                      })));
            Check("sel.valid_shape_appends", false,
                  Throws(() => new WorldEventFeed("t").Append("PlayerSelection", 1,
                      new Dictionary<string, object> { { "name", "{A} Open" } })));
            // S-A neutrality law (hand-pinned): the new kinds are audit-trail
            // ONLY - projecting a list WITHOUT and WITH a trailing
            // SectionActivated yields the same posture/objective/round/threat
            // view fields (the fact moved the seq/hash, not the belief). One
            // feed instance numbers both lists - two feeds would collide on
            // Seq and Project would (rightly) refuse the window.
            var lawFeed = new WorldEventFeed("n");
            lawFeed.Append("MissionStart", 1, new Dictionary<string, object>
            {
                { "objective", "Hold." }, { "priorityTarget", "Rebel" },
                { "roundLimit", 6L }, { "mission", "n" }, { "missionType", "Story" },
            });
            var vb = WorldEventVector.Project(new List<WorldEvent>(lawFeed.Events), "n", "h");
            lawFeed.Append("SectionActivated", 1, P("section", "s-1"));
            var vw = WorldEventVector.Project(new List<WorldEvent>(lawFeed.Events), "n", "h");
            Check("sec.inert_posture", vb.Posture, vw.Posture);
            Check("sec.inert_objective", vb.Objective, vw.Objective);
            Check("sec.inert_round", (long)vb.Round, (long)vw.Round);
            Check("sec.inert_threat", vb.ThreatBalance, vw.ThreatBalance);
            // The fact IS visible in the audit trail: exactly one more seq.
            Check("sec.moves_the_seq", 1L, vw.Seq - vb.Seq);

            if (Record)
            {
                var path = Path.Combine(FIX, "section_feed_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
