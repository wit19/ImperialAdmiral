// M5 Scope B (concept 3.3.1b): the PASSIVE world-feed adapter. A runtime
// AddComponent through the bridge's session start (TacticalAIBridge
// .EnsureFeed) - NO prefab surgery, NO scene edits, NO new player input.
// It only READS state the App maintains anyway for the game flow itself:
//
//   * missionLogger.logItems      -> Spawn/Death/Removal/ActivationStart
//                                    (SagaDGPrefab/SagaHGPrefab log these
//                                    for the mission log UI anyway)
//   * DeploymentCard.woundTrackerValue -> GroupDamage (the human's damage
//                                    wheels; read-only diff, the wheel UI
//                                    needs nothing from us)
//   * HeroState.isWounded/isDefeated/hasActivated -> HeroWounded/HeroDefeated/
//                                    HeroActivated (squad HUD state)
//   * gameVars.round              -> RoundEnd
//   * gameVars.currentThreat      -> ThreatSpend
//   * gameVars.currentObjective   -> ObjectiveChange
//   * missionProperties           -> MissionStart (posture input, 3.3.1b #4)
//   * DataStore.mission.mapEntities (Door.doorOpen, Door.cs:18)
//                                 -> DoorOpen/DoorClosed (R-A: the DIGITAL
//                                    door state, flipped only by app/event
//                                    plumbing - ModifyMapEntity ->
//                                    MapEntityManager.ModifyEntityCoroutine
//                                    -> DoorPrefab.ModifyEntity. A door
//                                    opened physically at the table never
//                                    moves the field and stays invisible;
//                                    never guessed. The mapEntities list is
//                                    the SAME object list the prefabs were
//                                    instantiated from (SagaController.cs:461
//                                    -> InstantiateEntities), so the data
//                                    read needs no scene traversal.)
//   * TileManager.IsMapSectionActive (S-A: section visibility, MapSection
//                                    .isActive flipped ONLY at
//                                    TileManager.ActivateMapSection /
//                                    DeactivateMapSection - the single
//                                    choke; the mission-event body that
//                                    carries mm.mapSection runs exactly
//                                    this lane, MissionEventHandler
//                                    MapManagement branch)
//                                    -> SectionActivated {section GUID,
//                                    name, round}; deactivation is
//                                    snapshot-tracked WITHOUT a kind (the
//                                    concept consumes activation only; a
//                                    re-activation re-emits honestly).
//   * entityProperties.isActive on Token/Terminal/Crate entities (S-A: the
//                                    same ModifyEntity plumbing lane R-A
//                                    proved for doors) -> EntityInteract
//                                    {entity GUID, name, section?, active,
//                                    round} - both directions, direction
//                                    is data. Doors keep their own R-A
//                                    pair; DP/Highlight/Tile isActive is
//                                    plumbing, not interaction. Live reads
//                                    go through MapEntityManager.GetEntity
//                                    so the poll follows the post-restore
//                                    object copies too (restore instantiates
//                                    from the SAVE list, SagaController
//                                    ContinueGame -> RestoreState).
//   * missionLogger PlayerSelection lines (PromptBox button/cancel,
//                                    PromptBox.cs:80/:95) -> PlayerSelection
//                                    {name} - the S-A 5.4 join channel:
//                                    consumer-side seq/round adjacency
//                                    between "the player chose X" and the
//                                    isActive/door flip it caused.
//
// Reset at (session-)mission start; a seq anomaly (restored session: the
// mission logger list shrinks below our drain index) triggers RESYNC = a
// re-log of the complete history from current state, never a guess.
//
// Every log line starts with "WorldEvents::" - that is the player.log
// surface future diagnosis reads. All payloads are PUBLIC information; the
// Core feed validates kind+key vocabulary fail-loud (leak invariant 7.1).
using System;
using System.Collections.Generic;
using UnityEngine;
using PlayerPrefs = UnityEngine.PlayerPrefs;
using Tac = TacticalAI.Core;

namespace TacticalAI
{
    [DisallowMultipleComponent]
    public sealed class WorldEventsUnity : MonoBehaviour
    {
        public Tac.WorldEventFeed Feed { get; private set; }

        string _sessionKey;
        int _drainedLog;                 // missionLogger.logItems drain cursor
        int _round;                      // gameVars.round we last saw
        long _threat;                    // currentThreat we last saw
        string _objective;               // currentObjective we last saw
        bool _firstLogged;               // "WorldEvents::first" line once
        float _pollAccum;

