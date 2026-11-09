using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
// The override-set runtime==census comparison (spec 5-H3/9.4) landed with
// P41c (card t_a2e54ec9): RunDpOverrideCensusEquality pins it BOTH direc-
// tions, RunMassiveFilterPins pins the filtered DP pools of the runtime
// rails (spec 4-D3-B v1.3) and RunSeamSourceContract pins the shipped
// seam/builder sources the fixture mirror reads - the Unity-side files
// (InteriorUnity.cs, the bridge block) are invisible to the harness, so
// THIS is where their law lines live.
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
        RunDpOverrideCensusEquality();
        RunMassiveFilterPins();
        RunSeamSourceContract();
        RunAnchorMaskContract();   // P41d (card t_59ba21ee): spec 9.3-2/9.3-3 seams + the two ui rails
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
            // census loader law (massive_interior_census.py: `e.get(biome) or
            // "None"`): missing AND empty-string labels both read "None"
            string[] biomes = { OrNone(Json.Get(d, "biomeA", null)), OrNone(Json.Get(d, "biomeB", null)) };
            Dictionary<int, string[]> byId;
            if (!dims.TryGetValue(exp, out byId)) { byId = new Dictionary<int, string[]>(); dims[exp] = byId; }
            byId[id] = biomes;
        }
        return dims;
    }

    static string OrNone(object v)
    {
        var s = v as string;
        return string.IsNullOrEmpty(s) ? "None" : s;
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
            string expected;
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
                int ei = (int)Json.AsLong(Json.Get(t, "expansion", 0L));
                if (ei < 0 || ei >= Expansions.Length)
                {
                    // census loader guard (massive_interior_census.py names the
                    // bad index instead of dying): fail the check, keep the run
                    Fail("CORE4 tile carries unknown expansion index " + ei + " - shipped data changed shape");
                    continue;
                }
                string exp = Expansions[ei];
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

    // ---------------- P41c: D6 override set == census set (spec 9.4 last ----
    // ---------------- bullet; controller note from P41b-QA, card t_a2e54ec9) -
    // THE equality the whole interior law builds on, in BOTH directions: the
    // InteriorDpProbe (a third, independent C# engine over the shipped mission
    // files + CardData, mirroring every authored-channel rule of the Python
    // census incl. sentinel lanes, the Active-spot editor-leftover rule and
    // the custom-lane authored keywords) must derive EXACTLY the census
    // section 6 override rows - a row the probe finds but the census does not
    // (or vice versa) fails the build and is NAMED. The 22 rows below are
    // hand-pinned from the P41a census run (docs/MASSIVE_interior_census.md
    // section 6, reviewed once - the wantByName discipline of this gate
    // family). Columns: mission|channel|cardID|dpGuid|tile|dp_kind|pinned.
    static readonly string[] AuthoredOverridePin =
    {
        "CORE1|event6|DG073|2252d135-34c0-437b-b7fc-4c850c6c3f24|Core14A|section|UNKNOWN",
        "CORE11|event6|DG014|9eda6375-5593-488d-9833-572a5104767f|Core2A|single|UNKNOWN",
        "CORE13|initial|DG014|67a37723-c6ab-438b-a4b8-60740ff43637|Core6A|status_uniform|UNKNOWN",
        "CORE16|event6|DG014|dd42f18e-e47b-4665-a0bf-0b4616624a14|Core4A|single|UNKNOWN",
        "CORE4|event21|fbc990e4-bb60-4996-9471-1c83ec228930|1390e70b-96fc-4759-a422-129c5eb175cd|Core19A|single|BANNED",
        "CORE4|event6|DG073|1390e70b-96fc-4759-a422-129c5eb175cd|Core19A|single|BANNED",
        "CORE4|event6|DG073|5259f0cc-9c65-4885-bf33-5a9b15aa78a2|Core2B|single|UNKNOWN",
        "EMPIRE1|event6|DG046|03dd838d-6ff6-4f89-a742-d64765178338|Empire1B|status_uniform|UNKNOWN",
        "EMPIRE13|event6|DG046|a8142ab6-52dc-43a9-8ed5-872cdf85a9a2|Empire8B|status_uniform|UNKNOWN",
        "EMPIRE4|event6|DG046|ded513cc-509e-4eb2-8c4a-2e29c559bcf2|Empire1B|status_uniform|UNKNOWN",
        "HOTH1|event21|88e2c662-66f1-4817-99c6-44d2dd36653e|57260ede-8508-43a0-aadb-3d1c46ef1cff|Hoth2A|status_uniform|UNKNOWN",
        "HOTH4|event6|DG025|e0d55fc0-2bce-4d74-a5a2-f0429c5fe941|Hoth20A|single|UNKNOWN",
        "HOTH8|event6|DG014|2bbe3a60-59f3-4744-b0f1-f7be8d56de16|Hoth20A|single|UNKNOWN",
        "HOTH8|event6|DG025|ef12143f-4674-41d9-ba04-2cf57310cf5c|Hoth5A|status_uniform|UNKNOWN",
        "JABBA11|event6|DG045|08528482-2f97-499b-b187-b994e0562bcb|Jabba4A|single|UNKNOWN",
        "JABBA12|event6|DG045|1a803129-7fa5-4d3a-8209-0346417b4ef1||ambiguous_mixed|AMBIG_MIXED",
        "JABBA16|event6|DG045|b47c341a-d8ec-41a3-8aeb-4c4bda7b8a61|Core16A|single|UNKNOWN",
        "JABBA9|event6|DG045|1b951ae3-9291-4a3a-980f-a1926fc87f61|Jabba3B|single|UNKNOWN",
        "OTHER16|event6|DG073|bde8faad-b2f6-4b5e-81a8-02f44626ac31|Core6A|single|UNKNOWN",
        "OTHER2|event6|DG014|7c932f59-9f05-4517-aa26-070303a417ec|Jabba8B|single|UNKNOWN",
        "OTHER25|event6|DG065|45d23632-7360-4164-bfda-5bc09be0f3b4|Core15B|single|UNKNOWN",
        "OTHER25|event6|DG065|da91186d-606b-46ff-b8d2-06dac5a2fb10|Twin4B|status_uniform|UNKNOWN",
    };

    // The builder-lane registration outcome over those rows (InteriorUnity's
    // exact RegisterAuthored path through the shipped law): ONLY the two
    // VERIFIED-BANNED (RR-pinned Core19A) rows lift; every other authored row
    // lands as override_unverified (D6 v1.3 fail-CLOSED - a lifted ban would
    // mean the law read Banned where the census says no ban).
    static readonly string[] RegisteredPin =
    {
        "CORE4.json:DG073:1390e70b-96fc-4759-a422-129c5eb175cd",
        "CORE4.json:fbc990e4-bb60-4996-9471-1c83ec228930:1390e70b-96fc-4759-a422-129c5eb175cd",
    };

    static void RunDpOverrideCensusEquality()
    {
        List<InteriorDpProbe.MissionProbe> all;
        try { all = InteriorDpProbe.ProbeAll(); }
        catch (Exception e) { Fail("dp probe fail-closed: " + e.Message); return; }
        Ok("dp probe: all " + all.Count + " shipped missions walked fail-closed");
        if (all.Count != 138) Fail("mission count " + all.Count + " != 138 shipped");

        // ---- direction 1+2: probe rows == pinned census rows, both ways ----
        var probeRows = new SortedSet<string>(StringComparer.Ordinal);
        var detail = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mp in all)
            foreach (var p in mp.Authored)
            {
                var key = mp.Name.Replace(".json", "") + "|" + p.Channel + "|" + p.CardId + "|" + p.Dp;
                probeRows.Add(key);
                var dp = mp.Dps[p.Dp];
                detail[key] = (dp.TileRef ?? "") + "|" + dp.Kind + "|" +
                              (dp.Status == TileStatus.Banned ? "BANNED"
                               : dp.Status == TileStatus.Open ? "OPEN" : "NOT_BANNED");
            }
        var pinRows = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var row in AuthoredOverridePin)
        {
            var c = row.Split('|');
            var key = c[0] + "|" + c[1] + "|" + c[2] + "|" + c[3];
            pinRows.Add(key);
            // Field equality. single/strict/section lanes are deterministic
            // in BOTH engines -> tile + kind must land 1:1. The
            // status_uniform/ambiguous_mixed lane is engine-specific in its
            // IDENTITY read (the census uniformity test is the (pinned,
            // candidate) word PAIR, the shipped law is the 3-state word -
            // they can disagree on WHICH tile the pool names while agreeing
            // on the status, and the ban law only ever consumes the status):
            // for those lanes the shared, pinworthy law is the ban word.
            var lane = c[5];
            var got = detail.ContainsKey(key) ? detail[key] : "<missing>";
            var wantWord = c[6] == "BANNED" ? "BANNED"
                           : c[6] == "OPEN" ? "OPEN" : "NOT_BANNED";
            if (lane == "status_uniform" || lane == "ambiguous_mixed" || lane == "tileless")
            {
                var parts = got.Split('|');
                var word = parts.Length == 3 ? parts[2] : got;
                if (word != wantWord)
                    Fail("override row " + row + " | probe law word " + word + ", census says " + wantWord);
            }
            else
            {
                var want = c[4] + "|" + c[5] + "|" + wantWord;
                if (got != want)
                    Fail("override row " + row + " | probe reads " + got + ", census says " + want);
            }
        }
        foreach (var k in pinRows)
            if (!probeRows.Contains(k)) Fail("census override row has NO probe twin: " + k);
        foreach (var k in probeRows)
            if (!pinRows.Contains(k)) Fail("probe override row has NO census twin: " + k);
        Ok("override rows == census rows both directions (" + AuthoredOverridePin.Length + " rows)");

        // ---- builder-lane registration outcome through the shipped law ----
        var reg = new SortedSet<string>(StringComparer.Ordinal);
        var unv = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var mp in all)
        {
            InteriorLaw.OverrideSet set;
            List<string> r, u;
            InteriorDpProbe.RegisterAuthored(mp, out set, out r, out u);
            foreach (var x in r) reg.Add(x);
            foreach (var x in u) unv.Add(x);
        }
        foreach (var x in RegisteredPin)
            if (!reg.Contains(x)) Fail("VERIFIED-BANNED authored row did NOT register: " + x);
        foreach (var x in reg)
            if (Array.IndexOf(RegisteredPin, x) < 0) Fail("unexpected registration: " + x);
        Ok("D6 registration set == pinned (" + reg.Count + " registered)");
        if (unv.Count != AuthoredOverridePin.Length - RegisteredPin.Length)
            Fail("override_unverified count " + unv.Count + " != " +
                 (AuthoredOverridePin.Length - RegisteredPin.Length));
        else Ok("all " + unv.Count + " non-VERIFIED-BANNED authored rows stay unverified (fail-closed, no lift)");

        // ---- totals cross-check (census stdout: dps=1660 override_rows=22) --
        int sumDps = all.Sum(m => m.Dps.Count);
        if (sumDps != 1660) Fail("total shipped DPs " + sumDps + " != census 1660");
        else Ok("total shipped DPs == census 1660");
        int sumBanned = all.Sum(m => m.BannedByDp.Count);
        if (sumBanned != 481) Fail("total Banned-mapped DPs " + sumBanned + " != census-derived 481");
        else Ok("total Banned-mapped DPs == 481 (RR-core pinned DP seats)");
    }

    // ------------------------------------------------ filtered DP pools -----
    // P41c (card t_a2e54ec9; spec 4-D3-B v1.3 "Rails to cover" + 9.4 last
    // bullet). The bridge's DP hard filter sits at the ONE seam every runtime
    // rail passes through (GetActiveDeploymentPoint -> TryChooseDeployment-
    // Point, pinned by RunSeamSourceContract), so per mission the rails feed
    // ONE pool: the authored-active DP pool minus the Banned-tile DPs (no
    // override). The table below pins that filtered set for the whole shipped
    // corpus, straight from the DP probe's first run (third engine, hand-
    // pinned like the crosswalk table - a moved DP placement fails here).
    static readonly string[] FilterPoolPin =
    {
        // mission-file | authored-active DPs | Banned among them | Banned GUIDs (ordinal)
        "BESPIN1.json|6|4|345e2621-e692-4c84-a0e4-b46f49c04387,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,7b29df31-50bf-45fa-8cfc-9f0a98e527a0,c0c065ed-7859-4d9b-a290-dba5f6ef5ad0",
        "BESPIN2.json|3|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2",
        "BESPIN3.json|2|1|d40f0588-3761-4977-91f6-5d8dbe09b59c",
        "BESPIN4.json|4|3|2e35c1c1-e257-4195-ae0a-0b94cbd3e374,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2",
        "BESPIN5.json|4|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "BESPIN6.json|2|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "CORE10.json|3|3|1c35bb0a-2f6f-478d-a31f-f042f7f2092a,9e551b22-5830-4da5-a5e1-5e8cc5c040ea,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE12.json|3|1|b4993597-fee3-44fc-81d9-42ea8cac7940",
        "CORE14.json|3|3|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE19.json|2|2|2d9c8b6a-3ec8-4786-8c6a-793752910de3,f9bf47e5-6555-47de-8500-135928e5d313",
        "CORE20.json|3|2|096beea1-a205-4fdd-88ed-2cad24533f45,531d543f-b3a1-47b5-9016-a07d1eefd16e",
        "CORE21.json|1|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE23.json|3|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE24.json|2|2|773794a5-f00e-45fe-8a05-2599293f083e,d6104e82-134f-4010-855c-bf56350f74ce",
        "CORE25.json|8|3|707bc37d-fd4c-4eca-9fc4-598548e51017,ba5e2aed-1eb8-4945-bd97-eee88724ec2e,c94c4371-3c65-44d5-9353-b66127d64738",
        "CORE26.json|3|3|2038cf68-5c40-454c-81bf-7cbab4266145,254a61d3-04ff-405b-b9a5-acd4debb522b,36e50a1c-586e-49e4-acff-4c513f033724",
        "CORE28.json|2|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "CORE29.json|3|3|29cd84c0-58f9-47e9-8ced-432f5515d9e9,d3d0ae17-f9cd-4516-8d5c-01b385b5080d,e78a0a86-e764-49fd-82f2-77fe019574e4",
        "CORE3.json|3|3|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE30.json|3|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE32.json|1|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE4.json|4|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE5.json|4|4|2e35c1c1-e257-4195-ae0a-0b94cbd3e374,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE7.json|2|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "CORE8.json|3|1|dfe8bc3a-fc2e-43b2-a275-c18e302d8c7b",
        "CORE9.json|4|3|533e29c6-441e-4bbf-bf0f-81cc06fb4b2a,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,748927fe-d4fd-468c-8ab1-5a82bb954ca9",
        "EMPIRE1.json|12|4|16d4fc00-dcf0-4848-8213-86be26d5c626,3676372a-1be6-4730-aa49-5e81fc638cff,3f0b8cbb-334f-4480-9f48-1209072af5d9,ac10b1ec-64de-4ad8-acf1-6d33eda75e91",
        "EMPIRE10.json|4|2|0a190ebe-515f-488f-99d3-fccf849b86b5,f930016a-0f7d-455f-8de9-5468b1af5fa7",
        "EMPIRE15.json|4|2|ad623ae2-dfaa-4387-8424-9da412e3060c,e639bc6c-c230-4461-8f5b-04baf9ae8b70",
        "EMPIRE16.json|5|2|58a56a1f-5c4b-4504-a296-3cd098f27824,674b8b91-cded-4b3a-b28b-d44e68c95f74",
        "EMPIRE3.json|6|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "EMPIRE6.json|8|6|2e35c1c1-e257-4195-ae0a-0b94cbd3e374,3d1d97c7-7847-495e-874d-350c919eb61b,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,9908c08b-ccdd-401f-8162-a06138d2a33a,b37863a7-3faa-4c5b-b2ec-6256560de146,b9857a1f-3670-4bab-a53a-368108242e83",
        "EMPIRE8.json|1|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "EMPIRE9.json|35|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "HOTH13.json|3|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "HOTH4.json|7|3|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "HOTH5.json|5|2|dcd58dac-f855-4582-9195-c8d30a25c54b,fc27b286-ed82-46d0-86ed-7d51ecafef08",
        "JABBA1.json|9|2|c8062d94-a52f-4458-b2cf-3c7b515747e5,dd1cde18-31f7-40af-b870-c40679beb7f0",
        "JABBA14.json|5|2|71503db5-2165-418b-a6d8-102cf990ba19,e98b1127-0ca0-4b0e-aa9e-543c8c207f9a",
        "JABBA15.json|2|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "JABBA2.json|8|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "JABBA5.json|3|1|64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2",
        "JABBA7.json|10|1|0edc5995-7760-4611-bfb0-0d9323942ae4",
        "JABBA8.json|4|2|c38d0068-5f0e-4a77-a21d-04e622adc520,ee06ae5c-c814-4c37-bb11-ee096e716915",
        "JABBA9.json|9|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "LOTHAL3.json|5|2|4c9dc91b-9115-404a-a37e-8f06cbb07c45,d1899911-c671-40f9-ba86-b113bdd9518a",
        "LOTHAL4.json|4|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "LOTHAL5.json|3|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER1.json|3|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER12.json|4|1|a523f2be-ada0-4ece-a32c-8813d0a2a60f",
        "OTHER13.json|5|3|38914952-e3f1-4b24-8c7e-9fc587efe1ec,4c600620-9135-41f6-a6bc-4bce2141e1b9,754b68c8-b523-4c3a-872e-7d86fa29a1fd",
        "OTHER14.json|4|4|2f23baa7-0857-46ae-a79a-3f0c3d8ef0e3,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,83857f4a-bd42-4930-92ed-87109bbc48e1,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER15.json|6|2|a2ab878c-79d9-43f3-9a6e-aa64a4eef7b2,d486b2a6-0001-4a6b-9172-983eb7a7b671",
        "OTHER19.json|4|1|aa5c6e13-ae44-4593-8b03-3baba0ba326c",
        "OTHER20.json|2|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER21.json|4|3|2c80523c-0fcb-4d95-947f-08909912bede,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,d47765c7-792b-44c8-9a94-2a29928e5449",
        "OTHER22.json|5|1|64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2",
        "OTHER24.json|6|4|2e35c1c1-e257-4195-ae0a-0b94cbd3e374,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c1724cd9-17cd-478a-87aa-2bf631d25013,e86e413d-50d1-462a-949d-7eadf3228ecc",
        "OTHER26.json|3|1|114be456-88e4-4bb8-81b4-c3ca3186bd90",
        "OTHER27.json|4|2|98aa7b67-4208-466d-91d9-34db2b0c42e9,ac4c7f78-1a60-4faf-b94f-beb3f14f0777",
        "OTHER28.json|3|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER29.json|3|1|63923376-f691-466d-af92-e30b36bea0df",
        "OTHER3.json|3|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER30.json|5|4|114faa29-2c9a-4422-904d-fdd1915c70dc,2e35c1c1-e257-4195-ae0a-0b94cbd3e374,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2",
        "OTHER33.json|5|5|1be7eabf-530e-44dd-b1d6-bfdcedde0f1d,28a70da5-feac-4fa0-a1ea-8c683aba98f2,35ee83a5-30ce-48b4-907b-ccc47e77e3ad,a63cc7fd-07e5-46c7-9781-ddb412bd0e4c,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER35.json|3|3|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5,d7ebcb77-84f8-42b8-a7b2-133ae1952eee",
        "OTHER36.json|5|2|8ce1e2f1-7a86-4e0f-a582-c1560288f3d0,f66bc97e-78dd-40c1-a1a8-ff28d8482f69",
        "OTHER37.json|2|2|64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER39.json|5|1|c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER4.json|4|4|a43ebea6-b1cf-4776-aa62-0f09dbdd6554,c8062d94-a52f-4458-b2cf-3c7b515747e5,f65f6be9-6a8f-4a73-9974-8af08307e5ff,f7900d85-8c56-49ca-87dc-9430a2df56a3",
        "OTHER40.json|5|5|2e35c1c1-e257-4195-ae0a-0b94cbd3e374,5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,8b502450-a1df-4cb0-9cd4-c9412392ecff,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER5.json|2|2|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "OTHER7.json|4|2|6d502bbb-3807-4243-8ede-676f0b1557fa,9a453ac7-3eed-4997-a7ea-aa92367f9f5a",
        "OTHER8.json|3|1|36d05822-7bb5-4387-993b-cdaa85609fd8",
        "TWIN2.json|3|1|5bdffef2-c7c7-4e16-ab66-dca82b0481fd",
        "TWIN4.json|8|4|0bf24492-f8ec-40a5-964d-327c6421f19f,70f2c672-3792-4a55-87f2-b1de449bc43c,c5ce6da4-f003-41b4-8ebb-d523ca3a1981,d8fb385b-cccc-4ecc-a57b-58a701642533",
        "TWIN5.json|4|3|5bdffef2-c7c7-4e16-ab66-dca82b0481fd,64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2,c8062d94-a52f-4458-b2cf-3c7b515747e5",
        "TWIN6.json|5|2|d1bd8711-c951-4fa9-8018-7a7217f64c97,e48bdeff-3417-47ee-8c15-6561dc1bee3c",
    };

    static void RunMassiveFilterPins()
    {
        var all = InteriorDpProbe.ProbeAll();       // tree under test (CWD-first)
        var lines = new List<string>();
        int sumActive = 0, sumBannedActive = 0;
        foreach (var mp in all)
        {
            var bannedActive = mp.ActiveDps
                .Where(g => mp.Dps[g].Status == TileStatus.Banned)
                .OrderBy(g => g, StringComparer.Ordinal).ToList();
            sumActive += mp.ActiveDps.Count;
            sumBannedActive += bannedActive.Count;
            if (bannedActive.Count > 0)
                lines.Add(mp.Name + "|" + mp.ActiveDps.Count + "|" + bannedActive.Count + "|" +
                          string.Join(",", bannedActive));
        }
        bool tableOk = lines.Count == FilterPoolPin.Length;
        if (!tableOk)
            Fail("filtered-pool table: " + lines.Count + " missions with Banned active DPs != pinned " + FilterPoolPin.Length);
        int mismatch = 0;
        for (int i = 0; tableOk && i < lines.Count; i++)
            if (lines[i] != FilterPoolPin[i])
            {
                if (mismatch < 5) Fail("filtered-pool row " + i + "\n      want: " + FilterPoolPin[i] + "\n      got:  " + lines[i]);
                mismatch++;
            }
        if (tableOk && mismatch == 0)
        {
            Ok("filtered-pool table: " + lines.Count + " missions, all rows field-identical to the pin (active/banned-active/Banned-GUID sets)");
        }
        else if (mismatch > 0) Fail("filtered-pool row mismatches: " + mismatch);

        if (sumActive != 523) Fail("authored-active DP total " + sumActive + " != pinned 523");
        else Ok("rails 2-5 feed one authored-active pool per mission: 523 DPs across the corpus");
        if (sumBannedActive != 167) Fail("Banned-among-active total " + sumBannedActive + " != pinned 167");
        else Ok("167 of them sit Banned: the hard filter's corpus delta for Massive cards without a D6 override");

        // CORE4 lane (the census' authored-indoor mission): DG073 keeps the
        // FULL pool (D6 override from the authored indoor placement), DG014
        // (no authored row) loses its two Banned greens - the pool the rails
        // feed differs per card, exactly as the bridge drop rule computes it.
        var c4 = all.First(m => m.Name == "CORE4.json");
        InteriorLaw.OverrideSet ovr4; List<string> reg4, unv4;
        InteriorDpProbe.RegisterAuthored(c4, out ovr4, out reg4, out unv4);
        if (!ovr4.Covers("DG073")) Fail("CORE4 lane: authored indoor Massive DG073 not covered - override lane broken");
        else Ok("CORE4 lane: DG073 (authored indoor, D6 override) keeps the FULL 4-DP pool");
        if (ovr4.Covers("DG014")) Fail("CORE4 lane: DG014 must NOT be covered (no authored row in CORE4)");
        else Ok("CORE4 lane: DG014 (no authored row) drops to the 2 survivor pool");
        var c4bannedActive = c4.ActiveDps.Where(g => c4.Dps[g].Status == TileStatus.Banned)
            .OrderBy(g => g, StringComparer.Ordinal).ToList();
        if (c4bannedActive.Count != 2) Fail("CORE4 lane: Banned active DPs " + c4bannedActive.Count + " != 2");
        else Ok("CORE4 lane: exactly the two authored-active greens sit on Banned RR tiles");
        var yellow = "1390e70b-96fc-4759-a422-129c5eb175cd";   // authored DG073 seat
        if (c4.Dps[yellow].Status != TileStatus.Banned || c4.Dps[yellow].TileRef != "Core19A" ||
            c4.ActiveDps.Contains(yellow))
            Fail("CORE4 lane: DP Yellow moved (ref=" + c4.Dps[yellow].TileRef +
                 " status=" + c4.Dps[yellow].Status + " active=" + c4.ActiveDps.Contains(yellow) + ")");
        else Ok("CORE4 lane: the authored seat itself is Core19A/Banned/authored-INACTIVE - the override guards its event-activation moment");

        // CORE3 lane: every authored-active DP is Banned - the ONLY mission
        // of census section 7 whose Massive groups have no DP outside the
        // banned set. Zero usable is an HONEST deploy fail (no spawn, threat
        // stays reserved), never a silent pick onto a Banned tile.
        var c3 = all.First(m => m.Name == "CORE3.json");
        if (c3.ActiveDps.Count != 3 || !c3.ActiveDps.All(g => c3.Dps[g].Status == TileStatus.Banned))
            Fail("CORE3 lane: zero-usable set moved (active=" + c3.ActiveDps.Count + ")");
        else Ok("CORE3 lane: all 3 authored-active DPs Banned -> zero-usable -> honest deploy fail, no silent banned spawn");
    }

    // ------------------------------------------------ source contract -------
    // The Unity-side files (InteriorUnity.cs, the bridge block) are invisible
    // to BOTH harness and gate compile contexts - the fixture mirror copies
    // the drop rule by hand. These probes pin the shipped law lines so a
    // drift fails the build (the contract the SectionsInterior header names).
    // Exact-text probes, never line numbers. Same tree-under-test rule as
    // the DP probe (InteriorDpProbe.DataRoot: CWD first).
    public static string ReadSrc(string rel)
    {
        return File.ReadAllText(Path.Combine(InteriorDpProbe.DataRoot(), rel));
    }

    // occurrences of `needle` on non-comment lines (the commented-out legacy
    // card-less seam call in DeploymentManager is history, not a caller).
    public static int CountActive(string src, string needle)
    {
        int n = 0;
        foreach (var line in src.Split('\n'))
        {
            var t = line.TrimStart();
            if (t.StartsWith("//")) continue;
            int idx = 0;
            while ((idx = t.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) { n++; idx += needle.Length; }
        }
        return n;
    }

    public sealed class RailFactsT
    {
        public int DepManWithCard;      // rails 2-4: card-supplying seam callers
        public int DepManCardlessLive;  // must stay 0 (NEW-Ground-2 law)
        public int PopupReRoll;         // rail 5: optional-window announce re-roll
        public int PopupInteriorRefs;   // the popup must NEVER grow its own filter
        public int SeamCalls;           // MapEntityManager's tactical seam calls
        public int FilterDpCalls;       // legacy props-filter law (two call sites)
        public bool LegacyRandom;       // the tactical-OFF line, verbatim
        public int SumActive;
        public int SumBannedActive;
        public int MissionsWithBannedActive;
        public bool Core4CoversDg073;
        public bool Core4CoversDg014;
    }

    public static RailFactsT CollectRailFacts()
    {
        var f = new RailFactsT();
        var dm = ReadSrc("ImperialCommander2/Assets/Scripts/Saga/Managers/DeploymentManager.cs");
        f.DepManWithCard = CountActive(dm, "GetActiveDeploymentPoint( enemyToAdd )");
        f.DepManCardlessLive = CountActive(dm, "GetActiveDeploymentPoint()");
        var popup = ReadSrc("ImperialCommander2/Assets/Scripts/Saga/UI/SagaDeploymentPopup.cs");
        f.PopupReRoll = CountActive(popup, "GetActiveDeploymentPoint( card )");
        f.PopupInteriorRefs = CountActive(popup, "InteriorLaw") + CountActive(popup, "InteriorUnity") +
                              CountActive(popup, "dp-massive") + CountActive(popup, "TileStatus");
        var mem = ReadSrc("ImperialCommander2/Assets/Scripts/Saga/Managers/MapEntityManager.cs");
        f.SeamCalls = CountActive(mem, "TryChooseDeploymentPoint(");
        f.FilterDpCalls = CountActive(mem, "dps = FilterDP(");
        f.LegacyRandom = mem.IndexOf("dps[GlowEngine.GenerateRandomNumbers( dps.Count )[0]].GUID",
                                     StringComparison.Ordinal) >= 0;
        foreach (var mp in InteriorDpProbe.ProbeAll())
        {
            int b = mp.ActiveDps.Count(g => mp.Dps[g].Status == TileStatus.Banned);
            f.SumActive += mp.ActiveDps.Count;
            f.SumBannedActive += b;
            if (b > 0) f.MissionsWithBannedActive++;
            if (mp.Name == "CORE4.json")
            {
                InteriorLaw.OverrideSet ovr; List<string> reg, unv;
                InteriorDpProbe.RegisterAuthored(mp, out ovr, out reg, out unv);
                f.Core4CoversDg073 = ovr.Covers("DG073");
                f.Core4CoversDg014 = ovr.Covers("DG014");
            }
        }
        return f;
    }

    static void RunSeamSourceContract()
    {
        // --- the bridge block: mode gate first, card==null probe lane next,
        // the P41c filter in front of the candidate-line building.
        var bridge = ReadSrc("ImperialCommander2/Assets/Scripts/TacticalAI/TacticalAIBridge.cs");
        int iGuard = bridge.IndexOf("|| !ActiveNow()) return false;", StringComparison.Ordinal);
        int iNull  = bridge.IndexOf("if (card == null) return false;", StringComparison.Ordinal);
        int iBuild = bridge.IndexOf("var interior = EnsureInteriorSnapshot();", StringComparison.Ordinal);
        int iLaw   = bridge.IndexOf("bool drop = !covered && st == Tac.TileStatus.Banned;", StringComparison.Ordinal);
        int iForen = bridge.IndexOf("\"[dp-massive] card=\"", StringComparison.Ordinal);
        int iFail  = bridge.IndexOf("zero-usable-dps", StringComparison.Ordinal);
        int iHonest = bridge.IndexOf("chosen=Guid.Empty, honest no-deploy", StringComparison.Ordinal);
        int iCand  = bridge.IndexOf("cands.Add(new Tac.DeployCandidate", StringComparison.Ordinal);
        if (iGuard < 0 || iNull < 0 || iBuild < 0 || iLaw < 0 || iForen < 0 || iFail < 0 || iHonest < 0 || iCand < 0)
            Fail("bridge seam contract: marker missing (guard=" + iGuard + " null=" + iNull +
                 " build=" + iBuild + " law=" + iLaw + " forensic=" + iForen +
                 " zerofail=" + iFail + " honest=" + iHonest + " cands=" + iCand + ")");
        else if (!(iGuard < iNull && iNull < iBuild && iBuild < iLaw && iLaw < iForen &&
                   iForen < iFail && iFail < iHonest && iHonest < iCand))
            Fail("bridge seam contract: block order moved - the interior filter belongs behind the mode gate and in front of the candidate rows");
        else Ok("bridge: interior block behind ActiveNow gate + card==null probe lane, in front of candidate rows; drop law, [dp-massive] forensic, zero-usable honest no-deploy (chosen=Guid.Empty, never false - MAJOR-4) all pinned");

        // --- the rails census (spec 4-D3-B v1.3): every REAL deploy supplies
        // the card; one seam; legacy law intact.
        var f = CollectRailFacts();
        if (f.DepManWithCard != 3) Fail("DeploymentManager card-supplying seam callers " + f.DepManWithCard + " != 3 (attack/reinforce, popup-event/landing/onslaught, DoMultipleDeployment)");
        else Ok("rails 2-4: all three GetActiveDeploymentPoint( enemyToAdd ) callers supply the card");
        if (f.DepManCardlessLive != 0) Fail("live card-less seam caller appeared (" + f.DepManCardlessLive + ") - NEW-Ground-2 law: card==null stays existence-probes only");
        else Ok("no live card-less seam caller (the commented-out legacy call stays a comment)");
        if (f.PopupReRoll != 1) Fail("optional-window re-roll callers " + f.PopupReRoll + " != 1");
        else Ok("rail 5 (M13c optional window): the popup re-roll supplies the card to the same seam");
        if (f.PopupInteriorRefs != 0) Fail("the popup grew its own interior logic (" + f.PopupInteriorRefs + " refs) - the filter lives at the seam ONLY");
        else Ok("rail 5 inherits the hard filter through the single seam (no interior logic in the popup)");
        if (f.SeamCalls != 1) Fail("tactical seam call sites in MapEntityManager " + f.SeamCalls + " != 1");
        else Ok("single tactical seam: one TryChooseDeploymentPoint call site - no rail can bypass the hard filter");
        if (f.FilterDpCalls != 2) Fail("FilterDP call sites " + f.FilterDpCalls + " != 2 (legacy props law must stay)");
        else Ok("legacy FilterDP law untouched (both pre-seam call sites) - the tile filter is a SECOND layer, not a replacement");
        if (!f.LegacyRandom) Fail("legacy random fallback line moved - tactical-OFF bit-identity broken at the source");
        else Ok("legacy random line intact verbatim (tactical-OFF bit-identity, source side)");

        // --- MAJOR-4 caller contract (fresh-look 09.11): the caller's
        // count-guard at :521 passes BEFORE the seam call, so a bridge that
        // empties the pool in-place and returns FALSE would hand control to
        // the legacy draw dps[GenerateRandomNumbers(dps.Count)[0]] with an
        // EMPTY list - GlowEngine.GenerateRandomNumbers(0) returns [0]
        // (GlowEngine.cs:334-341) and dps[0] IndexRange-crashes the deploy;
        // restoring the pool instead would let the same draw pick a BANNED
        // DP (silent ban bypass). Pinned: the guard sits between nothing and
        // the seam, the legacy draw AFTER the seam (structure that makes the
        // quirk reachable) AND the bridge's zero-usable lane answers TRUE
        // with the app's own no-DP marker (chosen=Guid.Empty) - the only
        // honest exit. Both wrong turns additionally stay closed by the
        // harness vector (interior dp_filter zero-usable).
        var mem = ReadSrc("ImperialCommander2/Assets/Scripts/Saga/Managers/MapEntityManager.cs");
        int iEmptyGuard = mem.IndexOf("if ( dps.Count > 0 )", StringComparison.Ordinal);
        int iSeam = mem.IndexOf("TryChooseDeploymentPoint(", StringComparison.Ordinal);
        int iRndDraw = mem.IndexOf("dps[GlowEngine.GenerateRandomNumbers( dps.Count )[0]].GUID", StringComparison.Ordinal);
        if (!(iEmptyGuard >= 0 && iEmptyGuard < iSeam && iSeam < iRndDraw))
            Fail("caller contract: guard/seam/legacy-draw order moved - the structural reason the zero-usable lane must NEVER return false (MAJOR-4)");
        else Ok("caller contract pinned: count-guard precedes the seam, legacy draw follows it - zero-usable cannot use the false lane (MAJOR-4 structure)");

        // --- the builder (InteriorUnity.cs): Unity-side law lines.
        var ib = ReadSrc("ImperialCommander2/Assets/Scripts/TacticalAI/InteriorUnity.cs");
        if (CountActive(ib, "FindObjectsOfTypeAll<Saga.TileRenderer>()") != 1) Fail("builder: the TileRenderer scan (the only legal object scan - MapTile is plain) moved");
        else Ok("builder scans the placed map through TileRenderer.mapTile (M13r MedPac pattern, FindObjectsOfTypeAll keeps hidden tiles)");
        if (CountActive(ib, "FindObjectsOfTypeAll<MapTile>") >= 1) Fail("builder: live FindObjectsOfTypeAll<MapTile> - MapTile is a plain class, it does not compile");
        else Ok("builder: no live FindObjectsOfTypeAll<MapTile> (plain-class law; the header note naming the forbidden form stays a comment)");
        // D6 lane (MAJOR-1/2/3 fix, fresh-look 09.11): the authored lanes
        // come from the MISSION'S OWN JSON (missionStringified, the exact
        // string ParseMission consumes) mirroring InteriorDpProbe 353-425 -
        // NOT from deployLedger, whose rows are campaign-scoped (stale lifts)
        // and whose event lane only lands at deployment time (after the
        // build). deployLedger consumption in the builder is a regression.
        if (CountActive(ib, "deployLedger") != 0)
            Fail("builder consumes deployLedger again - campaign-scoped rows leak lifts across missions (MAJOR-1/2, fresh-look 09.11)");
        else Ok("builder D6 source = mission's own lanes, no deployLedger (mission-scoped by construction, event lanes complete at t=0 - MAJOR-1/2)");
        if (ib.IndexOf("missionStringified", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("\"eventActionType\"", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("SectionSentinel", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("NullGuid", StringComparison.Ordinal) < 0)
            Fail("builder: the mission-lane law moved (missionStringified / eventActionType lanes / sentinel rules - InteriorDpProbe mirror)");
        else Ok("builder mission lanes mirror InteriorDpProbe 353-425: missionStringified, eventActionType 6/7/21, sentinel + Specific rules pinned");
        if (ib.IndexOf("[interior] override_unverified", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("RegisterAuthored", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("isMassiveCard == null || !isMassiveCard(", StringComparison.Ordinal) < 0)
            Fail("builder: the fail-closed D6 lane moved (override_unverified / RegisterAuthored / spec-9.2 IsMassive gate)");
        else Ok("builder D6 lane pinned: fail-closed registration, [interior] override_unverified stays, IsMassive gate (MAJOR-3)");
        // The Massive view must be HANDED to the builder - a null predicate
        // registers nothing (fail-closed), which would silently strip every
        // legitimate CORE4-style lift if the bridge ever stopped passing it.
        if (CountActive(bridge, "CatalogCardIsMassive);") != 1)
            Fail("bridge no longer hands CatalogCardIsMassive to InteriorUnity.Build - the IsMassive gate would fail closed on every real lift (MAJOR-3)");
        else Ok("bridge hands the runtime Massive view to the builder (spec 9.2 IsMassive gate live)");
        if (ib.IndexOf("[interior] massive-override", StringComparison.Ordinal) < 0)
            Fail("builder: the D6 forensic line (spec 'massive-override::card<-mission') moved");
        else Ok("builder forensic lane pinned: [interior] massive-override registers the (mission, card) lift");
        if (ib.IndexOf("[interior] snapshot-empty", StringComparison.Ordinal) < 0 ||
            ib.IndexOf("[interior] snapshot tiles=", StringComparison.Ordinal) < 0)
            Fail("builder: the Null-Law / summary log lines moved");
        else Ok("builder Null-Law lane pinned: empty index -> empty snapshot -> one log line, law inactive");
    }

    // ------------------------------------------- P41d anchor-mask contract --
    // spec 9.3-2 (goal masking + seal at the HeroInSection read sites),
    // 9.3-3 (REPLAN fact 4 behind the three feed facts) and the two ui.json
    // rails. Exact-text probes, never line numbers; the bridge lines live
    // in no fixture, so THIS is their law home (same rule as
    // RunSeamSourceContract). Same tree-under-test rule: ReadSrc resolves
    // through InteriorDpProbe.DataRoot (CWD first -> the worktree).
    static void RunAnchorMaskContract()
    {
        var bridge = ReadSrc("ImperialCommander2/Assets/Scripts/TacticalAI/TacticalAIBridge.cs");

        // --- the helper: one decision, gates inside Core, forensic line ---
        if (bridge.IndexOf("static string MassiveAnchorMask(string cardId, string anchorSec, out string fact)",
                StringComparison.Ordinal) < 0)
            Fail("P41d: the ONE anchor-mask helper signature moved - read sites would drift apart");
        else Ok("P41d: single MassiveAnchorMask helper (one decision for every HeroInSection read site)");
        if (CountActive(bridge, "Tac.InteriorLaw.AnchorMaskFor(") != 3)
            Fail("P41d: InteriorLaw.AnchorMaskFor active call sites " +
                 CountActive(bridge, "Tac.InteriorLaw.AnchorMaskFor(") +
                 " != 3 (helper + block-achse + object-guard per-group variants) - a mirror is drifting");
        else Ok("P41d: exactly three Core mask calls (helper + both hold variants) - no fourth mirror of the law");

        // --- read site 1 (bridge:3180/3185 law): the object-seam focus read
        if (bridge.IndexOf("var focusSecMasked = MassiveAnchorMask(cd.id, focusSec, out maskFact);",
                StringComparison.Ordinal) < 0)
            Fail("P41d: the TryObjectTarget focus-sec mask moved off the pinned HeroInSection read");
        else Ok("P41d: pinned focus read masked behind the HeroInSection read (spec 9.3-2 site 1)");
        if (bridge.IndexOf("if (maskFact == Tac.InteriorLaw.MaskSealMissing) return;",
                StringComparison.Ordinal) < 0)
            Fail("P41d: the honest-absence lane (seal_missing -> no object target, never a chase) moved");
        else Ok("P41d: seal_missing answers with honest absence - the canon Fokus/Reaktion fallback decides");

        // --- goal resolution: per-activation disclosure for EVERY Massive
        if (CountActive(bridge, "MassiveAnchorMask(cd.id,") != 2)
            Fail("P41d: goal-lane mask call sites " + CountActive(bridge, "MassiveAnchorMask(cd.id,") +
                 " != 2 (goal resolution + object seam) - an activation lane lost its disclosure");
        else Ok("P41d: both activation-lane reads masked (goal resolution covers basis-veto rows too)");
        if (bridge.IndexOf("_interiorAnchorFactThis = \"\";   // P41d: same law for the anchor-mask fact",
                StringComparison.Ordinal) < 0)
            Fail("P41d: the per-activation fact reset moved - stale facts would ride the next popup");
        else Ok("P41d: anchor-mask fact resets per TrySelectTarget (P38 marker law)");

        // --- read site 2 (bridge:3694/3696 law): block-achse per-group variant
        if (bridge.IndexOf("holdThis = blockHoldMassive ?? \"\";", StringComparison.Ordinal) < 0 ||
            bridge.IndexOf("BlockHold = holdThis,", StringComparison.Ordinal) < 0)
            Fail("P41d: the per-group sealed BlockHold selection moved - the hole would reach RiskFacts");
        else Ok("P41d: block-achse sealed variant rides ONLY Massive-without-override groups (canon bit-identity)");

        // --- read site 2b (spec 9.0 table '+3738+3739'): object-guard hero reads
        if (bridge.IndexOf("rail=object-guard fact=seal", StringComparison.Ordinal) < 0)
            Fail("P41d: the object-guard seal forensic line moved - the pinned per-hero reads lost their interior view");
        else Ok("P41d: object-guard hero reads masked behind the seal (patrol-the-room-exits doctrine)");
        if (bridge.IndexOf("bool canonHoldFromObjectGuard = blockHold != null && blockHold == objectHold;",
                StringComparison.Ordinal) < 0 ||
            bridge.IndexOf("objectGuardVariant && canonHoldFromObjectGuard &&", StringComparison.Ordinal) < 0)
            Fail("P41d: the object-guard variant lost its free-slot gate - it could override the mission-END block hold");
        else Ok("P41d: object-guard variant engages only on the free slot (canon block wiring keeps priority)");

        // --- read site 3 (bridge:2685 law): procurement rail instrumented
        if (bridge.IndexOf("rail=procurement anchor=", StringComparison.Ordinal) < 0)
            Fail("P41d: the procurement anchor forensic line moved - the axis-zone read lost its interior view (economy seam P41e builds on it)");
        else Ok("P41d: procurement axis-zone read instrumented (shared list stays; per-card USEFULNESS rides 5-H5)");

        // --- REPLAN fact 4: BEHIND the three feed facts, same block
        int iFeed = bridge.IndexOf("Tac.WorldEventVector.MidroundReplanTrigger(", StringComparison.Ordinal);
        int iFact = bridge.IndexOf("replan = \"InteriorIngress\";", StringComparison.Ordinal);
        int iGuard = bridge.IndexOf("if (replan == null)\n                {", StringComparison.Ordinal);
        if (iFeed < 0 || iFact < 0)
            Fail("P41d: fact-4 wiring missing (feed trigger=" + iFeed + " ingress trigger=" + iFact + ")");
        else if (iGuard < 0)
            Fail("P41d: the second-check guard moved off the ingress block - the precedence law went unwatched");
        else if (iGuard < iFeed || iGuard > iFact)
            Fail("P41d: the interior fact check is not the SECOND check behind the feed facts - the three P33 facts lost precedence");
        else Ok("P41d: REPLAN fact 4 rides the P33-H4b block, second behind the three feed facts (spec 9.3-3)");
        if (bridge.IndexOf("[interior-ingress] hero=", StringComparison.Ordinal) < 0)
            Fail("P41d: the ingress forensic line moved - the trigger would print without its fact");
        else Ok("P41d: ingress trigger carries its [interior-ingress] forensic line");
        if (bridge.IndexOf("_roundFocusHero, spcCap.HeroInSection(_roundFocusHero)", StringComparison.Ordinal) < 0)
            Fail("P41d: the build-time anchor capture (focus hero first) moved - fact 4 would track nothing");
        else Ok("P41d: ingress capture at build: focus hero first, then block axis (deterministic caller order)");

        // --- the two popup rails: reader + field + 11 languages ---
        if (bridge.IndexOf("Lh.ordExplainMassiveSeal", StringComparison.Ordinal) < 0)
            Fail("P41d: the EXPLAIN seam stopped reading ordExplainMassiveSeal");
        else Ok("P41d: seal explanation rides the gated EXPLAIN block (reader null-safe, declutter law 9.6)");
        if (bridge.IndexOf("Li.ordInfoMassiveInterior", StringComparison.Ordinal) < 0)
            Fail("P41d: the INFO seam stopped reading ordInfoMassiveInterior");
        else Ok("P41d: stay-out advisory rides the existing INFO-TEXT block (missing key skips the line)");

        var ui = ReadSrc("ImperialCommander2/Assets/Scripts/LanguageControllers/UILanguage.cs");
        if (ui.IndexOf("public string ordExplainMassiveSeal, ordInfoMassiveInterior;",
                StringComparison.Ordinal) < 0)
            Fail("P41d: UILanguage.cs lost the two rail fields (they ship in the SAME commit as the keys)");
        else Ok("P41d: UILanguage.cs fields present (same-commit contract with the ui.json keys)");

        var langRoot = Path.Combine(InteriorDpProbe.DataRoot(), "ImperialCommander2",
                                    "Assets", "Resources", "Languages");
        var langs = Directory.GetDirectories(langRoot);
        Array.Sort(langs);   // Br sorts BEFORE En - so the rails are read
        if (langs.Length != 11) { Fail("11 shipped language dirs expected, found " + langs.Length); }
        var sealBy = new Dictionary<string, string>(StringComparer.Ordinal);
        var infoBy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dir in langs)
        {
            var leaf = Path.GetFileName(dir);
            var doc = Json.AsDict(ReadJsonFile(Path.Combine(dir, "ui.json")));
            object railBag;
            if (!doc.TryGetValue("uiTacticalAI", out railBag))
            { Fail(leaf + ": ui.json has no uiTacticalAI block"); continue; }
            var t = Json.AsDict(railBag);
            object seal, info;
            if (!t.TryGetValue("ordExplainMassiveSeal", out seal) || !(seal is string) ||
                ((string)seal).Length == 0 ||
                !t.TryGetValue("ordInfoMassiveInterior", out info) || !(info is string) ||
                ((string)info).Length == 0)
            { Fail(leaf + ": the two P41d rails missing/empty"); continue; }
            sealBy[leaf] = (string)seal;
            infoBy[leaf] = (string)info;
            Ok(leaf + ": both P41d rails present and non-empty");
        }
        {
            string enSeal, enInfo;
            if (!sealBy.TryGetValue("En", out enSeal) || !infoBy.TryGetValue("En", out enInfo))
                Fail("En/ui.json carries no P41d rails - the inventory root is missing");
            else
            {
                var order = new List<string>(infoBy.Keys);
                order.Sort(StringComparer.Ordinal);   // deterministic gate output
                foreach (var leaf in order)
                {
                    if (leaf == "En") continue;
                    if (leaf == "De")
                    {
                        // De is the shipped reference translation
                        // (UiCompletenessGate law): real umlauts, and the
                        // printed stay-out reminder in German -
                        // 'Außenfeldern', never the EN copy.
                        if (infoBy["De"].IndexOf("Au\u00dfenfeldern", StringComparison.Ordinal) < 0 ||
                            sealBy["De"].IndexOf("d\u00fcrfen", StringComparison.Ordinal) < 0 ||
                            string.Equals(infoBy["De"], enInfo, StringComparison.Ordinal))
                            Fail("De: the P41d rails are not a real German translation (umlaut lanes / EN copy?)");
                        else Ok("De: both P41d rails are real German (true umlauts, stay-out reminder translated)");
                        continue;
                    }
                    if (!string.Equals(sealBy[leaf], enSeal, StringComparison.Ordinal) ||
                        !string.Equals(infoBy[leaf], enInfo, StringComparison.Ordinal))
                        Fail(leaf + ": the P41d rails diverge from En (byte-exact EN copy law)");
                    else Ok(leaf + ": both P41d rails byte-exact vs En");
                }
            }
        }
    }
}
