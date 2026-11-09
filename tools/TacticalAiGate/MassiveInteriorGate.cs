using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TacticalAI.Core;

// P41b (card t_1b28a273; CONCEPT_massive_interior_v1.md v1.3 sections 5-H2,
// 9.4): zero-fixture live gate for the Massive interior law. Reads the REAL
// shipped dimensions.json, the REAL P41a census artifact
// (tools/massive_interior_crosswalk_candidates.json) and ONE real mission
// file (Core/CORE4.json) - it never touches the harness fixtures - and pins,
// against the shipped Core/InteriorLaw.cs crosswalk data table:
//   (1) census-vs-file equality: every census row is a data row of the law
//       table and vice versa (position + field equality - the table and the
//       census artifact can never drift silently);
//   (2) RR enumeration completeness, fail-CLOSED (D2 "the mapping
//       verification stays fail-CLOSED"): every enumerated core id
//       19..36 resolves Banned and 38/39 resolves Open through BOTH sides,
//       every one of them found in dimensions.json and in the table - an
//       unmatched RR id fails the build and is named;
//   (3) mapping verification: each row's candidate re-derives from the
//       shipped biome label by the D1 rule, and a signed ruling is only ever
//       a known status word (no shipped row is signed yet - the slot is
//       verified empty/consistent);
//   (4) the ONE error class the design must not commit (spec section 7):
//       numeric overlap - Hoth ids inside 19..36 must NEVER resolve Banned;
//   (5) the four-state derivation on the REAL CORE4 map (a second compile
//       context of the same law the harness pins): Start Section Banned,
//       Hangar Mixed, Warehouse Mixed, Clearing Ambiguous - the authored
//       indoor Massive case of the census section 6.
// The override-set runtime==census comparison is the H3 hop's job (spec
// section 5-H3); this gate carries the census-vs-file equality it builds on.
public static class MassiveInteriorGate
{
    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";
    static readonly string Repo =
        "/home/open/work/ia-ai/ImperialCommander2";

    static int fails;
    static int checks;
    static void Ok(string m) { checks++; Console.WriteLine("PASS interior: " + m); }
    static void Fail(string m) { fails++; Console.WriteLine("FAIL interior: " + m); }

    // D1/H1 candidate rule, mirrored 1:1 from tools/massive_interior_census.py
    // (OPEN_BIOMES / AMBIG_BIOMES / the Interior branch): biome LABELS are
    // evidence only - which is exactly why expansion rows never auto-ban.
    // Unknown labels are unresolved (AMBIGUOUS candidate) in the census; the
    // gate additionally names them, a new shipped label is a data event.
    static readonly HashSet<string> OpenLabels = new HashSet<string>(StringComparer.Ordinal)
    { "Streets", "Desert", "Plains", "Snow", "Ice", "Forest" };
    static readonly HashSet<string> AmbigLabels = new HashSet<string>(StringComparer.Ordinal)
    { "Halls", "Jabba", "Imperial", "None", "Menu" };
    const string InteriorLabel = "Interior";

    static readonly string[] Expansions = { "Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal", "Other" };

    public static void Run()
    {
        RunCensusVsFileAndRrLaw();
        RunCore4Sections();
        Console.WriteLine("MassiveInteriorGate: " + (fails == 0 ? "ALL PASS" : "FAILURES") +
                          " | checks=" + checks + " fails=" + fails);
        if (fails > 0) Environment.ExitCode = 1;
    }

    // ------------------------------------------------ census vs file -------
    // The shipped dimensions.json carries a trailing comma before the final
    // ']' (measured: exactly one, array end) that MiniJson (correctly)
    // rejects. tools/massive_interior_census.py strips `,` before '}'/']'
    // with the same regex when it loads the file - the gate mirrors THAT
    // rule, so gate and census always read the identical descriptor set
    // (data-exists law: the two engines parse one way).
    static readonly Regex TcRule = new Regex(",\\s*([}\\]])");
    static object ReadJsonFile(string path)
    {
        var text = File.ReadAllText(path);
        return Json.Parse(TcRule.Replace(text, "$1"));
    }

