// O-B supporter-movement vectors: officer_support.json pins the two laws of
// concept offizier_supporter §2 Schicht C (Thomas-Gesetz 04.11.: Offiziere
// wuerfen keine Vorstoss-Doktrin mehr) on the REAL board graph:
//
//   narrow cases - Exhaust end states run through the composed narrowing
//     (Vorstoss-Sperre first, Gruppenleine with its Leere-Set-Gesetz second,
//     card scenarios (a)-(e) of the O-B brief). The set entering the laws is
//     the pattern's own move shackle (the EXISTING support distance rule of
//     Patterns.cs), never a second truth.
//   plan cases  - ActionBuilder.PlanActivation consumption: a barred
//     supporter keeps attack-from-standstill and withdrawal, loses advance
//     candidates (move-then-attack and approach); E1/E4 and the
//     order-receiver exception restore the full pre-O-B behaviour.
//
// expect blocks were cast once with `dotnet run -- <fixtures> --record`,
// reviewed case by case (officer_support_record.json is the raw record
// twin); the LAW checks below are hand-pinned invariants that must hold on
// EVERY case (no survivor closes the nearest-hero gap; while the leash is
// active and was not restored, no survivor exceeds the current anchor gap;
// inert callers see the untouched set). Non-supporter twins pin bit-identity
// of the pre-O-B behaviour class.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static OfficerEmergency ParseEmergency(string s)
        {
            switch (s)
            {
                case "None": return OfficerEmergency.None;
                case "E1": return OfficerEmergency.E1;
                case "E2": return OfficerEmergency.E2;
                case "E3": return OfficerEmergency.E3;
                case "E4": return OfficerEmergency.E4;
                default: throw new InvalidOperationException("unknown emergency: " + s);
            }
        }

        static void SecOfficerSupport()
        {
            var v = LoadJson("officer_support.json");
            // inline helper boards (keyword_rules.json route; canonical
            // boards stay untouched)
            if (v.ContainsKey("boards"))
                foreach (var br in Json.AsList(v["boards"]))
                {
                    var nb = Board.FromJson(Json.AsDict(br));
                    Boards()[nb.Name] = nb;
                }
            // inline supporter cards: the bridge keyword lane exactly as
            // TacticalAIBridge.ToRecord injects it live (officer_doctrine.json
            // route); units_core.json stays untouched.
            var extraCards = DeploymentCard.LoadCatalog(Json.AsDict(v["cards"]));
            var scenarios = Json.AsDict(v["scenarios"]);
            var cases = Json.AsList(v["cases"]);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var kind = Json.AsStr(Json.Get(c, "kind", (object)"narrow"));
                var emergency = ParseEmergency(Json.AsStr(Json.Get(c, "emergency", (object)"None")));
                bool orderRange = Json.AsLong(Json.Get(c, "order_range", 0L)) == 1;

                var built = BuildScenarioWorld(Json.AsDict(scenarios[Json.AsStr(c["scenario"])]));
                var world = built.Item2;
                var fig = world.ById(Json.AsStr(c["fig"]));
                if (fig == null) { Fail(name + ".fig", "figure missing from world"); continue; }
                if (c.ContainsKey("card_override") && c["card_override"] != null)
                    fig.Card = extraCards[Json.AsStr(c["card_override"])];

                Dictionary<string, object> got;
                if (kind == "narrow")
                {
                    var all = world.Graph.Exhaust(fig, fig.Anchor, fig.Orientation,
                                                  fig.Card.Speed, null, "move");
                    // The pattern's own end-state shackle feeds the laws
                    // (support: keep >=2 from heroes - the existing law).
                    var shackle = Patterns.MoveShackle(world, fig);
                    long shackleDropped = 0;
                    var baseStates = all;
                    if (shackle != null)
                    {
                        baseStates = new Dictionary<(Cell, int), int>();
                        foreach (var kv in all)
                            if (shackle(kv.Key)) baseStates[kv.Key] = kv.Value;
                        shackleDropped = all.Count - baseStates.Count;
                    }
                    var r = OfficerSupportMovement.Narrow(world, fig, baseStates,
                                                         emergency, orderRange);
                    var kept = r.Kept
                        .Select(kv => kv.Key.Item1.X + "," + kv.Key.Item1.Y + "," +
                                      kv.Key.Item2 + "," + kv.Value)
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .Select(s => (object)s).ToList();
                    got = new Dictionary<string, object>
                    {
                        { "kept", kept },
                        { "shackle_dropped", shackleDropped },
                        { "advance_law", (object)r.AdvanceLaw },
                        { "leash_law", (object)r.LeashLaw },
                        { "removed_advance", r.RemovedAdvance },
                        { "removed_leash", r.RemovedLeash },
                        { "leash_restored", (object)r.LeashRestored },
                        { "cur_hero_gap", (long)(r.CurHeroGap == 1 << 30 ? -1 : r.CurHeroGap) },
                        { "cur_anchor_gap", (long)r.CurAnchorGap },
                    };

                    // ---- hand-pinned law invariants on EVERY case ----
                    long badAdvance = 0, badLeash = 0;
                    foreach (var kv in r.Kept)
                    {
                        if (r.AdvanceLaw &&
                            Patterns.HeroGap(world, Geometry.FootprintCells(
                                kv.Key.Item1, fig.Card.MiniSize, kv.Key.Item2), fig, null)
                            < r.CurHeroGap) badAdvance++;
                        if (r.LeashLaw && !r.LeashRestored && r.CurAnchorGap >= 0 &&
                            OfficerSupportMovement.AnchorGapAt(world, fig,
                                kv.Key.Item1, kv.Key.Item2) > r.CurAnchorGap) badLeash++;
                    }
                    Check(name + ".law.no_advance_survivor", 0L, badAdvance);
                    Check(name + ".law.leash_held", 0L, badLeash);
                    // inert callers (no supporter flag) must see the set UNTOUCHED
                    if (!fig.Card.Supporter)
                    {
                        Check(name + ".law.inert_set", (long)baseStates.Count, (long)r.Kept.Count);
                        Check(name + ".law.inert_counts", false, r.AdvanceLaw || r.LeashLaw);
                    }
                }
                else // plan: ActionBuilder consumption
                {
                    var plan = ActionBuilder.PlanActivation(world, fig, true, true,
                                                            emergency, orderRange);
                    var invalid = ActionBuilder.ValidatePlan(fig, plan);
                    Check(name + ".law.valid_plan", 0L, (long)invalid.Count);
                    bool barred = OfficerSupportMovement.AdvanceBarred(fig, emergency, orderRange);
                    if (barred)
                    {
                        // O-B fairness pin: NO planned move may end closer to
                        // the nearest hero than the current position (advance
                        // candidates were removed; hold/withdrawal stay).
                        long curGap = Patterns.HeroGap(world, SpaceLegality.FigureCells(fig), fig, null);
                        long bad = 0;
                        foreach (var a in plan.Actions)
                        {
                            if (a.Type != "move") continue;
                            if (a.Params["states"] is List<(Cell, int)> sts && sts.Count > 0)
                            {
                                var end = sts[sts.Count - 1];
                                if (Patterns.HeroGap(world, Geometry.FootprintCells(
                                        end.Item1, fig.Card.MiniSize, end.Item2), fig, null) < curGap)
                                    bad++;
                            }
                        }
                        Check(name + ".law.plan_no_advance", 0L, bad);
                    }
                    bool hasMove = plan.Actions.Any(a => a.Type == "move");
                    bool hasAttack = plan.AttackActions > 0;
                    got = (Dictionary<string, object>)Norm(plan.AsDict());
                    got["has_move"] = hasMove;
                    got["has_attack"] = hasAttack;
                    // The order-receiver why-line is public-safe text: it may
                    // only appear when the exception was actually granted.
                    Check(name + ".law.why_order_line",
                        orderRange && fig.Card.Supporter && hasMove,
                        plan.Why.Contains("order codex"));
                }

                if (Record) { recorded[name] = got; continue; }
                if (!c.ContainsKey("expect"))
                {
                    Check(name + ".expect", "present", "missing - run with --record");
                    continue;
                }
                Check(name + ".expect", Json.AsDict(c["expect"]), got);
            }
            if (Record)
            {
                var path = Path.Combine(FIX, "officer_support_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
