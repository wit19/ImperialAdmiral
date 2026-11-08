// P37c (card t_e80246ed, concept docs/CONCEPT_missions_duty.md §1b G1-G4 +
// §9.2): the Escort-Quota — a per-round, seed-stable duty split of the
// imperial roster. Law (Rev 3.3 decisions, all three binding):
//
//   G2  "Escorts sind modifizierte Muster, nicht ein Extra-Würfel": the
//       escort CANDIDATES come from the dutyModifiers matrix (which rolled
//       patterns ride the duty branch). The quota only CAPS them
//       (k <= ceil(n/2)) and only FLOORS them (k >= 1 whenever a duty
//       exists and at least one mobile group exists — §9.2: ruhende Runden
//       sind verboten, "nie 0, nie alle"). Over-cap escorts are demoted
//       back to the combat branch by the seed-ordered ranking (§1b).
//   G1  the duty branch lives in the MOVEMENT branch only: this class
//       decides a marker (DutyOf ∈ {none, escort}) + the anchor target name
//       — it never reads the board, never touches an attack slot, never
//       re-rolls or rewrites a pattern (pattern is an INPUT, echoed back).
//   Fairness line: inputs are the session key (public save identity, the
//       same material SessionSeed hashes), the round, the group ids + their
//       ALREADY ROLLED patterns + the printed mobility flag, the
//       PatternWeightTable dutyModifiers data row and the anchor NAMES the
//       DutyAnchors reader parsed (Loch-Gesetz: name + type, never a
//       coordinate). No board knowledge is possible by construction.
//   Seeded-draw law (Seeded-draw-Gesetz): Fnv1a64 of an END-THREADED key
//       (round inside the string, never a post-mix end salt) XOR the mission
//       seed, THEN the Murmur3/splitmix fmix64 finalizer, and only the
//       mixed value is used for modulo/ranking (the end-salted top-bit draw
//       is dead — same house law as PatternWeights.Roll).
//   Meta-law: pure static function of its inputs — reachable only from
//       ActiveNow()-gated bridge code; tactical OFF never calls it, so the
//       legacy behaviour is bit-identical (Null-Law).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TacticalAI.Core
{
    /// One group's round facts the quota may see (public/printed only).
    public sealed class DutyGroup
    {
        public string CardId;
        public string Pattern;        // the ALREADY ROLLED pattern (echoed, never re-rolled)
        public bool Mobile = true;    // printed Stativ/Tripod law: immobile groups never escort
    }

    /// One round's decided duty split (the Core-Marker source for the plan).
    public sealed class DutyRound
    {
        public int RoundNo;
        public int K;                                  // escorts chosen this round
        public int Cap;                                // ceil(n/2) — never-all ceiling
        public string Mode = DutyQuota.ModeNone;       // none | degradiert | matrix | keinDuty
        public List<string> Ranking = new List<string>();    // seed order, ALL groups (quota tie net)
        public List<string> Escorts = new List<string>();    // the cohort (K entries, possibly demoted)
        public string TargetName = "";                 // anchor NAME (button/target-field/marker token)
        public string Explain = "";                    // ASCII disclosure line (k, ranking, seed-readable)

        /// Core-Marker value for one group: "escort" | "none" (G1: marker only).
        public string DutyOf(string cardId) =>
            cardId != null && Escorts.Contains(cardId) ? DutyQuota.DutyEscort : DutyQuota.DutyNone;

        /// Groups that stay in the (untouched) combat branch.
        public List<string> Combat()
        {
            var outp = new List<string>();
            foreach (var id in Ranking) if (!Escorts.Contains(id)) outp.Add(id);
            return outp;
        }
    }

    public static class DutyQuota
    {
        public const string DutyNone = "none";
        public const string DutyEscort = "escort";
        public const string ModeNone = "keinDuty";       // no duty fact exists for this mission
        public const string ModeDegraded = "degradiert"; // shipped asset has no dutyModifiers key
        public const string ModeMatrix = "matrix";       // matrix decides the candidates

        // Murmur3-style finalizer (the house fmix64 — same constants as
        // PatternWeights.Roll / World.DeterministicRandom.Next64).
        static ulong FMix64(ulong h)
        {
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27; h *= 0x94D049BB133111EBUL;
            return h ^ h >> 31;
        }

        /// Seed derivation (shared by the bridge and the harness — the ONE
        /// way to salt the quota): Fnv1a64 over the session-key string
        /// (campaign|mission, the WorldEvents session line), the same
        /// 0-guard as the bridge SessionSeed.
        public static ulong SessionSeedOf(string sessionKey)
        {
            var h = ImperialDecks.Fnv1a64(sessionKey ?? "solo");
            return h == 0 ? 0xA5A5A5A5A5A5A5A5UL : h;
        }

        /// A dutyModifiers row is POSITIVE (pattern rides the duty) when its
        /// integer sum is > 0 (sum == 0 or negative shadows the pattern out).
        public static bool RowPositive(Dictionary<string, int> row)
        {
            if (row == null) return false;
            int sum = 0;
            foreach (var kv in row) sum += kv.Value;
            return sum > 0;
        }

        static ulong RankHash(ulong seed, string cardId, int roundNo) =>
            FMix64(ImperialDecks.Fnv1a64("dutyrank|" + cardId + "|r" + roundNo) ^ seed);

        /// The per-round quota (bridge feeds this once per EnsureRound).
        ///   sessionKey   campaign|mission identity (SessionKey())
        ///   roundNo      the live round number
        ///   groups       living imperial groups with ROLLED patterns + mobility
        ///   table        PatternWeightTable (null = no asset = degraded path)
        ///   targetName   anchor name ("" when none) — echoed to the marker
        ///   hasDuty      the mission has a parsed duty fact (DutyAnchors)
        public static DutyRound Compute(string sessionKey, int roundNo,
                                        IReadOnlyList<DutyGroup> groups,
                                        PatternWeightTable table,
                                        string targetName, bool hasDuty)
        {
            var r = new DutyRound
            {
                RoundNo = roundNo,
                TargetName = targetName ?? "",
            };
            var gs = (groups ?? new List<DutyGroup>())
                .Where(g => g != null && !string.IsNullOrEmpty(g.CardId))
                .ToList();
            int n = gs.Count;
            // Cap over ALL groups ("Rangfolge aller imperialen Gruppen");
            // K in [1..cap] is enforced only when a duty + a mobile group
            // exist — the G2 floors are never invented from an empty roster.
            r.Cap = (n + 1) / 2;

            // Ranking: ALL groups, seed-ordered (fmix over an end-threaded
            // key — the Seeded-draw law), Ordinal tiebreak on the card id.
            var seed = SessionSeedOf(sessionKey);
            r.Ranking = gs
                .OrderBy(g => RankHash(seed, g.CardId, roundNo))
                .ThenBy(g => g.CardId, StringComparer.Ordinal)
                .Select(g => g.CardId)
                .ToList();

            if (!hasDuty || n == 0)
            {
                r.Mode = ModeNone;
                r.K = 0;
                r.Explain = ExplainLine(r, roundNo, gs, 0, 0);
                return r;
            }

            // The quota draw (Quota-Wuerfel): seed-stable, k in [1..cap].
            // The draw bounds the COHORT SIZE; the G2 candidate law below
            // decides WHO may ride. cap >= 1 here (n >= 1).
            int kDrawn = 1 + (int)(FMix64(
                ImperialDecks.Fnv1a64("dutyq|r" + roundNo) ^ seed) % (ulong)r.Cap);

            // Candidates: the modifizierte Muster (G2). Degraded (no
            // dutyModifiers key): BASIS-GEWICHTE + Quota-Rangfolge — every
            // mobile group is a candidate and the seed ranking picks the
            // quota (this is the shipped-asset path, clean degradation).
            bool matrix = table != null && table.DutyMods != null && table.DutyMods.Count > 0;
            r.Mode = matrix ? ModeMatrix : ModeDegraded;
            var mobile = gs.Where(g => g.Mobile).ToList();
            List<DutyGroup> cands = matrix
                ? mobile.Where(g => RowPositive(table.DutyMods.TryGetValue(g.Pattern ?? "", out var row) ? row : null))
                        .ToList()
                : mobile;
            r.Escorts = r.Ranking.Where(id => cands.Any(c => c.CardId == id))
                                 .Take(kDrawn).ToList();
            // §9.2 floor: a duty exists and a mobile group exists => k >= 1.
            if (r.Escorts.Count == 0 && mobile.Count > 0)
                r.Escorts.Add(r.Ranking.First(id => mobile.Any(c => c.CardId == id)));
            r.K = r.Escorts.Count;
            r.Explain = ExplainLine(r, roundNo, gs, kDrawn, cands.Count);
            return r;
        }

        // ASCII disclosure (ZIEL 4): k, full ranking, mode, candidate size —
        // seed-ablesbar: every number is reproducible from sessionKey+round
        // with the law above. Raw debug channel (Player-22 law), no game text.
        static string ExplainLine(DutyRound r, int roundNo,
                                  IReadOnlyList<DutyGroup> gs, int q, int candCount)
        {
            var pat = string.Join(",", gs.Select(g => g.CardId + ":" + (g.Pattern ?? "?")));
            return "duty r" + roundNo.ToString(CultureInfo.InvariantCulture)
                   + " mode=" + r.Mode
                   + " k=" + r.K + "/" + r.Cap
                   + " q=" + q + " cand=" + candCount
                   + " ranking=" + (r.Ranking.Count > 0 ? string.Join(",", r.Ranking) : "-")
                   + " escorts=" + (r.Escorts.Count > 0 ? string.Join(",", r.Escorts) : "-")
                   + " patterns=" + (pat.Length > 0 ? pat : "-");
        }
    }
}
