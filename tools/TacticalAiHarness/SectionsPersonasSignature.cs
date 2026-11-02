// M6l SecPersonasSignature: the v2.1 signature-text canon (QA-FUND RR-B +
// C2, CONCEPT_personas_v2 Sec.2 R4 rows, Delta-0 Beschluss 11). Hand-pinned
// fixture (personas_signature.json, route like action_tree.json - NO
// --record; fragments authored from the Thomas-signed signaturText anchors
// and the card law). Zero new Core semantics: this section reads the SHIPPED
// ui.json of all 11 languages and pins the display contract:
//   1 presence    - all 7 keys (6 signatures + personaDelta0Hint) exist and
//                   are non-empty in every language (the 3 new signaturText
//                   channels of the asset finally have a field to fill -
//                   they were dead data before M6l)
//   2 canon De    - the German values carry the signed fragments
//   3 EN copies   - the 10 non-De languages match the En value byte-exact
//                   (M6c fleet law: De translated, 10 EN copies)
//   4 retired     - the 3 gift-era keys are GONE in every language (5b)
//   5 gift ban    - no signature/Delta-0 value cites gift wording, in any
//                   language (RR-B: 'Sturmtruppen verbilligt' must never
//                   come back)
//   6 C1 law      - no signature value pairs Bewegung/Angriff with 'oder'
//                   (the DG006 order-scope dispute is UNSIGNED - no shipped
//                   text may pre-decide it)
//   7 Delta-0     - the hint sentence is datenfrei: no placeholders, no
//                   digits; and it says replacement + equality
//   8 asset cross - the shipped En/tactical_personas.json carries EXACTLY
//                   the 6 signed signaturText keys, nothing else references
//                   a signature field.
// Canon is untouched: no pre-existing fixture or vector is read or changed;
// this section only ADDS checks on top.
using System;
using System.Collections.Generic;
using System.Globalization;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecPersonasSignature()
        {
            var v = LoadJson("personas_signature.json");
            var langs = Json.AsList(v["langs"]);
            Check("langs inventory", 11L, (long)langs.Count);
            var keys = Json.AsDict(v["keys"]);
            var retired = Json.AsList(v["retired_keys"]);
            var giftWords = Json.AsList(v["gift_words"]);
            var c1Pairs = Json.AsList(v["c1_forbidden_pairs"]);
            var noElite = Json.AsList(v["no_elite_claim_keys"]);
            var delta0 = Json.AsList(v["delta0_keys"]);

            // one parsed ui.json per language (section 'uiTacticalAI' only)
            var secs = new Dictionary<string, Dictionary<string, object>>();
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                var doc = Json.AsDict(LoadLangJson(l, "ui.json"));
                secs[l] = Json.AsDict(doc["uiTacticalAI"]);
            }

            // -------------------------------------------------- 1 presence
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                foreach (var kv in keys)
                {
                    object val;
                    var have = secs[l].TryGetValue(kv.Key, out val);
                    Check("present[" + kv.Key + "|" + l + "]", true,
                          have && !string.IsNullOrEmpty(Json.AsStr(val)));
                }
            }

            // --------------------------------------------- 2 canon fragments
            foreach (var kv in keys)
            {
                var de = Json.AsStr(secs["De"][kv.Key]);
                foreach (var fO in Json.AsList(Json.AsDict(kv.Value)["de_contains"]))
                {
                    var f = Json.AsStr(fO);
                    Check("de_canon[" + kv.Key + "] contains '" + f + "'", true,
                          de.Contains(f));
                }
            }

            // -------------------------------------------------- 3 EN copies
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                if (l == "De" || l == "En") continue;
                foreach (var kv in keys)
                    Check("en_copy[" + kv.Key + "|" + l + "]",
                          Json.AsStr(secs["En"][kv.Key]), Json.AsStr(secs[l][kv.Key]));
            }

            // -------------------------------------------------- 4 retired
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                foreach (var rO in retired)
                {
                    var r = Json.AsStr(rO);
                    Check("retired_gone[" + r + "|" + l + "]", false,
                          secs[l].ContainsKey(r));
                }
            }

            // -------------------------------------------------- 5 gift ban
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                foreach (var kv in keys)
                {
                    var val = (Json.AsStr(secs[l][kv.Key]) ?? "").ToLowerInvariant();
                    var hits = new List<string>();
                    foreach (var gO in giftWords)
                    {
                        var g = Json.AsStr(gO);
                        if (val.Contains(g.ToLowerInvariant())) hits.Add(g);
                    }
                    Check("no_gift_word[" + kv.Key + "|" + l + "]", "",
                          string.Join(",", hits.ToArray()));
                }
            }

            // ---------------------------------------------------- 6 C1 law
            foreach (var lO in langs)
            {
                var l = Json.AsStr(lO);
                foreach (var kv in keys)
                {
                    var val = (Json.AsStr(secs[l][kv.Key]) ?? "").ToLowerInvariant();
                    var hits = new List<string>();
                    foreach (var pO in c1Pairs)
                    {
                        var p = Json.AsStr(pO);
                        if (val.Contains(p.ToLowerInvariant())) hits.Add(p);
                    }
                    Check("no_dg006_or_claim[" + kv.Key + "|" + l + "]", "",
                          string.Join(",", hits.ToArray()));
                }
            }

            // the swap-frei persona must NOT claim 'seine Elite' (P4 honesty:
            // the Hutt escalates through the hunt, it has no swap target)
            foreach (var nO in noElite)
            {
                var n = Json.AsStr(nO);
                Check("no_elite_claim[De|" + n + "]", false,
                      (Json.AsStr(secs["De"][n]) ?? "").ToLowerInvariant().Contains("elite"));
                Check("no_elite_claim[En|" + n + "]", false,
                      (Json.AsStr(secs["En"][n]) ?? "").ToLowerInvariant().Contains("elite"));
            }

            // -------------------------------------------------- 7 Delta-0
            foreach (var dO in delta0)
            {
                var d = Json.AsStr(dO);
                foreach (var lO in langs)
                {
                    var l = Json.AsStr(lO);
                    var val = Json.AsStr(secs[l][d]) ?? "";
                    var dirty = val.Contains("{") || val.Contains("}");
                    foreach (var ch in val)
                        if (char.IsDigit(ch)) dirty = true;
                    Check("delta0_datenfrei[" + l + "]", false, dirty);
                }
            }

            // ------------------------------------------------ 8 asset cross
            var asset = Json.AsDict(LoadLangJson("En", "tactical_personas.json"));
            var found = new Dictionary<string, string>();   // persona id -> key
            foreach (var pO in Json.AsList(asset["personas"]))
            {
                var p = Json.AsDict(pO);
                var pid = Json.AsStr(p["id"]);
                foreach (var rO in Json.AsList(p["ranks"]))
                    foreach (var eO in Json.AsList(Json.AsDict(rO)["effects"]))
                    {
                        var e = Json.AsDict(eO);
                        if (Json.AsStr(e["kind"]) == "signaturText")
                            found[pid] = Json.AsStr(e["key"]);
                    }
            }
            var want = Json.AsDict(v["asset_signature_keys"]);
            Check("asset signaturText row count", (long)want.Count, (long)found.Count);
            foreach (var kv in want)
            {
                string got;
                Check("asset_key[" + kv.Key + "]", Json.AsStr(kv.Value),
                      found.TryGetValue(kv.Key, out got) ? got : "<missing>");
                // and the referenced field is actually present (no dead lane)
                Check("asset_key_resolves[" + kv.Key + "]", true,
                      (Json.AsStr(secs["En"][Json.AsStr(kv.Value)]) ?? "") != "");
            }
        }
    }
}
