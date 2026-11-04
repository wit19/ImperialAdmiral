// S-B (Konzept positionierung v1 8.1 + 9): the section-space projector. It
// consumes the S-A feed kinds (SectionActivated, EntityInteract) plus the
// R-A door events and the movement/damage kinds already on the wire and
// derives the deterministic, coordinate-free state the S-D consumers read:
// the SectionGraph, the per-group SectionMask beliefs, the OBSERVED speed
// track and the two duty-fact probes (Wachzone/Palpatine, 3c).
//
// Laws this class encodes (each pinned by section_space.json):
//  * DATA-EXISTS: no fact without payload - an EntityInteract without a
//    section anchors nobody; a group without a deploy seed stays unseeded;
//    a probe without its positive facts returns nothing. Nothing is ever
//    derived from absence, no default is invented for missing data.
//  * ATTRIBUTION (M10b law, same as the vector ledger): an EntityInteract
//    belongs to the hero whose activation window is open; windows are
//    strictly sequential in the App log (HeroActivationStart replaces the
//    open window), so the window hero decides - no open window, no fact.
//  * CROSSING LAW (concept 9 law "Held bewegt sich, also existiert ein
//    Pfad", section granularity): distinct sections anchored back-to-back
//    inside ONE hero window mean the hero crossed - the edge is CONFIRMED
//    and the crossing count feeds the speed track. A door event inside the
//    window between two different anchors additionally records a door edge
//    (state rides the door history via NoteDoorState).
//  * SPEED IS OBSERVED, NEVER INTENDED (concept 0'): growth speed = max
//    observed hero crossings per window (floor 1 before any observation) -
//    own intent would be self-confirmation.
//  * FENSTER-UM-DIE-RUNDEN (concept 0' c): every belief is evaluated AT a
//    round (Candidates(..., roundNow, ...)); nothing ever claims "the hero
//    WILL be able to reach X next round".
//  * GRUNDGESETZ (8 law): the graph never blocks, distances are lower
//    bounds, masks have no "closed" status. Probes report facts, never
//    prohibitions and never an ordering (ordering is S-D's decision).
//  * HOLE LAW: GUIDs and app-authored names only. No section rect, no tile,
//    no coordinate ever enters this module (the feed guard already refuses
//    such payload keys; this class never even accepts a numeric position).
//
// Determinism: replaying events onto a Clone equals one-shot projection
// (the vector's incremental law, pinned for this module too); every dump is
// sorted; no clocks, no RNG.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// One hero-interaction factum inside a mission-critical section whose
    /// guard mask overlaps it - the Wachzone positive-evidence pair (concept
    /// 5.3 + 10). Activating the guard ahead of plan order is S-D's decision;
    /// this object only states the facts that obligate it.
    public sealed class WachzoneFact
    {
        public string Hero;
        public string Section;
        public string GuardGroup;
        public int Round;
    }

    /// One 3c factum (concept 10 "3c"): a section activated in the SAME round
    /// as a fresh spawn whose seed belief sits in that section, AND a hero
    /// factually in that section that round (the Eindringling). "Newly placed
    /// units first" is S-D's ordering law; this is the fact that triggers it.
    public sealed class ThreeCFact
    {
        public string Group;
        public string Section;
        public string Hero;
        public int Round;
    }

    /// S-C projection options. Defaults reproduce the S-B law exactly
    /// (Refine = true, no door-owner knowledge), so every pre-S-C call
    /// site - the whole section_space.json canon - stays bit-identical.
    ///  * Refine=false (low skill rungs, Thomas-Gesetz 04.11.): the belief
    ///    carries ONLY feed facts - deploy seed, dilation, observed speed,
    ///    graph edges, alive bookkeeping. No hit re-grounding, no wounded
    ///    group disjunctive, no transit hypotheses. The exception probes
    ///    (Wachzone/3c) and the victim ring do NOT consult this flag:
    ///    Fakt-Reaktion ist Fairness, kein Intelligenz-Bonus.
    ///  * DoorOwners: door GUID -> owning section GUID from the shipped
    ///    tactical_mission_critical table (SectionTrust.DoorOwnersOf). A
    ///    door flip inside an open hero window then records the TRANSIT
    ///    HYPOTHESIS "the hero went through / into the owner section"
    ///    (concept 8.1 L2': hypothesis grade, never fact).
    public sealed class SectionOptions
    {
        public bool Refine = true;
        public Dictionary<string, string> DoorOwners;

        public SectionOptions Copy()
        {
            return new SectionOptions { Refine = Refine, DoorOwners = DoorOwners };
        }
    }

    public sealed class SectionSpace
    {
        public SectionGraph Graph = new SectionGraph();

        // Last observed section per hero (fact grade: came from an
        // EntityInteract with a section payload inside that hero's window).
        private readonly Dictionary<string, string> _heroInSection =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _heroInSectionRound =
            new Dictionary<string, int>(StringComparer.Ordinal);

        // All attributed interaction facta (hero, entity, section, round) -
        // the Wachzone rail reads these; the vector ledger anchors on the
        // identical law (SectionSpace never re-derives attribution rules).
        private struct InteractionFactum
        {
            public string Hero; public string Entity; public string Section; public int Round;
        }
        private readonly List<InteractionFactum> _interactions = new List<InteractionFactum>();

        // Sections activated per round (SectionActivated payloads).
        private readonly Dictionary<int, List<string>> _activatedByRound =
            new Dictionary<int, List<string>>();
        // Fresh spawns: group -> record (round + seed sections snapshot).
        private sealed class SpawnRecord
        {
            public int Round;
            public readonly List<string> Seeds = new List<string>();
        }
        private readonly Dictionary<string, SpawnRecord> _spawnsByGroup =
            new Dictionary<string, SpawnRecord>(StringComparer.Ordinal);

        private readonly Dictionary<string, GroupSectionBelief> _beliefs =
            new Dictionary<string, GroupSectionBelief>(StringComparer.Ordinal);

        // ---- speed track: observed crossings per hero window -------------
        public struct SpeedObservation
        {
            public string Hero; public int Round; public int Crossings; public int SectionsTouched;
        }
        private readonly List<SpeedObservation> _speedObs = new List<SpeedObservation>();
        public IReadOnlyList<SpeedObservation> SpeedObservations { get { return _speedObs; } }
        /// Growth speed for the masks: max observed crossings per window,
        /// floor 1 until anything is observed (observed, never intended).
        public int SectionsPerWindow
        {
            get
            {
                int max = 1;
                foreach (var o in _speedObs) if (o.Crossings > max) max = o.Crossings;
                return max;
            }
        }

        // Transient hero-window state: ordered section anchors + the doors
        // that moved while the window was open (with the section the hero
        // stood on at the flip - a carried fact, never a position).
        private sealed class HeroWindow
        {
            public int Round;
            public readonly List<string> Sections = new List<string>();
            public readonly List<PendingDoor> Doors = new List<PendingDoor>();
        }
        private struct PendingDoor { public string Door; public string Section; }
        private readonly Dictionary<string, HeroWindow> _windows =
            new Dictionary<string, HeroWindow>(StringComparer.Ordinal);

        // Imperial window for HeroWounded attribution (P-A law: the LAST
        // ActivationStart, closed by the ActivationEnd of that same group).
        private string _imperialWindowGroup = "";

        // S-C: projection options (see SectionOptions) + the transit
        // hypothesis track. A transit is a door flip inside an open hero
        // window whose OWNING section is named by the shipped table - the
        // hero plausibly went there (hypothesis grade, concept 8.1 L2').
        // Facts stay facts: the hero's fact anchor only ever moves via an
        // EntityInteract with a section payload.
        private SectionOptions _opts = new SectionOptions();
        public SectionOptions Options { get { return _opts; } }
        private struct TransitRecord { public string Hero; public string Door; public string Section; public int Round; }
        private readonly List<TransitRecord> _transits = new List<TransitRecord>();

        public int Round { get; private set; }
        public bool IsResync { get; private set; }

        public GroupSectionBelief BeliefOf(string group)
        {
            GroupSectionBelief b;
            return group != null && _beliefs.TryGetValue(group, out b) ? b : null;
        }

        public string HeroInSection(string hero)
        {
            string s;
            return hero != null && _heroInSection.TryGetValue(hero, out s) ? s : "";
        }

        // ---- projection ---------------------------------------------------
        /// Replay events onto an optional prior state (incremental law).
        /// deploySeeds: group -> sections from the deploy-marker facts (the
        /// S-A result: exact = one section, candidates = several).
        /// doorEdges: [sectionA, sectionB, doorGuid] rows from mined data
        /// (S-C feeds the tactical_mission_critical hypotheses; a missing
        /// row keeps the pair unknown-PERMEABLE, never a wall).
        public static SectionSpace Project(IReadOnlyList<WorldEvent> events,
                                           SectionSpace prior = null,
                                           Dictionary<string, string[]> deploySeeds = null,
                                           List<string[]> doorEdges = null,
                                           SectionOptions opts = null)
        {
            var s = prior != null ? prior.Clone() : new SectionSpace();
            if (prior != null) s.IsResync = true;
            // S-C: an explicit opts always wins; without one a prior keeps
            // its own (the curve does not flip mid-mission), a fresh space
            // takes the S-B default (full refinement, no door owners).
            if (opts != null) s._opts = opts.Copy();
            if (deploySeeds == null) deploySeeds = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (doorEdges != null)
                foreach (var row in doorEdges)
                    if (row != null && row.Length >= 3) s.Graph.AddDoorEdge(row[0], row[1], row[2]);
            if (events == null) return s;
            foreach (var e in events) s.Step(e, deploySeeds);
            return s;
        }

        static string S(WorldEvent e, string key)
        {
            var v = e.Str(key);
            return v ?? "";
        }

        private void Step(WorldEvent e, Dictionary<string, string[]> deploySeeds)
        {
            if (e.Round > Round) Round = e.Round;
            switch (e.Kind)
            {
                case "MissionStart":
                    IsResync = false;
                    break;

                case "SectionActivated":
                {
                    // The node itself is the fact; a section that never
                    // activated stays unknown-PERMEABLE, never excluded
                    // (no-wall Grundgesetz).
                    var sec = S(e, "section");
                    if (sec.Length > 0)
                    {
                        Graph.AddSection(sec, S(e, "name"));
                        List<string> l;
                        if (!_activatedByRound.TryGetValue(e.Round, out l))
                        {
                            l = new List<string>();
                            _activatedByRound[e.Round] = l;
                        }
                        if (!l.Contains(sec)) l.Add(sec);
                        l.Sort(StringComparer.Ordinal);
                    }
                    break;
                }

                case "HeroActivationStart":
                {
                    var hero = S(e, "hero");
                    if (hero.Length > 0)
                    {
                        // The App log is strictly sequential: the new window
                        // replaces whatever was open (same law as the vector
                        // ledger's heroWindow variable).
                        var w = new HeroWindow { Round = e.Round };
                        // The section the hero was FACTUALLY last seen in is
                        // the window's starting anchor (carried fact).
                        string last;
                        if (_heroInSection.TryGetValue(hero, out last)) w.Sections.Add(last);
                        _windows[hero] = w;
                    }
                    break;
                }

                case "HeroActivated":
                {
                    var hero = S(e, "hero");
                    HeroWindow w;
                    if (hero.Length > 0 && _windows.TryGetValue(hero, out w))
                    {
                        int crossings = 0;
                        for (int i = 1; i < w.Sections.Count; i++)
                        {
                            if (string.Equals(w.Sections[i - 1], w.Sections[i], StringComparison.Ordinal)) continue;
                            crossings++;
                            Graph.ConfirmEdge(w.Sections[i - 1], w.Sections[i]); // crossing law
                        }
                        // Doors that moved inside this window: a door edge
                        // between the section the hero stood ON when the door
                        // flipped and every section anchored AFTER it. No
                        // standing section -> no edge (data-exists).
                        foreach (var d in w.Doors)
                        {
                            if (d.Section.Length == 0) continue;
                            bool seenDoor = false;
                            foreach (var sec in w.Sections)
                            {
                                if (!seenDoor)
                                {
                                    if (string.Equals(sec, d.Section, StringComparison.Ordinal)) seenDoor = true;
                                    continue;
                                }
                                if (!string.Equals(sec, d.Section, StringComparison.Ordinal))
                                    Graph.AddDoorEdge(d.Section, sec, d.Door);
                            }
                        }
                        var distinct = new HashSet<string>(w.Sections, StringComparer.Ordinal);
                        _speedObs.Add(new SpeedObservation
                        {
                            Hero = hero, Round = w.Round, Crossings = crossings, SectionsTouched = distinct.Count
                        });
                        _windows.Remove(hero);
                    }
                    break;
                }

                case "EntityInteract":
                {
                    var sec = S(e, "section");
                    var entity = S(e, "entity");
                    if (sec.Length > 0)
                    {
                        // NO name here on purpose: an EntityInteract's "name"
                        // is the ENTITY's public name, not the section's.
                        // Section names ride SectionActivated payloads only
                        // (or the S-C table rows) - the EXPLAIN chain must
                        // never show an entity name as a section name.
                        Graph.AddSection(sec);
                        // Attribution law (M10b, vector parity): the OPEN
                        // window decides. The App log opens windows strictly
                        // one at a time, so at most one is tracked open for
                        // this hero; a HeroActivationStart without a closing
                        // HeroActivated simply gets replaced (drain-race law).
                        foreach (var kv in _windows)
                        {
                            var hero = kv.Key;
                            _heroInSection[hero] = sec;
                            _heroInSectionRound[hero] = e.Round;
                            var win = kv.Value;
                            if (win.Sections.Count == 0
                                || !string.Equals(win.Sections[win.Sections.Count - 1], sec, StringComparison.Ordinal))
                                win.Sections.Add(sec);
                            _interactions.Add(new InteractionFactum
                            {
                                Hero = hero, Entity = entity, Section = sec, Round = e.Round
                            });
                            break; // sequential windows: at most one
                        }
                    }
                    break;
                }

                case "DoorOpen":
                case "DoorClosed":
                {
                    var door = S(e, "door");
                    if (door.Length == 0) break; // fail-loud upstream; defensive
                    Graph.NoteDoorState(door, e.Kind == "DoorOpen");
                    // Remember the flip inside every open window for the
                    // door-edge law (with the section the hero stood on).
                    foreach (var kv in _windows)
                    {
                        var win = kv.Value;
                        var standing = win.Sections.Count > 0
                            ? win.Sections[win.Sections.Count - 1] : "";
                        win.Doors.Add(new PendingDoor { Door = door, Section = standing });
                    }
                    // S-C transit hypothesis (concept 8.1 L2'): a door flip
                    // inside an open hero window whose OWNING section the
                    // shipped table names records "the hero plausibly went
                    // through/into that section" for the window hero -
                    // HYPOTHESIS grade, the fact anchor is untouched. Refine
                    // off (low rungs) or owner unknown: no hypothesis, and
                    // nothing is excluded either (Grundgesetz). The owner
                    // node becomes known here - the hero physically used it.
                    if (_opts.Refine && _opts.DoorOwners != null)
                    {
                        string owner;
                        if (_opts.DoorOwners.TryGetValue(door, out owner) && owner.Length > 0)
                            foreach (var kv in _windows)
                            {
                                Graph.AddSection(owner);
                                _transits.Add(new TransitRecord
                                {
                                    Hero = kv.Key, Door = door, Section = owner, Round = e.Round
                                });
                            }
                    }
                    break;
                }

                case "Spawn":
                {
                    var group = S(e, "group");
                    if (group.Length == 0) break;
                    long size = e.Long("size", 1);
                    if (size < 1) size = 1;
                    _beliefs[group] = GroupSectionBelief.FromSpawn(group, (int)size);
                    var rec = new SpawnRecord { Round = e.Round };
                    // Seed sources, priority: the Spawn's OWN section payload
                    // (the S-C wiring path), else the deploy-marker seeds fed
                    // into Project (the S-A result). No source -> unseeded
                    // belief: honest, the probes simply never fire for it.
                    var sec = S(e, "section");
                    if (sec.Length > 0) rec.Seeds.Add(sec);
                    else
                    {
                        string[] seeds;
                        if (deploySeeds.TryGetValue(group, out seeds))
                            foreach (var sd in seeds)
                                if (!string.IsNullOrEmpty(sd) && !rec.Seeds.Contains(sd)) rec.Seeds.Add(sd);
                    }
                    rec.Seeds.Sort(StringComparer.Ordinal);
                    _spawnsByGroup[group] = rec;
                    if (rec.Seeds.Count > 0)
                    {
                        foreach (var sd in rec.Seeds) Graph.AddSection(sd); // nodes exist for belief math
                        _beliefs[group].SeedDeploy(rec.Seeds, e.Round);
                    }
                    break;
                }

                case "GroupDamage":
                {
                    var group = S(e, "group");
                    GroupSectionBelief b;
                    if (group.Length == 0 || !_beliefs.TryGetValue(group, out b)) break; // data-exists
                    // S-C skill curve: the Eichung re-grounding is mask
                    // VERFEINERUNG - low rungs keep the seed-and-dilation
                    // entry reading (Thomas-Gesetz 04.11., no switch).
                    if (!_opts.Refine) break;
                    int figure = (int)e.Long("figure");
                    b.NoteHit(Graph, figure, e.Round, SectionsPerWindow);
                    break;
                }

                case "HeroWounded":
                {
                    // Attribution: the group whose imperial window is open
                    // (last ActivationStart not closed - P-A law). No window,
                    // no attribution, no anchor (never from absence). AND no
                    // attribution while a HERO window is open: a wound during
                    // the hero's own activation is a mission trigger or
                    // environment effect, not an attack by the last imperial
                    // group - naming that group would invent causation.
                    if (_imperialWindowGroup.Length == 0) break;
                    if (_windows.Count > 0) break;
                    GroupSectionBelief b;
                    if (!_beliefs.TryGetValue(_imperialWindowGroup, out b)) break;
                    if (!_opts.Refine) break;   // S-C skill curve: the
                    // group disjunctive is a VERFEINERUNG observation, the
                    // low rung keeps its entry reading (see GroupDamage).
                    b.NoteHeroWounded(Graph, e.Round, SectionsPerWindow);
                    break;
                }

                case "ActivationStart":
                {
                    var g = S(e, "group");
                    if (g.Length > 0) _imperialWindowGroup = g;
                    break;
                }

                case "ActivationEnd":
                {
                    var g = S(e, "group");
                    if (g.Length > 0 && g == _imperialWindowGroup) _imperialWindowGroup = "";
                    break;
                }

                case "Death":
                {
                    var group = S(e, "group");
                    GroupSectionBelief b;
                    if (group.Length > 0 && _beliefs.TryGetValue(group, out b)) b.NoteDeath();
                    break;
                }
            }
        }

        // ---- probes (facts only; S-D decides, this module never orders) ----
        /// Wachzone duty facts: every attributed hero interaction inside a
        /// section the consumer names as mission-critical, crossed with the
        /// guard groups whose mask overlaps that section AT the interaction's
        /// round (fenster-um-die-runden: evaluated where it happened).
        public List<WachzoneFact> WachzoneFacts(IEnumerable<string> criticalSections,
                                                IEnumerable<string> guardGroups)
        {
            var crit = new HashSet<string>(criticalSections ?? new string[0], StringComparer.Ordinal);
            var guards = new List<string>(guardGroups ?? new string[0]);
            guards.Sort(StringComparer.Ordinal);
            var facts = new List<WachzoneFact>();
            foreach (var f in _interactions)
            {
                if (!crit.Contains(f.Section)) continue;
                foreach (var g in guards)
                {
                    GroupSectionBelief b;
                    if (!_beliefs.TryGetValue(g, out b)) continue;
                    if (!GroupMaskOverlaps(b, f.Section, f.Round)) continue;
                    facts.Add(new WachzoneFact { Hero = f.Hero, Section = f.Section, GuardGroup = g, Round = f.Round });
                }
            }
            return facts;
        }

        /// Group occupancy at a round: ANY figure's candidate set containing
        /// the section (group mask; the per-figure masks stay the key).
        public bool GroupMaskOverlaps(GroupSectionBelief b, string section, int roundNow)
        {
            foreach (var idx in b.FigureIndexes())
            {
                var m = b.MaskOf(idx);
                if (m != null && m.Status(Graph, section, roundNow, SectionsPerWindow) == SectionMask.StatusInside)
                    return true;
            }
            return false;
        }

        /// 3c duty facts (concept 10): SectionActivated in round R + a fresh
        /// spawn in the SAME round whose seed belief sits in that section + a
        /// hero factually in that section in that same round (Eindringling).
        /// Any leg missing -> no fact (data-exists), never a suspicion.
        public List<ThreeCFact> ThreeCFacts()
        {
            var facts = new List<ThreeCFact>();
            var rounds = new List<int>(_activatedByRound.Keys);
            rounds.Sort();
            foreach (var round in rounds)
            {
                foreach (var sec in _activatedByRound[round])
                {
                    // hero in that section that round (fact grade only)
                    var heroes = new List<string>();
                    foreach (var hf in _interactions)
                        if (hf.Round == round && string.Equals(hf.Section, sec, StringComparison.Ordinal)
                            && !heroes.Contains(hf.Hero)) heroes.Add(hf.Hero);
                    if (heroes.Count == 0) continue;   // no Eindringling fact -> no 3c
                    heroes.Sort(StringComparer.Ordinal);
                    var groups = new List<string>(_spawnsByGroup.Keys);
                    groups.Sort(StringComparer.Ordinal);
                    foreach (var g in groups)
                    {
                        var rec = _spawnsByGroup[g];
                        if (rec.Round != round) continue;      // "neu platziert" = same round
                        if (!rec.Seeds.Contains(sec)) continue; // belief sits in the new section
                        foreach (var hero in heroes)
                            facts.Add(new ThreeCFact { Group = g, Section = sec, Hero = hero, Round = round });
                    }
                }
            }
            return facts; // built in sorted order already
        }

        // S-C (concept 10 "VERGELTUNG", card Scope 1): the backwards ring for
        // ONE victim. The shooter's position is what Vergeltung wants; the
        // VICTIM's position is what the command actually knows nothing about
        // beyond its own belief - so the only honest ring is built around the
        // argmax of the victim's OWN mask (card wording). Semantics:
        //  * strong = the victim mask's candidate set. Masks are uniform-weight
        //    (S-B set semantics), so "the argmax" of a uniform mask is exactly
        //    its candidate set - a tie is not broken by invention, it is stated
        //    (one candidate = the sharp case; several = the honest disjunctive).
        //  * figureIndex -1 (anonymous victim, S-B convention): the victim
        //    figure is unknown, so the ring rides the union over ALL of the
        //    group's figure masks and NEVER names a figure (disjunctive law).
        //  * weak = the distributed disjunctiv over the REST: every other
        //    KNOWN section, ordered by lower-bound distance from the strong set
        //    (unknown distance last, then ordinal). A section the graph never
        //    saw is not in the list because nothing is known about it - and it
        //    is NEVER excluded (Grundgesetz: the ring names possibilities, not
        //    prohibitions; consumers may down-weight weak, never forbid strong).
        //  * Death deduction: alive==0 => basis "wiped", empty ring (a defeated
        //    group was not shot at - the feed cannot produce that anyway);
        //    PoolSize states the surviving-figure pool honestly.
        // NOT skill-gated, NOT trust-gated: a hit is a FACT, reaction to facts
        // is fairness (Thomas-Gesetz 04.11.).
        public sealed class VictimRing
        {
            public string Group;
            public int Figure;
            public string Basis;                 // argmax | anon-disjunctive | wiped | unseeded | no-belief
            public int PoolSize;                 // surviving figures (Death deduction applied)
            public readonly List<string> Strong = new List<string>();
            public readonly List<string> Weak = new List<string>();

            /// Deterministic one-line form (harness pin + EXPLAIN rail).
            public string Dump()
            {
                return "ring " + Group + " fig=" + Figure + " basis=" + Basis +
                       " pool=" + PoolSize +
                       " strong=" + (Strong.Count == 0 ? "-" : string.Join(",", Strong.ToArray())) +
                       " weak=" + (Weak.Count == 0 ? "-" : string.Join(",", Weak.ToArray())) + "\n";
            }
        }

        /// The transit hypothesis sections (hypothesis grade) a hero plausibly
        /// moved through/into via a table-owned door during an activation
        /// window. Fact anchors are NOT mixed in - HeroInSection stays the
        /// fact rail. Sorted, unique, deterministic.
        public List<string> HeroTransitSections(string hero)
        {
            var l = new List<string>();
            if (hero == null) return l;
            foreach (var t in _transits)
                if (string.Equals(t.Hero, hero, StringComparison.Ordinal) && !l.Contains(t.Section))
                    l.Add(t.Section);
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        /// Backwards ring around the victim-argmax of its own mask (see
        /// VictimRing). No belief for the group => null (data-exists).
        public VictimRing VictimRingOf(string group, int figureIndex)
        {
            GroupSectionBelief b;
            if (group == null || !_beliefs.TryGetValue(group, out b)) return null;
            var ring = new VictimRing { Group = group, Figure = figureIndex, Basis = "no-belief" };
            ring.PoolSize = b.Alive;                                // Death deduction
            if (b.Alive <= 0) { ring.Basis = "wiped"; return ring; }
            var strong = new HashSet<string>(StringComparer.Ordinal);
            if (figureIndex >= 0)
            {
                var m = b.MaskOf(figureIndex);
                if (m == null) { ring.Basis = "no-belief"; return ring; }
                foreach (var s in m.Candidates(Graph, Round, SectionsPerWindow)) strong.Add(s);
                ring.Basis = strong.Count > 0 ? "argmax" : "unseeded";
            }
            else
            {
                foreach (var idx in b.FigureIndexes())
                {
                    var m = b.MaskOf(idx);
                    if (m == null) continue;
                    foreach (var s in m.Candidates(Graph, Round, SectionsPerWindow)) strong.Add(s);
                }
                ring.Basis = strong.Count > 0 ? "anon-disjunctive" : "unseeded";
            }
            foreach (var s in strong) ring.Strong.Add(s);
            ring.Strong.Sort(StringComparer.Ordinal);
            if (ring.Strong.Count == 0) return ring;   // no base knowledge: honest empty ring
            var weak = new List<string>();
            var dist = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var s in Graph.Sections)
            {
                if (strong.Contains(s)) continue;
                int best = SectionGraph.NoKnowledge;
                foreach (var a in ring.Strong)
                {
                    int d = Graph.DistanceLowerBound(a, s);
                    if (d == SectionGraph.NoKnowledge) continue;
                    if (best == SectionGraph.NoKnowledge || d < best) best = d;
                }
                dist[s] = best;
                weak.Add(s);
            }
            weak.Sort((x, y) =>
            {
                int dx = dist[x], dy = dist[y];
                bool nx = dx == SectionGraph.NoKnowledge, ny = dy == SectionGraph.NoKnowledge;
                if (nx != ny) return nx ? 1 : -1;      // no-knowledge last, never "far"
                if (dx != dy) return dx < dy ? -1 : 1;
                return string.CompareOrdinal(x, y);
            });
            ring.Weak.AddRange(weak);
            return ring;
        }

        // ---- EXPLAIN evidence chain (S-C roundtrip law) ----------------------
        // One line per carried observation, coordinate-free by construction:
        // section GUIDs plus the app-authored public name in parentheses when
        // known. Loch-Gesetz: EXPLAIN shows EREIGNIS + REGEL, never a position
        // (the vector's coordinate-free Public() surface is untouched - this
        // trail is the belief rail's own disclosure, not the public vector).
        public List<string> ExplainTrail()
        {
            var lines = new List<string>();
            lines.Add("explain space round=" + Round + " speed=" + SectionsPerWindow +
                      " sections=" + Graph.Sections.Count);
            var groups = new List<string>(_beliefs.Keys);
            groups.Sort(StringComparer.Ordinal);
            foreach (var g in groups)
            {
                var b = _beliefs[g];
                lines.Add("explain group " + g + " alive=" + b.Alive + " fallen=" + b.Fallen +
                          " anonVictim=" + (b.AnonymousVictimObserved ? "yes" : "no") +
                          " woundedDisjunctive=" + (b.WoundedDisjunctiveObserved ? "yes" : "no"));
                foreach (var idx in b.FigureIndexes())
                {
                    var m = b.MaskOf(idx);
                    if (m == null) continue;
                    foreach (var v in m.ObservationViews())
                        lines.Add("explain obs group=" + g + " fig=" + idx + " round=" + v.Round +
                                  " src=" + v.Source + " rule=" + ExplainRule(v.Source, v.AlternativeCount) +
                                  " secs=" + JoinSecs(v.Sections));
                    var c = m.Candidates(Graph, Round, SectionsPerWindow);
                    bool degraded = m.DegradedToUnion;
                    lines.Add("explain mask group=" + g + " fig=" + idx + " cands=" + JoinSecs(c) +
                              (degraded ? " degraded=union" : "") +
                              (c.Count > 0 ? " dom=" + new Rational(1, c.Count).ToString() : ""));
                }
            }
            var heroKeys = new List<string>(_heroInSection.Keys);
            heroKeys.Sort(StringComparer.Ordinal);
            foreach (var h in heroKeys)
                lines.Add("explain hero " + h + " section=" + JoinSec(_heroInSection[h]) +
                          " round=" + _heroInSectionRound[h] + " rule=fenster-fakt");
            foreach (var t in _transits)
                lines.Add("explain transit hero=" + t.Hero + " door=" + t.Door +
                          " section=" + JoinSec(t.Section) +
                          " round=" + t.Round + " rule=transit-hypothese");
            return lines;
        }

        // Regel-Ids are stable identifiers (ui text mapping is S-D's job; the
        // trail itself carries the rule NAME so a Player.log line is readable
        // without any lookup).
        static string ExplainRule(string source, int alternativeCount)
        {
            if (source == "deploy") return alternativeCount == 1 ? "saat-exakt" : "saat-kandidatenmenge";
            if (source == "hit") return "eichung-hit-reground";
            if (source == "hit-group") return "eichung-anon-disjunktiv";
            if (source == "wounded-group") return "vergeltung-gruppen-disjunktiv";
            return source.Length == 0 ? "regel-unnamed" : "regel:" + source;
        }

        string JoinSecs(IEnumerable<string> secs)
        {
            var l = new List<string>();
            foreach (var s in secs) if (!string.IsNullOrEmpty(s)) l.Add(JoinSec(s));
            l.Sort(StringComparer.Ordinal);
            return l.Count == 0 ? "-" : string.Join(",", l.ToArray());
        }

        string JoinSec(string s)
        {
            var n = Graph.SectionName(s);
            return n.Length > 0 ? s + "(" + n + ")" : s;
        }

    // ---- deterministic dump (harness pin + EXPLAIN trail) --------------
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("round=").Append(Round)
              .Append(" speedSectionsPerWindow=").Append(SectionsPerWindow).Append('\n');
            sb.Append(Graph.Dump());
            foreach (var o in _speedObs)
                sb.Append("speed ").Append(o.Hero).Append(" round=").Append(o.Round)
                  .Append(" crossings=").Append(o.Crossings)
                  .Append(" touched=").Append(o.SectionsTouched).Append('\n');
            var groups = new List<string>(_beliefs.Keys);
            groups.Sort(StringComparer.Ordinal);
            foreach (var g in groups)
                sb.Append(_beliefs[g].Dump(Graph, Round, SectionsPerWindow));
            var heroKeys = new List<string>(_heroInSection.Keys);
            heroKeys.Sort(StringComparer.Ordinal);
            foreach (var h in heroKeys)
                sb.Append("hero ").Append(h).Append(" section=").Append(_heroInSection[h])
                  .Append(" round=").Append(_heroInSectionRound[h]).Append('\n');
            foreach (var f in _interactions)
                sb.Append("interact ").Append(f.Hero).Append(' ').Append(f.Entity)
                  .Append(" section=").Append(f.Section).Append(" round=").Append(f.Round).Append('\n');
            // S-C transit hypotheses: emitted ONLY when the track is
            // populated (door-owner knowledge present), so every pre-S-C
            // dump - the whole section_space.json canon - is unchanged.
            foreach (var t in _transits)
                sb.Append("transit ").Append(t.Hero).Append(' ').Append(t.Door)
                  .Append(" section=").Append(t.Section).Append(" round=").Append(t.Round).Append('\n');
            return sb.ToString();
        }

        // ---- incremental law -----------------------------------------------
        public SectionSpace Clone()
        {
            var s = new SectionSpace
            {
                Graph = Graph.Clone(),
                Round = Round,
                IsResync = IsResync,
                _imperialWindowGroup = _imperialWindowGroup
            };
            foreach (var kv in _heroInSection) s._heroInSection[kv.Key] = kv.Value;
            foreach (var kv in _heroInSectionRound) s._heroInSectionRound[kv.Key] = kv.Value;
            s._interactions.AddRange(_interactions);
            foreach (var kv in _activatedByRound) s._activatedByRound[kv.Key] = new List<string>(kv.Value);
            foreach (var kv in _spawnsByGroup)
            {
                var r = new SpawnRecord { Round = kv.Value.Round };
                r.Seeds.AddRange(kv.Value.Seeds);
                s._spawnsByGroup[kv.Key] = r;
            }
            foreach (var kv in _beliefs) s._beliefs[kv.Key] = kv.Value.Clone();
            s._speedObs.AddRange(_speedObs);
            s._transits.AddRange(_transits);
            s._opts = _opts.Copy();
            foreach (var kv in _windows)
            {
                var w = new HeroWindow { Round = kv.Value.Round };
                w.Sections.AddRange(kv.Value.Sections);
                w.Doors.AddRange(kv.Value.Doors);
                s._windows[kv.Key] = w;
            }
            return s;
        }
    }
}
