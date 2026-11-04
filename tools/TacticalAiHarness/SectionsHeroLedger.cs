// R-B Last-Seen-Ledger vectors: hero_ledger.json pins the SYMBOLIC presence
// anchors of concept v1.2 §5 B on the REAL WorldEventFeed.Append path (same
// fail-loud discipline as door_feed). The ledger is projection-only: zones
// are event-anchor identities ("obj:<text>", "door:<GUID>", "at:<group>"),
// never coordinates, and the anchor KIND is first-class - "fact" (the public
// M10b attribution law places the actor there) vs "hypothesis" (a hit proves
// weapon-range proximity, NOT a placed position). expect blocks were
// recorded once via `dotnet run -- <fixtures> --record` and reviewed (record
// discipline); the LAW checks at the bottom are hand-pinned ground truth
// (the law IS the expectation - a recorded output would only mirror code).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecHeroLedger()
        {
            var v = LoadJson("hero_ledger.json");
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
                Check(name + ".replay_ledger_text", view1.LedgerText(), view2.LedgerText());
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
                    { "ledger_text", view1.LedgerText() },
                    { "anchors", LedgerFields(view1) },
                    { "spawned_in_window", SpawnedFields(view1) },
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
                                                  "threat", "seq", "hash", "canon", "ledger_text",
                                                  "anchors", "spawned_in_window" })
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
                foreach (var k in new[] { "posture", "objective", "objective_actor", "round",
                                          "threat", "seq", "hash", "canon", "ledger_text",
                                          "anchors", "spawned_in_window" })
                    Check(name + "." + k, exp[k], got[k]);
            }

            // ---- hand-pinned ledger laws (ground truth, not recorded) ----
            // R-B law 1: a hit inside an open window is a HYPOTHESIS anchor
            // and the kind MUST show in the debug projection - a consumer
            // (and Thomas' log) can never misread it as a placed fact.
            var f3 = new WorldEventFeed("law");
            f3.Append("MissionStart", 1, new Dictionary<string, object> {
                { "objective", "o" }, { "priorityTarget", "" }, { "roundLimit", -1L },
                { "mission", "law" } });
            f3.Append("HeroActivationStart", 2, P("hero", "Lokaku"));
            f3.Append("GroupDamage", 2, new Dictionary<string, object> {
                { "group", "STORM" }, { "figure", 0L }, { "wheel", 1L }, { "health", 4L } });
            var vHyp = WorldEventVector.Project(f3.Events, "law", f3.Hash());
            Check("ledger.hypothesis_kind_shown", true,
                  vHyp.Canon().Contains("kind=hypothesis"));
            Check("ledger.hypothesis_worded", true,
                  vHyp.LedgerText().Contains("vermutlich"));

            // R-B law 2: NO entry is EVER written from absence - a hero who
            // took damage OUTSIDE any window gets no anchor row at all, and
            // the ledger stays byte-empty.
            var f4 = new WorldEventFeed("law");
            f4.Append("MissionStart", 1, new Dictionary<string, object> {
                { "objective", "o" }, { "priorityTarget", "" }, { "roundLimit", -1L },
                { "mission", "law" } });
            f4.Append("GroupDamage", 2, new Dictionary<string, object> {
                { "group", "STORM" }, { "figure", 0L }, { "wheel", 1L }, { "health", 4L } });
            f4.Append("HeroWounded", 2, P("hero", "Han"));
            var vAbs = WorldEventVector.Project(f4.Events, "law", f4.Hash());
            Check("ledger.no_anchor_from_absence", "", LedgerFields(vAbs));
            Check("ledger.empty_ledger_text", true, vAbs.LedgerText().Length == 0);
            // Substring probes must anchor at the LINE start: a bare "seen="
            // also occurs inside "Stormtroopers" (R-B first record run caught
            // this very trap). Canon ledger lines always start a line.
            Check("ledger.no_seen_line", false, vAbs.Canon().Contains("\nseen="));

            // R-B law 3: the kind vocabulary is EXACTLY {fact, hypothesis}
            // across every case above - no third reading is constructible.
            bool kindsOk = true;
            foreach (var h in vHyp.Heroes)
                if (h.LastSeenZone != null && h.LastSeenKind != FeedWorldView.HeroView.LedgerKindFact
                                          && h.LastSeenKind != FeedWorldView.HeroView.LedgerKindHypothesis)
                    kindsOk = false;
            foreach (var h in vAbs.Heroes)
                if (h.LastSeenZone != null) kindsOk = false;
            Check("ledger.kind_vocabulary", true, kindsOk);

            // R-B law 4: a spawn OUTSIDE any window never sets the marker -
            // "in_window" is a window fact, not a spawn fact.
            var f5 = new WorldEventFeed("law");
            f5.Append("MissionStart", 1, new Dictionary<string, object> {
                { "objective", "o" }, { "priorityTarget", "" }, { "roundLimit", -1L },
                { "mission", "law" } });
            f5.Append("Spawn", 1, new Dictionary<string, object> {
                { "group", "STORM" }, { "size", 2L } });
            var vSpawn = WorldEventVector.Project(f5.Events, "law", f5.Hash());
            Check("ledger.spawn_outside_window", "", SpawnedFields(vSpawn));
            Check("ledger.spawn_row_unchanged", false, vSpawn.Canon().Contains("|in_window"));

            if (Record)
            {
                var path = Path.Combine(FIX, "hero_ledger_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }

        // "Hero|zone|kind|seq|round" per hero WITH an anchor (sorted view
        // order), joined by ';'. Heroes without an anchor contribute NOTHING
        // - the never-from-absence law visible in the pin format itself.
        static string LedgerFields(FeedWorldView view)
        {
            var sb = new StringBuilder();
            foreach (var h in view.Heroes)
            {
                if (string.IsNullOrEmpty(h.LastSeenZone)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(h.HeroId).Append('|').Append(h.LastSeenZone).Append('|')
                  .Append(h.LastSeenKind).Append('|').Append(h.LastSeenSeq).Append('|')
                  .Append(h.LastSeenRound);
            }
            return sb.ToString();
        }

        // "Name" per group whose Spawn fell into an open hero window.
        static string SpawnedFields(FeedWorldView view)
        {
            var sb = new StringBuilder();
            foreach (var g in view.Groups)
            {
                if (!g.SpawnedInHeroWindow) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(g.GroupId);
            }
            return sb.ToString();
        }
    }
}
