// P33-H5 (card t_94d361bb) section: the Beschaffungs-Ranking law vectors.
// Every expect block is HAND-PINNED against the documented BudgetPlanner
// weight table (the fixture _comment carries the table); the harness
// --record cross-check must reproduce the pins byte-identically - drift
// here means the Core law changed, which demands a playtest re-read, not
// a re-record. The Null-Law case pins the machine guarantee (Rank answers
// null without zone facts => the popup never leaves the strict sequence),
// the acceptance cases (a)-(d) are named buy_beats_reinforce_hunt /
// reinforce_beats_buy_defend / spare_defers_heavy_buy(+dump variant) /
// null_law_no_zones. Junk rows fail CLOSED (reader law).
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static SectionGraph BuildBudgetGraph(Dictionary<string, object> g)
        {
            var graph = new SectionGraph();
            foreach (var s in Json.AsList(g["sections"]))
                graph.AddSection(Json.AsStr(s));
            foreach (var raw in Json.AsList(g["doors"]))
            {
                var e = Json.AsList(raw);
                graph.AddDoorEdge(Json.AsStr(e[0]), Json.AsStr(e[1]), Json.AsStr(e[2]));
            }
            return graph;
        }

        static List<string> BudgetStrList(Dictionary<string, object> c, string key)
        {
            var outp = new List<string>();
            object v;
            if (!c.TryGetValue(key, out v) || v == null) return outp;
            foreach (var x in Json.AsList(v)) outp.Add(Json.AsStr(x));
            return outp;
        }

        static List<long> BudgetLongList(Dictionary<string, object> c, string key)
        {
            var outp = new List<long>();
            object v;
            if (!c.TryGetValue(key, out v) || v == null) return outp;
            foreach (var x in Json.AsList(v)) outp.Add(Json.AsLong(x));
            return outp;
        }

        static void SecBudgetRank()
        {
            var v = LoadJson("budget_rank.json");
            var graph = BuildBudgetGraph(Json.AsDict(v["graph"]));

            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);

                var actions = new List<BudgetAction>();
                foreach (var araw in Json.AsList(c["actions"]))
                {
                    var a = Json.AsDict(araw);
                    var act = new BudgetAction
                    {
                        Kind = Json.AsStr(a["kind"]),
                        Id = Json.AsStr(a["id"]),
                        Name = a.ContainsKey("name") && a["name"] != null ? Json.AsStr(a["name"]) : "",
                        Cost = (int)Json.AsLong(a["cost"]),
                        Losses = a.ContainsKey("losses") && a["losses"] != null ? (int)Json.AsLong(a["losses"]) : 0,
                        Size = a.ContainsKey("size") && a["size"] != null ? (int)Json.AsLong(a["size"]) : 0,
                        Firepower = a.ContainsKey("firepower") && a["firepower"] != null ? (int)Json.AsLong(a["firepower"]) : 0,
                    };
                    foreach (var s in BudgetStrList(a, "sections")) act.Sections.Add(s);
                    actions.Add(act);   // null junk rows are exercised via the null literal below
                }
                // one real null row for the fail-closed reader law
                if (name == "reader_junk_fails_closed") actions.Insert(0, null);

                var ranked = BudgetPlanner.Rank(
                    graph,
                    BudgetStrList(c, "zones"),
                    BudgetStrList(c, "axis"),
                    BudgetStrList(c, "friendly"),
                    Json.AsStr(c["posture"]),
                    Json.AsLong(c["threat"]),
                    Json.AsLong(c["gain"]),
                    c.ContainsKey("dump") && c["dump"] != null && Json.AsBool(c["dump"]),
                    actions,
                    BudgetLongList(c, "future"));

                // ------------------------------------------------ Null-Law
                var expOrder = exp["order"];
                if (expOrder == null)
                {
                    Check("budget[" + name + "].null", true, ranked == null);
                    continue;
                }
                Check("budget[" + name + "].notNull", true, ranked != null);
                if (ranked == null) continue;

                // ------------------------------------------------ full order
                var order = ranked
                    .Select(x => x.Kind == BudgetPlanner.KindHold
                        ? "hold"
                        : x.Kind + ":" + x.Id)
                    .ToList();
                Check("budget[" + name + "].order",
                    Json.AsList(expOrder).ConvertAll(x => (object)Json.AsStr(x)),
                    order.Cast<object>().ToList());

                // ----------------------------------------- per-action fields
                foreach (var praw in Json.AsList(expOrder))
                {
                    var key = Json.AsStr(praw);
                    if (key == "hold") continue;
                    object spec;
                    if (!exp.TryGetValue(key, out spec) || spec == null) continue;
                    var e = Json.AsDict(spec);
                    var row = ranked.FirstOrDefault(x =>
                        x.Kind + ":" + x.Id == key);
                    if (row == null) { Fail("budget[" + name + "]." + key, "row missing"); continue; }
                    Check("budget[" + name + "]." + key + ".axis",
                        (long)Json.AsLong(e["axis"]), (long)row.AxisDist);
                    Check("budget[" + name + "]." + key + ".zone",
                        (long)Json.AsLong(e["zone"]), (long)row.ZoneDist);
                    Check("budget[" + name + "]." + key + ".leash",
                        Json.AsBool(e["leash"]), row.Leash);
                    Check("budget[" + name + "]." + key + ".spare",
                        Json.AsBool(e["spare"]), row.Spared);
                    Check("budget[" + name + "]." + key + ".score",
                        (long)Json.AsLong(e["score"]), (long)row.Score);
                }

                // -------------------------------------------------- the law
                // Rank is a pure function: two runs over the same facts must
                // be byte-identical (determinism canon, zero RNG).
                var again = BudgetPlanner.Rank(
                    graph, BudgetStrList(c, "zones"), BudgetStrList(c, "axis"), BudgetStrList(c, "friendly"),
                    Json.AsStr(c["posture"]), Json.AsLong(c["threat"]), Json.AsLong(c["gain"]),
                    c.ContainsKey("dump") && c["dump"] != null && Json.AsBool(c["dump"]),
                    ranked.Where(x => x.Kind != BudgetPlanner.KindHold).ToList(),
                    BudgetLongList(c, "future"));
                var order2 = again == null ? null : again
                    .Select(x => x.Kind == BudgetPlanner.KindHold ? "hold" : x.Kind + ":" + x.Id)
                    .ToList();
                Check("budget[" + name + "].determinism",
                    Canonical(order.Cast<object>().ToList()),
                    Canonical(order2 == null ? null : order2.Cast<object>().ToList()));
            }
        }
    }
}
