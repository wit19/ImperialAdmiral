// P50 attack-window vectors: the finisher_last tail pull becomes CONDITIONAL
// (T. ruling 25.11.): hero and Imperial activations interleave, so a damage
// group with a hero ring believed in its OWN zone is attackable NOW and must
// not cool in the tail slot while the heroes shoot at it untouched - it keeps
// its ring-lifted rank and strikes when it activates. Everything else keeps
// today's law bit-identical: ring elsewhere, no feed, empty feed, unknown own
// zone -> canon tail pull. Hand-pinned laws (the law IS the
// expectation, same discipline as SectionsReactionOrder's bottom block -
// never --record; the relations are computed against the real PlanRound).
// P51 (T. ruling 2 of 25.11.): the window is NOT exempted by any counter -
// woundAll counts wounds as they land, so even under that counter an
// attackable damage dealer keeps its front rank.
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecAttackWindow()
        {
            var v = LoadJson("attack_window.json");
            var catalog = Catalog();
            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                List<string> ring = c.ContainsKey("ring") && c["ring"] != null
                    ? Json.AsList(c["ring"]).Select(Json.AsStr).ToList() : null;
                string counter = c.ContainsKey("counter") && c["counter"] != null
                    ? Json.AsStr(c["counter"]) : null;
                bool finZoneUnknown = c.ContainsKey("fin_zone_unknown") &&
                                      c["fin_zone_unknown"] != null &&
                                      Json.AsBool(c["fin_zone_unknown"]);

                var roster = new List<GroupFacts>
                {
                    new GroupFacts("FIN", 0, "DG021", "Schwere Sturmtruppen", "aggressive",
                        2, Rational.One, zone: finZoneUnknown ? null : "ZR"),
                    new GroupFacts("WEAK", 1, "DG009", "Leichte Sturmtruppen", "aggressive",
                        2, Rational.One, zone: "ZX"),
                };
                // Premise (generator law, never hope): FIN prints the higher
                // damage EV, so it IS the finisher candidate of this roster.
                var finKey = OrderPlanner.WithEv(roster[0], catalog["DG021"]).PrintedEvTotal;
                var weakKey = OrderPlanner.WithEv(roster[1], catalog["DG009"]).PrintedEvTotal;
                Check(name + ".premise_fin_is_candidate", true, finKey > weakKey);

                var plan = OrderPlanner.PlanRound(3, roster, catalog, "eliminate",
                    SkillProfile.For("S5"), new DeterministicRandom(7), 1,
                    ring, null, counter);
                OrderPlanner.AssertPermutation(plan, roster.Select(g => g.GroupId));

                var exp = Json.AsDict(c["expect"]);
                Check(name + ".order", Json.AsStr(exp["order"]),
                      string.Join(",", plan.GroupIds()));
                var slot = plan.Slots.Single(s => s.GroupId == "FIN");
                Check(name + ".fin_finisher_last", Json.AsBool(exp["fin_finisher_last"]),
                      slot.Tags.Contains("finisher_last"));
                Check(name + ".fin_attack_window", Json.AsBool(exp["fin_attack_window"]),
                      slot.Tags.Contains("attack_window"));
                Check(name + ".fin_why_contains", true,
                      slot.Why.Contains(Json.AsStr(exp["fin_why_contains"])));
            }
        }
    }
}
