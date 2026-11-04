// O-A supporter-codex vectors: officer_doctrine.json pins the emergency
// cascade of concept offizier_supporter §2 (OfficerDoctrine.EmergencyFor) on
// the REAL WorldEventFeed.Append + WorldEventVector.Project path (same
// fail-loud discipline as hero_ledger/reaction_order). expect blocks were
// recorded once via `dotnet run -- <fixtures> --record` and reviewed (record
// discipline); the LAW blocks at the bottom are hand-pinned ground truth
// (S1 silence, card-identity flags, legacy-ctor neutrality - the law IS the
// expectation, a recorded output would only mirror code).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecOfficerDoctrine()
        {
            var v = LoadJson("officer_doctrine.json");

            // ---- card identity: printed token -> Supporter (hand-pinned) ----
            var cards = new Dictionary<string, DeploymentCard>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(v["cards"]))
            {
                var c0 = DeploymentCard.FromRecord(Json.AsDict(raw));
                cards[c0.Id] = c0;
            }
            // Supporters: the printed officer trio + token-carrying imports
            // (inheritance is keyword-driven, never name-driven).
            foreach (var id in new[] { "DG004", "DG005", "DG006", "CUSTOM_FAN", "CUSTOM_FAN_MEL" })
                Check("cards.supporter." + id, true, cards[id].Supporter);
            // Field-commander exceptions (Thomas-Gesetz 04.11.) + plain troops:
            // DG075 = Kayn Somos, DG078 = General Sorin (shipped-name truth;
            // the concept text has the pair swapped - ids exclude, names never).
            foreach (var id in new[] { "DG075", "DG078", "DG021", "DG009", "DG046" })
                Check("cards.supporter." + id, false, cards[id].Supporter);

            var cases = Json.AsList(v["cases"]);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                int skill = (int)Json.AsLong(c["skill"]);
                bool catalogNull = Json.AsLong(Json.Get(c, "catalog_null", 0L)) == 1;
                bool direct = Json.AsLong(Json.Get(c, "direct_threat", 0L)) == 1;
                string threat = c.ContainsKey("threat_hero") && !(c["threat_hero"] is null)
                    ? Json.AsStr(c["threat_hero"]) : null;
                var selfId = Json.AsStr(c["self"]);

                // THE REAL feed path (Append validates kind + payload).
                var feed = new WorldEventFeed("vector|" + name);
                AppendFeedEvents(feed, c["events"]);
                var view = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());

                var rows = Json.AsList(c["groups"]);
                var facts = FactsFromJson(c["groups"]);
                // O-A law: the adapter fills Supporter from the catalog card;
                // a row may override explicitly (fan import without a catalog
                // entry - the Bridge:642 synthetic path reads the printed
                // lane there; the fixture models that with an explicit flag).
                for (int gi = 0; gi < rows.Count; gi++)
                {
                    var g = Json.AsDict(rows[gi]);
                    DeploymentCard card;
                    bool sup = g.ContainsKey("supporter")
                        ? Json.AsLong(g["supporter"]) == 1
                        : cards.TryGetValue(Json.AsStr(g["card"]), out card) && card.Supporter;
                    facts[gi].Supporter = sup;
                }
                GroupFacts self = null;
                foreach (var f in facts)
                    if (f.GroupId == selfId) self = f;
                if (self == null)
                { Fail(name + ".self", "self group '" + selfId + "' not in groups"); continue; }

                var catalog = catalogNull ? null : cards;
                var r1 = OfficerDoctrine.EmergencyFor(skill, view, self, facts, threat, catalog, direct);
                // Determinism law: same inputs -> identical stage AND why-line
                // (checked on EVERY case; the view rebuilds bit-identical).
                var view2 = WorldEventVector.Project(feed.Events, "vector|" + name, feed.Hash());
                var r2 = OfficerDoctrine.EmergencyFor(skill, view2, self, facts, threat, catalog, direct);
                Check(name + ".determinism_stage", r1.Stage.ToString(), r2.Stage.ToString());
                Check(name + ".determinism_why", r1.Why, r2.Why);

                // S1 law (hand-pinned): below S2 the codex NEVER speaks -
                // same inputs, same case, every emergency condition intact.
                var rS1 = OfficerDoctrine.EmergencyFor(1, view, self, facts, threat, catalog, direct);
                Check(name + ".s1_stage", "None", rS1.Stage.ToString());
                Check(name + ".s1_why", "", rS1.Why);

                // Legacy-ctor law (hand-pinned bit-identity): a GroupFacts
                // built with the pre-O-A constructor (Supporter defaults to
                // false, via the untouched FactsFromJson helper) is inert -
                // no old call site can trip the codex by accident.
                var legacyFacts = FactsFromJson(c["groups"]);
                GroupFacts legacySelf = null;
                foreach (var f in legacyFacts)
                    if (f.GroupId == selfId) legacySelf = f;
                var rLegacy = OfficerDoctrine.EmergencyFor(skill, view, legacySelf, legacyFacts,
                    threat, catalog, direct);
                Check(name + ".legacy_ctor_stage", "None", rLegacy.Stage.ToString());

                var got = new Dictionary<string, object>
                {
                    { "stage", r1.Stage.ToString() },
                    { "why", r1.Why },
                };
                if (Record)
                {
                    recorded[name] = got;
                    if (c.ContainsKey("expect"))
                    {
                        var exp0 = Json.AsDict(c["expect"]);
                        Check(name + ".recorded.stage", exp0["stage"], got["stage"]);
                        Check(name + ".recorded.why", exp0["why"], got["why"]);
                    }
                    continue;
                }
                if (!c.ContainsKey("expect"))
                { Check(name + ".expect", "present", "missing - run with --record"); continue; }
                var exp = Json.AsDict(c["expect"]);
                Check(name + ".stage", exp["stage"], got["stage"]);
                Check(name + ".why", exp["why"], got["why"]);
            }

            if (Record)
            {
                var path = Path.Combine(FIX, "officer_doctrine_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
