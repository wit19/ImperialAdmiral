// PersonaState - the single Unity-side access point for the imperial
// PERSONAS (Thomas decisions 01.11./02.11., CONCEPT_personas.md). Every
// other file (bridge, deploy popups, campaign overview, IMPERIUM panel)
// asks HERE; nobody else parses the asset or resolves ranks.
//
// Storage law (mirrors the aiMode pattern the app already ships):
//  * global default  -> TacticalAISettings.PersonaDefault (PlayerPrefs)
//  * per campaign    -> SagaCampaign.persona (campaign JSON via
//                       SaveCampaignState); missing field in old saves = ""
//                       = follow the default - exactly how aiMode migrates.
// Rank law: pure function of (persona, threat of the mission being played)
// - no XP, no purchase, no memory (Core.Personas header).
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;
using UnityEngine;

namespace TacticalAI
{
    public static class PersonaState
    {
        public const string None = "";                 // follow global default
        public const string ExplicitNone = "none";     // explicit "no persona"

        // M6l: the legacy subsidy identity statics (_subsidyKey /
        // _grantedSubsidy) went with the subsidy machinery above.

        /// <summary>The persona asset (null when it failed to load - every
        /// method below fails open to 'no persona').</summary>
        static PersonaTable Table()
        {
            try { return TacticalAIAssets.PersonasRoot; }
            catch { return null; }
        }

        static Saga.SagaCampaign Live()
        {
            try { return Saga.RunningCampaign.sagaCampaign; }
            catch { return null; }
        }

        /// <summary>All selectable defs, asset order.</summary>
        public static List<PersonaDef> Defs()
        {
            var t = Table();
            return t == null ? new List<PersonaDef>() : t.Personas;
        }

        public static PersonaDef Def(string id)
        {
            var t = Table();
            return t == null ? null : t.Find(id);
        }

        /// <summary>What the campaign picker stores ("" = follow default).</summary>
        public static string StoredFor(Saga.SagaCampaign camp)
        {
            return camp == null ? None : (camp.persona ?? None);
        }

        /// <summary>The id in effect for a campaign: explicit field first,
        /// else the global default; unknown ids degrade to none (the asset
        /// is the validator - a stale save id never crashes anything).</summary>
        public static string EffectiveId(Saga.SagaCampaign camp)
        {
            var raw = string.IsNullOrEmpty(StoredFor(camp)) ? SettingsDefault() : StoredFor(camp);
            if (string.IsNullOrEmpty(raw) || raw == ExplicitNone) return ExplicitNone;
            return Def(raw) == null ? ExplicitNone : raw;
        }

        public static string SettingsDefault()
        {
            try { return TacticalAISettings.PersonaDefault; }
            catch { return ""; }
        }

        /// <summary>Set the campaign persona (picker). Same value = no-op;
        /// persists like every other campaign field.</summary>
        public static void SetFor(Saga.SagaCampaign camp, string id)
        {
            if (camp == null || camp.persona == id) return;
            camp.persona = id ?? None;
            try { camp.SaveCampaignState(); }
            catch (Exception e) { Debug.LogWarning("PersonaState::save failed " + e.Message); }
        }

        /// <summary>Cycle the campaign picker: follow-default -> none ->
        /// each asset persona (asset order) -> follow-default.</summary>
        public static string Cycle(Saga.SagaCampaign camp)
        {
            if (camp == null) return None;
            var ids = new List<string> { None, ExplicitNone };
            ids.AddRange(Defs().Select(d => d.Id));
            var cur = camp.persona ?? None;
            var i = ids.IndexOf(cur);
            if (i < 0) i = 0;
            var next = ids[(i + 1) % ids.Count];
            SetFor(camp, next);
            return next;
        }

        /// <summary>Cycle the GLOBAL default (settings row): none -> each
        /// asset persona (asset order) -> none. The campaign field "" keeps
        /// following whatever this says.</summary>
        public static string CycleDefault()
        {
            var ids = new List<string> { None, ExplicitNone };
            ids.AddRange(Defs().Select(d => d.Id));
            var cur = SettingsDefault();
            var i = ids.IndexOf(cur);
            if (i < 0) i = 0;
            var next = ids[(i + 1) % ids.Count];
            try { TacticalAISettings.PersonaDefault = next; } catch { }
            return next;
        }

        /// <summary>Settings-row label for the global default (asset is the
        /// validator: an unknown stored id displays as none).</summary>
        public static string DescribeDefault()
        {
            var lang = L();
            var raw = SettingsDefault();
            if (string.IsNullOrEmpty(raw) || raw == ExplicitNone || Def(raw) == null)
                return lang != null ? lang.personaNone : "none";
            return DisplayName(raw);
        }

