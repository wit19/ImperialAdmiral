// P43 PERSONAL reward-card channel law (Thomas rulings 21.11.). One curated
// channel per card from the CLOSED vocabulary (asset "rewards", schema
// tacai.hero_cards/6, curated in wiki_data/tools/curate_hero_cards.py
// REWARD_EFFECTS - every row carries its printed zitat as evidence). The
// section pins FOUR laws:
//  1) READER law (hand-pinned): all 21 personal ids resolve, each to its
//     curated kanal; an unknown id reads null (the null law).
//  2) FAIL-CLOSED reader (hand-pinned, synthetic doc rows): unknown kanal,
//     weapon channel without printed pool, fraction channel without a
//     fraction - every malformed row stays fully INERT.
//  3) NULL law (hand-pinned relation): a hero holding ONLY nur_rekord/
//     unknown cards has bit-identical eliminate ThreatParts to his bare
//     profile - knowledge is recorded, never weighted (Null-Law).
//  4) MODEL outputs (--record discipline): the waffen_pool card (personal15
//     at Diala) and the fraction delta (personal18 on ANY holder - the card
//     follows its holder, per-hero code does not exist). Deltas stated as
//     exact rationals are hand-pinned relations; the absolute ThreatParts
//     of the weapon vector is measured, reviewed once, pinned.
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecPersonalRewards()
        {
            var v = LoadJson("personal_rewards.json");
            if (HeroCards.DocOrNull() == null)
                throw new InvalidOperationException("hero-card asset not primed");
            var table = HeroCards.Load();
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);

            // ---- 1) reader law over the SHIPPED asset (hand-pinned) ----
            foreach (var raw in Json.AsList(v["reader"]))
            {
                var row = Json.AsDict(raw);
                var id = Json.AsStr(row["id"]);
                var want = Json.AsStr(row["kanal"]);
                var gotRow = table.Reward(id);
                var got = gotRow == null ? "" : gotRow.Kanal;
                Check("reward.reader[" + id + "]", want, got);
            }

            // ---- 2) fail-closed reader: synthetic junk rows (hand-pinned)
            var junkDoc = new Dictionary<string, object>
            {
                { "heroes", new List<object>() },
                { "items", new List<object>() },
                { "rewards", Json.AsList(v["junk"]) },
            };
            var junkTable = HeroCardTable.FromDoc(junkDoc);
            foreach (var raw in Json.AsList(v["junk"]))
            {
                var row = Json.AsDict(raw);
                var id = Json.AsStr(row["id"]);
                Check("reward.junk[" + id + "]", null, junkTable.Reward(id));
            }

            // profiles + loadout seam, exactly like the live bridge feeds it
            var assetDoc = HeroProfiles.DocOrNull()
                ?? throw new InvalidOperationException("asset doc not primed");
            var baseProfiles = HeroProfiles.ProfilesFromDoc(assetDoc);
            var refs = new Dictionary<string, object>
            {
                { "items", LoadLangJson("En", "CampaignData/items.json") },
                { "skills", LoadLangJson("En", "CampaignData/skills.json") },
            };
            HeroThreatProfile With(string hero, List<object> rewardIds)
            {
                return With2(hero, null, rewardIds);
            }
            HeroThreatProfile With2(string hero, List<object> itemIds, List<object> rewardIds)
            {
                var entries = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "hero", hero },
                        { "items", itemIds },
                        { "skills", null },
                        { "rewards", rewardIds },
                    },
                };
                return HeroProfiles.ApplyCampaignLoadouts(
                    baseProfiles, entries, refs)[hero];
            }
            Rational Threat(string hero, HeroThreatProfile prof, string posture)
            {
                var t = HeroThreatModelV2.Threat(HeroCards.For(hero), table, prof,
                                                 false, posture, null);
                return t ?? Rational.Zero;
            }
            HeroThreatModelV2.ThreatParts? Parts(string hero, List<object> rewardIds)
            {
                var card = HeroCards.For(hero);
                var prof = rewardIds == null ? baseProfiles[hero] : With(hero, rewardIds);
                return HeroThreatModelV2.PartsOf(card, table, prof, false, "eliminate", null);
            }

            // ---- 3) NULL law (hand-pinned relation, bit-identical) ----
            foreach (var rawN in Json.AsList(v["null_law"]))
            {
                var nc = Json.AsDict(rawN);
                var nm = Json.AsStr(nc["name"]);
                var hero = Json.AsStr(nc["hero"]);
                var ids = Json.AsList(nc["rewards"]);
                var baseP = Parts(hero, null);
                var withP = Parts(hero, ids);
                Check("reward.null[" + nm + "].damage_ev",
                      baseP?.DamageEv.ToString(), withP?.DamageEv.ToString());
                Check("reward.null[" + nm + "].best_pool",
                      baseP.HasValue ? string.Join("", baseP.Value.BestPool) : null,
                      withP.HasValue ? string.Join("", withP.Value.BestPool) : null);
                Check("reward.null[" + nm + "].bonus",
                      baseP?.Bonus.ToString(), withP?.Bonus.ToString());
            }

            // ---- 4a) fraction delta relation (hand-pinned exact rational)
            // The card follows WHOEVER holds it: same delta on Han as on Fenn.
            foreach (var rawD in Json.AsList(v["delta"]))
            {
                var dc = Json.AsDict(rawD);
                var nm = Json.AsStr(dc["name"]);
                var hero = Json.AsStr(dc["hero"]);
                var rid = Json.AsStr(dc["reward"]);
                var want = Rational.Parse(Json.AsStr(dc["expect_delta"]));
                var a = Parts(hero, null);
                var b = Parts(hero, new List<object> { rid });
                if (!a.HasValue || !b.HasValue)
                {
                    Check("reward.delta[" + nm + "].parts", "present",
                          "null - hero not in the shipped card asset?");
                    continue;
                }
                Check("reward.delta[" + nm + "]", want.ToString(),
                      (b.Value.Bonus - a.Value.Bonus).ToString());
            }

            // ---- 4c) P44 health-delta law (T. ruling 21.11., hand-pinned)
            // factor = H/(H+delta) from printed facts only; the eliminate
            // AND attrition threat scale by exactly that rational, the
            // runner postures (protect/stall) must stay bit-identical, and
            // a carrier reward / cardless hero must give factor 1.
            foreach (var rawH in Json.AsList(v.ContainsKey("health_law") ? v["health_law"] : new List<object>()))
            {
                var hc = Json.AsDict(rawH);
                var nm = Json.AsStr(hc["name"]);
                var hero = Json.AsStr(hc["hero"]);
                var items = hc.ContainsKey("items") ? Json.AsList(hc["items"]) : null;
                var rewards = hc.ContainsKey("rewards") ? Json.AsList(hc["rewards"]) : null;
                var want = Rational.Parse(Json.AsStr(hc["expect_factor"]));
                var bareP2 = With2(hero, null, null);
                var withP2 = With2(hero, items, rewards);
                Check("reward.health[" + nm + "].factor", want.ToString(),
                      HeroThreatModelV2.HealthKillFactor(
                          HeroCards.For(hero), table, withP2).ToString());
                foreach (var post in new[] { "eliminate", "attrition" })
                {
                    var tBare = Threat(hero, bareP2, post);
                    var tWith = Threat(hero, withP2, post);
                    Check("reward.health[" + nm + "]." + post,
                          (tBare * want).ToString(), tWith.ToString());
                }
                var pBare = Threat(hero, bareP2, "protect");
                var pWith = Threat(hero, withP2, "protect");
                Check("reward.health[" + nm + "].protect_inert",
                      pBare.ToString(), pWith.ToString());
            }

            // ---- 4b) model outputs (record discipline) ----
            foreach (var rawM in Json.AsList(v["model"]))
            {
                var mc = Json.AsDict(rawM);
                var nm = Json.AsStr(mc["name"]);
                var hero = Json.AsStr(mc["hero"]);
                var p = Parts(hero, Json.AsList(mc["rewards"]));
                var bp = Parts(hero, null);
                var got = new Dictionary<string, object>
                {
                    { "damage_ev", p?.DamageEv.ToString() },
                    { "best_pool", p.HasValue ? string.Join("", p.Value.BestPool) : null },
                    { "energy", p?.Energy.ToString() },
                    { "bonus", p?.Bonus.ToString() },
                    { "basis_damage_ev", bp?.DamageEv.ToString() },   // measured baseline
                };
                if (Record) { recorded[nm] = got; continue; }
                if (!mc.ContainsKey("expect"))
                {
                    Check(nm + ".expect", "present", "missing - run with --record");
                    continue;
                }
                Check(nm + ".expect", Json.AsDict(mc["expect"]), got);
            }
            if (Record)
            {
                var path = System.IO.Path.Combine(FIX, "personal_rewards_record.json");
                System.IO.File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }
    }
}