        // passive snapshots (diff sources) - keyed by card id / hero name
        readonly Dictionary<string, int[]> _wheels = new Dictionary<string, int[]>();
        readonly HashSet<string> _seenGroups = new HashSet<string>();
        readonly HashSet<string> _heroWounded = new HashSet<string>();
        readonly HashSet<string> _heroDefeated = new HashSet<string>();
        readonly HashSet<string> _heroActivated = new HashSet<string>();
        // M6s-B7: heroes whose end-of-turn checkbox flip is already in the
        // mission log (name#round) - the HUD flag poll must not double-count.
        readonly HashSet<string> _heroLoggedActivated = new HashSet<string>();
        // R-A: digital door state snapshot (door GUID -> last polled
        // doorOpen). Polling dedup = snapshot diff, the exact law of the
        // damage dials: one event per OBSERVED state change, never per frame
        // the state stays flipped, never for flips we did not witness (a
        // door first seen mid-mission is baselined without an event).
        readonly Dictionary<string, bool> _doors = new Dictionary<string, bool>();
        // S-A: section visibility snapshot (section GUID -> last OBSERVED
        // live isActive via TileManager.IsMapSectionActive - the accessor
        // the game itself answers from, so it stays correct after a save
        // restore rebinds TileManager to the save-state copies). The
        // snapshot tracks BOTH directions; only false->true carries the
        // SectionActivated kind. Same polling dedup law as the dials.
        readonly Dictionary<string, bool> _sections = new Dictionary<string, bool>();
        // S-A: interactable entity flags (Token/Terminal/Crate GUID -> last
        // observed live entityProperties.isActive, read through
        // MapEntityManager.GetEntity - the manager's list holds the very
        // objects the prefabs mutate, on the fresh AND the restore path).
        readonly Dictionary<string, bool> _entities = new Dictionary<string, bool>();
        // scene-bound controller ref; a destroyed one compares == null and
        // is re-found on the next poll (scene reload / save-restore).
        Saga.SagaController _ctrlCache;

        // M8/F5 mission-clock watch: the authored/runtime roundLimit only
        // EXIST if the player's roundLimitToggle setting lets the app show
        // and enforce it (Utils.IsRoundLimitReachedWithSetting: the limit
        // event fires at setting==1, the 'dangerous' demo at setting==2,
        // NEVER at setting==0). Emitting the start snapshot anyway gave the
        // planner a stall posture with a clock that will never run.
        long _lastRoundLimit = -2;      // -2 = never read yet
        bool _runtimeDirty;             // event rewired the clock mid-mission

        /// <summary>M8/F5 seam: MissionEventHandler calls this when a
        /// ModifyRoundLimit event actually changed gameVars.roundLimit - the
        /// feed re-reads the clock on the next poll (the start snapshot
        /// alone would leave the planner on a stale clock).</summary>
        public void MarkRuntimeDirty() { _runtimeDirty = true; }

        /// <summary>The clock as the GAME will treat it: -1 (no clock) when
        /// the user switched the round limit off, else runtime value, else
        /// authored value (runtime wins once the session booted).</summary>
        static long EffectiveRoundLimit(Saga.SagaGameVars gv, Saga.MissionProperties mp)
        {
            try
            {
                if (PlayerPrefs.GetInt("roundLimitToggle", 1) == 0) return -1;   // user: clock OFF
            }
            catch { }
            long rt = gv != null ? gv.roundLimit : -1;
            if (rt >= 0) return rt;
            return mp != null && mp.roundLimit >= 0 ? mp.roundLimit : -1;
        }

        const float PollInterval = 0.25f;    // seconds; UI-speed state, no game coupling

        void Awake()
        {
            Feed = new Tac.WorldEventFeed("pending");
        }

        void Update()
        {
            try
            {
                if (!TacticalAISettings.Enabled) return;
                _pollAccum += Time.unscaledDeltaTime;
                if (_pollAccum < PollInterval) return;
                _pollAccum = 0f;
                Poll();
            }
            catch (Exception e)
            {
                // the feed must NEVER break the game; loss is visible in the log
                Debug.LogWarning("WorldEvents::poll error (ignored): " + e.Message);
            }
        }

        /// <summary>Synchronous pull used by the bridge before EVERY decision
        /// (and by tests): ingest current state, then project it.</summary>
        public Tac.FeedWorldView ProjectNow()
        {
            try { Poll(); }
            catch (Exception e) { Debug.LogWarning("WorldEvents::pull error (ignored): " + e.Message); }
            if (Feed == null) return null;
            return Tac.WorldEventVector.Project(Feed.Events, Feed.MissionKey, Feed.Hash());
        }

