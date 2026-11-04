// Player-24 BEFEHLSZEITPUNKT vectors (Thomas 04.11 Go): officer_timing.json
// pins CommandTiming.ForRound on the REAL feed path (WorldEventFeed.Append +
// WorldEventVector.Project + SectionSpace.Project, same fail-loud discipline
// as officer_doctrine), then pins the PLAN CONSUMPTION in OrderPlanner:
// the named receiver replaces the blind first-attacker anchor, the slot
// follows the payload (before = Bewegungshilfe, after = zweiter Angriff,
// tag "receiver_before_buff"), and the decision why-line rides on the
// supporter's order card. timing=null cases prove the blind canon stays
// reachable and bit-identical (the pinned order_plan.json vectors govern the
// untouched path; here the blind why-string is asserted directly).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecOfficerTiming()
        {
            var v = LoadJson("officer_timing.json");

            var cards = new Dictionary<string, DeploymentCard>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(v["cards"]))
            {
                var c0 = DeploymentCard.FromRecord(Json.AsDict(raw));
                cards[c0.Id] = c0;
            }

            // ---------------- decision cases --------------------------------
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                int round = (int)Json.AsLong(Json.Get(c, "round", 2L));

                var feed = new WorldEventFeed("vector|" + name);
                AppendFeedEvents(feed, c["events"]);
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                SectionTrust.Clear();
                var space = SectionSpace.Project(feed.Events, null,
                    ReadSeeds(c), ReadEdges(c), new SectionOptions());
                var facts = ReadTimingFacts(c);

                var got = CommandTiming.ForRound(round, facts, space, view, cards);

                var gotRow = new Dictionary<string, object>(StringComparer.Ordinal);
                var exp = Json.AsDict(c["expect"]);
                foreach (var kv in exp)
                {
                    var e = Json.AsDict(kv.Value);
                    CommandTimingDecision d;
                    bool have = got.TryGetValue(kv.Key, out d);
                    int present = have ? 1 : 0;
                    Check(name + "." + kv.Key + ".present",
                          (long)Json.AsLong(e["present"]), (long)present);
                    if (!have)
                    {
                        gotRow[kv.Key] = new Dictionary<string, object> { { "present", 0L } };
                        continue;
                    }
                    Check(name + "." + kv.Key + ".receiver", Json.AsStr(e["receiver"]), d.ReceiverGroup);
                    Check(name + "." + kv.Key + ".before",
                          (long)Json.AsLong(e["before"]), d.Before ? 1L : 0L);
                    Check(name + "." + kv.Key + ".ring",
                          (long)Json.AsLong(e["ring"]), d.RingShared ? 1L : 0L);
                    Check(name + "." + kv.Key + ".why", Json.AsStr(e["why"]), d.Why);
                    gotRow[kv.Key] = new Dictionary<string, object>
                    {
                        { "present", 1L },
                        { "receiver", d.ReceiverGroup },
                        { "before", d.Before ? 1L : 0L },
                        { "ring", d.RingShared ? 1L : 0L },
                        { "why", d.Why },
                    };
                }
                // groups the decision must NEVER speak for (silence law):
                if (c.ContainsKey("silent"))
                    foreach (var sid in Json.AsList(c["silent"]))
                        Check(name + ".silent." + Json.AsStr(sid), false,
                              got.ContainsKey(Json.AsStr(sid)));
                recorded[name] = gotRow;
            }
            if (Record)
            {
                var path = Path.Combine(FIX, "officer_timing_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }

            // ---------------- plan consumption ------------------------------
            foreach (var raw in Json.AsList(Json.Get(v, "plan_cases", new List<object>())))
            {
                var c = Json.AsDict(raw);
                var name = "plan." + Json.AsStr(c["name"]);
                int round = (int)Json.AsLong(Json.Get(c, "round", 2L));

                var feed = new WorldEventFeed("vector|" + name);
                AppendFeedEvents(feed, c["events"]);
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                SectionTrust.Clear();
                var space = SectionSpace.Project(feed.Events, null,
                    ReadSeeds(c), ReadEdges(c), new SectionOptions());
                var facts = ReadTimingFacts(c);

                Dictionary<string, CommandTimingDecision> timing = null;
                if (Json.AsLong(Json.Get(c, "use_timing", 1L)) == 1)
                    timing = CommandTiming.ForRound(round, facts, space, view, cards);

                var prof = SkillProfile.For("S" + (int)Json.AsLong(Json.Get(c, "skill", 5L)));
                var rng = new DeterministicRandom(0x5EEDBEEFUL);
                var plan = OrderPlanner.PlanRound(round, facts, cards, "eliminate", prof, rng,
                                                  round, null, null, null, null, timing);
                var ids = plan.GroupIds();
                Check(name + ".permutation", (long)facts.Count, (long)ids.Count);

                var sup = Json.AsStr(c["supporter"]);
                var rec = Json.AsStr(c["receiver"]);
                int iS = ids.IndexOf(sup), iR = ids.IndexOf(rec);
                OrderSlot slot = null;
                foreach (var s in plan.Slots)
                    if (s.GroupId == sup) slot = s;
                var expect = Json.AsStr(c["expect"]);
                if (expect == "before")
                    Check(name + ".slot", true, iS < iR);
                else if (expect == "after")
                {
                    // the law is DIRECT adjacency (the interrupt pays right
                    // after the receiver's activation - and stays adjacent
                    // even when finisher_last pulled the receiver away).
                    Check(name + ".slot", true, iS == iR + 1);
                    Check(name + ".tag", true, slot != null && slot.Tags.Contains("receiver_before_buff"));
                    Check(name + ".why", true,
                          slot != null && slot.Why.Contains("Befehlszeitpunkt"));
                }
                else if (expect == "blind")
                {
                    Check(name + ".slot", true, iS < iR);   // blind law: before first attacker
                    Check(name + ".tag", true, slot != null && slot.Tags.Contains("buff_before_receiver"));
                    Check(name + ".why", true,
                          slot != null && slot.Why.Contains("Buff zuerst: Staerkt die Angreifer dieser Runde"));
                }
            }
        }

        // Row schema: {id, card, name, pattern, n_alive, vitality, zone,
        // supporter, plain_order}. Own reader (the shared FactsFromJson does
        // not carry zone/supporter/plain rows, and ReadSdFacts forces them
        // false - a timing vector must be able to SAY who commands whom).
        static List<GroupFacts> ReadTimingFacts(Dictionary<string, object> c)
        {
            var l = new List<GroupFacts>();
            if (!c.ContainsKey("groups")) return l;
            int idx = 0;
            foreach (var grow in Json.AsList(c["groups"]))
            {
                var g = Json.AsDict(grow);
                l.Add(new GroupFacts(
                    Json.AsStr(g["id"]), idx++,
                    Json.AsStr(g["card"]), Json.AsStr(g["name"]),
                    Json.AsStr(Json.Get(g, "pattern", (object)"aggressive")),
                    (int)Json.AsLong(Json.Get(g, "n_alive", 1L)),
                    Rational.Parse(Json.AsStr(Json.Get(g, "vitality", (object)"1"))),
                    zone: g.ContainsKey("zone") && !(g["zone"] is null) ? Json.AsStr(g["zone"]) : null,
                    supporter: Json.AsLong(Json.Get(g, "supporter", 0L)) == 1,
                    movementBuffOnly: Json.AsLong(Json.Get(g, "plain_order", 0L)) == 1));
            }
            return l;
        }
    }
}
