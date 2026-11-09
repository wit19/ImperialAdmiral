// P41b (card t_1b28a273; CONCEPT_massive_interior_v1.md v1.3 9.1/9.4): drives the
// SAME InteriorLaw functions the H3 live seams will call over the hand-pinned
// ground truth in Assets/Tests/Fixtures/tacai/massive_interior.json (NOT
// --record; same route as tracker_bind.json/duty_loader.json). Canonical
// vectors come out of the P41a census (docs/MASSIVE_interior_census.md - the
// CORE4 map sections, the CORE4/CORE11/JABBA9 authored rows, the census tile
// classes incl. the Hoth-19 numeric-overlap guard); hand-written vectors only
// for edges (section edges, synthetic signed rows - no shipped row is signed
// yet, so the signature path is pinned through StatusForRow). This section
// mirrors the shipped crosswalk law, never re-implements it: expectations are
// the hand-derived fixture values, the code only calls the Core functions.
using System;
using System.Collections.Generic;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static TileStatus TileStatusOf(string s)
        {
            if (s == "Open") return TileStatus.Open;
            if (s == "Banned") return TileStatus.Banned;
            if (s == "Ambiguous") return TileStatus.Ambiguous;
            throw new Exception("massive_interior: unknown TileStatus '" + s + "'");
        }

        static SectionStatusT SectionStatusOf(string s)
        {
            if (s == "Open") return SectionStatusT.Open;
            if (s == "Banned") return SectionStatusT.Banned;
            if (s == "Mixed") return SectionStatusT.Mixed;
            if (s == "Ambiguous") return SectionStatusT.Ambiguous;
            throw new Exception("massive_interior: unknown SectionStatusT '" + s + "'");
        }

        // tile ref -> the Core read incl. the forensic reason
        static TileStatus TileOf(Dictionary<string, object> t, out string reason)
        {
            return InteriorLaw.StatusForTile(
                Json.AsStr(t["expansion"]), Json.AsStr(t["tileId"]),
                Json.AsStr(t["side"]), Json.AsStr(t["biome"]), out reason);
        }

        static string TileRef(Dictionary<string, object> t)
        {
            return Json.AsStr(t["expansion"]) + Json.AsStr(t["tileId"]) +
                   InteriorLaw.NormalizeSide(Json.AsStr(t["side"]));
        }

        static void SecInterior()
        {
            var v = LoadJson("massive_interior.json");

            // ---- tile law + forensic reason --------------------------------
            foreach (var raw in Json.AsList(v["tiles"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);
                string reason;
                var st = InteriorLaw.StatusForTile(
                    Json.AsStr(c["expansion"]), Json.AsStr(c["tileId"]),
                    Json.AsStr(c["side"]), Json.AsStr(c["biome"]), out reason);
                Check("interior[" + name + "].status", Json.AsStr(exp["status"]), st.ToString());
                Check("interior[" + name + "].reason", Json.AsStr(exp["reason"]), reason);
            }

            // ---- the Thomas-signature path (synthetic rows: no shipped row
            //      is signed yet) ----------------------------------------------
            foreach (var raw in Json.AsList(v["rows"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var r = Json.AsDict(c["row"]);
                var exp = Json.AsDict(c["expect"]);
                var row = new CrosswalkRow(
                    Json.AsStr(r["expansion"]), Json.AsStr(r["tileId"]),
                    Json.AsStr(r["side"]), Json.AsStr(r["biome"]),
                    Json.AsStr(r["candidate"]), Json.AsBool(r["rr"]),
                    Json.AsStr(r["ruling"]));
                string reason;
                var st = InteriorLaw.StatusForRow(row, Json.AsStr(c["placedBiome"]), out reason);
                Check("interior[rows." + name + "].status", Json.AsStr(exp["status"]), st.ToString());
                Check("interior[rows." + name + "].reason", Json.AsStr(exp["reason"]), reason);
            }

            // ---- four-state section derivation ----------------------------
            foreach (var raw in Json.AsList(v["sections"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);
                var tiles = new List<TileStatus>();
                if (c.ContainsKey("tiles"))
                {
                    // real authored map: derive every tile status through the
                    // law itself (crosswalk + RR pins), then the section law
                    foreach (var tRaw in Json.AsList(c["tiles"]))
                    {
                        string reason;
                        tiles.Add(TileOf(Json.AsDict(tRaw), out reason));
                    }
                }
                else
                {
                    foreach (var sRaw in Json.AsList(c["status"]))
                        tiles.Add(TileStatusOf(Json.AsStr(sRaw)));
                }
                Check("interior[sections." + name + "].status",
                    Json.AsStr(exp["status"]), InteriorLaw.SectionStatus(tiles).ToString());
                Check("interior[sections." + name + "].forensic",
                    Json.AsBool(exp["forensic"]), InteriorLaw.SectionNeedsAmbiguousForensic(tiles));
            }

            // ---- D6 override registration from authored rows --------------
            foreach (var raw in Json.AsList(v["overrides"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);
                var set = new InteriorLaw.OverrideSet();   // one instance per mission (card x mission)
                var facts = new List<string>();
                foreach (var pRaw in Json.AsList(c["placements"]))
                {
                    var p = Json.AsDict(pRaw);
                    string reason;
                    var tileStatus = TileOf(Json.AsDict(p["tile"]), out reason);
                    facts.Add(Json.AsStr(p["card"]) + "=" +
                        set.RegisterAuthored(Json.AsStr(p["card"]), tileStatus));
                }
                Check("interior[overrides." + name + "].facts",
                    Json.AsStr(exp["facts"]), string.Join(";", facts.ToArray()));
                var covExp = new List<string>();
                foreach (var o in Json.AsList(exp["covered"])) covExp.Add(Json.AsStr(o));
                Check("interior[overrides." + name + "].covered",
                    string.Join(",", covExp.ToArray()),
                    string.Join(",", set.CardNames().ToArray()));
                foreach (var kv in Json.AsDict(exp["probe"]))
                    Check("interior[overrides." + name + "].covers." + kv.Key,
                        Json.AsBool(kv.Value), set.Covers(kv.Key));
                // derived never stored (D5): rebuilding over the same authored
                // rows is the same set - deterministic replay, no persistence.
                var again = new InteriorLaw.OverrideSet();
                foreach (var pRaw in Json.AsList(c["placements"]))
                {
                    var p = Json.AsDict(pRaw);
                    string reason;
                    again.RegisterAuthored(Json.AsStr(p["card"]), TileOf(Json.AsDict(p["tile"]), out reason));
                }
                Check("interior[overrides." + name + "].rebuild",
                    string.Join(",", set.CardNames().ToArray()),
                    string.Join(",", again.CardNames().ToArray()));
            }

            // ---- NotBanned node filter (D3-A graph view, D2 fail-open) ----
            foreach (var raw in Json.AsList(v["mask"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var map = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["sections"]))
                    map[kv.Key] = SectionStatusOf(Json.AsStr(kv.Value));
                var mask = InteriorLaw.NotBanned(map);
                var outs = new List<string>();
                foreach (var pRaw in Json.AsList(c["probes"]))
                    outs.Add(mask(Json.AsStr(pRaw)) ? "1" : "0");
                Check("interior[mask." + name + "]",
                    Json.AsStr(c["expect"]), string.Join(";", outs.ToArray()));
            }

            // ---- seal the approach (D3-A BFS + fixed tie law) -------------
            foreach (var raw in Json.AsList(v["seal"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var status = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["status"]))
                    status[kv.Key] = SectionStatusOf(Json.AsStr(kv.Value));
                var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["edges"]))
                {
                    var l = new List<string>();
                    foreach (var n in Json.AsList(kv.Value)) l.Add(Json.AsStr(n));
                    edges[kv.Key] = l;
                }
                var seal = InteriorLaw.SealSection(
                    Json.AsStr(c["start"]),
                    s => edges.ContainsKey(s) ? edges[s] : (List<string>)null,
                    s => status.ContainsKey(s) ? status[s] : SectionStatusT.Ambiguous);
                Check("interior[seal." + name + "]",
                    Json.AsStr(c["expect"]), seal ?? "");
            }

            // ---- shipped-table sanity (mirror of the gate, small core) ----
            Check("interior[table].rows", 276L, (long)InteriorLaw.CrosswalkRows.Length);

            // ---- DP hard filter (P41c, card t_a2e54ec9; spec 4-D3-B v1.3,
            //      9.3-1, 9.4 Beweispunkte) ---------------------------------
            // Hand-pinned vectors from the H1 census over the REAL missions
            // Core/CORE4.json + Core/CORE3.json (real DP GUIDs, names,
            // sections, tiles - nothing invented). Tile statuses, the D6
            // override registration and the pick all run the REAL Core law.
            // The WINNERS come from one real Core ChooseScored run with
            // pinned seeds (seed-chain absolute winners are record-derived,
            // reviewed once - the dp_score.json discipline, never hand-
            // recomputed). The DROP RULE below is the pinned MIRROR of the
            // three-condition law inline in TacticalAIBridge.
            // TryChooseDeploymentPoint; MassiveInteriorGate's source-contract
            // check fails the build when the bridge block drifts away from
            // the anchors this mirror reads (the bridge itself is Unity-side
            // and not compile-visible here).
            foreach (var raw in Json.AsList(v["dp_filter"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);
                var card = Json.AsDict(c["card"]);
                var cardId = Json.AsStr(card["id"]);
                bool massive = Json.AsBool(card["massive"]);
                ulong seed = (ulong)Json.AsLong(c["seed"]);
                int round = (int)Json.AsLong(c["round"]);
                var tileList = Json.AsList(c["tiles"]);
                var dpList = Json.AsList(c["dps"]);
                // Null-Law first (builder law 9.2): 0 tiles / 0 DPs => empty
                // snapshot => the block is INACTIVE, no status consulted.
                bool snapshotEmpty = tileList.Count == 0 || dpList.Count == 0;

                // dpGuid (lowercase - the runtime key lane InteriorUnity.DpKey)
                // -> the REAL status read of its own tile; pool membership
                // (authored-active) + candidate shape kept in caller order.
                var dpStatus = new Dictionary<string, TileStatus>(StringComparer.Ordinal);
                var pool = new List<string>();
                var poolName = new Dictionary<string, string>(StringComparer.Ordinal);
                var poolSec = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var draw in dpList)
                {
                    var d = Json.AsDict(draw);
                    var guid = Json.AsStr(d["guid"]).ToLowerInvariant();
                    string reason;
                    dpStatus[guid] = snapshotEmpty ? TileStatus.Ambiguous
                                                   : TileOf(Json.AsDict(d["tile"]), out reason);
                    if (Json.AsBool(d["pool"]))
                    {
                        pool.Add(guid);
                        poolName[guid] = Json.AsStr(d["name"]);
                        poolSec[guid] = Json.AsStr(d["section"]);
                    }
                }

                // D6 override: the 'authored' ledger rows of the mission
                // through the REAL fail-closed registration (builder path).
                var ovr = new InteriorLaw.OverrideSet();
                foreach (var araw in Json.AsList(c["authored"]))
                {
                    var a = Json.AsDict(araw);
                    var aKey = Json.AsStr(a["dp"]).ToLowerInvariant();
                    ovr.RegisterAuthored(Json.AsStr(a["card_id"]),
                        dpStatus.ContainsKey(aKey) ? dpStatus[aKey] : TileStatus.Ambiguous);
                }
                bool covered = ovr.Covers(cardId);
                Check("interior[dpfilter." + name + "].covered", Json.AsBool(exp["covered"]), covered);

                // the bridge drop rule, mirrored (see header note)
                var surv = new List<string>();
                var drop = new List<string>();
                foreach (var g in pool)
                {
                    bool isDrop = !snapshotEmpty && massive && !covered &&
                                  dpStatus[g] == TileStatus.Banned;
                    if (isDrop) drop.Add(g); else surv.Add(g);
                }
                var expDrop = Json.AsList(exp["drop"]);
                Check("interior[dpfilter." + name + "].drop.count", (long)drop.Count, (long)expDrop.Count);
                for (int i = 0; i < Math.Min(drop.Count, expDrop.Count); i++)
                    Check("interior[dpfilter." + name + "].drop[" + i + "]",
                        Json.AsStr(expDrop[i]).ToLowerInvariant(), drop[i]);
                var expSurv = Json.AsList(exp["survivors"]);
                Check("interior[dpfilter." + name + "].survivors.count", (long)surv.Count, (long)expSurv.Count);
                for (int i = 0; i < Math.Min(surv.Count, expSurv.Count); i++)
                    Check("interior[dpfilter." + name + "].survivors[" + i + "]",
                        Json.AsStr(expSurv[i]).ToLowerInvariant(), surv[i]);

                // the pick: the REAL scored path over the surviving pool in
                // caller order, empty anchors/zone/leash/diversity (the
                // Null-Law ladder where the legacy seed chain decides).
                Func<List<string>, string> pick = list =>
                {
                    if (list.Count == 0) return "";
                    var cands = new List<DeployCandidate>();
                    foreach (var g in list)
                        cands.Add(new DeployCandidate
                        {
                            Guid = g, Name = poolName[g], SectionGuid = poolSec[g], X = 0, Y = 0,
                        });
                    var ch = DeploymentAnchors.ChooseScored(
                        cands, new DeployAnchor[0], null, null, seed, round, null, null, out _);
                    return ch == null ? "" : ch.Candidate.Guid;
                };
                Check("interior[dpfilter." + name + "].winner",
                    Json.AsStr(exp["winner"]).ToLowerInvariant(), pick(surv));
                if (exp.ContainsKey("honest_fail"))
                {
                    // zero-usable pool => honest deploy fail (empty-pool lane
                    // at the caller: no spawn, threat stays reserved) - never
                    // a silent pick onto a Banned tile (spec 4-D3-B).
                    Check("interior[dpfilter." + name + "].honest_fail.empty_pool", 0L, (long)surv.Count);
                    Check("interior[dpfilter." + name + "].honest_fail.no_winner", "", pick(surv));
                }
                if (exp.ContainsKey("identical"))
                {
                    // Null-Law bit-identity (card Beweispunkt, vector side):
                    // survivors == pool UNTOUCHED and the winner equals the
                    // unfiltered pick.
                    Check("interior[dpfilter." + name + "].identical.untouched", (long)pool.Count, (long)surv.Count);
                    Check("interior[dpfilter." + name + "].identical.winner", pick(pool), pick(surv));
                }
            }
        }
    }
}
