// S-E section: section_support.json pins the SUPPORTER-MASKEN-VERTRAUEN
// consumer (card t_9dd56737, concept positionierung v1 5/6 + D4 + Ziel-
// Matrix Z6 "Offizier supportet statt zu raten"). The belief rail feeds
// the supporter's TARGET CHOICE: a trusted own section with exactly one
// FRESH hero anchor = strike ("Feuer in die richtige Sektion"); a diffuse
// or ambiguous belief = support (the decision names NO target, never a
// guessed one); the channel itself is silent (null) below the mask-
// Verfeinerung-Rung, without the trust table, without a seeded belief and
// whenever a FACT key is in play (FAKT SCHLAEGT MASKE).
//
// Same feed path as section_consumers.json (WorldEventFeed + Vector +
// SectionSpace.Project through the REAL events), the decision goes through
// the public SectionConsumers.SupporterTargetFor entry point exactly the
// way TacticalAIBridge calls it. Expect blocks were recorded ONCE via
// --record and reviewed line by line; the LAWS (mode per named probe, the
// Z6 monotonicity shares, the fact-precedence nulls, the skill curve, the
// MASKE-NULL pair, the Rank slot behind the pinned trio, the F3 ring-flip)
// are hand-pinned against the computed values, never recorded.
//
// Canon accounting: NO pre-existing fixture is touched. Every new Core
// parameter is null-defaulted, so every pre-S-E vector runs the pre-S-E
// path; section_support.json is the only new file in the fixture dir and
// the full run proves the canon counts stay zahlgleich.
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSectionSupport()
        {
            var v = LoadJson("section_support.json");
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

                SectionTrust.Clear();
                if (c.ContainsKey("trust")) SectionTrust.PrimeDoc(c["trust"]);

                var seeds = ReadSeeds(c);
                var edges = ReadEdges(c);
                var opts = new SectionOptions { Refine = true };
                var space = SectionSpace.Project(feed.Events, null, seeds, edges, opts);

                var ro = new List<string>();

                // -- 1) supporter mask-trust probes -------------------------
                // rows: [group, profile, reactionHero, wachzoneHero, vergeltungHero]
                if (c.ContainsKey("probes"))
                    foreach (var raw2 in Json.AsList(c["probes"]))
                    {
                        var row = Json.AsList(raw2);
                        var g = Json.AsStr(row[0]);
                        var profStr = row[1] == null ? null : Json.AsStr(row[1]);
                        var prof = profStr == null ? null : SkillProfile.For(profStr);
                        var react = row.Count > 2 && row[2] != null ? Json.AsStr(row[2]) : null;
                        var wach = row.Count > 3 && row[3] != null ? Json.AsStr(row[3]) : null;
                        var verg = row.Count > 4 && row[4] != null ? Json.AsStr(row[4]) : null;
                        var tag = row.Count > 5 && row[5] != null ? Json.AsStr(row[5]) : null;
                        var d = SectionConsumers.SupporterTargetFor(space, view, prof, g,
                                                                     react, wach, verg);
                        ro.Add("sup." + g + "." + (profStr ?? "-") +
                               (tag == null ? "" : "." + tag) + "=" +
                               (d == null ? "null"
                                : d.Mode + "|" + (d.Hero ?? "-") + "|" + (d.Section ?? "-")
                                  + "|r" + d.Round));
                    }

                // -- 2) MASKE NULL = heutiger Kanon (bit-identischer Nachweis)
                // The SAME rank vector with and without the trust key: the
                // null-defaulted parameter keeps every pre-S-E row byte-equal
                // (this is the consumer-side half of the Kanon-Pin; the
                // corpus-wide half is the full-run count).
                var rankC = new List<FocusCandidate>
                {
                    new FocusCandidate { HeroId = "KA", SquadRow = 0, AttackDice = 2, Health = 3 },
                    new FocusCandidate { HeroId = "KB", SquadRow = 1, AttackDice = 2, Health = 3 },
                };
                var rkNo = OverlordFocus.Rank(rankC, "eliminate", null, null, 0, null, null);
                var rkNull = OverlordFocus.Rank(rankC, "eliminate", null, null, 0, null, null,
                                                null, null, null, null);
                Check("law.rank_trust_null_byte_equal",
                      JoinPickVectors(rkNo), JoinPickVectors(rkNull));

                // -- 3) support-share accounting (Z6 curve, readout-driven) --
                if (c.ContainsKey("sharePrefixes"))
                {
                    foreach (var raw2 in Json.AsList(c["sharePrefixes"]))
                    {
                        var pfx = Json.AsStr(raw2);
                        int n = 0, sup = 0;
                        foreach (var line in ro)
                        {
                            if (!line.StartsWith("sup." + pfx, StringComparison.Ordinal)) continue;
                            if (!line.Contains("=")) continue;
                            n++;
                            if (line.Contains("=support")) sup++;
                        }
                        ro.Add("share." + pfx + "=" + sup + "/" + n);
                    }
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
                        foreach (var key in SeExpectKeys)
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
                foreach (var key in SeExpectKeys)
                    if (exp.ContainsKey(key)) Check(name + "." + key, exp[key], got[key]);

                // replay determinism per case (same law as every S section)
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                Check(name + ".replay_canon", view.Canon(), view2.Canon());
                var space2 = SectionSpace.Project(feed.Events, null, seeds, edges, opts);
                Check(name + ".replay_dump", space.Dump(), space2.Dump());

                // ---- hand-pinned laws (ground truth, never recorded) ----

                // Strike path: the trusted own section names the hero and
                // the section COHERENTLY (target inside the dominant mask).
                if (name == "se_strike_kohaerent")
                    Check("law.strike_cohere", "sup.OFF.S3=strike|LU|s-a|r2",
                          Find(ro, "sup.OFF.S3="));

                // Diffuse belief = NO claim = support, even while a hero is
                // factually anchored somewhere (never point at a maybe).
                if (name == "se_support_diffus")
                    Check("law.diffus_support", "sup.OFF.S3=support|-|-|r2",
                          Find(ro, "sup.OFF.S3="));
                if (name == "se_support_halb_diffus")
                    Check("law.half_below_threshold", "sup.OFF.S3=support|-|-|r2",
                          Find(ro, "sup.OFF.S3="));

                // Trusted mask, TWO fresh anchors: the mask names the
                // section but no safe target - support, never a coin flip.
                if (name == "se_zwei_anker")
                    Check("law.two_anchors_no_guess", "sup.OFF.S3=support|-|s-a|r2",
                          Find(ro, "sup.OFF.S3="));
                // Stale anchor only (round 1, mask read in round 2): the
                // fresh-anchor law holds - support.
                if (name == "se_anker_veraltet")
                    Check("law.stale_anchor_support", "sup.OFF.S3=support|-|s-a|r2",
                          Find(ro, "sup.OFF.S3="));

                // FAKT SCHLAEGT MASKE: while ANY fact key is in play the
                // channel is silent - E1..E4 outrank every belief.
                if (name == "se_fakt_schlaegt_maske")
                {
                    Check("law.fakt_reaktion_stumm", "sup.OFF.S3.reaktion=null",
                          Find(ro, "sup.OFF.S3.reaktion="));
                    Check("law.fakt_wachzone_stumm", "sup.OFF.S3.wach=null",
                          Find(ro, "sup.OFF.S3.wach="));
                    Check("law.fakt_vergeltung_stumm", "sup.OFF.S3.verg=null",
                          Find(ro, "sup.OFF.S3.verg="));
                }

                // MASKE NULL = heutiger Kanon: unseeded belief and missing
                // rail BOTH ways = the channel never speaks (null-pair law:
                // the rank call above is byte-equal without the key).
                if (name == "se_maske_null")
                {
                    Check("law.maske_null_unseeded", "sup.NEUL.S3=null", Find(ro, "sup.NEUL.S3="));
                    Check("law.maske_null_no_obs", "sup.OBSL.S3=null", Find(ro, "sup.OBSL.S3="));
                }
                if (name == "se_strike_kohaerent")
                {
                    var dNoSpace = SectionConsumers.SupporterTargetFor(
                        null, view, SkillProfile.For("S3"), "OFF", null, null, null);
                    Check("law.maske_null_space_null", true, dNoSpace == null);
                }

                // No trust data = fail-closed (data-exists law).
                if (name == "se_strike_kohaerent")
                {
                    SectionTrust.Clear();
                    var dNoData = SectionConsumers.SupporterTargetFor(
                        space, view, SkillProfile.For("S3"), "OFF", null, null, null);
                    Check("law.no_trust_data_null", true, dNoData == null);
                    SectionTrust.PrimeDoc(c["trust"]);
                }

                // Skill curve (SectionSkill data table, kein Schalter): the
                // channel speaks on the mask-Verfeinerung-Rung (S3/S4/S5)
                // ONLY; S1/S2 and the profile-free reference path stay
                // bit-identical canon (null = consumer silent).
                if (name == "se_skill_kurve")
                {
                    Check("law.curve_s1_null", "sup.OFF.S1=null", Find(ro, "sup.OFF.S1="));
                    Check("law.curve_s2_null", "sup.OFF.S2=null", Find(ro, "sup.OFF.S2="));
                    Check("law.curve_noprofile_null", "sup.OFF.-=null", Find(ro, "sup.OFF.-="));
                    Check("law.curve_s3_strike", "sup.OFF.S3=strike|LU|s-a|r2",
                          Find(ro, "sup.OFF.S3="));
                    Check("law.curve_s4_strike", "sup.OFF.S4=strike|LU|s-a|r2",
                          Find(ro, "sup.OFF.S4="));
                    Check("law.curve_s5_strike", "sup.OFF.S5=strike|LU|s-a|r2",
                          Find(ro, "sup.OFF.S5="));
                }

                // Z6-MONOTONIE (numerisch, beide Ränder + Zwischenschritt):
                // support share falls strictly as belief concentrates across
                // the supporter set - diffuse end all support, middle step
                // partial, tight end all strike.
                if (name == "se_z6_kurve")
                {
                    Check("law.z6_share_diffus", "share.D=3/3", Find(ro, "share.D"));
                    Check("law.z6_share_mittel", "share.M=2/3", Find(ro, "share.M"));
                    Check("law.z6_share_eng", "share.E=0/3", Find(ro, "share.E"));
                    Check("law.z6_monotonic_strict", true,
                          Share(ro, "share.D") > Share(ro, "share.M")
                          && Share(ro, "share.M") > Share(ro, "share.E"));
                    // the tight end really fires (not an inert stream):
                    Check("law.z6_eng_strikes", "sup.E1.S3=strike|LU|s-a|r2", Find(ro, "sup.E1.S3="));
                }

                SectionTrust.Clear();
            }

            if (Record)
            {
                var path = Path.Combine(FIX, "section_support_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
                return;
            }

            // ============ hand-pinned laws outside the case loop ============

            // ---- RANK SLOT LAW (Z6): maskenvertrauen rides BEHIND the
            // pinned trio reaktion > wachzone > vergeltung (verbatim, the
            // mask never enters the fact track) and AHEAD of the economy -
            // "Feuer in die richtige Sektion statt Bedrohungs-Schaetzung".
            SectionTrust.Clear();
            var slotC = new List<FocusCandidate>
            {
                new FocusCandidate { HeroId = "KA", SquadRow = 0, AttackDice = 2, Health = 3 },
                new FocusCandidate { HeroId = "KB", SquadRow = 1, AttackDice = 2, Health = 3 },
                new FocusCandidate { HeroId = "KC", SquadRow = 2, AttackDice = 2, Health = 3 },
            };
            // trust key alone: leads, basis names the mask track.
            var trustOnly = OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null,
                                               null, null, null, "KC");
            Check("law.trust_solo_first", "KC,KA,KB", JoinPickIds(trustOnly));
            Check("law.trust_solo_basis", "maskenvertrauen,proxy,proxy", JoinPickBases(trustOnly));
            // trust vs the trio: the trio stays verbatim in FRONT of it.
            var trioPlus = OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null,
                                              "KA", "KB", "KC", "KC");
            Check("law.trio_beats_trust", "KA,KB,KC", JoinPickIds(trioPlus));
            Check("law.trio_basis_verbatim", "reaktion,wachzone,vergeltung", JoinPickBases(trioPlus));
            // multiset fairness: the key REORDERS, never re-scopes.
            var mNo = new List<string>(); var mTr = new List<string>();
            foreach (var p in OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null)) mNo.Add(p.HeroId);
            foreach (var p in trustOnly) mTr.Add(p.HeroId);
            mNo.Sort(StringComparer.Ordinal); mTr.Sort(StringComparer.Ordinal);
            Check("law.trust_fairness_multiset", string.Join(",", mNo.ToArray()),
                  string.Join(",", mTr.ToArray()));
            // AsDict discipline: the key appears ONLY where true.
            Check("law.asdict_trust_only_where_true", true,
                  trustOnly[0].AsDict().ContainsKey("maskenvertrauen")
                  && !trustOnly[1].AsDict().ContainsKey("maskenvertrauen"));
            Check("law.asdict_no_trust_no_key", true,
                  !OverlordFocus.Rank(slotC, "eliminate", null, null, 0, null, null)[0]
                      .AsDict().ContainsKey("maskenvertrauen"));

            // ---- F3 PIN (QA t_dfbb4252): the ringZones channel flips the
            // DECISION (OrderPlanner BasisPlan), not just the zone dump.
            // QA measured baseF=A,B -> ringF=B,A at S2 AND S4 (x5/4 zone
            // multiplier); pinned here as corpus law, not a scratch probe.
            var f3Card = DeploymentCard.FromRecord(new Dictionary<string, object>
            {
                { "id", "CARD_G" }, { "name", "Shield Guard" }, { "miniSize", "Small1x1" },
                { "speed", 4L }, { "health", 3L }, { "attackType", "Ranged" },
                { "attacks", new List<object> { "Blue" } }, { "isElite", false },
            });
            var f3Cat = new Dictionary<string, DeploymentCard>(StringComparer.Ordinal)
            { { "CARD_G", f3Card } };
            foreach (var lv in new[] { "S2", "S4" })
            {
                var f3Prof = SkillProfile.For(lv);
                foreach (var posture in new[] { "eliminate", "attrition" })
                {
                    var f3Base = F3Plan(f3Cat, "s-a", "s-b", null, null, posture, f3Prof);
                    var f3Ring = F3Plan(f3Cat, "s-a", "s-b", new List<string> { "s-b" }, null, posture, f3Prof);
                    var f3Contest = F3Plan(f3Cat, "s-a", "s-b", null, "s-b", posture, f3Prof);
                    Check("law.f3_ring_flip_" + lv + "_" + posture,
                          f3Base + "->" + f3Ring, "A,B->B,A");
                    // contest x23/20 (same decision-side channel): boost of
                    // B's zone; order-visible on this layout.
                    Check("law.f3_contest_" + lv + "_" + posture,
                          f3Base + "->" + f3Contest, "A,B->B,A");
                }
            }
        }

        static readonly string[] SeExpectKeys = { "canon", "dump", "readout" };

        // F3 decision-side runner: the EXACT consumer path R-C presses
        // (ReactionOrder.BasisPlan with the zone channels from PlanZones).
        static string F3Plan(Dictionary<string, DeploymentCard> cat,
                             string zoneA, string zoneB,
                             List<string> ring, string contest,
                             string posture, SkillProfile prof)
        {
            var facts = new List<GroupFacts>
            {
                new GroupFacts("A", 0, "CARD_G", "Guard A", pattern: "guard", nAlive: 2,
                    vitalityFrac: Rational.One, zone: zoneA, arrivedRound: null,
                    isHeavy: false, isSwarm: false, priorityOne: false, supporter: false),
                new GroupFacts("B", 1, "CARD_G", "Guard B", pattern: "guard", nAlive: 2,
                    vitalityFrac: Rational.One, zone: zoneB, arrivedRound: null,
                    isHeavy: false, isSwarm: false, priorityOne: false, supporter: false),
            };
            var plan = ReactionOrder.BasisPlan(2, facts, cat, posture, prof, 42UL, "c", ring, contest);
            return string.Join(",", plan.GroupIds().ToArray());
        }

        // share.D=3/3 -> 1.0 (support share of a named probe block)
        static double Share(List<string> ro, string line)
        {
            foreach (var s in ro)
                if (s.StartsWith(line + "=", StringComparison.Ordinal))
                {
                    var parts = s.Substring(line.Length + 1).Split('/');
                    return double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture)
                         / double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                }
            return -1.0;
        }

        static string JoinPickVectors(List<FocusPick> ps)
        {
            var l = new List<object>();
            foreach (var p in ps) l.Add(p.AsDict());
            return DumpJson(l);
        }
    }
}