        /// <summary>S-C belief rail (card t_865d5997, concept 8.1): the
        /// SectionSpace projection for ONE computation, pulled by the bridge
        /// exactly like ProjectNow. Producers, all data-of-existence:
        ///  * seeds: the mission's DeploymentPoint entities' own
        ///    mapSectionOwner - the S-A result, the digital picker carries no
        ///    readable per-group DP (R-B finding), so every deployed group
        ///    gets the CANDIDATE SET of DP sections (concept 5.6 law:
        ///    kandidatenmenge, never a precision lie). No DP section readable
        ///    -> unseeded belief, probes simply never fire for it.
        ///  * doorEdges + doorOwners: mined from the shipped
        ///    tactical_mission_critical table for THIS mission (missionID =
        ///    the SessionKey tail); runtimeSwapped rows are skipped by the
        ///    miner. Missing rows keep pairs unknown-PERMEABLE (Grundgesetz).
        ///  * refinement: the Thomas-Gesetz 04.11. skill curve - low rungs
        ///    (S1/S2/reference/no profile) read the coarse chain-entry
        ///    belief; the event-driven exceptions are ungated in Core.
        /// RESYNC LAW: full replay with prior=null EVERY pull, never a
        /// continuation - after a feed Resync (re-log replaces history) the
        /// hypotheses are buried and only re-logged facts survive; an
        /// incremental continuation would double-count the re-logged window.
        /// Loch-Gesetz: everything here is GUIDs and app names, never a
        /// coordinate; the vector's coordinate-free Public() surface is
        /// untouched by this rail.</summary>
        public Tac.SectionSpace ProjectSectionSpaceNow()
        {
            try { Poll(); }
            catch (Exception e) { Debug.LogWarning("WorldEvents::section pull error (ignored): " + e.Message); }
            if (Feed == null) return null;
            try
            {
                var opts = new Tac.SectionOptions
                {
                    Refine = Tac.SectionSkill.MaskRefinementEnabled(SessionSkillProfile()),
                };
                var table = TacticalAIAssets.MissionCriticalRoot;
                var missionId = MissionIdFromKey(Feed.MissionKey);
                List<string[]> edges = null;
                if (table != null && !string.IsNullOrEmpty(missionId))
                {
                    var owners = Tac.SectionTrust.DoorOwnersOf(table, missionId);
                    if (owners != null && owners.Count > 0) opts.DoorOwners = owners;
                    edges = Tac.SectionTrust.MissionDoorEdges(table, missionId);
                    if (edges != null && edges.Count == 0) edges = null;
                }
                var space = Tac.SectionSpace.Project(Feed.Events, null, BuildDeploySeeds(), edges, opts);
                LogExplainTrail(space);
                return space;
            }
            catch (Exception e)
            {
                // the belief rail must NEVER break a decision: the bridge
                // plans on LastSectionSpace == null exactly like "no feed".
                Debug.LogWarning("WorldEvents::section space projection failed (belief skipped): " + e.Message);
                return null;
            }
        }

        Tac.SkillProfile SessionSkillProfile()
        {
            try
            {
                return Tac.SkillProfile.For(TacticalAISettings.EffectiveSkill(
                    DataStore.sagaSessionData?.setupOptions?.aiMode));
            }
            catch { return null; }   // unknown token / session not up: coarse rung
        }

        static string MissionIdFromKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int bar = key.IndexOf('|');
            return bar < 0 ? null : key.Substring(bar + 1);
        }

        // DP-marker seeds (S-A check 1 outcome: candidate set). Identity
        // law: group keys are the SAME cd.id strings PollGroups Spawns with.
        static Dictionary<string, string[]> BuildDeploySeeds()
        {
            var seeds = new Dictionary<string, string[]>(StringComparer.Ordinal);
            try
            {
                var ents = DataStore.mission != null ? DataStore.mission.mapEntities : null;
                if (ents == null) return seeds;
                var dpSections = new List<string>();
                foreach (var me in ents)
                {
                    if (me == null || me.entityType != Saga.EntityType.DeploymentPoint) continue;
                    Guid own;
                    try { own = me.mapSectionOwner; } catch { continue; }
                    if (own == Guid.Empty) continue;
                    var s = own.ToString();
                    if (!dpSections.Contains(s)) dpSections.Add(s);
                }
                if (dpSections.Count == 0) return seeds;   // nothing readable: unseeded
                var arr = dpSections.ToArray();
                foreach (var cd in DataStore.deployedEnemies)
                {
                    if (cd == null || cd.isDummy || string.IsNullOrEmpty(cd.id)) continue;
                    seeds[cd.id] = arr;   // kandidatenmenge, not a per-group claim
                }
            }
            catch { }
            return seeds;
        }

        // EXPLAIN disclosure: whenever the evidence chain MOVED (an event
        // changed a belief), log the whole trail once. Lines carry
        // EREIGNIS + REGEL + section GUID/name only - no coordinates. A
        // Player.log grep for "SectionSpace::" replays why the belief moved.
        string _lastExplainTrail = "";
        void LogExplainTrail(Tac.SectionSpace space)
        {
            var trail = space.ExplainTrail();
            var joined = string.Join("\n", trail.ToArray());
            if (joined == _lastExplainTrail) return;
            _lastExplainTrail = joined;
            foreach (var line in trail) Debug.Log("SectionSpace::" + line);
        }

