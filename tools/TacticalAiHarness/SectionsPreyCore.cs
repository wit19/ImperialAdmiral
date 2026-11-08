// P38 (card t_11ebb843, playtest Empire3) section: the NEUTRALE-BEUTE core
// law. Four laws pinned:
//  (A) FEED SCOPE - the REAL Empire/EMPIRE3.json object rows (mined by the
//      gate 'gen' mode through the producer mirror: true GUIDs/sections read,
//      never hardcoded) replay through WorldEventVector.Project: nine
//      bystander prey rows (token + jagt + person wiring), the Protester
//      GUID-set equals the mined set, and the Null-Law twin (entityType key
//      stripped = every pre-P38 row) yields ZERO bystanders and zero canon
//      segments - the TryObjectTarget path stays P36-verbatim there.
//  (B) BYSTANDER INTENSION - the person-vocabulary law over synthetic rows,
//      expectations HAND-PINNED case by case (the census extensional golden:
//      claim/rescue/defeat/reveal EN + Anspruch/retten/befreien/besiegen/
//      vernichten/enthüllen DE ride both lanes; bare "destroy"/"zerstören"
//      is the DEVICE stem; the entityType leg locks doors/terminals/
//      highlights/crates out; unknown type "" is never a bystander).
//  (C) PREY QUOTA - the golden rerun vectors (prey_core.json quota_vectors,
//      produced by the gate 'gen' mode, cross-checked by the INDEPENDENT
//      Python twin tools/p38_prey_twin.py): k in [1..ceil(n/2)], never all
//      (k < n at n>=2), n=0 => no quota law at all, session salt moves the
//      dice, seed-stability inside a round (Gleichlauf), chosen = first k of
//      the seed ranking.
//  (D) BEDROHUNGS-GATTER - PreyThreat.ThreatSectionOf over HeroDeployed fact
//      anchors (the SAME feed knowledge the focus rule uses): unit section
//      first, prey section second, multi-entry sets retract, no hero => no
//      threat. Expectations pinned from the gate 'gen' mode (golden rerun).
// Plus the Hop-D popup mirror: apPrey* keys 11/11 (De/En real, 9 byte-exact
// EN copies) and the exact AppendPreyLines rendering seam (k/n split, TF
// fill, brace guard, missing key fail-closed, relative threat fallback).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecPreyCore()
        {
            var v = LoadJson("prey_core.json");

            // =============== (A) EMPIRE3 feed: scope + Null-Law ============
            var em3 = Json.AsDict(v["em3"]);
            var events = BuildEvents(Json.AsList(em3["events"]));
            foreach (var e in events)
            {
                try { WorldEventFeed.Validate(e.Kind, e.Payload); }
                catch (Exception vx) { Check("em3.vocabulary[" + e.Kind + "]", "public", vx.Message); }
            }
            var v1 = WorldEventVector.Project(events, "prey|EMPIRE3");
            var v2 = WorldEventVector.Project(events, "prey|EMPIRE3");
            Check("em3.replay_canon", v1.Canon(), v2.Canon());
            Check("em3.replay_hash", v1.HistoryHash, v2.HistoryHash);

            Check("em3.rows", 9L, (long)v1.Objects.Count);
            var hunt = v1.HuntObjects();
            Check("em3.hunt_all_jagt", 9L, (long)hunt.Count);
            var prey = hunt.FindAll(o => o.Bystander);
            Check("em3.prey", Json.AsLong(em3["expectPrey"]), (long)prey.Count);
            Check("em3.prey_all_token", true, prey.All(o => o.EntityType == "token"));
            Check("em3.prey_all_jagt", true, prey.All(o => o.Polaritaet == "jagt" && o.Basis == "P1"));
            var pinGuids = Json.AsList(em3["protesterGuids"]).Select(Json.AsStr)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();
            var liveGuids = prey.Select(o => o.Guid)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();
            Check("em3.protester_guids", string.Join(",", pinGuids), string.Join(",", liveGuids));
            // the emitter's bystander set must equal the CONSUMER's scope test
            Check("em3.scope_equals_protesters", true,
                  prey.All(o => NeutralPrey.IsInScope(o)));

            // data-existence: the segments appear on the carried rows
            var canon = v1.Canon();
            Check("em3.canon_has_token", true, canon.Contains("|token"));
            Check("em3.canon_has_bystander", true, canon.Contains("|bystander"));

            // Null-Law twin: strip the entityType key (every pre-P38 row
            // shape) -> no bystander verdict, no canon segment, the
            // TryObjectTarget path runs the P36 law bit-identically.
            var strippedEvents = new List<WorldEvent>();
            int seq = 1;
            foreach (var e in events)
            {
                var p = new Dictionary<string, object>(e.Payload);
                p.Remove("entityType");
                strippedEvents.Add(new WorldEvent(e.Kind, e.Round, p) { Seq = seq++ });
            }
            var v0 = WorldEventVector.Project(strippedEvents, "prey|EMPIRE3");
            Check("null_law.rows_kept", 9L, (long)v0.Objects.Count);
            Check("null_law.no_bystander", 0L, (long)v0.Objects.Values.Count(o => o.Bystander));
            var sc = v0.Canon();
            Check("null_law.canon_no_token_seg", false, sc.Contains("|token"));
            Check("null_law.canon_no_bystander_seg", false, sc.Contains("|bystander"));

            // ============ (B) bystander intension (hand-pinned) ============
            BystanderCase("protester_real", "token", "claim;defeat", "", true);
            BystanderCase("en_claim", "token", "claim", "", true);
            BystanderCase("en_rescue", "token", "rescue the prisoner", "", true);
            BystanderCase("en_defeat", "token", "defeat", "", true);
            BystanderCase("en_reveal", "token", "reveal", "", true);
            // bare "destroy" is the DEVICE stem (47/47 measured, census):
            BystanderCase("en_destroy_is_device", "token", "destroy", "", false);
            BystanderCase("de_claim", "token", "Anspruch erheben", "", true);
            BystanderCase("de_retten", "token", "retten", "", true);
            BystanderCase("de_befreien", "token", "befreien", "", true);
            BystanderCase("de_besiegen", "token", "besiegen", "", true);
            BystanderCase("de_vernichten", "token", "vernichten", "", true);
            BystanderCase("de_enthuellen", "token", "enth\u00fcllen", "", true);
            BystanderCase("de_zerstoeren_is_device", "token", "zerst\u00f6ren", "", false);
            // eventNames lane (the wired event name, not only the button):
            BystanderCase("eventnames_lane", "token", "", "defeat protester", true);
            BystanderCase("eventnames_device", "token", "", "destroy barricade", false);
            // the entityType leg locks everything else out (measured:
            // Northern Door=door, Terminal1=terminal, Warship=highlight):
            BystanderCase("door_locked", "door", "claim", "", false);
            BystanderCase("terminal_locked", "terminal", "rescue", "", false);
            BystanderCase("highlight_locked", "highlight", "defeat", "", false);
            BystanderCase("crate_locked", "crate", "claim", "", false);
            BystanderCase("tile_locked", "tile", "defeat", "", false);
            BystanderCase("dp_locked", "deploymentpoint", "defeat", "", false);
            // Null-Law: unknown/absent type is NEVER a bystander.
            BystanderCase("no_type_null_law", "", "claim", "", false);
            // scope leg 1 (POLAR prey line): a person-wired row that the
            // ladder does NOT classify as jagt is bystander but OUT of the
            // application scope - the OTHER11 refugee shape (polaritaet
            // neutral, no claim sentence in the objective):
            {
                var o = new FeedWorldView.MissionObjectFact
                { EntityType = "token", Buttons = "defeat", Polaritaet = "neutral" };
                Check("scope.neutral_bystander_out_of_scope", false, NeutralPrey.IsInScope(o));
                Check("scope.bystander_still_person", true, NeutralPrey.IsBystander(o));
                var o2 = new FeedWorldView.MissionObjectFact
                { EntityType = "token", Buttons = "defeat", Polaritaet = "schuetzt" };
                Check("scope.schuetzt_out_of_scope", false, NeutralPrey.IsInScope(o2));
            }

            // ================== (C) prey quota vectors =====================
            var cohorts = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var raw in Json.AsList(v["quota_cohorts"]))
            {
                var c = Json.AsDict(raw);
                cohorts[Json.AsStr(c["name"])] = c;
            }
            int vectorCount = 0, nonNull = 0;
            string sagaR1 = "", saltR1 = "";
            foreach (var rawPin in Json.AsList(v["quota_vectors"]))
            {
                var pin = Json.AsStr(rawPin);
                // pin = name|session|round|explain — name carries no '|',
                // round is the field before the explain (which may be empty);
                // parse from the cohort side instead of guessing pipe counts.
                var parts = pin.Split('|');
                var cname = parts[0];
                var cohort = cohorts[cname];
                var session = Json.AsStr(cohort["session"]);
                // rebuild the prefix and take the remainder as the pin:
                var round = parts[parts.Length - 2];
                int roundNo = int.Parse(round, CultureInfo.InvariantCulture);
                var prefix = cname + "|" + session + "|" + round + "|";
                Check("quota." + cname + ".r" + roundNo + ".prefix", true, pin.StartsWith(prefix));
                var pinExplain = pin.Substring(prefix.Length);
                var hs = PreyHunters(Json.AsList(cohort["members"]));
                var pr = PreyQuota.Compute(session, roundNo, hs);
                vectorCount++;
                Check("quota." + cname + ".r" + roundNo + ".explain", pinExplain,
                      pr == null ? "" : pr.Explain);
                if (pr == null)
                {
                    Check("quota." + cname + ".r" + roundNo + ".null_law", true, pinExplain.Length == 0);
                    continue;
                }
                nonNull++;
                // band law: k in [1..ceil(n/2)], n>=1; never all (k < n at n>=2)
                Check("quota." + cname + ".r" + roundNo + ".band", true,
                      pr.K >= 1 && pr.K <= pr.Cap && pr.Cap == (pr.N + 1) / 2);
                Check("quota." + cname + ".r" + roundNo + ".never_all", true,
                      pr.N < 2 || pr.K < pr.N);
                Check("quota." + cname + ".r" + roundNo + ".chosen_prefix", true,
                      pr.Chosen.Count == pr.K && pr.Chosen.SequenceEqual(pr.Ranking.Take(pr.K)));
                // Gleichlauf: a mid-round REPLAN re-runs the pure law and
                // lands on the SAME k and selection (seed constant in round).
                var again = PreyQuota.Compute(session, roundNo, PreyHunters(Json.AsList(cohort["members"])));
                Check("quota." + cname + ".r" + roundNo + ".rerun", pr.Explain, again.Explain);
                if (cname == "em3_n9" && roundNo == 1) sagaR1 = pr.Explain;
                if (cname == "salt" && roundNo == 1) saltR1 = again.Explain;
            }
            Check("quota.vectors_pinned", 20L, (long)vectorCount);
            Check("quota.non_null", 15L, (long)nonNull);
            // session salt moves the dice (same cohort, different key)
            Check("quota.salt_moves_dice", true, sagaR1.Length > 0 && saltR1.Length > 0 && sagaR1 != saltR1);
            // no-chase cohort: n=0 => the quota law does not exist at all
            Check("quota.no_chase_null", true,
                  PreyQuota.Compute("saga|EMPIRE3", 1,
                      PreyHunters(Json.AsList(cohorts["no_chase"]["members"]))) == null);

            // ================= (D) Bedrohungs-Gatter vectors ===============
            foreach (var raw in Json.AsList(v["threat_vectors"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var feed = new WorldEventFeed("vector|prey_threat|" + name);
                foreach (var evraw in Json.AsList(c["events"]))
                {
                    var p = Json.AsStr(evraw).Split('|');
                    feed.Append(p[0], int.Parse(p[3], CultureInfo.InvariantCulture),
                        new Dictionary<string, object>
                        { { "hero", p[1] }, { "section", p[2] }, { "dp", PreyDpOf(p[2]) } });
                }
                var space = SectionSpace.Project(feed.Events, null, null, null,
                                                 new SectionOptions { Refine = true });
                var got = PreyThreat.ThreatSectionOf(space, Json.AsStr(c["unitSection"]),
                                                     Json.AsStr(c["preySection"])) ?? "";
                Check("threat." + name, Json.AsStr(c["expect"]), got);
            }
            // no fact at all (null space) can never threaten - never a guess
            Check("threat.null_space", "", PreyThreat.ThreatSectionOf(null, "sec-a", "sec-b") ?? "");

            // ===================== langs: 11/11 keys =======================
            var lang = Json.AsDict(v["lang"]);
            var langs = Json.AsList(lang["langs"]).Select(Json.AsStr).ToList();
            Check("langs inventory", 11L, (long)langs.Count);
            var keys = Json.AsList(lang["keys"]).Select(Json.AsStr).ToList();
            Check("prey keys", 3L, (long)keys.Count);
            var secs = new Dictionary<string, Dictionary<string, object>>();
            foreach (var l in langs)
                secs[l] = Json.AsDict(Json.AsDict(LoadLangJson(l, "ui.json"))["uiTacticalAI"]);
            foreach (var l in langs)
                foreach (var k in keys)
                {
                    object val;
                    var have = secs[l].TryGetValue(k, out val);
                    Check("present[" + k + "|" + l + "]", true,
                          have && !string.IsNullOrEmpty(Json.AsStr(val)));
                }
            var deBlob = string.Join("\n", keys.Select(k => Json.AsStr(secs["De"][k])));
            var enBlob = string.Join("\n", keys.Select(k => Json.AsStr(secs["En"][k])));
            foreach (var f in Json.AsList(lang["de_contains"]).Select(Json.AsStr))
                Check("de_canon contains '" + f + "'", true, deBlob.Contains(f));
            foreach (var f in Json.AsList(lang["en_contains"]).Select(Json.AsStr))
                Check("en_canon contains '" + f + "'", true, enBlob.Contains(f));
            foreach (var l in langs)
            {
                if (l == "De" || l == "En") continue;
                foreach (var k in keys)
                    Check("en_copy[" + k + "|" + l + "]", Json.AsStr(secs["En"][k]), Json.AsStr(secs[l][k]));
            }

            // ============ popup mirror (exact AppendPreyLines seam) ========
            // The em3_n9 r1 vector decided k=5 of n=9 (pinned above) - the
            // popup renders it over the shipped keys, DE and EN.
            var q1 = PreyQuota.Compute("saga|EMPIRE3", 1, PreyHunters(Json.AsList(cohorts["em3_n9"]["members"])));
            var marker = q1.K.ToString(CultureInfo.InvariantCulture) + "/" +
                         q1.N.ToString(CultureInfo.InvariantCulture);
            foreach (var l in new[] { "De", "En" })
            {
                var rows = PreyPopupRows(Json.AsStr(secs[l]["apPreyQuotaLine"]),
                                         Json.AsStr(secs[l]["apPreyThreatLine"]),
                                         Json.AsStr(secs[l]["apPreyThreatRel"]),
                                         marker, "Kantine");
                Check("popup[" + l + "].quota_row", 2L, (long)rows.Count);
                Check("popup[" + l + "].no_brace", false, rows.Any(x => x.Contains("{")));
                Check("popup[" + l + "].cites_kn", true,
                      rows[0].Contains(q1.K.ToString(CultureInfo.InvariantCulture)) &&
                      rows[0].Contains(q1.N.ToString(CultureInfo.InvariantCulture)));
                Check("popup[" + l + "].threat_row", true,
                      rows[1].Contains("Kantine") || rows[1].Contains("section") || rows[1].Contains("Sektion"));
                // reopen determinism: identical render
                var rows2 = PreyPopupRows(Json.AsStr(secs[l]["apPreyQuotaLine"]),
                                          Json.AsStr(secs[l]["apPreyThreatLine"]),
                                          Json.AsStr(secs[l]["apPreyThreatRel"]), marker, "Kantine");
                Check("popup[" + l + "].reopen_identical", string.Join("\n", rows), string.Join("\n", rows2));
            }
            Check("popup.de_exact_phrase", true,
                  TFMirror(Json.AsStr(secs["De"]["apPreyQuotaLine"]), q1.K, q1.N)
                      .Contains("Objektjagd: 5 von 9 Einheiten"));
            // Null-Law: no markers => no rows (popup byte-identical);
            // missing key (pre-P38 asset state) => no row (fail-closed);
            // stale placeholder => brace guard drops the row.
            Check("popup.null_law_no_markers", 0L, (long)PreyPopupRows(
                Json.AsStr(secs["De"]["apPreyQuotaLine"]), Json.AsStr(secs["De"]["apPreyThreatLine"]),
                Json.AsStr(secs["De"]["apPreyThreatRel"]), "", "").Count);
            Check("popup.missing_key_no_row", 0L, (long)PreyPopupRows("", "", "", marker, "Kantine").Count);
            Check("popup.stale_placeholder_dropped", 0L, (long)PreyPopupRows(
                "Objektjagd: {0} von {1} {2} Einheiten", "", "", marker, "").Count);
            // relative fallback fires when the authored section name does not
            // inform (CleanSectionName verdict = no name): the bridge renders
            // apPreyThreatRel - never an id, never a coordinate.
            var relRows = PreyPopupRows(Json.AsStr(secs["De"]["apPreyQuotaLine"]),
                                        Json.AsStr(secs["De"]["apPreyThreatLine"]),
                                        Json.AsStr(secs["De"]["apPreyThreatRel"]),
                                        "", "sec-7", named: false);
            Check("popup.rel_fallback_when_no_name", true,
                  relRows.Count == 1 && relRows[0].Contains("derselben Sektion") && !relRows[0].Contains("sec-7"));
            // named threat row (informing authored name) uses apPreyThreatLine:
            var namedRows = PreyPopupRows("", Json.AsStr(secs["De"]["apPreyThreatLine"]),
                                          Json.AsStr(secs["De"]["apPreyThreatRel"]),
                                          "", "Kantine", named: true);
            Check("popup.named_threat_row", true,
                  namedRows.Count == 1 && namedRows[0].Contains("Bedrohung in Sektion Kantine"));
        }

        // ---------------- helpers (pure fixture plumbing, no law here) -----

        static void BystanderCase(string name, string entityType, string buttons,
                                  string eventNames, bool expect)
        {
            var o = new FeedWorldView.MissionObjectFact
            {
                EntityType = entityType, Buttons = buttons, EventNames = eventNames,
                Polaritaet = "jagt",
            };
            Check("bystander." + name, expect, NeutralPrey.IsBystander(o));
        }

        static List<PreyHunter> PreyHunters(List<object> rows)
        {
            var outp = new List<PreyHunter>();
            foreach (var raw in rows)
            {
                var d = Json.AsDict(raw);
                outp.Add(new PreyHunter
                {
                    CardId = Json.AsStr(d["id"]),
                    Pattern = Json.AsStr(Json.Get(d, "pattern", "")),
                });
            }
            return outp;
        }

        // The SAME synthetic dp the gate 'gen' mode emitted (identity key,
        // never consumed by these vectors - fixture plumbing, not law).
        static string PreyDpOf(string section)
        {
            char c = section.Length > 0 ? section[section.Length - 1] : 'f';
            if (c < 'a' || c > 'f') c = 'f';
            var s = new string(c, 4);
            return s + s + "-" + s + "-4" + s.Substring(0, 3) + "-8" + s.Substring(0, 3)
                 + "-" + s + s + s;
        }

        // Exact AppendPreyLines seam mirror (Bridge P38): quota marker "k/n"
        // int-parsed NumberStyles.None, TF fill, brace guard; threat: NAMED
        // row when the section carries an informing authored name (the
        // §8.4 CleanSectionName verdict, carried here as the named flag),
        // RELATIVE row when it does not - never an id, never a coordinate; a
        // missing key renders NOTHING (fail-closed).
        static List<string> PreyPopupRows(string quotaVal, string threatVal,
                                          string relVal, string marker,
                                          string threatSec, bool named = true)
        {
            var outp = new List<string>();
            if (!string.IsNullOrEmpty(marker))
            {
                var bits = marker.Split('/');
                int k, n;
                if (bits.Length == 2
                    && int.TryParse(bits[0], NumberStyles.None, CultureInfo.InvariantCulture, out k)
                    && int.TryParse(bits[1], NumberStyles.None, CultureInfo.InvariantCulture, out n)
                    && !string.IsNullOrEmpty(quotaVal))
                {
                    var q = TFMirror(quotaVal, k, n);
                    if (!string.IsNullOrEmpty(q) && q.IndexOf('{') < 0) outp.Add(q);
                }
            }
            if (!string.IsNullOrEmpty(threatSec))
            {
                string t = null;
                if (named && !string.IsNullOrEmpty(threatVal)) t = TFMirror(threatVal, threatSec);
                else if (!string.IsNullOrEmpty(relVal)) t = TFMirror(relVal);
                if (!string.IsNullOrEmpty(t) && t.IndexOf('{') < 0) outp.Add(t);
            }
            return outp;
        }

        // TFMirror: the shared UI.TF mirror lives in SectionsDutyPopup.cs
        // (same seam, one definition for both P37d and P38 popup checks).
    }
}