    static Dictionary<string, Dictionary<int, string[]>> LoadDims()
    {
        var dims = new Dictionary<string, Dictionary<int, string[]>>(StringComparer.Ordinal);
        var dimList = Json.AsList(ReadJsonFile(App + "/Assets/Resources/dimensions.json"));
        foreach (var dRaw in dimList)
        {
            var d = Json.AsDict(dRaw);
            string exp = Json.AsStr(Json.Get(d, "expansion", "Core"));
            int id = Json.AsInt(Json.Get(d, "id", 0L));
            string[] biomes =
            {
                (string)Json.Get(d, "biomeA", null) ?? "None",
                (string)Json.Get(d, "biomeB", null) ?? "None"
            };
            Dictionary<int, string[]> byId;
            if (!dims.TryGetValue(exp, out byId)) { byId = new Dictionary<int, string[]>(); dims[exp] = byId; }
            byId[id] = biomes;
        }
        return dims;
    }

    static void RunCensusVsFileAndRrLaw()
    {
        // 1. load the shipped dimensions.json (real data, MiniJson tolerates
        //    the repo trailing-comma flavour)
        Dictionary<string, Dictionary<int, string[]>> dims;
        try
        {
            dims = LoadDims();
        }
        catch (Exception e) { Fail("dimensions.json unreadable: " + e.Message); return; }
        int dimCount = 0;
        foreach (var kv in dims) dimCount += kv.Value.Count;
        Ok("dimensions.json parsed (" + dimCount + " tile descriptors)");

        // 2. load the P41a census artifact (the H1 verification table)
        List<object> censusRows;
        try
        {
            var cen = Json.AsDict(ReadJsonFile(Repo + "/tools/massive_interior_crosswalk_candidates.json"));
            var schema = Json.AsStr(Json.Get(cen, "schema", ""));
            if (schema == "wa.massive_crosswalk/1") Ok("census artifact schema wa.massive_crosswalk/1");
            else Fail("census artifact schema is '" + schema + "', expected wa.massive_crosswalk/1");
            censusRows = Json.AsList(Json.Get(cen, "rows", null));
        }
        catch (Exception e) { Fail("census artifact unreadable: " + e.Message); return; }

        // 3. census-vs-file equality (the hop's core pin): the law table is
        //    the census table, row by row, field by field, in census order.
        var lawRows = InteriorLaw.CrosswalkRows;
        if (lawRows.Length != censusRows.Count)
        {
            Fail("law table rows=" + lawRows.Length + " != census rows=" + censusRows.Count);
            return;
        }
        Ok("law table row count == census row count (" + lawRows.Length + ")");
        int drift = 0, signed = 0, candErr = 0;
        for (int i = 0; i < censusRows.Count; i++)
        {
            var c = Json.AsDict(censusRows[i]);
            var r = lawRows[i];
            string cExp = Json.AsStr(c["expansion"]);
            string cId = ((long)Json.AsLong(c["tileID"])).ToString();
            string cSide = Json.AsStr(c["side"]);
            string cBiome = Json.AsStr(c["biome"]);
            string cCand = Json.AsStr(c["candidate_status"]);
            bool cRr = Json.AsBool(c["rr_enumerated"]);
            object cRulingRaw = Json.Get(c, "thomas_ruling", null);
            string cRuling = cRulingRaw == null ? "" : Json.AsStr(cRulingRaw);
            string where = cExp + cId + cSide;
            if (r.Expansion != cExp || r.TileId != cId || r.Side != cSide ||
                r.Biome != cBiome || r.Candidate != cCand || r.RrEnumerated != cRr ||
                r.Ruling != cRuling)
            {
                drift++;
                Fail("file row " + i + " differs from census: law(" + r.Expansion + "," + r.TileId +
                     "," + r.Side + "," + r.Biome + "," + r.Candidate + "," + r.RrEnumerated +
                     ",\"" + r.Ruling + "\") vs census(" + cExp + "," + cId + "," + cSide + "," +
                     cBiome + "," + cCand + "," + cRr + ",\"" + cRuling + "\")");
            }
            // mapping verification (fail-closed): the candidate re-derives
            // from the shipped biome label by the D1 rule
            string expected = null;
            if (RrInterior(cExp, (int)Json.AsLong(c["tileID"]))) expected = "BANNED";
            else if (RrExterior(cExp, (int)Json.AsLong(c["tileID"]))) expected = "OPEN";
            else if (cBiome == InteriorLabel) expected = "BANNED_CANDIDATE";
            else if (OpenLabels.Contains(cBiome)) expected = "OPEN";
            else if (AmbigLabels.Contains(cBiome)) expected = "AMBIGUOUS";
            else expected = "AMBIGUOUS";   // census law: unknown label -> unresolved candidate
            if (!OpenLabels.Contains(cBiome) && !AmbigLabels.Contains(cBiome) &&
                cBiome != InteriorLabel && !cRr)
            {
                // the shipped data grew a label neither law addresses - the
                // read is honest (AMBIGUOUS above) but the event is named.
                candErr++;
                Fail(where + ": shipped biome '" + cBiome + "' is a NEW label - the census reads it " +
                     "AMBIGUOUS, a rulebook row must classify it (spec section 7)");
            }
            else if (cCand != expected)
            {
                candErr++;
                Fail(where + ": census candidate " + cCand + " != D1 rule re-derivation " + expected);
            }
            // signature slot integrity (no shipped row is signed yet):
            if (cRuling != "")
            {
                signed++;
                if (cRuling != "BANNED" && cRuling != "OPEN" && cRuling != "AMBIGUOUS")
                {
                    Fail(where + ": thomas_ruling '" + cRuling + "' is not a status word");
                }
            }
        }
        if (drift == 0) Ok("census-vs-file equality: all " + lawRows.Length + " rows field-identical, same order");
        if (candErr == 0) Ok("mapping verification: every candidate re-derives from the shipped biome label (D1 rule)");
        if (signed == 0) Ok("signature slot: no shipped row signed yet (all rows law-free except the RR pins)");
        else Ok("signature slot: " + signed + " signed row(s), every ruling a known status word");

        // 4. RR enumeration completeness - fail-CLOSED, unmatched ids named.
        int unmatched = 0, wrong = 0;
        for (int id = 19; id <= 39; id++)
        {
            bool interior = id >= 19 && id <= 36;
            bool exterior = id == 38 || id == 39;
            if (!interior && !exterior) continue;   // id 37 carries no RR law row
            foreach (var side in new[] { "A", "B" })
            {
                string where = "Core" + id + side;
                Dictionary<int, string[]> core;
                string biome = "None";
                bool inDims = dims.TryGetValue("Core", out core) && core.ContainsKey(id);
                if (inDims) biome = core[id][side == "A" ? 0 : 1];
                CrosswalkRow row;
                bool inTable = InteriorLaw.TryRow("Core", id.ToString(), side, out row);
                string reason;
                var st = InteriorLaw.StatusForTile("Core", id.ToString(), side, biome, out reason);
                var want = interior ? TileStatus.Banned : TileStatus.Open;
                if (!inDims || !inTable || row.Biome != biome || reason != InteriorLaw.ReasonRrPin)
                {
                    unmatched++;
                    Fail("unmatched RR id " + where + ": in dimensions.json=" + inDims +
                         ", in law table=" + inTable + " (D2: mapping verification fails the build)");
                }
                else if (st != want)
                {
                    wrong++;
                    Fail("RR id " + where + " resolves " + st + " through InteriorLaw, RR text says " + want);
                }
            }
        }
        if (unmatched == 0) Ok("RR enumeration: all 40 enumerated (id,side) pairs found in dimensions.json AND the law table (fail-closed)");
        if (wrong == 0) Ok("RR enumeration: 19..36 both sides resolve Banned, 38/39 resolve Open through InteriorLaw");

        // 5. the ONE error class (spec section 7): numeric overlap must not
        //    ban a non-core physical tile. Hoth carries shipped ids inside
        //    19..36 (census section 5).
        int overlap = 0, overlapOk = 0;
        foreach (var r in lawRows)
        {
            int id;
            if (!int.TryParse(r.TileId, out id)) continue;
            if (r.Expansion == "Core" || id < 19 || id > 36) continue;
            overlap++;
            string reason;
            var st = InteriorLaw.StatusForTile(r.Expansion, r.TileId, r.Side, r.Biome, out reason);
            if (st == TileStatus.Banned)
                Fail("numeric overlap committed: " + r.Expansion + r.TileId + r.Side +
                     " inside the RR id range resolves Banned (expansion ids never join the enumeration)");
            else overlapOk++;
        }
        if (overlap > 0 && overlap == overlapOk)
            Ok("numeric-overlap guard: all " + overlap + " non-core shipped ids inside 19..36 resolve NOT-Banned");
        else if (overlap == 0)
            Fail("numeric-overlap guard found no non-core ids inside 19..36 - dimensions.json changed shape?");
    }

