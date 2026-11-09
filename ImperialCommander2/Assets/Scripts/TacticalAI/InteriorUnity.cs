// P41c (card t_a2e54ec9; docs/CONCEPT_massive_interior_v1.md v1.3 9.2, 4-D6):
// the Unity-side BUILDER of the per-mission interior snapshot the Massive law
// consumes. Core/InteriorLaw.cs (P41b) holds the LAW; this file assembles the
// FACTS of the loaded mission into one immutable snapshot:
//   tiles      - Resources.FindObjectsOfTypeAll<Saga.TileRenderer>() reading
//                .mapTile. MapTile is a PLAIN class (MapTile.cs:6, IMapEntity),
//                NOT a UnityEngine.Object - FindObjectsOfTypeAll<MapTile> would
//                not compile; TileRenderer is the MonoBehaviour carrying it
//                (TileRenderer.cs:8,14). Same MedPac pattern as the M13r
//                TooltipHandler scan (SagaController.cs:1018ff).
//   sections   - tile -> mapSectionOwner grouping; every section's four-state
//                derives through InteriorLaw.SectionStatus (D1/D3-B view).
//   DP -> tile - the planner space-cell mapping MIRROR (spec 9.2): the tile
//                rect is the Unity corner-anchor AABB of the authored
//                entityPosition, pivot at the corner, rotated about the pivot,
//                dims from TileDescriptor * PX_PER_HEX (the same px/hex
//                constant as tools/section_geometry_census.py and
//                tools/massive_interior_census.py - all three engines parse
//                one mapping law); the DP point resolves inclusive-inside.
//                Tiebreaks are the census law 1:1: strict interior, then the
//                DP's own mapSectionOwner, then a same-status pool (identity
//                stays ambiguous, the STATUS is what D3-B needs); mixed-status
//                survivors and zero-rect DPs read AMBIGUOUS - reported, never
//                silently defaulted (spec 9.6: a tileless DP is an Ambiguous
//                DP, and the ban can never fire on it).
//   overrides  - D6 v1.3, FAIL-CLOSED: the MISSION'S OWN authored lanes
//                (fresh-look review MAJOR-1/2, 09.11): deployLedger rows are
//                campaign-scoped and survive mission changes (ParseMission
//                rewrites only the current mission's start groups), so a stale
//                row would lift a ban in a mission that never authored the
//                indoor DP, and the event lane only reaches the ledger at
//                event-DEPLOYMENT time (DeploymentManager.cs:474) - after this
//                builder ran. Spec 9.2 names the ledger; spec 4-D6 states the
//                LAW: "At mission start, for every group the mission authored
//                ... register override" - the mission file's own pointList IS
//                the authored truth (4-D6 words it so), and it is what the
//                census (tools/massive_interior_census.py) and the gate probe
//                (InteriorDpProbe) pin. So the builder reads the authored
//                lanes from the mission's own JSON (session.missionStringified
//                - the exact string ParseMission consumes at every load site),
//                mirroring InteriorDpProbe 353-425 1:1: start groups + event
//                lanes eventActionType 6/7/21 with deploymentPoint==1, the
//                sentinel/keyword rules identical. Mission-scoped by
//                construction (no stale lift), event lanes present at t=0
//                (fresh == restored == replay, D5). The authored DP resolves
//                through MapEntityManager.GetEntity(guid) to its tile, the
//                card must pass the runtime Massive view (spec 9.2 "whose
//                card IsMassive"), and ONLY a VERIFIED-BANNED read (RR pin or
//                a signed row - an unsigned row can never read Banned, the
//                law guarantees it) registers the lift. Anything unverified
//                registers NOTHING and logs '[interior] override_unverified' -
//                an override lifts the ban EVERYWHERE, so it must never ride
//                an unverified row (v1.3 finding 4).
// Empty index (0 tiles or 0 DPs) => IsEmpty snapshot => the consumers run the
// NULL-LAW (behaviour identical to pre-P41), and exactly one log line says so.
// The snapshot holds NO persistence (D5 - derived, rebuilt per mission id).
using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalAI.Core;