        /// <summary>Threat that drives the rank: the live mission budget
        /// (setupOptions.threatLevel - HUD-visible), -1 without a session
        /// (then there is no rank; the overview shows per-mission values).</summary>
        public static int LiveThreat()
        {
            try
            {
                var s = DataStore.sagaSessionData;
                return s != null && s.setupOptions != null ? s.setupOptions.threatLevel : -1;
            }
            catch { return -1; }
        }

        /// <summary>Resolve the live campaign's persona at the live threat.
        /// Empty view (Rank 0) = no persona / no session / broken asset.</summary>
        public static PersonaView ResolveLive()
        {
            return ResolveFor(EffectiveId(Live()), LiveThreat());
        }

        public static PersonaView ResolveFor(string id, int threat)
        {
            var t = Table();
            if (t == null || threat < 0 || string.IsNullOrEmpty(id) || id == ExplicitNone)
                return new PersonaView();
            try { return t.Resolve(id, threat); }
            catch (Exception e)
            {
                Debug.LogWarning("PersonaState::resolve failed: " + e.Message);
                return new PersonaView();
            }
        }

        /// <summary>Deploy/reinforce price of a printed card for the LIVE
        /// campaign. Fail-open: printed cost (a display or booking site
        /// must never be broken by the persona layer).
        /// MODE GATE (Thomas 07.11 standing rule): tactical missions only -
        /// the mission-frozen ActiveNow() seam, NOT the bare master toggle.
        /// A badge-off (legacy) mission must price exactly like the
        /// untouched app; personas live in the tactical AI only.</summary>
        public static int CostFor(global::DeploymentCard card, int printedCost)
        {
            try
            {
                if (card == null || !TacticalAIBridge.TacticalMissionActive()) return printedCost;
                var v = ResolveLive();
                return v.IsEmpty ? printedCost
                                 : v.CostFor(card.name, card.faction, printedCost);
            }
            catch { return printedCost; }
        }

        // ------------------------------------------------ M6b elite_swap ---
        // The Unity seam of the elite_swap channel (Core.PersonaSwaps holds
        // the law; CONCEPT_personas_v2 v2.1 Sec.1.1). Call sites: the four
        // HAND-DRAWN deployment rails in SagaDeploymentPopup (landing d1/d2,
        // Onslaught draw loop, manual optional confirm). Mission-authored
        // EnemyDeployment events and mission start groups deliberately do
        // NOT swap: their cards are mission id-authoring and board INPUTS
        // for the persona's board law, not persona candidates (conservative
        // reading, documented in the commit).
        //
        // Ledger identity: the SAME recipe as the subsidy (campaign GUID +
        // missionID + threat - a rescaled restart counts as new). Static
        // like the subsidy ledger: a mid-mission reload starts it empty and
        // the BOARD law keeps its guard (a live elite card stands in
        // deployedEnemies regardless of ledger history), so a persona can
        // never put a SECOND simultaneous elite of one name on the board.
        static SwapLedger _swapLedger;
        static string _swapKey;

        /// <summary>Mission identity for per-mission persona bookings:
        /// campaign GUID + missionID + live threat (subsidy recipe, M2).</summary>
        static string MissionIdentity()
        {
            var camp = Live();
            var s = DataStore.sagaSessionData;
            if (s == null || s.setupOptions == null || s.setupOptions.projectItem == null)
                return null;
            return (camp != null ? camp.GUID.ToString() : "solo")
                   + "|" + s.setupOptions.projectItem.missionID + "|" + LiveThreat();
        }