    static bool RrInterior(string exp, int id) { return exp == "Core" && id >= 19 && id <= 36; }
    static bool RrExterior(string exp, int id) { return exp == "Core" && (id == 38 || id == 39); }

    // ------------------------------------------------ CORE4 four-state -----
    static void RunCore4Sections()
    {
        Dictionary<string, Dictionary<int, string[]>> dims;
        try
        {
            dims = LoadDims();
        }
        catch (Exception e) { Fail("dimensions.json unreadable for the CORE4 read: " + e.Message); return; }
        Dictionary<string, object> mission;
        try
        {
            // shipped shape: one mission OBJECT (census load_mission law)
            mission = Json.AsDict(ReadJsonFile(App + "/Assets/Resources/SagaMissions/Core/CORE4.json"));
        }
        catch (Exception e) { Fail("CORE4.json unreadable: " + e.Message); return; }

        var secs = (List<object>)Json.Get(mission, "mapSections", new List<object>());
        if (secs.Count != 4)
        {
            Fail("CORE4 mapSections count=" + secs.Count + ", expected 4 (census section 4)");
            return;
        }
        var wantByName = new Dictionary<string, SectionStatusT>(StringComparer.Ordinal)
        {
            { "Start Section", SectionStatusT.Banned },          // pure interior room of the authored Weiss/AT-ST
            { "Section 2 - Hangar", SectionStatusT.Mixed },
            { "Section 3 - Warehouse", SectionStatusT.Mixed },
            { "Section 4 - Clearing", SectionStatusT.Ambiguous } // unsigned core outdoor - fail-open WITH the forensic line
        };
        foreach (var sRaw in secs)
        {
            var s = Json.AsDict(sRaw);
            string name = Json.AsStr(Json.Get(s, "name", "?"));
            var tiles = new List<TileStatus>();
            bool anyAmbig = false;
            foreach (var tRaw in Json.AsList(Json.Get(s, "mapTiles", new List<object>())))
            {
                var t = Json.AsDict(tRaw);
                string exp = Expansions[(int)Json.AsLong(Json.Get(t, "expansion", 0L))];
                string tileId = Json.AsStr(Json.Get(t, "tileID", ""));
                string side = InteriorLaw.NormalizeSide((string)Json.Get(t, "tileSide", "A"));
                // pass the SHIPPED biome from dimensions.json - the four-state
                // read is driven by real data, not by an assumed label
                string biome = "None";
                Dictionary<int, string[]> byId;
                int tid;
                if (int.TryParse(tileId, out tid) && dims.TryGetValue(exp, out byId))
                {
                    string[] bs;
                    if (byId.TryGetValue(tid, out bs)) biome = bs[side == "A" ? 0 : 1];
                }
                string reason;
                var st = InteriorLaw.StatusForTile(exp, tileId, side, biome, out reason);
                tiles.Add(st);
                if (st == TileStatus.Ambiguous) anyAmbig = true;
            }
            var got = InteriorLaw.SectionStatus(tiles);
            SectionStatusT want;
            if (!wantByName.TryGetValue(name, out want))
            {
                Fail("CORE4 unknown section name '" + name + "' - the shipped map changed under the gate");
                continue;
            }
            if (got != want) Fail("CORE4 section '" + name + "' derives " + got + ", expected " + want);
            else Ok("CORE4 section '" + name + "' derives " + got + " (tiles=" + tiles.Count + ")");
            bool foreExpected = anyAmbig || tiles.Count == 0;
            bool foreGot = InteriorLaw.SectionNeedsAmbiguousForensic(tiles);
            if (foreGot != foreExpected)
                Fail("CORE4 section '" + name + "' forensic=" + foreGot + ", expected " + foreExpected);
        }
    }
}
