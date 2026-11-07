// S-B (Konzept positionierung v1 8.1 + 9): the SECTION-space graph. Nodes are
// section GUIDs (the only spatial fact the feed carries - Revision IV.1
// retired the tile space: no wall bit exists in any shipped data). Edges are
// three-valued KNOWLEDGE about a connection, never a permission:
//
//   door      - data-derived: a door entity links the two sections and its
//               state rides the door history (DoorOpen/DoorClosed events,
//               R-A/S-A feed); never observed => state "unknown".
//   confirmed - a crossing was OBSERVED: a hero anchored in section A and in
//               section B inside the SAME activation window means the hero
//               crossed between them (concept 9 law "Held bewegt sich, also
//               existiert ein Pfad" at section granularity).
//   unknown   - NO edge record between two sections. This is the absence of
//               knowledge, never knowledge of absence.
//
// GRUNDGESETZ AS CODE (non-negotiable, 8 law): an unknown edge is PERMEABLE.
// IsPassable() is literally true for every pair, a closed door is a STATE,
// not a blockade, and DistanceLowerBound() is a LOWER bound with -1 meaning
// "no knowledge" - never "impossible". Any consumer that derives "the hero
// cannot come here" from this graph is violating the law the QA gate evicts;
// every reaction must run on positive evidence.
//
// Pure C# (no UnityEngine), fully deterministic (every enumeration sorted by
// ordinal), no doubles anywhere.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public sealed class SectionGraph
    {
        public const string KindDoor = "door";
        public const string KindConfirmed = "confirmed";
        // Door edge states from the door history. "unknown" = no DoorOpen/
        // DoorClosed event ever named this door: permeable belief, NOT closed.
        public const string DoorUnknown = "unknown";
        public const string DoorOpen = "open";
        public const string DoorClosed = "closed";
        // Returned by DistanceLowerBound when the recorded graph carries no
        // route. Because unknown edges are permeable, this reads "nothing is
        // known about the distance", never "far away" and never "blocked".
        public const int NoKnowledge = -1;

        private sealed class Edge
        {
            public string Kind = KindDoor;          // KindDoor | KindConfirmed
            public readonly List<string> Doors = new List<string>(); // sorted guids
        }

        // section guid -> neighbour guid -> edge (stored on both endpoints).
        private readonly Dictionary<string, Dictionary<string, Edge>> _adj =
            new Dictionary<string, Dictionary<string, Edge>>(StringComparer.Ordinal);
        // door guid -> open|closed (last observation wins, feed order).
        private readonly Dictionary<string, string> _doorState =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // section guid -> app-authored public name (hole law: names only, no
        // coordinates - a section's rect is never stored here).
        private readonly Dictionary<string, string> _names =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // ---- nodes ------------------------------------------------------
        public IReadOnlyList<string> Sections
        {
            get
            {
                var l = new List<string>(_adj.Keys);
                l.Sort(StringComparer.Ordinal);
                return l;
            }
        }

        public bool HasSection(string guid)
            => !string.IsNullOrEmpty(guid) && _adj.ContainsKey(guid);

        /// Register a section node (SectionActivated payload, deploy marker or
        /// mined table row). Names are the app-authored public names; a null
        /// name keeps a previously stored one. Re-registering is idempotent.
        public void AddSection(string guid, string name = null)
        {
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("SectionGraph: section guid required");
            Dictionary<string, Edge> dummy;
            if (!_adj.TryGetValue(guid, out dummy)) _adj[guid] = new Dictionary<string, Edge>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(name) && !_names.ContainsKey(guid)) _names[guid] = name;
        }

        public string SectionName(string guid)
        {
            string n;
            return guid != null && _names.TryGetValue(guid, out n) ? n : "";
        }

        // ---- edges ------------------------------------------------------
        private Dictionary<string, Edge> EnsureNode(string guid)
        {
            Dictionary<string, Edge> adj;
            if (!_adj.TryGetValue(guid, out adj)) { adj = new Dictionary<string, Edge>(StringComparer.Ordinal); _adj[guid] = adj; }
            return adj;
        }

        private Edge GetOrCreateEdge(string a, string b)
        {
            var adjA = EnsureNode(a);
            var adjB = EnsureNode(b);
            Edge e;
            if (!adjA.TryGetValue(b, out e))
            {
                e = new Edge();
                adjA[b] = e;
                adjB[a] = e; // same object on both endpoints
            }
            return e;
        }

        /// Data-derived door edge (e.g. mined door<->section table rows fed by
        /// the S-C bridge). Idempotent; later doors between the same pair
        /// accumulate (state is open if ANY of them is open).
        public void AddDoorEdge(string a, string b, string doorGuid)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || string.IsNullOrEmpty(doorGuid))
                throw new ArgumentException("SectionGraph: door edge needs both sections and a door guid");
            if (string.Equals(a, b, StringComparison.Ordinal)) return; // self-loop is no edge
            var e = GetOrCreateEdge(a, b);
            if (!e.Doors.Contains(doorGuid)) e.Doors.Add(doorGuid);
        }

        /// Fact-grade edge: the crossing was observed inside one hero window.
        /// Upgrades an existing door edge's knowledge class (the door record
        /// stays for state display); never downgrades.
        public void ConfirmEdge(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return;
            if (string.Equals(a, b, StringComparison.Ordinal)) return;
            var e = GetOrCreateEdge(a, b);
            e.Kind = KindConfirmed;
        }

        /// Door history (R-A feed): DoorOpen -> true, DoorClosed -> false.
        /// Unknown doors stay unknown until a named event arrives (data-exists).
        public void NoteDoorState(string doorGuid, bool open)
        {
            if (string.IsNullOrEmpty(doorGuid)) return;
            _doorState[doorGuid] = open ? DoorOpen : DoorClosed;
        }

        /// P33-H4c: the visible state of ONE door by GUID (DoorOpen/DoorClosed/
        /// DoorUnknown). The TUER-sprengen plan option reads ONLY this visible
        /// flip (SICHTBARKEITSGRENZE: doorOpen, never a damage counter) - an
        /// already open door is nothing left to blow.
        public string DoorStateOfGuid(string doorGuid)
        {
            if (string.IsNullOrEmpty(doorGuid)) return DoorUnknown;
            string st;
            return _doorState.TryGetValue(doorGuid, out st) ? st : DoorUnknown;
        }

        /// State of the pair's connection: open if any door edge door was last
        /// seen open; closed if door edges exist and every one was last seen
        /// closed; otherwise unknown. A closed state is a STATE, not a
        /// blockade - IsPassable ignores it (Grundgesetz).
        public string DoorStateOf(string a, string b)
        {
            Edge e;
            Dictionary<string, Edge> adj;
            if (a == null || b == null) return DoorUnknown;
            if (!_adj.TryGetValue(a, out adj)) return DoorUnknown;
            if (!adj.TryGetValue(b, out e)) return DoorUnknown;
            if (e.Doors.Count == 0) return DoorUnknown;
            bool anyOpen = false, anyClosed = false;
            foreach (var d in e.Doors)
            {
                string st;
                if (!_doorState.TryGetValue(d, out st)) continue;      // unseen door: no vote
                if (st == DoorOpen) anyOpen = true; else anyClosed = true;
            }
            if (anyOpen) return DoorOpen;
            if (anyClosed && !anyOpen) return DoorClosed;
            return DoorUnknown;
        }

        /// The knowledge class of a recorded edge: KindDoor | KindConfirmed,
        /// or null when NO edge is recorded (that is "unknown", which is
        /// permeable by the Grundgesetz - null is not a blockade either).
        public string EdgeKind(string a, string b)
        {
            Edge e;
            if (a == null || b == null) return null;
            Dictionary<string, Edge> adj;
            return _adj.TryGetValue(a, out adj) && adj.TryGetValue(b, out e) ? e.Kind : null;
        }

        /// P33-H4c: the door GUIDs recorded on the edge a<->b (sorted), or an
        /// empty list when no edge is recorded. The TUER-sprengen Wegwahl
        /// asks "does a door sit ON this edge" - identity only, Loch-Gesetz.
        public List<string> DoorsOnEdge(string a, string b)
        {
            Edge e;
            if (a == null || b == null) return new List<string>();
            Dictionary<string, Edge> adj;
            if (!_adj.TryGetValue(a, out adj) || !adj.TryGetValue(b, out e))
                return new List<string>();
            return new List<string>(e.Doors);
        }

        /// GRUNDGESETZ AS CODE: every connection is permeable. Unknown edges
        /// are passable, closed doors are states not walls. A consumer may
        /// use this only for reachability beliefs - and may NEVER call its
        /// negation, because no pair is ever closed.
        public bool IsPassable(string a, string b) => true;

        /// Neighbours over ALL recorded edge kinds (door + confirmed), sorted.
        /// Sections that are NOT listed are unknown, not unreachable.
        public IReadOnlyList<string> NeighborsOf(string s)
        {
            Dictionary<string, Edge> adj;
            if (s == null || !_adj.TryGetValue(s, out adj)) return new List<string>();
            var l = new List<string>(adj.Keys);
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        /// BFS hop count over the RECORDED edges (all kinds, all passable -
        /// a closed door still counts: Untergrenze). Returns NoKnowledge (-1)
        /// when the recorded graph has no route: because unknown edges are
        /// permeable, the true distance could be anything INCLUDING 1 -
        /// consumers must treat -1 as "no knowledge", never as "far" or
        /// "blocked".
        public int DistanceLowerBound(string a, string b)
        {
            if (a == null || b == null) return NoKnowledge;
            if (string.Equals(a, b, StringComparison.Ordinal)) return 0;
            if (!_adj.ContainsKey(a) || !_adj.ContainsKey(b)) return NoKnowledge;
            var dist = new Dictionary<string, int>(StringComparer.Ordinal) { { a, 0 } };
            var queue = new Queue<string>();
            queue.Enqueue(a);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var d = dist[cur];
                foreach (var nb in NeighborsOf(cur))
                {
                    if (dist.ContainsKey(nb)) continue;
                    dist[nb] = d + 1;
                    if (string.Equals(nb, b, StringComparison.Ordinal)) return d + 1;
                    queue.Enqueue(nb);
                }
            }
            return NoKnowledge;
        }

        /// Positive-evidence ball: every section at recorded-distance <= t
        /// from s (t < 0 behaves like 0: just s itself when s is known).
        /// Sections outside the ball are UNKNOWN, never excluded: the mask
        /// status law (SectionMask.StatusUnknown) is the only honest reading.
        public IReadOnlyList<string> BallWithin(string s, int t)
        {
            var result = new List<string>();
            if (s == null || !_adj.ContainsKey(s)) return result;
            var dist = new Dictionary<string, int>(StringComparer.Ordinal) { { s, 0 } };
            result.Add(s);
            var queue = new Queue<string>();
            queue.Enqueue(s);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                var d = dist[cur];
                if (d >= t) continue;
                foreach (var nb in NeighborsOf(cur))
                {
                    if (dist.ContainsKey(nb)) continue;
                    dist[nb] = d + 1;
                    result.Add(nb);
                    queue.Enqueue(nb);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        // ---- deterministic dump (harness pin + EXPLAIN trail) -----------
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in Sections)
            {
                sb.Append("sec ").Append(s);
                var n = SectionName(s);
                if (n.Length > 0) sb.Append(" name=").Append(n);
                sb.Append('\n');
                foreach (var nb in NeighborsOf(s))
                {
                    // print each edge once: only while s < neighbour
                    if (string.CompareOrdinal(s, nb) >= 0) continue;
                    Edge e;
                    _adj[s].TryGetValue(nb, out e);
                    sb.Append("edge ").Append(s).Append(' ').Append(nb)
                      .Append(" kind=").Append(e != null ? e.Kind : "?");
                    if (e != null && e.Doors.Count > 0)
                    {
                        var ds = new List<string>(e.Doors);
                        ds.Sort(StringComparer.Ordinal);
                        sb.Append(" doors=").Append(string.Join(",", ds.ToArray()));
                        sb.Append(" state=").Append(DoorStateOf(s, nb));
                    }
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        // ---- copy (incremental law: prior state continues, never re-rolls)
        public SectionGraph Clone()
        {
            var g = new SectionGraph();
            foreach (var s in _adj.Keys)
            {
                g.AddSection(s, SectionName(s));
                foreach (var kv in _adj[s])
                {
                    if (string.CompareOrdinal(s, kv.Key) >= 0) continue; // both endpoints once
                    var e = GetOrCreateEdgeOn(g, s, kv.Key);
                    e.Kind = kv.Value.Kind;
                    foreach (var d in kv.Value.Doors)
                        if (!e.Doors.Contains(d)) e.Doors.Add(d);
                }
            }
            foreach (var kv in _doorState) g._doorState[kv.Key] = kv.Value;
            return g;
        }

        private static SectionGraph.Edge GetOrCreateEdgeOn(SectionGraph g, string a, string b)
        {
            g.EnsureNode(a); g.EnsureNode(b);
            Edge e;
            if (!g._adj[a].TryGetValue(b, out e))
            {
                e = new Edge();
                g._adj[a][b] = e;
                g._adj[b][a] = e;
            }
            return e;
        }
    }
}