namespace TacticalAI
{
    /// <summary>The immutable interior facts of ONE loaded mission: DP -> tile
    /// status, section statuses, and the D6 override set (card x mission -
    /// this instance IS the mission scope). Keyed by lowercase invariant GUID
    /// strings (DpKey) so ledger rows, runtime entities and forensic lines
    /// share one identity lane.</summary>
    public sealed class MissionInteriorSnapshot
    {
        public sealed class DpEntry
        {
            public TileStatus Status;
            public string TileRef;      // forensic 'Core19A' ('-' when unresolved)
            public string Label;        // the placed biome label ('None' = unknown)
            public string SectionGuid;  // the DP's own mapSectionOwner (lowercase)
            public string Kind;         // single/strict/section/status_uniform/ambiguous_mixed/tileless
        }

        readonly Dictionary<string, DpEntry> _dps;
        readonly Dictionary<string, SectionStatusT> _sections;

        internal MissionInteriorSnapshot(
            Dictionary<string, DpEntry> dps,
            Dictionary<string, SectionStatusT> sections,
            InteriorLaw.OverrideSet overrides,
            int tileCount)
        {
            _dps = dps;
            _sections = sections;
            Overrides = overrides;
            TileCount = tileCount;
            DpCount = dps.Count;
            // Null-Law (spec 9.2): an empty index is NOT a law event - the
            // consumers keep their pre-P41 behaviour, bit for bit.
            IsEmpty = tileCount == 0 || dps.Count == 0;
        }

        /// <summary>The D6 override set. Keyed by the deploy-ledger identity
        /// (the card.id the ledger row lives under) - the language lane is
        /// SetCardTranslations-owned (DG013 law) and elite cards share the
        /// plain card's name, so the IDENTITY lane is the only collision-free
        /// card x mission key at the runtime seam.</summary>
        public InteriorLaw.OverrideSet Overrides { get; private set; }

        public int TileCount { get; private set; }
        public int DpCount { get; private set; }
        public bool IsEmpty { get; private set; }

        /// <summary>The law read of the DP's own tile. Unknown DP (not in the
        /// index - e.g. an entity spawned after the build) reads Ambiguous:
        /// fail-open like every unverified row, never a silent Banned.</summary>
        public TileStatus StatusForDp(string dpGuidKey)
        {
            DpEntry e;
            return dpGuidKey != null && _dps.TryGetValue(dpGuidKey, out e)
                ? e.Status : TileStatus.Ambiguous;
        }

        public DpEntry Find(string dpGuidKey)
        {
            DpEntry e;
            return dpGuidKey != null && _dps.TryGetValue(dpGuidKey, out e) ? e : null;
        }

        public SectionStatusT SectionStatusOf(string sectionGuidKey)
        {
            SectionStatusT s;
            return sectionGuidKey != null && _sections.TryGetValue(sectionGuidKey, out s)
                ? s : SectionStatusT.Ambiguous;
        }
    }

    public static class InteriorUnity
    {
        // The planner space-cell constant (section_geometry_census.py /
        // massive_interior_census.py PX_PER_HEX): authored entityPosition is
        // in ICE editor units, 10 units per hex, dims.width/height in hexes.
        public const double PxPerHex = 10.0;
        // Inclusive-boundary epsilon - the census tiebreak epsilon, same
        // reading in both engines.
        const double Eps = 1e-6;

        /// <summary>One identity lane for every DP key (ledger rows, entity
        /// GUIDs, forensic lines, the bridge filter lookup).</summary>
        public static string DpKey(Guid guid)
        {
            return guid.ToString().ToLowerInvariant();
        }

        struct TileRect
        {
            public double X0, X1, Y0, Y1;
            public string SectionGuid;   // lower
            public TileStatus Status;
            public string Ref;           // 'Core19A'
            public string Label;
        }

