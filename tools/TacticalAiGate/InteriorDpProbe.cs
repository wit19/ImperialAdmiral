using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TacticalAI.Core;

// P41c (card t_a2e54ec9; CONCEPT_massive_interior_v1.md v1.3 sections 4-D3-B,
// 4-D6, 9.4 last bullet): the DP-side census-equality probe for the gates.
//
// This is the THIRD engine over the same shipped data (after the Python
// census of P41a and the harness law vectors of P41b/P41c). It re-derives,
// IN C#, directly from the 138 shipped mission files + shipped CardData lanes:
//   * every DP and its tile mapping (InteriorUnity's rect/containment law -
//     the same law tools/massive_interior_census.py ships, mirrored 1:1);
//   * the authored D6 override pairs (initialDeploymentGroups + event lanes
//     6/7/21 with deploymentPoint==1, sentinel rules, editor-leftover rule,
//     custom-lane keyword authored Massive - every rule of the census, fail-
//     closed included);
//   * the registration outcome through the REAL InteriorLaw (StatusForTile +
//     OverrideSet, the P41b shipped law).
// RunOverrideCensusEquality then asserts EQUALITY IN BOTH DIRECTIONS against
// the census ground truth (spec 9.4: 'Override-Menge == Census-Menge BEIDE
// Richtungen'): the 22 authored-DP rows hand-pinned below (they come from the
// P41a census run, docs/MASSIVE_interior_census.md section 6 - a row the
// probe finds but the census does not, or vice versa, fails the build and is
// NAMED), and RunBannedDpMap cross-checks every mission's Banned-DP set
// against the census artifact's per-mission counters (tools/
// massive_interior_crosswalk_candidates.json is the P41a shipped artifact;
// the per-mission ledger rides in docs/MASSIVE_interior_census.md section 9's
// dp-kind counts - the probe cross-checks the counts it can read machine-
// side, plus its own hand-pinned map of the Banned DP GUIDs, reviewed once).
// RunMassiveFilterPins pins the FILTERED DP sets (spec 4-D3-B v1.3): for each
// mission with a Banned DP, the pool the rails feed into
// TacticalAIBridge.TryChooseDeploymentPoint minus Banned (no override), and
// the CORE4 DG073 case where the D6 override keeps the full pool.
// NOTE (P41c): all three assertion steps run against this probe inside
// MassiveInteriorGate - RunDpOverrideCensusEquality (both directions + the
// Banned-DP map), RunMassiveFilterPins (the FilterPoolPin table + the CORE4/
// CORE3 lanes) and RunSeamSourceContract (the shipped rail/builder source
// lines). DeploymentFilterGate and OptionalDeployGate consume the same probe
// through MassiveInteriorGate.CollectRailFacts (rails 2-5 / rail 5).
public static class InteriorDpProbe
{
    const double Eps = 1e-6;
    const double PxPerHex = 10.0;
    const string NullGuid = "00000000-0000-0000-0000-000000000000";
    const string SectionSentinel = "11111111-1111-1111-1111-111111111111";

    static readonly string[] Expansions =
        { "Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal", "Other" };

