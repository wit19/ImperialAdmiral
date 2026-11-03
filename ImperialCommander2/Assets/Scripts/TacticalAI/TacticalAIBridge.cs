// M3 integration bridge between the IC2 Unity runtime and TacticalAI.Core
// (concept section 4.2 hook table). EVERY entry point here is called behind
//   if (TacticalAI.TacticalAISettings.Enabled)
// and every hook site keeps its original legacy code as the else/default
// branch. With the settings default (off / "legacy") nothing in this file
// ever executes - the game is bit-compatible with the shipped build.
//
// What is faithful TODAY, inside this repo's runtime model:
//  * round activation ORDER  -> Core OrderPlanner (initiative/elite/deterministic
//    tiebreak, seeded DeterministicRandom, why-strings; never GlowEngine RNG),
//  * per-round activation blocks -> synthesized since M11 (Thomas 31.10.:
//    the order DECKS were a concept corpse - RR gives every activation its
//    printed actions (move + attack, one attack cap); the printed Stativ/
//    Tripod exclusion lives on the figure (catalog IsTripod), not on a card),
//  * deployment-point choice -> deterministic (no hidden RNG),
//  * target selection        -> deterministic priority order, no hidden RNG.
//
// Documented deviations (see docs/PR_draft.md): the IC2 Saga map is a
// tilemap of deployment points, NOT a hex grid with figure positions - there
// is no hex world model in the Unity code to snapshot. Per concept v4.0
// (3.3.1b, Thomas 27.10) the planned "hex adapter" is REPLACED by a PASSIVE
// world feed: tile graph + token-to-region reading + event stream + mission
// properties, all read-only, zero manual position entry (canon rule: a
// feature may never make a human report positions/state to the app). The
// WorldProvider seam below stays as the registration point; until a passive
// feed registers, "overlord" plans like "reference" (announced in-game).
// Posture is fixed to "eliminate" until the mission-property posture mapper
// lands; group pattern defaults to "aggressive" until the pattern
// classification asset ships. None of these degrade the legacy path.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Tac = TacticalAI.Core;

namespace TacticalAI
{
    /// <summary>Everything the activation popup needs for ONE tactical
    /// activation. Null plan => the popup runs its untouched legacy path.</summary>
    public sealed class ActivationPlanData
    {
        public string CardId;           // M11: the activating deployment card (was DeckCardId)
        public string[] Blocks;         // action tokens pre-render (public text)
        public string TargetName;       // deterministic focus target
        public string TargetWhy;
        // Thomas 02.11 (popup declutter, request 1): the focus-target WHY
        // lives here now - the popup line stays short ("Fokus-Ziel: Name")
        // and the reason renders inside the gated EXPLAIN section only.
        public string FocusReason;
        public string ExplainLine;      // printed why-string (ExplainLevel)
        public bool HasTarget;
        public string Pattern;          // doctrine pattern (dev 11); null = aggressive
        // M6p (Thomas 02.11): the doctrine line renders PERMANENTLY right after
        // the focus-target line - independent of the EXPLAIN level, exactly
        // like the focus line itself. Built from the existing uiTacticalAI keys
        // (doctrineHeader + doc* name); ExplainFor does not carry the doctrine
        // anymore (no double display).
        public string DoctrineLine;
        // M13t (Thomas 30.10.): the activation's card prints Stativ/Tripod.
        // The renderer swaps in the emplacement action trees: every move
        // forfeits the attack, so the tree must never suggest one while a
        // post is held or a target is in range.
        public bool Tripod;
        // Noldorion playtest 2.11 (PR #194): the card prints Assault
        // (multiple attack actions per activation - E-Web, AT-DP, IG-88).
        // The activation then runs TWO attack blocks; the second one is the
        // printed second attack, not a conditional move-check.
        public bool Assault;
        // Thomas 02.11 (Offizier-Baeume): printed command ability, surfaced as
        // catalog keywords in ToRecord (ability lane + DG-id belt; the
        // ignored-lane route is dead at runtime - SetCardTranslations()
        // blanks it):
        //   "OfficerOrder"     -> apTreeOfficer<Pattern>       (DG004/DG005)
        //   "OfficerExecOrder" -> apTreeOfficerElite<Pattern>  (DG006)
        public bool Officer;
        public bool OfficerElite;
        // Personas (M3): "Persona - Rang - aktive Boni" line for this
        // group's activation popup (PersonaState.DescribeLine); null when no
        // persona plays. Pure display - changes no plan number.
        public string PersonaLine;
    }

    public static class TacticalAIBridge
    {
        /// <summary>Seam for a future hex-map adapter: when a World provider is
        /// registered, overlord mode runs the full Core belief/focus loop.
        /// Null in this repo state (no hex world model in the Unity runtime).
        /// M5 (concept 3.3.1b): the hex-adapter plan is REPLACED by the
        /// passive world feed - the feed below is the sanctioned producer;
        /// until a full board World exists behind this seam, the bridge
        /// consumes the projected FeedWorldView (posture + wheel vitality).</summary>
        public static Func<Tac.World> WorldProvider;

        /// <summary>M5 passive world feed (concept 3.3.1b). Registered by
        /// WorldEventsUnity at session start (runtime AddComponent through
        /// EnsureFeed) - the ONLY sanctioned source of world state; the canon
        /// rule forbids any manual position/state entry (zero new inputs).</summary>
        public static Tac.WorldEventFeed Feed;
        static WorldEventsUnity _feedAdapter;
        static bool _feedTried;

        // M11 (Thomas 31.10.): order decks retired (concept corpse). The set
        // below replaces the old deck-membership check as the tactical-support
        // whitelist: it is the EXACT key set of the retired public
        // order_decks.json artifact (89 shipped cards DG001-DG069 + the 20
        // unique villains DG071-DG090; DG070 never shipped) - a printed-card
        // fact, no hidden data. Custom/embedded imports stay outside and keep
        // the legacy popup/activation path verbatim.
        internal static readonly HashSet<string> TacticalShippedIds =
            new HashSet<string>
            {
                "DG001","DG002","DG003","DG004","DG005","DG006","DG007","DG008","DG009","DG010",
                "DG011","DG012","DG013","DG014","DG015","DG016","DG017","DG018","DG019","DG020",
                "DG021","DG022","DG023","DG024","DG025","DG026","DG027","DG028","DG029","DG030",
                "DG031","DG032","DG033","DG034","DG035","DG036","DG037","DG038","DG039","DG040",
                "DG041","DG042","DG043","DG044","DG045","DG046","DG047","DG048","DG049","DG050",
                "DG051","DG052","DG053","DG054","DG055","DG056","DG057","DG058","DG059","DG060",
                "DG061","DG062","DG063","DG064","DG065","DG066","DG067","DG068","DG069",
                "DG071","DG072","DG073","DG074","DG075","DG076","DG077","DG078","DG079","DG080",
                "DG081","DG082","DG083","DG084","DG085","DG086","DG087","DG088","DG089","DG090",
            };

        static Dictionary<string, Tac.DeploymentCard> _catalog;
        // dev 11: card id -> doctrine pattern for the CURRENT round (fed from
        // the Core Figure records; 'aggressive' everywhere until M4 assigns
        // per-mission roles - the renderer is ready for all six patterns).
        static Dictionary<string, string> _patternByCard;
        static int _planRound = -1;
        static List<Tac.OrderSlot> _slots = new List<Tac.OrderSlot>();
        static string _publicLine = "";
        static ulong _sessionSeed;
        static string _sessionKey;   // campaign|mission identity of the loaded session
        static bool _overlordNoticeShown;  // one-shot visible ORD->REF degradation notice
        static int _postureLogRound = -1;  // M5: one posture log per round (feed provenance)
        // M6 overlord layer on the world feed: last projected view, the
        // round's focus designation (focus fire across groups), per-group
        // risk positions and the threat-economy mirror. ALL overlord-mode +
        // feed gated; reference/legacy paths never read these fields.
        static Tac.FeedWorldView _lastFeedView;
        static string _roundFocusHero;
        static Tac.FocusPick _roundFocus;
        static int _roundFocusRound = -1;
        // Noldorion direction reply 2.11 (focus-score transparency): the
        // RAW round-focus facts (runner-up + posture) for the EXPLAIN=VOLL
        // disclosure; localization happens in ExplainFor, never here.
        static Tac.FocusPick _roundFocusRunnerUp;
        static string _roundFocusPosture;

        // M13u (variante c): last rolled doctrine per group
        // ("pattern|drawPct|dist tokens") for the EXPLAIN disclosure and
        // the Debug channel; rewritten by ClassifyPattern every round.
        static readonly Dictionary<string, string> _patternRoll =
            new Dictionary<string, string>();
        // Punkt-2 block (Thomas + Noldorion 2.11): doctrine phase state.
        // _doctrineEffective is the per-round posture the classification
        // consumes (base defend -> hunt phase flip); the guard band holds
        // the back-half troopers of a defend deployment (S4/S5, see
        // DoctrineFor). Announcements are one-shot per mission session.
        static string _doctrineBase;
        static string _doctrineEffective;
        static bool _phaseShiftAnnounced;
        // _splitAnnounced REMOVED (Thomas 02.11, Unity CS0414): the Personas
        // commit fa8597f added it with its session reset but no consumer ever
        // existed - dead state, deleted rather than silenced.
        static int _guardBandCount;
        static HashSet<string> _guardIds;   // back-half defend troopers (this round)
        // Personas (M2, Thomas 02.11.): the live round's resolved persona
        // (rank + effects). EMPTY view = no persona = the exact pre-persona
        // behaviour on every hook below. Announcements are once-per-mission
        // (ResetSession clears); the rank itself is a pure (persona, threat)
        // function - no memory, no purchase (Core.Personas rank law).
        static Tac.PersonaView _personaView = new Tac.PersonaView();
        static bool _personaQMAnnounced;
        static Dictionary<string, Tac.RiskDecision> _riskByGroup;
        static Dictionary<string, Tac.HeroThreatProfile> _profileByName;
        static Tac.ThreatLedger _threatLedger;
        static int _ledgerRound = -1;

        public static void ResetSession()
        {
            _catalog = null;
            _patternByCard = null;
            _planRound = -1;
            _slots = new List<Tac.OrderSlot>();
            _publicLine = "";
            _sessionSeed = 0;
            _sessionKey = null;
            _overlordNoticeShown = false;
            _postureLogRound = -1;
            _lastFeedView = null;
            _roundFocusHero = null;
            _roundFocus = null;
            _roundFocusRound = -1;
            _roundFocusRunnerUp = null;
            _roundFocusPosture = null;
            _doctrineBase = null;
            _doctrineEffective = null;
            _patternRoll.Clear();
            _phaseShiftAnnounced = false;
            _guardBandCount = 0;
            _guardIds = null;
            _personaView = new Tac.PersonaView();
            // M6b: fresh mission identity -> fresh elite_swap ledger
            // (SwapDeployCandidate also re-keys lazily; this is the
            // explicit teardown so a campaign switch cannot carry swaps).
            PersonaState.ResetSwapLedger();
            _personaQMAnnounced = false;
            _riskByGroup = null;
            _threatLedger = null;
            _ledgerRound = -1;
            // M5: the adapter may live on (DontDestroyOnLoad) and re-registers
            // its new-mission Feed itself; allow the lookup to run again.
            _feedTried = false;
        }

        /// <summary>Identity of the loaded session (same material SessionSeed
        /// hashes). Null when no saga session is up.</summary>
        static string SessionKey()
        {
            try
            {
                return Saga.RunningCampaign.sagaCampaignGUID + "|" +
                       (DataStore.sagaSessionData?.setupOptions?.projectItem?.missionID ?? "solo");
            }
            catch { return null; }
        }

        /// <summary>Save-resume and mission switches bypass SagaSetup, so the
        /// static planner state would leak the previous mission's decks/plan.
        /// Every hook entry revalidates the campaign|mission identity and
        /// resets when it changed (audit FINAL-QA).</summary>
        /// <summary>FNV-1a string ordinal -> ulong (deterministic, locale-free).</summary>
        static ulong OrdinalOf(string text)
        {
            ulong h = 1469598103934665603UL;
            foreach (var c in text ?? "") { h ^= c; h *= 1099511628211UL; }
            return h == 0 ? 0x9E3779B97F4A7C15UL : h;
        }

