// M6t section (Thomas Go 03.11., Player-18 playtest bug): avalanche test for
// the per-round doctrine dice. FNV-1a alone does not spread a change of the
// LAST key character (the round!) into the upper bits, and Roll draws the
// top 53 bits - so the pre-fix round salt was inert: every figure kept one
// pattern for the whole mission (measured 0/100 differing draws). The FMix64
// finalizer in Roll is pinned here as the fix of the bug CLASS: over 100
// constructed key pairs that differ ONLY in the final character, the number
// of pairs whose draw bucket changes is pinned exactly, and a standing law
// (>= 90/100) keeps the class dead even if the hash is ever swapped again.
// Hand-pinned fixture pattern_avalanche.json (twin-computed expect block,
// harness cross-check) - the pattern-level number (61) is the categorical
// ceiling of a 55/10/25/10 four-way weighted draw, NOT a bug.
using System;
using System.Collections.Generic;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecPatternAvalanche()
        {
            var v = LoadJson("pattern_avalanche.json");
            PatternWeights.PrimeDoc(Json.AsDict(LoadLangJson("En", "tactical_pattern_weights.json")));
            var t = PatternWeights.Load();
            if (!t.IsActive) throw new InvalidOperationException("pattern-weight asset empty");

            var id = Json.AsStr(v["card_id"]);
            var posture = Json.AsStr(v["posture"]);
            var seed = (ulong)Json.AsLong(v["seed"]);
            var wantPairs = Json.AsLong(v["pairs"]);

            var rec = ShippedCardRecords().Find(r => Json.AsStr(r["id"]) == id);
            if (rec == null) throw new InvalidOperationException("avalanche card missing: " + id);
            var dist = t.Effective(t.WeightsFor(id, Json.AsStr(rec["name"])), posture);

            // Pair construction: consecutive (2k-1, 2k) rounds, KEPT only when
            // the two salt keys change exactly ONE character at the end
            // ("r9"/"r10" and friends change length, not just the tail).
            var pairs = new List<int[]>();
            for (int k = 1; pairs.Count < wantPairs; k++)
            {
                int a = 2 * k - 1, b = 2 * k;
                var ka = "r" + a;
                var kb = "r" + b;
                if (ka.Length != kb.Length) continue;
                if (ka.Substring(0, ka.Length - 1) != kb.Substring(0, kb.Length - 1)) continue;
                if (ka[ka.Length - 1] == kb[kb.Length - 1]) continue;
                pairs.Add(new[] { a, b });
                if (k > 100000) throw new InvalidOperationException("avalanche pairs do not converge");
            }
            Check("avalanche_pair_count", wantPairs, (long)pairs.Count);

            long changedDraws = 0, changedPatterns = 0;
            foreach (var pr in pairs)
            {
                int pctA, pctB;
                var patA = t.Roll(dist, seed, pr[0], id, out pctA);
                var patB = t.Roll(dist, seed, pr[1], id, out pctB);
                if (pctA != pctB) changedDraws++;
                if (patA != patB) changedPatterns++;
            }

            // exact canon pins (FMix64 + this seed; any mix change re-bases)
            Check("avalanche_changed_draws", Json.AsLong(v["expect_changed_draws"]), changedDraws);
            Check("avalanche_changed_patterns",
                  Json.AsLong(v["expect_changed_patterns"]), changedPatterns);
            // the standing law: >= 90/100 pairs MUST move
            Check("avalanche_min_law", true, changedDraws >= Json.AsLong(v["min_changed_draws"]));
        }
    }
}
