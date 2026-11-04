// M6 overlord-focus vectors: overlord_focus.json pins the DETERMINISTIC
// target ranking of the OverlordFocus layer (aim marks > objective actor >
// counter chip law > threat economy > wounded > M10a campaign investment >
// reference proxy > stable tie chain). M11b: the threat basis is each hero's
// OWN printed hero card (tactical_hero_cards.json asset, primed from the
// shipped file) — the M10b mixed-basis purity case is rewritten onto the
// single common basis; rows outside the 21-hero roster keep the curated
// fallback and are labelled "economy". M11d: optional per-hero strain_markers
// add the capped +8 pressure to the econ key when the printed buffer is full
// (m11d_* cases; the strain_druck expect key appears only on firing rows, so
// every pre-M11d canon is byte-identical). Like world_feed.json this is a
// C#-side canonical vector (the sim has no app feed); its W components are
// sim-vectored via hero_profiles.json / hero_w.json. Expect blocks were
// recorded once with `--record`, reviewed, then pinned - never hand-written.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecOverlordFocus()
        {
            var v = LoadJson("overlord_focus.json");
            var cases = Json.AsList(v["cases"]);
            var assetDoc = HeroProfiles.DocOrNull()
                ?? throw new InvalidOperationException("asset doc not primed");
            var profiles = HeroProfiles.ProfilesFromDoc(assetDoc);
            var recorded = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var raw in cases)
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var posture = Json.AsStr(c["posture"]);
                string counter = null;
                if (c.ContainsKey("counter") && c["counter"] != null)
                    counter = Json.AsStr(c["counter"]);
                // M11b: optional case-level "defense" - the defense dice of
                // the engaging imperial figure ("Black"/"White", printed
                // deployment-card data). Absent -> no figure engaged yet.
                List<string> defender = null;
                if (c.ContainsKey("defense") && c["defense"] != null)
                    defender = Json.AsList(c["defense"]).ConvertAll(o => Json.AsStr(o));
                // R-D (concept v1.2 §2.3): optional case-level "reaction" -
                // the activation facts fed to OverlordFocus.ReactionHeroFor.
                // The key is computed THROUGH THE REAL HELPER (never a
                // literal), so the key laws and their rank outcome are pinned
                // together in the same expect blocks (reviewer-replay law:
                // one vector pins bug and rule together). No "reaction" block
                // = null key = the old path - every pre-R-D case replays
                // bit-identical (the fixture diff stays additive-only).
                string reactionKey = null;
                if (c.ContainsKey("reaction") && c["reaction"] != null)
                {
                    var ri = Json.AsDict(c["reaction"]);
                    reactionKey = OverlordFocus.ReactionHeroFor(
                        FocusReactionView(ri), Json.AsStr(ri["group_id"]),
                        ri.ContainsKey("pattern") && ri["pattern"] != null ? Json.AsStr(ri["pattern"]) : null,
                        (int)Json.AsLong(ri["skill"]));
                    if (ri.ContainsKey("key"))
                    {
                        var wantKey = ri["key"] == null ? "" : Json.AsStr(ri["key"]);
                        Check(name + ".key", wantKey, reactionKey ?? "");
                    }
                }

                List<FocusCandidate> Build()
                {
                    // M10a: a case hero may carry a campaign "loadout"
                    // ({items, skills}); it is merged into the curated/
                    // placeholder profile EXACTLY like the live bridge's
                    // ProfileFor does (ApplyCampaignLoadouts on the app's
                    // own CampaignData items/skills tables).
                    var merged = profiles;
                    var loEntries = new List<object>();
                    foreach (var rawH in Json.AsList(c["heroes"]))
                    {
                        var h = Json.AsDict(rawH);
                        if (h.ContainsKey("loadout") && h["loadout"] != null)
                        {
                            var lo = Json.AsDict(h["loadout"]);
                            loEntries.Add(new Dictionary<string, object>
                            {
                                { "hero", Json.AsStr(h["hero"]) },
                                { "items", Json.Get(lo, "items", null) },
                                { "skills", Json.Get(lo, "skills", null) },
                            });
                        }
                    }
                    if (loEntries.Count > 0)
                    {
                        var refs = new Dictionary<string, object>
                        {
                            { "items", LoadLangJson("En", "CampaignData/items.json") },
                            { "skills", LoadLangJson("En", "CampaignData/skills.json") },
                        };
                        merged = HeroProfiles.ApplyCampaignLoadouts(
                            profiles, loEntries, refs);
                    }

                    var list = new List<FocusCandidate>();
                    foreach (var rawH in Json.AsList(c["heroes"]))
                    {
                        var h = Json.AsDict(rawH);
                        HeroThreatProfile p = null;
                        if (h.ContainsKey("profile") && h["profile"] != null)
                            merged.TryGetValue(Json.AsStr(h["profile"]), out p);
                        list.Add(new FocusCandidate
                        {
                            HeroId = Json.AsStr(h["hero"]),
                            SquadRow = Json.AsInt(h["row"]),
                            Profile = p,
                            Wounded = Json.AsBool(h["wounded"]),
                            Marks = (int)Json.AsLong(h["marks"]),
                            MarkedRound = (int)Json.AsLong(h["marked_round"]),
                            AttackDice = (int)Json.AsLong(h["attack_dice"]),
                            Health = Json.AsInt(h["health"]),
                            // M10b: optional per-hero flag (default false).
                            ObjectiveActor = h.ContainsKey("objective_actor")
                                && Json.AsBool(h["objective_actor"]),
                            // Player-19: optional engagement round (the live
                            // bridge folds feed actor-round + strike-round
                            // into it). ABSENT -> 0 keeps every pre-Player-19
                            // case bit-identical.
                            EngagedRound = h.ContainsKey("engaged_round")
                                ? (int)Json.AsLong(h["engaged_round"]) : 0,
                            // M11b: the printed hero card by NAME (same key
                            // the live bridge uses); rows outside the 21-hero
                            // roster (fixture heroes) stay on the fallback.
                            Card = HeroCards.For(Json.AsStr(h["hero"])),
                            // M11d: optional per-hero strain input. ABSENT =
                            // the live-app state (source cannot see strain ->
                            // zero influence; every pre-M11d case stays
                            // byte-identical). The buffer itself is NEVER a
                            // fixture input - Rank() computes it from the
                            // hero's shipped card (fairness canon).
                            StrainMarkers = h.ContainsKey("strain_markers") && h["strain_markers"] != null
                                ? (int?)Json.AsLong(h["strain_markers"]) : null,
                            AvailableXp = h.ContainsKey("xp_amount") && h["xp_amount"] != null
                                ? (int?)Json.AsLong(h["xp_amount"]) : null,
                            // M11h: "runden" = completed hero activations on
                            // the public feed (mirrors HeroView.Activations,
                            // the live bridge source). ABSENT -> 0: every
                            // pre-M11h case replays at r = 0 - prior level 0,
                            // no dict key - so the whole existing canon must
                            // pass bit-identical (Canon test, not a new rule).
                            RoundsCompleted = h.ContainsKey("runden") && h["runden"] != null
                                ? (int?)Json.AsLong(h["runden"]) : 0,
                        });
                    }
                    return list;
                }

                // replay twice: the layer is a pure function (determinism law)
                var d1 = OverlordFocus.Rank(Build(), posture, counter, defender, 0, null, null, reactionKey);
                var d2 = OverlordFocus.Rank(Build(), posture, counter, defender, 0, null, null, reactionKey);
                Check(name + ".replay",
                      d1.Select(x => (object)x.AsDict()).ToList(),
                      d2.Select(x => (object)x.AsDict()).ToList());

                // fairness canon: ranking is a PERMUTATION of the input,
                // never a filter (checked for every case, always)
                var inIds = Json.AsList(c["heroes"]).Select(Json.AsDict)
                    .Select(h => Json.AsStr(h["hero"])).OrderBy(x => x, StringComparer.Ordinal)
                    .Select(x => (object)x).ToList();
                var outIds = d1.Select(x => x.HeroId).OrderBy(x => x, StringComparer.Ordinal).ToList();
                Check(name + ".permutation", inIds, outIds);

                var got = d1.Select(x => (object)x.AsDict()).ToList();
                if (Record) { recorded[name] = got; continue; }

                if (!c.ContainsKey("expect"))
                {
                    Check(name + ".expect", "present", "missing - run with --record");
                    continue;
                }
                Check(name + ".rank", Json.AsList(c["expect"]), got);
            }
            // R-D hand-pinned laws (ground truth = the law itself, same
            // pattern as the reaction_order.json section). The fixture cases
            // pin the Guardhold (named) leg of the WACH exception; these pin
            // the OTHER legs and the boundaries the cases cannot reach:
            //  - guard-doctrine leg: a rolled guard pattern suppresses the
            //    key even where nothing is named (R-C anchor law reused),
            //  - a never-shot group and an OLD-round shot stay null (the key
            //    is consumed with this activation - no carry-over),
            //  - S1 stays blind (disclosed handicap, R1's floor exactly).
            Check("law.guard_pattern_suppresses", "",
                  OverlordFocus.ReactionHeroFor(FocusReactionView(Ri(4, "Defeat the rebels.",
                        "DG031", "Patrol", "Gaarkhan", 4)), "DG031", "guard", 2) ?? "");
            Check("law.no_shot_key_null", "",
                  OverlordFocus.ReactionHeroFor(FocusReactionView(Ri(4, "",
                        "DG031", "Patrol", null, 0)), "DG031", "hunter", 2) ?? "");
            Check("law.old_shot_key_null", "",
                  OverlordFocus.ReactionHeroFor(FocusReactionView(Ri(4, "",
                        "DG031", "Patrol", "Gaarkhan", 3)), "DG031", "hunter", 2) ?? "");
            Check("law.s1_blind_key_null", "",
                  OverlordFocus.ReactionHeroFor(FocusReactionView(Ri(4, "",
                        "DG031", "Patrol", "Gaarkhan", 4)), "DG031", "hunter", 1) ?? "");
            // positive control (the helper must STAY able to speak - a
            // suppression-only law set goes green on an always-null bug):
            Check("law.plain_shot_key_fires", "Gaarkhan",
                  OverlordFocus.ReactionHeroFor(FocusReactionView(Ri(4, "Defeat the rebels.",
                        "DG031", "Patrol", "Gaarkhan", 4)), "DG031", "hunter", 2) ?? "");
            // R-D insertion rank laws (the card's core claim, pinned as
            // ground truth): "einzureihen NACH mark ... VOR counter".
            //  - mark stays king: a marked hero WITHOUT the key outranks
            //    the reaction hero who carries it;
            //  - reaktion pre-empts the counter tier: it outranks the M10b
            //    objective-actor head (the counter law's leading slot).
            Check("law.mark_stays_king_over_reaktion", "Plain",
                  OverlordFocus.Rank(new List<FocusCandidate>
                  {
                      new FocusCandidate { HeroId = "Plain", SquadRow = 0, Marks = 1, MarkedRound = 4 },
                      new FocusCandidate { HeroId = "Shooter", SquadRow = 1 },
                  }, "eliminate", null, null, 0, null, null, "Shooter")[0].HeroId);
            Check("law.reaktion_beats_objective_actor", "Shooter",
                  OverlordFocus.Rank(new List<FocusCandidate>
                  {
                      new FocusCandidate { HeroId = "Actor", SquadRow = 0, ObjectiveActor = true },
                      new FocusCandidate { HeroId = "Shooter", SquadRow = 1 },
                  }, "eliminate", null, null, 0, null, null, "Shooter")[0].HeroId);

            if (Record)
            {
                var path = Path.Combine(FIX, "overlord_focus_record.json");
                File.WriteAllText(path, DumpJson(recorded));
                Console.WriteLine("   (record) wrote " + path + " for " + recorded.Count + " cases");
            }
        }

        // R-D: the minimal feed view the ReactionHeroFor cases read - Round,
        // Objective and ONE GroupView row carrying the R-A shot attribution.
        // Built straight from the fixture fields (no event fold needed - the
        // helper is a pure reader of exactly these columns).
        static FeedWorldView FocusReactionView(Dictionary<string, object> ri)
        {
            var view = new FeedWorldView();
            view.Round = (int)Json.AsLong(ri["view_round"]);
            view.Objective = ri.ContainsKey("objective") && ri["objective"] != null
                ? Json.AsStr(ri["objective"]) : "";
            var gv = new FeedWorldView.GroupView { GroupId = Json.AsStr(ri["group_id"]) };
            if (ri.ContainsKey("display") && ri["display"] != null)
                gv.DisplayName = Json.AsStr(ri["display"]);
            if (ri.ContainsKey("shot_hero") && ri["shot_hero"] != null)
                gv.LastShotHero = Json.AsStr(ri["shot_hero"]);
            if (ri.ContainsKey("shot_round") && ri["shot_round"] != null)
                gv.LastShotRound = (int)Json.AsLong(ri["shot_round"]);
            view.Groups.Add(gv);
            return view;
        }

        // hand-pinned-law builder (same field names as the fixture block).
        static Dictionary<string, object> Ri(int viewRound, string objective,
                                            string groupId, string display,
                                            string shotHero, int shotRound)
            => new Dictionary<string, object>
            {
                { "view_round", (long)viewRound },
                { "objective", objective },
                { "group_id", groupId },
                { "display", display },
                { "shot_hero", (object)shotHero },
                { "shot_round", (long)shotRound },
            };
    }
}
