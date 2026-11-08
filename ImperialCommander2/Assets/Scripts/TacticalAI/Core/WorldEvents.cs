// M5 (concept 3.3.1b, v4.0 Thomas-Entscheid 27.10): PASSIVE world feed.
// An append-only, per-mission event history built ONLY from signals the App
// produces anyway while playing - canon rule: no feature may ever require
// the human to report positions or states to the App (zero new input).
//
// LEAK INVARIANT (principle 5 / 7.1): every payload is public table
// knowledge - names, zone labels, rounds, damage-WHEEL values, threat
// balance. Hidden information (hero cards, dice, hands, medpacks, villain
// deck) has no home in this vocabulary: Append() rejects unknown payload
// keys fail-loud, and the kind/key tables below are the whole surface.
//
// Pure C# (no Unity): the runtime capture lives in WorldEventsUnity.cs, the
// deterministic projection into a planner-facing view in WorldEventVector.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TacticalAI.Core
{
    public sealed class WorldEvent
    {
        public int Seq;                 // 1-based, contiguous per mission
        public string Kind;             // must be in WorldEventFeed.Kinds
        public int Round;               // gameVars.round at emit time
        public Dictionary<string, object> Payload;

        public WorldEvent(string kind, int round, Dictionary<string, object> payload)
        {
            Kind = kind;
            Round = round;
            Payload = payload ?? new Dictionary<string, object>();
        }

        public string Str(string key)
        {
            object v;
            return Payload.TryGetValue(key, out v) ? Json.AsStr(v) : null;
        }
        public long Long(string key, long dflt = 0)
        {
            object v;
            return Payload.TryGetValue(key, out v) ? Json.AsLong(v) : dflt;
        }
        // Player-22 (Thomas 04.11): boolean payload reader, same pattern.
        // Paired with Has() so a MISSING key stays distinguishable from a
        // present false (data-existence law - absence never reads as fact).
        public bool Bool(string key, bool dflt = false)
        {
            object v;
            return Payload.TryGetValue(key, out v) ? Json.AsBool(v) : dflt;
        }
        public bool Has(string key)
        {
            return Payload.ContainsKey(key);
        }
    }

    /// <summary>Append-only public-event history for ONE mission run. Seq is
    /// contiguous by construction; consumers that read a stale window detect
    /// the gap with VerifySeq and demand a Resync (re-log of the complete
    /// history from current App state - never a guess).</summary>
    public sealed class WorldEventFeed
    {
        // M5 event vocabulary (task t_006ccc2b Scope B). Kinds the App can
        // derive passively TODAY are wired in WorldEventsUnity; Move and
        // TileChange are vocabulary slots reserved for the tile-graph read
        // (concept 3.3.1b items 1-2, open point 3) - no producer yet, by
        // design: inventing movement from beliefs would violate the
        // no-estimation rule.
        // R-A (concept v1.2 §5): the DOOR is a live producer since R-A - the
        // adapter polls the digital door state (Door.doorOpen, Door.cs:18),
        // which flips ONLY through app/event plumbing (mission event
        // ModifyMapEntity -> MapEntityManager.ModifyEntityCoroutine ->
        // DoorPrefab.ModifyEntity). HONEST BOUNDARY: a door opened PHYSICALLY
        // at the table never moves that field and stays invisible to the feed
        // - a public fact the App cannot see; never guessed, never inferred
        // from traffic. Door events carry NO name claim beyond the entity's
        // own name. R-A left them as pure audit-trail history; R-B adds ONE
        // lawful projection: a flip inside an OPEN hero window anchors the
        // window hero symbolically at "door:<GUID>" (fact) in the
        // Last-Seen-Ledger. That anchor is a PROJECTION, not vocabulary -
        // zones (obj:/door:/at:) are view-side anchor identities, never
        // payload keys a producer appends, which is why the ledger needed
        // NO change to Kinds or PayloadKeys below.
        public static readonly HashSet<string> Kinds = new HashSet<string>
        {
            "MissionStart", "Spawn", "Move", "Death", "Removal",
            "ObjectiveChange", "TileChange", "FocusChange", "ThreatSpend",
            "ActivationStart", "ActivationEnd",
            // Player-19 (Thomas 03.11): the hero-turn OPEN marker (the App's
            // own mission-log GroupActivation line names the hero - public
            // activation order, the same channel M10b attribution already
            // uses). It lets the feed tell an objective change that happened
            // DURING a hero's turn from one that happened between turns.
            "HeroActivationStart",
            "HeroActivated", "HeroWounded", "HeroDefeated",
            "GroupDamage", "RoundEnd",
            // R-A: digital door flips, one event per observed state change
            // (adapter-side snapshot dedup, same law as the damage dials).
            "DoorOpen", "DoorClosed",
            // S-A (concept positionierung v1 §5.1/§8.1, card t_00fda230):
            // SectionActivated - a map section became publicly visible
            // (TileManager.ActivateMapSection is the single choke that flips
            // MapSection.isActive; the feed diffs the LIVE state through the
            // app's own IsMapSectionActive accessor, so it also reads the
            // post-restore copy). Deactivation is snapshot-tracked but
            // carries NO kind yet: the concept consumes activation only, and
            // a re-activation after a removal re-emits honestly. One kind on
            // purpose - a wall is never modeled (Grundgesetz): a section the
            // feed has no event for stays through-pass, never "closed".
            "SectionActivated",
            // S-A §5.1/§5.2: EntityInteract - a Token/Terminal/Crate entity
            // flipped its digital entityProperties.isActive (the ModifyEntity
            // plumbing lane, MissionEventHandler's "Open Crate"-class events;
            // the same flip law R-A proved for doorOpen). BOTH directions
            // ride this one kind - direction is data ("active"), the fact is
            // "something touched this entity". Doors are deliberately NOT
            // this kind (doorOpen has its own R-A pair; DoorPrefab normalizes
            // door isActive to true at init); DeploymentPoint/Highlight/Tile
            // isActive is validity/visibility plumbing, not interaction.
            // A2 law (measured 04.11.): terminals that never flip simply
            // produce no events - degradation, section anchors keep running.
            "EntityInteract",
            // S-A §5.4 join channel: the PlayerSelection mission-logger line
            // (PromptBox button/cancel, the same logger M8/F1 already drains)
            // enters the feed as a typed event so consumers can JOIN it with
            // the isActive/door diff by seq/round adjacency ("the player
            // clicked X right as the entity flag moved"). App-authored
            // button text is a legal anchor name (Loch-Gesetz: no coords).
            "PlayerSelection",
            // M14e (Thomas 07.11, CONCEPT_standort_ledger_v2): HeroDeployed
            // - a hero/ally entered the mission through its blue ENTRY
            // field. The entry field is PRINTED mission data (the mined
            // taktische_hero_entries table), the placement itself is the
            // printed setup step - this is table knowledge, not a position
            // report: zero new input (the field is law, not a choice).
            // Missions without a field emit nothing; multi-field missions
            // emit ONE event per field the hero's ledger carries (the set
            // is the honest claim, SectionSpace seeds all of them as
            // possible sections, never a point).
            "HeroDeployed",
            // M8/F5: mission clock re-wired at runtime (ModifyRndLimit event
            // or the roundLimitToggle flip) - public table info, the feed
            // gate re-derives posture from it.
            "RoundLimitChanged",
            // P33-H4b (Thomas 08.11, CONCEPT_missionsachse): VictoryConditions
            // - the mission's PUBLIC win wiring, derived from the mission file's
            // own fields (fixedAlly/specificAlly, useAllyDefeated+endMission
            // wiring, escape-vocabulary entities whose button ends the mission).
            // Reading fields, never guessing; nothing parseable => no event
            // (Null-Law: today's behaviour bit-identical).
            "VictoryConditions",
            // P33-H4c (Thomas 08.11, CONCEPT_missionsachse §2.4): MissionObject
            // - a mission-file object whose OWN theText carries the printed
            // board rule "can attack X (Health: N[, Defense: ...])". The
            // attackable fact plus its public wiring evidence (button texts,
            // wired event names, outcome side) ride as raw public data; the
            // POLARITY LADDER is Core law (WorldEventVector), not a producer
            // claim. Nothing parseable => no event (Null-Law).
            "MissionObject",
        };

        // The COMPLETE payload surface. Anything else (a die value, a hero
        // card field, a hidden counter) cannot enter the feed.
        public static readonly HashSet<string> PayloadKeys = new HashSet<string>
        {
            "group", "figure", "wheel", "health", "hero", "zone", "tile",
            "objective", "focus", "balance", "roundLimit", "priorityTarget",
            "mission", "missionType", "resync", "size",
            // M23 (Thomas 06.11): the Empire campaign authored its objective
            // text as the IMPERIAL task list ("Find and destroy the control
            // terminals") - every other campaign writes it as the rebel task.
            // The producer states the perspective (0/1) as a public fact
            // (campaign expansion code); the posture law reads it. Absent = 0
            // = the legacy reading (attack verbs belong to the enemy).
            "objectiveForEmpire",
            // Player-19: the group's DISPLAY NAME on Spawn (the same name the
            // table reads on the deployment card - public). The designation
            // law needs it to catch name collisions (Ivan Talos / Verena
            // Talos): a mission text naming an imperial figure must never
            // mark the rebel hero who merely shares the surname.
            "name",
            // Player-22 (Thomas 04.11): the scenario-critical defeat wiring
            // on Spawn (deployment-override setTrigger/setEvent, read at
            // emit time). LESSON PLAYER-22 (EMPIRE2 log, "poll error
            // (ignored): payload key not public-vocabulary: critical"): a
            // new payload key that skips THIS registration poisons the
            // whole event - and the seen-ledger used to swallow the spawn
            // forever. New kind OR key: register here AND let the harness
            // validate every fixture payload through Validate().
            "critical",
            // R-A: the door's GUID string (stable public identity); "name"
            // above is the optional display sugar on door events too.
            "door",
            // S-A: the entity's GUID string (EntityInteract identity) and
            // the section's GUID string (SectionActivated identity; on
            // EntityInteract the optional anchor read live from the entity's
            // OWN mapSectionOwner field - fact from the data model,
            // swap-corrected, never estimated). "active": the post-flip
            // isActive state on EntityInteract (direction is data, not kind).
            // Loch-Gesetz holds: GUIDs and app names, never coordinates.
            "entity", "section", "active",
            // M14e: the ENTRY-POINT identity (DP/highlight entity GUID
            // string) on Spawn ("dp": the ledger's entry DP this group came
            // through - the verdict comment in WorldEventsUnity is retired,
            // the ledger IS the deploy-path ledger it demanded) and on
            // HeroDeployed ("dp": the blue hero-entry field). "hero" is the
            // existing key. Same law as "entity": identity GUID, never a
            // coordinate; absence stays distinguishable from a value
            // (Has()), no-claim deployments carry NO dp key.
            "dp",
            // P33-H4b VictoryConditions (CONCEPT_missionsachse). All values
            // are public mission-file facts: "imperialWinBy"/"rebelWinBy" the
            // win-law tokens ("" = this side has no parsed win wiring -
            // explicit absence, never a guess), "actor"/"actorName" the axis
            // figure (fixedAlly/specificAlly identity + its public display
            // name), "escortHero" the specificHero card's public name,
            // "section" (registered above) the escape/evac section GUID on
            // the block axis. Loch-Gesetz holds: ids and app names only.
            "imperialWinBy", "rebelWinBy", "actor", "actorName", "escortHero",
            // P33-H4c MissionObject (CONCEPT_missionsachse §2.4). All values
            // are public mission-file facts read from the object's OWN
            // fields: "name"/"entity"/"section" above carry identity and
            // the entity's own mapSectionOwner; "health"/"defense" are the
            // PRINTED numbers inside the object's own "can attack ...
            // (Health: N, Defense: ...)" text (SICHTBARKEITSGRENZE: printed
            // text values, never a runtime damage counter);
            // "buttons"/"eventNames" are RAW public authoring strings
            // (";"-joined, ordinal) - the P1 evidence the CORE ladder
            // classifies, not a producer verdict; "outcomeSide" is the
            // losing side named by the WIRED end event's outcome sentence
            // (P3). failImperial/failRebel are dead and never enter here.
            // P38 (card t_11ebb843): "entityType" is the app's AUTHORED
            // entity-type enum name, lower-cased ("token"/"door"/"terminal"/
            // "highlight"/...) - a public mission-file field, same category
            // as the printed text; the CORE bystander law classifies it,
            // the producer never judges. Data-existence optional.
            "defense", "buttons", "eventNames", "outcomeSide", "entityType",
        };

        // P33-H4c: keys scoped to ONE kind (leak-guard law stays global for
        // every other kind). "position" is the entity's PRINTED setup
        // placement "x;y" - authored data-model identity of the object
        // itself, allowed ONLY on MissionObject. The Loch-Gesetz keeps
        // holding exactly where it was measured: no runtime kind (Move,
        // hero/guard anchors, door/section/entity events) may ever carry a
        // position - the two leak-guard fixtures pin that.
        static readonly Dictionary<string, string[]> KindScopedKeys =
            new Dictionary<string, string[]>
        {
            { "MissionObject", new[] { "position" } },
        };

        // Minimal per-kind shape guard (typos fail loud, not silently).
        static readonly Dictionary<string, string[]> RequiredKeys =
            new Dictionary<string, string[]>
        {
            { "MissionStart",     new[] { "objective", "priorityTarget", "roundLimit", "mission" } },
            { "Spawn",            new[] { "group" } },
            { "Move",             new[] { "group", "zone" } },
            { "Death",            new[] { "group" } },
            { "Removal",          new[] { "group" } },
            { "ObjectiveChange",  new[] { "objective" } },
            { "TileChange",       new[] { "tile" } },
            { "FocusChange",      new[] { "focus" } },
            { "ThreatSpend",      new[] { "balance" } },
            { "ActivationStart",  new[] { "group" } },
            { "ActivationEnd",    new[] { "group" } },
            { "HeroActivationStart", new[] { "hero" } },
            { "HeroActivated",    new[] { "hero" } },
            { "HeroWounded",      new[] { "hero" } },
            { "HeroDefeated",     new[] { "hero" } },
            { "GroupDamage",      new[] { "group", "figure", "wheel", "health" } },
            // R-A: identity is required; the display name is optional.
            { "DoorOpen",         new[] { "door" } },
            { "DoorClosed",       new[] { "door" } },
            // S-A: identities required; name/section/active are optional
            // sugar or facts-of-the-moment (a section name may be empty in
            // authoring; section on EntityInteract is only appended when the
            // entity's own mapSectionOwner is set - data-existence law).
            { "SectionActivated", new[] { "section" } },
            { "EntityInteract",   new[] { "entity" } },
            // M14e: hero + entry field identity required; "section" is the
            // live mapSectionOwner of that field (data-exists: the producer
            // only emits when the map carries the entity).
            { "HeroDeployed",     new[] { "hero", "dp" } },
            { "PlayerSelection",  new[] { "name" } },
            { "RoundEnd",         new string[0] },
            { "RoundLimitChanged", new[] { "roundLimit" } },
            // P33-H4b: BOTH win tokens required on every VictoryConditions
            // event (empty string = explicit "no wiring on this side");
            // section rides along on the block axis (data-existence law).
            { "VictoryConditions", new[] { "imperialWinBy", "rebelWinBy" } },
            // P33-H4c: identity + the printed Health fact are required (the
            // parse gate IS the Null-Law: no "can attack (Health: N" => no
            // event at all). section/position/defense/buttons/eventNames/
            // outcomeSide are data-existence optional (authoring varies).
            { "MissionObject", new[] { "name", "entity", "health" } },
        };

        public readonly string MissionKey;
        readonly List<WorldEvent> _events = new List<WorldEvent>();
        int _resyncs;

        public WorldEventFeed(string missionKey)
        {
            MissionKey = missionKey ?? "solo";
        }

        public IReadOnlyList<WorldEvent> Events => _events;
        public int Count => _events.Count;
        public int LastSeq => _events.Count == 0 ? 0 : _events[_events.Count - 1].Seq;
        public int ResyncCount => _resyncs;

        /// <summary>Append one public event; seq = count+1 (contiguous by
        /// construction). Throws on unknown kind / payload key (leak guard).</summary>
        public WorldEvent Append(string kind, int round, Dictionary<string, object> payload)
        {
            Validate(kind, payload);
            var e = new WorldEvent(kind, Math.Max(0, round), payload)
            { Seq = _events.Count + 1 };
            _events.Add(e);
            return e;
        }

        /// <summary>Replace the history with a full re-log (seq must come in
        /// contiguous from 1). Used when a consumer saw a seq gap: the whole
        /// truth is re-logged from current App state, never estimated.</summary>
        public void Resync(IEnumerable<WorldEvent> fullLog)
        {
            var rebuilt = new List<WorldEvent>();
            int expect = 1;
            foreach (var e in fullLog)
            {
                Validate(e.Kind, e.Payload);
                if (e.Seq != expect)
                    throw new InvalidOperationException(
                        "WorldEventFeed.Resync: seq gap in re-log (expected "
                        + expect + ", got " + e.Seq + ")");
                rebuilt.Add(new WorldEvent(e.Kind, e.Round, e.Payload) { Seq = expect++ });
            }
            _events.Clear();
            _events.AddRange(rebuilt);
            _resyncs++;
        }

        // PUBLIC (Player-22 lesson): this is the ONE vocabulary law - the
        // harness pre-validates every fixture payload through it, so a key
        // that the in-game feed would reject fails the harness FIRST
        // (Player-22 shipped past the harness and died in-game: poll error,
        // spawn swallowed). Producers may call it too; it never mutates.
        public static void Validate(string kind, Dictionary<string, object> payload)
        {
            if (kind == null || !Kinds.Contains(kind))
                throw new ArgumentException("WorldEventFeed: unknown event kind " + kind);
            string[] req;
            if (!RequiredKeys.TryGetValue(kind, out req)) req = new string[0];
            foreach (var k in req)
                if (payload == null || !payload.ContainsKey(k))
                    throw new ArgumentException("WorldEventFeed: " + kind + " requires key " + k);
            if (payload == null) return;
            string[] scoped;
            KindScopedKeys.TryGetValue(kind, out scoped);
            foreach (var k in payload.Keys)
                if (!PayloadKeys.Contains(k) &&
                    (scoped == null || Array.IndexOf(scoped, k) < 0))
                    throw new ArgumentException("WorldEventFeed: payload key not public-vocabulary: " + k);
        }

        /// <summary>First missing seq in a consumer's window, or null when
        /// contiguous 1..n. A non-null answer mandates Resync, not guessing.</summary>
        public static int? VerifySeq(IReadOnlyList<WorldEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i].Seq != i + 1) return i + 1;
            return null;
        }

        // -------------------------------------------------------------- hash
        /// <summary>SHA-256 over the canonical history text. Deterministic:
        /// payload keys sorted ordinal, invariant-culture scalars.</summary>
        public string Hash() => HashOf(_events);

        public static string HashOf(IReadOnlyList<WorldEvent> events)
        {
            var sb = new StringBuilder();
            foreach (var e in events) sb.Append(CanonicalLine(e)).Append('\n');
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        public static string CanonicalLine(WorldEvent e)
        {
            return e.Seq.ToString(CultureInfo.InvariantCulture) + "|" + e.Kind + "|"
                 + e.Round.ToString(CultureInfo.InvariantCulture) + "|" + CanonPayload(e.Payload);
        }

        public static string CanonPayload(Dictionary<string, object> payload)
        {
            if (payload == null || payload.Count == 0) return "{}";
            var keys = new List<string>(payload.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder("{");
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(keys[i]).Append(":").Append(CanonValue(payload[keys[i]]));
            }
            return sb.Append("}").ToString();
        }

        static string CanonValue(object v)
        {
            if (v == null) return "~";
            if (v is bool) return ((bool)v) ? "1" : "0";
            if (v is string) return "\"" + ((string)v).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            if (v is float) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
            if (v is IList<object>)
            {
                var list = (IList<object>)v;
                var parts = new List<string>();
                foreach (var x in list) parts.Add(CanonValue(x));
                return "[" + string.Join(",", parts.ToArray()) + "]";
            }
            if (v is Dictionary<string, object>)
                return CanonPayload((Dictionary<string, object>)v);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }
}