    // Data root: the tree under test. The gates run with CWD = repo root
    // (dotnet run --project tools/...), so CWD-relative reads measure the
    // worktree during a hop and the merged main afterwards - one rule, no
    // hop-dependent constants. Fallback keeps the gate runnable from the
    // tools dir itself.
    // tree under test: CWD first (the gate/harness run from the repo root of
    // the worktree under review), the main tree as fallback. PUBLIC: the P41c
    // rail source-contract reads follow the SAME rule (MassiveInteriorGate.
    // ReadSrc) - one tree per gate run, never a mix.
    public static string DataRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cwd, "ImperialCommander2", "Assets", "Resources", "SagaMissions")))
            return cwd;
        var up = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (Directory.Exists(Path.Combine(up, "ImperialCommander2"))) return up;
        return "/home/open/work/ia-ai/ImperialCommander2";
    }

    static readonly Regex TcRule = new Regex(",\\s*([}\\]])");
    static object ReadJsonFile(string path)
    {
        var text = File.ReadAllText(path);
        return Json.Parse(TcRule.Replace(text, "$1"));
    }

    static string OrNone(object v)
    {
        var s = v as string;
        return string.IsNullOrEmpty(s) ? "None" : s;
    }

    // GUID keys in the probe's lane: lowercase, N-hyphenated text form -
    // identical to InteriorUnity.DpKey and to the census (str(...).lower()).
    static string Key(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return Guid.Parse(s.Trim()).ToString(); }
        catch { return s.Trim().ToLowerInvariant(); }
    }
    static string KeyOf(object v)
    {
        return v == null ? null : Key(Json.AsStr(v));
    }
    // Card ids: CardData lanes mix string ids (DG073) and GUID ids; the
    // census reads them as-is - probe keys them textually, invariant.
    static string CardIdOf(object v)
    {
        if (v == null) return null;
        if (v is long l) return l.ToString();
        var s = v as string;
        return string.IsNullOrEmpty(s) ? null : s;
    }

    // ------------------------------------------------------------ dims -----
    sealed class DimRec { public int W, H; public string BiomeA = "None", BiomeB = "None"; }

    static Dictionary<string, Dictionary<int, DimRec>> LoadDims()
    {
        var dims = new Dictionary<string, Dictionary<int, DimRec>>(StringComparer.Ordinal);
        foreach (var dRaw in Json.AsList(ReadJsonFile(
            Path.Combine(DataRoot(), "ImperialCommander2", "Assets", "Resources", "dimensions.json"))))
        {
            var d = Json.AsDict(dRaw);
            var exp = Json.AsStr(Json.Get(d, "expansion", "Core"));
            var rec = new DimRec
            {
                W = Json.AsInt(Json.Get(d, "width", 0L)),
                H = Json.AsInt(Json.Get(d, "height", 0L)),
                BiomeA = OrNone(Json.Get(d, "biomeA", null)),
                BiomeB = OrNone(Json.Get(d, "biomeB", null)),
            };
            var id = Json.AsInt(Json.Get(d, "id", 0L));
            Dictionary<int, DimRec> byId;
            if (!dims.TryGetValue(exp, out byId)) { byId = new Dictionary<int, DimRec>(); dims[exp] = byId; }
            byId[id] = rec;
        }
        return dims;
    }

    // ----------------------------------------------------------- cards -----
    sealed class CardRec { public string Name; public bool Massive; }

    static Dictionary<string, CardRec> LoadCards(out HashSet<string> massiveNames)
    {
        var cards = new Dictionary<string, CardRec>(StringComparer.Ordinal);
        massiveNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lane in new[] { "enemies", "villains", "allies", "heroes" })
        {
            var path = Path.Combine(DataRoot(), "ImperialCommander2", "Assets",
                                    "Resources", "CardData", lane + ".json");
            foreach (var cRaw in Json.AsList(ReadJsonFile(path)))
            {
                var c = Json.AsDict(cRaw);
                var id = CardIdOf(Json.Get(c, "id", null));
                if (id == null) continue;
                if (cards.ContainsKey(id))
                    throw new Exception("duplicate card id across CardData lanes: " + id);
                bool massive = Json.AsList(Json.Get(c, "keywords", new List<object>()))
                                   .Any(k => Json.AsStr(k) == "Massive");
                cards[id] = new CardRec { Name = Json.AsStr(Json.Get(c, "name", "")), Massive = massive };
                if (massive) massiveNames.Add(Json.AsStr(Json.Get(c, "name", "")));
            }
        }
        return cards;
    }

    // ------------------------------------------------------- tile/rect -----
    // InteriorUnity.TryRect / tools/massive_interior_census.py rect_of.
    static bool TryRect(double x, double y, double rotDeg, double w, double h, out Rect r)
    {
        r = default(Rect);
        double rot = rotDeg % 360.0;
        if (rot > 180.0) rot -= 360.0; else if (rot < -180.0) rot += 360.0;
        if (Math.Abs(rot) < Eps) { r = new Rect { MinX = x, MaxX = x + w, MinY = y, MaxY = y + h }; return true; }
        if (Math.Abs(rot - 90.0) < Eps) { r = new Rect { MinX = x - h, MaxX = x, MinY = y, MaxY = y + w }; return true; }
        if (Math.Abs(rot - 180.0) < Eps || Math.Abs(rot + 180.0) < Eps) { r = new Rect { MinX = x - w, MaxX = x, MinY = y - h, MaxY = y }; return true; }
        if (Math.Abs(rot + 90.0) < Eps) { r = new Rect { MinX = x, MaxX = x + h, MinY = y - w, MaxY = y }; return true; }
        return false;
    }

    public struct Rect
    {
        public double MinX, MaxX, MinY, MaxY;
        public bool Contains(double px, double py)
        {
            return px >= MinX - Eps && px <= MaxX + Eps && py >= MinY - Eps && py <= MaxY + Eps;
        }
        public bool StrictContains(double px, double py)
        {
            return px > MinX + Eps && px < MaxX - Eps && py > MinY + Eps && py < MaxY - Eps;
        }
    }

    // ------------------------------------------------------- probe types ---
    public sealed class TileRec
    {
        public string Expansion; public string TileId; public string Side; public string Biome;
        public string SectionGuid; public Rect R;
        public TileStatus Status;    // the shipped InteriorLaw reading
    }
    public sealed class DpRec
    {
        public string Guid; public double X, Y; public string SectionGuid;
        public string Kind;          // single/strict/section/status_uniform/ambiguous_mixed/tileless
        public string TileRef;       // EXPANSION+id+side, like InteriorUnity.TileRef
        public TileStatus Status;    // law reading of the mapped tile (Ambiguous for mixed/tileless)
    }
    public sealed class AuthoredPair
    {
        public string Mission;       // "CORE4.json" file name (census lane)
        public string Channel;       // "initial" / "event6" / "event7" / "event21"
        public string CardId;        // authored cardID (nullable for name-only lanes)
        public string Dp;            // dp guid (null = no real DP under the slot)
    }
    public sealed class MissionProbe
    {
        public string Name;
        public Dictionary<string, DpRec> Dps;            // guid -> dp
        public List<string> ActiveDps;                   // active-flag pool lane, sorted
        public List<AuthoredPair> Authored;              // D6 candidate pairs (real DPs)
        public Dictionary<string, TileStatus> BannedByDp; // guid -> Banned dp set (filter lane)
    }

    static string TileRefOf(TileRec t) { return t.Expansion + t.TileId + t.Side; }

    // ----------------------------------------------------- mission probe ---
    static MissionProbe ProbeMission(string path, Dictionary<string, Dictionary<int, DimRec>> dims,
                                     Dictionary<string, CardRec> cards, HashSet<string> massiveNames)
    {
        var mp = new MissionProbe
        {
            Name = Path.GetFileName(path),
            Dps = new Dictionary<string, DpRec>(StringComparer.Ordinal),
            ActiveDps = new List<string>(),
            Authored = new List<AuthoredPair>(),
            BannedByDp = new Dictionary<string, TileStatus>(StringComparer.Ordinal),
        };
        var d = Json.AsDict(ReadJsonFile(path));

        // ---- tiles (census law: mapSections[].mapTiles[], rect from dims)
        var tiles = new List<TileRec>();
        foreach (var secRaw in Json.AsList(Json.Get(d, "mapSections", new List<object>())))
        {
            var sec = Json.AsDict(secRaw);
            var secGuid = KeyOf(Json.Get(sec, "GUID", null)) ?? "";
            foreach (var tRaw in Json.AsList(Json.Get(sec, "mapTiles", new List<object>())))
            {
                var t = Json.AsDict(tRaw);
                var ei = (long)Json.Get(t, "expansion", 0L);
                if (ei < 0 || ei >= Expansions.Length)
                    throw new Exception(mp.Name + ": expansion index outside enum (fail-closed)");
                var exp = Expansions[(int)ei];
                var tid = Json.AsStr(Json.Get(t, "tileID", ""));
                var side = Json.AsStr(Json.Get(t, "tileSide", "A")) == "A" ? "A" : "B";
                var rotRaw = Json.Get(t, "entityRotation", 0.0);
                var rot = rotRaw is double ? (double)rotRaw
                        : rotRaw is long ? (long)rotRaw : D(rotRaw);
                string biome = "None";
                DimRec rec = null;
                Dictionary<int, DimRec> dr;
                if (dims.TryGetValue(exp, out dr))
                {
                    int idNum;
                    DimRec one;
                    if (dr.TryGetValue(int.TryParse(tid, out idNum) ? idNum : -1, out one))
                    {
                        rec = one;
                        biome = side == "A" ? one.BiomeA : one.BiomeB;
                    }
                }
                string reason;
                string pos = Json.AsStr(Json.Get(t, "entityPosition", ""));
                var parts = pos.Split(',');
                double px = parts.Length > 0 ? D(parts[0]) : 0.0;
                double py = parts.Length > 1 ? D(parts[1]) : 0.0;
                Rect r;
                if (rec == null || rot % 90.0 != 0.0 || !TryRect(px, py, rot, rec.W * PxPerHex, rec.H * PxPerHex, out r))
                    continue;   // census law: no rect => not in the mapping pool
                tiles.Add(new TileRec
                {
                    Expansion = exp, TileId = tid, Side = side, Biome = biome, SectionGuid = secGuid, R = r,
                    Status = InteriorLaw.StatusForTile(exp, tid, side, biome, out reason),
                });
            }
        }

        // ---- DPs (census law: mapEntities entityType==3)
        var dpSecs = new Dictionary<string, List<TileRec>>(StringComparer.Ordinal);
        foreach (var eRaw in Json.AsList(Json.Get(d, "mapEntities", new List<object>())))
        {
            var e = Json.AsDict(eRaw);
            if ((long)Json.Get(e, "entityType", -1L) != 3L) continue;
            var guid = KeyOf(Json.Get(e, "GUID", null)) ?? "";
            var pos = Json.AsStr(Json.Get(e, "entityPosition", ""));
            var parts = pos.Split(',');
            var x = parts.Length > 0 ? D(parts[0]) : 0.0;
            var y = parts.Length > 1 ? D(parts[1]) : 0.0;
            var sec = KeyOf(Json.Get(e, "mapSectionOwner", null)) ?? "";
            bool active = true;   // entityProperties.isActive is a JSON bool
            var epRaw = Json.Get(e, "entityProperties", null);
            if (epRaw is Dictionary<string, object>)
            {
                var av = Json.Get(Json.AsDict(epRaw), "isActive", (object)true);
                if (av is bool ab) active = ab;
                else if (av is string asv) active = !(asv == "false" || asv == "False" || asv == "0");
            }
            var secTiles = new List<TileRec>();
            foreach (var t in tiles) if (t.SectionGuid == sec) secTiles.Add(t);
            dpSecs[guid] = secTiles;
            mp.Dps[guid] = new DpRec { Guid = guid, X = x, Y = y, SectionGuid = sec };
            if (active) mp.ActiveDps.Add(guid);
        }

        // ---- DP -> tile mapping (InteriorUnity.ResolveDp 1:1: strict means
        // STRICT CONTAINMENT (census lines 501-503), the section filter runs
        // over (strict or cands) and only ever SELECTS; the status-uniformity
        // pool is (strict or cands) read through the shipped 3-state law).
        foreach (var dp in mp.Dps.Values)
        {
            var cands = tiles.Where(t => t.R.Contains(dp.X, dp.Y)).ToList();
            TileRec chosen;
            if (cands.Count == 0)
            {
                dp.Kind = "tileless"; dp.Status = TileStatus.Ambiguous;
            }
            else if (cands.Count == 1)
            {
                dp.Kind = "single"; chosen = cands[0];
                dp.TileRef = TileRefOf(chosen); dp.Status = chosen.Status;
            }
            else
            {
                var strict = cands.Where(t => t.R.StrictContains(dp.X, dp.Y)).ToList();
                if (strict.Count == 1)
                {
                    dp.Kind = "strict"; chosen = strict[0];
                    dp.TileRef = TileRefOf(chosen); dp.Status = chosen.Status;
                }
                else
                {
                    var basePool = strict.Count > 0 ? strict : cands;
                    var secf = basePool.Where(t => t.SectionGuid == dp.SectionGuid).ToList();
                    if (secf.Count == 1)
                    {
                        dp.Kind = "section"; chosen = secf[0];
                        dp.TileRef = TileRefOf(chosen); dp.Status = chosen.Status;
                    }
                    else if (basePool.All(t => t.Status == basePool[0].Status))
                    {
                        dp.Kind = "status_uniform"; chosen = basePool[0];
                        dp.TileRef = TileRefOf(chosen); dp.Status = chosen.Status;
                    }
                    else { dp.Kind = "ambiguous_mixed"; dp.Status = TileStatus.Ambiguous; }
                }
            }
            if (dp.Kind == "ambiguous_mixed" || dp.Kind == "tileless")
            { dp.TileRef = null; dp.Status = TileStatus.Ambiguous; }
        }

        // ---- authored D6 channels (census law, both lanes)
        var realDps = mp.Dps;   // existence lane
        // initial groups
        foreach (var gRaw in Json.AsList(Json.Get(d, "initialDeploymentGroups", new List<object>())))
        {
            var g = Json.AsDict(gRaw);
            var cid = CardIdOf(Json.Get(g, "cardID", null));
            var name = Json.AsStr(Json.Get(g, "cardName", "")) ?? "";
            bool massive = (cid != null && cards.ContainsKey(cid) && cards[cid].Massive)
                           || (massiveNames.Contains(name) && string.IsNullOrEmpty(cid));
            if (!massive) continue;
            foreach (var pRaw in Json.AsList(Json.Get(g, "pointList", new List<object>())))
            {
                var gu = KeyOf(Json.Get(Json.AsDict(pRaw), "GUID", null));
                if (gu == null || gu == NullGuid || gu == SectionSentinel) continue;
                if (!realDps.ContainsKey(gu))
                    throw new Exception(mp.Name + ": initial pointList GUID matches no DP (fail-closed)");
                mp.Authored.Add(new AuthoredPair { Mission = mp.Name, Channel = "initial", CardId = cid, Dp = gu });
            }
        }
        // event lanes 6/7/21 with deploymentPoint==1
        var actions = new List<Dictionary<string, object>>();
        foreach (var key in new[] { "globalEvents", "eventGroups", "mapSections",
                                    "initialDeploymentGroups", "reservedDeploymentGroups" })
            WalkActions(Json.Get(d, key, null), actions);
        foreach (var a in actions)
        {
            var t = (long)Json.Get(a, "eventActionType", -1L);
            if (t != 6L && t != 7L && t != 21L) continue;
            var egdRaw = Json.Get(a, "enemyGroupData", null);
            var egd = egdRaw is Dictionary<string, object> ? (Dictionary<string, object>)egdRaw
                                                          : new Dictionary<string, object>();
            string cid; string name; bool massive;
            if (t == 21L)
            {
                cid = CardIdOf(Json.Get(egd, "cardID", null));
                name = FirstNonEmpty(Json.Get(a, "customName", null), Json.Get(egd, "cardName", null),
                                      Json.Get(a, "displayName", null)) ?? "CustomEnemyDeployment";
                massive = KeywordsAreMassive(Json.Get(a, "keywords", null));
            }
            else
            {
                cid = CardIdOf(Json.Get(egd, "cardID", null)) ?? CardIdOf(Json.Get(a, "allyID", null));
                name = FirstNonEmpty(Json.Get(a, "enemyName", null), Json.Get(egd, "cardName", null),
                                     Json.Get(a, "allyName", null));
                if (cid != null && !cards.ContainsKey(cid))
                    throw new Exception(mp.Name + ": event cardID matches no CardData row (fail-closed)");
                massive = (cid != null && cards.ContainsKey(cid) && cards[cid].Massive)
                          || (name != null && massiveNames.Contains(name) && string.IsNullOrEmpty(cid));
            }
            if (!massive) continue;
            var spot = (long)Json.Get(a, "deploymentPoint", 0L);
            var guids = new List<string>();
            var sdp = KeyOf(Json.Get(a, "specificDeploymentPoint", null));
            if (!string.IsNullOrEmpty(sdp) && sdp != NullGuid)
            {
                if (sdp == SectionSentinel) { /* editor leftover - no tile */ }
                else if (realDps.ContainsKey(sdp)) guids.Add(sdp);
                else throw new Exception(mp.Name + ": event specificDeploymentPoint matches no DP (fail-closed)");
            }
            foreach (var pRaw in Json.AsList(Json.Get(egd, "pointList", new List<object>())))
            {
                var gu = KeyOf(Json.Get(Json.AsDict(pRaw), "GUID", null));
                if (string.IsNullOrEmpty(gu)) continue;
                if (gu == NullGuid || gu == SectionSentinel) { /* sentinel */ }
                else if (realDps.ContainsKey(gu)) guids.Add(gu);
                else throw new Exception(mp.Name + ": event pointList GUID matches no DP (fail-closed)");
            }
            // D6 authored channel = Spot Specific (deploymentPoint==1); a real
            // GUID under Active is an editor leftover - never an override row.
            if (spot != 1L) continue;
            foreach (var gu in new SortedSet<string>(guids))
                mp.Authored.Add(new AuthoredPair { Mission = mp.Name, Channel = "event" + t, CardId = cid, Dp = gu });
        }

        // ---- filter lane: every Banned dp
        foreach (var dp in mp.Dps.Values)
            if (dp.Status == TileStatus.Banned) mp.BannedByDp[dp.Guid] = dp.Status;
        mp.ActiveDps.Sort(StringComparer.Ordinal);
        return mp;
    }

    static string OrEmpty(object v) { return v as string ?? ""; }
    // census law (massive_interior_census.py ~640): the custom lane authors
    // keywords as ONE string ("Massive \r\n+3 Accuracy") split on
    // [\r\n;,]+; the CardData lanes carry lists with exact entries.
    static bool KeywordsAreMassive(object kw)
    {
        if (kw is string s)
            return Regex.Split(s, "[\\r\\n;,]+").Any(x => x.Trim() == "Massive");
        if (kw is List<object>)
            return Json.AsList(kw).Any(k => Json.AsStr(k) == "Massive");
        return false;
    }
    static double D(object v)
    {
        return double.Parse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture);
    }
    static string FirstNonEmpty(params object[] vals)
    {
        foreach (var v in vals) { var s = v as string; if (!string.IsNullOrEmpty(s)) return s; }
        return null;
    }

    static void WalkActions(object node, List<Dictionary<string, object>> outl)
    {
        if (node is Dictionary<string, object> dict)
        {
            var ea = Json.Get(dict, "eventActions", null);
            if (ea is List<object>)
                foreach (var aRaw in Json.AsList(ea))
                    if (aRaw is Dictionary<string, object>) outl.Add((Dictionary<string, object>)aRaw);
            foreach (var v in dict.Values) WalkActions(v, outl);
        }
        else if (node is List<object>)
        {
            foreach (var v in Json.AsList(node)) WalkActions(v, outl);
        }
    }

    // --------------------------------------------------------- the probe ---
    public static List<MissionProbe> ProbeAll()
    {
        var dims = LoadDims();
        HashSet<string> massiveNames;
        var cards = LoadCards(out massiveNames);
        var dir = Path.Combine(DataRoot(), "ImperialCommander2", "Assets", "Resources", "SagaMissions");
        var paths = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
                             .OrderBy(p => p, StringComparer.Ordinal).ToList();
        var all = new List<MissionProbe>();
        foreach (var p in paths) all.Add(ProbeMission(p, dims, cards, massiveNames));
        return all;
    }

    // the D6 registration outcome over the REAL law (builder lane):
    // authored pair -> RegisterAuthored(cardID, status of its dp).
    public static void RegisterAuthored(MissionProbe mp, out InteriorLaw.OverrideSet set,
                                        out List<string> registered, out List<string> unverified)
    {
        set = new InteriorLaw.OverrideSet();
        registered = new List<string>();
        unverified = new List<string>();
        foreach (var p in mp.Authored)
        {
            if (p.Dp == null || !mp.Dps.ContainsKey(p.Dp)) continue;
            var fact = set.RegisterAuthored(p.CardId, mp.Dps[p.Dp].Status);
            var line = mp.Name + ":" + p.CardId + ":" + p.Dp;
            if (fact == InteriorLaw.OverrideRegistered) registered.Add(line);
            else unverified.Add(line);
        }
        registered.Sort(StringComparer.Ordinal);
        unverified.Sort(StringComparer.Ordinal);
    }
}
