// M14c P25-C2 (card t_15ea0fba) section: the pure DP anchor ranker. The
// expect blocks were generated INDEPENDENTLY by tools/gen_dp_anchor_fixture.py
// (a Python mirror of the Core law, same two-implementations discipline as
// the sim-exported vectors - hand-pinned route like optional_deploy.json,
// Canon vectors untouched). The Empire11 data pin runs against the SHIPPED
// table + mission files (zero fixtures): the mined row must carry the real
// Informant GUID and section, the mission file must agree with them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        // Walk up from the fixtures dir to the repo, then into
        // Assets/Resources/Data (same seam style as LanguagesDir()).
        static string RepoFile(params string[] parts)
        {
            var d = new DirectoryInfo(FIX);
            for (int i = 0; i < 12 && d != null; i++, d = d.Parent)
            {
                foreach (var root in new[]
                {
                    Path.Combine(d.FullName, "ImperialCommander2", "Assets"),
                    Path.Combine(d.FullName, "Assets"),
                })
                {
                    var p = Path.Combine(new[] { root }.Concat(parts).ToArray());
                    if (File.Exists(p)) return p;
                }
            }
            return null;
        }

        static void SecDpAnchors()
        {
            var v = LoadJson("dp_anchors.json");

            // ----------------------------------------------------- rank cases
            foreach (var raw in Json.AsList(v["rank_cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var seed = (ulong)Json.AsLong(c["seed"]);
                int round = (int)Json.AsLong(c["round"]);
                var table = Json.AsDict(c["table"]);
                var missionFile = Json.AsStr(c["missionFile"]);

                var anchors = DeploymentAnchors.RowsForMission(table, missionFile);
                var cands = Json.AsList(c["cands"]).Select(x =>
                {
                    var cd = Json.AsDict(x);
                    return new DeployCandidate
                    {
                        Guid = Json.AsStr(cd["guid"]),
                        Name = Json.AsStr(cd["name"]),
                        SectionGuid = Json.AsStr(cd["section"]),
                        X = (double)Json.AsLong(cd["x"]),
                        Y = (double)Json.AsLong(cd["y"]),
                    };
                }).ToList();
                var pick = DeploymentAnchors.Choose(cands, anchors, seed, round);

                var exp = Json.AsDict(c["expect"]);
                Check("rank[" + name + "].guid",
                    Json.AsStr(exp["guid"]), pick == null ? null : pick.Candidate.Guid);

                // diagnostics: anchor identity + distance (None => no anchor)
                var expEnt = exp["anchorEntity"];
                Check("rank[" + name + "].anchorNull", expEnt == null,
                      pick == null || pick.Anchor == null);
                if (expEnt != null && pick != null && pick.Anchor != null)
                {
                    Check("rank[" + name + "].anchorEntity", Json.AsStr(expEnt), pick.Anchor.EntityGuid);
                    Check("rank[" + name + "].anchorSection", Json.AsStr(exp["anchorSection"]),
                          pick.Anchor.SectionGuid);
                    Check("rank[" + name + "].dist", Json.AsDouble(exp["dist"]), pick.Dist);
                }

                // Alt-Ergebnis-Pin: the LEGACY chain winner stays recorded,
                // recomputed here with the very key law the bridge line uses
                // (TieKey -> name -> GUID) against the same Core hash.
                var legacy = cands
                    .OrderBy(x => DeploymentAnchors.TieKey(x.Name, seed, round))
                    .ThenBy(x => x.Name ?? "", StringComparer.Ordinal)
                    .ThenBy(x => x.Guid ?? "", StringComparer.Ordinal)
                    .First();
                Check("rank[" + name + "].legacy", Json.AsStr(exp["legacyGuid"]), legacy.Guid);

                // (d) empty anchors => ranker AND legacy agree bit-for-bit
                if (Json.AsList(table["rows"]).Count == 0)
                    Check("rank[" + name + "].emptyCompat", legacy.Guid,
                          pick == null ? null : pick.Candidate.Guid);
            }

            // ------------------------------------------------------ table law
            foreach (var kv in Json.AsDict(v["table_cases"]))
            {
                var t = Json.AsDict(kv.Value);
                var doc = new Dictionary<string, object>
                {
                    ["columns"] = t["columns"],
                    ["rows"] = t["rows"],
                };
                foreach (var qraw in Json.AsList(t["queries"]))
                {
                    var q = Json.AsDict(qraw);
                    var got = DeploymentAnchors.RowsForMission(doc, Json.AsStr(q["file"]));
                    Check("table[" + kv.Key + "][" + Json.AsStr(q["file"]) + "]",
                          Json.AsLong(q["expect"]), (long)got.Count);
                }
            }

            // ------------------------------------- Empire11 data pin (SHIPPED)
            // Card t_15ea0fba pin (e): row EMPIRE11.json -> the Informant
            // GUID + section. Real GUID from the shipped mission file
            // (the card body carried a mistyped middle segment that exists
            // nowhere in the data - data-exists law pins the real one).
            const string INFORMANT = "576c3ba5-801c-42fb-b32f-14109702c8cc";
            const string START_SEC = "11111111-1111-1111-1111-111111111111";
            var tablePath = RepoFile("Resources", "Data", "taktische_mission_critical.json");
            Check("shipped.tableExists", true, tablePath != null);
            if (tablePath != null)
            {
                var root = Json.AsDict(Json.Parse(File.ReadAllText(tablePath)));
                var cols = Json.AsList(root["columns"]);
                Check("shipped.firstColumnIsMissionFile", "missionFile", Json.AsStr(cols[0]));
                var rows = DeploymentAnchors.RowsForMission(root, "EMPIRE11.json");
                Check("shipped.empire11.rowCount", 1L, (long)rows.Count);
                if (rows.Count == 1)
                {
                    Check("shipped.empire11.entityGuid", INFORMANT, rows[0].EntityGuid);
                    Check("shipped.empire11.section", START_SEC, rows[0].SectionGuid);
                    Check("shipped.empire11.posX", 1080.0, rows[0].X);
                    Check("shipped.empire11.posY", 1080.0, rows[0].Y);
                }
                // file-id match is OrdinalIgnoreCase, a title NEVER matches
                Check("shipped.caseInsensitiveRow", 1L,
                      (long)DeploymentAnchors.RowsForMission(root, "empire11.json").Count);
                Check("shipped.titleNeverMatches", 0L,
                      (long)DeploymentAnchors.RowsForMission(root, "Empire 11").Count);

                // the table must agree with the shipped mission file
                var missionPath = RepoFile("Resources", "SagaMissions", "Empire", "EMPIRE11.json");
                Check("shipped.missionFileExists", true, missionPath != null);
                if (missionPath != null)
                {
                    var mission = Json.AsDict(Json.Parse(File.ReadAllText(missionPath)));
                    var ent = Json.AsList(mission["mapEntities"])
                        .Select(x => Json.AsDict(x))
                        .FirstOrDefault(e => string.Equals(Json.AsStr(e["GUID"]), INFORMANT,
                                                       StringComparison.OrdinalIgnoreCase));
                    Check("shipped.missionRowExists", true, ent != null);
                    if (ent != null)
                    {
                        Check("shipped.missionPosition", "1080,1080", Json.AsStr(ent["entityPosition"]));
                        Check("shipped.missionSection", START_SEC, Json.AsStr(ent["mapSectionOwner"]));
                    }
                }
            }
        }
    }
}
