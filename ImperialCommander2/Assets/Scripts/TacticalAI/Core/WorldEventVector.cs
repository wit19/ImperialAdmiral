// M5 Scope B (concept 3.3.1b): deterministic projection of the passive
// world-feed history into a planner-facing view of the world ("Tac.World
// Sicht"). PURE mapping - replay the append-only public events in seq order;
// no RNG, no clock, no hidden state, no estimation: a group nobody wounded
// keeps full vitality (vitalityFrac = 1), a figure never observed keeps its
// printed assumption, and nothing in here ever claims a figure position.
//
// The projection is a pure function of the event list: replaying the same
// history always yields a bit-identical view (NUnit pin: 2x replay), and the
// history hash travels with the view so consumers can pin what they decided
// on.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TacticalAI.Core
{
    /// <summary>Planner-facing snapshot projected from the public feed.</summary>
    public sealed class FeedWorldView
    {
        public string MissionKey = "solo";
        public int Round;
        public string Posture = "eliminate";
        // M23 (Thomas 06.11): feed-stated perspective of the objective text
        // (Empire campaign: the authored task line belongs to the IMPERIAL
        // side). Derived-only, never planner-direct; NOT in Canon() (the
        // derivation is a pure function of the events, existing vectors
        // must stay hash-stable).
        public bool ObjectiveForEmpire = false;
        public string Objective = "";
        // M10b: hero credited with the LAST objective progress (public
        // activation-order attribution, "" = unknown/none). Event-derived,
        // never a position read - the app cannot see hero cells.
        public string ObjectiveActor = "";
        // Player-19 (Thomas 03.11): round of that credit (0 = none). The
        // engaged tier (focus key 2) can hold the credited mover AND a hero
        // who struck the mission-named figure; the credit ROUND breaks the
        // tie - the most recent engagement leads.
        public int ObjectiveActorRound;
        // Player-21-b (Thomas 04.11, "Da stimmt dann etwas nicht"): when a
        // mission's CURRENT phase is fought through freshly spawned troops
        // (Garrison: officer (Elite) + Ordnungssoldaten), engaging THEM is
        // objective work - even though the objective text ("Sucht im Diner
        // nach dem Duros.") never names them. Facts only: the Spawn ROUND of
        // the group, the round of the last ObjectiveChange, and the damage
        // event's activation window - no text parsing, no positions. Before
        // the first ObjectiveChange this channel is CLOSED (initial-phase
        // vectors stay byte-identical; only the named-figure law applies).
        public bool ObjectiveChanged;
        public int ObjectiveSinceRound = 1;
        // M8/F5: MissionStart clock inputs, kept so a runtime RoundLimitChanged
        // can re-derive the posture exactly like a fresh MissionStart would.
        // (Canon text unchanged - posture, not its inputs, is the decision.)
        public string PriorityTarget = "";
        public long RoundLimit = -1;
        // P33-H4b (CONCEPT_missionsachse): the mission's PUBLIC win wiring as
        // projected from VictoryConditions events (producer: mission-file
        // fields only - fixedAlly/specificAlly identity, defeat->end wiring,
        // escape-vocabulary entities whose button ends the mission). "" /
        // empty list = no fact = every consumer stays exactly today's law
        // (Null-Law). Scalars are last-event-wins (a later event refining the
        // wiring legitimately rewrites the claim); EscapeSections is an
        // additive sorted set (every parsed evac zone, ordinal).
        public string ImperialWinBy = "";     // "" | "allyDefeated"
        public string RebelWinBy = "";        // "" | "allyReaches"
        public string AxisActor = "";         // ally id (A001-style card id)
        public string AxisActorName = "";     // its public display name
        public string EscortHero = "";        // specificHero display name, "" = unknown
        public readonly List<string> EscapeSections = new List<string>(); // sorted GUIDs
        // P33-H4c (CONCEPT_missionsachse §2.4): the mission's attackable
        // objects ("can attack X (Health: N...)" in the object's OWN EN
        // mission text) keyed by entity GUID (last-event-wins per GUID,
        // additive across GUIDs — same data-existence law as the victory
        // row: no MissionObject events -> no rows -> Canon and every
        // consumer byte-identical, Null-Law). Polaritaet/Basis are derived
        // by the CORE ladder (CONCEPT 2.4 P1-P4), never by the producer.
        public readonly Dictionary<string, MissionObjectFact> Objects =
            new Dictionary<string, MissionObjectFact>(StringComparer.Ordinal);

        /// P33-H4c: objects the Empire attacks (polaritaet "jagt"), name
        /// ordinal then GUID ordinal — deterministic target ordering.
        public List<MissionObjectFact> HuntObjects()
        {
            var list = new List<MissionObjectFact>();
            foreach (var kv in Objects)
                if (kv.Value.Polaritaet == "jagt") list.Add(kv.Value);
            list.Sort((a, b) =>
            {
                int c = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.Guid, b.Guid, StringComparison.Ordinal);
            });
            return list;
        }

        /// P33-H4c: objects the Empire must PROTECT (polaritaet "schuetzt":
        /// the opponent must destroy/claim them - CONCEPT 2.4 SCHUTZGESETZ),
        /// name ordinal then GUID ordinal.
        public List<MissionObjectFact> ProtectObjects()
        {
            var list = new List<MissionObjectFact>();
            foreach (var kv in Objects)
                if (kv.Value.Polaritaet == "schuetzt") list.Add(kv.Value);
            list.Sort((a, b) =>
            {
                int c = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                return c != 0 ? c : string.Compare(a.Guid, b.Guid, StringComparison.Ordinal);
            });
            return list;
        }

        /// P33-H4c ZUSÄTZLICH clause: sections of POLAR objects (jagt or
        /// schuetzt - contested by mission wiring) as contested-zone
        /// candidates for the ZoneBelief/critical-section layer; unique,
        /// ordinal. Neutral objects contribute nothing.
        public List<string> ContestedObjectSections()
        {
            var list = new List<string>();
            foreach (var kv in Objects)
                if (kv.Value.Polaritaet != "neutral" && !string.IsNullOrEmpty(kv.Value.Section)
                    && !list.Contains(kv.Value.Section))
                    list.Add(kv.Value.Section);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>P33-H4c (CONCEPT_missionsachse §2.4): one attackable
        /// mission object. Facts are the object's OWN canonical EN mission
        /// text/data (Health/Defense printed in the text, section, printed
        /// position) plus the raw public wiring evidence (button texts,
        /// wired event names, outcome side). Polaritaet/Basis are the CORE
        /// ladder's verdict (P1-P4), recomputed over the completed stream -
        /// never a producer claim, never hidden state.</summary>
        public sealed class MissionObjectFact
        {
            public string Guid = "";        // entity GUID (Objects key, identity)
            public string Name = "";        // public app name (ziel=OBJEKT:<name>)
            public string Section = "";     // own mapSectionOwner GUID, "" = unknown
            public string Position = "";    // printed setup "x;y" (SICHTBARKEITSGRENZE)
            public long Health = -1;        // printed "Health: N" from the text
            public long Defense = -1;       // printed "Defense: N" (None => 0); -1 = no fact
            public string Buttons = "";     // raw button texts, ";" joined, ordinal
            public string EventNames = "";  // wired event names, ";" joined, ordinal
            public string OutcomeSide = ""; // losing side of the wired end event
            public string Polaritaet = "neutral";  // "jagt" | "schuetzt" | "neutral"
            public string Basis = "P4";            // "P1" | "P2" | "P3" | "P4"
            // P38 (card t_11ebb843): the app's authored entity-type field,
            // lower-cased enum name ("token" | "door" | "terminal" | ...),
            // data-existence optional — a pre-P38 row carries NO key and
            // stays "" (Null-Law: the bystander class never fires on it).
            public string EntityType = "";
            // P38: the CORE bystander verdict (NeutralPrey.IsBystander),
            // derived in the SAME pass as the polaritaet — never a producer
            // claim. Default false: every pre-P38 row/vector byte-identical.
            public bool Bystander = false;

            /// The P1 evidence pool: button texts and wired event names.
            public List<string> EvidenceStrings()
            {
                var list = new List<string>();
                foreach (var src in new[] { Buttons, EventNames })
                    foreach (var s in (src ?? "").Split(';'))
                    {
                        var t = s.Trim();
                        if (t.Length > 0) list.Add(t);
                    }
                return list;
            }
        }

        public long ThreatBalance;
        public bool ThreatSeen;            // at least one public ThreatSpend event
        public int Seq;
        public string HistoryHash = "";

        public readonly List<GroupView> Groups = new List<GroupView>();      // sorted by GroupId
        public readonly List<HeroView> Heroes = new List<HeroView>();        // sorted by HeroId

        public sealed class GroupView
        {
            public string GroupId;
            public string Name;
            // Player-19: the group's DISPLAY name from the Spawn payload
            // (the deployment-card name every player reads). Null when the
            // spawn event carried no name (old vectors keep their exact
            // canon). The designation law uses it to catch surname
            // collisions; the strike law uses it to find the figure the
            // objective text names.
            public string DisplayName;
            public bool Deployed;
            public bool Removed;
            public int Size = 1;                 // printed group size (Spawn event)
            public int Alive = 1;                // Spawn(size) seeds, Death decrements, floor 0
            public int DeployedRound;
            public int Health;                   // printed, from GroupDamage events (0 = never observed)
            public int FiguresSeen;              // distinct figure indices in wheel events
            public readonly Dictionary<int, long> Wheels = new Dictionary<int, long>();

            // R-A (concept v1.2 §5): WHO shot this group - pure projection of
            // the public activation window over the damage facts. The damage
            // dials are MANUAL (HPTracker.cs:37 - a human turns the wheel;
            // the GroupDamage payload never carries a shooter), so the hero
            // whose activation window ENCLOSES the hit is the only publicly
            // causal attribution available (M10b attribution law, mirror
            // image of the objective credit). Last hit wins: a later hit in
            // a (different) window overwrites. Fields are NEVER guessed from
            // absence: a hit between windows leaves the previous attribution
            // untouched, and a group never shot inside an open window stays
            // unset (no "it was the last hero anyway").
            public string LastShotHero;          // null = never shot within an open window
            public long LastShotSeq;             // seq of the crediting GroupDamage event
            public int LastShotRound;            // round of that hit (0 = none)

            // R-B (concept v1.2 §5 B): the Spawn event fell inside an OPEN
            // hero window - the "this intruder was pulled in by hero
            // activity" insight (R-C R3 input). Pure projection: the window
            // state is part of the event stream itself
            // (HeroActivationStart/HeroActivated), so this reads NO new feed
            // input; the spawn ROUND already rides DeployedRound.
            public bool SpawnedInHeroWindow;

            // Player-22 (Thomas 04.11, objective-panel screenshot): the
            // Spawn payload's "critical" flag - a FACT read at emit time
            // from the group's deployment override: setTrigger/setEvent
            // non-empty means THIS group's defeat moves the scenario
            // machinery (the mission file wired a defeated-trigger/event
            // for it - EMPIRE2: the Elite officer DG006 and the Riot
            // Troopers DG047 carry one, the mid-war Stormtrooper wave DG002
            // and the probe droids DG012 carry NONE). CriticalKnown keeps a
            // missing key (pre-Player-22 vectors) apart from a real false:
            // absence never reads as fact, and old vectors keep byte-
            // identical canon and hash.
            public bool Critical;
            public bool CriticalKnown;

            /// <summary>Remaining group vitality as exact Rational (M9-F6/A0,
            /// closes M7-F4): the wound dials show ERLITTENER Schaden per
            /// figure — a fresh group reads 0 (Thomas board fact 2026-10-27;
            /// DeploymentCard.cs:55 fresh {0,0,0}, HPTracker.cs:43-48
            /// double-click reset to 0, label groupWoundNumber). Remaining
            /// HP per figure is therefore health - min(health, dial); the
            /// old reading summed the dials AS life and inverted every
            /// partially damaged group. A figure with NO wheel event never
            /// moved its dial -> damage 0 -> full HP (same "never guessed
            /// from absence" invariant that keeps an untouched group at 1).
            /// Denominator: printed card health of EVERY figure the group
            /// deployed with (FINAL-QA audit #10 group granularity); the
            /// result is capped by the alive figures' HP so a Death event
            /// with dials not yet clicked down cannot inflate vitality.
            /// Dials are absolute public facts; health rides the event.
            /// Known public blind spot: a group deployed PRE-DAMAGED at the
            /// baseline sees its starting dials invisible (no event) - same
            /// class as the M5 baseline rule, documented, not guessed.</summary>
            public Rational VitalityFrac
            {
                get
                {
                    if (Wheels.Count == 0 || Health <= 0) return Rational.One;
                    int n = Math.Max(1, Math.Max(Size, FiguresSeen));
                    long hp = (long)Health * n;
                    long dmg = 0;
                    foreach (var kv in Wheels) dmg += Math.Max(0, Math.Min(Health, kv.Value));
                    long alive = Math.Max(0, Math.Min(n, Alive));
                    long rem = Math.Min(Math.Max(0, hp - dmg), (long)Health * alive);
                    return new Rational(rem, hp <= 0 ? 1 : hp);
                }
            }
        }

        public sealed class HeroView
        {
            public string HeroId;
            public string Name;
            public bool Wounded;
            public bool Defeated;
            public int Activations;
            public int LastActivatedRound;
            // M6 aim tokens (concept 3.3.1b FocusChange vocabulary + public
            // designation): a PUBLIC mission designation - the current
            // objective text naming a hero ("kill Jabba", "escort Han") is
            // table text every player reads; the FocusChange vocabulary slot
            // carries an explicit designation the moment the app grows a
            // focus UI. Marks NAME heroes, never positions, and every player
            // sees the same designation - not extra sight.
            public int Marks;                  // how often the mark hit (cumulative)
            public int MarkedRound;            // round of the LAST mark, 0 = never
            // Player-19 strike credit: GroupDamage events that landed on the
            // figure the CURRENT mission objective names ("Besiegt Ivan
            // Talos.") while THIS hero's activation window was open. Public
            // activation order is the attribution channel (M10b law, mirror
            // image): the hero whose turn encloses the damage did it. Never
            // a position read, never a guess from absence.
            public int Strikes;                // cumulative strikes on the mission-named figure
            public int StrikeRound;            // round of the LAST strike, 0 = never

            // ---- R-B Last-Seen-Ledger (concept v1.2 §5 B) ----
            // SYMBOLIC presence anchors. A zone is an EVENT-ANCHOR identity,
            // never a position: "obj:<objective text>" (the hero whose open
            // window encloses an ObjectiveChange is the actor per the public
            // M10b/M6s-B6 attribution law), "door:<door GUID>" (a digital
            // door flip inside the open window), "at:<group id>" (a hit on
            // that group inside the open window), and since S-B "sec:<section
            // GUID>" (an EntityInteract WITH a section payload inside the
            // open window places the hero in the entity's own section - the
            // section-granular anchor of concept 8.1 L1', kind FACT: the
            // interaction is public proof the hero was there. An interact
            // WITHOUT a section payload anchors nobody - data-exists).
            // NO coordinates, NO
            // distances, NO movement commitment - and the ledger NEVER
            // writes from absence: a hero with no anchoring event keeps
            // LastSeenZone null, and an anchor is only ever OVERWRITTEN by
            // the next anchoring event (latest seq wins).
            // KIND is a first-class part of the fact: "fact" = the public
            // attribution law PLACES the actor there (objective, door);
            // "hypothesis" = a public proximity anchor (a hit means the
            // shooter was within weapon range of the group - NOT a placed
            // position). Consumers MUST show/store the kind and MUST NOT
            // treat a hypothesis like a fact.
            public const string LedgerKindFact = "fact";
            public const string LedgerKindHypothesis = "hypothesis";
            public string LastSeenZone;        // null = no anchor yet (absence writes nothing)
            public string LastSeenKind;        // LedgerKindFact | LedgerKindHypothesis
            public long LastSeenSeq;           // seq of the anchoring event (0 = none)
            public int LastSeenRound;          // round of the anchoring event (0 = none)
        }

        public GroupView FindGroup(string id)
        {
            foreach (var g in Groups) if (g.GroupId == id) return g;
            return null;
        }

        /// <summary>Stable canonical text of the whole view - the equality
        /// unit for replay tests and fixture vectors.</summary>
        public string Canon()
        {
            var sb = new StringBuilder();
            sb.Append("mission=").Append(MissionKey).Append('\n');
            sb.Append("round=").Append(Round.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("posture=").Append(Posture).Append('\n');
            sb.Append("objective=").Append(Objective).Append('\n');
            sb.Append("objective_actor=").Append(ObjectiveActor).Append('\n');
            sb.Append("threat=").Append(ThreatBalance.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("threatseen=").Append(ThreatSeen ? 1 : 0).Append('\n');
            sb.Append("seq=").Append(Seq.ToString(CultureInfo.InvariantCulture)).Append('\n');
            // P33-H4b data-existence law (Player-19/22 pattern): the victory row
            // and the escape rows appear ONLY where the mission really parsed
            // win wiring - streams without VictoryConditions facts keep their
            // exact pre-H4b Canon (Null-Law, pinned in axis.json).
            if (!string.IsNullOrEmpty(ImperialWinBy) || !string.IsNullOrEmpty(RebelWinBy)
                || !string.IsNullOrEmpty(AxisActor) || !string.IsNullOrEmpty(AxisActorName)
                || !string.IsNullOrEmpty(EscortHero) || EscapeSections.Count > 0)
            {
                sb.Append("victory=").Append(ImperialWinBy).Append('|').Append(RebelWinBy)
                  .Append('|').Append(AxisActor).Append('|').Append(AxisActorName)
                  .Append('|').Append(EscortHero).Append('\n');
                foreach (var z in EscapeSections)
                    sb.Append("escape=").Append(z).Append('\n');
            }
            // P33-H4c data-existence law (same as the victory row): object
            // rows appear ONLY where the mission really parsed attackable
            // objects - streams without MissionObject events keep their
            // exact pre-H4c Canon byte-identical (Null-Law, marker_object.json).
            if (Objects.Count > 0)
            {
                var okeys = new List<string>(Objects.Keys);
                okeys.Sort(StringComparer.Ordinal);
                foreach (var k in okeys)
                {
                    var o = Objects[k];
                    sb.Append("object=").Append(o.Guid).Append('|').Append(o.Name).Append('|')
                      .Append(string.IsNullOrEmpty(o.Section) ? "-" : o.Section).Append('|')
                      .Append(string.IsNullOrEmpty(o.Position) ? "-" : o.Position).Append('|')
                      .Append(o.Health.ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(o.Defense.ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(o.Polaritaet).Append('|').Append(o.Basis);
                    // P38 data-existence law: the entityType segment appears
                    // ONLY on rows that carry the key, the bystander marker
                    // ONLY where the CORE law said yes — every pre-P38 row
                    // keeps its exact Canon byte-identity (Null-Law).
                    if (!string.IsNullOrEmpty(o.EntityType))
                        sb.Append('|').Append(o.EntityType);
                    if (o.Bystander) sb.Append("|bystander");
                    sb.Append('\n');
                }
            }
            foreach (var g in Groups)
            {
                sb.Append("G|").Append(g.GroupId).Append('|').Append(g.Name).Append('|')
                  .Append(g.Deployed ? 1 : 0).Append('|').Append(g.Removed ? 1 : 0).Append('|')
                  .Append(g.Alive).Append('|').Append(g.Size).Append('|').Append(g.DeployedRound).Append('|')
                  .Append(g.Health).Append('|').Append(g.VitalityFrac.ToString());
                // Player-19: display name appears ONLY where the Spawn
                // carried one (record discipline: pre-Player-19 canon stays
                // byte-identical, every old vector keeps its hash).
                if (!string.IsNullOrEmpty(g.DisplayName))
                    sb.Append("|name=").Append(g.DisplayName);
                // Player-22: the crit column appears ONLY where the Spawn
                // carried the flag (record discipline: pre-Player-22 canon
                // byte-identical; where the fact exists the column MUST show
                // - true AND false print, data-existence law).
                if (g.CriticalKnown)
                    sb.Append("|crit=").Append(g.Critical ? 1 : 0);
                // R-A: the shooter column appears ONLY where a hit landed
                // inside an open hero window (record discipline: vectors
                // without that fact keep their exact canon and hash; where
                // the fact exists the column MUST show - data-existence law,
                // M6s-B6 pattern).
                if (!string.IsNullOrEmpty(g.LastShotHero))
                    sb.Append("|shot=").Append(g.LastShotHero).Append('|')
                      .Append(g.LastShotSeq.ToString(CultureInfo.InvariantCulture)).Append('|')
                      .Append(g.LastShotRound.ToString(CultureInfo.InvariantCulture));
                // R-B: the in-window spawn marker appears ONLY where the
                // Spawn really fell into an open hero window (same
                // data-existence law: spawns at mission baseline or between
                // windows keep the exact pre-R-B row).
                if (g.SpawnedInHeroWindow)
                    sb.Append("|in_window");
                sb.Append('\n');
            }
            foreach (var h in Heroes)
            {
                sb.Append("H|").Append(h.HeroId).Append('|').Append(h.Name).Append('|')
                  .Append(h.Wounded ? 1 : 0).Append('|').Append(h.Defeated ? 1 : 0).Append('|')
                  .Append(h.Activations).Append('|').Append(h.LastActivatedRound).Append('|')
                  .Append(h.Marks).Append('|').Append(h.MarkedRound);
                // Player-19: strike row appears ONLY where strikes landed.
                if (h.Strikes > 0)
                    sb.Append('|').Append(h.Strikes).Append('|').Append(h.StrikeRound);
                sb.Append('\n');
                // R-B Last-Seen-Ledger: one ledger row per hero that HAS an
                // anchor, printed as its own line right below the hero row -
                // the H row itself stays byte-identical to pre-R-B canon
                // (data-existence law: heroes without an anchor emit no
                // line, so every vector without ledger facts keeps its
                // exact pre-R-B canon; hashes ride the EVENTS only and are
                // untouched by any of this). The KIND is printed always -
                // a hypothesis must never read like a fact (Thomas:
                // Konsumenten duerfen hypothesis nie wie fact behandeln).
                if (!string.IsNullOrEmpty(h.LastSeenZone))
                    sb.Append("seen=").Append(h.HeroId)
                      .Append(" zone=").Append(h.LastSeenZone)
                      .Append(" kind=").Append(h.LastSeenKind)
                      .Append(" seq=").Append(h.LastSeenSeq.ToString(CultureInfo.InvariantCulture))
                      .Append(" round=").Append(h.LastSeenRound.ToString(CultureInfo.InvariantCulture))
                      .Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>R-B: the Last-Seen-Ledger as HUMAN-READABLE debug text -
        /// Thomas reads the Player.log as explanation UI, so the ledger's
        /// kind distinction must survive into the log, not only into the
        /// machine Canon. One line per hero WITH an anchor (absence prints
        /// nothing); zones are read out by their anchor kind, hypotheses are
        /// worded as hypotheses. Deterministic (sorted Heroes), pure.</summary>
        public string LedgerText()
        {
            var sb = new StringBuilder();
            foreach (var h in Heroes)
            {
                if (string.IsNullOrEmpty(h.LastSeenZone)) continue;
                sb.Append("ledger ").Append(h.Name);
                if (h.LastSeenKind == HeroView.LedgerKindHypothesis)
                    sb.Append(" war vermutlich in der Nahe von ").Append(ZoneReadout(h.LastSeenZone));
                else
                    sb.Append(" war bei ").Append(ZoneReadout(h.LastSeenZone));
                sb.Append(" (Runde ").Append(h.LastSeenRound.ToString(CultureInfo.InvariantCulture))
                  .Append(", seq ").Append(h.LastSeenSeq.ToString(CultureInfo.InvariantCulture))
                  .Append(", ").Append(h.LastSeenKind).Append(")\n");
            }
            foreach (var g in Groups)
                if (g.SpawnedInHeroWindow)
                    sb.Append("spawn ").Append(g.Name)
                      .Append(" fiel in ein offenes Heldenfenster (Runde ")
                      .Append(g.DeployedRound.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            return sb.ToString();
        }

        static string ZoneReadout(string zone)
        {
            // Symbolic anchors only - the readout keeps the prefix meaning
            // visible (objective text / door identity / group identity).
            if (zone.StartsWith("obj:", StringComparison.Ordinal)) return "Ziel \"" + zone.Substring(4) + "\"";
            if (zone.StartsWith("door:", StringComparison.Ordinal)) return "Tuer " + zone.Substring(5);
            if (zone.StartsWith("at:", StringComparison.Ordinal)) return "Gruppe " + zone.Substring(3);
            if (zone.StartsWith("sec:", StringComparison.Ordinal)) return "Sektion " + zone.Substring(4);
            return zone;
        }
    }

    public static class WorldEventVector
    {
        /// <summary>Replay the public history into a FeedWorldView. Pure:
        /// same events -> bit-identical view, no side effects.</summary>
        public static FeedWorldView Project(IReadOnlyList<WorldEvent> events,
                                            string missionKey = null,
                                            string historyHash = null)
        {
            var gap = WorldEventFeed.VerifySeq(events);
            if (gap != null)
                throw new InvalidOperationException(
                    "WorldEventVector.Project: seq gap at " + gap
                    + " - demand a Resync (re-log of the complete history), never project a partial window");
            var v = new FeedWorldView
            {
                MissionKey = missionKey ?? "solo",
                Seq = events.Count == 0 ? 0 : events[events.Count - 1].Seq,
                HistoryHash = historyHash ?? WorldEventFeed.HashOf(events),
            };

            // M10b objective attribution (Thomas revision 27.10): the feed
            // knows objective PROGRESS (ObjectiveChange) and WHO ACTIVATED
            // (HeroActivated) - never cell positions. Pure replay state -
            // same events, same attribution, resync-safe.
            // Player-19 revision (Thomas 03.11): the credit is TERMINAL at
            // the moment of the change. The captor is the hero whose
            // activation WINDOW is open (HeroActivationStart ... 
            // HeroActivated); when the change arrives between turns (mission
            // events fired during the imperial phase - HOTH11's Face Off),
            // the hero who acted LAST before it keeps the credit. The old
            // law let the NEXT activator overwrite the provisional one -
            // that stole Lokaku's door credit onto MHD-19 in Player-19.
            string lastActivator = null;
            string heroWindow = null;
            bool attributionPending = false;
            // Player-19 M6s-B7 strike channel: damage on the objective-named
            // figure that could not credit an open window arms a pending
            // strike; the hero whose turn CLOSES next (causal - the checkbox
            // flip is now a mission-log line, M6s-B7 adapter) is the shooter
            // or the blocker that made it happen. Expires at RoundEnd: a
            // damage older than the round is history, not causation.
            bool strikePending = false;
            // Player-25-B (card t_4fd126ad, fixplan_Player-25_v2 B): the
            // CLOSER channel for shot attribution - ALL groups, no window
            // event required. group -> {hit seq, hit round}. The runtime
            // NEVER emits HeroActivationStart (Thomas 04.11, explicit), so
            // the window path stays dead in play and LastShotHero never
            // sets: the whole Vergeltung rail (R1 push, S2 rule, officer
            // E1) sleeps (Player-25 log, Dreistrom). This is the strike
            // channel's already-live law (M6s-B7, r5 objActor=Jyn Odan)
            // lifted from the objective-named group to every group: "the
            // hero whose turn CLOSES NEXT was the shooter". Hitgewinn per
            // group: the FIRST windowless hit of the round wins; groups are
            // independent. The window path keeps VORRANG: when a Start did
            // count (a later in-window hit set LastShotHero with a higher
            // seq), the pending closer never overwrites it. TISCH-DISZIPLIN
            // (table discipline, same law as the strike channel): damage
            // must be marked BEFORE the end of the turn - a hit that lands
            // after the LAST HeroActivated of the round finds no closer and
            // credits NOBODY (RoundEnd deletes the pending: history !=
            // causation, exactly like strikePending).
            var shotPending = new Dictionary<string, long[]>();
            // Player-21-b/-22 single arming question for the strike channel:
            // does damage on THIS group count as working the CURRENT
            // objective? Canon branch: the objective text NAMES the figure
            // (Player-19 law, e.g. "Besiegt Ivan Talos."). Garrison branch
            // TWO-factor (Thomas 04.11 screenshot law): the group must be
            // SCENARIO-CRITICAL (Spawn "critical" fact: its defeat drives a
            // wired trigger/event) AND it must be the CURRENT phase's own
            // spawn (deployed during/after the last change). One without the
            // other is exactly the two bugs: rounds alone dragged the marker
            // onto ambient mid-phase reinforcements; the flag alone would
            // credit a LAST phase's kill during this one.
            Func<FeedWorldView.GroupView, bool> engages = g =>
                (!string.IsNullOrEmpty(v.Objective) && !string.IsNullOrEmpty(g.DisplayName)
                    && NamesHero(v.Objective, g.DisplayName))
                || (v.ObjectiveChanged && g.Critical && g.CriticalKnown
                    && g.DeployedRound > 0 && g.DeployedRound >= v.ObjectiveSinceRound);
            foreach (var e in events)
            {
                if (e.Round > v.Round) v.Round = e.Round;
                switch (e.Kind)
                {
                    case "MissionStart":
                        v.Objective = e.Str("objective") ?? "";
                        v.PriorityTarget = e.Str("priorityTarget") ?? "";
                        v.RoundLimit = e.Long("roundLimit", -1);
                        // M23: perspective fact (absent = legacy rebel-side
                        // reading); set BEFORE the derivation reads it.
                        v.ObjectiveForEmpire = e.Long("objectiveForEmpire", 0) != 0;
                        v.Posture = DerivePosture(e.Str("objective"),
                                                  e.Str("priorityTarget"),
                                                  e.Long("roundLimit", -1),
                                                  v.ObjectiveForEmpire);
                        ApplyDesignation(v, v.Objective, e.Round);   // initial public designation
                        v.ObjectiveChanged = false;
                        v.ObjectiveSinceRound = e.Round;   // Player-21-b: phase clock
                        break;
                    case "ObjectiveChange":
                        v.Objective = e.Str("objective") ?? v.Objective;
                        ApplyDesignation(v, v.Objective, e.Round);
                        v.ObjectiveChanged = true;             // Player-21-b: from here on
                        v.ObjectiveSinceRound = e.Round;       // fresh spawns = the phase's garrison
                        // Player-19: terminal credit - open window first,
                        // else the hero who acted just before the change.
                        v.ObjectiveActor = heroWindow ?? lastActivator ?? "";
                        v.ObjectiveActorRound = string.IsNullOrEmpty(v.ObjectiveActor)
                                                ? 0 : e.Round;
                        // M10b retro channel (kept, see HeroActivated): logs
                        // WITHOUT activation-start events still let the hero
                        // finishing his turn after the change overwrite the
                        // provisional credit; logs WITH a window trust the
                        // window (Player-19: open window first).
                        attributionPending = true;
                        // R-B Last-Seen-Ledger: the OPEN-WINDOW branch of the
                        // public attribution law anchors the actor at the
                        // objective itself (zone "obj:<text>", kind FACT -
                        // the M10b/M6s-B6 law places him there). The legacy
                        // provisional/retro channel is deliberately NOT
                        // mirrored into the ledger: its provisional credit
                        // can still be overwritten, and an anchor written
                        // from a guess-then-overwrite channel would violate
                        // the never-from-absence law.
                        if (heroWindow != null)
                        {
                            var actor = Hero(v, heroWindow);
                            actor.LastSeenZone = "obj:" + v.Objective;
                            actor.LastSeenKind = FeedWorldView.HeroView.LedgerKindFact;
                            actor.LastSeenSeq = e.Seq;
                            actor.LastSeenRound = e.Round;
                        }
                        break;
                    case "DoorOpen":
                    case "DoorClosed":
                        // R-A: for the view these flips are audit-trail
                        // history (they move the hash, nothing else). R-B
                        // adds ONE lawful projection: a flip INSIDE an open
                        // hero window is a public presence anchor per the
                        // same M10b law (whose else could operate the digital
                        // door while this hero is in turn). Zone
                        // "door:<GUID>" = the door's identity, kind FACT; a
                        // flip outside any window anchors nobody - the
                        // door_feed vectors WITHOUT a hero window keep their
                        // exact canon.
                        if (heroWindow != null)
                        {
                            var near = Hero(v, heroWindow);
                            near.LastSeenZone = "door:" + e.Str("door");
                            near.LastSeenKind = FeedWorldView.HeroView.LedgerKindFact;
                            near.LastSeenSeq = e.Seq;
                            near.LastSeenRound = e.Round;
                        }
                        break;
                    case "EntityInteract":
                        // S-B SecLedger (card t_30ba9943 item 2): the S-A feed
                        // carries the entity's OWN mapSectionOwner. An
                        // interaction inside an open hero window is a FACT
                        // anchor of the hero at that section ("sec:"), the
                        // same M10b attribution law as the door flip. A
                        // SectionActivated is NOT a hero fact (sections
                        // activate through mission events, not through the
                        // hero) and anchors nobody; an EntityInteract without
                        // a section payload anchors nobody (data-exists); a
                        // flip outside any window anchors nobody (never from
                        // absence). Vectors without these kinds keep their
                        // exact pre-S-B canon - the rows exist only where the
                        // facts exist.
                        {
                            var sec = e.Str("section");
                            if (heroWindow != null && !string.IsNullOrEmpty(sec))
                            {
                                var at = Hero(v, heroWindow);
                                at.LastSeenZone = "sec:" + sec;
                                at.LastSeenKind = FeedWorldView.HeroView.LedgerKindFact;
                                at.LastSeenSeq = e.Seq;
                                at.LastSeenRound = e.Round;
                            }
                            break;
                        }
                    case "RoundLimitChanged":
                        // M8/F5: the mission clock is a posture input (clock ->
                        // stall unless protect wins). Re-wire = re-derive with
                        // the MissionStart priority target, exactly the way a
                        // fresh MissionStart would decide.
                        // P33-H4b: the re-derive reads the projected victory
                        // wiring FIRST - a ModifyRndLimit(DISABLE) clock
                        // (-1) no longer auto-stalls a mission the win
                        // wiring already answers (CONCEPT_missionsachse 2.2).
                        // No victory facts -> exact M8/F5 law (Null-Law).
                        v.RoundLimit = e.Long("roundLimit", -1);
                        v.Posture = DerivePosture(v.Objective, v.PriorityTarget, v.RoundLimit,
                                                  v.ObjectiveForEmpire,
                                                  v.ImperialWinBy, v.RebelWinBy);
                        break;
                    case "VictoryConditions":
                        // P33-H4b (CONCEPT_missionsachse §1): the mission's
                        // public win wiring as ONE event per parsed escape
                        // zone (scalars repeat). Skalar fields are last-wins;
                        // an escape section joins the sorted set (additive -
                        // zones are never removed from the feed, the PHASE
                        // law is enforced by the consumer at decision time,
                        // analog M14d/H4 dynamic anchors). Posture re-derives
                        // victory-aware (FIRST input, see DerivePosture).
                        if (e.Has("imperialWinBy")) v.ImperialWinBy = e.Str("imperialWinBy") ?? "";
                        if (e.Has("rebelWinBy")) v.RebelWinBy = e.Str("rebelWinBy") ?? "";
                        if (e.Has("actor")) v.AxisActor = e.Str("actor") ?? "";
                        if (e.Has("actorName")) v.AxisActorName = e.Str("actorName") ?? "";
                        if (e.Has("escortHero")) v.EscortHero = e.Str("escortHero") ?? "";
                        {
                            var z = e.Str("section");
                            if (!string.IsNullOrEmpty(z) && !v.EscapeSections.Contains(z))
                            {
                                v.EscapeSections.Add(z);
                                v.EscapeSections.Sort(StringComparer.Ordinal);
                            }
                        }
                        v.Posture = DerivePosture(v.Objective, v.PriorityTarget, v.RoundLimit,
                                                  v.ObjectiveForEmpire,
                                                  v.ImperialWinBy, v.RebelWinBy);
                        break;
                    case "MissionObject":
                        // P33-H4c (CONCEPT_missionsachse §2.4): one event per
                        // attackable mission object, keyed by entity GUID
                        // (last-event-wins per GUID - a late-resolvable
                        // section legitimately restates the row). The raw
                        // public evidence rides along (button texts, wired
                        // event names, outcome side); the POLARITY is the
                        // CORE ladder, computed over the whole set AFTER the
                        // stream (P2 reads the final projected objective).
                        {
                            var og = e.Str("entity");
                            if (string.IsNullOrEmpty(og)) break;    // identity required
                            FeedWorldView.MissionObjectFact o;
                            if (!v.Objects.TryGetValue(og, out o))
                            {
                                o = new FeedWorldView.MissionObjectFact { Guid = og };
                                v.Objects[og] = o;
                            }
                            if (e.Has("name")) o.Name = e.Str("name") ?? "";
                            if (e.Has("section")) o.Section = e.Str("section") ?? "";
                            if (e.Has("position")) o.Position = e.Str("position") ?? "";
                            if (e.Has("health")) o.Health = e.Long("health", -1);
                            if (e.Has("defense")) o.Defense = e.Long("defense", -1);
                            if (e.Has("buttons")) o.Buttons = e.Str("buttons") ?? "";
                            if (e.Has("eventNames")) o.EventNames = e.Str("eventNames") ?? "";
                            if (e.Has("outcomeSide")) o.OutcomeSide = e.Str("outcomeSide") ?? "";
                            // P38: the authored entity type (data-existence:
                            // pre-P38 rows carry no key -> "" stays unknown).
                            if (e.Has("entityType")) o.EntityType = e.Str("entityType") ?? "";
                        }
                        break;
                    case "ThreatSpend":
                        v.ThreatBalance = e.Long("balance");
                        v.ThreatSeen = true;
                        break;
                    case "Spawn":
                        {
                            var g = Group(v, e.Str("group"));
                            g.Deployed = true;
                            var disp = e.Str("name");
                            if (!string.IsNullOrEmpty(disp)) g.DisplayName = disp;
                            // Player-22: the scenario-critical fact rides the
                            // Spawn (deployment-override trigger/event read at
                            // emit time); a missing key = unknown, never false.
                            if (e.Has("critical"))
                            {
                                g.CriticalKnown = true;
                                g.Critical = e.Bool("critical");
                            }
                            if (g.DeployedRound == 0) g.DeployedRound = e.Round;
                            long size = e.Long("size", 0);
                            if (size > 0)
                            {
                                g.Size = (int)size;
                                g.Alive = Math.Max(g.Alive, g.Size);
                            }
                            // R-B: "this intruder was pulled in by hero
                            // activity" - the window state is already part of
                            // the event stream, so this needs NO new payload
                            // key. Once set the marker never unsets (a second
                            // Spawn-style event cannot revoke the insight).
                            if (heroWindow != null)
                                g.SpawnedInHeroWindow = true;
                            break;
                        }
                    case "Death":
                        {
                            var g = Group(v, e.Str("group"));
                            g.Alive = Math.Max(0, g.Alive - 1);
                            break;
                        }
                    case "Removal":
                        Group(v, e.Str("group")).Removed = true;
                        break;
                    case "GroupDamage":
                        {
                            var g = Group(v, e.Str("group"));
                            int fig = (int)e.Long("figure");
                            g.Health = (int)e.Long("health");
                            g.Wheels[fig] = e.Long("wheel");
                            g.FiguresSeen = Math.Max(g.FiguresSeen, fig + 1);
                            // R-A shooter attribution (see GroupView fields):
                            // the hit landed while a hero's activation window
                            // was open -> that hero shot. Last hit wins; a hit
                            // between windows changes nothing (never guessed
                            // from absence). Independent of the strike law
                            // below, which additionally requires the objective
                            // to NAME the hit figure.
                            if (heroWindow != null)
                            {
                                g.LastShotHero = heroWindow;
                                g.LastShotSeq = e.Seq;
                                g.LastShotRound = e.Round;
                                // R-B Last-Seen-Ledger: the same window fact
                                // anchors the SHOOTER symbolically at the hit
                                // group - but as a HYPOTHESIS, never a fact:
                                // a hit proves weapon-range proximity, not a
                                // placed position. Kind rides visibly with
                                // the anchor (Canon + LedgerText); consumers
                                // must never render it like an objective/door
                                // fact.
                                var anchor = Hero(v, heroWindow);
                                anchor.LastSeenZone = "at:" + g.GroupId;
                                anchor.LastSeenKind = FeedWorldView.HeroView.LedgerKindHypothesis;
                                anchor.LastSeenSeq = e.Seq;
                                anchor.LastSeenRound = e.Round;
                            }
                            // Player-19 strike credit: damage that landed while
                            // a hero's activation window was open, on the figure
                            // the CURRENT objective text names ("Besiegt Ivan
                            // Talos." + a group displaying that name). Public
                            // activation order is the attribution channel - the
                            // M10b law, mirror image. Never a position read.
                            if (heroWindow != null && engages(g))
                            {
                                var striker = Hero(v, heroWindow);
                                striker.Strikes++;
                                striker.StrikeRound = e.Round;
                                // Player-21-b: a landed strike MOVED the
                                // marker - the hero fighting the phase's
                                // troops works the objective, so the marker
                                // follows the work (Thomas: "Wieso war Davith
                                // durch sein Türöffnen noch der
                                // Missionszielmarker?").
                                v.ObjectiveActor = heroWindow;
                                v.ObjectiveActorRound = e.Round;
                            }
                            else if (heroWindow == null && engages(g))
                            {
                                strikePending = true;   // credits the closer
                            }
                            // Player-25-B closer channel (see the shotPending
                            // declaration for the law): a windowless hit arms
                            // the pending credit for THIS group. First hit of
                            // the round wins (Hitgewinn); a re-arm after a
                            // resolved credit is legal - the new shot needs
                            // its own closer, else RoundEnd buries it
                            // (Tisch-Disziplin). The window path above is
                            // untouched and keeps Vorrang (seq guard below).
                            if (heroWindow == null && !shotPending.ContainsKey(g.GroupId))
                                shotPending[g.GroupId] = new[] { (long)e.Seq, (long)e.Round };
                            break;
                        }
                    // P33-H2 (findings_Player-33 §1, Thomas 08.11): the hero
                    // ENTERS the view at its printed blue deploy field, not
                    // only when it first acts or bleeds. Round 1 the Imperials
                    // activate BEFORE any hero has closed a turn, so without
                    // this registration the fresh deploy-fakt anchor had no
                    // living hero to hang the ring on and the whole S-D rail
                    // stayed blind in r1. Registering a hero that just entered
                    // the board is public table truth (the card sits there);
                    // status stays healthy/never-acted - nothing else changes.
                    case "HeroDeployed":
                        {
                            var hd = e.Str("hero");
                            if (!string.IsNullOrEmpty(hd)) Hero(v, hd);
                            break;
                        }
                    case "HeroWounded":
                        Hero(v, e.Str("hero")).Wounded = true;
                        break;
                    case "HeroDefeated":
                        {
                            var h = Hero(v, e.Str("hero"));
                            h.Defeated = true;
                            h.Wounded = true;      // the App marks defeated heroes wounded too
                            break;
                        }
                    case "HeroActivated":
                        {
                            var h = Hero(v, e.Str("hero"));
                            h.Activations++;
                            h.LastActivatedRound = e.Round;
                            lastActivator = e.Str("hero");
                            if (heroWindow == e.Str("hero"))
                            {
                                heroWindow = null;   // HIS turn ends - window closed
                            }
                            else if (heroWindow == null && attributionPending)
                            {
                                // M10b (EMPIRE6, pinned by vectors) - the
                                // legacy channel: no window events in this
                                // log, so the hero who FINISHES his turn
                                // after the change is the one who did it.
                                // A log that carries HeroActivationStart
                                // never lands here, so Player-19's "open
                                // window first" law wins whenever the App
                                // knows who was in turn.
                                v.ObjectiveActor = lastActivator;
                                v.ObjectiveActorRound = e.Round;
                                attributionPending = false;
                            }
                            // A late closure for a DIFFERENT hero than the
                            // open window is a drain race (the App noticed
                            // the next activation start before the previous
                            // hero's HUD flag) - the window hero is still in
                            // turn, the window stays open.
                            if (strikePending)
                            {
                                // M6s-B7: THIS hero's turn closed right after
                                // the mission-named figure took damage - with
                                // the checkbox flip now a causal log line,
                                // the closer is the shooter (their table
                                // discipline: status flips at the END of the
                                // turn, and events fire mid-turn).
                                var s = Hero(v, e.Str("hero"));
                                s.Strikes++;
                                s.StrikeRound = e.Round;
                                // Player-21-b: same marker move for the
                                // closed-window attribution (the flip law).
                                v.ObjectiveActor = e.Str("hero");
                                v.ObjectiveActorRound = e.Round;
                                strikePending = false;
                            }
                            // Player-25-B closer channel resolves HERE: THIS
                            // hero's turn closed in the SAME round as the
                            // windowless hit, so by the strike law the closer
                            // is the shooter. Entries from another round stay
                            // pending until RoundEnd buries them (history !=
                            // causation). Vorrang of the window path is a
                            // seq guard: a newer in-window attribution on the
                            // same group (higher LastShotSeq) already named
                            // the shooter - the older pending credit must not
                            // overwrite it. No ledger anchor is written (the
                            // closer is a turn-order hypothesis, weaker than
                            // the window fact - same reading the strike
                            // closer gets).
                            if (shotPending.Count > 0)
                            {
                                List<string> settled = null;
                                foreach (var kv in shotPending)
                                {
                                    if (kv.Value[1] != e.Round) continue;
                                    var gg = Group(v, kv.Key);
                                    if (!string.IsNullOrEmpty(gg.LastShotHero)
                                        && gg.LastShotSeq > kv.Value[0]) continue;
                                    gg.LastShotHero = e.Str("hero");
                                    gg.LastShotSeq = kv.Value[0];
                                    gg.LastShotRound = (int)kv.Value[1];
                                    if (settled == null) settled = new List<string>();
                                    settled.Add(kv.Key);
                                }
                                if (settled != null)
                                    foreach (var key in settled) shotPending.Remove(key);
                            }
                            break;
                        }
                    case "HeroActivationStart":
                        // Player-19: the window in which THIS hero's acts land
                        // (objective credits and strikes on the mission-named
                        // figure). Derived from the App's own mission-log
                        // GroupActivation line - public activation order.
                        heroWindow = e.Str("hero");
                        break;
                    // M6 aim tokens: the focus marker the human sets in the
                    // existing focus panel (public designation, read passively
                    // from the click - ZERO new input). The mark names a hero;
                    // it never claims sight, position or hidden state.
                    case "FocusChange":
                        {
                            var focus = e.Str("focus");
                            if (!string.IsNullOrEmpty(focus))
                            {
                                var h = Hero(v, focus);
                                h.Marks++;
                                h.MarkedRound = e.Round;
                            }
                            break;
                        }
                    case "RoundEnd":
                        // M6s-B7: a strike credit survives no round boundary -
                        // damage from the finished round is history, not the
                        // next closer's causation.
                        strikePending = false;
                        // Player-25-B: same law for the closer channel - an
                        // unresolved windowless hit had no closer in its own
                        // round (Tisch-Disziplin: damage must be marked
                        // before turn end), so it credits nobody.
                        shotPending.Clear();
                        break;
                    // ActivationStart/End, Move, TileChange,
                    // RoundEnd: recorded history for the audit trail and for
                    // consumers that DO have a belief layer; the view itself
                    // only exposes the state facts above.
                }
            }
            // P33-H4c: the object polarity ladder runs over the COMPLETED
            // stream - P2 needs the FINAL projected objective and a restated
            // objective legitimately re-polarises (the ladder is a pure
            // function of row + final objective, so replay stays
            // bit-identical). No objects -> no pass (Null-Law).
            DeriveObjectPolarities(v);
            return v;
        }

        // ------------------------------------------------------------ posture
        // Mission properties only - public authored data (concept 3.3.1b
        // item 4: "Posture aus Missionseigenschaften ... eliminate ist nur
        // noch Fallback"). Deterministic priority:
        //   1. hold/defend/escort goals -> protect
        //   2. mission clock / rebel progress goals (round limit, escape,
        //      terminals, recovery) -> stall (block the Konten-Punkte)
        //   3. everything else -> eliminate (legacy fallback, honest default)
        // Text matching runs on the ENGLISH authored mission data strings
        // (ui.json localisation happens later; the authored objective text is
        // public table text). Matching is WORD-prefix based: a token matches
        // when it starts with a keyword ("escorted" yes, "stronghold" does
        // NOT match "hold") - deterministic, documented heuristic; every
        // non-match honestly falls back to eliminate.
        static readonly string[] ProtectKeywords =
            { "defend", "protect", "guard", "escort", "hold" };
        static readonly string[] StallKeywords =
            { "escape", "flee", "survive", "reach", "activate", "recover",
              "steal", "deliver", "evacuate" };
        // M23 (Thomas ruling "A", 06.11, Nebenauftrag-Belagerung): the clock
        // law below silently treated EVERY round-limited mission as "the
        // enemy races the clock, we hold" - true only when the authored
        // objective belongs to the REBEL side (every campaign except Empire).
        // In the Empire campaign the SAME line is the imperial task list, so
        // an attack verb ("Destroy the crates in 6 rounds") must flip the
        // posture to eliminate BEFORE the clock law speaks. Word-prefix law
        // exactly like ProtectKeywords; DE token rides along because custom
        // missions may be authored in German (the shipped feed text is EN -
        // ui.json localizes later). Empirical anchor: an imperial guard was
        // garrisoning for six rounds instead of storming the crates.
        static readonly string[] AttackKeywords =
            { "destroy", "assault", "zerstör" };

        public static string DerivePosture(string objective, string priorityTarget, long roundLimit,
                                           bool objectiveForEmpire = false,
                                           string imperialWinBy = "", string rebelWinBy = "")
        {
            // P33-H4b ACHSEN-GESETZ FIRST (CONCEPT_missionsachse 2.2, Thomas
            // 08.11: posture=stall R1-R5 despite ModifyRndLimit-DISABLE was
            // the misread; the win wiring is public mission data and OUTRANKS
            // the text vocabulary and the clock):
            //   rebel-authored objective (objectiveForEmpire == false):
            //     ally defeat ends the mission  -> the Empire WINS by killing
            //       the axis actor -> eliminate (hunt; the kill ENDS the
            //       mission, so it outranks the escape race's stall - Player-
            //       33 acceptance (c): posture != stall despite roundLimit);
            //     ally reaches an escape zone   -> the Rebels win by racing
            //       -> stall (block the race - the existing stall semantics
            //       "rebel progress goals -> stall", now from wiring, not
            //       from text luck).
            //   imperial-authored objective (objectiveForEmpire == true):
            //     POLARITY FLIPS - the authored "ally" is the side's own
            //     escort figure, so BOTH wirings read as protect (hold the
            //     axis figure; the escape line is our own escort route, never
            //     a block target). Empirics: no shipped Empire mission wires
            //     ally defeat to a mission end (scan 06.11: 0/16) - the flip
            //     is the honest polarity law, vector-pinned in axis.json.
            // No parsed wiring ("" / "") -> the exact pre-H4b chain below,
            // bit-identical for every existing vector (Null-Law).
            if (imperialWinBy == "allyDefeated")
                return objectiveForEmpire ? "protect" : "eliminate";
            if (rebelWinBy == "allyReaches")
                return objectiveForEmpire ? "protect" : "stall";
            var words = ((objective ?? "") + " " + (priorityTarget ?? "")).ToLowerInvariant()
                        .Split(new[] { ' ', '\t', '\n', '\r', '-', ',', ';', ':', '.', '!', '?' },
                               StringSplitOptions.RemoveEmptyEntries);
            foreach (var k in ProtectKeywords)
                foreach (var w in words)
                    if (w.StartsWith(k, StringComparison.Ordinal)) return "protect";
            if (objectiveForEmpire)
                foreach (var k in AttackKeywords)
                    foreach (var w in words)
                        if (w.StartsWith(k, StringComparison.Ordinal)) return "eliminate";
            if (roundLimit >= 0) return "stall";
            foreach (var k in StallKeywords)
                foreach (var w in words)
                    if (w.StartsWith(k, StringComparison.Ordinal)) return "stall";
            return "eliminate";
        }

        // ---------------------------------------------------------- counter
        // Mission-END derivation (wounded-focus-magnet fix, Thomas 03.11).
        // The victory counter decides WHICH target law is right: a woundAll
        // end (healthy == 0) makes every hit on an already-wounded hero
        // worth ZERO progress -> healthy-first; an incapacitateAll end makes
        // the finisher doctrine genuinely optimal. Same word-boundary law as
        // DerivePosture; EN + DE vocabulary (the app ships translated
        // missions). Decisive sentence = mission-end words (rebel defeat OR
        // imperial win) + hero word + quantifier + a status word; FIRST
        // decisive sentence wins (deterministic). No match -> null: the
        // caller keeps the honest legacy default (incapacitateAll), i.e.
        // today's behaviour - derivation NEVER invents a counter.
        static readonly string[] EndWords =
            { "defeat", "defeated", "lose", "loses", "losing", "lost",
              "fail", "fails", "failed", "win", "wins", "winning",
              "niederlage", "verlieren", "verliert", "verloren",
              "scheitern", "scheitert", "gescheitert",
              "gewinnen", "gewinnt", "sieg", "siegt" };
        static readonly string[] HeroWords =
            { "hero", "heroes", "held", "helden", "rebel", "rebels", "rebellen" };
        static readonly string[] QuantifierWords =
            { "all", "alle", "aller", "every", "jeder", "jede", "jedes",
              "any", "kein", "keine", "keinen", "no", "last", "letzte",
              "letzter", "letztes", "ein", "eine", "einen" };
        static readonly string[] WoundWords =
            { "wound", "wounded", "verwundet", "verwundung", "verwundete" };
        static readonly string[] IncapWords =
            { "incapacit", "kampfunf", "ausgeschaltet", "killed", "kill",
              "getotet", "get\u00f6tet", "erschosse" };

        static readonly char[] WordSplit = new[]
        { ' ', '\t', '\n', '\r', '-', ',', ';', ':', '.', '!', '?', '"', '\'' };

        /// <summary>Derived from the mission's own text (public briefing /
        /// objective - the same class of fact DerivePosture reads), never
        /// trusted from anywhere else. "woundAll" | "incapacitateAll" | null.
        /// "wounded OR incapacitated" ends classify as woundAll: exactly like
        /// VictoryCounter.Progress, a wounded-or-worse hero already satisfies
        /// them, so further hits on him are zero counter progress.</summary>
        public static string DeriveCounter(string missionText)
        {
            if (string.IsNullOrEmpty(missionText)) return null;
            foreach (var raw in missionText.Split(
                         new[] { '.', '!', '?', ';', '\n', '\r' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var words = raw.ToLowerInvariant().Split(WordSplit,
                    StringSplitOptions.RemoveEmptyEntries);
                if (!SentenceHas(words, EndWords)) continue;
                if (!SentenceHas(words, HeroWords)) continue;
                if (!SentenceHas(words, QuantifierWords)) continue;
                if (SentenceHas(words, WoundWords)) return "woundAll";
                if (SentenceHas(words, IncapWords)) return "incapacitateAll";
            }
            return null;
        }

        static bool SentenceHas(string[] words, string[] vocab)
        {
            foreach (var v in vocab)
                foreach (var w in words)
                    if (w.StartsWith(v, StringComparison.Ordinal)) return true;
            return false;
        }

        static FeedWorldView.GroupView Group(FeedWorldView v, string id)
        {
            id = id ?? "?";
            var g = v.FindGroup(id);
            if (g == null)
            {
                g = new FeedWorldView.GroupView { GroupId = id, Name = id };
                v.Groups.Add(g);
                v.Groups.Sort((a, b) => string.CompareOrdinal(a.GroupId, b.GroupId));
            }
            return g;
        }

        static FeedWorldView.HeroView Hero(FeedWorldView v, string id)
        {
            id = id ?? "?";
            foreach (var h in v.Heroes) if (h.HeroId == id) return h;
            var x = new FeedWorldView.HeroView { HeroId = id, Name = id };
            v.Heroes.Add(x);
            v.Heroes.Sort((a, b) => string.CompareOrdinal(a.HeroId, b.HeroId));
            // M6 retro mark: a designation can exist BEFORE the hero first
            // appears in the stream (healthy heroes emit nothing until they
            // act or bleed) - the standing designation names every hero.
            // Player-19: same collision law as ApplyDesignation (this hero
            // is already in v.Heroes, so uniqueness counts his own name).
            if (Designates(v, v.Objective, id))
            {
                x.Marks++;
                x.MarkedRound = v.Round;
            }
            return x;
        }

        /// <summary>M6 public designation: the current objective text names
        /// this hero as a whole word / whole name (same deterministic
        /// word-splitting law as DerivePosture: 'smuggler' must NOT hit
        /// 'smuggle', 'Han' must not hit 'Hans').
        /// Player-19 collision law (Thomas 03.11): a LONE name token
        /// designates only while it is UNIQUE in the public name space -
        /// every hero plus every deployed group's display name, all of it
        /// table-visible. "Besiegt Ivan Talos." must NOT mark Verena Talos:
        /// the surname has two public owners, so only the full-name
        /// sequence may designate her. An objective that fully names an
        /// imperial figure is a protect signal, never a rebel designation.</summary>
        static void ApplyDesignation(FeedWorldView v, string text, int round)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var h in v.Heroes)
                if (Designates(v, text, h.HeroId))
                {
                    h.Marks++;
                    h.MarkedRound = round;
                }
        }

        /// <summary>Player-19 designation law = a NamesHero match AND, when
        /// the match rests on a LONE name token (no full-name sequence in
        /// the text), that token must have exactly ONE owner in the public
        /// name space (all hero names + all group display names). Full-name
        /// sequences are never ambiguous and always designate. With no group
        /// display names known (old event logs) the hero names alone decide
        /// - the pre-Player-19 behaviour for every name that is unique
        /// among the heroes, which is exactly what the old vectors encode.</summary>
        static bool Designates(FeedWorldView v, string text, string heroId)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(heroId) || heroId == "?")
                return false;
            var words = SplitWords(text);
            var name = SplitWords(heroId);
            if (name.Count == 0 || words.Count == 0) return false;
            // full-name sequence -> unambiguous, designates
            if (words.Count >= name.Count)
                for (int i = 0; i + name.Count <= words.Count; i++)
                {
                    bool hit = true;
                    for (int j = 0; j < name.Count; j++)
                        if (string.Compare(words[i + j], name[j], StringComparison.OrdinalIgnoreCase) != 0)
                        { hit = false; break; }
                    if (hit) return true;
                }
            // lone name part -> only when the token is public-name-space unique
            foreach (var n in name)
                if (n.Length >= 3 && ContainsWord(words, n) && TokenOwners(v, n) <= 1)
                    return true;
            return false;
        }

        static bool ContainsWord(List<string> words, string token)
        {
            foreach (var w in words)
                if (string.Compare(w, token, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            return false;
        }

        /// Owners of a name token in the public name space: every hero plus
        /// every group whose DISPLAY name is known (Spawn carried "name").
        static int TokenOwners(FeedWorldView v, string token)
        {
            int n = 0;
            foreach (var h in v.Heroes)
                if (ContainsWord(SplitWords(h.Name ?? h.HeroId), token)) n++;
            foreach (var g in v.Groups)
                if (!string.IsNullOrEmpty(g.DisplayName) && ContainsWord(SplitWords(g.DisplayName), token))
                    n++;
            return n;
        }

        /// <summary>Player-19 public seam: does this text name this figure
        /// by full-name sequence or lone name token (the same NamesHero law
        /// the designation logic runs)? Exposed so the app layer can
        /// recognise a PROTECT objective ("Besiegt Ivan Talos." names a
        /// deployed imperial figure).</summary>
        public static bool TextNames(string text, string displayName)
            => NamesHero(text, displayName);

        static bool NamesHero(string text, string heroId)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(heroId) || heroId == "?")
                return false;
            var words = SplitWords(text);
            var name = SplitWords(heroId);
            if (name.Count == 0 || words.Count == 0) return false;
            // full-name sequence first (covers names whose parts are all short)
            if (words.Count >= name.Count)
                for (int i = 0; i + name.Count <= words.Count; i++)
                {
                    bool hit = true;
                    for (int j = 0; j < name.Count; j++)
                        if (string.Compare(words[i + j], name[j], StringComparison.OrdinalIgnoreCase) != 0)
                        { hit = false; break; }
                    if (hit) return true;
                }
            // single name part as a word ("kill Jabba" names 'Jabba the Hutt');
            // length>=3 guard: stray 1-2 letter tokens must not designate.
            foreach (var n in name)
                if (n.Length >= 3)
                    foreach (var w in words)
                        if (string.Compare(w, n, StringComparison.OrdinalIgnoreCase) == 0)
                            return true;
            return false;
        }

        static List<string> SplitWords(string text)
        {
            var words = new List<string>();
            var cur = new StringBuilder();
            foreach (var ch in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch)) cur.Append(ch);
                else { if (cur.Length > 0) { words.Add(cur.ToString()); cur.Length = 0; } }
            }
            if (cur.Length > 0) words.Add(cur.ToString());
            return words;
        }

        // ------------------------------------------------- midround replan
        // P33-H4b (CONCEPT_missionsachse 2.3, Player-33: "Helden-Zugang
        // mitten in der Runde ist Fakt, aber Fokus/Reihenfolge erst am
        // naechsten Rundenanfang neu"). The three feed facts that make the
        // CURRENT round's focus/order stale INSIDE the round: a new mission
        // objective (ObjectiveChange), newly parsed win wiring
        // (VictoryConditions) and a hero/ally entering mid-round
        // (HeroDeployed). This is the pure detector; the REACTION law
        // (ReactionOrder at activation, a board rule) keeps its precedence -
        // only the PLAN/ORD layers are recomputed same-round by the caller.
        // Deterministic: FIRST matching event after planSeq in stream order,
        // in the CURRENT round only (later rounds roll over the plan anyway
        // via the normal round-start rebuild). No matching event -> null ->
        // the caller plans exactly once per round like today (Null-Law).
        public static string MidroundReplanTrigger(IReadOnlyList<WorldEvent> events,
                                                   int planSeq, int round)
        {
            if (events == null) return null;
            foreach (var e in events)
            {
                if (e.Seq <= planSeq) continue;
                if (e.Round != round) continue;
                if (e.Kind == "ObjectiveChange" || e.Kind == "VictoryConditions"
                    || e.Kind == "HeroDeployed" || e.Kind == "MissionObject")
                    return e.Kind;
            }
            return null;
        }

        // ================================================ P33-H4c polaritaet
        // CONCEPT_missionsachse §2.4 POLARITÄTS-LEITER (Thomas 3, hart):
        // an object becomes an attack target ONLY when its polarity is
        // DERIVED; without a rung it is NEUTRAL - nobody attacks it, nobody
        // defends it. The ladder reads ONLY public mission-file data that
        // the MissionObject event carries (button texts, wired event names,
        // outcome side) plus the projected objective channel:
        //   P2 Objective: startingObjective/ObjectiveChange text +
        //      objectiveForEmpire names the object with a destroy/claim/
        //      disable verb => the side OF the goal attacks, the opponent
        //      gets the SCHUTZ-pflicht.
        //   P1 Buttons/event names: 'Claim for Empire' at the object =>
        //      Empire may claim => Rebels contest it (mirror: a hero-side
        //      claim/rescue button or a faction event name => Empire
        //      ATTACKS - the EMPIRE3 protester form).
        //   P3 Outcome text at the destroy/claim event: the side that LOSES
        //      when the object falls, defends it.
        //   P4 nothing derivable => NEUTRAL.
        // Mehrdeutig P1 vs P2 => P2 gewinnt (explicit Thomas ruling);
        // otherwise the rung order decides (P1 rides above P3). failImperial/
        // failRebel are DEAD flags (measured 0x true in the whole collection)
        // and are NEVER read anywhere. Deterministic: no RNG, ordinal ties.
        // P34-A2: "anspruch" is the authored-DE claim stem (beansprucht/
        // beanspruchen/Anspruch) - EN "claim" keeps its exact behavior.
        static readonly string[] P2Verbs =
            { "destroy", "zerstör", "zerstoer", "sabotag", "deactivat", "disable",
              "disarm", "entwaffn", "claim", "besetz", "anspruch" };

        /// Recompute every object's polaritaet over the COMPLETED stream:
        /// a pure function of the row + final objective + perspective flag,
        /// so replaying the same events yields the same verdict bit-identically.
        public static void DeriveObjectPolarities(FeedWorldView v)
        {
            if (v == null || v.Objects.Count == 0) return;   // Null-Law
            foreach (var o in v.Objects.Values)
                DeriveObjectPolarity(o, v.Objective, v.ObjectiveForEmpire);
        }

        /// The ladder for ONE object (public for the harness vectors).
        public static void DeriveObjectPolarity(FeedWorldView.MissionObjectFact o,
                                                string objective, bool objectiveForEmpire)
        {
            // P38: the BYSTANDER flag is derived in the same pure pass (the
            // ladder verdicts below stay bit-identical — the bystander class
            // is orthogonal, see Core/NeutralPrey.cs).
            o.Bystander = NeutralPrey.IsBystander(o);
            // P2 wins over P1 by ruling - evaluate it FIRST.
            if (NamesObject(objective, o.Name) && HasVerb(objective, P2Verbs))
            {
                o.Polaritaet = objectiveForEmpire ? "jagt" : "schuetzt";
                o.Basis = "P2";
                return;
            }
            // P1: hero-side claim/rescue evidence OUTRANKS empire-side claim
            // evidence (EMPIRE3 carries BOTH buttons and its verdict is
            // JAGT per the concept's own example).
            bool rebel = false, emp = false;
            foreach (var s in o.EvidenceStrings())
            {
                if (RebelClaimEvidence(s)) rebel = true;
                else if (EmpClaimEvidence(s)) emp = true;
            }
            if (rebel) { o.Polaritaet = "jagt"; o.Basis = "P1"; return; }
            if (emp) { o.Polaritaet = "schuetzt"; o.Basis = "P1"; return; }
            // P3: the producer read the outcome sentence of the WIRED end
            // event ("rebels" = the Rebels lose when this object falls).
            if (o.OutcomeSide == "rebels") { o.Polaritaet = "jagt"; o.Basis = "P3"; return; }
            if (o.OutcomeSide == "empire") { o.Polaritaet = "schuetzt"; o.Basis = "P3"; return; }
            // P4: NEUTRAL - kein Angriffsziel, kein Schutzziel.
            o.Polaritaet = "neutral"; o.Basis = "P4";
        }

        /// 'Claim for Empire' form: an imperial faction marker WITH a claim
        /// word, on a button text or a wired event name.
        public static bool EmpClaimEvidence(string s)
        {
            var t = (s ?? "").ToLowerInvariant();
            if (t.Length == 0) return false;
            bool imperial = t.Contains("empire") || t.Contains("imperium");
            bool claim = t.Contains("claim") || t.Contains("besetz") || t.Contains("anspruch");
            return imperial && claim;
        }

        /// The rebel/hero side of P1: a faction event name ("Protester
        /// Rebels N") or a hero-side claim/rescue/interact button WITHOUT
        /// an imperial marker (buttons are presented to the hero player).
        public static bool RebelClaimEvidence(string s)
        {
            var t = (s ?? "").ToLowerInvariant();
            if (t.Length == 0) return false;
            if (t.Contains("rebel") || t.Contains("rebellen")) return true;
            bool imperial = t.Contains("empire") || t.Contains("imperium");
            bool claim = t.Contains("claim") || t.Contains("besetz") || t.Contains("anspruch");
            bool rescue = t.Contains("rescue") || t.Contains("rette") || t.Contains("befrei");
            return (claim || rescue) && !imperial;
        }

        /// The objective names the object: full lower-case name containment,
        /// or the cluster base name (trailing " <number>" stripped, at least
        /// 4 chars) - the same full-name-token discipline NamesAllyKill uses.
        public static bool NamesObject(string objective, string objectName)
        {
            var o = (objective ?? "").ToLowerInvariant();
            var n = (objectName ?? "").Trim().ToLowerInvariant();
            if (o.Length == 0 || n.Length == 0) return false;
            if (o.Contains(n)) return true;
            int sp = n.LastIndexOf(' ');
            if (sp >= 0)
            {
                var tail = n.Substring(sp + 1);
                bool digits = tail.Length > 0;
                foreach (var ch in tail) if (ch < '0' || ch > '9') { digits = false; break; }
                if (digits)
                {
                    var baseName = n.Substring(0, sp);
                    if (baseName.Length >= 4 && o.Contains(baseName)) return true;
                }
            }
            return false;
        }

        static bool HasVerb(string text, string[] verbs)
        {
            var t = (text ?? "").ToLowerInvariant();
            foreach (var v in verbs) if (t.Contains(v)) return true;
            return false;
        }
    }
}