        /// <summary>elite_swap candidate transformation at the moment a hand
        /// card is taken for deployment. Returns the ELITE card when the
        /// persona may swap this candidate (budget, name cap, board law,
        /// elite card in hand - all in Core.PersonaSwaps.TrySwap), else the
        /// UNCHANGED input. A swap consumes the elite card: the caller
        /// deploys it INSTEAD of the regular while the threat ledger keeps
        /// booking the REGULAR's printed cost (Delta-0, Sec.1.1-1). Fails
        /// HARMLESSLY on every unmet condition and on any error: the
        /// regular deploys as-is - no crash, no fallback, no substitution.
        /// Books the mission ledger HERE (at selection time), so a later
        /// draw in the same window can never see the consumed elite card;
        /// the legacy elite rails (Hard-Roll RNG, upstream upgrades) run
        /// over the result UNCHANGED (Sec.1.2-2 - read yes, change never).</summary>
        public static global::DeploymentCard SwapDeployCandidate(global::DeploymentCard card)
        {
            try
            {
                if (card == null || card.isElite) return card;
                // same mode-frozen gate as CostFor (Thomas 07.11): a
                // badge-off legacy mission never sees a persona swap -
                // the shared landing/onslaught/manual rails stay untouched.
                if (!TacticalAIBridge.TacticalMissionActive()) return card;
                var v = ResolveLive();
                if (v.IsEmpty || PersonaSwaps.Budget(v) <= 0) return card;
                var key = MissionIdentity();
                if (key == null) return card;
                if (key != _swapKey) { _swapKey = key; _swapLedger = new SwapLedger(); }

                // Internal (English) names, NEVER the localized cd.name:
                // SetCardTranslations overwrites live card names with the
                // active language (the M13f traits trap), so the asset's
                // name filters may only ever run on the internal lane.
                var name = InternalName(card.id) ?? card.name;

                // Board, read-only and DEFENSIVE: every deployed elite
                // counts, mission start groups and Hard-Mode upgrade
                // products included (Sec.1.1-4). Nothing here changes them.
                var board = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pool = DataStore.deployedEnemies;
                if (pool != null)
                    foreach (var d in pool)
                        if (d != null && d.isElite && !string.IsNullOrEmpty(d.name))
                            board.Add(PersonaSwaps.BaseOf(InternalName(d.id) ?? d.name));

                // The public deploy hand (the same list the landing draws from).
                var handIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hand = DataStore.deploymentHand;
                if (hand != null)
                    foreach (var h in hand)
                        if (h != null && !string.IsNullOrEmpty(h.id)) handIds.Add(h.id);

                var probe = new SwapCard(card.id, name, card.faction ?? "",
                                         card.cost, card.size, false);
                string eliteId;
                if (!PersonaSwaps.TrySwap(v, _swapLedger, probe, board, handIds, out eliteId))
                    return card;
                var elite = DataStore.deploymentCards == null ? null :
                    DataStore.deploymentCards.FirstOrDefault(x => x != null && x.isElite
                        && string.Equals(x.id, eliteId, StringComparison.OrdinalIgnoreCase));
                if (elite == null) return card;   // no elite card behind the id: harmless
                _swapLedger.Book(PersonaSwaps.BaseOf(name));
                Debug.Log("PersonaSwap::" + v.Id + " " + card.id + "->" + elite.id);
                return elite;
            }
            catch (Exception e)
            {
                Debug.LogWarning("PersonaState::swap failed: " + e.Message);
                return card;
            }
        }

        // id -> INTERNAL (English) card name, from the shipped EN card
        // asset (the same locale-stable lane the officer/Tripod detections
        // ride). Loaded once, lazily; unreadable asset -> null map and the
        // callers fall back to the localized name (matching may miss = a
        // harmless no-swap, never a wrong swap: the swap target itself is
        // always the row's elite_card id).
        static Dictionary<string, string> _internalNames;
        static bool _internalNamesTried;

        static string InternalName(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            if (_internalNames == null && !_internalNamesTried)
            {
                _internalNamesTried = true;
                try
                {
                    var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>(
                        "Languages/En/DeploymentGroups/enemies");
                    if (ta != null)
                    {
                        var txt = System.Text.RegularExpressions.Regex.Replace(
                            ta.text, @",(\s*[}\]])", "$1");   // legacy trailing commas
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var raw in Json.AsList(Json.Parse(txt)))
                        {
                            var d = raw as Dictionary<string, object>;
                            if (d == null) continue;
                            object o;
                            var id = d.TryGetValue("id", out o) && o != null ? Json.AsStr(o) : null;
                            var nm = d.TryGetValue("name", out o) && o != null ? Json.AsStr(o) : null;
                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(nm)) map[id] = nm;
                        }
                        if (map.Count > 0) _internalNames = map;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("PersonaState::internal name table failed: " + e.Message);
                }
            }
            if (_internalNames == null) return null;
            string got; return _internalNames.TryGetValue(cardId, out got) ? got : null;
        }

        /// <summary>Reset the per-mission swap ledger (session teardown /
        /// new campaign). The ledger also re-keys lazily in
        /// SwapDeployCandidate when the mission identity changes.</summary>
        public static void ResetSwapLedger()
        {
            _swapLedger = null;
            _swapKey = null;
        }

        // M6l (Delta-0 law, Beschluss 10/11): the pre-v2.1 mission-subsidy
        // machinery (EnsureMissionSubsidy / MissionSubsidy / HandRows) is
        // REMOVED with the gift kinds it read - free_deployment occurs 0x in
        // the shipped v2.1 asset, so the grant could never fire (M6g QA-FUND
        // C2: consumers keyed to kinds that do not exist = mechanically
        // dead). Branches die WITH the kind, not just the ui fields.

        // ------------------------------------------------------------ texts
        static UITacticalAI L()
        {
            try { return DataStore.uiLanguage != null ? DataStore.uiLanguage.uiTacticalAI : null; }
            catch { return null; }
        }

        static string TF(string fmt, params object[] args)
        {
            if (string.IsNullOrEmpty(fmt)) return "";
            try { return args.Length > 0 ? string.Format(fmt, args) : fmt; }
            catch (System.FormatException) { return fmt; }
        }