        /// <summary>Corner-anchor AABB rotated about the authored pivot
        /// (census rect_of, 1:1): rot 0 [x,x+w]x[y,y+h]; 90 [x-h,x]x[y,y+w];
        /// 180 [x-w,x]x[y-h,y]; 270 [x,x+h]x[y-w,y]. Returns false for any
        /// other rotation (the census SKIPS those rects and warns - a rotated
        /// tile never silently claims a DP).</summary>
        static bool TryRect(double x, double y, double rot, double w, double h,
                            out TileRect r)
        {
            r = default(TileRect);
            long q = ((long)Math.Round(rot) % 360 + 360) % 360;
            double x0, x1, y0, y1;
            if (q == 0)      { x0 = x; x1 = x + w; y0 = y; y1 = y + h; }
            else if (q == 90)  { x0 = x - h; x1 = x; y0 = y; y1 = y + w; }
            else if (q == 180) { x0 = x - w; x1 = x; y0 = y - h; y1 = y; }
            else if (q == 270) { x0 = x; x1 = x + h; y0 = y - w; y1 = y; }
            else return false;
            r.X0 = x0; r.X1 = x1; r.Y0 = y0; r.Y1 = y1;
            return true;
        }

        static string OrNone(string s)
        {
            return string.IsNullOrEmpty(s) ? "None" : s;
        }

        static string TileRefOf(string expansion, string tileId, string side)
        {
            return (expansion ?? "?") + (tileId ?? "?") + InteriorLaw.NormalizeSide(side);
        }

