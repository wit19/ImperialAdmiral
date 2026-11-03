// M6b SecPersonasSwap: the elite_swap canon (CONCEPT_personas_v2 v2.1
// Sec.5-2c). Hand-authored fixture (personas_swap.json, same documented
// route as action_tree.json / optional_deploy.json - no --record; the expect
// vectors come from an independent Python re-implementation of the Sec.1.1
// law, tools/gen_personas_swap_fixture.py). Per persona x test mission x
// T2/4/6/8 this section pins:
//   3a Delta-0 parity   - the persona plan's figure count AND booked cost
//                         equal the baseline (persona=none) plan EXACTLY
//                         (never "at most"): the baseline is re-planned on
//                         the same vector and compared, so parity can never
//                         be quietly dropped from the expect vectors.
//   3b double elite     - no plan ever holds two elite prints of one
//                         figure name (max ONE elite per name on the
//                         board; an elite beside a regular of the same
//                         name is the legal plain post-swap state).
//   3c swap proof       - the exact plan card identity (expect_out) incl.
//                         the elite card ids DG003/DG006/DG013/DG008
//                         wherever the persona is entitled to swap, also
//                         for stacked budgets > 1 (droiden_direktor /
//                         grossmoff at T8: both elite ids must appear).
// The fixture's swap_personas block mirrors the M6a2 draft rank ladders in
// the FLAT schema, so Personas.PrimeDoc+Load() exercises the real loader
// (elite_card lane) end-to-end. A live cross-check at the end compares the
// swap-row inventory of the SHIPPED asset against this table - before the
// separate asset-swap commit it reports the pending state, after it it
// becomes a hard per-persona budget equality.
using System;
using System.Collections.Generic;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecPersonasSwap()
        {
            var v = LoadJson("personas_swap.json");

            // the fixture persona table goes through the REAL loader
            Personas.PrimeDoc(new Dictionary<string, object>
            {
                { "personas", v["swap_personas"] }
            });
            var table = Personas.Load();
            Check("swap_personas loaded (M6a2 six-persona mirror)", 6L,
                  (long)table.Personas.Count);

            // ---------------------------------------------------- missions
            var missions = new Dictionary<string, SwapMission>();
            var mdoc = Json.AsDict(v["missions"]);
            foreach (var kv in mdoc)
            {
                var m = Json.AsDict(kv.Value);
                missions[kv.Key] = new SwapMission
                {
                    Draft = Cards(m["draft"]),
                    Board = Cards(m["board"]),
                    Hand = Cards(m["hand"]),
                };
            }
            Check("missions loaded", 6L, (long)missions.Count);

            // baseline law: persona=none never moves a card
            var none = new PersonaView();
            foreach (var kv in missions)
            {
                var plan = PersonaSwaps.Plan(none, kv.Value.Draft, kv.Value.Board, kv.Value.Hand);
                var ids = new List<string>();
                foreach (var r in plan) ids.Add(r.Out.Id);
                var want = new List<string>();
                foreach (var c in kv.Value.Draft) want.Add(c.Id);
                Check("none_identity[" + kv.Key + "]", string.Join(",", want), string.Join(",", ids));
            }

            // ------------------------------------------------- swap budget
            var budgets = Json.AsDict(v["budgets"]);
            foreach (var kv in budgets)
            {
                var row = Json.AsList(kv.Value);
                for (int i = 0; i < row.Count; i++)
                {
                    var threat = 2 + 2 * i;   // T2/4/6/8
                    var view = table.Resolve(kv.Key, threat);
                    Check("budget[" + kv.Key + "@" + threat + "]",
                          Json.AsLong(row[i]), (long)PersonaSwaps.Budget(view));
                }
            }

            // ------------------------------------------------------- cases
            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var pid = Json.AsStr(c["persona"]);
                var threat = Json.AsInt(c["threat"]);
                var mname = Json.AsStr(c["mission"]);
                var m = missions[mname];
                var tag = pid + "@" + threat + "|" + mname;
                var view = table.Resolve(pid, threat);
                var plan = PersonaSwaps.Plan(view, m.Draft, m.Board, m.Hand);
                var basePlan = PersonaSwaps.Plan(none, m.Draft, m.Board, m.Hand);

                // 3c swap proof: the exact plan card identity
                var want = new List<string>();
                foreach (var o in Json.AsList(c["expect_out"])) want.Add(Json.AsStr(o));
                var got = new List<string>();
                foreach (var r in plan) got.Add(r.Out.Id);
                Check("swapplan[" + tag + "]", string.Join(",", want), string.Join(",", got));
                Check("swapcount[" + tag + "]", Json.AsLong(c["expect_swaps"]),
                      (long)PersonaSwaps.SwapsInPlan(plan));

                // 3a Delta-0 parity: EXACTLY equal against the re-planned
                // baseline, never "at most" (Beschluss 11).
                Check("delta0_figures[" + tag + "]",
                      (long)PersonaSwaps.FiguresInPlan(basePlan),
                      (long)PersonaSwaps.FiguresInPlan(plan));
                Check("delta0_cost[" + tag + "]",
                      (long)PersonaSwaps.CostInPlan(basePlan),
                      (long)PersonaSwaps.CostInPlan(plan));

                // 3b double-elite non-existence (Beschluss 12, persona rail)
                Check("double_elite[" + tag + "]", false,
                      PersonaSwaps.HasDoubleElite(plan));
            }

            // ------------------------------------- shipped-asset cross-check
            // Restore the shipped asset and compare its swap inventory
            // against the pinned table. Pre-swap the shipped asset carries
            // NO elite_swap rows (legacy 3-persona fleet) -> the section
            // states that openly instead of pretending equality. After the
            // separate asset-swap commit the same loop becomes the hard
            // live-law check (same budgets, per persona x threat).
            Personas.PrimeDoc(Json.AsDict(LoadLangJson("En", "tactical_personas.json")));
            var shipped = Personas.Load();
            bool shippedHasSwaps = false;
            foreach (var p in shipped.Personas)
                foreach (var r in p.Ranks)
                    foreach (var e in r.Effects)
                        if (e.Kind == "elite_swap") shippedHasSwaps = true;
            if (!shippedHasSwaps)
            {
                Check("shipped_asset_swap_rows[pending: legacy fleet]", 0L, 0L);
            }
            else
            {
                foreach (var kv in budgets)
                {
                    var row = Json.AsList(kv.Value);
                    for (int i = 0; i < row.Count; i++)
                    {
                        var threat = 2 + 2 * i;
                        Check("live_budget[" + kv.Key + "@" + threat + "]",
                              Json.AsLong(row[i]),
                              (long)PersonaSwaps.Budget(shipped.Resolve(kv.Key, threat)));
                    }
                }
            }
        }

        sealed class SwapMission
        {
            public List<SwapCard> Draft;
            public List<SwapCard> Board;
            public List<SwapCard> Hand;
        }

        static List<SwapCard> Cards(object rows)
        {
            var outList = new List<SwapCard>();
            foreach (var raw in Json.AsList(rows))
            {
                var d = Json.AsDict(raw);
                outList.Add(new SwapCard(
                    Json.AsStr(d["id"]), Json.AsStr(d["name"]), Json.AsStr(d["faction"]),
                    Json.AsInt(d["cost"]), Json.AsInt(d["size"]),
                    Json.AsLong(d["elite"]) != 0));
            }
            return outList;
        }
    }
}
