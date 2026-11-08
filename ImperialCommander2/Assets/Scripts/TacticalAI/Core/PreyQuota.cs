// P38 (card t_11ebb843, playtest Empire3): the OBJEKTJAGD-QUOTA — the
// seed-stable QUOTA-BAND for the units that chase a NEUTRAL bystander prey
// object (NeutralPrey scope) inside one round block. Same law family as the
// P37 Escort-Quota (docs/CONCEPT_missions_duty.md §9.2: "nie 0, nie alle"),
// reused as a SEAM (DutyQuota.SessionSeedOf + the house fmix64), never
// copied.
//
// The law (card §Mechanik 2, binding):
//   n     = the hunting units of this round block — every living figure the
//           Muster-Gatter puts on the chase (pattern hunter|aggressive, the
//           exact OBJEKT-Ziel pattern law). n = 0 => the quota law does not
//           exist at all (no prey round).
//   k     = 1 + (fmix64(Fnv1a64("preyq|r<round>") ^ SessionSeed) mod ceil(n/2))
//           => k in [1 .. ceil(n/2)] — never 0, never all.
//   Seed  = session key (campaign|mission) + round — CONSTANT within the
//           round, so the same selection is identical across every
//           activation of the round (QA Gleichlauf: no hunter switching
//           inside a round). A mid-round replan re-runs the pure law and
//           lands on the SAME k and selection.
//   Rank  = seed-ordered over the cohort with an ORDINAL ascending unit-id
//           tie net (card: "Rangfolge: aufsteigende Einheiten-Ids (stabil)"
//           — the same tie discipline as DutyQuota.Ranking; the seed decides
//           WHO, ids stabilise equal hashes).
//   The quota picks the first k of the ranking. Over-quota units get NO
//   OBJEKT-Ziel for bystander prey; they fall back to the EXISTING rule
//   (focus/reaction) — they do not stand still. Combat objects, doors,
//   terminals keep the untouched P36 law even for non-chosen units.
//
// Fairness line: the inputs are the session key (public save identity), the
// round, the figure ids and their ALREADY ROLLED patterns — nothing else.
// The Bedrohungs-Gatter is NOT here (it is a per-activation feed-fact read,
// PreyThreat below); the card's gate ORDER (Muster -> Neutral-Check ->
// Bedrohung -> Quota) keeps the quota's n independent of the threat facts.
//
// Meta-law: pure static function, reachable only from ActiveNow()-gated
// bridge code; tactical OFF never calls it (Null-Law, legacy bit-identical).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TacticalAI.Core
{
    /// One figure's round facts the prey quota may see (public/printed only).
    public sealed class PreyHunter
    {
        public string CardId;
        public string Pattern;      // the ALREADY ROLLED pattern (echoed, never re-rolled)
    }

    /// One round's decided prey-hunt split.
    public sealed class PreyRound
    {
        public int RoundNo;
        public int N;                                  // cohort size
        public int Cap;                                // ceil(n/2)
        public int K;                                  // chasers chosen this round
        public List<string> Ranking = new List<string>();   // seed order over the cohort
        public List<string> Chosen = new List<string>();    // the first K of the ranking
        public string Explain = "";                    // ASCII disclosure (raw debug channel)

        /// Did the seed pick this unit for the bystander chase this round?
        public bool IsChosen(string cardId) =>
            cardId != null && Chosen.Contains(cardId);

        /// Is this unit inside the Muster cohort at all? (Disclosure duty:
        /// every cohort member's popup carries the quota line.)
        public bool InCohort(string cardId) =>
            cardId != null && Ranking.Contains(cardId);
    }

    public static class PreyQuota
    {
        /// The two chase patterns — the EXACT Muster-Gatter of the OBJEKT-Ziel
        /// law (TryObjectTarget), not a new list.
        public static bool IsChasePattern(string pattern) =>
            pattern == "hunter" || pattern == "aggressive";

        // The house fmix64 is DutyQuota's (P37c) — reached through the
        // shared SessionSeedOf seam below; the quota draw mirrors the duty
        // draw with its OWN domain salt so the two dice stay independent.
        static ulong RankHash(ulong seed, string cardId, int roundNo) =>
            FMix64(ImperialDecks.Fnv1a64("preyrank|" + cardId + "|r" + roundNo) ^ seed);

        static ulong FMix64(ulong h)
        {
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27; h *= 0x94D049BB133111EBUL;
            return h ^ h >> 31;
        }

        /// The per-round quota (bridge feeds this once per EnsureRound).
        /// Returns NULL when the law has nothing to decide: no cohort
        /// (n == 0 — "bei n = 0 entfällt die Quota vollständig") or no
        /// mission in scope (the caller only calls it for in-scope missions).
        public static PreyRound Compute(string sessionKey, int roundNo,
                                        IReadOnlyList<PreyHunter> hunters)
        {
            var hs = (hunters ?? new List<PreyHunter>())
                .Where(h => h != null && !string.IsNullOrEmpty(h.CardId)
                            && IsChasePattern(h.Pattern))
                .ToList();
            int n = hs.Count;
            if (n == 0) return null;

            var r = new PreyRound { RoundNo = roundNo, N = n, Cap = (n + 1) / 2 };
            var seed = DutyQuota.SessionSeedOf(sessionKey);   // shared P37 seam
            r.Ranking = hs
                .OrderBy(h => RankHash(seed, h.CardId, roundNo))
                .ThenBy(h => h.CardId, StringComparer.Ordinal)
                .Select(h => h.CardId)
                .ToList();
            int k = 1 + (int)(FMix64(
                ImperialDecks.Fnv1a64("preyq|r" + roundNo.ToString(CultureInfo.InvariantCulture)) ^ seed)
                % (ulong)r.Cap);
            r.K = k;
            r.Chosen = r.Ranking.Take(k).ToList();
            r.Explain = "prey r" + r.RoundNo
                        + " n=" + n + " cap=" + r.Cap + " k=" + k
                        + " ranking=" + string.Join(",", r.Ranking)
                        + " chosen=" + string.Join(",", r.Chosen);
            return r;
        }
    }

    /// P38 Bedrohungs-Gatter (card §Mechanik 1): the chase of a neutral prey
    /// object by one unit is SUPPRESSED while a rebel figure is OBSERVED in
    /// the same section as the unit OR in the same section as the object.
    /// Foundation = the SAME feed facts the focus rule uses for observed
    /// heroes (SectionSpace.HeroInSection — latest fact anchor); nothing new
    /// is surveyed, no half-knowledge is invented. Deterministic, pure.
    public static class PreyThreat
    {
        /// Returns the threatened section (unit section first — the card's
        /// enumeration order), or null when no observed rebel threatens.
        /// Unknown sections ("" = no fact) can never threaten — never a
        /// guess.
        public static string ThreatSectionOf(SectionSpace space, string unitSection,
                                             string objectSection)
        {
            if (space == null) return null;
            var anchored = space.AnchoredHeroes();
            if (anchored.Count == 0) return null;
            if (!string.IsNullOrEmpty(unitSection))
                foreach (var h in anchored)
                    if (string.Equals(space.HeroInSection(h), unitSection, StringComparison.Ordinal))
                        return unitSection;
            if (!string.IsNullOrEmpty(objectSection))
                foreach (var h in anchored)
                    if (string.Equals(space.HeroInSection(h), objectSection, StringComparison.Ordinal))
                        return objectSection;
            return null;
        }
    }
}
