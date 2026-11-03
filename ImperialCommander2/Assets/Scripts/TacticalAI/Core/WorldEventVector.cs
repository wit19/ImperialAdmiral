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
        // M8/F5: MissionStart clock inputs, kept so a runtime RoundLimitChanged
        // can re-derive the posture exactly like a fresh MissionStart would.
        // (Canon text unchanged - posture, not its inputs, is the decision.)
        public string PriorityTarget = "";
        public long RoundLimit = -1;
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
            }
            return sb.ToString();
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
            foreach (var e in events)
            {
                if (e.Round > v.Round) v.Round = e.Round;
                switch (e.Kind)
                {
                    case "MissionStart":
                        v.Objective = e.Str("objective") ?? "";
                        v.PriorityTarget = e.Str("priorityTarget") ?? "";
                        v.RoundLimit = e.Long("roundLimit", -1);
                        v.Posture = DerivePosture(e.Str("objective"),
                                                  e.Str("priorityTarget"),
                                                  e.Long("roundLimit", -1));
                        ApplyDesignation(v, v.Objective, e.Round);   // initial public designation
                        break;
                    case "ObjectiveChange":
                        v.Objective = e.Str("objective") ?? v.Objective;
                        ApplyDesignation(v, v.Objective, e.Round);
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
                        break;
                    case "RoundLimitChanged":
                        // M8/F5: the mission clock is a posture input (clock ->
                        // stall unless protect wins). Re-wire = re-derive with
                        // the MissionStart priority target, exactly the way a
                        // fresh MissionStart would decide.
                        v.RoundLimit = e.Long("roundLimit", -1);
                        v.Posture = DerivePosture(v.Objective, v.PriorityTarget, v.RoundLimit);
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
                            if (g.DeployedRound == 0) g.DeployedRound = e.Round;
                            long size = e.Long("size", 0);
                            if (size > 0)
                            {
                                g.Size = (int)size;
                                g.Alive = Math.Max(g.Alive, g.Size);
                            }
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
                            // Player-19 strike credit: damage that landed while
                            // a hero's activation window was open, on the figure
                            // the CURRENT objective text names ("Besiegt Ivan
                            // Talos." + a group displaying that name). Public
                            // activation order is the attribution channel - the
                            // M10b law, mirror image. Never a position read.
                            if (heroWindow != null && !string.IsNullOrEmpty(g.DisplayName)
                                && !string.IsNullOrEmpty(v.Objective)
                                && NamesHero(v.Objective, g.DisplayName))
                            {
                                var striker = Hero(v, heroWindow);
                                striker.Strikes++;
                                striker.StrikeRound = e.Round;
                            }
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
                    // ActivationStart/End, Move, TileChange,
                    // RoundEnd: recorded history for the audit trail and for
                    // consumers that DO have a belief layer; the view itself
                    // only exposes the state facts above.
                }
            }
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

        public static string DerivePosture(string objective, string priorityTarget, long roundLimit)
        {
            var words = ((objective ?? "") + " " + (priorityTarget ?? "")).ToLowerInvariant()
                        .Split(new[] { ' ', '\t', '\n', '\r', '-', ',', ';', ':', '.', '!', '?' },
                               StringSplitOptions.RemoveEmptyEntries);
            foreach (var k in ProtectKeywords)
                foreach (var w in words)
                    if (w.StartsWith(k, StringComparison.Ordinal)) return "protect";
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
    }
}