        // ------------------------------------------------------------------
        void Poll()
        {
            var sd = DataStore.sagaSessionData;
            if (sd == null || sd.gameVars == null) return;
            string key = SessionKey();
            if (key == null) return;
            if (key != _sessionKey) SessionStart(key);

            var gv = sd.gameVars;
            int round = Math.Max(1, gv.round);

            // restore/resync detector: a reloaded session replaces the log;
            // our cursor beyond the list end can only mean a seq discontinuity
            var logger = sd.missionLogger;
            if (logger != null && logger.logItems != null && logger.logItems.Count < _drainedLog)
            {
                _drainedLog = 0;
                Resync();
            }

            // round boundary: the finished round becomes public fact
            if (round != _round)
            {
                if (_round > 0) Append("RoundEnd", _round, null);
                _round = round;
            }

            // M8/F5 clock watch: ModifyRndLimit events (MarkRuntimeDirty)
            // surface here as the public event RoundLimitChanged - the feed
            // gate re-derives posture; a start snapshot alone went stale.
            if (_runtimeDirty || _lastRoundLimit == -2)
            {
                _runtimeDirty = false;
                long lim = EffectiveRoundLimit(gv, TryMissionProperties());
                if (_lastRoundLimit != -2 && lim != _lastRoundLimit)
                    Append("RoundLimitChanged", round, P("roundLimit", lim));
                _lastRoundLimit = lim;
            }

            // M6s-B7 causal order (Player-19 law): damage first, then the
            // table's words about it. The wheel dials are polled BEFORE the
            // logger drain and the objective HUD, so a shot observed in the
            // same batch as the shooter's end-of-turn checkbox line keeps the
            // causal sequence shot -> close (strike credit depends on it).
            PollGroups(round);

            // R-A: digital door flips (snapshot diff, one event per observed
            // change). Independent of the shot->close causal ordering above.
            PollDoors(round);

            // S-A: section visibility + interactable entity flags (same
            // snapshot-dedup law as the doors; audit-trail facts, no causal
            // ordering obligation against the shot->close pair).
            PollSections(round);
            PollEntities(round);

            // threat + objective (public HUD state)
            long threat = gv.currentThreat;
            if (threat != _threat)
            {
                if (_threat != 0 || Feed.Count > 0)
                    Append("ThreatSpend", round, P("balance", threat));
                _threat = threat;
            }
            var obj = gv.currentObjective;
            if (!string.Equals(obj ?? "", _objective ?? ""))
            {
                Append("ObjectiveChange", round, P("objective", obj ?? ""));
                _objective = obj ?? "";
            }

            // mission logger: what the App wrote for the player's own log
            DrainLogger(round);

            // hero markers (checkbox fallback for sessions without the
            // M6s-B7 activation lines)
            PollHeroes(round);
        }

        void SessionStart(string key)
        {
            _sessionKey = key;
            Feed = new Tac.WorldEventFeed(key);
            TacticalAIBridge.Feed = Feed;
            _drainedLog = 0;
            _round = 0;
            _threat = 0;
            _objective = null;
            _wheels.Clear();
            _seenGroups.Clear();
            _heroWounded.Clear();
            _heroDefeated.Clear();
            _heroActivated.Clear();
            _heroLoggedActivated.Clear();
            _doors.Clear();
            // S-A: section/entity snapshots reset per session; their FIRST
            // readable poll claims the initial visible/hidden state as
            // resync facts (see PollSections/PollEntities) - the tile and
            // entity managers are not guaranteed to be populated yet at
            // this very moment, so no baseline is taken here.
            _sections.Clear();
            _entities.Clear();
            _ctrlCache = null;

            var sd = DataStore.sagaSessionData;
            var mp = TryMissionProperties();
            string mission = mp != null ? (mp.missionName ?? mp.missionID ?? "mission") : "mission";
            string objective = (sd.gameVars.currentObjective ?? null)
                               ?? (mp != null ? mp.startingObjective : null) ?? "";
            _objective = objective;
            _round = Math.Max(1, sd.gameVars.round);
            _threat = sd.gameVars.currentThreat;
            _lastRoundLimit = EffectiveRoundLimit(sd.gameVars, mp);
            Append("MissionStart", _round, new Dictionary<string, object>
            {
                { "objective", objective },
                { "priorityTarget", mp != null ? mp.priorityTargetType.ToString() : "Rebel" },
                // M8/F5: the user's roundLimitToggle decides whether a clock
                // exists at all (setting 0 = off: the game never runs the
                // limit event); runtime value wins over authored (-1 = none).
                { "roundLimit", _lastRoundLimit },
                { "mission", mission },
                { "missionType", mp != null ? mp.missionType.ToString() : "Story" },
            });
            _objective = objective;   // MissionStart is the objective baseline;
                                      // ObjectiveChange only fires on real change.

            // baseline the wheels WITHOUT emitting events (state at feed
            // start is not a delta), and remember what is on the board.
            foreach (var cd in DataStore.deployedEnemies)
            {
                if (cd == null || cd.isDummy) continue;
                _wheels[cd.id] = (int[])cd.woundTrackerValue.Clone();
                _seenGroups.Add(cd.id);
            }
            // R-A: same law for the digital door states (a door authored
            // open at mission start is baseline, not an event).
            foreach (var d in MissionDoors())
                _doors[d.GUID.ToString()] = d.doorOpen;
            Debug.Log("WorldEvents::session " + key + " mission=" + mission
                      + " objective=\"" + objective + "\"");
        }