        /// <summary>Build the snapshot for the loaded mission. Any caller on
        /// the tactical rails may pass (FindObjectOfType&lt;MapEntityManager&gt;(),
        /// DataStore.mission, DataStore.sagaSessionData); nulls degrade to the
        /// empty snapshot (Null-Law), never an exception at the seam.
        /// isMassiveCard is the runtime Massive view (bridge
        /// CatalogCardIsMassive) - the spec 9.2 "whose card IsMassive" gate;
        /// null keeps every authored lane unregistered (fail-closed: no
        /// lift without the Massive view).</summary>
        public static MissionInteriorSnapshot Build(
            Saga.MapEntityManager manager,
            Saga.Mission missionData,
            Saga.SagaSession session,
            Func<string, bool> isMassiveCard)
        {
            // ---- tiles ------------------------------------------------------
            // FindObjectsOfTypeAll (NOT FindObjectsOfType): hidden/inactive
            // tiles are part of the interior index too (M13r lane). Order is
            // undefined -> sort by tile GUID so the snapshot never depends on
            // scene load order (determinism law).
            var renderers = new List<Saga.TileRenderer>(
                Resources.FindObjectsOfTypeAll<Saga.TileRenderer>());
            renderers.Sort((a, b) => string.Compare(
                TileGuidKey(a), TileGuidKey(b), StringComparison.Ordinal));

            var tiles = new List<TileRect>();
            var sectionTiles = new Dictionary<string, List<TileStatus>>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int seenRotSkips = 0;
            foreach (var tr in renderers)
            {
                if (tr == null || tr.mapTile == null) continue;   // renderer without data
                var mt = tr.mapTile;
                var key = TileGuidKey(tr);
                if (!seen.Add(key)) continue;                     // restore duplicates collapse
                string side = InteriorLaw.NormalizeSide(mt.tileSide);   // GetBiomeType law
                string expansion = mt.expansion.ToString();
                string label = OrNone(tr.tileDescriptor == null ? null :
                    (side == "A" ? tr.tileDescriptor.biomeA : tr.tileDescriptor.biomeB));
                string tref = TileRefOf(expansion, mt.tileID, side);
                string reason;
                var status = InteriorLaw.StatusForTile(expansion, mt.tileID, side, label, out reason);
                if (reason == InteriorLaw.ReasonBiomeDrift)
                    Debug.Log("[interior] row_biome_drift tile=" + tref +
                              " label=\"" + label + "\" signed row overridden fail-closed to Ambiguous");
                if (!sectionTiles.ContainsKey(mt.mapSectionOwner.ToString().ToLowerInvariant()))
                    sectionTiles[mt.mapSectionOwner.ToString().ToLowerInvariant()] =
                        new List<TileStatus>();
                sectionTiles[mt.mapSectionOwner.ToString().ToLowerInvariant()].Add(status);

                TileRect r;
                if (tr.tileDescriptor == null)
                {
                    if (seenRotSkips < 16)
                        Debug.LogWarning("[interior] tile_no_descriptor tile=" + tref +
                                         " - no rect (status/section still index)");
                    seenRotSkips++;
                    continue;
                }
                double w = tr.tileDescriptor.width * PxPerHex;
                double h = tr.tileDescriptor.height * PxPerHex;
                if (mt.entityPosition == null)
                {
                    if (seenRotSkips < 16)
                        Debug.LogWarning("[interior] tile_no_position tile=" + tref +
                                         " - no rect (status/section still index)");
                    seenRotSkips++;
                    continue;
                }
                if (!TryRect(mt.entityPosition.X, mt.entityPosition.Y,
                             mt.entityRotation, w, h, out r))
                {
                    if (seenRotSkips < 16)
                        Debug.LogWarning("[interior] tile_rot_skipped tile=" + tref +
                                         " rot=" + mt.entityRotation + " rect skipped (census law)");
                    seenRotSkips++;
                    continue;
                }
                r.SectionGuid = mt.mapSectionOwner.ToString().ToLowerInvariant();
                r.Status = status;
                r.Ref = tref;
                r.Label = label;
                tiles.Add(r);
            }

            // ---- sections (D1 four-state over the same statuses) ----------
            var sections = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
            foreach (var kv in sectionTiles)
                sections[kv.Key] = InteriorLaw.SectionStatus(kv.Value);

            // ---- DP -> tile (census mapping, 1:1) --------------------------
            var dps = new Dictionary<string, MissionInteriorSnapshot.DpEntry>(StringComparer.Ordinal);
            var entities = missionData != null ? missionData.mapEntities : null;
            if (entities != null)
            {
                foreach (var e in entities)
                {
                    if (e == null || e.entityType != Saga.EntityType.DeploymentPoint) continue;
                    var dpsec = e.mapSectionOwner.ToString().ToLowerInvariant();
                    double x = e.entityPosition == null ? 0 : e.entityPosition.X;
                    double y = e.entityPosition == null ? 0 : e.entityPosition.Y;
                    var cands = new List<TileRect>();
                    foreach (var t in tiles)
                        if (t.X0 - Eps <= x && x <= t.X1 + Eps && t.Y0 - Eps <= y && y <= t.Y1 + Eps)
                            cands.Add(t);
                    string kind;
                    TileRect chosen;
                    TileStatus status;
                    ResolveDp(cands, x, y, dpsec, out kind, out chosen, out status);
                    dps[DpKey(e.GUID)] = new MissionInteriorSnapshot.DpEntry
                    {
                        Status = status,
                        Kind = kind,
                        TileRef = kind == "ambiguous_mixed" || kind == "tileless" ? "-" : chosen.Ref,
                        Label = kind == "ambiguous_mixed" || kind == "tileless" ? "None" : chosen.Label,
                        SectionGuid = dpsec,
                    };
                }
            }

            var overrides = new InteriorLaw.OverrideSet();

            // ---- D6 overrides: the mission's OWN authored lanes, fail-CLOSED -
            // Fresh-look review MAJOR-1/2 fix (09.11): no deployLedger here
            // (campaign-scoped rows would leak a lift into missions that never
            // authored the indoor DP; event rows only land in the ledger at
            // deployment time). The mission's own JSON is the authored truth
            // (spec 4-D6 "at mission start, for every group the mission
            // authored"), and InteriorDpProbe.cs:353-425 is the pinned law -
            // this block mirrors it 1:1; the gate's census-equality pin (spec
            // 9.4) only holds because both sides read the same lanes.
            var lanes = new List<KeyValuePair<string, string>>(); // (cardID, dp guid)
            string missionJson = session != null ? session.missionStringified : null;
            if (!string.IsNullOrEmpty(missionJson))
            {
                try
                {
                    var root = TacticalAI.Core.Json.AsDict(TacticalAI.Core.Json.Parse(
                        TrailingCommaRx.Replace(missionJson, "$1")));
                    // -- initial groups (probe 356-372) --
                    foreach (var gRaw in TacticalAI.Core.Json.AsList(
                                 TacticalAI.Core.Json.Get(root, "initialDeploymentGroups", new List<object>())))
                    {
                        var g = TacticalAI.Core.Json.AsDict(gRaw);
                        var cid = CardIdOf(TacticalAI.Core.Json.Get(g, "cardID", null));
                        if (string.IsNullOrEmpty(cid)) continue; // name-only: unattributable at runtime, registers nothing anyway
                        foreach (var pRaw in TacticalAI.Core.Json.AsList(
                                     TacticalAI.Core.Json.Get(g, "pointList", new List<object>())))
                        {
                            var gu = DpText(TacticalAI.Core.Json.Get(
                                TacticalAI.Core.Json.AsDict(pRaw), "GUID", null));
                            if (string.IsNullOrEmpty(gu) || gu == NullGuid || gu == SectionSentinel) continue;
                            lanes.Add(new KeyValuePair<string, string>(cid, gu));
                        }
                    }
                    // -- event lanes eventActionType 6/7/21 (probe 373-426) --
                    var actions = new List<Dictionary<string, object>>();
                    foreach (var key in new[] { "globalEvents", "eventGroups", "mapSections",
                                                "initialDeploymentGroups", "reservedDeploymentGroups" })
                        WalkActions(TacticalAI.Core.Json.Get(root, key, null), actions);
                    foreach (var a in actions)
                    {
                        var t = (long)TacticalAI.Core.Json.Get(a, "eventActionType", -1L);
                        if (t != 6L && t != 7L && t != 21L) continue;
                        var egdRaw = TacticalAI.Core.Json.Get(a, "enemyGroupData", null);
                        var egd = egdRaw is Dictionary<string, object>
                            ? (Dictionary<string, object>)egdRaw
                            : new Dictionary<string, object>();
                        string cid = t == 21L
                            ? CardIdOf(TacticalAI.Core.Json.Get(egd, "cardID", null))
                            : CardIdOf(TacticalAI.Core.Json.Get(egd, "cardID", null))
                              ?? CardIdOf(TacticalAI.Core.Json.Get(a, "allyID", null));
                        if (string.IsNullOrEmpty(cid)) continue; // unattributable - no lift possible (the probe's name-only Massive lane can never register either: RegisterAuthored(null) is unverified)
                        // Runtime Massive view = catalog keywords (spec 9.2
                        // "whose card IsMassive" - the CARD's view). The
                        // probe's type-21 keyword-only lane has no shipped row
                        // (registration pins carry only the two cid rows); a
                        // custom-keyword lane without a catalog card stays the
                        // disclosed census gap - no lift, ban stays.
                        if (isMassiveCard == null) continue;      // no Massive view - fail-closed, no lift
                        if (!isMassiveCard(cid)) continue;        // spec 9.2 "whose card IsMassive"
                        var guids = new List<string>();
                        var sdp = DpText(TacticalAI.Core.Json.Get(a, "specificDeploymentPoint", null));
                        if (!string.IsNullOrEmpty(sdp) && sdp != NullGuid && sdp != SectionSentinel)
                            guids.Add(sdp);
                        foreach (var pRaw in TacticalAI.Core.Json.AsList(
                                     TacticalAI.Core.Json.Get(egd, "pointList", new List<object>())))
                        {
                            var gu = DpText(TacticalAI.Core.Json.Get(
                                TacticalAI.Core.Json.AsDict(pRaw), "GUID", null));
                            if (string.IsNullOrEmpty(gu) || gu == NullGuid || gu == SectionSentinel) continue;
                            if (!guids.Contains(gu)) guids.Add(gu);
                        }
                        // D6 authored channel = Spot Specific (==1); a real
                        // GUID under Active is an editor leftover (probe 421-423).
                        var spot = (long)TacticalAI.Core.Json.Get(a, "deploymentPoint", 0L);
                        if (spot != 1L) continue;
                        foreach (var gu in new SortedSet<string>(guids))
                            lanes.Add(new KeyValuePair<string, string>(cid, gu));
                    }
                }
                catch (Exception ex)
                {
                    // A broken mission lane registers NOTHING - the ban stays
                    // (fail-CLOSED for the override, the one direction the law
                    // can take without a mission). The gate fail-closes hard on
                    // the same shapes, so shipped data cannot drift here silent.
                    Debug.LogWarning("[interior] override_lane_unparsed - " + ex.Message
                                     + " - no overrides registered (ban stays)");
                    lanes.Clear();
                }
            }
            else if (missionData != null && missionData.initialDeploymentGroups != null)
            {
                // Defensive lane: no stringified mission on the session (every
                // real load site sets it - SagaController:257/275,
                // MissionPicker:442, SagaSetup). The live start groups carry
                // the same authored start truth; the event lanes cannot be
                // recovered here, and that is said out loud.
                Debug.LogWarning("[interior] override_lane_json_missing - start groups only, event authored lanes unavailable");
                foreach (var g in missionData.initialDeploymentGroups)
                {
                    if (g == null || string.IsNullOrEmpty(g.cardID) || g.pointList == null) continue;
                    if (isMassiveCard != null && !isMassiveCard(g.cardID)) continue;
                    if (isMassiveCard == null) break; // fail-closed: no Massive view
                    foreach (var p in g.pointList)
                    {
                        if (p == null) continue;
                        var gu = DpText(p.GUID);
                        if (gu == NullGuid || gu == SectionSentinel) continue;
                        lanes.Add(new KeyValuePair<string, string>(g.cardID, gu));
                    }
                }
            }
            foreach (var pair in lanes)
            {
                // spec 9.2: only lanes of cards the runtime Massive view
                // confirms are candidates; no Massive view = fail-closed.
                if (isMassiveCard == null || !isMassiveCard(pair.Key)) continue;
                Guid g;
                MissionInteriorSnapshot.DpEntry entry = null;
                TileStatus st = TileStatus.Ambiguous;
                string tref = "-";
                if (Guid.TryParse(pair.Value, out g))
                {
                    // DP identity through the LIVE entity (spec 9.2): GetEntity
                    // is the sanctioned resolution seam.
                    if (manager != null && manager.GetEntity(g) == null && !dps.ContainsKey(DpKey(g)))
                    {
                        // Not a DeploymentPoint entity in this mission (stale
                        // authoring) - registers NOTHING, ban stays.
                        Debug.Log("[interior] override_unverified card=" + pair.Key +
                                  " dp=" + pair.Value + " tile=- - no DP entity in this mission");
                        continue;
                    }
                    if (dps.TryGetValue(DpKey(g), out entry))
                    {
                        st = entry.Status;
                        tref = entry.TileRef;
                    }
                }
                var fact = overrides.RegisterAuthored(pair.Key, st);
                if (fact == InteriorLaw.OverrideRegistered)
                    Debug.Log("[interior] massive-override card=" + pair.Key +
                              " dp=" + pair.Value + " tile=" + tref +
                              " - VERIFIED-BANNED authored tile, card lifted for this mission (D6)");
                else if (fact == InteriorLaw.OverrideUnverified)
                    Debug.Log("[interior] override_unverified card=" + pair.Key +
                              " dp=" + pair.Value + " tile=" + tref +
                              " - ban STAYS until the mapping row is verified (D6 fail-closed)");
                // OutdoorNoNeed (authored DP Open): nothing to lift, no line.
            }

            var snap = new MissionInteriorSnapshot(dps, sections, overrides, tiles.Count);
            if (snap.IsEmpty)
                Debug.Log("[interior] snapshot-empty tiles=" + snap.TileCount +
                          " dps=" + snap.DpCount + " - Null-Law (interior filter inactive, behaviour unchanged)");
            else
                Debug.Log("[interior] snapshot tiles=" + snap.TileCount + " dps=" + snap.DpCount +
                          " sections=" + sections.Count + " overrides=" + overrides.Count);
            return snap;
        }

