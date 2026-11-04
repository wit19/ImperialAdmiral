// R-A door-event vectors: door_feed.json pins the vocabulary contract of the
// R-A door kinds (DoorOpen/DoorClosed) on the REAL feed path - SectionsD
// builds WorldEvent objects directly and thus bypasses the feed's fail-loud
// Validate(); this section appends through WorldEventFeed.Append, so an
// unknown kind, a missing "door" key or a non-public payload key THROWS.
// expect blocks were recorded once via `--record` and reviewed (record
// discipline); the three vocabulary laws below are hand-pinned ground truth
// (the law IS the expectation - a recorded output would only mirror code).
// Honest boundary pinned here too: door events are audit-trail history -
// they move the history hash but leave NO trace in the projected view (the
// reviewed canon shows it; the Core projection has no door consumers, and
// inventing positions from door facts would violate the no-estimation rule).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecDoorFeed()
        {
            var v = LoadJson("door_feed.json");
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

                if (Record) { recorded[name] = got; continue; }

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
            // R-A law 1: a door event without its identity key is a caller bug.
            Check("door.requires_door_key", true,
                  Throws(() => new WorldEventFeed("t").Append("DoorOpen", 1, new Dictionary<string, object>())));
            // R-A law 2: only the two kinds exist - lookalikes fail loud
            // (the same law retires typos like "DoorOpened"/"OpenDoor").
            Check("door.unknown_kind_fails", true,
                  Throws(() => new WorldEventFeed("t").Append("DoorOpened", 1, P("door", "d-1"))));
            // Leak guard covers the new kinds too: a non-public payload key
            // (a position!) cannot ride a door event. ("zone" would PASS -
            // it is a reserved Move key of the public vocabulary; the guard
            // only rejects keys OUTSIDE the whole surface.)
            Check("door.leak_guard", true,
                  Throws(() => new WorldEventFeed("t").Append("DoorOpen", 1,
                      new Dictionary<string, object> { { "door", "d-1" }, { "position", "A1" } })));
            // Positive control: the valid shape appends cleanly (no throw).
            Check("door.valid_shape_appends", false,
                  Throws(() => new WorldEventFeed("t").Append("DoorClosed", 1,
                      new Dictionary<string, object> { { "door", "d-1" }, { "name", "Hangar West" } })));

            if (Record)
            {
                var path = Path.Combine(FIX, "door_feed_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }

        static bool Throws(Action act)
        {
            try { act(); return false; }
            catch (ArgumentException) { return true; }
        }

        static Dictionary<string, object> P(string k, object v)
            => new Dictionary<string, object> { { k, v } };
    }
}