        // -------------------------------------------------------- logger feed
        void DrainLogger(int round)
        {
            var sd = DataStore.sagaSessionData;
            var logger = sd == null ? null : sd.missionLogger;
            if (logger == null || logger.logItems == null) return;
            var items = logger.logItems;
            while (_drainedLog < items.Count)
            {
                var it = items[_drainedLog++];
                if (it == null || string.IsNullOrEmpty(it.message)) continue;
                int r = Math.Max(1, it.round);
                // M8/F1: LogItem.message is the LOCALIZED rich-text piece
                // (label + colored NAME); the raw card name is the last
                // color span - the raw-text compare of M5 never matched in
                // the shipping game and leaked labels into GroupId/hash.
                var name = Tac.MissionLogText.ExtractRawName(it.message);
                if (string.IsNullOrEmpty(name)) continue;
                switch (it.missionLogType)
                {
                    case Saga.MissionLogType.GroupDefeated:
                        {
                            // heroes and groups share the log kind - resolve
                            // by what is actually deployed (public state)
                            if (_heroDefeated.Contains(name) || IsHeroName(name))
                                Append("HeroDefeated", r, P("hero", name));
                            else
                                Append("Death", r, P("group", IdForName(name)));
                            break;
                        }
                    case Saga.MissionLogType.GroupRemoved:
                        Append("Removal", r, P("group", IdForName(name)));
                        break;
                    case Saga.MissionLogType.GroupActivation:
                        // M6s-B7: heroes now WRITE this line too (SagaHGPrefab
                        // logs the end-of-turn checkbox flip) - the log order
                        // IS the table order, so "the hero whose turn closed
                        // after the event did it" runs on real, causally
                        // ordered data (M10b law finally fed correctly).
                        // Imperial popups keep their ActivationStart meaning.
                        if (!IsHeroName(name)) Append("ActivationStart", r, P("group", IdForName(name)));
                        else if (_heroLoggedActivated.Add(name + "#" + r))
                            Append("HeroActivated", r, P("hero", name));
                        break;
                    case Saga.MissionLogType.PlayerSelection:
                        // S-A (concept 5.4): the prompt-button choice the
                        // player made (PromptBox.cs:95 button / :80 cancel)
                        // joins the stream as a typed fact. The projection
                        // stays inert (audit trail + hash); the JOIN is
                        // consumer-side seq/round adjacency to the
                        // EntityInteract/DoorOpen flips the choice caused.
                        // MissionLogText.ExtractRawName returns the value-
                        // colour span = the raw button text (the Player-
                        // Selection format is label-span + value-span, same
                        // law as GroupActivation).
                        Append("PlayerSelection", r, P("name", name));
                        break;
                }
            }
        }

        // ------------------------------------------------------- group wheels
        void PollGroups(int round)
        {
            // R-B card point 3 asked whether the Spawn payload can carry the
            // DEPLOY POINT ("dp") without new inputs. VERDICT: NO - the
            // honest ceiling is "ist gespawnt (Runde r)" (+ the Core-side
            // SpawnedInHeroWindow marker). Proof from the shipped deploy
            // paths: there is NO group->DP ledger to read.
            // GetActiveDeploymentPoint is a pure CHOOSE function and records
            // nothing (MapEntityManager.cs:501-534); the ONE recording is
            // DeploymentManager.lastActiveDPByCard, written ONLY on the
            // ovrd.Active branch (DeploymentManager.cs:327-332). The routine
            // flows never write it: starting groups and the
            // reinforcement/landing/onslaught popups run the ovrd==null
            // branch (DeployGroupList -> DeploymentManager.cs:376, called
            // from SagaDeploymentPopup.cs:211), and the DoMultipleDeployment
            // custom-override list is highlight-only
            // (DeploymentManager.cs:416-440). Emitting "dp" for the narrow
            // override flows alone would make a MISSING key ambiguous
            // ("unreadable flow" vs "no DP") - a guessing surface for every
            // consumer. NIE RATEN: the key stays out of the vocabulary until
            // a real deploy-path ledger exists (own card).
            foreach (var cd in DataStore.deployedEnemies)
            {
                if (cd == null || cd.isDummy) continue;
                if (!_seenGroups.Contains(cd.id))
                {
                    _seenGroups.Add(cd.id);
                    _wheels[cd.id] = (int[])cd.woundTrackerValue.Clone();
                    Append("Spawn", round, new Dictionary<string, object>
                    {
                        { "group", cd.id },
                        { "size", (long)Math.Max(1, cd.size) },
                        // Player-19: the deployment-card display name (the
                        // same name the table reads). Public; powers the
                        // designation collision law and the strike law.
                        { "name", cd.name ?? "" },
                    });
                    continue;
                }
                int[] prev;
                if (!_wheels.TryGetValue(cd.id, out prev))
                {
                    // the card list rebuilt under us (save-restore): wheel
                    // history is gone -> full re-log, never a guess
                    prev = (int[])cd.woundTrackerValue.Clone();
                    _wheels[cd.id] = prev;
                    Resync();
                    return;
                }
                int n = Math.Min(Math.Max(cd.size, 1), Math.Min(prev.Length, cd.woundTrackerValue.Length));
                for (int i = 0; i < n; i++)
                {
                    int cur = cd.woundTrackerValue[i];
                    if (cur == prev[i]) continue;
                    prev[i] = cur;
                    Append("GroupDamage", round, new Dictionary<string, object>
                    {
                        { "group", cd.id },
                        { "figure", (long)i },
                        { "wheel", (long)cur },
                        { "health", (long)cd.health },
                    });
                }
            }
        }