        /// <summary>Hook gate: master switch on AND the loaded mission's
        /// effective mode is not "legacy" (concept 4.2.7). The mode frozen
        /// into setupOptions at mission start decides - mid-mission global
        /// toggles do not silently re-plan a legacy mission (FINAL-QA audit).</summary>
        static bool ActiveNow()
        {
            if (!TacticalAISettings.Enabled) return false;
            var mission = DataStore.sagaSessionData?.setupOptions?.aiMode;
            return TacticalAISettingsUnity.MissionMode(mission) != "legacy";
        }

        /// <summary>Public gate for game-side features that the Reference
        /// Rules replace outright (dev 15, Thomas): MedPacs are the app's
        /// healing shortcut - printed rules keep a hero wounded until the
        /// mission ends, so Saga hides the whole MedPac mechanic in REF/ORD
        /// missions. Same mission-frozen semantics as ActiveNow().</summary>
        public static bool TacticalMissionActive()
        {
            try { return ActiveNow(); }
            catch { return false; }
        }

        // A sentence is one [^ .!?\n]-run up to a terminator; a MedPac
        // sentence carries the token in any language form (the shipped
        // translations keep "medpac" as a loanword - De "Medpac", En
        // "medpac", Ru "медпак", Hu "medpac-et"). A "claim the token"
        // instruction (Marker/token/жетон/jelző/żeton) in the SAME line is
        // the bookkeeping half of the same reward and goes with it; on
        // untouched lines those words keep their mission-token meaning.
        static readonly Regex MedpacSentenceRx = new Regex(
            @"[^.!?\n\r]*(?:med.?pac|медпак)[^.!?]*[.!?]?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex ClaimTokenRx = new Regex(
            @"[^.!?\n\r]*(?:\bmarker|\btoken|jelz|жетон|żeton)[^.!?]*[.!?]?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>REF/ORD text law (Thomas playtest 29.10.): crates still
        /// exist, but the MedPac reward is mechanically SKIPPED in tactical
        /// missions (dev 15) - so no mission text may promise one either.
        /// Strips every MedPac sentence from mission prose, plus the
        /// paired "claim the medpac token" instruction on the same line.
        /// Only ever active while TacticalMissionActive(); fail-open (a
        /// regex hiccup shows the original text, never an empty box).</summary>
        public static string ScrubMedpacRewardText(string text)
        {
            if ( string.IsNullOrEmpty( text ) )
                return text;
            if ( !TacticalMissionActive() )
                return text;
            try
            {
                if ( text.IndexOf( "pac", StringComparison.OrdinalIgnoreCase ) < 0
                     && text.IndexOf( "медпак", StringComparison.OrdinalIgnoreCase ) < 0 )
                    return text;   // no MedPac token anywhere - untouched
                var lines = text.Replace( "\r\n", "\n" ).Split( '\n' );
                var kept = new List<string>( lines.Length );
                foreach ( var line in lines )
                {
                    var scrubbed = MedpacSentenceRx.Replace( line, "" );
                    if ( scrubbed != line )   // a reward sentence died here:
                    {
                        scrubbed = ClaimTokenRx.Replace( scrubbed, "" );  // take its claim-token instruction with it
                        kept.Add( scrubbed );
                    }
                    else
                        kept.Add( line );
                }
                // drop lines the scrub emptied entirely, then collapse the
                // paragraph gap they orphaned; everything else stays verbatim.
                var sb = new StringBuilder();
                for ( var i = 0; i < kept.Count; i++ )
                {
                    if ( kept[i].Trim().Length == 0 && lines[i].Trim().Length > 0 )
                        continue;   // line existed before and is empty only because we scrubbed it
                    sb.Append( kept[i] );
                    if ( i < kept.Count - 1 )
                        sb.Append( '\n' );
                }
                var result = Regex.Replace( sb.ToString().TrimEnd( '\n' ), @"\n{3,}", "\n\n" );
                if ( result.Trim().Length == 0 )
                    return text;   // would blank the whole box - keep original
                if ( result != text )
                    Debug.Log( "TacticalAIBridge::ScrubMedpacRewardText - MedPac reward sentence removed for REF/ORD mission text" );
                return result;
            }
            catch
            {
                return text;
            }
        }

        /// <summary>M13c: the optional-deployment path fork (Scope D) fed
        /// with the live, mission-frozen mode - same gate semantics as
        /// ActiveNow(). The pure decision lives in Core
        /// (OptionalDeploy.Path, fixture optional_deploy.json); this is the
        /// Unity seam the ImperialPopup button calls so both the UI and the
        /// pinned law share ONE truth.</summary>
        public static string OptionalDeployPath()
        {
            try
            {
                var mission = DataStore.sagaSessionData?.setupOptions?.aiMode;
                return Core.OptionalDeploy.Path(
                    TacticalAISettings.Enabled,
                    TacticalAISettings.EffectiveMode(null),   // global mode, mission token passed below
                    mission);
            }
            catch { return Core.OptionalDeploy.PathManual; }
        }

        static void EnsureSessionFresh()
        {
            var k = SessionKey();
            if (k == null) return;                 // no session context yet
            if (_sessionKey != null && k != _sessionKey) ResetSession();
            _sessionKey = k;
            // M5 (concept 3.3.1b): every hook entry is a decision point - the
            // bridge pulls the CURRENT passive-feed state here (adapter polls
            // App state it already maintains; zero human input). Stale views
            // from another mission never cross the session boundary.
            PullFeedView();
        }

        /// <summary>M5: install the passive world-feed adapter at runtime
        /// (AddComponent through the bridge's session start - NO prefab
        /// surgery, NO scene edits). Inside the Enabled gate only: legacy
        /// play never creates the component.</summary>
        static void EnsureFeed()
        {
            if (_feedTried) return;
            _feedTried = true;
            try
            {
                if (!TacticalAISettings.Enabled) return;
                _feedAdapter = UnityEngine.Object.FindObjectOfType<WorldEventsUnity>();
                if (_feedAdapter == null)
                {
                    var go = new GameObject("TacticalAI_WorldFeed");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _feedAdapter = go.AddComponent<WorldEventsUnity>();
                }
                Feed = _feedAdapter.Feed;
                Debug.Log("TacticalAIBridge::world feed adapter installed (passive, zero input)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("TacticalAIBridge::feed install failed, legacy paths stay: " + e.Message);
                _feedAdapter = null;
                Feed = null;
            }
        }

        /// <summary>Feed state for the CURRENT decision, or null when no feed
        /// is registered (overlord then honestly degrades to reference rules
        /// WITH the visible notice). Only the adapter's own MissionKey can
        /// match the live session - never plan on a stale mission's history.</summary>
        static Tac.FeedWorldView PullFeedView()
        {
            EnsureFeed();
            try
            {
                if (!TacticalAISettings.Enabled) return null;
                if (_feedAdapter == null || Feed == null) return null;
                var v = _feedAdapter.ProjectNow();
                if (v == null) return null;
                var k = SessionKey();
                if (k != null && v.MissionKey != k) return null;
                _lastFeedView = v;      // M6: overlord focus reads this
                return v;
            }
            catch (Exception e)
            {
                Debug.LogWarning("TacticalAIBridge::feed pull failed (planning without feed): " + e.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- plan

        /// <summary>Round planner, called from the IncreaseRound hook (the
        /// nearest existing equivalent of the concept's deployment_finished
        /// event trigger - see PR draft deviation note) and lazily before any
        /// activation. Rebuilds order + deals EVERY living group's card at
        /// round start (concept 3.4.1: deal-all-first, never at activation).</summary>
        public static void OnNewRound(int roundNo)
        {
            if (!ActiveNow()) return;   // master off OR this mission is legacy
            // Playtest hotfix 27.10: round rollover must never die on a
            // planning exception (same fail-open as the activation hooks).
            try
            {
            EnsureSessionFresh();
            // Thomas 02.11: the post_hoc ("NACHHER") explain mode is GONE -
            // the explain switch is a plain AN/AUS toggle now (AN == the old
            // VOLL per-activation block). The round-end quick message that
            // carried the post_hoc summary is removed with the mode.
            EnsureRound(roundNo);
            }
            catch (Exception e)
            {
                Debug.LogWarning("TacticalAIBridge::OnNewRound failed (plan rebuilt on next hook): " + e);
                _planRound = -1;
            }
        }

        static void EnsureRound(int roundNo)
        {
            if (_planRound == roundNo) return;
            EnsurePersona();
            // M5: fresh feed state BEFORE building this round's plan
            var feedView = PullFeedView();
            // Noldorion direction reply 2.11 + Thomas 01.11. (Punkt 2):
            // doctrine phase. Base = MissionPosture() (structured mission
            // data). A defend mission WITH a public round clock (the user's
            // roundLimitToggle included - clock off = no phase, honest)
            // shifts to hunt in the final third: the Imperium stops holding
            // and races the rebels, exactly like a human would. Public
            // trigger (HUD round display), deterministic, announced once.
            SetDoctrinePhase(roundNo);
            BuildGroups(out var facts, out var figures, feedView);
            _patternByCard = new Dictionary<string, string>();
            foreach (var f in figures) if (f != null) _patternByCard[f.Id] = f.Pattern;
            if (facts.Count == 0)
            {
                // Noldorion PR #194 bug 2.11 hardening: an empty round must
                // NEVER be cached as "planned". If this build ever runs
                // before the deployed roster is up (early hook, restore
                // race), caching the round here would pin the whole round to
                // an empty order -> every activation silently falls back to
                // the legacy random draw. Leave _planRound untouched: the
                // next hook/activation rebuilds from the real roster.
                _slots = new List<Tac.OrderSlot>();
                _publicLine = "";
                return;
            }

            var mode = EffectiveMissionMode();
            // M5 (concept 3.3.1b item 4): posture is DERIVED from mission
            // properties via the passive feed (WorldEventVector.DerivePosture:
            // objective text, round clock, priority target). "eliminate" is
            // now only the honest fallback when no feed is registered.
            var posture = "eliminate";
            if (feedView != null && !string.IsNullOrEmpty(feedView.Posture))
                posture = feedView.Posture;
            if (feedView != null && _postureLogRound != roundNo)
            {
                _postureLogRound = roundNo;
                Debug.Log("TacticalAIBridge::posture=" + posture + " round=" + roundNo +
                          " [feed seq=" + feedView.Seq + " hash=" +
                          (feedView.HistoryHash ?? "").Substring(0, Math.Min(12, (feedView.HistoryHash ?? "").Length)) +
                          " objective=\"" + feedView.Objective + "\"]");
            }
            // dev 21: a mission badge can pin its OWN ladder rung (S1..S5);
            // AUTO/OFF fall back to the global ladder.
            var skill = Tac.SkillProfile.For(TacticalAISettings.EffectiveSkill(CurrentMissionAiMode()));
            // NOTE(overlord): the ORD optimization goal runs on the PASSIVE
            // world feed (concept v4.0 3.3.1b) - event stream + derived
            // posture + wheel vitality, NEVER manual position entry. With a
            // registered feed the S4/S5 plan is honest ORD and NO notice is
            // shown. Only without any feed does overlord fall back to the
            // reference ruleset - visibly, not silently (Thomas playtest 27.10).
            // M6 A.7: the condition is on the PROJECTED VIEW, not the Feed
            // object - a registered-but-unusable feed (stale mission key,
            // failed pull) warns exactly like a missing one; with an active
            // feed the projection exists and this branch can never fire.
            if (mode == "overlord" && WorldProvider == null && feedView == null)
            {
                Debug.Log("TacticalAIBridge::overlord without world feed - planning at reference ruleset");
                // ...and once per mission ALSO visibly, not just in the log
                // (Thomas playtest 27.10: he selected OVERLORD and correctly
                // observed reference behaviour - the degradation must not be
                // silent in the player's face).
                if (!_overlordNoticeShown)
                {
                    _overlordNoticeShown = true;
                    try
                    {
                        var line = DataStore.uiLanguage?.uiTacticalAI?.overlordFallback;
                        var qm = GlowEngine.FindUnityObject<QuickMessage>();
                        if (qm != null && !string.IsNullOrEmpty(line)) qm.Show(line);
                    }
                    catch { /* notice is cosmetic; never break planning */ }
                }
            }

            // ------------------------------------------------------------------
            // M6: the overlord layer ON the world feed (concept 3.3/3.5).
            // Gated on overlord-mode AND a live feed projection - reference and
            // legacy paths never read a single field filled here. The inputs
            // are EXACTLY the feed projection + the curated public threat
            // table: never more, never better information than a human GM
            // (fairness canon 3.3.1/12). Threat-economy mirror (calibrated
            // save/idle levers from the skill profile), round focus
            // (focus fire across groups) and the per-group risk position
            // (coverage <-> fire-line trade, concept 3.5.1) all live here.
            _riskByGroup = null;
            _roundFocus = null;
            _roundFocusHero = null;
            _roundFocusRound = -1;
            if (mode == "overlord" && WorldProvider == null && feedView != null)
            {
                EnsureThreatMirror(skill, feedView, roundNo);
                ComputeRoundFocus(feedView, posture);
                BuildRiskPositions(skill, posture, feedView, facts);
                if (_roundFocus != null)
                    Debug.Log("TacticalAIBridge::ORD focus r" + roundNo + " -> \"" + _roundFocusHero +
                              "\" basis=" + _roundFocus.Basis +
                              " threat=" + (_roundFocus.Threat.HasValue ? _roundFocus.Threat.Value.ToString() : "-") +
                              " marked=" + (_roundFocus.Marked ? 1 : 0) +
                              " objActor=" + (string.IsNullOrEmpty(feedView.ObjectiveActor) ? "-" : feedView.ObjectiveActor) +
                              " feedSeq=" + feedView.Seq +
                              " risk=" + (_riskByGroup != null ? _riskByGroup.Count : 0) + " groups");
            }

            var rng = new Tac.DeterministicRandom(SessionSeed() ^ (ulong)roundNo * 0x9E3779B97F4A7C15UL);
            // M12d: the campaign deck's acquired class cards hook into the
            // SAME pipeline (activation efficiency + printed strength). No
            // deck / no firing hook -> null -> bit-identical canon plan.
            // Personas (M4): the class-card hook channel is GONE - the
            // persona replaces the card system (CONCEPT_personas.md 6); the
            // persona never touches the plan numbers (fairness law: only
            // costs, doctrine share, guard band, texts). null = the exact
            // bit-identical canon plan the fixtures pin.
            var plan = Tac.OrderPlanner.PlanRound(roundNo, facts, _catalog, posture, skill, rng,
                                                  roundNo, null, null, null, null);
            Tac.OrderPlanner.AssertPermutation(plan, facts.Select(f => f.GroupId));
            _slots = plan.Slots;
            var names = facts.ToDictionary(f => f.GroupId, f => f.Name);
            _publicLine = plan.PublicLine(names);
            _planRound = roundNo;

            // M11 (Thomas 31.10.): the per-round order-card deal is GONE with
            // the decks (concept corpse). Nothing to reap, nothing to deal -
            // the round plan above is the only round-start state.
        }

        // ------------------------------------------------------ activation hook

        /// <summary>OnActivateImperial hook: next group in the planned order
        /// instead of the legacy 70%-priority random draw. Returns false (=>
        /// caller keeps legacy RNG path verbatim) when the planned group is
        /// not among the candidates (e.g. custom-imported cards without a
        /// catalog entry, deployed but never planned).</summary>
        public static bool TryPeekNextActivation(List<global::DeploymentCard> candidates,
                                                 out global::DeploymentCard next)
        {
            next = null;
            try
            {
                EnsureSessionFresh();
                // M8/F2: ActiveNow() carries the per-mission override semantics
                // (mission badge "off" => legacy) - Enabled alone must never plan
                // an activation order for a mission the user switched OFF.
                if (!ActiveNow() || candidates == null || candidates.Count == 0)
                    return false;
                EnsureRound(CurrentRound());
                foreach (var slot in _slots)
                {
                    var cd = candidates.FirstOrDefault(x => x.id == slot.GroupId);
                    if (cd != null) { next = cd; return true; }
                }
            }
            catch (Exception e)
            {
                // Playtest hotfix 27.10 (Thomas): ANY planning exception must
                // never swallow the activation click - fail-open to the legacy
                // dice draw, loudly. Root cause stays in the log for diagnosis.
                Debug.LogWarning("TacticalAIBridge::peek failed -> legacy draw: " + e);
                _planRound = -1;
                _slots = new List<Tac.OrderSlot>();
            }
            return false;
        }

        /// <summary>Activation popup hook: printed order-deck card blocks +
        /// deterministic target + explain line, replacing the random
        /// instruction option, hidden bonus roll and random hero pick.
        /// False => popup keeps its legacy blocks verbatim.</summary>
        public static bool TryBuildActivationPlan(global::DeploymentCard cd, out ActivationPlanData plan)
        {
            // Playtest hotfix 27.10 (Thomas): the activation popup must NEVER
            // die on a planning exception - fail-open to the legacy blocks,
            // loudly. Root cause stays in the log for diagnosis.
            try
            {
                return TryBuildActivationPlanInner(cd, out plan);
            }
            catch (Exception e)
            {
                Debug.LogWarning("TacticalAIBridge::plan failed -> legacy popup: " + e);
                _planRound = -1;
                plan = null;
                return false;
            }
        }

        static bool TryBuildActivationPlanInner(global::DeploymentCard cd, out ActivationPlanData plan)
        {
            plan = null;
            EnsureSessionFresh();
            if (!ActiveNow() || cd == null) return false;   // M8/F2: mission badge OFF => legacy popup
            EnsureRound(CurrentRound());
            EnsureCatalog();
            Tac.DeploymentCard catCard;
            // M11 (Thomas 31.10.): the order decks are retired as a concept
            // corpse - no assets to load, no cards to draw or finish. The
            // catalog membership (printed-card whitelist, see
            // TacticalShippedIds) alone decides tactical vs. legacy popup;
            // custom/embedded imports are not shipped printed cards and keep
            // the legacy path verbatim.
            if (_catalog == null || !_catalog.TryGetValue(cd.id, out catCard)) return false;

            // Noldorion playtest 2.11: a card that PRINTS Assault (multiple
            // attack actions per activation) runs both slots as attacks -
            // the E-Web saw a "Tripod description" as its second action
            // instead of its printed second shot. Tripod without Assault:
            // the emplacement either holds and shoots or leaves (never
            // both). Everything else: the printed move + attack pair.
            // Thomas playtest 02.11 (Player-16): the persona line MUST be
            // assigned AFTER the object exists - the M3 insert sat above the
            // ctor, NRE'd on every activation and failed the whole popup
            // open to the legacy blocks.
            plan = new ActivationPlanData { CardId = cd.id, Tripod = catCard.IsTripod,
                                            Assault = catCard.HasAssault };
            plan.PersonaLine = PersonaLine(cd.id);
            // Offizier detection (Thomas 02.11, Player-17 fix): keyword view
            // of the CATALOG card - injected from the printed ability lane +
            // DG-id belt in ToRecord. The first draft read cd.ignored, which
            // the app's SetCardTranslations() overwrites with the language
            // card's ignored="" at load - dead at runtime, never fired.
            plan.OfficerElite = catCard.Keywords.Contains("OfficerExecOrder");
            plan.Officer = plan.OfficerElite || catCard.Keywords.Contains("OfficerOrder");
            if (plan.Officer)
                Debug.Log("TacticalAIBridge::officer " + cd.id +
                          " order=1 elite=" + (plan.OfficerElite ? 1 : 0));
            // Aufgabe 4: the fixed move/attack pair synthesis is GONE - the
            // universal AP pool (RenderBlocks) replaces the block sequence;
            // Assault/Tripod still ride as flags on the plan itself.
            if (TrySelectTarget(cd, out var targetName, out var why))
            {
                plan.HasTarget = true;
                plan.TargetName = targetName;
                // Thomas 02.11 (request 1): the action screen shows ONLY the
                // target name; TrySelectTarget hands back the bare reason,
                // which ExplainFor renders in the EXPLAIN section.
                plan.TargetWhy = TF(UIx.focusPrefix) + targetName;
                plan.FocusReason = why;
            }
            // Thomas 29.10: the focus line is NOT part of the explanation
            // anymore - the popup prints it ABOVE the action blocks (focus
            // first, then actions, then the gated explanation); the
            // ExplainLevel gate now really hides only the explanation.
            // Doctrine pattern from the card's Core catalog record (dev 11),
            // classified by MissionPosture() x printed traits at catalog
            // build (M4, Thomas 29.10.): defend-missions put guards and
            // blockers on the objective, hunt-missions run zero guards.
            // The renderer carries full action-tree text for all six
            // patterns, so no doctrine ever ships as bare tokens.
            // (M11: the Tripod flag already rides on the plan from the same
            // catalog record - see the block synthesis above.)
            plan.Pattern = _patternByCard != null && _patternByCard.TryGetValue(cd.id, out var pat)
                           && !string.IsNullOrEmpty(pat) ? pat : "aggressive";
            // M6p (Thomas 02.11): the doctrine moved OUT of the gated explain
            // block into this permanent line - the popup prints it directly
            // after the focus-target line, always visible (EXPLAIN level is
            // irrelevant, same law as the focus line). Reuses the existing
            // uiTacticalAI keys doctrineHeader + doc* (no new key, 11/11 by
            // construction). Missing key -> no line (never a blank row).
            if (UIx != null && !string.IsNullOrEmpty(UIx.doctrineHeader))
                plan.DoctrineLine = TF(UIx.doctrineHeader,
                                       DoctrineName(plan.Pattern ?? "aggressive", UIx));
            // Thomas 02.11: explain is built AFTER the pattern exists (M6p: it
            // no longer reads it - the doctrine left the explain block) and
            // before the blocks render.
            plan.ExplainLine = ExplainFor(cd.id, plan);
            // Playtest 27.10 (Thomas): raw deck tokens ('move', 'attack') are
            // not an instruction a human Imperial player can execute. Render
            // every block as a concrete, localized order naming the focus
            // target (which names are PUBLIC once dealt - fairness unaffected).
            plan.Blocks = RenderBlocks(plan, cd);

            // M11e: a printed {A} special action of the villain card as the
            // THIRD block next to move/attack - but only when the VillainOptions
            // law says it beats the plain dice attack (S4+ gate exactly like
            // CardKeywords; the ladder comes from the same resolution the
            // round planner uses). The block text is the card's own localized
            // ability line: the printed condition ("within 4 spaces and line
            // of sight" ...) rides along verbatim, and reading the live board
            // stays the human Imperial player's job - the same doctrine that
            // makes every other block a printed Wenn-Dann template. The
            // Overlord may take the block or leave it (scope: the ORD blocks
            // above remain the planned proposal); without a focus target the
            // option would name nobody -> no block.
            plan.Blocks = WithVillainOption(plan, cd);
            // Noldorion review 2.11 (PR #194): the honest boundary line. A
            // non-villain group's printed abilities (Order, Cower, Squad
            // Training, Self-Destruct ...) are REAL printed card data — the
            // planner simply does not model them, so the mode never
            // pre-decides those "may" calls; they stay at the table. The note
            // says exactly that, in the group's own popup (the villain-option
            // block above already names its ability -> no duplicate advice).
            plan.Blocks = WithAbilitiesLaneNote(plan, cd);
            // M13n (Thomas 29.10): general RR info block under the actions,
            // BEFORE the gated explanation (popup appends ExplainLine after
            // Blocks, so appending here lands exactly between them). Door law
            // verified against RR DOORS: "A figure can interact with a door to
            // open it" - INTERACT is one of the five action types, so opening
            // costs an action; it is never a free side-effect of movement.
            // Missing keys skip their line (never a blank row).
            var Li = UIx;
            // M13c (Thomas 30.10): ONE merged info paragraph at the door-law
            // position - basic attack rule + attack line + door law - under
            // the actions, BEFORE the gated explanation; hideable via the new
            // settings row "Info-Text" (default ON). Missing keys skip their
            // sentence (never a blank row / dangling separator).
            if (TacticalAISettings.InfoText && Li != null)
            {
                var infoLines = new List<string>();
                if (!string.IsNullOrEmpty(Li.ordInfoRule)) infoLines.Add(Li.ordInfoRule);
                if (!string.IsNullOrEmpty(Li.ordInfoAttack)) infoLines.Add(Li.ordInfoAttack);
                if (!string.IsNullOrEmpty(Li.ordInfoDoor)) infoLines.Add(Li.ordInfoDoor);
                if (infoLines.Count > 0)
                {
                    var withInfo = new List<string>(plan.Blocks ?? new string[0]);
                    withInfo.Add(string.Join(" ", infoLines));   // internal ONE block
                    plan.Blocks = withInfo.ToArray();
                }
            }
            return true;
        }

        /// M11e: append the qualifying {A} option block to a rendered plan
        /// (no-op unless gate + law + localized ability text all hold).
        static string[] WithVillainOption(ActivationPlanData plan, global::DeploymentCard cd)
        {
            if (plan == null || !plan.HasTarget || cd == null) return plan?.Blocks;
            var ladder = TacticalAISettings.EffectiveSkill(CurrentMissionAiMode());
            var profile = string.IsNullOrEmpty(ladder) ? null : Tac.SkillProfile.For(ladder);
            if (profile == null || !Tac.VillainOptions.ActiveFor(profile, cd.id))
                return plan.Blocks;
            Tac.DeploymentCard villainCard;
            if (!_catalog.TryGetValue(cd.id, out villainCard)) return plan.Blocks;
            var opt = Tac.VillainOptions.PickLive(villainCard, OptionBalance(_lastFeedView));
            if (opt.Option == null) return plan.Blocks;
            var ab = cd.abilities;
            if (ab == null || opt.Option.AbilityIndex < 0 || opt.Option.AbilityIndex >= ab.Length)
                return plan.Blocks;   // language asset missing the print -> no block
            var a = ab[opt.Option.AbilityIndex];
            var L = UIx;
            if (L == null || string.IsNullOrEmpty(L.ordOptionPrinted)) return plan.Blocks;
            var lines = new List<string>(plan.Blocks ?? new string[0]);
            // same glyph rendering the card zoom uses for this very text
            lines.Add(TF(L.ordOptionPrinted, Saga.Utils.ReplaceGlyphs(a.name),
                                    Saga.Utils.ReplaceGlyphs(a.text)));
            return lines.ToArray();
        }

        /// Noldorion review 2.11: append the printed-ability human-lane note
        /// to a rendered plan. Fires for NON-villain groups that print at
        /// least one named ability (Officer's Order, Probe Droid's
        /// Self-Destruct, Stormtrooper's Squad Training ...). Groups with no
        /// printed abilities (none in the shipped data) and groups that
        /// already received their villain {A} option block (which quotes the
        /// printed text) get nothing. Missing language key -> no note.
        static string[] WithAbilitiesLaneNote(ActivationPlanData plan, global::DeploymentCard cd)
        {
            if (plan == null || !plan.HasTarget || cd == null) return plan?.Blocks;
            var ab = cd.abilities;
            if (ab == null || ab.Length == 0) return plan.Blocks;
            bool printed = false;
            for (int i = 0; i < ab.Length; i++)
                if (ab[i] != null && !string.IsNullOrEmpty(ab[i].name)) { printed = true; break; }
            if (!printed) return plan.Blocks;
            var L = UIx;
            if (L == null || string.IsNullOrEmpty(L.ordAbilitiesLane)) return plan.Blocks;
            var lines = plan.Blocks ?? new string[0];
            // the villain-option block already IS the printed ability -> no
            // duplicate advice. Detect it by its localized template prefix
            // (same TF formatting, empty placeholders - prefix stays).
            if (!string.IsNullOrEmpty(L.ordOptionPrinted))
            {
                var probe = TF(L.ordOptionPrinted, "", "");
                for (int i = 0; i < lines.Length; i++)
                    if (probe.Length > 4 && lines[i] != null
                        && lines[i].StartsWith(probe.Substring(0, Math.Min(probe.Length, 16)),
                                              StringComparison.Ordinal))
                        return lines;
            }
            var outp = new List<string>(lines);
            outp.Add(L.ordAbilitiesLane);
            return outp.ToArray();
        }

        /// Threat balance for the utility gate; NULL (hidden) when neither the
        /// public feed nor the ledger mirror has seen a value - an unknown
        /// balance must never SHOW a threat-spending option.
        static int? OptionBalance(Tac.FeedWorldView view)
        {
            if (view == null) return null;
            if (view.ThreatSeen) return (int)view.ThreatBalance;
            return _threatLedger != null ? (int?)_threatLedger.Balance : null;
        }

        // --------------------------------------------------------------------
        // Aufgabe 4 (Thomas 02.11): the universal action block. The fixed
        // "Aktion 1 / Aktion 2" pair is RETIRED: ONE action-point pool runs a
        // per-doctrine decision tree once per point, framed by the accent
        // head, the italic reading rule and the red loop-trap end marker
        // (" Noch Punkte uebrig? -> Baum von oben." - Thomas' corrected
        // semantics: the marker loops the tree, it does NOT skip to the next
        // figure; dice/effects belong to the actions themselves and are
        // deliberately NOT mentioned in the frame).
        // Frame A (Thomas' formatting choice 02.11): TMP tags and the {0}
        // speed slot live in the ui.json values - the language owns the
        // text, the code only assembles (ui.json law). Every tree line
        // becomes its own popup row (fixed-height TMP rows cannot carry
        // embedded newlines).
        // Attack-point counter y is DETECTED from the printed card
        // (plan.Assault <- Core catalog keyword read, Core/DeploymentCard
        // .HasAssault parses the printed Assault keyword from the card data
        // - E-Web Engineer x2 + AT-DP today, any future printed card
        // included automatically). Nothing on this surface is hardcoded.
        static string[] RenderBlocks(ActivationPlanData plan, global::DeploymentCard cd)
        {
            var L = UIx;                    // Languages/<Lang>/ui.json: uiTacticalAI
            var outp = new List<string>();
            if (L == null) return outp.ToArray();   // broken language asset -> legacy fallback
            var head = plan.Assault && !string.IsNullOrEmpty(L.apHeadAssault)
                ? TF(L.apHeadAssault, 2, 2)
                : TF(L.apHead, 2, plan.Assault ? 2 : 1);
            if (!string.IsNullOrEmpty(head)) outp.Add(head);
            if (!string.IsNullOrEmpty(L.apRule)) outp.Add(L.apRule);
            var tree = TreeFor(L, plan);
            var speed = (cd != null ? cd.speed : 0)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var line in (tree ?? "").Replace("\r", "").Split('\n'))
                if (!string.IsNullOrEmpty(line.Trim()))
                    outp.Add(line.Replace("{0}", speed));
            if (!string.IsNullOrEmpty(L.apEnd)) outp.Add(L.apEnd);
            return outp.ToArray();
        }

        // Doctrine -> universal tree key. Missing key (stale language asset)
        // falls back to the aggressive tree - never an empty popup. Tripod
        // units always take their printed emplacement tree (M13t: movement
        // and shot exclude each other), independent of the pattern - the
        // E-Web saw pattern-dependent trees before, the printed rule wins.
        // Officers (Thomas 02.11, corrected): command ability + PER-DOCTRINE
        // officer tree - the doctrine's own action wording (Wachzone,
        // Blockierfeld, cover, ambush) stays, the Order branches ride on top.
        // Elite ("Executive Order", C1 sign-off 02.11.) asks one receiver
        // question per activation - Bewegung (up to 2 spaces) OR Angriff,
        // one of the two per command, never both - and prefers its elite
        // row, falling back to the plain officer row, then the pattern tree.
        static string TreeFor(UITacticalAI L, ActivationPlanData plan)
        {
            if (!plan.HasTarget)
                return FirstNonEmpty(L.apTreeNoTarget, L.apTreeAggressive);
            if (plan.Officer)
            {
                string off = null, offElite = null;
                switch (plan.Pattern ?? "aggressive")
                {
                    case "guard":    off = L.apTreeOfficerGuard;    offElite = L.apTreeOfficerEliteGuard;    break;
                    case "blocker":  off = L.apTreeOfficerBlocker;  offElite = L.apTreeOfficerEliteBlocker;  break;
                    case "support":  off = L.apTreeOfficerSupport;  offElite = L.apTreeOfficerEliteSupport;  break;
                    case "ambusher": off = L.apTreeOfficerAmbush;   offElite = L.apTreeOfficerEliteAmbush;   break;
                    // hunter shares the aggressive officer row (mirrors the
                    // plain pattern trees, where hunter = aggressive).
                    default:         off = L.apTreeOfficerAggressive; offElite = L.apTreeOfficerEliteAggressive; break;
                }
                if (plan.OfficerElite)
                    return FirstNonEmpty(offElite, off, PatternTree(L, plan));
                return FirstNonEmpty(off, PatternTree(L, plan));
            }
            return PatternTree(L, plan);
        }

        static string PatternTree(UITacticalAI L, ActivationPlanData plan)
        {
            if (plan.Tripod) return FirstNonEmpty(L.apTreeTripod, L.apTreeAggressive);
            switch (plan.Pattern ?? "aggressive")
            {
                case "guard":    return FirstNonEmpty(L.apTreeGuard,    L.apTreeAggressive);
                case "blocker":  return FirstNonEmpty(L.apTreeBlocker,  L.apTreeAggressive);
                case "support":  return FirstNonEmpty(L.apTreeSupport,  L.apTreeAggressive);
                case "ambusher": return FirstNonEmpty(L.apTreeAmbush,   L.apTreeAggressive);
                default:         return L.apTreeAggressive;
            }
        }

        static string FirstNonEmpty(params string[] vals)
        {
            foreach (var v in vals) if (!string.IsNullOrEmpty(v)) return v;
            return null;
        }

        // ------------------------------------------------------- language files
        // PR convention (Thomas' review 27.10): every player-facing string of
        // this layer lives in Resources/Languages/<Lang>/ui.json under
        // uiTacticalAI; code only holds format keys and calls TF().
        // NOTE: slot.Why (activation ranking) is Core text - Core is the
        // vector-verified mirror of the Python sim, so its why-strings ship
        // German-first (documented deviation in the PR draft, sim i18n is a
        // separate work item).

        static UITacticalAI UIx =>
            DataStore.uiLanguage != null ? DataStore.uiLanguage.uiTacticalAI : null;

        static string TF(string fmt, params object[] args)
        {
            if (string.IsNullOrEmpty(fmt)) return "";
            try { return args.Length > 0 ? string.Format(fmt, args) : fmt; }
            catch (System.FormatException) { return fmt; }
        }

        // Punkt-2 phase vocabulary (MissionPosture vocabulary only; unknown
        // -> null, the caller skips the line - never a wrong label).
        static string PhaseName(string posture)
        {
            var L = UIx;
            if (L == null || string.IsNullOrEmpty(posture)) return null;
            switch (posture)
            {
                case "defend": return L.ordPhaseDefend;
                case "hunt":   return L.ordPhaseHunt;
                default:       return null;
            }
        }

        // Focus-transparency vocabularies (Noldorion direction reply 2.11).
        // Posture -> what the disclosed score MEASURES (OverlordFocus.
        // ThreatBase semantics, sim-pinned): eliminate/attrition = printed
        // damage per round, protect/stall = objective-runner value, deny =
        // node rules -> no score at all (helper returns null, no line).
        static string FocusMeasureName(string posture)
        {
            var L = UIx;
            if (L == null || string.IsNullOrEmpty(posture)) return null;
            switch (posture)
            {
                case "protect":
                case "stall":   return L.ordFocusMeasureRunner;
                case "eliminate":
                case "attrition": return L.ordFocusMeasureDamage;
                default:        return null;
            }
        }

        // FocusPick.Basis -> plain label (mark = defeated/wounded flip during
        // imperial activation, chip = woundAll counter chip, card = printed
        // conditioning, economy = wound-economy finish, proxy = printed
        // values only). Unknown -> null (TF keeps the raw token visible,
        // never a silently wrong label).
        static string FocusBasisName(string basis)
        {
            var L = UIx;
            if (L == null || string.IsNullOrEmpty(basis)) return "-";
            switch (basis)
            {
                case "mark":    return L.ordFocusBasisMark;
                case "chip":    return L.ordFocusBasisChip;
                case "card":    return L.ordFocusBasisConditioned;
                case "economy": return L.ordFocusBasisEconomy;
                case "proxy":   return L.ordFocusBasisPrinted;
                default:        return basis;
            }
        }

        static string DoctrineName(string p, UITacticalAI L)
        {
            switch (p)
            {
                case "guard":    return L.docGuard;
                case "blocker":  return L.docBlocker;
                case "hunter":   return L.docHunter;
                case "support":  return L.docSupport;
                case "ambusher": return L.docAmbusher;
                default:         return L.docAggressive;
            }
        }

        // ------------------------------------------------------- deployment hook

        /// <summary>GetActiveDeploymentPoint hook: deterministic choice among
        /// the already-filtered active DPs instead of the uniform random pick
        /// (concept 3.4.2: random DP = exploitable unfairness; the legacy
        /// filter list is reused verbatim). Spatial scoring (objective/zone/
        /// colour) is deferred to the hex adapter - see PR draft. Name-ordinal
        /// then GUID-ordinal keeps the choice stable across reloads.</summary>
        public static bool TryChooseDeploymentPoint(global::DeploymentCard card,
                                                    List<Saga.IMapEntity> dps, out Guid chosen)
        {
            chosen = Guid.Empty;
            if (dps == null || dps.Count == 0 || !ActiveNow()) return false;
            // Mission-EVENT deployments call this with card == null (the shared
            // GetActiveDeploymentPoint helper). Those were never part of the
            // planner's contract and must keep the legacy random line - a fixed
            // point here would cluster every reinforcement wave (audit FINAL-QA).
            if (card == null) return false;
            var best = dps
                // round-varying deterministic pick: seeded by the campaign|mission
                // key + round (all public/campaign-scoped info), so it is neither
                // hidden RNG nor a constant the players can farm (audit FINAL-QA).
                .OrderBy(x => OrdinalOf(x.name ?? "") ^ (SessionSeed() ^ (ulong)Math.Max(1, CurrentRound()) * 0x2545F4914F6CDD1DUL))
                .ThenBy(x => x.name ?? "", StringComparer.Ordinal)
                .ThenBy(x => x.GUID.ToString(), StringComparer.Ordinal)
                .First();
            chosen = best.GUID;
            Debug.Log($"TacticalAIBridge::DP -> {best.name} ({card?.name}) [deterministic, no RNG]");
            return true;
        }

        // ---------------------------------------------------------- targeting

        /// <summary>Deterministic focus WITHOUT the hex board (playtest 27.10
        /// rounds 4-6). Ranking over ALL living heroes, public information
        /// only: (1) wounded heroes first - the manual damage tracker is
        /// shared table knowledge and finishing a wounded hero is what a human
        /// Imperial always does; (2) printed firepower proxy: attack dice, then
        /// health (exact loadout EV via HeroThreatProfile/campaignItems is the
        /// M5 upgrade, the plumbing exists); (3) squad row left-to-right.
        /// NO printed preferred-target rule (Thomas round 6: real Imperial
        /// cards print no target preference - the internal preferredTargets
        /// field is legacy engine data and must never surface to players).
        /// No chance, no hidden state. Proximity stays only as order-level
        /// fallback so a decoy parked next to an Imperial cannot pull focus
        /// across the map. The WorldProvider seam replaces this with the Core
        /// FocusResolver as soon as a hex adapter exists (documented
        /// deviation).</summary>
        static bool TrySelectTarget(global::DeploymentCard cd, out string heroName, out string why)
        {
            heroName = null; why = null;
            var heroes = DataStore.deployedHeroes;
            if (heroes == null || heroes.Count == 0) return false;
            var living = heroes.Where(h => h.heroState == null || !h.heroState.isDefeated).ToList();
            if (living.Count == 0) living = heroes;    // all defeated: legacy would find none either

            // ---------------------------------------------------- M6 overlord
            // On overlord missions WITH the feed the focus layer ranks the
            // SAME living heroes (permutation only - identical legal target
            // set, fairness canon) on public threat economy + aim marks.
            // Without a feed the branch below keeps the reference ranking -
            // bit-compatible, exactly like the planning fallback.
            var ordView = EffectiveMissionMode() == "overlord" && WorldProvider == null
                ? (_lastFeedView ?? PullFeedView()) : null;
            if (ordView != null)
            {
                var cands = BuildFocusCandidates(ordView, living);
                if (cands.Count > 0)
                {
                    // Personas (M4): the card hook channels are gone - the
                    // rank law runs canon (null/0), personas never touch it.
                    var ranked = Tac.OverlordFocus.Rank(cands,
                        string.IsNullOrEmpty(ordView.Posture) ? "eliminate" : ordView.Posture,
                        null,                      // the app has no victory counter
                        // M11b: threat v2 runs against THIS figure's printed
                        // defense pool (public deployment-card data).
                        DefenseOf(cd), 0, null);
                    var dec = ranked[0];
                    _roundFocus = dec;
                    _roundFocusHero = dec.HeroId;
                    _roundFocusRound = ordView.Round;
                    var L0 = UIx;
                    if (L0 != null)     // broken language asset -> reference path (legacy popup)
                    {
                        heroName = dec.HeroId;
                        why = BuildOverlordWhy(L0, dec, heroName);
                        return true;
                    }
                }
                // no candidate could be built (asset missing) -> reference path
            }

            bool Wounded(global::DeploymentCard h) => h.heroState != null && h.heroState.isWounded;

            // ranking is total + deterministic (see doc-comment)
            var pick = living.Select((h, i) => new { h, i })
                .OrderBy(x => Wounded(x.h) ? 0 : 1)
                .ThenByDescending(x => x.h.attacks?.Length ?? 0)
                .ThenByDescending(x => x.h.health)
                .ThenBy(x => x.i)          // squad row left-to-right
                .First().h;

            var L = UIx;
            if (L == null) return false;    // broken language asset -> legacy popup
            var parts = new List<string>();
            if (Wounded(pick)) parts.Add(TF(L.focusWounded));
            parts.Add(TF(L.focusFirepower));
            // M13c (Thomas 30.10): the "kein Zufall / no chance" tail removed.
            // Thomas 02.11 (request 1): bare reason only - the caller builds
            // the short "Fokus-Ziel: {name}" line and the EXPLAIN section.
            heroName = pick.name;
            why = string.Join("; ", parts);
            return true;
        }

        // ------------------------------------------------- M6 overlord helpers

        /// <summary>Build the overlord's candidate rows from the SAME living
        /// heroes the reference path ranks (permutation-only - fairness test
        /// pins this). Inputs: the feed projection (wounded, aim marks) and
        /// the curated public threat table via the M3.1 asset seam. No
        /// positions, no hidden state, no dice.</summary>
        static List<Tac.FocusCandidate> BuildFocusCandidates(
            Tac.FeedWorldView view, List<global::DeploymentCard> living)
        {
            var outp = new List<Tac.FocusCandidate>();
            _profileByName = new Dictionary<string, Tac.HeroThreatProfile>();
            var heroViews = new Dictionary<string, Tac.FeedWorldView.HeroView>();
            foreach (var hv in view.Heroes) heroViews[hv.HeroId] = hv;
            int row = 0;
            foreach (var h in living)
            {
                var id = h.name ?? h.id;
                Tac.FeedWorldView.HeroView hv;
                heroViews.TryGetValue(id, out hv);
                var ch = CampaignHeroFor(id);
                var prof = TacticalAIAssets.ProfileFor(id, ch);
                _profileByName[id] = prof;
                outp.Add(new Tac.FocusCandidate
                {
                    HeroId = id,
                    SquadRow = row++,
                    Profile = prof,
                    // M11b: the printed hero card (null only for rows
                    // outside the 21-hero app roster -> curated fallback).
                    Card = TacticalAIAssets.CardFor(id),
                    // feed fact first (deterministic public history); the live
                    // hero state is the fallback for heroes the feed never saw
                    Wounded = hv != null ? hv.Wounded : (h.heroState != null && h.heroState.isWounded),
                    Marks = hv != null ? hv.Marks : 0,
                    MarkedRound = hv != null ? hv.MarkedRound : 0,
                    AttackDice = h.attacks?.Length ?? 0,
                    Health = h.health,
                    // M10b: event-derived objective credit (view-level, the
                    // feed names the hero - never a cell/position read).
                    ObjectiveActor = view != null && !string.IsNullOrEmpty(view.ObjectiveActor)
                        && System.StringComparer.Ordinal.Equals(view.ObjectiveActor, id),
                    // M11d READY-TO-WIRE (the honest half): the strain BUFFER
                    // is public printed data and is fully wired - card
                    // reactions + the hero's current unspent campaign XP
                    // (purchase-screen law). The MARKERS are the missing
                    // input: the app does not model strain markers anywhere
                    // (M11d research: no StrainMarker/stress state exists -
                    // only the DG 'exhausted' card flag), so this stays null
                    // and the pressure term is provably inert live. POLLING
                    // SEAM: when endurance/strain polling lands (concept
                    // M11d 'Ausdauer-Polling erforschen'), pass the per-hero
                    // marker count here; budget, ranking, cap and the
                    // ordStrainPressure WHY cell already work end-to-end
                    // (pinned by overlord_focus.json m11d_* cases).
                    StrainMarkers = null,
                    AvailableXp = ch != null ? (int?)ch.xpAmount : null,
                    // M11h: the FUZZY usage prior's only input - completed
                    // hero activations from the PUBLIC feed projection
                    // (HeroView.Activations counts the HeroActivated events
                    // every table player sees; nothing hidden, nothing
                    // guessed). No feed row for this hero = no round source
                    // = null = the prior stays silent for him (fallback law:
                    // never rate what we cannot see). Mode gate: this whole
                    // builder only ever runs on the tactical-overlord focus
                    // path - reference and the S1-S3 legacy planners never
                    // build candidates here, so they stay bit-identical.
                    RoundsCompleted = hv != null ? (int?)hv.Activations : null,
                });
            }
            return outp;
        }

        /// <summary>M11b: the engaging imperial figure's printed defense
        /// pool as Core die names (Dice.DefenseDieFaces keys). Public
        /// deployment-card data; unknown/absent figures -> no blocks.</summary>
        static System.Collections.Generic.List<string> DefenseOf(global::DeploymentCard cd)
        {
            var outp = new System.Collections.Generic.List<string>();
            if (cd == null || cd.defense == null) return outp;
            foreach (var d in cd.defense)
            {
                var name = d.ToString();
                if (name == "Black" || name == "White") outp.Add(name);
            }
            return outp;
        }

        /// <summary>Campaign loadout lookup (app-managed public data, concept
        /// 3.3.1a). Solo missions / missing campaign -> null -> curated-only
        /// profile, which is exactly the documented fallback.</summary>
        static Saga.CampaignHero CampaignHeroFor(string heroName)
        {
            try
            {
                var camp = Saga.RunningCampaign.sagaCampaign;
                if (camp == null || camp.campaignHeroes == null || string.IsNullOrEmpty(heroName))
                    return null;
                return camp.campaignHeroes.FirstOrDefault(x => x.heroID == heroName);
            }
            catch { return null; }
        }

        // Personas (M4): the M12d card-effect hook channel (ImperialDeckLive
        // / ImperialCardHooks / ActiveImperialCardInfos) and the agenda
        // readers (AgendaLive / audit views / AgendaClassification) are GONE
        // with the class-deck + agenda-card system they read. Personas touch
        // NONE of the planner's number channels (fairness law) - they drive
        // costs, doctrine share, guard band and texts through their own
        // hooks (EnsurePersona / DoctrineFor / BuildGroups / CostFor).
        // OverlordFocus.Rank keeps its null channels (canon default).

        /// <summary>Round-level designation on the feed state (focus fire:
        /// every group plans towards the SAME target; the order plan decides
        /// WHO walks it first - Mehrgruppenzug). Recomputed on activation when
        /// the feed moved (a hero falling wounded re-ranks chip-first).</summary>
        static void ComputeRoundFocus(Tac.FeedWorldView view, string posture)
        {
            var heroes = DataStore.deployedHeroes;
            if (heroes == null) return;
            var living = heroes.Where(h => h.heroState == null || !h.heroState.isDefeated).ToList();
            if (living.Count == 0) return;
            var cands = BuildFocusCandidates(view, living);
            if (cands.Count == 0) return;
            // Personas (M4): canon channels (personas never touch focus math).
            var ranked = Tac.OverlordFocus.Rank(cands, posture ?? "eliminate", null,
                                                null, 0, null);
            _roundFocus = ranked.Count > 0 ? ranked[0] : null;
            _roundFocusHero = _roundFocus != null ? _roundFocus.HeroId : null;
            _roundFocusRound = view.Round;
            _roundFocusRunnerUp = ranked.Count > 1 ? ranked[1] : null;
            _roundFocusPosture = posture ?? "eliminate";
        }

        /// <summary>Threat-economy mirror (concept 3.9, the calibrated
        /// save/idle levers come from the skill profile - "wie in der Sim
        /// kalibriert"). Fed ONLY by feed facts: per-round income tick with
        /// the feed's own living-count, no estimation. The public HUD balance
        /// (first observed ThreatSpend) stays ground truth; the ledger model
        /// is the economic context BEFORE the first spend.</summary>
        static void EnsureThreatMirror(Tac.SkillProfile skill, Tac.FeedWorldView view, int roundNo)
        {
            if (_threatLedger == null)
                _threatLedger = new Tac.ThreatLedger(skill != null ? skill.EconSave : 2,
                                                     skill != null ? skill.EconIdle : 2);
            if (_ledgerRound >= roundNo) return;
            int alive = 0;
            foreach (var g in view.Groups)
                if (g.Deployed && !g.Removed) alive += Math.Max(0, g.Alive);
            var from = _ledgerRound < 0 ? 1 : _ledgerRound + 1;
            for (var r = from; r <= roundNo; r++) _threatLedger.Tick(r, alive);
            _ledgerRound = roundNo;
        }

        static int RiskLedgerBalance(Tac.FeedWorldView view)
        {
            long b = view.ThreatSeen ? view.ThreatBalance
                   : (_threatLedger != null ? _threatLedger.Balance : view.ThreatBalance);
            if (b < 0) b = 0;
            return b > int.MaxValue ? int.MaxValue : (int)b;
        }

        /// <summary>Threat-weighted positioning (concept 3.5.1, A.4): the
        /// sim-calibrated RiskPosture press/hold/guard_hold/disengage per
        /// group - the coverage <-> fire-line trade runs on AppView facts
        /// (wheel vitality, printed replace cost vs threat balance, the focus
        /// hero's printed range band). rho = the skill profile's bändigung
        /// lever. The German sim Why strings stay in the log (vectors pin
        /// them); the player surface is the localized position label only.</summary>
        static void BuildRiskPositions(Tac.SkillProfile skill, string posture,
                                       Tac.FeedWorldView view, List<Tac.GroupFacts> facts)
        {
            _riskByGroup = new Dictionary<string, Tac.RiskDecision>();
            Tac.HeroThreatProfile focusProf = null;
            if (_roundFocusHero != null && _profileByName != null)
                _profileByName.TryGetValue(_roundFocusHero, out focusProf);
            foreach (var f in facts)
            {
                var gv = view.FindGroup(f.GroupId);
                int? replaces = null;
                if (_catalog != null && _catalog.TryGetValue(f.CardId, out var core))
                    replaces = core.Cost;
                var rf = new Tac.RiskFacts
                {
                    GroupId = f.GroupId,
                    Pattern = f.Pattern ?? "aggressive",
                    Posture = posture ?? "eliminate",
                    Counter = null,          // the app carries no victory counter
                    Vitality = gv != null ? gv.VitalityFrac : Tac.Rational.One,
                    RingNear = false,        // belief layer needs the tile graph (open)
                    OnAnchor = false,        // anchors need mission geometry (open)
                    ReplacesAt = replaces,
                    LedgerBalance = RiskLedgerBalance(view),
                    ThreatIgnoreLos = focusProf != null && focusProf.IgnoreLos,
                    ThreatRangeMax = focusProf != null ? focusProf.RangeMax : 0,
                };
                try { _riskByGroup[f.GroupId] = Tac.RiskPosture.RiskPosition(rf, skill != null ? skill.Rho : 3); }
                catch (Exception e) { Debug.LogWarning("TacticalAIBridge::risk " + f.GroupId + ": " + e.Message); }
            }
        }

        /// <summary>The public WHY line (A.6): every part names a fact a
        /// human GM also has - mark, printed threat, wound state, the shared
        /// focus. Localized via ui.json; no numbers the table cannot see.</summary>
        static string BuildOverlordWhy(UITacticalAI L, Tac.FocusPick dec, string heroName)
        {
            var parts = new List<string>();
            if (dec.Marked) parts.Add(TF(L.ordMarked));
            if (dec.Threat.HasValue)
                parts.Add(TF(L.ordThreat, Tac.Py.F2(dec.Threat.Value.ToDouble())));
            else parts.Add(TF(L.focusFirepower));
            if (dec.Wounded) parts.Add(TF(L.focusWounded));
            // M11d: only when the pressure actually fired this round
            if (dec.StrainDruck.HasValue && dec.StrainDruck.Value == Tac.StrainPressure.Druck)
                parts.Add(TF(L.ordStrainPressure));
            // M11h: the fuzzy round prior speaks ONLY when it fired and the
            // exact rule above stayed silent - and it says "wahrscheinlich"
            // (probably): a prior is never stated as knowledge ("hat vollen
            // Strain" would be a lie - the app cannot see markers).
            else if (dec.StrainDruckUnscharf.HasValue && dec.StrainDruckUnscharf.Value > 0
                     && dec.StrainPriorRunden.HasValue && dec.StrainPriorDecke.HasValue)
                parts.Add(TF(L.ordStrainFuzzy, dec.StrainPriorRunden.Value, dec.StrainPriorDecke.Value));
            // M13c (Thomas 30.10): the "alle Gruppen richten sich auf
            // dasselbe Fokusziel; kein Zufall" tail removed by Thomas.
            // Thomas 02.11 (request 1): the name/prefix wrapper is GONE here -
            // the caller prints the short target line, this returns the bare
            // reason for the EXPLAIN section.
            return string.Join("; ", parts);
        }

        static string RiskLabel(UITacticalAI L, string position)
        {
            string v;
            switch (position)
            {
                case "press": v = L.ordRiskPress; break;
                case "hold": v = L.ordRiskHold; break;
                case "guard_hold": v = L.ordRiskGuardHold; break;
                case "disengage": v = L.ordRiskDisengage; break;
                default: return null;
            }
            return string.IsNullOrEmpty(v) ? null : v;
        }

        // ------------------------------------------------------------- explain

        static string ExplainFor(string groupId, ActivationPlanData plan)
        {
            if (TacticalAISettings.ExplainLevel == "none") return null;
            if (TacticalAISettings.ExplainLevel == "full")
            {
                string line = null;
                // Thomas 02.11 (request 3): the section header makes the
                // explain block readable as a block; content lines keep
                // their own labels (one section per popup row). If NOTHING
                // below fires, the bare header is dropped (never a lone
                // "ERKLÄRUNG" row).
                var Lh = UIx;
                if (Lh != null && !string.IsNullOrEmpty(Lh.ordExplainHead))
                    line = Lh.ordExplainHead;
                // Thomas 02.11 (declutter): the persona line moved out of
                // every activation's action area into this section - who you
                // play against, rank, active bonuses (self-labelled line).
                if (plan != null && !string.IsNullOrEmpty(plan.PersonaLine))
                    line = (line == null ? "" : line + "\n") + plan.PersonaLine;
                var slot = _slots.FirstOrDefault(s => s.GroupId == groupId);
                if (slot != null && UIx != null)
                    // (append, not assign - the header row stays on top)
                    line = (line == null ? "" : line + "\n")
                           + TF(UIx.whyPrefix) + slot.Why + " " + TF(UIx.whyRank, slot.Rank + 1);
                // M6: the threat-weighted risk POSITION for this group
                // (concept 3.5.1). Localized label only - the sim-pinned
                // German Why/Fallback texts stay in the Debug log, never in
                // the player surface (no hardcoded game text, ui.json law).
                if (_riskByGroup != null && UIx != null &&
                    _riskByGroup.TryGetValue(groupId, out var rd))
                {
                    var label = RiskLabel(UIx, rd.Position);
                    if (label != null)
                        line = (line == null ? "" : line + "\n") + TF(UIx.ordRiskLabel, label);
                    Debug.Log("TacticalAIBridge::risk " + groupId + ": " + rd.Position +
                              " why: " + rd.Why + " | fallback: " + rd.Fallback +
                              (string.IsNullOrEmpty(rd.Anchor) ? "" : " anchor: " + rd.Anchor));
                }
                // Punkt-2: the phase flip is also explainable in-place.
                if (UIx != null && !string.IsNullOrEmpty(UIx.ordPhaseExplain)
                    && _doctrineEffective != null && _doctrineBase != null
                    && _doctrineEffective != _doctrineBase)
                {
                    var phaseLine = TF(UIx.ordPhaseExplain,
                        PhaseName(_doctrineBase), PhaseName(_doctrineEffective));
                    line = (line == null ? "" : line + "\n") + phaseLine;
                }
                // M13u (variant c): the per-round doctrine dice is fully
                // transparent - the 53-bit draw as 0-99 (the exact match
                // window of the rolled pattern), the round and the card id,
                // so a player can replay any activation. All data is public
                // (asset + mission structure).
                // M6p (Thomas 02.11): the doctrine NAME left this block - it
                // renders permanently right after the focus-target line now
                // (ActivationPlanData.DoctrineLine). The roll row keeps only
                // the draw value: the name would be a double display, and the
                // explain section must not contain the doctrine any more.
                if (UIx != null && !string.IsNullOrEmpty(UIx.ordPatternRoll)
                    && _patternRoll.TryGetValue(groupId, out var rolled))
                {
                    var bits = rolled.Split('|');
                    if (bits.Length >= 2)
                    {
                        var rollLine = TF(UIx.ordPatternRoll, bits[1]);
                        line = (line == null ? "" : line + "\n") + rollLine;
                        Debug.Log("TacticalAIBridge::patternRoll " + groupId +
                                  ": " + rolled + " posture=" + _doctrineEffective);
                    }
                }
                // Thomas 02.11 (request 1): the focus-target reasoning moved
                // out of the action area into this section (the line above
                // the blocks now only names the target).
                if (UIx != null && plan != null && plan.HasTarget
                    && !string.IsNullOrEmpty(plan.FocusReason)
                    && !string.IsNullOrEmpty(UIx.ordExplainFocusWhy))
                    line = (line == null ? "" : line + "\n")
                           + TF(UIx.ordExplainFocusWhy, plan.TargetName, plan.FocusReason);
                // Noldorion direction reply 2.11 ("it just needs to be very
                // transparent how exactly that power score is calculated"):
                // EXPLAIN=VOLL discloses the REAL round-focus computation -
                // the score, what it measures in this mission posture, the
                // wound economy, the decision basis and the runner-up. All
                // numbers are the fixture-pinned sim values; every input is
                // a public table artifact (printed hero card + wound mark +
                // mission goal). Round focus only - never the activation
                // order (post_hoc leak law untouched).
                if (UIx != null && !string.IsNullOrEmpty(UIx.ordFocusDetail)
                    && _roundFocus != null && _roundFocus.Threat.HasValue)
                {
                    var focusLine = TF(UIx.ordFocusDetail,
                        _roundFocus.HeroId ?? "",
                        _roundFocus.Threat.Value.ToString(),
                        FocusMeasureName(_roundFocusPosture),
                        FocusBasisName(_roundFocus.Basis),
                        _roundFocusRunnerUp != null && _roundFocusRunnerUp.Threat.HasValue
                            ? _roundFocusRunnerUp.HeroId + " ("
                              + _roundFocusRunnerUp.Threat.Value + ")"
                            : "-");
                    line = (line == null ? "" : line + "\n") + focusLine;
                }
                // Noldorion review 2.11 (PR #194): the class cards that move
                // the planner's numbers become VISIBLE. Announce exactly when
                // the strength/efficiency stage fires (OrderPlan cardsFire
                // semantics) - the named cards are public campaign data once
                // bought, and the printed text is already in the IMPERIUM
                // popup; this is the missing "what is actually in play" line.
                // The German sim-pinned Kartenwirkung why stays Debug.log only
                // (no hardcoded game text on the player surface, ui.json law).
                // Personas (M4): the class-card announce block is GONE -
                // personas own this display slot now (plan.PersonaLine).
                // request 3: never ship a lone header row.
                if (line != null && Lh != null && line == Lh.ordExplainHead) return null;
                return line;
            }
            // Defensive default (unreachable since 02.11: ExplainLevelValues
            // is exactly { "none", "full" }).
            return null;
        }

        static string CurrentMissionAiMode()
        {
            try { return DataStore.sagaSessionData?.setupOptions?.aiMode; }
            catch { return null; /* session not up yet */ }
        }

        static string EffectiveMissionMode()
        {
            return TacticalAISettingsUnity.MissionMode(CurrentMissionAiMode());
        }

        // ------------------------------------------------------------- plumbing

        static int CurrentRound()
        {
            try { return Math.Max(1, DataStore.sagaSessionData?.gameVars?.round ?? 1); }
            catch { return 1; }
        }

        static ulong SessionSeed()
        {
            if (_sessionSeed != 0) return _sessionSeed;
            var key = SessionKey() ?? "solo";
            ulong h = 1469598103934665603UL;
            foreach (var c in key) { h ^= c; h *= 1099511628211UL; }
            if (h == 0) h = 0xA5A5A5A5A5A5A5A5UL;
            _sessionSeed = h;
            return h;
        }

        // M11 (Thomas 31.10.): EnsureDecks/OrderDeckManager are GONE with the
        // order decks - the activation blocks are synthesized from the
        // figure's printed facts (see TryBuildActivationPlanInner).

        static void EnsureCatalog()
        {
            if (_catalog != null) return;
            _catalog = new Dictionary<string, Tac.DeploymentCard>();
            // M4 (Thomas 29.10): doctrines are SITUATION-driven. The mission
            // posture decides whether anyone may guard AT ALL; the printed
            // traits then sort the roster. Both inputs are public (mission
            // data + printed cards), never rebel positions.
            var posture = MissionPosture();
            // BOTH card pools: the app keeps unique villains (DG071-DG090 -
            // Vader, Palpatine, Boba Fett, Maul, Thrawn, ...) in a SEPARATE
            // villainCards list. Iterating only deploymentCards left the
            // whole villain roster out of the catalog, so every unique
            // villain missed the round deal and its activation popup fell
            // back to the legacy instructions (Thomas bug 4+5, 29.10.:
            // "Palpatine/Vader show the old actions" - all 20 were hit).
            IEnumerable<global::DeploymentCard> pools = DataStore.deploymentCards
                ?? Enumerable.Empty<global::DeploymentCard>();
            if (DataStore.villainCards != null) pools = pools.Concat(DataStore.villainCards);
            foreach (var cd in pools)
            {
                if (cd.isDummy) continue;
                // M11: shipped-tactical-support membership (was: has an order
                // deck). The unique villains (DG071-DG090) live in the app's
                // SEPARATE villainCards list and are NOT characterType
                // Imperial - the whitelist lets exactly the printed shipped
                // roster in, custom imports stay legacy.
                bool shipped = TacticalShippedIds.Contains(cd.id);
                if (cd.characterType != Saga.CharacterType.Imperial && !shipped) continue;
                if (_catalog.ContainsKey(cd.id)) continue;
                try
                {
                    var core = Tac.DeploymentCard.FromRecord(ToRecord(cd));
                    core.Pattern = ClassifyPattern(core, posture);
                    _catalog[cd.id] = core;
                }
                catch (Exception e) { Debug.LogWarning("TacticalAIBridge::catalog skip " + cd.id + ": " + e.Message); }
            }
            var sb = new System.Text.StringBuilder("TacticalAIBridge::doctrines posture=" + posture + ":");
            foreach (var kv in _catalog) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value.Pattern);
            Debug.Log(sb.ToString());
        }

        /// Punkt-2 phase rule: compute base/effective doctrine posture for
        /// this round and announce the flip ONCE per mission session (QM +
        /// one Debug breadcrumb). Round 1 never flips (limit<3 guard: no
        /// degenerate "final third" on mission start).
        static void SetDoctrinePhase(int roundNo)
        {
            _doctrineBase = MissionPosture();
            _doctrineEffective = _doctrineBase;
            if (_doctrineBase == "defend")
            {
                long lim = DoctrineRoundLimit();
                if (lim >= 3 && roundNo >= lim - Math.Max(1, lim / 3))
                    _doctrineEffective = "hunt";
            }
            // guard band membership for a defend deployment (count computed
            // in BuildGroups; DoctrineFor consumes it): recompute per round,
            // wiped the moment the effective posture stops being defend.
            if (_doctrineEffective != "defend") { _guardBandCount = 0; _guardIds = null; }
            if (!_phaseShiftAnnounced && _doctrineEffective != _doctrineBase)
            {
                _phaseShiftAnnounced = true;
                var L = UIx;
                if (L != null && !string.IsNullOrEmpty(L.ordPhaseShiftQM))
                {
                    try
                    {
                        var qm = GlowEngine.FindUnityObject<QuickMessage>();
                        if (qm != null) qm.Show(TF(L.ordPhaseShiftQM, roundNo, DoctrineRoundLimit()));
                    }
                    catch { /* cosmetic */ }
                }
                Debug.Log("TacticalAIBridge::doctrine phase " + _doctrineBase + " -> "
                          + _doctrineEffective + " r" + roundNo + " limit=" + DoctrineRoundLimit());
            }
        }

        /// Punkt-2 phase rule (see SetDoctrinePhase caller comment): one
        /// group's doctrine = ClassifyPattern x effective posture, PLUS the
        /// defend guard band. A defend deployment of small ranged shooters
        /// (the plain trooper roster) is a human's split decision, not a
        /// 100 % advance: the BACK HALF of the deployment order (public squad
        /// order) holds the objective zone as guard, the front half keeps the
        /// printed advance role. S4/S5 only - the reference brain stays the
        /// simple printed-role doctrine (chess-model: doctrine depth is the
        /// overlord rung's play strength, not a hidden buff). Zero position
        /// knowledge: the trigger is the deployment order, never the board.

        // ---------------------------------------------------------- personas
        /// <summary>Personas (M2/M3): refresh the live persona view at the
        /// mission threat; announce rank changes
        /// ONCE per mission (the rank is a pure (persona, threat) function -
        /// see Core.Personas rank law). M6l: the former mission-subsidy
        /// backstop left with the retired gift kinds - v2.1 personas grant
        /// nothing (Delta-0 law, Beschluss 11).</summary>
        static void EnsurePersona()
        {
            var camp = LiveCampaign();
            _personaView = PersonaState.ResolveLive();
            if (_personaQMAnnounced || _personaView.Rank <= 0 || camp == null) return;
            _personaQMAnnounced = true;
            string qm = null;
            try
            {
                var tracked = camp.personaRank ?? new Saga.PersonaRankMark();
                int old = string.Equals(tracked.personaId, _personaView.Id, System.StringComparison.Ordinal)
                          ? tracked.rank : 0;
                if (old != _personaView.Rank)
                {
                    var line = PersonaState.RankChangeText(_personaView, old, _personaView.Rank > old);
                    qm = string.IsNullOrEmpty(qm) ? line
                         : (string.IsNullOrEmpty(line) ? qm : qm + "\n" + line);
                    tracked.personaId = _personaView.Id;
                    tracked.rank = _personaView.Rank;
                    camp.personaRank = tracked;
                    try { camp.SaveCampaignState(); } catch { }
                }
            }
            catch { return; }
            if (string.IsNullOrEmpty(qm)) return;
            try
            {
                var q = GlowEngine.FindUnityObject<QuickMessage>();
                if (q != null) q.Show(qm);
            }
            catch { /* cosmetics never break a round */ }
            Debug.Log("TacticalAIBridge::persona " + _personaView.Id + " rank " + _personaView.Rank);
        }

        static Saga.SagaCampaign LiveCampaign()
        {
            try { return Saga.RunningCampaign.sagaCampaign; }
            catch { return null; }
        }

        /// <summary>The instruction-card persona line for one card (internal
        /// name + printed faction from the live roster; the internal name IS
        /// DeploymentCard.name - translations only re-skin the UI).</summary>
        static string PersonaLine(string cardId)
        {
            if (_personaView.IsEmpty) return null;
            try
            {
                string faction = null;
                var pool = DataStore.deployedEnemies;
                if (pool != null)
                    foreach (var c in pool)
                        if (c != null && c.id == cardId) { faction = c.faction; break; }
                return PersonaState.DescribeLine(_personaView, cardId, faction);
            }
            catch { return null; }
        }

        /// <summary>Does any active guard_split row apply to this catalog
        /// card (internal-name match)?</summary>
        static bool PersonaGuardApplies(Tac.DeploymentCard core)
        {
            if (_personaView.IsEmpty || core == null) return false;
            foreach (var e in _personaView.Active)
                if (e.GuardMatches(core.Name, null)) return true;
            return false;
        }

        /// <summary>Guard share for the band; -1 = keep the 50 % default.
        /// Uses the FIRST band member (a mission whose roster mixes several
        /// guard_split rows keeps the default - no silent pick winner).</summary>
        static int PersonaGuardPercent(List<string> bandIds)
        {
            if (_personaView.IsEmpty || bandIds == null || bandIds.Count == 0) return -1;
            try
            {
                string name = null, faction = null;
                foreach (var c in DataStore.deployedEnemies)
                    if (c != null && c.id == bandIds[bandIds.Count - 1])
                    { name = c.name; faction = c.faction; break; }
                if (name == null) return -1;
                return _personaView.GuardPercentFor(name, faction);
            }
            catch { return -1; }
        }

        static string DoctrineFor(Tac.DeploymentCard core, string cardId, string posture)
        {
            var pat = ClassifyPattern(core, posture);
            if (posture == "defend" && _guardBandCount > 0 && _guardIds != null
                && _guardIds.Contains(cardId)
                && core != null && core.AttackType == "Ranged" && !core.IsTripod)
            {
                var ladder = TacticalAISettings.EffectiveSkill(CurrentMissionAiMode());
                var prof = string.IsNullOrEmpty(ladder) ? null : Tac.SkillProfile.For(ladder);
                if (prof != null && Tac.CardKeywords.SkillReadsCards(prof)) pat = "guard";
            }
            // Personas: the seeded doctrine-share flip on top (pure hash of
            // persona+session seed+round+card - the same mission replays
            // every flip; no persona -> the base doctrine unchanged).
            if (!_personaView.IsEmpty)
                pat = _personaView.PatternFor(core != null ? core.Name : null, null,
                                              cardId, pat, SessionSeed(), CurrentRound());
            return pat;
        }

        /// EffectiveRoundLimit semantics mirrored from WorldEventsUnity
        /// (user's roundLimitToggle included - a clock the player switched
        /// off must never drive doctrine). Same source of truth, same
        /// priority: toggle, then runtime var, then authored mission data.
        static long DoctrineRoundLimit()
        {
            try
            {
                if (PlayerPrefs.GetInt("roundLimitToggle", 1) == 0) return -1;
            }
            catch { }
            try
            {
                long rt = DataStore.sagaSessionData?.gameVars?.roundLimit ?? -1;
                if (rt >= 0) return rt;
            }
            catch { }
            try
            {
                var mp = DataStore.mission?.missionProperties;
                return mp != null && mp.roundLimit >= 0 ? mp.roundLimit : -1;
            }
            catch { return -1; }
        }

        /// M4 posture from STRUCTURED mission data (never event texts, so no
        /// language file is ever a rules source):
        ///  "defend" - the mission puts something to defend on the board
        ///           (terminals the Imperials work for/protect, or an ally
        ///           priority target the mission cares about);
        ///  "hunt"   - nothing to defend: the Imperials must run the rebels
        ///           down. (The old "in a hunt NOBODY gets Wachschutz" law was
        ///           retired by Thomas on 03.11.: doctrine comes from the live
        ///           dice law — hunt rolls the unmodified weight rows, defend
        ///           is the single modifier row. tactical_pattern_weights.json
        ///           is the only rules source, this comment never was.)
        /// Null mission (classic mode etc.) -> hunt (plain annihilation).
        static string MissionPosture()
        {
            try
            {
                var m = DataStore.mission;
                if (m == null) return "hunt";
                if (m.missionProperties != null &&
                    m.missionProperties.priorityTargetType == Saga.PriorityTargetType.Ally) return "defend";
                if (m.mapEntities != null)
                    foreach (var e in m.mapEntities)
                        if (e != null && e.entityType == Saga.EntityType.Terminal) return "defend";
                return "hunt";
            }
            catch (Exception e)
            {
                Debug.LogWarning("TacticalAIBridge::posture fail-open hunt: " + e.Message);
                return "hunt";
            }
        }

        static Tac.PatternWeightTable PatternWeightTable()
        {
            // M13u: the shipped pattern-weight table (lazy, cached in Core;
            // null only when the asset cannot load - the dice then stay dark
            // and ClassifyPattern falls back to the legacy branch).
            try { return TacticalAIAssets.PatternWeightsRoot; }
            catch { return null; }
        }

        /// M4 printed-trait x posture classification (fairness whitelist only:
        /// Id/Speed/AttackType/MiniSize/Keywords - all printed, all public).
        /// defend: immobile AND melee figures hold the objective zone (several
        ///   guards per Thomas), Massive/large bases block the lanes to it,
        ///   mobile shooters keep the counter-attack fire line (aggressive).
        /// hunt:   zero guards. Large bases cut escape lanes (blocker), fast
        ///   melee chases (hunter), everything else advances (aggressive).
        /// Manual: DG071 IG-88 always hunter - its printed Relentless pursuit
        ///   is the tracker role (matches its own deck design note).
        static string ClassifyPattern(Tac.DeploymentCard c, string posture)
        {
            // Printed movement law BEFORE every dice (dev 41): a deployed
            // Stativ/Tripod is immobile - the Wache-assignment is not a
            // probability. Same for the DG071 hunter rule (see below).
            if (c.IsTripod) return "guard";

            // M13u (Thomas 01.11., variante c): the weighted per-round
            // doctrine dice replaces the posture CODE BRANCH. Every group
            // rolls its printed-flavour distribution (public asset
            // tactical_pattern_weights.json); the mission structure shifts
            // it through the defend MODIFIER ROW, never through different
            // code. Seed = mission + card + round: one stable doctrine per
            // group per round, replay-identical, EXPLAIN-disclosable.
            // No asset / no row -> legacy branch stays authoritative
            // (bit-identical fallback, all pre-M13u fixtures untouched).
            var tbl = PatternWeightTable();
            if (tbl != null && tbl.IsActive)
            {
                var dist0 = tbl.WeightsFor(c.Id, c.Name);
                if (dist0 != null)
                {
                    var dist = Tac.PatternWeightTable.FilterIllegal(
                        tbl.Effective(dist0, posture), c.IsMassive);
                    int draw;
                    var pat = tbl.Roll(dist, SessionSeed(), CurrentRound(), c.Id, out draw);
                    if (pat != null)
                    {
                        _patternRoll[c.Id] = pat + "|" + draw + "|" +
                             string.Join(" ", System.Linq.Enumerable.Select(
                                System.Linq.Enumerable.OrderBy(dist.Keys, k => k,
                                    StringComparer.Ordinal),
                                k => k + ":" + dist[k]));
                        c.Pattern = pat;    // keep the catalog in sync
                        return pat;
                    }
                }
            }
            return ClassifyPatternLegacy(c, posture);
        }

        /// M13u: legacy doctrine branch (kept bit-identical as the
        /// no-asset fallback - see ClassifyPattern wrapper).
        static string ClassifyPatternLegacy(Tac.DeploymentCard c, string posture)
        {
            if (c.Id == "DG071") return "hunter";
            // M13t (Thomas 30.10., EMPIRE6 playtest): a Stativ/Tripod figure
            // may never hold a MOVING role. Its printed rule forbids moving
            // and attacking in the same activation, so the whole value of the
            // figure is its fire lane. DG007/DG008 (E-Web-Schütze) were being
            // read as big bases -> "blocker" (engage the lane, reposition);
            // that made the AI walk the emplacement toward the rebels and
            // waste the attack. The correct doctrine is "guard": hold the
            // ground it was placed on (the objective / the lane the rebels
            // must cross), do NOT chase, and pour fire into anything that
            // steps into the Wachzone. Guard is the one pattern whose Move-
            // Shackle keeps it home and whose attack fires on zone entry -
            // exactly the "unter Feuer nehmen, wenn die zum Fluchtpunkt
            // kommen" behaviour. This overrides the big-base branch below.
            if (c.IsTripod) return "guard";
            bool big = c.IsLargeBase || c.IsMassive;
            if (posture == "defend")
            {
                if (big) return "blocker";
                if (c.Speed <= 2) return "guard";
                if (c.AttackType == "Melee") return "guard";
                return "aggressive";
            }
            if (big) return "blocker";
            if (c.AttackType == "Melee" && c.Speed >= 5) return "hunter";
            return "aggressive";
        }

        /// <summary>Only the PRINTED card values go through (fairness
        /// whitelist Core DeploymentCard.CardFieldWhitelist); no bonus data.</summary>
        static Dictionary<string, object> ToRecord(global::DeploymentCard cd)
        {
            // M13t (Thomas 30.10., EMPIRE6 playtest): the printed
            // Stativ/Tripod rule must reach the doctrine layer. Source is
            // the card's own printed ability line (En asset says "Tripod",
            // De asset says "Stativ"; other language files are partial
            // overlays whose runtime ability list is filled from the EN
            // backup, so both names cover every shipped language). DG-id
            // belt: across ALL shipped language assets only DG007/DG008
            // print Stativ - the same printed-fact class as the DG071
            // hunter rule. Never read from hidden bonus data.
            var kws = new List<string>(cd.keywords ?? new string[0]);
            bool tripod = false, assault = false, order = false, execOrder = false;
            var ab = cd.abilities;
            if (ab != null)
                foreach (var a in ab)
                {
                    if (a == null) continue;
                    if (a.name == "Tripod" || a.name == "Stativ") tripod = true;
                    // Noldorion playtest 2.11: printed Assault in any shipped
                    // language name (De "Sturm", Ru "Штурм", Fr "Assaut";
                    // partial overlays fall back to the EN name).
                    if (a.name == "Assault" || a.name == "Sturm" ||
                        a.name == "Штурм" || a.name == "Assaut") assault = true;
                    // Thomas 02.11 (Player-17): the printed command abilities.
                    // DETECTION LIVES ON THE ABILITY LANE, NOT cd.ignored -
                    // SetCardTranslations() overwrites cd.ignored with the
                    // LANGUAGE card's value and EVERY shipped language card
                    // asset carries ignored="" there (verified 02.11: 0 of 69
                    // En overlay cards carry the lane), so the raw
                    // CardData/enemies.json "ignored" lane never survives
                    // into a live session card. The ability lane DOES survive
                    // (De+En assets print it; the 9 partial overlays fall
                    // back to the EN name, same as Tripod/Assault):
                    //   En  "{A} Order" / "{A} Executive Order"
                    //   De  "{A} Befehl" / "{A} Vollstreckungsbefehl"
                    if (a.name == "{A} Order" || a.name == "{A} Befehl") order = true;
                    if (a.name == "{A} Executive Order" ||
                        a.name == "{A} Vollstreckungsbefehl") execOrder = true;
                }
            if (cd.id == "DG007" || cd.id == "DG008") tripod = true;
            if (tripod && !kws.Contains("Tripod")) kws.Add("Tripod");
            // DG-id belt (same printed-fact class as the Tripod/Assault
            // belts): verified 02.11 across ALL shipped language assets -
            // exactly DG004/DG005 print the plain command ability and DG006
            // prints the executive one (officer trio of the Imperial core).
            if (cd.id == "DG004" || cd.id == "DG005") order = true;
            if (cd.id == "DG006") execOrder = true;
            if (order && !kws.Contains("OfficerOrder")) kws.Add("OfficerOrder");
            if (execOrder && !kws.Contains("OfficerExecOrder")) kws.Add("OfficerExecOrder");
            // DG-id belt (same printed-fact class as the Tripod belt): across
            // ALL shipped language assets (base pools + 11 overlays) exactly
            // these four cards print Assault: DG007/DG008 (E-Web, Elite),
            // DG046 (AT-DP), DG071 (IG-88). Verified 2.11 programmatically.
            if (cd.id == "DG007" || cd.id == "DG008" ||
                cd.id == "DG046" || cd.id == "DG071") assault = true;
            if (assault && !kws.Contains("Assault")) kws.Add("Assault");
            return new Dictionary<string, object>
            {
                { "id", cd.id },
                { "name", cd.name },
                { "subname", cd.subname ?? "" },
                { "miniSize", cd.miniSize.ToString() },
                { "speed", cd.speed },
                { "health", cd.health },
                { "attackType", cd.attackType.ToString() },
                { "attacks", (cd.attacks ?? new DiceColor[0]).Select(a => a.ToString()).ToArray() },
                { "keywords", kws.ToArray() },
                { "isElite", cd.isElite },
                { "defense", (cd.defense ?? new DiceColor[0]).Select(a => a.ToString()).ToArray() },
                { "surges", cd.surges ?? new string[0] },
                { "cost", cd.cost },
                { "characterType", "Imperial" },
            };
        }

        /// <summary>Group facts + Core figures (round-deal order) from the
        /// deployed enemy list. One GroupFacts per group id (= card id), the
        /// same identity the legacy group manager uses. The M5 feed view, when
        /// present, fills vitalityFrac from the human's damage WHEELS
        /// (concept 3.3.3 Nr. 2b: real attrition strength for the OrderPlanner
        /// strength-efficiency matrix); groups the feed never saw stay 1.</summary>
        static void BuildGroups(out List<Tac.GroupFacts> facts, out List<Tac.Figure> figures,
                                Tac.FeedWorldView feedView = null)
        {
            EnsureCatalog();
            facts = new List<Tac.GroupFacts>();
            figures = new List<Tac.Figure>();
            var posture = _doctrineEffective ?? MissionPosture();
            var seen = new HashSet<string>();
            // Punkt-2 guard band (first pass): who WOULD advance in this
            // posture (ClassifyPattern's small ranged shooters). The back
            // half of the public deployment order holds instead - split into
            // _guardIds, consumed by DoctrineFor in the second pass.
            if (posture == "defend")
            {
                var advancers = new List<string>();
                var seen1 = new HashSet<string>();
                foreach (var cd in DataStore.deployedEnemies)
                {
                    if (cd == null || cd.isDummy || cd.currentSize <= 0) continue;
                    if (!seen1.Add(cd.id)) continue;
                    if (!_catalog.TryGetValue(cd.id, out var c1)) continue;
                    if (ClassifyPattern(c1, posture) == "aggressive"
                        && c1.AttackType == "Ranged" && !c1.IsTripod
                        // Personas: when a persona authors a guard_split row,
                        // only ITS matching groups share the band (e.g. the
                        // captain bands his stormtroopers, an AT-ST keeps the
                        // printed blocker role). No matching row -> the
                        // uniform 50 % default (dev 41) stays.
                        && (_personaView.IsEmpty || PersonaGuardApplies(c1)))
                        advancers.Add(cd.id);
                }
                int pct = PersonaGuardPercent(advancers);   // -1 = 50 % default
                int back = pct < 0 ? advancers.Count / 2
                                   : advancers.Count * pct / 100;
                _guardBandCount = back;
                _guardIds = new HashSet<string>(
                    advancers.Skip(advancers.Count - back).Take(back));
            }
            else { _guardBandCount = 0; _guardIds = null; }
            int idx = 0;
            foreach (var cd in DataStore.deployedEnemies)
            {
                if (cd == null || cd.isDummy) continue;
                if (cd.currentSize <= 0) continue;             // wiped group
                if (!seen.Add(cd.id)) continue;                // first instance = the group
                if (!_catalog.TryGetValue(cd.id, out var core)) continue;  // unknown card: legacy fallback
                var gv = feedView != null ? feedView.FindGroup(cd.id) : null;
                var vitality = gv != null ? gv.VitalityFrac : Tac.Rational.One;
                var pattern = DoctrineFor(core, cd.id, posture);
                facts.Add(new Tac.GroupFacts(
                    cd.id, idx, cd.id, cd.name,
                    pattern: pattern,                          // M4 + Punkt-2 band
                    nAlive: Math.Max(1, cd.currentSize),
                    vitalityFrac: vitality,
                    zone: null, arrivedRound: null,
                    isHeavy: core.IsLargeBase, isSwarm: false));
                var fig = new Tac.Figure
                {
                    Id = cd.id, GroupId = cd.id, Card = core, Team = "imperial",
                    Anchor = new Tac.Cell(0, 0), Orientation = 0,
                    Vitality = core.Health, Index = idx, Alive = true, Pattern = pattern,
                };
                figures.Add(fig);
                idx++;
            }
        }

        static Tac.Figure MakeFigure(global::DeploymentCard cd, int index)
        {
            EnsureCatalog();
            if (cd == null || !_catalog.TryGetValue(cd.id, out var core)) return null;
            return new Tac.Figure
            {
                Id = cd.id, GroupId = cd.id, Card = core, Team = "imperial",
                Anchor = new Tac.Cell(0, 0), Orientation = 0,
                Vitality = core.Health, Index = Math.Max(0, index), Alive = true, Pattern = core.Pattern,
            };
        }

        static int IndexOf(global::DeploymentCard cd)
        {
            var list = DataStore.deployedEnemies;
            for (int i = 0; i < list.Count; i++) if (list[i] == cd) return i;
            return -1;
        }
    }
}