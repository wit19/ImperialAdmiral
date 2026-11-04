// S-C section: section_trust.json pins the S-C mask-update rules on the
// S-B module - backwards victim ring (argmax of its OWN mask, distributed
// disjunctiv, Death deduction), the door transit hypothesis (hypothesis
// grade, table-owned doors), the EXPLAIN evidence chain (Ereignis+Regel,
// coordinate-free) and the coarse low-skill reading - on the same synthetic
// section missions as section_space.json. Events go through the REAL feed
// path; expect blocks were recorded ONCE via --record and reviewed line by
// line (record discipline); the laws that ARE ground truth (skill curve,
// trust table, ring basics, transit, EXPLAIN rule ids, calibration math,
// mission-table mining) are hand-pinned directly below.
//
// Canon accounting: NO pre-existing fixture is touched. The S-B default
// path is untouched code-wise (SectionOptions defaults to full refinement,
// no door owners), so section_space.json stays byte-identical - the whole
// run proves it.
//
// The shipped-data half of this hop (tactical_section_trust.json +
// tactical_mission_critical mining against REAL missions) is pinned by the
// SHIPPED-FILE gate SectionTrustGate (zero fixtures), per pipeline law.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSectionTrust()
        {
            var v = LoadJson("section_trust.json");
            var cases = Json.AsList(v["cases"]);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);

                var feed = new WorldEventFeed("vector|" + name);
                foreach (var eraw in Json.AsList(c["events"]))
                {
                    var e = Json.AsDict(eraw);
                    var payload = e.ContainsKey("payload")
                        ? new Dictionary<string, object>(Json.AsDict(e["payload"]))
                        : new Dictionary<string, object>();
                    feed.Append(Json.AsStr(e["kind"]), (int)Json.AsLong(e["round"]), payload);
                }

                var seeds = ReadSeeds(c);
                var edges = ReadEdges(c);
                var owners = ReadOwners(c);
                var opts = new SectionOptions { Refine = true, DoorOwners = owners.Count > 0 ? owners : null };

                // Vector canon: the S-C hop changes no vector law - replay
                // determinism still pins per case.
                var view1 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view1.Canon(), view2.Canon());

                var space = SectionSpace.Project(feed.Events, null, seeds, edges, opts);
                var spaceAgain = SectionSpace.Project(feed.Events, null, seeds, edges, opts);
                Check(name + ".replay_dump", space.Dump(), spaceAgain.Dump());

                var got = new Dictionary<string, object>
                {
                    { "dump", space.Dump() },
                    { "canon", view1.Canon() },
                    { "explain", new List<object>(space.ExplainTrail().ToArray()) },
                    { "ring", RingLines(space, c) },
                    { "transit", TransitLines(space, c) },
                    { "coarse", CoarseDump(feed, seeds, edges, owners) },
                };

                if (Record)
                {
                    recorded[name] = got;
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        foreach (var key in TrustExpectKeys)
                            if (exp0.ContainsKey(key)) Check(name + ".recorded." + key, exp0[key], got[key]);
                    }
                    continue;
                }
                if (!c.ContainsKey("expect"))
                {
                    Check(name + ".expect", "present", "missing - run with --record");
                    continue;
                }
                var exp = Json.AsDict(c["expect"]);
                foreach (var key in TrustExpectKeys)
                    if (exp.ContainsKey(key)) Check(name + "." + key, exp[key], got[key]);
            }

            // ================= hand-pinned laws (ground truth) ==============

            // ---- 1) SKILL CURVE (Thomas-Gesetz 04.11.): the three gated
            // channels open exactly at the middle rung S3 and never below;
            // profile absence (reference path) reads FALSE. No switches:
            // these functions are the whole mechanism.
            foreach (var level in new[] { "S1", "S2", "S3", "S4", "S5" })
            {
                var prof = SkillProfile.For(level);
                bool want = level == "S3" || level == "S4" || level == "S5";
                Check("law.skill_" + level + "_refine", want, SectionSkill.MaskRefinementEnabled(prof));
                Check("law.skill_" + level + "_clamp", want, SectionSkill.PostureClampBasisEnabled(prof));
                Check("law.skill_" + level + "_nav", want, SectionSkill.SupportNavigationEnabled(prof));
            }
            Check("law.no_profile_refine_off", false, SectionSkill.MaskRefinementEnabled(null));
            Check("law.no_profile_clamp_off", false, SectionSkill.PostureClampBasisEnabled(null));
            Check("law.no_profile_nav_off", false, SectionSkill.SupportNavigationEnabled(null));

            // ---- 2) TRUST TABLE: fail-closed without data; rational rows;
            // first valid skill match, default fallback; invalid rows absent;
            // diffuse (degraded) masks never trusted, boundary is >=.
            SectionTrust.Clear();
            Rational thrTmp;
            Check("law.trust_no_table_false", false, SectionTrust.TryThresholdFor(null, out thrTmp));
            var trustG = new SectionGraph();
            trustG.AddSection("s-a"); trustG.AddSection("s-b"); trustG.AddSection("s-c");
            var trustSharp = new SectionMask();
            trustSharp.AddObservation(1, "deploy", new[] { "s-a" }, "fact");
            Check("law.trust_no_table_key_off", false,
                  SectionTrust.Trusted(trustSharp, trustG, 1, 0, SkillProfile.For("S5")));

            SectionTrust.PrimeDoc(Json.Parse(
                "{\"rows\":[" +
                "{\"id\":\"s5\",\"skill\":\"S5\",\"num\":3,\"den\":4}," +
                "{\"id\":\"bad-big\",\"num\":4,\"den\":3}," +   // invalid: >1
                "{\"id\":\"bad-zero\",\"num\":0,\"den\":3}," + // invalid: 0
                "{\"id\":\"def\",\"num\":2,\"den\":3}]}"));
            Check("law.trust_default_row", "2/3",
                  SecTrustThreshold(null));
            Check("law.trust_s1_default", "2/3", SecTrustThreshold(SkillProfile.For("S1")));
            Check("law.trust_s5_own_row", "3/4", SecTrustThreshold(SkillProfile.For("S5")));
            Check("law.trust_s4_falls_back", "2/3", SecTrustThreshold(SkillProfile.For("S4")));
            var s5 = SkillProfile.For("S5");
            // sharp mask: dominance 1 >= any sane threshold -> trusted
            Check("law.trust_sharp", true, SectionTrust.Trusted(trustSharp, trustG, 1, 0, s5));
            // 3 candidates: 1/3 < 2/3 (and < 3/4) -> not trusted
            var trustWide = new SectionMask();
            trustWide.AddObservation(1, "deploy", new[] { "s-a", "s-b", "s-c" }, "fact");
            Check("law.trust_wide_not", false, SectionTrust.Trusted(trustWide, trustG, 1, 0, s5));
            // unobserved mask: nothing to trust
            Check("law.trust_empty_not", false,
                  SectionTrust.Trusted(new SectionMask(), trustG, 1, 0, s5));
            // boundary is inclusive AND degradation vetoes a passing score
            SectionTrust.PrimeDoc(Json.Parse("{\"rows\":[{\"id\":\"half\",\"num\":1,\"den\":2}]}"));
            var trustTwo = new SectionMask();
            trustTwo.AddObservation(1, "deploy", new[] { "s-a", "s-b" }, "fact");
            Check("law.trust_boundary_inclusive", true, SectionTrust.Trusted(trustTwo, trustG, 1, 0, s5));
            var trustDeg = new SectionMask();
            trustDeg.AddObservation(1, "deploy", new[] { "s-a" }, "fact");
            trustDeg.AddObservation(2, "hit", new[] { "s-b" }, "fact"); // disjoint -> union s-a,s-b degraded
            Check("law.trust_degraded_not", false, SectionTrust.Trusted(trustDeg, trustG, 1, 0, s5));
            SectionTrust.Clear();

            // ---- 3) BIT-IDENTITY at the low rung: a stream without any
            // refinement carrier (no hit, no wounded, no door owners)
            // projects BYTE-IDENTICAL with Refine=false (chain-entry state).
            var lowFeed = new WorldEventFeed("law|low_identical");
            AppendMission(lowFeed, "LAW1");
            AppendSpawn(lowFeed, "GUARD", 2, 1, "s-a");
            AppendSection(lowFeed, "s-a", "Alpha", 1);
            var lowOn = SectionSpace.Project(lowFeed.Events);
            var lowOff = SectionSpace.Project(lowFeed.Events, null, null, null,
                new SectionOptions { Refine = false });
            Check("law.low_tier_dump_identical", lowOn.Dump(), lowOff.Dump());
            Check("law.low_tier_explain_identical",
                  string.Join("\n", lowOn.ExplainTrail().ToArray()),
                  string.Join("\n", lowOff.ExplainTrail().ToArray()));
            // and WITH a hit the two readings DO differ: the hit re-grounds
            // only on the high rung (the coarse dump never carries src=hit)
            var hitFeed = new WorldEventFeed("law|hit_gated");
            AppendMission(hitFeed, "LAW2");
            AppendSpawn(hitFeed, "GUARD", 1, 1, "s-a");
            AppendSection(hitFeed, "s-a", "Alpha", 1);
            AppendHit(hitFeed, "GUARD", 0, 2);
            var hitOn = SectionSpace.Project(hitFeed.Events);
            var hitOff = SectionSpace.Project(hitFeed.Events, null, null, null,
                new SectionOptions { Refine = false });
            Check("law.refined_carries_hit", true, hitOn.Dump().Contains("src=hit"));
            Check("law.coarse_drops_hit", false, hitOff.Dump().Contains("src=hit"));
            // wounded disjunctive is refinement too: low rung stays clean
            var wdFeed = new WorldEventFeed("law|wounded_gated");
            AppendMission(wdFeed, "LAW3");
            AppendSpawn(wdFeed, "GUARD", 1, 1, "s-a");
            wdFeed.Append("ActivationStart", 2, Dict("group", "GUARD"));
            wdFeed.Append("HeroWounded", 2, Dict("hero", "LEIA"));
            var wdOff = SectionSpace.Project(wdFeed.Events, null, null, null,
                new SectionOptions { Refine = false });
            Check("law.coarse_wounded_stays_no", false, wdOff.BeliefOf("GUARD").WoundedDisjunctiveObserved);
            var wdOn = SectionSpace.Project(wdFeed.Events);
            Check("law.refined_wounded_yes", true, wdOn.BeliefOf("GUARD").WoundedDisjunctiveObserved);

            // ---- 4) BACKWARDS RING basics (fixture cases pin the exact
            // strong/weak lines; these pin the invariant laws).
            Check("law.ring_no_belief_null", null, hitOn.VictimRingOf("NOPE", 0));
            var unFed = new WorldEventFeed("law|ring_unseeded");
            AppendMission(unFed, "LAW4");
            AppendSpawn(unFed, "GUARD", 1, 1, null);
            var unSp = SectionSpace.Project(unFed.Events);
            var unRing = unSp.VictimRingOf("GUARD", 0);
            Check("law.ring_unseeded_basis", "unseeded", unRing.Basis);
            Check("law.ring_unseeded_strong_empty", "", string.Join(",", unRing.Strong.ToArray()));
            Check("law.ring_unseeded_weak_empty", "", string.Join(",", unRing.Weak.ToArray()));
            // Death deduction: one survivor, then wiped.
            var dFeed = new WorldEventFeed("law|ring_death");
            AppendMission(dFeed, "LAW5");
            AppendSpawn(dFeed, "GUARD", 2, 1, "s-a");
            AppendSection(dFeed, "s-a", "Alpha", 1);
            var dSpace = SectionSpace.Project(dFeed.Events);
            Check("law.ring_pool_alive", "pool=2", dSpace.VictimRingOf("GUARD", 0).Dump().Split(' ')[4]);
            dFeed.Append("Death", 2, Dict("group", "GUARD"));
            dFeed.Append("Death", 2, Dict("group", "GUARD"));
            dSpace = SectionSpace.Project(dFeed.Events);
            var wiped = dSpace.VictimRingOf("GUARD", 0);
            Check("law.ring_wiped_basis", "wiped", wiped.Basis);
            Check("law.ring_wiped_pool0", 0L, (long)wiped.PoolSize);
            Check("law.ring_wiped_empty_strong", "", string.Join(",", wiped.Strong.ToArray()));
            // Grundgesetz mirror: every ring name is a KNOWN section and the
            // ring never excludes (weak subset of the graph, nothing else).
            var ringCase = hitOn.VictimRingOf("GUARD", 0);
            bool allKnown = true;
            foreach (var s in ringCase.Strong) if (!hitOn.Graph.HasSection(s)) allKnown = false;
            foreach (var s in ringCase.Weak) if (!hitOn.Graph.HasSection(s)) allKnown = false;
            Check("law.ring_only_known_sections", true, allKnown);
            // Exceptions are NOT gated: a Wachzone fires on the COARSE
            // reading exactly like on the refined one (Fakt-Reaktion ist
            // Fairness, kein Intelligenz-Bonus).
            var wzFeed = new WorldEventFeed("law|wachzone_coarse");
            AppendMission(wzFeed, "LAW6");
            AppendSpawn(wzFeed, "GUARD", 1, 1, "s-array");
            AppendSection(wzFeed, "s-array", "Array Room", 1);
            wzFeed.Append("HeroActivationStart", 2, Dict("hero", "LEIA"));
            wzFeed.Append("EntityInteract", 2, Dict3("entity", "t-array", "name", "Comms Array",
                                                    "section", "s-array"));
            wzFeed.Append("HeroActivated", 2, Dict("hero", "LEIA"));
            var crit = new[] { "s-array" };
            var guard = new[] { "GUARD" };
            var wzCoarse = SectionSpace.Project(wzFeed.Events, null, null, null,
                new SectionOptions { Refine = false });
            var wzRefined = SectionSpace.Project(wzFeed.Events);
            Check("law.wachzone_fires_coarse", 1L, (long)wzCoarse.WachzoneFacts(crit, guard).Count);
            Check("law.wachzone_fires_refined", 1L, (long)wzRefined.WachzoneFacts(crit, guard).Count);
            // The victim ring is ungated too: coarse projection still rings.
            var coarseRing = wzCoarse.VictimRingOf("GUARD", 0);
            Check("law.ring_ungated", "argmax", coarseRing.Basis);

            // ---- 5) TRANSIT HYPOTHESIS: door flip inside an open hero
            // window whose owning section the table names -> hypothesis on
            // the hero track; every missing leg (no owner, outside the
            // window, low rung) stays silent; the fact rail is never moved.
            var trFeed = new WorldEventFeed("law|transit");
            AppendMission(trFeed, "LAW7");
            AppendSpawn(trFeed, "GUARD", 1, 1, "s-a");
            AppendSection(trFeed, "s-a", "Corridor", 1);
            trFeed.Append("HeroActivationStart", 2, Dict("hero", "LEIA"));
            trFeed.Append("DoorOpen", 2, Dict2("door", "d-own", "name", "Catwalk Door"));
            trFeed.Append("HeroActivated", 2, Dict("hero", "LEIA"));
            var ownersMap = new Dictionary<string, string>(StringComparer.Ordinal) { { "d-own", "s-cat" } };
            var trOn = SectionSpace.Project(trFeed.Events, null, null, null,
                new SectionOptions { Refine = true, DoorOwners = ownersMap });
            Check("law.transit_dump", true,
                  trOn.Dump().Contains("transit LEIA d-own section=s-cat round=2"));
            Check("law.transit_sections", "s-cat", string.Join(",", trOn.HeroTransitSections("LEIA").ToArray()));
            bool trailHasTransit = false;
            foreach (var l in trOn.ExplainTrail())
                if (l.Contains("rule=transit-hypothese") && l.Contains("door=d-own")) trailHasTransit = true;
            Check("law.transit_explain", true, trailHasTransit);
            // fact rail untouched: LEIA has no fact anchor (the door carries
            // no section payload), only the hypothesis exists.
            Check("law.transit_fact_rail_clean", "", trOn.HeroInSection("LEIA"));
            var trCoarse = SectionSpace.Project(trFeed.Events, null, null, null,
                new SectionOptions { Refine = false, DoorOwners = ownersMap });
            Check("law.transit_coarse_off", false, trCoarse.Dump().Contains("transit "));
            Check("law.transit_coarse_sections_empty", "", string.Join(",", trCoarse.HeroTransitSections("LEIA").ToArray()));
            var trNoOwner = SectionSpace.Project(trFeed.Events);
            Check("law.transit_without_table_off", false, trNoOwner.Dump().Contains("transit "));
            var trOutside = new WorldEventFeed("law|transit_outside");
            AppendMission(trOutside, "LAW8");
            trOutside.Append("DoorOpen", 2, Dict2("door", "d-own", "name", "Catwalk Door"));
            var trOut = SectionSpace.Project(trOutside.Events, null, null, null,
                new SectionOptions { Refine = true, DoorOwners = ownersMap });
            Check("law.transit_needs_window", false, trOut.Dump().Contains("transit "));

            // ---- 6) EXPLAIN chain: exact hand-written trail for the seed
            // world + rule ids for hit/wounded + the Loch-Gesetz scan (no
            // coordinate token ever appears in any trail line).
            var eSpace = SectionSpace.Project(lowFeed.Events);
            Check("law.explain_seed_trail",
                  "explain space round=1 speed=1 sections=1\n" +
                  "explain group GUARD alive=2 fallen=0 anonVictim=no woundedDisjunctive=no\n" +
                  "explain obs group=GUARD fig=0 round=1 src=deploy rule=saat-exakt secs=s-a(Alpha)\n" +
                  "explain mask group=GUARD fig=0 cands=s-a(Alpha) dom=1\n" +
                  "explain obs group=GUARD fig=1 round=1 src=deploy rule=saat-exakt secs=s-a(Alpha)\n" +
                  "explain mask group=GUARD fig=1 cands=s-a(Alpha) dom=1",
                  string.Join("\n", eSpace.ExplainTrail().ToArray()));
            bool hitRule = false;
            foreach (var l in hitOn.ExplainTrail()) if (l.Contains("rule=eichung-hit-reground")) hitRule = true;
            Check("law.explain_hit_rule", true, hitRule);
            bool woundedRule = false;
            foreach (var l in wdOn.ExplainTrail()) if (l.Contains("rule=vergeltung-gruppen-disjunktiv")) woundedRule = true;
            Check("law.explain_wounded_rule", true, woundedRule);
            var candFeed = new WorldEventFeed("law|seed_candidates");
            AppendMission(candFeed, "LAW9");
            candFeed.Append("Spawn", 1, Dict2("group", "GUARD", "size", 1L));
            candFeed.Append("SectionActivated", 1, Dict2("section", "s-a", "name", "Alpha"));
            candFeed.Append("SectionActivated", 1, Dict2("section", "s-b", "name", "Beta"));
            var candSeed = new Dictionary<string, string[]>(StringComparer.Ordinal) { { "GUARD", new[] { "s-a", "s-b" } } };
            var candSp = SectionSpace.Project(candFeed.Events, null, candSeed, null);
            bool candRule = false;
            foreach (var l in candSp.ExplainTrail()) if (l.Contains("rule=saat-kandidatenmenge")) candRule = true;
            Check("law.explain_candidates_rule", true, candRule);
            // Loch-Gesetz scan: no coordinate token in any trail line of any
            // law world built above (dump too: same producer, same law).
            bool coordFree = true;
            foreach (var l in trOn.ExplainTrail())
                if (l.Contains(" x=") || l.Contains(" y=") || l.Contains("cell") || l.Contains("coord"))
                    coordFree = false;
            Check("law.explain_coord_free", true, coordFree);

            // ---- 7) RESYNC LAW: the feed re-log buries hypotheses and
            // keeps facts (full replay per computation, never continuation).
            var rsFeed = new WorldEventFeed("law|resync");
            AppendMission(rsFeed, "LAW10");
            AppendSection(rsFeed, "s-array", "Array Room", 1);
            AppendSpawn(rsFeed, "GUARD", 1, 1, "s-array");
            rsFeed.Append("HeroActivationStart", 2, Dict("hero", "LEIA"));
            rsFeed.Append("DoorOpen", 2, Dict2("door", "d-own", "name", "Catwalk Door"));
            var rsBefore = SectionSpace.Project(rsFeed.Events, null, null, null,
                new SectionOptions { Refine = true, DoorOwners = ownersMap });
            Check("law.resync_before_hypothesis", true, rsBefore.Dump().Contains("transit "));
            // Re-log WITHOUT the door event (the App state says it never
            // happened): re-projected from the re-logged history, the
            // hypothesis is buried, the SectionActivated fact stays.
            var relog = new List<WorldEvent>();
            foreach (var e in rsFeed.Events) if (e.Kind != "DoorOpen") relog.Add(e);
            rsFeed.Resync(relog);
            var rsAfter = SectionSpace.Project(rsFeed.Events, null, null, null,
                new SectionOptions { Refine = true, DoorOwners = ownersMap });
            Check("law.resync_hypothesis_buried", false, rsAfter.Dump().Contains("transit "));
            Check("law.resync_fact_remains", true, rsAfter.Dump().Contains("sec s-array name=Array Room"));

            // ---- 8) CALIBRATION (audit channel): hand-computed oracle over
            // a seeded world: rounds 1 and 2 dilate by 1 step; hitrate and
            // tightness are exact rationals, empty batch reports 0.
            var calG = new SectionGraph();
            calG.AddSection("s-a"); calG.AddSection("s-b"); calG.AddSection("s-c");
            calG.AddDoorEdge("s-a", "s-b", "d-1");
            var calSpace = SectionSpace.Project(new List<WorldEvent>());
            // build a small real space instead (graph+belief from events):
            var calFeed = new WorldEventFeed("law|calibration");
            AppendMission(calFeed, "LAW11");
            AppendSpawn(calFeed, "GUARD", 1, 1, "s-a");
            AppendSection(calFeed, "s-a", "Alpha", 1);
            AppendSection(calFeed, "s-b", "Beta", 1);
            var calEdges = new List<string[]> { new[] { "s-a", "s-b", "d-1" } };
            var calSp = SectionSpace.Project(calFeed.Events, null, null, calEdges);
            var calReport = SectionCalibration.Report("coarse", calSp, new[]
            {
                new CalibrationRow { Group = "GUARD", Figure = 0, Round = 1, Section = "s-a" },
                new CalibrationRow { Group = "GUARD", Figure = 0, Round = 2, Section = "s-b" },
                new CalibrationRow { Group = "GUARD", Figure = 0, Round = 2, Section = "s-c" },
            });
            Check("law.calib_report",
                  "calib coarse row group=GUARD fig=0 round=1 cands=1 hit=yes\n" +
                  "calib coarse row group=GUARD fig=0 round=2 cands=2 hit=yes\n" +
                  "calib coarse row group=GUARD fig=0 round=2 cands=2 hit=no\n" +
                  "calib coarse summary n=3 hits=2 hitrate=2/3 tightness=2/3",
                  string.Join("\n", calReport.ToArray()));
            var calEmpty = SectionCalibration.Report("none", calSp, new CalibrationRow[0]);
            Check("law.calib_empty", "calib none summary n=0 hits=0 hitrate=0 tightness=0",
                  string.Join("\n", calEmpty.ToArray()));

            // ---- 9) MISSION TABLE MINING (synthetic doc; the shipped-file
            // half is pinned by SectionTrustGate against the real asset).
            var mineDoc = Json.Parse(
                "{\"missions\":[" +
                "{\"file\":\"MINE1.json\"," +
                "\"criticalEntities\":[" +
                "{\"entity\":\"d-1\",\"entityType\":\"Door\",\"section\":\"s-a\",\"runtimeSwapped\":false}," +
                "{\"entity\":\"d-2\",\"entityType\":\"Door\",\"section\":\"s-x\",\"runtimeSwapped\":true}," +
                "{\"entity\":\"t-9\",\"entityType\":\"Terminal\",\"section\":\"s-a\",\"runtimeSwapped\":false}]," +
                "\"sectionHypotheses\":[" +
                "{\"section\":\"s-b\",\"entities\":[{\"entity\":\"d-1\",\"entityType\":\"Door\"}," +
                "{\"entity\":\"t-9\",\"entityType\":\"Terminal\"}]}," +
                "{\"section\":\"\",\"entities\":[{\"entity\":\"d-1\",\"entityType\":\"Door\"}]}," +
                "{\"section\":\"s-a\",\"entities\":[{\"entity\":\"d-1\",\"entityType\":\"Door\"}]}," +
                "{\"section\":\"s-c\",\"entities\":[{\"entity\":\"d-2\",\"entityType\":\"Door\"}," +
                "{\"entity\":\"d-unknown\",\"entityType\":\"Door\"}]}," +
                "{\"section\":\"s-b\",\"entities\":[{\"entity\":\"d-1\",\"entityType\":\"Door\"}]}]}," +
                "{\"file\":\"MINE2.json\",\"criticalEntities\":[],\"sectionHypotheses\":[]}]}");
            var ownersMined = SectionTrust.DoorOwnersOf(mineDoc, "MINE1");
            Check("law.mine_owners_count", 1L, (long)ownersMined.Count);
            Check("law.mine_owner_d1", "s-a", ownersMined.ContainsKey("d-1") ? ownersMined["d-1"] : "-");
            Check("law.mine_owner_swapped_skipped", false, ownersMined.ContainsKey("d-2"));
            Check("law.mine_owner_non_door_skipped", false, ownersMined.ContainsKey("t-9"));
            var edgesMined = SectionTrust.MissionDoorEdges(mineDoc, "MINE1");
            Check("law.mine_edges_dedup", 1L, (long)edgesMined.Count);
            Check("law.mine_edge_row", "s-a|s-b|d-1",
                  edgesMined.Count == 1 ? string.Join("|", edgesMined[0]) : "-");
            Check("law.mine_unknown_mission", 0L, (long)SectionTrust.MissionDoorEdges(mineDoc, "NOPE").Count);
            Check("law.mine_unknown_mission_owners", 0L, (long)SectionTrust.DoorOwnersOf(mineDoc, "NOPE").Count);

            if (Record)
            {
                var path = Path.Combine(FIX, "section_trust_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }

        static readonly string[] TrustExpectKeys = { "dump", "canon", "explain", "ring", "transit", "coarse" };

        static string SecTrustThreshold(SkillProfile p)
        {
            Rational t;
            return SectionTrust.TryThresholdFor(p, out t) ? t.ToString() : "-";
        }

        static Dictionary<string, object> Dict(string k1, object v1)
        {
            var d = new Dictionary<string, object>(); d[k1] = v1; return d;
        }

        static Dictionary<string, object> Dict2(string k1, object v1, string k2, object v2)
        {
            var d = new Dictionary<string, object>(); d[k1] = v1; d[k2] = v2; return d;
        }

        static Dictionary<string, object> Dict3(string k1, object v1, string k2, object v2,
                                               string k3, object v3)
        {
            var d = new Dictionary<string, object>(); d[k1] = v1; d[k2] = v2; d[k3] = v3; return d;
        }

        static void AppendMission(WorldEventFeed f, string tag)
        {
            f.Append("MissionStart", 1, new Dictionary<string, object>
            {
                { "objective", "Pin the law." }, { "priorityTarget", "REBEL" },
                { "roundLimit", 6L }, { "mission", tag }
            });
        }

        static void AppendSpawn(WorldEventFeed f, string group, long size, int round, string section)
        {
            var p = new Dictionary<string, object> { { "group", group }, { "size", size } };
            if (!string.IsNullOrEmpty(section)) p["section"] = section;
            f.Append("Spawn", round, p);
        }

        static void AppendSection(WorldEventFeed f, string sec, string name, int round)
        {
            f.Append("SectionActivated", round, Dict2("section", sec, "name", name));
        }

        static void AppendHit(WorldEventFeed f, string group, long figure, int round)
        {
            f.Append("GroupDamage", round, new Dictionary<string, object>
            {
                { "group", group }, { "figure", figure }, { "wheel", 2L }, { "health", 5L }
            });
        }

        static Dictionary<string, string> ReadOwners(Dictionary<string, object> c)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!c.ContainsKey("doorOwners")) return map;
            foreach (var kv in Json.AsDict(c["doorOwners"])) map[kv.Key] = Json.AsStr(kv.Value);
            return map;
        }

        static List<object> RingLines(SectionSpace space, Dictionary<string, object> c)
        {
            var lines = new List<object>();
            if (!c.ContainsKey("rings")) return lines;
            foreach (var raw in Json.AsList(c["rings"]))
            {
                var row = Json.AsList(raw);
                var ring = space.VictimRingOf(Json.AsStr(row[0]), (int)Json.AsLong(row[1]));
                lines.Add(ring == null ? "null" : ring.Dump());
            }
            return lines;
        }

        static List<object> TransitLines(SectionSpace space, Dictionary<string, object> c)
        {
            var lines = new List<object>();
            if (!c.ContainsKey("heroTransits")) return lines;
            foreach (var raw in Json.AsList(c["heroTransits"]))
                lines.Add(string.Join(",", space.HeroTransitSections(Json.AsStr(raw)).ToArray()));
            return lines;
        }

        // The low-rung reading of the same case (Thomas-Gesetz curve):
        // Refine=false, the door-owner table still supplied (it is DATA -
        // the low rung simply does not spin hypotheses from it).
        static string CoarseDump(WorldEventFeed feed, Dictionary<string, string[]> seeds,
                                 List<string[]> edges, Dictionary<string, string> owners)
        {
            var sp = SectionSpace.Project(feed.Events, null, seeds, edges,
                new SectionOptions { Refine = false, DoorOwners = owners.Count > 0 ? owners : null });
            return sp.Dump();
        }
    }
}
