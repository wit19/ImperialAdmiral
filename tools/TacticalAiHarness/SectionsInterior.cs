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
        }
    }
}
