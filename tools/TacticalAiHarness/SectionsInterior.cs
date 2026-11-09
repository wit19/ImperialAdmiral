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
            // LEGALITY lane of the law inline in
            // TryChooseDeploymentPoint; MassiveInteriorGate's source-contract
            // check fails the build when the bridge block drifts away from
            // the anchors this mirror reads (the bridge itself is Unity-side
            // and not compile-visible here). P41e (card t_1f93d98d) added a
            // second drop condition behind this lane - DPs OUTSIDE the
            // USEFUL-DP set (spec 4-D3-C2); that lane needs sections, edges
            // and an op zone, so it is pinned by the useful block below and
            // the gate's RunUsefulnessContract, not by these legality
            // vectors (their values and pins are untouched).
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

                // D6 override: the mission's authored lane pairs (pinned
                // verbatim from the census, which reads the mission file's
                // own pointList/event lanes) through the REAL fail-closed
                // registration - the same OverrideSet.RegisterAuthored call
                // the builder makes from the mission JSON at runtime.
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
                    // zero-usable pool => honest NO-DEPLOY: no winner at all.
                    // Bridge-side law (MAJOR-4, fresh-look 09.11): the bridge
                    // answers the caller with TRUE + chosen = Guid.Empty -
                    // the app's own no-DP marker. Returning FALSE is forbidden
                    // here: the caller's legacy draw would run on the list the
                    // filter had just emptied (GenerateRandomNumbers(0)==[0]
                    // -> IndexOutOfRange) or, with a restored pool, randomly
                    // pick a BANNED DP. Never a silent pick onto a Banned tile
                    // (spec 4-D3-B); vector pins: empty pool, no winner.
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

            // ---- P41d (card t_59ba21ee; spec 9.3-2): the anchor mask ----
            // The SAME InteriorLaw.AnchorMaskFor the bridge helper
            // MassiveAnchorMask calls at every HeroInSection read site.
            // Section statuses come from the P41b census rows above (CORE4
            // four-state map); edges are hand-written (P41b law: hand-
            // written vectors only for edges). An unknown section reads
            // Ambiguous - the snapshot's own SectionStatusOf unknown law.
            foreach (var raw in Json.AsList(v["anchor"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var status = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["status"]))
                    status[kv.Key] = SectionStatusOf(Json.AsStr(kv.Value));
                var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["edges"]))
                {
                    var l = new List<string>();
                    foreach (var n in Json.AsList(kv.Value)) l.Add(Json.AsStr(n));
                    adj[kv.Key] = l;
                }
                Func<string, IEnumerable<string>> nb = s =>
                    adj.ContainsKey(s) ? adj[s] : new List<string>();
                Func<string, SectionStatusT> st = s =>
                    status.ContainsKey(s) ? status[s] : SectionStatusT.Ambiguous;
                var m = InteriorLaw.AnchorMaskFor(
                    Json.AsStr(c["anchor"]),
                    Json.AsBool(c["massive"]), Json.AsBool(c["override"]), nb, st);
                var exp = Json.AsDict(c["expect"]);
                Check("interior[anchor." + name + "].section",
                    Json.AsStr(exp["section"]), m.Section ?? "");
                Check("interior[anchor." + name + "].fact",
                    Json.AsStr(exp["fact"]), m.Fact);
            }

            // ---- P41e USEFULNESS truth table (card t_1f93d98d; spec 9.4:
            //      the USEFULNESS truth table incl. the D3-C2 named
            //      vector) -------------------------------------------------
            // The SAME InteriorLaw.UsefulDps the bridge's MassiveUsefulDps
            // calls for the buy gates (TryRankProcurement +
            // DataStore.GetStrictDeployable + PersonaState.SwapDeployCandidate),
            // the DP scorer (TryChooseDeploymentPoint) and the refund guard
            // (popup deploy trigger). Sections and edges hand-written (P41b
            // law), the CORE4 vocabulary carried over from the anchor
            // vectors; every DP carries its D3-B tile status. The buy/refund
            // rows pin the DP-first economy outcome (skip + rerank + refund,
            // Thomas 09.11); the bridge-side literals are pinned by the
            // gate's RunUsefulnessContract (the bridge is Unity-side, not
            // compile-visible here).
            // The block is OPTIONAL per file (additive-block law): a tree
            // without P41e vectors still runs every other interior section.
            foreach (var raw in Json.AsList(v.ContainsKey("useful") ? v["useful"] : new List<object>()))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var status = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["status"]))
                    status[kv.Key] = SectionStatusOf(Json.AsStr(kv.Value));
                var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["edges"]))
                {
                    var l = new List<string>();
                    foreach (var n in Json.AsList(kv.Value)) l.Add(Json.AsStr(n));
                    adj[kv.Key] = l;
                }
                Func<string, IEnumerable<string>> nb = s =>
                    adj.ContainsKey(s) ? adj[s] : new List<string>();
                Func<string, SectionStatusT> st = s =>
                    status.ContainsKey(s) ? status[s] : SectionStatusT.Ambiguous;
                var probes = new List<InteriorLaw.UsefulProbe>();
                foreach (var draw in Json.AsList(c["dps"]))
                {
                    var dp = Json.AsDict(draw);
                    probes.Add(new InteriorLaw.UsefulProbe
                    {
                        Key = Json.AsStr(dp["key"]),
                        Section = Json.AsStr(dp["section"]),
                        Tile = TileStatusOf(Json.AsStr(dp["tile"])),
                    });
                }
                var zone = new List<string>();
                foreach (var z in Json.AsList(c["opzone"])) zone.Add(Json.AsStr(z));
                var res = InteriorLaw.UsefulDps(probes, st, nb, zone,
                    Json.AsBool(c["massive"]), Json.AsBool(c["override"]));
                var exp = Json.AsDict(c["expect"]);
                Check("interior[useful." + name + "].active",
                    Json.AsBool(exp["active"]), res.Active);
                Check("interior[useful." + name + "].keys",
                    Json.AsStr(exp["keys"]), string.Join(",", res.Keys.ToArray()));
                Check("interior[useful." + name + "].fact",
                    Json.AsStr(exp["fact"]), res.Fact);
                var reasons = new List<string>();
                foreach (var p in probes)
                {
                    string rr;
                    reasons.Add(p.Key + ":" +
                        (res.Reason.TryGetValue(p.Key, out rr) ? rr : "-"));
                }
                Check("interior[useful." + name + "].reasons",
                    Json.AsStr(exp["reasons"]), string.Join(",", reasons.ToArray()));
                // Named scenario part (a), blockade branch: the buy IS
                // allowed and the order carries a pinned SEAL, never a
                // chase - the P41d law over the SAME topology (spec 9.4
                // "...a pinned SEAL order, never a chase"; 4-D3-C2
                // blockade predicate DECIDED 09.11).
                if (exp.ContainsKey("seal"))
                {
                    var m = InteriorLaw.AnchorMaskFor(Json.AsStr(exp["seal"]),
                        true, false, nb, st);
                    Check("interior[useful." + name + "].seal_section",
                        Json.AsStr(exp["seal_section"]), m.Section ?? "");
                    Check("interior[useful." + name + "].seal_fact",
                        Json.AsStr(exp["seal_fact"]), m.Fact);
                }
                // Named scenario part (b), beyond branch: the USELESS
                // Massive is skipped BEFORE scoring and the strongest
                // USEFUL alternative at the same threat takes the slot -
                // the strict buy law's own order (cost desc, ties by id
                // Ordinal), never a repricing (spec 4-D3-C "the gate just
                // removes dead candidates before scoring"). Rows carry
                // their useful answer; useful=false models the empty
                // USEFUL-DP set of this vector's topology.
                if (exp.ContainsKey("buy"))
                {
                    var b = Json.AsDict(exp["buy"]);
                    long budget = Json.AsLong(b["budget"]);
                    string winner = ""; long best = -1;
                    foreach (var rRaw in Json.AsList(b["rows"]))
                    {
                        var r = Json.AsDict(rRaw);
                        long cost = Json.AsLong(r["cost"]);
                        if (!Json.AsBool(r["useful"]) || cost > budget) continue;
                        var id = Json.AsStr(r["id"]);
                        if (cost > best ||
                            (cost == best && string.CompareOrdinal(id, winner) < 0))
                        { best = cost; winner = id; }
                    }
                    Check("interior[useful." + name + "].buy_winner",
                        Json.AsStr(b["expect_winner"]), winner);
                }
                if (exp.ContainsKey("refund"))
                {
                    var rf = Json.AsDict(exp["refund"]);
                    // the spec forensics shape (9.3-4) incl. the EXACT
                    // deterministic fact of this vector's emptied set
                    var line = "[dp-massive] refund card=" + Json.AsStr(rf["card"])
                               + " ap=" + Json.AsLong(rf["ap"])
                               + " reason=no_useful_dp_after=" + res.Fact;
                    Check("interior[useful." + name + "].refund_line",
                        Json.AsStr(rf["line"]), line);
                }
            }

            // ---- P41d (spec 9.3-3/5-H4): REPLAN fact 4, interior ingress --
            // The SAME InteriorLaw.IngressFact the EnsureRound seam calls
            // behind the three P33-H4b feed facts. anchors = [hero,
            // section-at-build] in caller (capture) order; current = the
            // hero's section as read mid-round. expect = the hit hero, ""
            // = the fact stays silent.
            foreach (var raw in Json.AsList(v["replan"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var status = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["status"]))
                    status[kv.Key] = SectionStatusOf(Json.AsStr(kv.Value));
                Func<string, SectionStatusT> st = s =>
                    status.ContainsKey(s) ? status[s] : SectionStatusT.Ambiguous;
                var cur = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in Json.AsDict(c["current"]))
                    cur[kv.Key] = Json.AsStr(kv.Value);
                var anchors = new List<KeyValuePair<string, string>>();
                foreach (var raw2 in Json.AsList(c["anchors"]))
                {
                    var a = Json.AsList(raw2);
                    anchors.Add(new KeyValuePair<string, string>(
                        Json.AsStr(a[0]), Json.AsStr(a[1])));
                }
                var hit = InteriorLaw.IngressFact(anchors,
                    h => cur.ContainsKey(h) ? cur[h] : "", st);
                Check("interior[replan." + name + "].hit",
                    Json.AsStr(c["expect"]), hit ?? "");
            }
        }
    }
}
