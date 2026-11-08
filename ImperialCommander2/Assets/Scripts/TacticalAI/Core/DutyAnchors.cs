// P37b (card t_1743a062, concept docs/CONCEPT_missions_duty.md §8 W3/W7,
// §9 documented assumption): the duty-line reader over the EXISTING
// MissionCritical channel. The mined duty rows (dutyAnchors in
// Languages/En/tactical_mission_critical.json, produced by the P37a fourth
// mechanical rule in tools/mission_critical_mine.py) are read here and ONLY
// here - same only-reader discipline as SectionTrust.DoorOwnersOf for the
// door rows of the very same artifact, and same fail-closed reading law:
// HYPOTHESIS NEVER WALL. A row that fails validation drops; a mission with
// no valid rows reads "no duty" and every consumer stays inert. The reader
// NEVER throws and NEVER touches Board cell validation: a duty target may
// legitimately be a NON-WALKABLE field (the escape-rim Highlight of HOTH10
// is the shipped example) - purpose-entrenching protectedEntities /
// Board.Load (which throws on non-standable cells) was explicitly rejected
// in §9; this is the OWN channel with OWN validation.
//
// Validation law (card-minimal, exactly three checks, no invented extras):
//   1. counter name (variable) non-empty        - else the DUTY row drops;
//   2. anchor name non-empty                    - else the ANCHOR drops;
//   3. anchor entity resolves against the caller's entity truth (GUID in
//      knownEntityGuids)                        - else the ANCHOR drops.
// A duty whose anchors all drop stays a duty with zero anchors (the CORE23
// variable-duty shape is the shipped proof that anchorless duties are
// legal); a duty with a missing threshold keeps threshold 0 and stays -
// threshold SEMANTICS (G2 quota k) is consumer law, not loader law.
// knownEntityGuids == null means "caller holds no entity truth": check 3
// is skipped (the authoring corpus is pinned data-exists by DutyMineGate
// against the shipped mission files; the runtime caller in Hop C passes the
// live EntityInteract set instead).
//
// Loch-Gesetz untouched: GUID + authored name + type only, no coordinates
// ever leave the reader. Tactical-mode-gated by construction (INVARIABLE
// META-LAW): nothing calls this from a legacy path - the duty consumers
// arrive in Hop C/D and must live behind TacticalMissionActive().
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public sealed class DutyAnchorTarget
    {
        public string Entity;         // entity GUID (identity, not a coordinate)
        public string Name;           // app-authored name
        public string EntityType;     // Token / Highlight / ...
    }

    public sealed class DutyAnchor
    {
        public string Variable;       // the duty counter name (mission variable)
        public long Threshold;        // printed threshold (0 = not authored)
        public string Law;            // mined setter channel (button|dgb|input-prompt|...)
        public List<DutyAnchorTarget> TargetFields = new List<DutyAnchorTarget>();
        public List<DutyAnchorTarget> MarkerTokens = new List<DutyAnchorTarget>();
        public int AnchorCount { get { return TargetFields.Count + MarkerTokens.Count; } }
    }

    public static class DutyAnchors
    {
        /// Duty rows of one mission (keyed by mission FILE, same match law
        /// as SectionTrust.MissionRow - OrdinalIgnoreCase over "file").
        /// Only class=="duty" yields rows; "borderline" (Text-QA list,
        /// concept §7) and "none" read empty. Deterministic order: authored
        /// duty order, authored anchor order within the duty.
        public static List<DutyAnchor> ForMission(object doc, string missionFile,
                                                  ICollection<string> knownEntityGuids = null)
        {
            var list = new List<DutyAnchor>();
            var md = MissionRow(doc, missionFile);
            if (md == null) return list;
            object daRaw;
            if (!md.TryGetValue("dutyAnchors", out daRaw)) return list;
            var da = daRaw as Dictionary<string, object>;
            if (da == null) return list;
            if (!string.Equals(Json.AsStr(Json.Get(da, "class", "")), "duty", StringComparison.Ordinal))
                return list;
            object dutiesRaw;
            if (!da.TryGetValue("duties", out dutiesRaw)) return list;
            var duties = dutiesRaw as List<object>;
            if (duties == null) return list;
            foreach (var d in duties)
            {
                var dd = d as Dictionary<string, object>;
                if (dd == null) continue;
                var variable = Json.AsStr(Json.Get(dd, "variable", "")) ?? "";
                if (variable.Length == 0) continue;       // validation 1: counter name
                var anchor = new DutyAnchor
                {
                    Variable = variable,
                    Threshold = Json.AsLong(Json.Get(dd, "threshold", 0L)),
                    Law = Json.AsStr(Json.Get(dd, "law", "")) ?? "",
                };
                ReadTargets(dd, "targetFields", anchor.TargetFields, knownEntityGuids);
                ReadTargets(dd, "markerTokens", anchor.MarkerTokens, knownEntityGuids);
                list.Add(anchor);
            }
            return list;
        }

        // Shared anchor reader for both roles. Drops (never throws): rows
        // that are not objects, anchors with an empty GUID, an empty name
        // (validation 2) or an unresolvable entity against a supplied
        // entity truth (validation 3). Non-empty GUID but unknown to the
        // caller's set is a GHOST (entity deleted after mining) - the anchor
        // drops, the mission keeps its remaining anchors.
        static void ReadTargets(Dictionary<string, object> dutyRow, string key,
                                List<DutyAnchorTarget> into,
                                ICollection<string> knownEntityGuids)
        {
            object raw;
            if (!dutyRow.TryGetValue(key, out raw)) return;
            var rows = raw as List<object>;
            if (rows == null) return;
            foreach (var t in rows)
            {
                var td = t as Dictionary<string, object>;
                if (td == null) continue;
                var guid = Json.AsStr(Json.Get(td, "entity", "")) ?? "";
                var name = Json.AsStr(Json.Get(td, "name", "")) ?? "";
                if (guid.Length == 0 || name.Length == 0) continue;
                if (knownEntityGuids != null && !knownEntityGuids.Contains(guid)) continue;
                into.Add(new DutyAnchorTarget
                {
                    Entity = guid,
                    Name = name,
                    EntityType = Json.AsStr(Json.Get(td, "entityType", "")) ?? "",
                });
            }
        }

        // Same row-match law as SectionTrust.MissionRow (OrdinalIgnoreCase
        // over the "file" column) - one artifact, one identity discipline.
        static Dictionary<string, object> MissionRow(object doc, string missionFile)
        {
            var d = doc as Dictionary<string, object>;
            if (d == null || string.IsNullOrEmpty(missionFile)) return null;
            object missionsRaw;
            if (!d.TryGetValue("missions", out missionsRaw)) return null;
            var missions = missionsRaw as List<object>;
            if (missions == null) return null;
            foreach (var m in missions)
            {
                var md = m as Dictionary<string, object>;
                if (md == null) continue;
                var file = Json.AsStr(Json.Get(md, "file", "")) ?? "";
                if (string.Equals(file, missionFile, StringComparison.OrdinalIgnoreCase)) return md;
            }
            return null;
        }
    }
}
