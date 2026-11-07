// SectionsMarkerObject.cs - P33-H4c (Thomas 08.11, CONCEPT_missionsachse
// 2.4, card t_2ff1ba37): the MARKER/OBJECT axis vectors. Feed law (the
// MissionObject projection: keyed by entity GUID, last-event-wins, printed
// text numbers only), the CORE polaritaet ladder P1-P4 (P2 wins over P1 by
// ruling; no rung => NEUTRAL => kein Angriffsziel, kein Schutzziel), the
// SCHUTZGESETZ Schnittstelle (mirrored block axis over the OBSERVED graph,
// Loch-Gesetz), and the ziel=OBJEKT ordering law (door ON the route first =
// "Tuer sprengen" as Weg-Alternative; doorOpen is the ONLY state read -
// no Aufwand-/Rest-Health-Kalkuel anywhere, nearest next, unknown last,
// ties name-ordinal).
//
// Null-Law discipline: the a_null_twin vector pins the canon byte-identity
// WITHOUT any "can attack (Health:" object (no object rows -> Canon and
// every consumer unchanged); empty guard/plantarget inputs stay null so the
// consumers keep today's Konten-Punkt/canon clauses bit-identically.
// Every relation is hand-pilled from the law - no recorded arithmetic, no
// RNG, determinism re-calls per case.
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecMarkerObject()
        {
            var v = LoadJson("marker_object.json");

            // ---------------- object_cases: feed + polaritaet ladder ------
            foreach (var raw in Json.AsList(v["object_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "obj|" + Json.AsStr(c["name"]);
                var events = BuildEvents(Json.AsList(c["events"]));
                foreach (var e in events)
                {
                    try { WorldEventFeed.Validate(e.Kind, e.Payload); }
                    catch (Exception vx) { Check(name + ".vocabulary[" + e.Kind + "]", "public", vx.Message); }
                }

                var v1 = WorldEventVector.Project(events, "obj|" + Json.AsStr(c["name"]));
                var v2 = WorldEventVector.Project(events, "obj|" + Json.AsStr(c["name"]));
                Check(name + ".replay_canon", v1.Canon(), v2.Canon());
                Check(name + ".replay_hash", v1.HistoryHash, v2.HistoryHash);

                var ex = Json.AsDict(c["expect"]);
                var canon = v1.Canon();
                if (ex.ContainsKey("canon_has"))
                    foreach (var sraw in Json.AsList(ex["canon_has"]))
                        Check(name + ".canon_has[" + Json.AsStr(sraw) + "]", true,
                              canon.Contains(Json.AsStr(sraw)));
                if (ex.ContainsKey("canon_not"))
                    foreach (var sraw in Json.AsList(ex["canon_not"]))
                        Check(name + ".canon_not[" + Json.AsStr(sraw) + "]", false,
                              canon.Contains(Json.AsStr(sraw)));

                // the three consumer views (Null-Law: empty WITHOUT facts)
                Check(name + ".hunt", Json.AsStr(ex["hunt"]),
                      string.Join("|", v1.HuntObjects().ConvertAll(o => o.Name)));
                Check(name + ".protect", Json.AsStr(ex["protect"]),
                      string.Join("|", v1.ProtectObjects().ConvertAll(o => o.Name)));
                Check(name + ".contested", Json.AsStr(ex["contested"]),
                      string.Join("|", v1.ContestedObjectSections()));

                // ladder verdict per GUID (only when the case pins one)
                if (Json.AsStr(ex["basis"]).Length > 0)
                {
                    Check(name + ".count", (long)1, (long)v1.Objects.Count);
                    var o = v1.Objects.Values.First();
                    Check(name + ".polaritaet", Json.AsStr(ex["pol"]), o.Polaritaet);
                    Check(name + ".basis", Json.AsStr(ex["basis"]), o.Basis);
                }
                else Check(name + ".count", (long)0, (long)v1.Objects.Count);
            }

            // ---------------- guard_cases: the SCHUTZGESETZ ---------------
            foreach (var raw in Json.AsList(v["guard_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "guard|" + Json.AsStr(c["name"]);
                var graph = new SectionGraph();
                foreach (var s in Json.AsList(c["sections"])) graph.AddSection(Json.AsStr(s));
                if (c.ContainsKey("edges"))
                    foreach (var eraw in Json.AsList(c["edges"]))
                    {
                        var e = Json.AsList(eraw);
                        graph.ConfirmEdge(Json.AsStr(e[0]), Json.AsStr(e[1]));
                    }
                var ap = Json.AsList(c["approach"]).ConvertAll(Json.AsStr);
                var objs = Json.AsList(c["objects"]).ConvertAll(Json.AsStr);
                var got = SectionConsumers.ObjectGuardSchnittstelle(graph, ap, objs) ?? "";
                Check(name, Json.AsStr(c["expect"]), got);
                var got2 = SectionConsumers.ObjectGuardSchnittstelle(graph, ap, objs) ?? "";
                Check(name + "|determinism", got, got2);
            }

            // ---------------- plantarget_cases: ziel=OBJEKT ---------------
            foreach (var raw in Json.AsList(v["plantarget_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = "plan|" + Json.AsStr(c["name"]);
                var graph = new SectionGraph();
                foreach (var s in Json.AsList(c["sections"])) graph.AddSection(Json.AsStr(s));
                if (c.ContainsKey("edges"))
                    foreach (var eraw in Json.AsList(c["edges"]))
                    {
                        var e = Json.AsList(eraw);
                        graph.ConfirmEdge(Json.AsStr(e[0]), Json.AsStr(e[1]));
                    }
                if (c.ContainsKey("door_edges"))
                    foreach (var eraw in Json.AsList(c["door_edges"]))
                    {
                        var e = Json.AsList(eraw);
                        graph.AddDoorEdge(Json.AsStr(e[0]), Json.AsStr(e[1]), Json.AsStr(e[2]));
                    }
                if (c.ContainsKey("door_states"))
                    foreach (var kv in Json.AsDict(c["door_states"]))
                        graph.NoteDoorState(kv.Key, Json.AsStr(kv.Value) == "open");
                var hunt = new List<FeedWorldView.MissionObjectFact>();
                foreach (var oraw in Json.AsList(c["objects"]))
                {
                    var o = Json.AsDict(oraw);
                    hunt.Add(new FeedWorldView.MissionObjectFact
                    {
                        Name = Json.AsStr(o["name"]),
                        Guid = Json.AsStr(o["guid"]),
                        Section = Json.AsStr(o["section"]),
                        Polaritaet = "jagt",     // caller law: only POLAR rows reach the seam
                    });
                }
                var group = Json.AsStr(c["group"]);
                var focus = Json.AsStr(c["focus"]);
                var pick = SectionConsumers.ObjectPlanTargetFor(graph, group, focus, hunt);
                Check(name, Json.AsStr(c["expect"]), pick == null ? "" : pick.Name);
                var pick2 = SectionConsumers.ObjectPlanTargetFor(graph, group, focus, hunt);
                Check(name + "|determinism", pick == null ? "" : pick.Name,
                      pick2 == null ? "" : pick2.Name);
            }
        }
    }
}
