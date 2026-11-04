// S-D (card t_c52dd34b, concept positionierung v1 Paragraphen 8.1/10/12/13,
// PLAN_sektionsraum Revision IV.1): the CONSUMER layer over the S-B/S-C
// belief rail. SectionSpace/SectionMask/SectionTrust only PROVE facts and
// refuse to decide ("probes (facts only; S-D decides, this module never
// orders)"); every decision this file reads out is the card's law, pure C#,
// UnityEngine-free, C#7-safe, deterministic.
//
// THE NULL LAW (card Gesetze): every entry point here takes the belief rail
// (or a fact) that may be absent - null space, no belief, no anchor, no hit
// - and returns the INERT answer (null / empty map / unchanged posture). An
// absent fact never becomes a guess: the fallback ladder of concept 10 reads
// "key OFF = today's canon", and the consumers pass that law through
// unchanged. Canon neutrality is machine-checked: every pre-S-D caller keeps
// its old signature (new parameters are optional and null-defaulted), the
// canon fixtures run bit-identical, and section_consumers.json pins the
// inert pair (same stream, space=null -> today's pick; space on -> the
// exception fires in the SAME round).
//
// THE EXCEPTIONS FIRE AT EVERY RUNG (Thomas-Gesetz 04.11., PLAN head):
// "Die EREIGNISGETRIEBENEN AUSNAHMEN (Wachzone, Vergeltung, 3c) greifen auf
// ALLEN Skill-Stufen - Fakt-Reaktion ist Fairness, kein Intelligenz-Bonus."
// Baustein 1-5 consult no SectionSkill. The three skill-gated
// channels (Masken-VERFEINERUNG, Posture-Clamp, Supporter-Masken-Vertrauen) are
// gated WHERE THEY ARE CONSUMED: the Posture-Clamp gate sits in the bridge
// (SectionSkill.PostureClampBasisEnabled) because the clamp's BASIS is a
// refinement of the posture reading; the clamp law itself lives here,
// ungated, so a coarse projection can be clamped once the consumer opens
// the channel (S-C SectionSkill header law). The Baustein 6 SUPPORTER-
// MASKEN-VERTRAUEN gate is the documented exception INSIDE the entry point:
// its verdict is itself the aggression decision (strike vs. support), so
// the rung decides whether the decision exists at all - a low rung may not
// even hear a "support" verdict (S1/S2 = canon, byte-identical).
//
// Loch-Gesetz: every string this file produces names FIGURES, OBJECTS and
// app-authored section NAMES - never a coordinate, never a section number.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// The S-D Wachzone decision for ONE guard group: the mission-critical
    /// object in this guard's mask is being worked by a hero who is still
    /// factually there. Carries the Bedrohung value the order layer ranks
    /// multiple candidates with, and the EXPLAIN reason line
    /// (Ereignis -> Regel; the Entscheidung is appended by the consumer).
    public sealed class WachzoneDecision
    {
        public string Group;
        public string Hero;
        public string Section;
        public string SectionName;          // app-authored name, "" = unknown
        public int Round;                   // round of the interaction fact
        public Rational Threat;             // printed damage EV x alive
        public bool ThreatKnown;            // false = no catalog card (never better)
        public string Reason;               // names-only EXPLAIN fragment

        /// Deterministic vector row (harness pin): "WZ|group|hero|section|round|threat/threatKnown".
        public string Dump()
        {
            return "WZ|" + Group + "|" + Hero + "|" + Section + "|" + Round +
                   "|" + (ThreatKnown ? Threat.ToString() : "-");
        }
    }

    /// The S-D proximity feed for OrderPlanner (Baustein 4): the belief in
    /// the group's OWN section (trust-gated, never a precision lie), the
    /// sections carrying a FRESH hero-ring belief, and the section the
    /// doctrine currently contests. A group/zone absent from a map simply
    /// gets the pre-S-D behaviour (null Zone => no bonus, blocker law stays
    /// on today's null==null reading).
    public sealed class ZoneBelief
    {
        public readonly Dictionary<string, string> ZoneByGroup =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> RingZones = new List<string>();
        public string ContestZone;          // null = nothing contested

        public string ZoneOf(string group)
        {
            string z;
            return group != null && ZoneByGroup.TryGetValue(group, out z) ? z : null;
        }

        /// Deterministic dump (harness pin).
        public string Dump()
        {
            var sb = new List<string>();
            var keys = new List<string>(ZoneByGroup.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys) sb.Add("zone " + k + "=" + ZoneByGroup[k] + "\n");
            sb.Add("ring " + (RingZones.Count == 0 ? "-" : string.Join(",", RingZones.ToArray())) + "\n");
            sb.Add("contest " + (ContestZone ?? "-") + "\n");
            return string.Join("", sb.ToArray());
        }
    }

    /// The S-E supporter-target decision (Baustein 6, card t_9dd56737,
    /// concept positionierung v1 5/6 + D4 + Ziel-Matrix Z6 "Offizier
    /// supportet statt zu raten"): what the SUPPORTER's target choice says
    /// given its mask trust. Mode "strike" = fire into the trusted own
    /// section (the hero factually anchored there); Mode "support" = hold
    /// and support - NO target is named, never a guessed one. A null
    /// decision (returned by the entry point itself) = the channel is
    /// inert: canon runs bit-identical.
    public sealed class SupporterTrustDecision
    {
        public const string ModeStrike = "strike";
        public const string ModeSupport = "support";

        public string Mode;
        public string Hero;                 // strike only: hero in the dominant section
        public string Section;              // dominant section id, null when belief diffuse
        public string SectionName;          // app-authored name, "" = unknown
        public int Round;
        public string Reason;               // names-only EXPLAIN fragment (coord-free)

        /// Deterministic vector row (harness pin): "ST|mode|hero|section|round".
        public string Dump()
        {
            return "ST|" + Mode + "|" + (Hero ?? "-") + "|" + (Section ?? "-") + "|r" + Round;
        }
    }

    public static class SectionConsumers
    {
        // ---- Baustein 1: WACHZONE ------------------------------------------
        // Concept 10 (WACHZONEN-Schluessel) + 13.3: the positive fact is the
        // S-B probe (hero interaction attributed inside a mission-critical
        // section, crossed with the guard mask overlapping it AT the fact
        // round - seed counts from the start, "Figuren, die im Zielbereich
        // spawnen/deployen, haben das von der Saat an"). The consumer adds
        // two laws on top:
        //  * HELD law: the exception rides the fact as long as the hero's
        //    fact anchor still SITS in the object section. The moment a new
        //    interaction anchors him elsewhere the exception releases -
        //    state-based, jitter-free (a pure function of the projection),
        //    and data-honest: the app never "sees" a hero leave without an
        //    event, and invents no departure either.
        //  * BEDROHNUNG order (card): several masked guards over one working
        //    hero queue by the plan's own threat yardstick - printed damage
        //    EV x alive (OrderPlanner.WithEv). A group without a catalog
        //    record is ThreatKnown=false: unknown is never better.
        // Latest fact wins per group (round desc, then hero ordinal) - the
        // freshest duty call is the live one; ties are ordinal-deterministic.
        public static Dictionary<string, WachzoneDecision> WachzoneDecisions(
            SectionSpace space, IEnumerable<string> criticalSections,
            IEnumerable<GroupFacts> candidateFacts,
            Dictionary<string, DeploymentCard> catalog)
        {
            var map = new Dictionary<string, WachzoneDecision>(StringComparer.Ordinal);
            if (space == null || candidateFacts == null) return map;
            var factById = new Dictionary<string, GroupFacts>(StringComparer.Ordinal);
            var ids = new List<string>();
            foreach (var f in candidateFacts)
            {
                if (f == null || string.IsNullOrEmpty(f.GroupId)) continue;
                if (!factById.ContainsKey(f.GroupId)) { factById[f.GroupId] = f; ids.Add(f.GroupId); }
            }
            if (ids.Count == 0) return map;
            var facts = space.WachzoneFacts(criticalSections, ids);
            foreach (var wf in facts)
            {
                // HELD law: the working hero's latest fact anchor must still
                // be the object section (see the block comment).
                if (!string.Equals(space.HeroInSection(wf.Hero), wf.Section, StringComparison.Ordinal))
                    continue;
                WachzoneDecision best;
                if (map.TryGetValue(wf.GuardGroup, out best))
                {
                    if (wf.Round < best.Round) continue;
                    if (wf.Round == best.Round &&
                        string.CompareOrdinal(wf.Hero, best.Hero) >= 0) continue;
                }
                GroupFacts gf;
                if (!factById.TryGetValue(wf.GuardGroup, out gf)) continue;
                DeploymentCard card = null;
                // Cardless import: CardId is NULL by law (unknown identity) -
                // Dictionary.TryGetValue(null) would throw, so guard it.
                if (catalog != null && !string.IsNullOrEmpty(gf.CardId))
                    catalog.TryGetValue(gf.CardId, out card);
                var have = card != null;
                var secName = space.Graph.SectionName(wf.Section);
                if (string.IsNullOrEmpty(secName)) secName = wf.Section;
                // Loch-Gesetz: the line names the hero and the section's
                // APP-AUTHORED name; the bare section id is the identity
                // fallback, never a coordinate or a section number.
                var d = new WachzoneDecision
                {
                    Group = wf.GuardGroup,
                    Hero = wf.Hero,
                    Section = wf.Section,
                    SectionName = secName,
                    Round = wf.Round,
                    ThreatKnown = have,
                    Threat = have ? OrderPlanner.WithEv(gf, card).PrintedEvTotal : Rational.Zero,
                    Reason = "Wachzone: " + wf.Hero + " am Missionsobjekt in " + secName +
                             " (Runde " + wf.Round + ")",
                };
                map[wf.GuardGroup] = d;
            }
            return map;
        }

        /// The Rank-side key (concept 13.4 "setzt den Wachzonen-Key daneben"):
        /// the hero the activating guard's Wachzone decision points at.
        /// null = no decision = key off = canon rank law byte-identical.
        public static string WachzoneHeroFor(SectionSpace space,
                                            IEnumerable<string> criticalSections,
                                            GroupFacts activatingFact,
                                            Dictionary<string, DeploymentCard> catalog)
        {
            if (space == null || activatingFact == null) return null;
            var map = WachzoneDecisions(space, criticalSections,
                new List<GroupFacts> { activatingFact }, catalog);
            WachzoneDecision d;
            return map.TryGetValue(activatingFact.GroupId, out d) ? d.Hero : null;
        }

        // ---- Baustein 2: VERGELTUNG, mask track ----------------------------
        // Concept 10 "VERGELTUNG_Schluessel (C(Schuetze)): wie reactionHero -
        // Kandidat aus argmax(Maske ∩ Ring); unsicher -> Schluessel bleibt
        // aus" + 13.1 "FAKT (Fenster) > HYPOTHESE (Maske). Die Maske fuellt
        // genau die Loicke, die das Fenster laesst". Laws, in order:
        //  * the FACT track (R-D window attribution) already named a shooter
        //    => the mask key stays off entirely (never double-signal a round;
        //    the pinned slot law Interrupt > ... > Vergeltung keeps the
        //    compare honest even if a caller passed both).
        //  * trigger = the RAW hit fact on this group THIS round
        //    (SectionSpace.LastHitRound - recorded ungated, being shot at is
        //    public). No hit this round, no retaliation: suspicion alone
        //    never fires (concept 10, "nie ein Verdacht allein").
        //  * candidates = living heroes with a FRESH fact anchor (same round)
        //    inside the victim ring's STRONG set (= the argmax of the victim's
        //    own mask, group-level disjunctive - the wheel never names which
        //    figure was hit). Weak/no-knowledge sections never produce a
        //    shooter: the strong set is already edge-free knowledge, and a
        //    shooter hypothesis from the weak tail would be a guess.
        //  * exactly one candidate -> that hero. zero or several -> null
        //    (unsicher -> Schluessel aus). Never a guess.
        public static string VergeltungHeroFor(FeedWorldView view, SectionSpace space,
                                               string victimGroup, string reactionHero)
        {
            if (!string.IsNullOrEmpty(reactionHero)) return null;   // fact track wins
            if (view == null || space == null || string.IsNullOrEmpty(victimGroup))
                return null;
            if (space.LastHitRound(victimGroup) != space.Round || space.Round <= 0)
                return null;                                        // no hit fact this round
            if (space.BeliefOf(victimGroup) == null) return null;   // data-exists
            var ring = space.VictimRingOf(victimGroup, -1);         // group disjunctive
            if (ring == null || ring.Strong.Count == 0) return null;
            var strong = new HashSet<string>(ring.Strong, StringComparer.Ordinal);
            string found = null;
            foreach (var h in view.Heroes)
            {
                if (h == null || h.Defeated) continue;
                var anchor = space.HeroInSection(h.HeroId);
                if (string.IsNullOrEmpty(anchor) || !strong.Contains(anchor)) continue;
                if (space.HeroSectionRound(h.HeroId) != space.Round) continue;  // fresh only
                if (found != null
                    && !string.Equals(found, h.HeroId, StringComparison.Ordinal))
                    return null;                                    // ambiguous -> off
                found = h.HeroId;
            }
            return found;
        }

        // ---- Baustein 3: 3c (fresh section + intruder, same round) ---------
        // The S-B probe already enforces the three-leg coincidence (Section
        // activated in round R + a spawn whose SEED sits in that section in
        // the SAME round + a hero fact in that section in that round). The
        // consumer only narrows to the LIVE reading round (the exception is
        // a reaction in that same round, not a standing order) and to the
        // groups still pending (SelectNext hands pending facts).
        public static Dictionary<string, ThreeCFact> ThreeCPushes(SectionSpace space,
                                                                  IEnumerable<string> pendingIds,
                                                                  int roundNow)
        {
            var map = new Dictionary<string, ThreeCFact>(StringComparer.Ordinal);
            if (space == null || pendingIds == null) return map;
            var pending = new HashSet<string>(pendingIds, StringComparer.Ordinal);
            foreach (var f in space.ThreeCFacts())
            {
                if (f.Round != roundNow) continue;                  // same-round law
                if (!pending.Contains(f.Group)) continue;           // not-yet-activated
                ThreeCFact best;
                if (map.TryGetValue(f.Group, out best))
                {
                    if (string.CompareOrdinal(f.Hero, best.Hero) >= 0) continue;
                }
                map[f.Group] = f;
            }
            return map;
        }

        public static string ThreeCReason(SectionSpace space, ThreeCFact f)
        {
            var n = space.Graph.SectionName(f.Section);
            if (string.IsNullOrEmpty(n)) n = f.Section;   // identity fallback, no coordinate
            return "3c: " + f.Hero + " drang in die neu aktivierte Sektion " +
                   n + " ein (Runde " + f.Round + ")";
        }

        // ---- Baustein 4: the proximity feed (ringZones/contestZone) --------
        // Concept 13.6: "ringZones/contestZone = Objekt-Sektion + Ueberlapp"
        // on the OrderPlan law that has waited for this feeder since day one
        // (x1.25 hero-ring, x1.15 contested zone; R-E Geist checked: the R
        // chain NEVER fed them - git-log over PlanRound callers is empty).
        //  * zone(group)  = the group's OWN belief, TRUST-GATED (this is the
        //    decision slot S-C left open for the trust table): SectionTrust
        //    .Trusted over the per-figure masks; a trusted figure's uniform
        //    candidate set must be EXACTLY one section (uniform weights have
        //    no honest argmax above one candidate - diffuse belief, no
        //    position claim). Figures that disagree across sections give the
        //    GROUP no zone (a disjunctive is never a position claim).
        //    No threshold row => no zones (fail-closed like every trust key).
        //    Side effect documented (card R-E Geist note): with real zones
        //    the shipped blocker_before_attached_attack law finally sees
        //    real zones - a blocker attaches to the attacks in ITS section,
        //    groups without a trusted belief keep today's null==null reading.
        //  * ringZones    = sections carrying a FRESH hero-ring belief: a
        //    living hero's fact anchor dated at the reading round. Fresh
        //    only - the age of a stale anchor dilates the MASK track (its
        //    own law), it is not silently reused as a hero position.
        //  * contestZone  = the contested section: a critical section that
        //    currently holds a fresh hero anchor (the object is being worked
        //    NOW), else a 3c section live in the reading round, else null.
    //    Ties break ordinal-deterministic. Null sections never enter.
        public static ZoneBelief PlanZones(SectionSpace space, FeedWorldView view,
                                          SkillProfile profile)
        {
            var zb = new ZoneBelief();
            if (space == null) return null;
            var graph = space.Graph;
            int roundNow = space.Round;

            Rational threshold;
            bool trustReadable = SectionTrust.TryThresholdFor(profile, out threshold);
            if (trustReadable)
            {
                foreach (var g in space.BeliefGroups())
                {
                    var b = space.BeliefOf(g);
                    if (b == null || b.Alive <= 0) continue;       // wiped group: no claim
                    string zone = null;
                    bool split = false;
                    foreach (var idx in b.FigureIndexes())
                    {
                        var m = b.MaskOf(idx);
                        if (m == null) continue;
                        if (!SectionTrust.Trusted(m, graph, roundNow, space.SectionsPerWindow, profile))
                            continue;
                        var c = m.Candidates(graph, roundNow, space.SectionsPerWindow);
                        if (c.Count != 1) continue;                 // uniform set >1: no argmax
                        if (zone == null) zone = c[0];
                        else if (!string.Equals(zone, c[0], StringComparison.Ordinal)) { split = true; break; }
                    }
                    if (!split && zone != null) zb.ZoneByGroup[g] = zone;
                }
            }

            // fresh hero anchors (the hero-ring belief) + the object contest
            var fresh = new HashSet<string>(StringComparer.Ordinal);
            if (view != null)
                foreach (var h in view.Heroes)
                {
                    if (h == null || h.Defeated) continue;
                    var anchor = space.HeroInSection(h.HeroId);
                    if (string.IsNullOrEmpty(anchor)) continue;
                    if (space.HeroSectionRound(h.HeroId) != roundNow) continue;
                    fresh.Add(anchor);
                }
            foreach (var s in fresh) zb.RingZones.Add(s);
            zb.RingZones.Sort(StringComparer.Ordinal);

            // contestZone needs the critical set - it is a consumer input on
            // the decision side, so PlanZones derives the contest from what
            // it may see WITHOUT it: a 3c-live section (the probe already
            // crossed activation + intruder + fresh seed), else the first
            // fresh hero-anchor section in ordinal order is NOT used here -
            // without the critical table there is no mission-critical
            // object, hence no contest (data-exists, never a guess).
            string contest = null;
            foreach (var f in space.ThreeCFacts())
                if (f.Round == roundNow && string.CompareOrdinal(f.Section, contest ?? "") < 0
                    && !string.IsNullOrEmpty(f.Section))
                    contest = f.Section;
            // The bridge passes the critical set through the dedicated
            // overload; the 3c fallback covers the no-table degradation.
            zb.ContestZone = contest;
            return zb;
        }

        /// The bridge overload: same feed law with the mission's critical
        /// sections (from the shipped tactical_mission_critical table) -
        /// a fresh hero anchor INSIDE a critical section is the contested
        /// object section and outranks the 3c fallback.
        public static ZoneBelief PlanZones(SectionSpace space, FeedWorldView view,
                                          SkillProfile profile, IEnumerable<string> criticalSections)
        {
            var zb = PlanZones(space, view, profile);
            if (zb == null) return null;
            var crit = new HashSet<string>(criticalSections ?? new string[0], StringComparer.Ordinal);
            if (crit.Count > 0)
            {
                string contested = null;
                foreach (var s in zb.RingZones)
                    if (crit.Contains(s) && (contested == null || string.CompareOrdinal(s, contested) < 0))
                        contested = s;
                if (contested != null) zb.ContestZone = contested;
            }
            return zb;
        }

        // ---- Baustein 5: POSTURE-CLAMP (skill-gated at its consumer) -------
        // Concept 10 fallback ladder + 13.9: "Durchbruchs-Clamp als neuer
        // Eingang; Jitter=0-Regel; niedrigste Stufe bit-identisch". The
        // Durchbruch fact: a hero is factually WORKING at the mission-
        // critical object (fresh or held anchor inside a critical section -
        // same HELD law as the Wachzone) or 3c fired live this round. The
        // clamp only speaks while the posture is a RACE posture
        // (eliminate/attrition): protect/stall/deny already hold, flipping
        // them would be a lie with no effect. Race -> "protect": Wehr-
        // Doktrin statt Suchkampf an der Ziel-Sektion.
        //
        // JITTER LAW (machine-proved in section_consumers.json): the clamp
        // is a PURE function of the projection + posture - the same stream
        // re-pulled N times yields the same answer every pull (0 flips in
        // quiet rounds), a flip requires an event that moves an anchor or
        // fires 3c. There is no hidden statics here to oscillate.
        public const string ClampTarget = "protect";

        public static string PostureClamp(SectionSpace space,
                                          IEnumerable<string> criticalSections,
                                          string posture)
        {
            if (space == null || string.IsNullOrEmpty(posture)) return null;
            if (posture != "eliminate" && posture != "attrition") return null;
            var crit = new HashSet<string>(criticalSections ?? new string[0], StringComparer.Ordinal);
            if (crit.Count > 0)
                foreach (var hero in space.AnchoredHeroes())
                    if (crit.Contains(space.HeroInSection(hero))) return ClampTarget;
            foreach (var f in space.ThreeCFacts())
                if (f.Round == space.Round) return ClampTarget;
            return null;
        }

        // ---- Baustein 7: FOKUSZONE (Player-20-B, Thomas 04.11) -------------
        // The hunter brand (concept hunt-richtlinie B, "Go" 04.11): a hunter
        // goes after the hero WORKING INSIDE THE FOCUS ZONE - the focus zone
        // being the section(s) of the mission-critical object, the SAME fact
        // the Wachzone key consumes (criticalSections), never a guess.
        // Laws, in order:
        //  * SKILL GATE INSIDE THE ENTRY POINT (S3+, like the mask-trust
        //    channel's rung): S1/S2 read null and the popup stays
        //    bit-identical reference behaviour - the sloppy brawler stays
        //    the S1/S2 hunter.
        //  * FACTS ONLY: a hero counts when HeroInSection (latest fact
        //    anchor) lands inside a critical section. No critical section,
        //    no anchored hero -> null = canon rank law byte-identical.
        //  * NO NEW RANKING: the canon-rank order is scanned and the FIRST
        //    hero inside the zone takes the head - between several workers
        //    the unchanged threat ranking decides. Nothing is filtered,
        //    nobody is invented (permutation law).
        //  * HEAD ALREADY IN ZONE: the helper returns the head itself; the
        //    consumer treats "override == head" as no change (canon stands,
        //    no tail, no extra line).
        //  * PASS-BY LAW (consumer side): while the lock names a hero, the
        //    hunter attacks THAT hero; a hero outside the zone who merely
        //    stands on the path is not picked as target. Retaliation stays
        //    untouched: the reaction/vergeltung fact keys outrank this
        //    channel entirely (the caller only consults it while all fact
        //    keys are silent), so shooting the hunter hands its shot back.
        public static string FokuszoneHeadFor(SectionSpace space,
            IEnumerable<string> criticalSections, IList<string> rankedHeroes,
            int rung)
        {
            if (rung < 3) return null;
            if (space == null || rankedHeroes == null || rankedHeroes.Count == 0)
                return null;
            var crit = new HashSet<string>(criticalSections ?? new string[0],
                                           StringComparer.Ordinal);
            if (crit.Count == 0) return null;
            foreach (var hero in rankedHeroes)
            {
                var sec = space.HeroInSection(hero);
                if (sec != null && crit.Contains(sec)) return hero;
            }
            return null;
        }

        // ---- Baustein 6: SUPPORTER-MASKEN-VERTRAUEN (S-E, Z6) --------------
        // Concept positionierung v1 5/6 (OB-Zeile: "Supporter-Zielwahl
        // profitiert von der Maske (Feuer in die richtige Sektion statt
        // Bedrohungs-Schaetzung)"), 10 (MASKEN-VERTRAUEN -> Aggressivitaet:
        // enge Maske erlaubt Verfolgen, diffuse Maske -> halten/Support)
        // and the Ziel-Matrix Z6 ("Offizier supportet statt zu raten",
        // Support-Anteil bei diffuser Maske > bei enger Maske).
        //
        // The mask here is the SUPPORTER GROUP's own belief (Eigen-Glaube,
        // S-B/S-C: the per-figure masks track the group's OWN position).
        // The dominant section = the group's trust-gated zone read - the
        // EXACT S-D Baustein 4 law (SectionConsumers.PlanZones: threshold
        // row + SectionTrust.Trusted + uniform candidate set == one section
        // + figures agree), reused, no parallel law. "Dominante Sektion
        // >= Schwelle" is that read; anything weaker (diffuse candidates,
        // split figures, degraded union, no threshold row) is diffuse.
        //
        // THE LAW (card t_9dd56737, laws a-d):
        //  * SKILL GATE: the channel lives on the mask-VERFEINERUNG rung
        //    only - SectionSkill.SupporterMaskTrustEnabled (the renamed
        //    former dead hook, Datentabelle, kein Schalter). The gate is
        //    INSIDE the entry point on purpose: a "support" verdict spoken
        //    at a low rung would itself be an intelligence bonus (S1/S2
        //    must not change a byte of today's popup), so the rung decides
        //    whether the decision EXISTS at all (concept 13.4: "Skill-
        //    Stufen entscheiden, welcher Key ab wann feuert"). The bridge
        //    consumer passes the mission profile; S1/S2/no-profile reach
        //    this function and get null = canon.
        //  * FAKT SCHLAEGT MASKE: if the fact track already named a target
        //    (reactionHero = shot at me this round; wachzoneHero = hero
        //    factually at the mission-critical object; vergeltungHero =
        //    the mask-track shooter read already fired) the mask-trust
        //    track stays silent: null. Emergency stages are the same fact
        //    class - the caller only consults this channel while the O-C
        //    cascade reads None (bridge law, documented at the call site).
        //  * MASKE NULL: no space, no belief, belief never seeded, wiped
        //    group, no threshold row -> null = today's canon bit-identical
        //    (pinned; "lageblind" is the reference case, not the error).
        //  * DOMINANTE SEKTION: exactly one living hero with a FACT anchor
        //    DATED AT THE READING ROUND (fresh law, same arithmetic as the
        //    ringZones feed) inside the dominant section -> "strike" that
        //    hero: Feuer in die richtige Sektion. Zero or several ->
        //    "support": unsicher wird nicht erraten (the Vergeltung
        //    unique-or-off law verbatim). The section claim comes from the
        //    mask, the hero comes from a fact - naming a hero the mask
        //    alone would have to guess is not what the channel may do.
        //  * DIFFUSE MASK: "support" - der Offizier supportet, statt zu
        //    raten (Z6). This is a DECISION, disclosed with its why-line;
        //    it is never a silent key that could rot dead.
        public static SupporterTrustDecision SupporterTargetFor(
            SectionSpace space, FeedWorldView view, SkillProfile profile,
            string supporterGroup, string reactionHero,
            string wachzoneHero, string vergeltungHero)
        {
            if (!SectionSkill.SupporterMaskTrustEnabled(profile)) return null;
            if (!string.IsNullOrEmpty(reactionHero) || !string.IsNullOrEmpty(wachzoneHero)
                || !string.IsNullOrEmpty(vergeltungHero)) return null;   // FAKT > MASKE
            if (space == null || view == null || string.IsNullOrEmpty(supporterGroup))
                return null;
            var b = space.BeliefOf(supporterGroup);
            if (b == null || b.Alive <= 0) return null;                  // MASKE NULL
            bool seeded = false;
            foreach (var idx in b.FigureIndexes())
            {
                var m = b.MaskOf(idx);
                if (m != null && m.HasObservation) { seeded = true; break; }
            }
            if (!seeded) return null;                                    // MASKE NULL
            Rational threshold;
            if (!SectionTrust.TryThresholdFor(profile, out threshold))
                return null;                                    // no data -> fail-closed

            var dec = new SupporterTrustDecision { Round = space.Round };
            var zones = PlanZones(space, view, profile);        // the pinned S-D zone law
            var zone = zones != null ? zones.ZoneOf(supporterGroup) : null;
            if (zone == null)
            {
                dec.Mode = SupporterTrustDecision.ModeSupport;  // diffuse belief: no claim
                dec.Reason = "support: die eigene Lage ist ueber zu viele Sektionen " +
                             "verstreut (Runde " + space.Round + ")";
                return dec;
            }
            dec.Section = zone;
            var name = space.Graph.SectionName(zone);
            dec.SectionName = string.IsNullOrEmpty(name) ? zone : name;
            string found = null;
            int freshInside = 0;
            foreach (var h in view.Heroes)
            {
                if (h == null || h.Defeated) continue;
                var anchor = space.HeroInSection(h.HeroId);
                if (!string.Equals(anchor, zone, StringComparison.Ordinal)) continue;
                if (space.HeroSectionRound(h.HeroId) != space.Round) continue; // fresh only
                freshInside++;
                found = h.HeroId;
            }
            if (freshInside == 1)
            {
                dec.Mode = SupporterTrustDecision.ModeStrike;
                dec.Hero = found;
                dec.Reason = "strike: " + found + " steht faktisch in " + dec.SectionName +
                             " (Runde " + space.Round + ") - Feuer in die eigene Sektion";
            }
            else
            {
                dec.Mode = SupporterTrustDecision.ModeSupport;
                dec.Reason = "support: " + (freshInside == 0
                    ? "kein frischer Helden-Anker in " + dec.SectionName
                    : freshInside + " frische Helden-Anker in " + dec.SectionName +
                      ", keines ist sicher das Ziel")
                    + " (Runde " + space.Round + ")";
            }
            return dec;
        }
    }
}
