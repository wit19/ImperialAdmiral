// S-D section: section_consumers.json pins the CONSUMER layer over the
// belief rail (card t_c52dd34b, concept positionierung v1 8.1/10/12/13):
// the Wachzone exception (held law, seed-from-the-start, Bedrohung order,
// IMMER on every rung), the 3c same-round rule, the mask-track Vergeltung
// (facts beat suspicion, ambiguity keeps the key off), the trust-gated
// proximity feed (ringZones/contestZone fire for the first time), the
// posture clamp and the pinned comparison slot law Interrupt > Wachzone >
// Vergeltung - plus the NULL LAW pair (same stream with space=null runs
// the pre-S-D order bit-identical).
//
// Events go through the REAL feed path (WorldEventFeed + WorldEventVector
// + SectionSpace.Project), the catalog cards go through DeploymentCard.
// FromRecord and the order through ReactionOrder.SelectNext end-to-end.
// The opaque blocks (canon, space dump, consumer readouts, why-accounts)
// were recorded ONCE via --record and reviewed line by line (record
// discipline); the LAWS that are ground truth - rung-1 exception firing,
// the released duty, the Bedrohung order, the same-round law, the verg
// tracks, the zone claims, jitter=0, the slot order and the null pairs -
// are hand-pinned against the computed values below, never recorded.
//
// Canon accounting: NO pre-existing fixture is touched. Every new
// parameter on the Core entry points is null-defaulted, so every
// pre-S-D vector runs the pre-S-D path; section_consumers.json is the
// only new file in the fixture dir and the full run proves the canon
// counts stay zahlgleich.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSectionConsumers()
        {
            var v = LoadJson("section_consumers.json");
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
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());

                // Trust table per case: PrimeDoc or explicit Clear - never
                // whatever the previous section left behind (a zone belief
                // is only as honest as the row that earned it).
                SectionTrust.Clear();
                if (c.ContainsKey("trust")) SectionTrust.PrimeDoc(c["trust"]);

                var seeds = ReadSeeds(c);
                var edges = ReadEdges(c);
                var opts = new SectionOptions { Refine = true };
                var space = SectionSpace.Project(feed.Events, null, seeds, edges, opts);

                var crit = new List<string>();
                if (c.ContainsKey("critical"))
                    foreach (var s in Json.AsList(c["critical"])) crit.Add(Json.AsStr(s));
                var catalog = new Dictionary<string, DeploymentCard>(StringComparer.Ordinal);
                if (c.ContainsKey("cards"))
                    foreach (var raw2 in Json.AsList(c["cards"]))
                    {
                        var card = DeploymentCard.FromRecord(Json.AsDict(raw2));
                        catalog[card.Id] = card;
                    }
                var facts = ReadSdFacts(c);
                var factById = new Dictionary<string, GroupFacts>(StringComparer.Ordinal);
                foreach (var f in facts) factById[f.GroupId] = f;

                var ro = new List<string>();

                // -- 1) Wachzone decisions + the Rank key ------------------
                if (c.ContainsKey("watchProbe"))
                {
                    var decisions = SectionConsumers.WachzoneDecisions(space, crit, facts, catalog);
                    foreach (var raw2 in Json.AsList(c["watchProbe"]))
                    {
                        var g = Json.AsStr(raw2);
                        WachzoneDecision d;
                        if (decisions.TryGetValue(g, out d))
                            ro.Add("watch." + g + "=" + d.Hero + "|" + d.SectionName + "|r" + d.Round +
                                   "|thr" + d.Threat + "|" + (d.ThreatKnown ? "known" : "unknown"));
                        else ro.Add("watch." + g + "=-");
                        var hk = SectionConsumers.WachzoneHeroFor(
                            space, crit, factById.ContainsKey(g) ? factById[g] : null, catalog);
                        ro.Add("wachHeroFor." + g + "=" + (hk ?? "-"));
                    }
                }

                // -- 2) Vergeltung (mask track, facts beat suspicion) ------
                if (c.ContainsKey("vergProbe"))
                    foreach (var raw2 in Json.AsList(c["vergProbe"]))
                    {
                        var row = Json.AsList(raw2);
                        var victim = Json.AsStr(row[0]);
                        var react = row.Count > 1 && row[1] != null ? Json.AsStr(row[1]) : null;
                        var h = SectionConsumers.VergeltungHeroFor(view, space, victim, react);
                        ro.Add("verg." + victim + "." + (react ?? "-") + "=" + (h ?? "-"));
                    }

                // -- 3) 3c -------------------------------------------------
                foreach (var f in space.ThreeCFacts())
                    ro.Add("3cf." + f.Group + "|" + f.Section + "|" + f.Hero + "|r" + f.Round);
                if (c.ContainsKey("threeNow") && c.ContainsKey("select"))
                {
                    var now = (int)Json.AsLong(c["threeNow"]);
                    var pend = new List<string>();
                    foreach (var s in Json.AsList(Json.AsDict(c["select"])["pending"])) pend.Add(Json.AsStr(s));
                    var pushes = SectionConsumers.ThreeCPushes(space, pend, now);
                    if (pushes.Count == 0) ro.Add("3cnow." + now + "=-");
                    else
                    {
                        var ks = new List<string>(pushes.Keys); ks.Sort(StringComparer.Ordinal);
                        foreach (var g in ks)
                            ro.Add("3cnow." + now + "." + g + "|" + pushes[g].Section +
                                   "|" + pushes[g].Hero + "|r" + pushes[g].Round);
                    }
                }

                // -- 4) proximity feed (zones / ring / contest) ------------
                if (c.ContainsKey("zones"))
                {
                    var prof = SkillProfile.For("S5");
                    var z3 = SectionConsumers.PlanZones(space, view, prof);
                    var z4 = SectionConsumers.PlanZones(space, view, prof, crit);
                    ro.Add("zones3[" + (z3 == null ? "null" : z3.Dump().TrimEnd('\n').Replace("\n", " ; ")) + "]");
                    ro.Add("zones4[" + (z4 == null ? "null" : z4.Dump().TrimEnd('\n').Replace("\n", " ; ")) + "]");
                    // Trust GATE at the consumer: without the table row the
                    // whole proximity feed must fall silent (fail-closed).
                    SectionTrust.Clear();
                    var zNo = SectionConsumers.PlanZones(space, view, prof, crit);
                    ro.Add("zonesNoTrust[" + (zNo == null ? "null" : zNo.Dump().TrimEnd('\n').Replace("\n", " ; ")) + "]");
                    if (c.ContainsKey("trust")) SectionTrust.PrimeDoc(c["trust"]);
                }

                // -- 5) posture clamp at the final state (+ purity pull) ---
                if (c.ContainsKey("clampPostures"))
                {
                    foreach (var raw2 in Json.AsList(c["clampPostures"]))
                    {
                        var p = Json.AsStr(raw2);
                        ro.Add("clamp." + p + "=" + (SectionConsumers.PostureClamp(space, crit, p) ?? "-"));
                    }
                    bool pure = true;
                    var first = SectionConsumers.PostureClamp(space, crit, "eliminate");
                    for (int i = 0; i < 3; i++)
                        if (!string.Equals(SectionConsumers.PostureClamp(space, crit, "eliminate") ?? "-",
                                           first ?? "-")) pure = false;
                    ro.Add("clamp.pure=" + (pure ? "yes" : "NO"));
                }

                // -- 6) R-C end-to-end picks (with and without the rail) ---
                if (c.ContainsKey("select"))
                {
                    var sel = Json.AsDict(c["select"]);
                    List<string> whys;
                    var picks = RunSdSelect(space, crit, catalog, facts, view, sel, out whys);
                    ro.Add("select.picks=" + picks);
                    for (int i = 0; i < whys.Count; i++) ro.Add("select.why" + (i + 1) + "=" + whys[i]);

                    // hand-pinned laws on named cases (ground truth, never
                    // recorded): the exceptions fire on EVERY rung and the
                    // null pair runs the pre-S-D order.
                    if (name == "sd_wachzone_rung1")
                    {
                        Check("law.wz_rung1_space", "GUARD,HUNTER",
                              Find(ro, "select.picks=").Substring("select.picks=".Length));
                        var s4 = new Dictionary<string, object>(sel);
                        s4["rung"] = 4L;
                        List<string> w4;
                        Check("law.wz_rung4_space", "GUARD,HUNTER",
                              RunSdSelect(space, crit, catalog, facts, view, s4, out w4));
                    }
                }
                foreach (var blockName in new[] { "select2", "selectRound4", "selectRung4" })
                {
                    if (!c.ContainsKey(blockName)) continue;
                    var sel = Json.AsDict(c[blockName]);
                    List<string> whys;
                    ro.Add(blockName + ".picks=" + RunSdSelect(space, crit, catalog, facts, view, sel, out whys));
                    for (int i = 0; i < whys.Count; i++) ro.Add(blockName + ".why" + (i + 1) + "=" + whys[i]);
                }
                if (c.ContainsKey("selectNoSpace"))
                {
                    var sel = Json.AsDict(c["selectNoSpace"]);
                    List<string> whys;
                    ro.Add("selectNoSpace.picks=" + RunSdSelect(null, null, catalog, facts, view, sel, out whys));
                    for (int i = 0; i < whys.Count; i++) ro.Add("selectNoSpace.why" + (i + 1) + "=" + whys[i]);
                    if (name == "sd_wachzone_rung1")
                        Check("law.wz_rung1_null_canon", "HUNTER,GUARD", Find(ro, "selectNoSpace.picks=").Substring("selectNoSpace.picks=".Length));
                    if (name == "sd_wachzone_bedrohung")
                        Check("law.bedrohung_null_index_law", "HUNTER,GUARDW,GUARDS,GUARDX",
                              Find(ro, "selectNoSpace.picks=").Substring("selectNoSpace.picks=".Length));
                }

                var got = new Dictionary<string, object>
                {
                    { "canon", view.Canon() },
                    { "dump", space.Dump() },
                    { "readout", string.Join("\n", ro.ToArray()) },
                };

                if (Record)
                {
                    recorded[name] = got;
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        foreach (var key in SdExpectKeys)
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
                foreach (var key in SdExpectKeys)
                    if (exp.ContainsKey(key)) Check(name + "." + key, exp[key], got[key]);

                // replay determinism per case (same law as every S section)
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view.Canon(), view2.Canon());
                var space2 = SectionSpace.Project(feed.Events, null, seeds, edges, opts);
                Check(name + ".replay_dump", space.Dump(), space2.Dump());

                // named-case ground-truth on the watch rail itself
                if (name == "sd_wachzone_held")
                    Check("law.wz_seed_from_start", true, ro[IndexOf(ro, "watch.GUARD=")].StartsWith("watch.GUARD=LU|"));
                if (name == "sd_wachzone_released")
                    Check("law.wz_held_released", "watch.GUARD=-", Find(ro, "watch.GUARD="));
                if (name == "sd_wachzone_bedrohung")
                {
                    Check("law.bedrohung_order", "GUARDS,GUARDW,GUARDX,HUNTER",
                          Find(ro, "select.picks=").Substring("select.picks=".Length));
                    Check("law.bedrohung_rung4", "GUARDS,GUARDW,GUARDX,HUNTER",
                          Find(ro, "selectRung4.picks=").Substring("selectRung4.picks=".Length));
                }
                if (name == "sd_drei_c_gleich_runde")
                {
                    Check("law.3c_rung1_first", "RECRUIT,HUNTER",
                          Find(ro, "select.picks=").Substring("select.picks=".Length));
                    Check("law.3c_rung2_first", "RECRUIT,HUNTER",
                          Find(ro, "select2.picks=").Substring("select2.picks=".Length));
                    if (c.ContainsKey("selectRound4"))
                        Check("law.3c_next_round_gone", "HUNTER,RECRUIT",
                              Find(ro, "selectRound4.picks=").Substring("selectRound4.picks=".Length));
                }
                if (name == "sd_vergeltung_mask_track")
                {
                    Check("law.verg_mask_candidate", "verg.GUARD.-=JY", Find(ro, "verg.GUARD.-="));
                    Check("law.verg_fact_beats_mask", "verg.GUARD.LU=-", Find(ro, "verg.GUARD.LU="));
                    Check("law.verg_no_belief", "verg.SCOUT.-=-", Find(ro, "verg.SCOUT.-="));
                }
                if (name == "sd_vergeltung_ambiguous")
                    Check("law.verg_ambiguous_off", "verg.GUARD.-=-", Find(ro, "verg.GUARD.-="));
                if (name == "sd_vergeltung_no_hit")
                    Check("law.verg_no_hit_off", "verg.GUARD.-=-", Find(ro, "verg.GUARD.-="));
                if (name == "sd_zonen_vertrauen")
                {
                    Check("law.zone_trusted_claim", true, Find(ro, "zones3[").Contains("zone GUARD=s-a"));
                    Check("law.ring_fresh_anchor", true, Find(ro, "zones3[").Contains("ring s-b"));
                    Check("law.contest_needs_object", true, Find(ro, "zones3[").Contains("contest -"));
                    Check("law.contest_object_section", true, Find(ro, "zones4[").Contains("contest s-b"));
                    Check("law.zone_gate_needs_trust", true, !Find(ro, "zonesNoTrust[").Contains("zone "));
                }
                if (name == "sd_klammer_durchbruch")
                {
                    Check("law.clamp_race_to_protect", "clamp.eliminate=protect", Find(ro, "clamp.eliminate="));
                    Check("law.clamp_attrition_to_protect", "clamp.attrition=protect", Find(ro, "clamp.attrition="));
                    Check("law.clamp_defensive_untouched", "clamp.protect=-", Find(ro, "clamp.protect="));
                    Check("law.clamp_stall_untouched", "clamp.stall=-", Find(ro, "clamp.stall="));
                    Check("law.clamp_pure", "clamp.pure=yes", Find(ro, "clamp.pure="));
                }
                if (name == "sd_klammer_ruhe")
                {
                    Check("law.clamp_quiet_eliminate", "clamp.eliminate=-", Find(ro, "clamp.eliminate="));
                    Check("law.clamp_quiet_attrition", "clamp.attrition=-", Find(ro, "clamp.attrition="));
                }
                SectionTrust.Clear();
            }

            if (Record)
            {
                var path = Path.Combine(FIX, "section_consumers_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
                return;
            }

            // ================= hand-pinned laws (ground truth) ==============

            // ---- JITTER LAW: the clamp is a pure function of the stream.
            // A quiet mission re-pulled round after round flips ZERO times;
            // exactly one breach event flips it exactly once (never back
            // and forth). Built here in code (the law is about the PULL
            // loop, not about one stored state).
            var quiet = new WorldEventFeed("code|jitter-quiet");
            quiet.Append("MissionStart", 1, new Dictionary<string, object> { { "objective", "Secure the vault." }, { "mission", "SDJ" }, { "roundLimit", 9L }, { "priorityTarget", "REBEL" } });
            quiet.Append("SectionActivated", 1, new Dictionary<string, object> { { "section", "s-a" }, { "name", "Relay Hall" } });
            quiet.Append("Spawn", 1, new Dictionary<string, object> { { "group", "GUARD" }, { "size", 2L }, { "section", "s-a" } });
            var quietCrit = new List<string> { "s-a" };
            var quietSeeds = new Dictionary<string, string[]>(StringComparer.Ordinal) { { "GUARD", new[] { "s-a" } } };
            int flipsQuiet = 0; string lastQuiet = null;
            for (int round = 1; round <= 6; round++)
            {
                quiet.Append("SectionActivated", round, new Dictionary<string, object> { { "section", "s-x" + round }, { "name", "Hall " + round } });
                var sp = SectionSpace.Project(quiet.Events, null, quietSeeds, null, new SectionOptions { Refine = true });
                var now = SectionConsumers.PostureClamp(sp, quietCrit, "eliminate") ?? "-";
                if (lastQuiet != null && now != lastQuiet) flipsQuiet++;
                lastQuiet = now;
            }
            Check("law.jitter_quiet_zero_flips", 0L, (long)flipsQuiet);

            var breach = new WorldEventFeed("code|jitter-breach");
            breach.Append("MissionStart", 1, new Dictionary<string, object> { { "objective", "Secure the vault." }, { "mission", "SDJ" }, { "roundLimit", 9L }, { "priorityTarget", "REBEL" } });
            breach.Append("SectionActivated", 1, new Dictionary<string, object> { { "section", "s-a" }, { "name", "Relay Hall" } });
            breach.Append("Spawn", 1, new Dictionary<string, object> { { "group", "GUARD" }, { "size", 2L }, { "section", "s-a" } });
            int flipsBreach = 0; string lastBreach = null;
            for (int round = 1; round <= 6; round++)
            {
                if (round == 3)
                {
                    breach.Append("HeroActivationStart", 3, new Dictionary<string, object> { { "hero", "LU" } });
                    breach.Append("EntityInteract", 3, new Dictionary<string, object> { { "section", "s-a" }, { "entity", "t-1" } });
                    breach.Append("HeroActivated", 3, new Dictionary<string, object> { { "hero", "LU" } });
                }
                var sp = SectionSpace.Project(breach.Events, null, quietSeeds, null, new SectionOptions { Refine = true });
                var now = SectionConsumers.PostureClamp(sp, quietCrit, "eliminate") ?? "-";
                if (lastBreach != null && now != lastBreach) flipsBreach++;
                lastBreach = now;
            }
            Check("law.jitter_breach_one_flip", 1L, (long)flipsBreach);

            // ---- SLOT LAW (card, pinned): Interrupt > Wachzone >
            // Vergeltung > (economy law untouched) and mark stays king.
            // Same threat values on every candidate: ONLY the keys order.
            SectionTrust.Clear();
            var slotC = new List<FocusCandidate>
            {
                new FocusCandidate { HeroId = "KA", SquadRow = 0, AttackDice = 2, Health = 3 },
                new FocusCandidate { HeroId = "KB", SquadRow = 1, AttackDice = 2, Health = 3 },
                new FocusCandidate { HeroId = "KC", SquadRow = 2, AttackDice = 2, Health = 3 },
            };
            var allKeys = OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null, "KB", "KC", "KA");
            Check("law.slot_all_three", "KB,KC,KA", JoinPickIds(allKeys));
            Check("law.slot_basis", "reaktion,wachzone,vergeltung", JoinPickBases(allKeys));
            var twoKeys = OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null, null, "KC", "KA");
            Check("law.slot_wach_before_verg", "KC,KA,KB", JoinPickIds(twoKeys));
            var marked = new List<FocusCandidate>
            {
                new FocusCandidate { HeroId = "KA", SquadRow = 0, AttackDice = 2, Health = 3, Marks = 1 },
                new FocusCandidate { HeroId = "KB", SquadRow = 1, AttackDice = 2, Health = 3 },
            };
            Check("law.slot_mark_king", "KA,KB",
                  JoinPickIds(OverlordFocus.Rank(marked, "eliminate", null, null, 0, null, null, "KB", null, null)));
            // Multiset fairness: keys REORDER, never re-scope the target set.
            var noKeys = OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null);
            var m1 = new List<string>(); var m2 = new List<string>();
            foreach (var p in allKeys) m1.Add(p.HeroId);
            foreach (var p in noKeys) m2.Add(p.HeroId);
            m1.Sort(StringComparer.Ordinal); m2.Sort(StringComparer.Ordinal);
            Check("law.slot_fairness_multiset", string.Join(",", m1.ToArray()), string.Join(",", m2.ToArray()));
            // Record discipline: the AsDict keys appear ONLY where true.
            var dW = allKeys[1].AsDict(); var dV = allKeys[2].AsDict(); var dR = allKeys[0].AsDict();
            Check("law.asdict_wachzone_only_where_true", true,
                  dW.ContainsKey("wachzone") && !dW.ContainsKey("vergeltung") && !dW.ContainsKey("reaktion"));
            Check("law.asdict_vergeltung_only_where_true", true,
                  dV.ContainsKey("vergeltung") && !dV.ContainsKey("wachzone") && !dV.ContainsKey("reaktion"));
            Check("law.asdict_reaktion_only_where_true", true,
                  dR.ContainsKey("reaktion") && !dR.ContainsKey("wachzone") && !dR.ContainsKey("vergeltung"));
            Check("law.asdict_no_keys_no_new_keys", true,
                  !noKeys[0].AsDict().ContainsKey("wachzone") && !noKeys[0].AsDict().ContainsKey("vergeltung")
                  && !noKeys[0].AsDict().ContainsKey("reaktion"));

            // ---- NULL LAW: every consumer inert without the rail --------
            Check("law.null_wach_empty", 0L,
                  (long)SectionConsumers.WachzoneDecisions(null, null, new List<GroupFacts>(), null).Count);
            Check("law.null_three_empty", 0L,
                  (long)SectionConsumers.ThreeCPushes(null, new List<string> { "G" }, 3).Count);
            Check("law.null_zones_null", true,
                  SectionConsumers.PlanZones(null, null, SkillProfile.For("S5")) == null);
            Check("law.null_clamp_null", true,
                  SectionConsumers.PostureClamp(null, new List<string> { "s-a" }, "eliminate") == null);
            Check("law.null_verg_null", true,
                  SectionConsumers.VergeltungHeroFor(null, null, "G", null) == null);
            Check("law.null_wachhero_null", true,
                  SectionConsumers.WachzoneHeroFor(null, null, null, null) == null);
        }

        static readonly string[] SdExpectKeys = { "canon", "dump", "readout" };

        static List<GroupFacts> ReadSdFacts(Dictionary<string, object> c)
        {
            var l = new List<GroupFacts>();
            if (!c.ContainsKey("facts")) return l;
            int idx = 0;
            foreach (var raw in Json.AsList(c["facts"]))
            {
                var row = Json.AsList(raw);
                var cardId = row[1] == null ? null : Json.AsStr(row[1]);
                l.Add(new GroupFacts(Json.AsStr(row[0]), idx++, cardId, Json.AsStr(row[2]),
                    pattern: Json.AsStr(row[3]),
                    nAlive: (int)Json.AsLong(row[4]),
                    vitalityFrac: Rational.One,
                    zone: null, arrivedRound: null,
                    isHeavy: false, isSwarm: false,
                    priorityOne: false, supporter: false));
            }
            return l;
        }

        // Click the R-C machine until the pending list is empty (the App's
        // per-press law); space=null runs the pre-S-D path (null pair).
        static string RunSdSelect(SectionSpace space, List<string> crit,
                                  Dictionary<string, DeploymentCard> catalog,
                                  List<GroupFacts> facts, FeedWorldView view,
                                  Dictionary<string, object> sel, out List<string> whys)
        {
            var pending = new List<string>();
            foreach (var s in Json.AsList(sel["pending"])) pending.Add(Json.AsStr(s));
            var mine = new List<GroupFacts>();
            foreach (var id in pending)
                foreach (var f in facts)
                    if (f.GroupId == id) mine.Add(f);
            var round = (int)Json.AsLong(sel["round"]);
            var rung = (int)Json.AsLong(sel["rung"]);
            var seed = (ulong)Json.AsLong(sel["seed"]);
            var posture = sel.ContainsKey("posture") ? Json.AsStr(sel["posture"]) : "eliminate";
            var picks = new List<string>();
            whys = new List<string>();
            int guard = pending.Count + 2;
            while (pending.Count > 0 && guard-- > 0)
            {
                string why;
                var pick = ReactionOrder.SelectNext(round, pending, mine, catalog,
                    posture, rung, seed, view, null, out why, space, crit);
                picks.Add(pick); whys.Add(why); pending.Remove(pick);
            }
            return string.Join(",", picks.ToArray());
        }

        static int IndexOf(List<string> lines, string prefix)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartsWith(prefix, StringComparison.Ordinal)) return i;
            return 0;
        }
        static string Find(List<string> lines, string prefix)
        {
            foreach (var s in lines)
                if (s.StartsWith(prefix, StringComparison.Ordinal)) return s;
            return "<missing:" + prefix + ">";
        }
        static string JoinPickIds(List<FocusPick> ps)
        {
            var l = new List<string>();
            foreach (var p in ps) l.Add(p.HeroId);
            return string.Join(",", l.ToArray());
        }
        static string JoinPickBases(List<FocusPick> ps)
        {
            var l = new List<string>();
            foreach (var p in ps) l.Add(p.Basis);
            return string.Join(",", l.ToArray());
        }
    }
}