        // ---------------------------------------------------------- door state
        // R-A: poll the DIGITAL door state. The list DataStore.mission
        // .mapEntities is the very object list the door prefabs wrap
        // (SagaController.cs:461 -> InstantiateEntities), so Door.doorOpen
        // read here is the same field DoorPrefab.ModifyEntity flips
        // (DoorPrefab.cs:108-120 via MapEntityManager.ModifyEntityCoroutine)
        // - no scene traversal, works for hidden sections too. Dedup law =
        // snapshot diff (one event per observed change). Honest boundary:
        // a PHYSICALLY opened table door never moves this field and stays
        // invisible to the feed; we never infer it from figure traffic.
        void PollDoors(int round)
        {
            foreach (var d in MissionDoors())
            {
                string id = d.GUID.ToString();
                bool prev;
                if (!_doors.TryGetValue(id, out prev))
                {
                    // first sight mid-mission: baseline WITHOUT an event -
                    // we did not witness the flip, so we claim nothing.
                    _doors[id] = d.doorOpen;
                    continue;
                }
                if (d.doorOpen == prev) continue;
                _doors[id] = d.doorOpen;
                Append(d.doorOpen ? "DoorOpen" : "DoorClosed", round,
                       new Dictionary<string, object>
                       {
                           { "door", id },
                           { "name", d.name ?? "" },
                       });
            }
        }

        static System.Collections.Generic.IEnumerable<Saga.Door> MissionDoors()
        {
            var ents = DataStore.mission != null ? DataStore.mission.mapEntities : null;
            if (ents == null) yield break;
            foreach (var me in ents)
            {
                var d = me as Saga.Door;
                if (d != null) yield return d;
            }
        }

        // ------------------------------------------------ S-A section state
        // S-A (card t_00fda230): section visibility is polled through the
        // app's OWN accessor TileManager.IsMapSectionActive - the live list
        // it answers from is DataStore.mission.mapSections on the fresh path
        // and the RESTORED copy after save/restore (SagaController
        // ContinueGame -> tileManager.RestoreState rebinds the list;
        // DataStore.mission is re-read from the mission file and would read
        // dead there). GUID/name identity stays the mission file's catalog.
        // Loch-Gesetz: the payload carries the section GUID + the app's own
        // section name - never tiles, never cell coordinates.
        void PollSections(int round)
        {
            var tm = TileManagerOrNull();
            if (tm == null || !tm.tilesLoaded) return;   // not readable yet:
            var secs = DataStore.mission != null ? DataStore.mission.mapSections : null;
            // never baseline on an empty/partial view (door law applies:
            // first sight is only trusted once the manager answers).
            if (secs == null) return;
            foreach (var s in secs)
            {
                if (s == null) continue;
                string id = s.GUID.ToString();
                bool live = tm.IsMapSectionActive(s.GUID);
                bool prev;
                if (!_sections.TryGetValue(id, out prev))
                {
                    // first sight: baseline. An ALREADY visible section is
                    // claimed as current-state fact (resync:true) - the feed
                    // started after the activation and S-B needs the initial
                    // visible inventory deterministically.
                    _sections[id] = live;
                    if (live)
                        Append("SectionActivated", round, new Dictionary<string, object>
                        {
                            { "section", id },
                            { "name", s.name ?? "" },
                            { "resync", true },
                        });
                    continue;
                }
                if (live == prev) continue;
                _sections[id] = live;
                // Deactivation: snapshot updated, NO kind (concept consumes
                // activation only; a later re-activation re-emits honestly).
                if (!live) continue;
                Append("SectionActivated", round, new Dictionary<string, object>
                {
                    { "section", id },
                    { "name", s.name ?? "" },
                });
            }
        }

        // ------------------------------------------------- S-A entity flags
        // S-A: Token/Terminal/Crate entityProperties.isActive diff. Live
        // reads through MapEntityManager.GetEntity (the manager's list holds
        // the objects the prefabs mutate on BOTH paths - fresh and restore);
        // the mission file's list is the identity catalog only. entity
        // .entityProperties is replaced wholesale by ModifyEntity(props)
        // (TerminalPrefab.cs ModifyEntity), so the read goes through the
        // entity object EVERY poll, never a cached props reference.
        void PollEntities(int round)
        {
            var mem = EntityManagerOrNull();
            var ents = DataStore.mission != null ? DataStore.mission.mapEntities : null;
            if (mem == null || ents == null) return;
            foreach (var me in ents)
            {
                if (me == null) continue;
                // Interactable set is the concept's 5.1 list minus the door
                // (its own R-A kind; DoorPrefab normalizes isActive=true at
                // init). DP/Highlight/Tile isActive = validity/visibility
                // plumbing - feeding it would be a false "interaction".
                if (me.entityType != Saga.EntityType.Token
                    && me.entityType != Saga.EntityType.Terminal
                    && me.entityType != Saga.EntityType.Crate) continue;
                string id = me.GUID.ToString();
                var live = mem.GetEntity(me.GUID) ?? me;
                bool cur = live.entityProperties != null && live.entityProperties.isActive;
                bool prev;
                if (!_entities.TryGetValue(id, out prev))
                {
                    // first sight: baseline. The ctor default for isActive
                    // is TRUE, so a deviating hidden entity is a current-
                    // state fact worth claiming (resync:true); entities in
                    // their default state stay silent (not ~30 no-op events).
                    _entities[id] = cur;
                    if (!cur)
                        Append("EntityInteract", round, EntityPayload(id, live, cur, true));
                    continue;
                }
                if (cur == prev) continue;
                _entities[id] = cur;
                Append("EntityInteract", round, EntityPayload(id, live, cur, false));
            }
        }

