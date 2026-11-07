// SectionsAxis.cs - P33-H4b (Thomas 08.11, CONCEPT_missionsachse): the
// mission-axis vectors. Feed law (VictoryConditions projection: scalars
// last-wins, escape zones additive-sorted, axis-aware posture DERIVED
// FIRST), the block Schnittstelle (observed graph, Loch-Gesetz), the
// RiskPosture block fallback clause (blocker/guard duty), the focus axis
// slot + Basis-Fallback pricing, and the midround replan detector.
//
// Null-Law discipline: every vector block carries its no-fact twin; the
// no-fact side must read exactly today's law (canon rows without the
// victory row, Konten-Punkt fallback clause, proxy pricing, once-per-round
// planning). Relations are hand-pilled; the two rational strings of the
// basis pricing are OBSERVED values pinned after review (record discipline
// - never hand-recomputed arithmetic).
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecAxis()
        {
            var v = LoadJson("axis.json");

            // ---------------- feed_cases: the VictoryConditions law ---------
            foreach (var raw in Json.AsList(v["feed_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "feed|" + Json.AsStr(c["name"]);
                var events = BuildEvents(Json.AsList(c["events"]));
                foreach (var e in events)
                {
                    try { WorldEventFeed.Validate(e.Kind, e.Payload); }
                    catch (Exception vx) { Check(name + ".vocabulary[" + e.Kind + "]", "public", vx.Message); }
                }

                var v1 = WorldEventVector.Project(events, "axis|" + Json.AsStr(c["name"]));
                var v2 = WorldEventVector.Project(events, "axis|" + Json.AsStr(c["name"]));
                Check(name + ".replay_canon", v1.Canon(), v2.Canon());
                Check(name + ".replay_hash", v1.HistoryHash, v2.HistoryHash);

                var canon = v1.Canon();
                Check(name + ".posture", Json.AsStr(Json.AsDict(c["expect"])["posture"]), v1.Posture);
                Check(name + ".imperialWinBy", Json.AsStr(c["expect"].AsDictFor("imperialWinBy")), v1.ImperialWinBy);
                Check(name + ".rebelWinBy", Json.AsStr(c["expect"].AsDictFor("rebelWinBy")), v1.RebelWinBy);
                Check(name + ".axis", Json.AsStr(c["expect"].AsDictFor("axis")), v1.AxisActor);
                Check(name + ".axisName", Json.AsStr(c["expect"].AsDictFor("axisName")), v1.AxisActorName);
                Check(name + ".escort", Json.AsStr(c["expect"].AsDictFor("escort")), v1.EscortHero);
                Check(name + ".escape",
                      string.Join("|", Json.AsList(c["expect"].AsDictFor("escape")).ConvertAll(Json.AsStr)),
                      string.Join("|", v1.EscapeSections));

                var ex = Json.AsDict(c["expect"]);
                if (ex.ContainsKey("canon_has"))
                    foreach (var sraw in Json.AsList(ex["canon_has"]))
                        Check(name + ".canon_has[" + Json.AsStr(sraw) + "]", true,
                              canon.Contains(Json.AsStr(sraw)));
                if (ex.ContainsKey("canon_not"))
                    foreach (var sraw in Json.AsList(ex["canon_not"]))
                        Check(name + ".canon_not[" + Json.AsStr(sraw) + "]", false,
                              canon.Contains(Json.AsStr(sraw)));
            }

            // ---------------- block_cases: the Schnittstelle ----------------
            foreach (var raw in Json.AsList(v["block_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "block|" + Json.AsStr(c["name"]);
                var graph = new SectionGraph();
                foreach (var s in Json.AsList(c["sections"])) graph.AddSection(Json.AsStr(s));
                if (c.ContainsKey("edges"))
                    foreach (var eraw in Json.AsList(c["edges"]))
                    {
                        var e = Json.AsList(eraw);
                        graph.ConfirmEdge(Json.AsStr(e[0]), Json.AsStr(e[1]));
                    }
                var axis = Json.AsStr(c["axis"]);
                var zones = Json.AsList(c["zones"]).ConvertAll(Json.AsStr);
                var got = SectionConsumers.BlockSchnittstelle(graph, axis, zones) ?? "";
                Check(name, Json.AsStr(c["expect"]), got);
                // determinism: same inputs, same answer.
                var got2 = SectionConsumers.BlockSchnittstelle(graph, axis, zones) ?? "";
                Check(name + "|determinism", got, got2);
            }

            // ---------------- risk_cases: the block fallback clause --------
            foreach (var raw in Json.AsList(v["risk_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "risk|" + Json.AsStr(c["name"]);
                RiskFacts Mk(string blockHold)
                {
                    return new RiskFacts
                    {
                        GroupId = "G1",
                        Pattern = Json.AsStr(c["pattern"]),
                        Posture = Json.AsStr(c["posture"]),
                        Vitality = Rational.One,
                        RingNear = false,
                        OnAnchor = false,
                        ReplacesAt = (int)Json.AsLong(c["replaces"]),
                        LedgerBalance = (int)Json.AsLong(c["balance"]),
                        ThreatRangeMax = (int)Json.AsLong(c["range"]),
                        BlockHold = blockHold,
                    };
                }
                int rho = (int)Json.AsLong(c["rho"]);
                var fact = Json.AsStr(c["fact"]);
                var r0 = RiskPosture.RiskPosition(Mk(""), rho);
                var r1 = RiskPosture.RiskPosition(Mk(fact), rho);
                var ex = Json.AsDict(c["expect"]);
                if (ex.ContainsKey("pos")) Check(name + ".pos", Json.AsStr(ex["pos"]), r0.Position);
                if (ex.ContainsKey("fallback0")) Check(name + ".fallback0", Json.AsStr(ex["fallback0"]), r0.Fallback);
                if (ex.ContainsKey("fallback1")) Check(name + ".fallback1", Json.AsStr(ex["fallback1"]), r1.Fallback);
                if (ex.ContainsKey("fallback_same"))
                    Check(name + ".fallback_same", Json.AsBool(ex["fallback_same"]), r0.Fallback == r1.Fallback);
                if (ex.ContainsKey("anchor1_has"))
                    Check(name + ".anchor1_has", true,
                          (r1.Anchor ?? "").Contains(Json.AsStr(ex["anchor1_has"])));
                // determinism per side
                var r0b = RiskPosture.RiskPosition(Mk(""), rho);
                Check(name + ".det0", r0.Fallback + "|" + r0.Position, r0b.Fallback + "|" + r0b.Position);
            }

            // ---------------- focus_cases: axis slot + basis pricing -------
            foreach (var raw in Json.AsList(v["focus_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "focus|" + Json.AsStr(c["name"]);
                var posture = Json.AsStr(c["posture"]);
                var cands = new List<FocusCandidate>();
                int row = 0;
                foreach (var hraw in Json.AsList(c["heroes"]))
                {
                    var h = Json.AsDict(hraw);
                    cands.Add(new FocusCandidate
                    {
                        HeroId = Json.AsStr(h["hero"]),
                        SquadRow = row++,
                        Profile = null,          // axis/basis vectors: NO curation row
                        Card = null,             // ... and NO hero card (NPC ally class)
                        Wounded = h.ContainsKey("wounded") && Json.AsBool(h["wounded"]),
                        Marks = h.ContainsKey("marks") ? (int)Json.AsLong(h["marks"]) : 0,
                        MarkedRound = h.ContainsKey("marked_round") ? (int)Json.AsLong(h["marked_round"]) : 0,
                        AttackDice = h.ContainsKey("attack_dice") ? (int)Json.AsLong(h["attack_dice"]) : 0,
                        Health = h.ContainsKey("health") ? Json.AsInt(h["health"]) : 4,
                        ObjectiveActor = h.ContainsKey("objective_actor") && Json.AsBool(h["objective_actor"]),
                        AxisHunt = h.ContainsKey("axis_hunt") && Json.AsBool(h["axis_hunt"]),
                        BasisDice = h.ContainsKey("basis_dice") && h["basis_dice"] != null
                            ? Json.AsList(h["basis_dice"]).ConvertAll(Json.AsStr) : null,
                        BasisSpeed = h.ContainsKey("basis_speed") ? (int)Json.AsLong(h["basis_speed"]) : -1,
                    });
                }
                var rank1 = OverlordFocus.Rank(cands, posture, null, null, 0, null, null, null);
                var rank2 = OverlordFocus.Rank(cands, posture, null, null, 0, null, null, null);
                Check(name + ".replay", rank1.Select(x => (object)x.AsDict()).ToList(),
                      rank2.Select(x => (object)x.AsDict()).ToList());

                var ex = Json.AsDict(c["expect"]);
                if (ex.ContainsKey("order"))
                    Check(name + ".order",
                          string.Join("|", Json.AsList(ex["order"]).ConvertAll(Json.AsStr)),
                          string.Join("|", rank1.Select(p => p.HeroId)));
                if (ex.ContainsKey("winner_axis"))
                    Check(name + ".winner_axis", Json.AsBool(ex["winner_axis"]),
                          rank1.Count > 0 && rank1[0].Achse);

                // the Basis-Fallback pricing itself (observed-then-pinned rationals)
                if (ex.ContainsKey("basis"))
                {
                    var b = Json.AsDict(ex["basis"]);
                    foreach (var kv in b)
                    {
                        var cand = cands.FirstOrDefault(x => x.HeroId == kv.Key);
                        var got = cand == null ? "<missing>"
                                  : (OverlordFocus.ThreatBasis(cand, posture)?.ToString() ?? "none");
                        Check(name + ".basis[" + kv.Key + "]", Json.AsStr(b[kv.Key]), got);
                    }
                }
            }

            // ---------------- replan_cases: the midround detector ----------
            foreach (var raw in Json.AsList(v["replan_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "replan|" + Json.AsStr(c["name"]);
                var events = BuildEvents(Json.AsList(c["events"]));
                var got = WorldEventVector.MidroundReplanTrigger(
                    events, (int)Json.AsLong(c["plan_seq"]), (int)Json.AsLong(c["round"]));
                Check(name, Json.AsStr(c["expect"]), got ?? "");
            }
        }

        // fixture helper: expect[key] as dict (null-tolerant)
    }

    static class AxisJsonExt
    {
        public static object AsDictFor(this object boxed, string key)
        {
            var d = boxed as Dictionary<string, object>;
            if (d == null) throw new InvalidOperationException("expect block missing");
            object x;
            return d.TryGetValue(key, out x) ? x : "";
        }
    }
}