        static string Localized(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var lang = L();
            if (lang == null) return null;
            var fi = typeof(UITacticalAI).GetField(key);
            return fi == null ? null : fi.GetValue(lang) as string;
        }

        /// <summary>Localized display name of a persona (its asset nameKey
        /// through ui.json; the raw id only as last-ditch fallback).</summary>
        public static string DisplayName(string id)
        {
            var def = Def(id);
            if (def == null) return "";
            var t = Localized(def.NameKey);
            return string.IsNullOrEmpty(t) ? def.Id : t;
        }

        /// <summary>The campaign-overview picker label: what is SELECTED
        /// (not what it currently means - the popup body shows the live
        /// rank). ""-default gets the explicit 'follow global' marker.</summary>
        public static string DescribeSelection(Saga.SagaCampaign camp)
        {
            var lang = L();
            if (lang == null) return "";
            var stored = camp == null ? None : (camp.persona ?? None);
            if (string.IsNullOrEmpty(stored))
            {
                var gd = SettingsDefault();
                var gdName = string.IsNullOrEmpty(gd) || gd == ExplicitNone
                    ? lang.personaNone : DisplayName(gd);
                return TF(lang.personaFollowDefault, gdName);
            }
            if (stored == ExplicitNone) return lang.personaNone;
            return DisplayName(stored);
        }

        /// <summary>M6d (Beschluss 5, CONCEPT_personas_v2 Sec.3): the
        /// 'Rang n/max' text every rank display shares (ERKLÄRUNG line,
        /// settings badge, campaign popup chip). Denominator = the view's
        /// MaxRank = highest rank DEFINED for the persona — a persona with
        /// three authored ranks shows '3/3', a missing rank 4 never
        /// produces '3/4' and never an error. Template = the M6c
        /// personaRankCounter key; placeholder Replace (not Format) on
        /// purpose: a language may repeat {0}/{1} without throwing. Null /
        /// rank 0 / hand-built view without a defined max / missing key ->
        /// null, the CALLER keeps its single-number personaRank fallback
        /// (never an empty chip).</summary>
        public static string RankCounterText(PersonaView v)
        {
            if (v == null || v.Rank <= 0 || v.MaxRank < v.Rank) return null;
            var lang = L();
            return v.RankCounter(lang != null ? lang.personaRankCounter : null);
        }

        /// <summary>The instruction-card/IMPERIUM line for one group:
        /// persona, rank, and the bonuses that bite for THIS card. ui.json
        /// vocabulary only; null when there is nothing to say.</summary>
        public static string DescribeLine(PersonaView v, string internalName, string faction)
        {
            var lang = L();
            if (lang == null || v == null || v.Rank <= 0) return null;
            var name = DisplayName(v.Id);
            if (string.IsNullOrEmpty(name)) return null;
            // M6d Beschluss 5: 'Rang n/max'; the single-number personaRank
            // stays the fail-safe fallback (missing key / hand-built view)
            // so the head can never ship a dangling " - ".
            var counter = RankCounterText(v);
            var head = name + " - " + (!string.IsNullOrEmpty(counter)
                ? counter : TF(lang.personaRank, v.Rank));
            var parts = new List<string>();
            // M6l (Delta-0, Beschluss 11): the cost-cut and free-deployment
            // chips are GONE with their gift kinds - the retired channels
            // occur 0x in the shipped asset, so the branches were
            // mechanically dead (M6g QA-FUND C2). The live v2.1 channels
            // (pattern_weight, guard_split) stay below; the elite swap is
            // announced by the signature line + the personaDelta0Hint
            // sentence in the persona popup, never by a cost chip.
            if (v.Active.Any(e => e.Kind == "pattern_weight"))
                parts.Add(lang.personaBonusDoctrine);
            var g = v.GuardPercentFor(internalName ?? "", faction);
            if (g >= 0) parts.Add(TF(lang.personaBonusGuard, g));
            var line = parts.Count == 0 ? head : head + " - " + string.Join(" · ", parts);
            var sig = Localized(v.SignatureKey());
            if (!string.IsNullOrEmpty(sig)) line += "\n" + sig;
            return line;
        }

        /// <summary>Rank-change announcement (bridge, once per mission):
        /// up AND down are named openly (rank law, Core.Personas header).</summary>
        public static string RankChangeText(PersonaView v, int oldRank, bool up)
        {
            var lang = L();
            if (lang == null || v == null || v.Rank <= 0) return null;
            var name = DisplayName(v.Id);
            if (string.IsNullOrEmpty(name)) return null;
            return up
                ? TF(lang.personaRankUpQM, name, oldRank, v.Rank)   // 346: template
                : TF(lang.personaRankDownQM, name, oldRank, v.Rank); // has {0}{1}{2}
        }
    }
}