        static Dictionary<string, object> EntityPayload(string id, Saga.IMapEntity e,
                                                        bool active, bool resync)
        {
            var p = new Dictionary<string, object>
            {
                { "entity", id },
                { "name", e.name ?? "" },
                { "active", active },
            };
            // Data-existence law: the section anchor rides ONLY when the
            // entity's OWN mapSectionOwner is set - a fact of the data
            // model (swap-corrected live object), never an estimate.
            try { if (e.mapSectionOwner != Guid.Empty) p["section"] = e.mapSectionOwner.ToString(); }
            catch { }
            if (resync) p["resync"] = true;
            return p;
        }

        Saga.SagaController Ctrl()
        {
            try
            {
                if (_ctrlCache == null) _ctrlCache = FindObjectOfType<Saga.SagaController>();
                return _ctrlCache;
            }
            catch { return null; }
        }

        Saga.TileManager TileManagerOrNull()
        {
            var c = Ctrl();
            return c != null ? c.tileManager : null;
        }

        Saga.MapEntityManager EntityManagerOrNull()
        {
            var c = Ctrl();
            return c != null ? c.mapEntityManager : null;
        }

        // -------------------------------------------------------- hero markers
        void PollHeroes(int round)
        {
            foreach (var cd in DataStore.deployedHeroes)
            {
                var hs = cd.heroState;
                if (hs == null) continue;
                var name = cd.name;
                if (hs.isDefeated && !_heroDefeated.Contains(name))
                {
                    _heroDefeated.Add(name);
                    Append("HeroDefeated", round, P("hero", name));
                }
                if (hs.isWounded && !_heroWounded.Contains(name))
                {
                    _heroWounded.Add(name);
                    Append("HeroWounded", round, P("hero", name));
                }
                if (hs.hasActivated != null)
                    for (int i = 0; i < hs.hasActivated.Length; i++)
                    {
                        if (!hs.hasActivated[i]) continue;
                        // M6s-B7: the logger line is the causal source; the
                        // HUD flag only fills in sessions whose log predates
                        // the activation-line change (old saves).
                        if (_heroLoggedActivated.Contains(name + "#" + round)) continue;
                        // The tag carries the ROUND: the checkbox resets each
                        // round, so a flip is a per-round fact - the old
                        // round-free tag emitted each hero exactly ONCE per
                        // game (Player-19 saw MHD-19 take the door credit
                        // simply because its first-ever flip followed it).
                        var tag = name + "#" + i + "#" + round;
                        if (_heroActivated.Add(tag))
                            Append("HeroActivated", round, P("hero", name));
                    }
            }
        }

