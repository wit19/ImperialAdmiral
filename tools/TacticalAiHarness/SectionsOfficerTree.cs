// O-C supporter-branch vectors: officer_tree.json pins the popup-tree variant
// selection of concept offizier_supporter §2 Schicht B (OfficerDoctrine.
// TreeKindFor) on top of the REAL feed path the bridge walks (WorldEventFeed.
// Append + WorldEventVector.Project -> EmergencyFor cascade, same fail-loud
// discipline as officer_doctrine). Every expect is HAND-PINNED (the law IS the
// expectation; the why strings are byte-identical to the O-A pins so the
// apTreeOfficerNotfall wrap rides exactly the O-A-pinned reasons). The pure
// skill x supporter x emergency matrix below is hand-pinned ground truth.
using System;
using System.Collections.Generic;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecOfficerTree()
        {
            var v = LoadJson("officer_tree.json");

            // Card catalog from the fixture records (shipped printed stats,
            // copied from officer_doctrine.json - the Supporter flag derives
            // from the printed token + id wall exactly like the bridge's
            // ToRecord keyword lane feeds it).
            var cards = new Dictionary<string, DeploymentCard>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(v["cards"]))
            {
                var c0 = DeploymentCard.FromRecord(Json.AsDict(raw));
                cards[c0.Id] = c0;
            }

            var cases = Json.AsList(v["cases"]);
            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                int skill = (int)Json.AsLong(c["skill"]);
                var selfId = Json.AsStr(c["self"]);

                var feed = new WorldEventFeed("vector|" + name);
                AppendFeedEvents(feed, c["events"]);
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());

                var rows = Json.AsList(c["groups"]);
                var facts = FactsFromJson(c["groups"]);
                for (int gi = 0; gi < rows.Count; gi++)
                {
                    DeploymentCard card;
                    facts[gi].Supporter =
                        cards.TryGetValue(Json.AsStr(Json.AsDict(rows[gi])["card"]), out card)
                        && card.Supporter;
                }
                GroupFacts self = null;
                foreach (var f in facts)
                    if (f.GroupId == selfId) self = f;
                if (self == null)
                { Fail(name + ".self", "self group '" + selfId + "' not in groups"); continue; }

                // The bridge's exact call shape: no designated threat hero and
                // no human threat-signal in the app (E3/E2 stay silent - the
                // fail-closed injection the bridge comment names).
                var r = OfficerDoctrine.EmergencyFor(skill, view, self, facts, null, cards, false);
                var kind = OfficerDoctrine.TreeKindFor(skill, self.Supporter, r.Stage);

                var exp = Json.AsDict(c["expect"]);
                Check(name + ".stage", exp["stage"], r.Stage.ToString());
                Check(name + ".why", exp["why"], r.Why);
                Check(name + ".kind", exp["kind"], kind.ToString());

                // Determinism law: identical inputs -> identical stage AND kind
                // (a popup reopen must never flip the tree variant).
                var r2 = OfficerDoctrine.EmergencyFor(skill, view, self, facts, null, cards, false);
                Check(name + ".determinism", kind.ToString(),
                      OfficerDoctrine.TreeKindFor(skill, self.Supporter, r2.Stage).ToString());

                // Why-line law: a proven emergency ALWAYS names its reason,
                // stage None NEVER carries one (the popup why-line renders
                // exactly when this holds - no half-rendered lines).
                Check(name + ".why_law", r.Stage != OfficerEmergency.None,
                      !string.IsNullOrEmpty(r.Why));
            }

            // ---- hand-pinned LAW matrix: skill x supporter x emergency ----
            // S1 (skill < MinSkill == 2) is Legacy on EVERY row: the codex
            // branch only speaks from S2 up (Thomas 04.11.). Non-supporters
            // (troops, DG075/DG078 id wall) are Legacy on EVERY row. A
            // supporter splits on the PROVEN emergency: None -> Keep,
            // E1..E4 -> Emergency (old tree + why-line).
            foreach (var sk in new[] { 1, 2, 3, 4, 5 })
                foreach (bool sup in new[] { true, false })
                    foreach (var em in new[] { OfficerEmergency.None, OfficerEmergency.E1,
                                               OfficerEmergency.E2, OfficerEmergency.E3,
                                               OfficerEmergency.E4 })
                    {
                        string want = sk < 2 ? "Legacy"
                            : (!sup ? "Legacy"
                                    : (em == OfficerEmergency.None ? "Keep" : "Emergency"));
                        Check("matrix.s" + sk + (sup ? ".sup" : ".plain") + "." + em,
                              want, OfficerDoctrine.TreeKindFor(sk, sup, em).ToString());
                    }
        }
    }
}