        static string TileGuidKey(Saga.TileRenderer tr)
        {
            if (tr == null || tr.mapTile == null) return "";
            return tr.mapTile.GUID.ToString();
        }

        // ---- mission-lane helpers (1:1 port of InteriorDpProbe.cs) ----
        const string NullGuid = "00000000-0000-0000-0000-000000000000";
        const string SectionSentinel = "11111111-1111-1111-1111-111111111111";
        // Same trailing-comma law as probe ReadJsonFile / gate / census.
        static readonly System.Text.RegularExpressions.Regex TrailingCommaRx =
            new System.Text.RegularExpressions.Regex(@",(\s*[}\]])");

        // probe Key()/KeyOf() lane (InteriorDpProbe.cs:87-96): GUID in its
        // canonical text form (lowercase, hyphenated), else lower-invariant.
        static string DpText(object v)
        {
            if (v == null) return null;
            if (v is Guid) return ((Guid)v).ToString();
            var s = v as string;
            if (string.IsNullOrEmpty(s)) return null;
            Guid g;
            if (Guid.TryParse(s.Trim(), out g)) return g.ToString();
            return s.Trim().ToLowerInvariant();
        }

        static string CardIdOf(object v)
        {
            if (v == null) return null;
            if (v is long) return v.ToString();
            var s = v as string;
            return string.IsNullOrEmpty(s) ? null : s;
        }