        // ------------------------------------------------------------- resync
        /// <summary>Re-log the COMPLETE current history (seq renumbered from
        /// 1) so consumers recover a contiguous stream. Built purely from
        /// current public state - nothing estimated.</summary>
        public void Resync()
        {
            var log = new List<Tac.WorldEvent>();
            int round = Math.Max(1, _round);
            var mp = TryMissionProperties();
            var sd = DataStore.sagaSessionData;
            string mission = mp != null ? (mp.missionName ?? mp.missionID ?? "mission") : "mission";
            string objective = _objective
                               ?? (mp != null ? mp.startingObjective : null) ?? "";
            log.Add(Mk(1, "MissionStart", round, new Dictionary<string, object>
            {
                { "objective", objective },
                { "priorityTarget", mp != null ? mp.priorityTargetType.ToString() : "Rebel" },
                // M8/F5: re-log must state the clock the GAME will honour.
                { "roundLimit", EffectiveRoundLimit(sd != null ? sd.gameVars : null, mp) },
                { "mission", mission },
                { "missionType", mp != null ? mp.missionType.ToString() : "Story" },
                { "resync", true },
            }));
            int seq = 2;
            foreach (var cd in DataStore.deployedEnemies)
            {
                if (cd == null || cd.isDummy) continue;
                log.Add(Mk(seq++, "Spawn", round, new Dictionary<string, object>
                {
                    { "group", cd.id },
                    { "size", (long)Math.Max(1, cd.size) },
                    { "name", cd.name ?? "" },   // Player-19 display name
                    { "resync", true },
                }));
                _wheels[cd.id] = (int[])cd.woundTrackerValue.Clone();
                _seenGroups.Add(cd.id);
                int n = Math.Min(Math.Max(cd.size, 1), Math.Min(3, cd.woundTrackerValue.Length));
                for (int i = 0; i < n; i++)
                    log.Add(Mk(seq++, "GroupDamage", round, new Dictionary<string, object>
                    {
                        { "group", cd.id },
                        { "figure", (long)i },
                        { "wheel", (long)cd.woundTrackerValue[i] },
                        { "health", (long)cd.health },
                        { "resync", true },
                    }));
            }
            // R-A: the re-log states every digital door as it IS (full
            // current-state re-log, same law as the dials above) and
            // re-bases the snapshot diff on it.
            foreach (var d in MissionDoors())
            {
                log.Add(Mk(seq++, d.doorOpen ? "DoorOpen" : "DoorClosed", round,
                           new Dictionary<string, object>
                           {
                               { "door", d.GUID.ToString() },
                               { "name", d.name ?? "" },
                               { "resync", true },
                           }));
                _doors[d.GUID.ToString()] = d.doorOpen;
            }
            // S-A: sections and interactable flags re-log from the SAME live
            // accessors the polls use (full-current-state law, no guessing):
            // every visible section as SectionActivated, every entity that
            // deviates from the default-active state as EntityInteract
            // {active:false}. Snapshots re-base on the live values.
            var tmS = TileManagerOrNull();
            var secsS = DataStore.mission != null ? DataStore.mission.mapSections : null;
            if (tmS != null && tmS.tilesLoaded && secsS != null)
                foreach (var s in secsS)
                {
                    if (s == null) continue;
                    bool sLive = tmS.IsMapSectionActive(s.GUID);
                    if (sLive)
                        log.Add(Mk(seq++, "SectionActivated", round, new Dictionary<string, object>
                        {
                            { "section", s.GUID.ToString() },
                            { "name", s.name ?? "" },
                            { "resync", true },
                        }));
                    _sections[s.GUID.ToString()] = sLive;
                }
            var memS = EntityManagerOrNull();
            var entsS = DataStore.mission != null ? DataStore.mission.mapEntities : null;
            if (memS != null && entsS != null)
                foreach (var me in entsS)
                {
                    if (me == null) continue;
                    if (me.entityType != Saga.EntityType.Token
                        && me.entityType != Saga.EntityType.Terminal
                        && me.entityType != Saga.EntityType.Crate) continue;
                    var live = memS.GetEntity(me.GUID) ?? me;
                    bool cur = live.entityProperties != null && live.entityProperties.isActive;
                    if (!cur)
                        log.Add(Mk(seq++, "EntityInteract", round,
                                   EntityPayload(me.GUID.ToString(), live, false, true)));
                    _entities[me.GUID.ToString()] = cur;
                }
            foreach (var h in _heroWounded) log.Add(Mk(seq++, "HeroWounded", round, P("hero", h)));
            foreach (var h in _heroDefeated) log.Add(Mk(seq++, "HeroDefeated", round, P("hero", h)));
            log.Add(Mk(seq++, "ThreatSpend", round, P("balance", (long)(sd?.gameVars?.currentThreat ?? 0))));
            Feed.Resync(log);
            // M8/F5: clock watch re-based on the re-log's own snapshot
            _lastRoundLimit = EffectiveRoundLimit(sd != null ? sd.gameVars : null, mp);
            _runtimeDirty = false;
            _drainedLog = 0;
            var logger = sd == null ? null : sd.missionLogger;
            if (logger != null && logger.logItems != null) _drainedLog = logger.logItems.Count;
            Debug.Log("WorldEvents::resync " + Feed.MissionKey + " seq=" + Feed.LastSeq
                      + " groups=" + _seenGroups.Count + " heroes=" + _heroWounded.Count + "/"
                      + _heroDefeated.Count + " (re-log from current state, no estimation)");
        }

        // ------------------------------------------------------------ plumbing
        Tac.WorldEvent Append(string kind, int round, Dictionary<string, object> payload)
        {
            var e = Feed.Append(kind, round, payload);
            if (!_firstLogged)
            {
                _firstLogged = true;
                Debug.Log("WorldEvents::first seq=" + e.Seq + " kind=" + kind
                          + " round=" + round + " mission=" + Feed.MissionKey);
            }
            return e;
        }

        static Tac.WorldEvent Mk(int seq, string kind, int round, Dictionary<string, object> p)
            => new Tac.WorldEvent(kind, round, p) { Seq = seq };

        static Dictionary<string, object> P(string k, object v)
            => new Dictionary<string, object> { { k, v } };

        static string SessionKey()
        {
            try
            {
                return Saga.RunningCampaign.sagaCampaignGUID + "|" +
                       (DataStore.sagaSessionData?.setupOptions?.projectItem?.missionID ?? "solo");
            }
            catch { return null; }
        }

        static Saga.MissionProperties TryMissionProperties()
        {
            try { return DataStore.mission?.missionProperties; }
            catch { return null; }
        }

        // name -> card id resolution over deployed public state (never hidden).
        // Unknown names pass through unchanged: they are still public facts.
        static string IdForName(string name)
        {
            try
            {
                foreach (var cd in DataStore.deployedEnemies)
                    if (cd != null && cd.name == name) return cd.id;
            }
            catch { }
            return name;
        }

        bool IsHeroName(string name)
        {
            try
            {
                foreach (var cd in DataStore.deployedHeroes)
                    if (cd != null && cd.name == name) return true;
            }
            catch { }
            return false;
        }
    }
}
