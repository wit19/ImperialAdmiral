// R-C ReactionOrder vectors: reaction_order.json pins the per-click re-
// scoring of concept v1.2 par.2.2 (Thomas decision 04.11.: at EVERY press
// the order is recomputed from the REMAINING figures - no switch, always).
// Same discipline as hero_ledger: cases run on the REAL WorldEventFeed.Append
// path (fail-loud vocabulary check), expect blocks were recorded once via
// `dotnet run -- <fixtures> --record` and reviewed (record discipline: a
// record run still compares EXISTING expects, so no regression hides behind
// --record). The LAW checks at the bottom are hand-pinned ground truth -
// the law IS the expectation (class dominance, skill gates, unknown block,
// fail-loud, finisher_last stays in the basis, no-feed basis, R3 window+
// round, seq-ordinal class bound).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecReactionOrder()
        {
            var v = LoadJson("reaction_order.json");
            var catalog = Catalog();
            var cases = Json.AsList(v["cases"]);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var round = (int)Json.AsLong(c["round"]);
                var skill = (int)Json.AsLong(c["skill"]);
                var seed = (ulong)Json.AsLong(c["seed"]);
                var posture = Json.AsStr(c["posture"]);
                string counter = c.ContainsKey("counter") && !(c["counter"] is null)
                    ? Json.AsStr(c["counter"]) : null;

                // THE REAL feed path: Append validates kind + payload fail-loud.
                var feed = new WorldEventFeed("vector|" + name);
                AppendFeedEvents(feed, c["events"]);

                var facts = FactsFromJson(c["groups"]);
                var pending = Json.AsList(c["pending"]).Select(Json.AsStr).ToList();

                // -- walk cases: the whole activation sequence of the round ----
                if (Json.AsLong(Json.Get(c, "walk", (long)0)) == 1)
                {
                    var pend = new List<string>(pending);
                    var factsNow = new List<GroupFacts>(facts);
                    var mid = Json.AsList(Json.Get(c, "mid_events", (object)new List<object>()));
                    var picks = new List<object>();
                    string lastWhy = "";
                    int step = 0;
                    bool dup = false;
                    while (pend.Count > 0)
                    {
                        foreach (var mO in mid.Select(Json.AsDict))
                            if ((int)Json.AsLong(Json.Get(mO, "after_step", (long)-1)) == step)
                            {
                                AppendFeedEvents(feed, mO["events"]);
                                // The App truth: a mid-round spawn joins the
                                // pending list ONLY once it is on the board -
                                // same click sees event + candidate.
                                foreach (var nf in FactsFromJson(Json.Get(mO, "groups_add",
                                    (object)new List<object>())))
                                {
                                    factsNow.Add(nf);
                                    if (!pend.Contains(nf.GroupId)) pend.Add(nf.GroupId);
                                }
                            }
                        var viewW = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                        var selW = factsNow.Where(f => pend.Contains(f.GroupId)).ToList();
                        var pickW = ReactionOrder.SelectNext(round, pend, selW, catalog,
                            posture, skill, seed, viewW, counter, out lastWhy);
                        if (!pend.Contains(pickW))
                        { Fail(name + ".walk.step" + step, "pick outside pending: " + pickW); break; }
                        if (picks.Select(x => (string)x).Contains(pickW)) dup = true;
                        picks.Add(pickW);
                        pend.Remove(pickW);
                        factsNow.RemoveAll(f => f.GroupId == pickW);
                        step++;
                    }
                    // Permutation law (concept 2.4, App-given via
                    // GetNonExhaustedGroups + ExhaustGroup): the walk visits
                    // EXACTLY the pending set (initial + mid-round arrivals) -
                    // no duplicate, no skipper.
                    Check(name + ".walk_no_duplicate", false, dup);
                    var wWant = new List<string>(pending);
                    foreach (var mO in mid.Select(Json.AsDict))
                        foreach (var nf in Json.AsList(Json.Get(mO, "groups_add", (object)new List<object>())).Select(Json.AsDict).Select(d => Json.AsStr(d["id"])))
                            if (!wWant.Contains(nf)) wWant.Add(nf);
                    wWant.Sort(StringComparer.Ordinal);
                    var wGot = picks.Select(x => (string)x).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    Check(name + ".walk_multiset", string.Join(",", wWant), string.Join(",", wGot));

                    var got = new Dictionary<string, object>
                    {
                        { "pick", picks.Count > 0 ? picks[0] : "" },
                        { "basis", BasisDict(round, facts, catalog, posture, skill, seed, counter) },
                        { "walk", picks },
                    };
                    if (Record)
                    {
                        recorded[name] = got;
                        if (c.ContainsKey("expect"))
                        {
                            var exp0 = Json.AsDict(c["expect"]);
                            Check(name + ".recorded.pick", exp0["pick"], got["pick"]);
                            Check(name + ".recorded.basis", exp0["basis"], got["basis"]);
                            Check(name + ".recorded.walk", exp0["walk"], got["walk"]);
                        }
                        continue;
                    }
                    if (!c.ContainsKey("expect"))
                    { Check(name + ".expect", "present", "missing - run with --record"); continue; }
                    var expW = Json.AsDict(c["expect"]);
                    Check(name + ".pick", expW["pick"], got["pick"]);
                    Check(name + ".basis", expW["basis"], got["basis"]);
                    Check(name + ".walk", expW["walk"], got["walk"]);
                    continue;
                }

                // -- single-pick cases ------------------------------------------
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var prof = SkillProfile.For("S" + skill);
                string why1, why2;
                var pick1 = ReactionOrder.SelectNext(round, pending, facts, catalog,
                    posture, skill, seed, view, counter, out why1);
                // Determinism law (concept 2.4): same pending set, same seed,
                // same event stream -> identical pick AND identical account
                // line. Checked on EVERY case (the project view rebuilds
                // bit-identical, so does the pick).
                var pick2 = ReactionOrder.SelectNext(round, pending, facts, catalog,
                    posture, skill, seed,
                    WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash()),
                    counter, out why2);
                Check(name + ".determinism_pick", pick1, pick2);
                Check(name + ".determinism_why", why1, why2);
                Check(name + ".pick_in_pending", true, pending.Contains(pick1));

                var gotS = new Dictionary<string, object>
                {
                    { "pick", pick1 },
                    { "why", why1 },
                    { "basis", BasisDict(round, facts, catalog, posture, skill, seed, counter) },
                };

                // pattern_stability: the basis is the EnsureRound PlanRound
                // VERBATIM - same round-seed formula (M13u: the seeded misses
                // roll the same numbers), same call shape. Pinned against a
                // direct PlanRound call, not just a second internal run.
                if (Json.AsLong(Json.Get(c, "pattern_stability", (long)0)) == 1)
                {
                    var known = facts.Where(f => catalog.ContainsKey(f.CardId)).ToList();
                    var rngD = new DeterministicRandom(ReactionOrder.RoundSeed(seed, round));
                    var direct = OrderPlanner.PlanRound(round, known, catalog, posture, prof,
                                                        rngD, round, null, null, counter, null);
                    var viaBasis = ReactionOrder.BasisPlan(round, known, catalog, posture, prof, seed, counter);
                    Check(name + ".basis_eq_planround", direct.AsDict(), viaBasis.AsDict());
                }

                if (Record)
                {
                    recorded[name] = gotS;
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        Check(name + ".recorded.pick", exp0["pick"], gotS["pick"]);
                        Check(name + ".recorded.why", exp0["why"], gotS["why"]);
                        Check(name + ".recorded.basis", exp0["basis"], gotS["basis"]);
                    }
                    continue;
                }
                if (!c.ContainsKey("expect"))
                { Check(name + ".expect", "present", "missing - run with --record"); continue; }
                var exp = Json.AsDict(c["expect"]);
                Check(name + ".pick", exp["pick"], gotS["pick"]);
                Check(name + ".why", exp["why"], gotS["why"]);
                Check(name + ".basis", exp["basis"], gotS["basis"]);

                // finisher law of the palpatine case: finisher_last stays
                // PlanRound law - the OVERLAY overrules the pick, the BASIS
                // still shows the finisher slot last. Hand-pinned on the
                // fixture's own recorded basis (expect is ground truth).
                if (name.Contains("palpatine") && exp["basis"] is object)
                {
                    var bOrd = Json.AsList(Json.AsDict(exp["basis"])["order"]);
                    if (bOrd.Count > 0)
                        Check(name + ".basis_finisher_last", "PALP", Json.AsStr(bOrd[bOrd.Count - 1]));
                }
            }

            if (Record)
            {
                var path = System.IO.Path.Combine(FIX, "reaction_order_record.json");
                System.IO.File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }

            // ================= hand-pinned laws (ground truth) ================
            var cat = Catalog();

            // LAW 1 - class dominance (Thomas 04.11. #4): Schutzpflicht (R2)
            // beats Vergeltung (R1) EVEN WHEN the R1 event is newer - the
            // Punktklassen order never dissolves into seq comparison.
            var f1 = ReactionLawVector();
            var v1 = WorldEventVector.Project(f1.Events, "law", f1.Hash());
            var g1 = ReactionLawGroups();
            var p1 = new List<string> { "GUARD", "SHOT", "OTHER" };
            string w1;
            var pick1s = ReactionOrder.SelectNext(2, p1, g1, cat, "eliminate", 4, 42UL,
                                                  v1, null, out w1);
            Check("law.class_dominance_pick", "GUARD", pick1s);
            Check("law.class_dominance_push", true, w1.Contains("|R2:"));
            Check("law.class_dominance_points", true, w1.Contains("|R2:3")); // class*1000 block
            Check("law.class_dominance_reason", true, w1.Contains("Missionsziel-Interaktion durch Lokaku"));

            // LAW 2 - skill gate exactly: S2 reads R1+R3, never R2. Same
            // vector: the shot group leads, the guard anchor gets nothing.
            string w2;
            var pick2s = ReactionOrder.SelectNext(2, p1, g1, cat, "eliminate", 2, 42UL, v1, null, out w2);
            Check("law.skill2_pick", "SHOT", pick2s);
            Check("law.skill2_push_r1", true, w2.Contains("|R1:"));
            Check("law.skill2_no_r2", false, w2.Contains("|R2:"));

            // LAW 3 - S1 reads the basis only: no push, pick == basis head.
            string w3;
            var pick3s = ReactionOrder.SelectNext(2, p1, g1, cat, "eliminate", 1, 42UL, v1, null, out w3);
            Check("law.skill1_no_push", true, w3.Contains("|Basis:0|"));
            var head3 = FirstBasis(2, g1, cat, "eliminate", 1, 42UL, null);
            Check("law.skill1_basis_head", head3, pick3s);

            // LAW 4 - unknown cards: basis behind every known figure; inside
            // the unknown block priority==1 (public deployment knowledge)
            // jumps to the front. Pure basis walk at S1, no feed at all.
            var gU = new List<GroupFacts>
            {
                new GroupFacts("K1", 0, "DG021", "Known", "aggressive", 2, Rational.One),
                new GroupFacts("U1", 1, "CUSTOM_FAN", "Fan One", "aggressive", 2, Rational.One),
                new GroupFacts("U2", 2, "CUSTOM_OTHER", "Fan Two", "aggressive", 2, Rational.One,
                               null, null, false, false, true),   // priority == 1
            };
            var pendU = new List<string> { "K1", "U1", "U2" };
            var pendWalk = new List<string>(pendU);
            var factsWalk = new List<GroupFacts>(gU);
            var walkU = new List<string>();
            while (pendWalk.Count > 0)
            {
                string wU;
                var s = ReactionOrder.SelectNext(1, pendWalk, factsWalk, cat, "eliminate", 1, 7UL,
                                                 null, null, out wU);
                walkU.Add(s);
                pendWalk.Remove(s);
                factsWalk.RemoveAll(f => f.GroupId == s);
            }
            Check("law.unknown_block_order", "K1,U2,U1", string.Join(",", walkU));

            // LAW 5 - fail-loud: empty pending and factless candidates throw
            // (the fail-open catch lives in the BRIDGE, the Core never
            // silently invents a pick).
            bool threwEmpty = false, threwNoFact = false;
            try { string wE; ReactionOrder.SelectNext(1, new List<string>(), new List<GroupFacts>(),
                  cat, "eliminate", 3, 1UL, null, null, out wE); }
            catch (ArgumentException) { threwEmpty = true; }
            try { string wE; ReactionOrder.SelectNext(1, new List<string> { "GHOST" },
                  new List<GroupFacts>(), cat, "eliminate", 3, 1UL, null, null, out wE); }
            catch (ArgumentException) { threwNoFact = true; }
            Check("law.fail_loud_empty", true, threwEmpty);
            Check("law.fail_loud_missing_fact", true, threwNoFact);

            // LAW 6 - no feed at all (view == null): pure basis order, honest
            // "|Basis|" account, still a total deterministic order.
            string w6;
            var pick6 = ReactionOrder.SelectNext(2, p1, g1, cat, "eliminate", 4, 42UL, null, null, out w6);
            Check("law.no_feed_basis", true, w6.Contains("|Basis:0|"));
            Check("law.no_feed_basis_head", FirstBasis(2, g1, cat, "eliminate", 4, 42UL, null), pick6);

            // LAW 7 - R3 needs BOTH facts: window AND current round. A spawn
            // outside any window this round, and a window-spawn from an
            // EARLIER round, must both stay un-pushed.
            var f7 = new WorldEventFeed("law");
            f7.Append("MissionStart", 1, new Dictionary<string, object> {
                { "objective", "o" }, { "priorityTarget", "" }, { "roundLimit", -1L },
                { "mission", "law" } });
            f7.Append("HeroActivationStart", 1, P("hero", "Luke"));
            f7.Append("Spawn", 1, new Dictionary<string, object> { { "group", "OLD" }, { "size", 2L } });
            f7.Append("HeroActivated", 1, P("hero", "Luke"));   // OLD: window fact, wrong round
            f7.Append("Spawn", 2, new Dictionary<string, object> { { "group", "OUT" }, { "size", 2L } });
                                                          // OUT: right round, no window
            f7.Append("HeroActivated", 2, P("hero", "Han"));
            var v7 = WorldEventVector.Project(f7.Events, "law", f7.Hash());
            var g7 = new List<GroupFacts>
            {
                new GroupFacts("OLD", 0, "DG021", "Old arrival", "aggressive", 2, Rational.One),
                new GroupFacts("OUT", 1, "DG009", "Quiet arrival", "aggressive", 2, Rational.One),
            };
            string w7;
            ReactionOrder.SelectNext(2, new List<string> { "OLD", "OUT" }, g7, cat, "eliminate",
                                     4, 9UL, v7, null, out w7);
            Check("law.r3_needs_round_and_window", false, w7.Contains("|R3:"));

            // LAW 8 - multi-push figure shows its HIGHEST class only: a guard
            // that is ALSO shot this round reads R2 (stacking R1+R3 would
            // imitate R2 and dissolve the class order - never sums).
            var f8 = ReactionLawVector();   // same vector: GUARD has the R2
            f8.Append("HeroActivationStart", 2, P("hero", "Chewbacca"));
            f8.Append("GroupDamage", 2, new Dictionary<string, object> {
                { "group", "GUARD" }, { "figure", 0L }, { "wheel", 1L }, { "health", 4L } });
            f8.Append("HeroActivated", 2, P("hero", "Chewbacca"));
            var v8 = WorldEventVector.Project(f8.Events, "law", f8.Hash());
            string w8;
            var pick8 = ReactionOrder.SelectNext(2, p1, g1, cat, "eliminate", 4, 42UL, v8, null, out w8);
            Check("law.multipush_top_class_pick", "GUARD", pick8);
            // Winner segment only (the runner-up may legitimately show R1).
            var winSeg = w8.Split(new[] { " ; vor " }, StringSplitOptions.None)[0];
            Check("law.multipush_shows_r2", true, winSeg.Contains("|R2:"));
            Check("law.multipush_no_r1_row", false, winSeg.Contains("|R1:"));
        }

        // ---- helpers ---------------------------------------------------------

        static void AppendFeedEvents(WorldEventFeed feed, object eventsObj)
        {
            foreach (var eraw in Json.AsList(eventsObj))
            {
                var e = Json.AsDict(eraw);
                var payload = e.ContainsKey("payload")
                    ? new Dictionary<string, object>(Json.AsDict(e["payload"]))
                    : new Dictionary<string, object>();
                feed.Append(Json.AsStr(e["kind"]), (int)Json.AsLong(e["round"]), payload);
            }
        }

        static List<GroupFacts> FactsFromJson(object groupsObj)
        {
            var outl = new List<GroupFacts>();
            var rows = Json.AsList(groupsObj);
            for (int gi = 0; gi < rows.Count; gi++)
            {
                var g = Json.AsDict(rows[gi]);
                int? arrived = g.ContainsKey("arrived_round") && !(g["arrived_round"] is null)
                    ? (int?)Json.AsLong(g["arrived_round"]) : null;
                outl.Add(new GroupFacts(
                    Json.AsStr(g["id"]),
                    (int)Json.AsLong(Json.Get(g, "index", (long)gi)),
                    Json.AsStr(g["card"]),
                    Json.AsStr(g["name"]),
                    Json.AsStr(Json.Get(g, "pattern", (object)"aggressive")),
                    (int)Json.AsLong(Json.Get(g, "n_alive", (long)1)),
                    Rational.Parse(Json.AsStr(Json.Get(g, "vitality", (object)"1"))),
                    zone: null, arrivedRound: arrived,
                    isHeavy: Json.AsLong(Json.Get(g, "heavy", (long)0)) == 1,
                    isSwarm: Json.AsLong(Json.Get(g, "swarm", (long)0)) == 1,
                    priorityOne: Json.AsLong(Json.Get(g, "priority_one", (long)0)) == 1));
            }
            return outl;
        }

        // Basis dict of a case: PlanRound over the KNOWN facts with the exact
        // round-seed formula - the same basis the overlay ranks on.
        static Dictionary<string, object> BasisDict(int round, List<GroupFacts> facts,
            Dictionary<string, DeploymentCard> catalog, string posture, int skill,
            ulong seed, string counter)
        {
            var prof = SkillProfile.For("S" + skill);
            var known = facts.Where(f => catalog.ContainsKey(f.CardId)).ToList();
            var sb = new StringBuilder();
            if (known.Count == 0)
                return new Dictionary<string, object> { { "order", new List<object>() }, { "miss", "" } };
            var plan = ReactionOrder.BasisPlan(round, known, catalog, posture, prof, seed, counter);
            var order = new List<object>();
            foreach (var id in plan.GroupIds()) order.Add(id);
            return new Dictionary<string, object> { { "order", order }, { "miss", plan.MissNote } };
        }

        static string FirstBasis(int round, List<GroupFacts> facts,
            Dictionary<string, DeploymentCard> catalog, string posture, int skill,
            ulong seed, string counter)
        {
            var prof = SkillProfile.For("S" + skill);
            var known = facts.Where(f => catalog.ContainsKey(f.CardId)).ToList();
            var plan = ReactionOrder.BasisPlan(round, known, catalog, posture, prof, seed, counter);
            return plan.Slots.Count > 0 ? plan.Slots[0].GroupId : "";
        }

        // LAW vector: round 2, an objective change inside Lokaku's OPEN
        // window (-> R2 event, anchor seq) and a LATER GroupDamage on SHOT
        // inside Han's window (-> R1, newer seq). GUARD (pattern guard) gets
        // the older-event R2; class must still dominate.
        static WorldEventFeed ReactionLawVector()
        {
            var f = new WorldEventFeed("law");
            f.Append("MissionStart", 1, new Dictionary<string, object> {
                { "objective", "Reach the terminal." }, { "priorityTarget", "" },
                { "roundLimit", -1L }, { "mission", "law" } });
            f.Append("Spawn", 1, new Dictionary<string, object> { { "group", "GUARD" }, { "size", 2L } });
            f.Append("Spawn", 1, new Dictionary<string, object> { { "group", "SHOT" }, { "size", 2L } });
            f.Append("Spawn", 1, new Dictionary<string, object> { { "group", "OTHER" }, { "size", 2L } });
            f.Append("HeroActivationStart", 2, P("hero", "Lokaku"));
            f.Append("ObjectiveChange", 2, new Dictionary<string, object> {
                { "objective", "Secure the canteen." } });
            f.Append("HeroActivated", 2, P("hero", "Lokaku"));
            f.Append("HeroActivationStart", 2, P("hero", "Han"));
            f.Append("GroupDamage", 2, new Dictionary<string, object> {
                { "group", "SHOT" }, { "figure", 0L }, { "wheel", 1L }, { "health", 4L } });
            f.Append("HeroActivated", 2, P("hero", "Han"));
            return f;
        }

        static List<GroupFacts> ReactionLawGroups()
        {
            return new List<GroupFacts>
            {
                new GroupFacts("GUARD", 0, "DG001", "Guard", "guard", 2, Rational.One),
                new GroupFacts("SHOT", 1, "DG021", "Shooter", "aggressive", 2, Rational.One),
                new GroupFacts("OTHER", 2, "DG009", "Other", "support", 2, Rational.One),
            };
        }
    }
}
