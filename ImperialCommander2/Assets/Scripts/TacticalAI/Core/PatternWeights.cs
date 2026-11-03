// M13u (Thomas 01.11., Variante c): the per-group doctrine dice. One rule
// engine for EVERY mission: each deployment group carries a printed-flavour
// weight distribution (tactical_pattern_weights.json, PUBLIC reviewable
// data - fairness rule, concept section 2), and the mission structure
// (posture "defend": a terminal or an escort target is in play) shifts it
// through DATA ROWS (postureModifiers), never through a code branch. The
// roll is per round, seeded (mission seed + card id + round): the same
// mission replays every draw, and EXPLAIN can disclose it.
//
// Layer order in the bridge (ClassifyPattern): printed movement law (Tripod)
// BEFORE the dice; the rolled pattern feeds the guard band and the persona
// multipliers unchanged. No asset / no matching row -> the legacy
// ClassifyPattern branch stays authoritative (bit-identical fallback).
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TacticalAI.Core
{
    public sealed class PatternWeightRow
    {
        /// Row authoring law: an id row hits exactly its card; an ids row
        /// hits any card of its pinned id list (Player-19 language-safe
        /// roster pin); a name row hits by regex; a row with neither is the
        /// ordered catch-all. Illegal regex never throws (it simply never
        /// matches).
        public string IdMatch;        // exact card id (optional; then the row is id-only)
        // Player-19 law (Thomas 03.11): nameMatch regexes carry ENGLISH
        // card names, but the live catalog hands the LOCALIZED name to the
        // matcher ("Rote Garde" never hits "Royal Guard"). The ids list
        // pins the shipped roster members of this role row language-safe:
        // group ids are identical in every language store (verified
        // En/De), display names are not. nameMatch stays as the import net.
        public List<string> Ids;      // exact card ids (optional; language-safe roster)
        public string NameMatch;      // case-insensitive regex over the internal name (optional)
        public Dictionary<string, int> Weights = new Dictionary<string, int>();

        public bool Matches(string cardId, string name)
        {
            if (!string.IsNullOrEmpty(IdMatch))
                return string.Equals(IdMatch, cardId, StringComparison.Ordinal);
            if (Ids != null)
            {
                // M6s-B8 note: an ids row is an ID VERDICT (B6 design) - a null
                // or non-pinned id is a miss, never a nameMatch fallback. The
                // role-anchor lookup (WeightsFor(cardId, name, role)) passes the
                // role AS cardId against exactly these rows.
                if (cardId == null) return false;
                for (int i = 0; i < Ids.Count; i++)
                    if (string.Equals(Ids[i], cardId, StringComparison.Ordinal)) return true;
                return false;
            }
            if (string.IsNullOrEmpty(NameMatch)) return true;
            try
            {
                return Regex.IsMatch(name ?? "", NameMatch, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException) { return false; }
        }
    }

    public sealed class PatternWeightTable
    {
        public List<PatternWeightRow> Rows = new List<PatternWeightRow>();
        public Dictionary<string, Dictionary<string, int>> PostureMods =
            new Dictionary<string, Dictionary<string, int>>();

        public bool IsActive { get { return Rows.Count > 0; } }

        /// First matching row wins (authored order = priority law, same as
        /// the persona effect order). Returns a FRESH copy - callers mutate
        /// it through Effective/Filter/Remove.
        public Dictionary<string, int> WeightsFor(string cardId, string name)
        {
            foreach (var r in Rows)
                if (r.Matches(cardId, name))
                    return new Dictionary<string, int>(r.Weights);
            return null;
        }

        /// M6s-B8 (Player-19 Ivan Talos, Thomas Go 03.11.): ROLE ANCHOR.
        /// A mission-authored custom figure (spawned by a CustomDeployment
        /// event, id = mission GUID) is not a pool card: its id and DE name
        /// match no row. The mission file itself says which printed card it
        /// REPRESENTS (thumbnailGroupImperial, machine-readable in all 39
        /// official custom deployments) - the app layer passes that id as
        /// role. A role is only ever resolved against rows that pin an
        /// explicit card id (a catch-all or name row is never a role), and
        /// a role that hits no row is not a verdict: the card's own
        /// id/name lookup still runs (exact old behavior). The role is the
        /// ROLE only - values, texts and state always stay the mission
        /// card's own printed data (Thomas' Repraesentation-is-nicht-
        /// Identitaets rule: a real DG006 group can sit on the same board).
        public Dictionary<string, int> WeightsFor(string cardId, string name, string role)
        {
            if (!string.IsNullOrEmpty(role))
                foreach (var r in Rows)
                    if ((!string.IsNullOrEmpty(r.IdMatch) || r.Ids != null) && r.Matches(role, name))
                        return new Dictionary<string, int>(r.Weights);
            return WeightsFor(cardId, name);
        }

        /// Posture shift (the data row that replaced the defend code
        /// branch): additive per pattern, clamped at 0, zero rows dropped,
        /// no renormalisation needed (the roll works on raw weights). A
        /// POSITIVE mod may open a pattern the row never carried (the
        /// defend case can put a guard on a pure-attack roster - authored
        /// 20.11 concept: "Wache +20" makes it possible, not just likelier).
        /// Unknown posture -> untouched copy.
        public Dictionary<string, int> Effective(Dictionary<string, int> dist, string posture)
        {
            var d = new Dictionary<string, int>(dist ?? new Dictionary<string, int>());
            Dictionary<string, int> mods;
            if (string.IsNullOrEmpty(posture) || !PostureMods.TryGetValue(posture, out mods))
                return d;
            foreach (var kv in mods)
            {
                int cur;
                d.TryGetValue(kv.Key, out cur);
                int v = cur + kv.Value;
                if (v <= 0) d.Remove(kv.Key);
                else d[kv.Key] = v;
            }
            return d;
        }

        /// Printed-law filter: patterns the card CANNOT play. Massive
        /// bases never chase (hunter); Tripod is gated before the dice.
        public static Dictionary<string, int> FilterIllegal(Dictionary<string, int> dist,
                                                            bool isMassive)
        {
            if (dist == null) return null;
            if (isMassive) dist.Remove("hunter");
            return dist;
        }

        /// The per-round draw. Key: Fnv1a64 over a public key, XOR the
        /// mission seed, 53-bit draw -- but the hash first passes the FMix64
        /// finalizer (MurmurHash3 final mix; M6t, 03.11., Player-18
        /// playtest). FNV-1a alone does not spread a change of the LAST key
        /// character (the round!) into the upper bits, and the draw reads
        /// only the top 53 bits -- without the finalizer the round salt was
        /// inert and every figure kept one pattern for the whole mission.
        /// Key format, the >>11 draw and the 53-bit division are unchanged.
        /// Deterministic pattern order (ordinal key sort) so the cumulative
        /// pick never depends on Dictionary enumeration order. Empty/zero
        /// distribution -> null (caller falls back to the legacy branch).
        public string Roll(Dictionary<string, int> dist, ulong missionSeed,
                           int roundNo, string cardId, out int drawPct)
        {
            drawPct = -1;
            if (dist == null || dist.Count == 0) return null;
            var keys = new List<string>(dist.Keys);
            keys.Sort(StringComparer.Ordinal);
            long total = 0;
            foreach (var k in keys) if (dist[k] > 0) total += dist[k];
            if (total <= 0) return null;
            var h = ImperialDecks.Fnv1a64("pw|" + (cardId ?? "") + "|r" + roundNo) ^ missionSeed;
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27; h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;
            var draw = (h >> 11) * (1.0 / 9007199254740992.0);   // [0,1), 53 bits
            double target = draw * total;
            double acc = 0;
            foreach (var k in keys)
            {
                if (dist[k] <= 0) continue;
                acc += dist[k];
                if (target < acc)
                {
                    drawPct = (int)(draw * 100);
                    return k;
                }
            }
            drawPct = (int)(draw * 100);
            return keys[keys.Count - 1];        // float guard: last pattern
        }
    }

    /// Asset seam (PrimeDoc/DocOrNull pattern of the tactical modules): the
    /// Unity side loads the TextAsset, Core parses, the harness pins against
    /// the SHIPPED asset.
    public static class PatternWeights
    {
        static Dictionary<string, object> _doc;
        static PatternWeightTable _table;

        public static void PrimeDoc(Dictionary<string, object> doc)
        {
            _doc = doc;
            _table = null;      // re-parse on next Load (settings reload)
        }
        public static Dictionary<string, object> DocOrNull() { return _doc; }

        public static PatternWeightTable Load()
        {
            if (_table != null) return _table;
            var t = new PatternWeightTable();
            if (_doc != null)
            {
                object modsObj;
                // postureModifiers is an object keyed by posture name
                if (_doc.TryGetValue("postureModifiers", out modsObj) &&
                    modsObj is Dictionary<string, object>)
                    foreach (var kv in (Dictionary<string, object>)modsObj)
                    {
                        var row = kv.Value as Dictionary<string, object>;
                        if (row == null) continue;
                        var d = new Dictionary<string, int>();
                        foreach (var e in row)
                        {
                            if (e.Value == null) continue;
                            d[e.Key] = (int)Json.AsLong(e.Value);
                        }
                        t.PostureMods[kv.Key] = d;
                    }
                object rowsObj;
                if (_doc.TryGetValue("rows", out rowsObj) && rowsObj != null)
                    foreach (var raw in Json.AsList(rowsObj))
                    {
                        var d = raw as Dictionary<string, object>;
                        if (d == null) continue;
                        var r = new PatternWeightRow();
                        object v;
                        if (d.TryGetValue("id", out v) && v != null) r.IdMatch = Json.AsStr(v);
                        // Player-19: language-safe roster pin (see
                        // PatternWeightRow) - exact card ids of this role row.
                        object idsv;
                        if (d.TryGetValue("ids", out idsv) && idsv != null)
                        {
                            r.Ids = new List<string>();
                            foreach (var raw2 in Json.AsList(idsv))
                            {
                                var s2 = Json.AsStr(raw2);
                                if (!string.IsNullOrEmpty(s2)) r.Ids.Add(s2);
                            }
                            if (r.Ids.Count == 0) r.Ids = null;
                        }
                        if (d.TryGetValue("nameMatch", out v) && v != null)
                            r.NameMatch = Json.AsStr(v);
                        if (d.TryGetValue("weights", out v) && v is Dictionary<string, object>)
                            foreach (var e in (Dictionary<string, object>)v)
                                if (e.Value != null && Patterns.IsPattern(e.Key))
                                    r.Weights[e.Key] = (int)Json.AsLong(e.Value);
                        if (r.Weights.Count > 0) t.Rows.Add(r);
                    }
            }
            _table = t;
            return _table;
        }
    }
}