        static void WalkActions(object node, List<Dictionary<string, object>> outl)
        {
            var dict = node as Dictionary<string, object>;
            if (dict != null)
            {
                var list = TacticalAI.Core.Json.Get(dict, "eventActions", null) as List<object>;
                if (list != null)
                    foreach (var aRaw in list)
                    {
                        var ad = aRaw as Dictionary<string, object>;
                        if (ad != null) outl.Add(ad);
                    }
                foreach (var v in dict.Values) WalkActions(v, outl);
            }
            else
            {
                var l = node as List<object>;
                if (l != null) foreach (var v in l) WalkActions(v, outl);
            }
        }

        /// <summary>The census DP resolution, 1:1 (massive_interior_census.py
        /// 520-548): 0 rects -> tileless (Ambiguous); 1 -> single; many ->
        /// strict-interior set, then the DP's own section, then a status-
        /// uniform pool (status resolved, identity ambiguous); mixed-status
        /// survivors -> ambiguous_mixed (Ambiguous). Fail-CLOSED for the BAN:
        /// only a Banned tile read under the pinned law (RR enumeration or a
        /// signed row) ever produces Banned - every unresolved shape reads
        /// Ambiguous, which the filter treats fail-OPEN (D2).</summary>
        static void ResolveDp(List<TileRect> cands, double x, double y, string dpsec,
                              out string kind, out TileRect chosen,
                              out TileStatus status)
        {
            chosen = default(TileRect);
            if (cands.Count == 0)
            {
                kind = "tileless";
                status = TileStatus.Ambiguous;
                return;
            }
            if (cands.Count == 1)
            {
                kind = "single";
                chosen = cands[0];
                status = chosen.Status;
                return;
            }
            var strict = new List<TileRect>();
            foreach (var t in cands)
                if (t.X0 + Eps < x && x < t.X1 - Eps && t.Y0 + Eps < y && y < t.Y1 - Eps)
                    strict.Add(t);
            if (strict.Count == 1)
            {
                kind = "strict";
                chosen = strict[0];
                status = chosen.Status;
                return;
            }
            var basePool = strict.Count > 0 ? strict : cands;
            var secf = new List<TileRect>();
            foreach (var t in basePool)
                if (t.SectionGuid == dpsec) secf.Add(t);
            if (secf.Count == 1)
            {
                kind = "section";
                chosen = secf[0];
                status = chosen.Status;
                return;
            }
            // census law: the uniformity pool is (strict or cands) - the
            // section filter only ever SELECTS a single tile, it never
            // narrows the status-uniformity pool.
            var pool = basePool;
            bool uniform = true;
            for (int i = 1; i < pool.Count; i++)
                if (pool[i].Status != pool[0].Status) { uniform = false; break; }
            if (uniform)
            {
                kind = "status_uniform";
                chosen = pool[0];
                status = pool[0].Status;
                return;
            }
            kind = "ambiguous_mixed";
            status = TileStatus.Ambiguous;
        }
    }
}
