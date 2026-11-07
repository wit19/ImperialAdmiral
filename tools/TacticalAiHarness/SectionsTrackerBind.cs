// P33-H3 tracker-bind section (findings_Player-33 §2c): drives the SAME
// TrackerBindLaw functions HPTrackerContainer calls in the Unity app
// (ResolveBoard for the bind, TryBeginCommit for every close rail) over an
// op-scripted container simulator. Ground truth is HAND-PINNED in
// tracker_bind.json (Thomas' route for law-pins without a Python sim twin,
// same as settings_load.json) - this section mirrors the container's op
// semantics, never re-implements the law. The two Player-33 oscillation
// replays (p33_z745, p33_z799) pin that no commit can land as [0,0,0] after
// real damage was on the card: structurally, via the single-commit flag.
using System;
using System.Collections.Generic;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        // card stand-in: the law keys only on id; the wound array plays the
        // role of DeploymentCard.woundTrackerValue.
        sealed class BindCard
        {
            public string Id;
            public int[] Wounds;
        }

        static string Arr(int[] a) => string.Join(",", a);

        static void SecTrackerBind()
        {
            var v = LoadJson("tracker_bind.json");
            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var exp = Json.AsDict(c["expect"]);
                var setup = Json.AsDict(Json.AsDict(v["cards"])[name]);

                // build the object graph FIRST: the deployed list references
                // the very same instances the popup rails hand over.
                var byKey = new Dictionary<string, BindCard>();
                foreach (var cr in Json.AsList(setup["cards"]))
                {
                    var d = Json.AsDict(cr);
                    var wounds = new List<int>();
                    foreach (var w in Json.AsList(d["wounds"])) wounds.Add((int)Json.AsLong(w));
                    byKey[Json.AsStr(d["key"])] = new BindCard { Id = Json.AsStr(d["id"]), Wounds = wounds.ToArray() };
                }
                var deployed = new List<BindCard>();
                foreach (var k in Json.AsList(setup["deployed"])) deployed.Add(byKey[Json.AsStr(k)]);

                // container simulator state (mirrors HPTrackerContainer fields)
                bool wasVisible = false;
                BindCard bound = null;
                int[] wheel = new int[0];

                var reasons = new List<string>();
                var commits = new List<string>();
                int skips = 0;

                foreach (var opRaw in Json.AsList(c["ops"]))
                {
                    var op = Json.AsStr(opRaw);
                    var ix = op.IndexOf(':');
                    var kind = ix < 0 ? op : op.Substring(0, ix);
                    var arg = ix < 0 ? "" : op.Substring(ix + 1);
                    if (kind == "open")
                    {
                        // ResetTracker(card): bind through the board law, arm
                        // the commit, re-read the wheels FROM the bound card.
                        var d = TrackerBindLaw.ResolveBoard(deployed, byKey[arg], x => x.Id);
                        reasons.Add(d.Reason);
                        bound = d.Bound;
                        wheel = (int[])bound.Wounds.Clone();
                        wasVisible = true;
                    }
                    else if (kind == "set")
                    {
                        var vals = new List<int>();
                        foreach (var s in arg.Split(',')) vals.Add(int.Parse(s.Trim()));
                        wheel = vals.ToArray();
                    }
                    else if (kind == "close" || kind == "hide")
                    {
                        // UpdateAndHide / OnDisable: one transaction, no double write.
                        if (TrackerBindLaw.TryBeginCommit(ref wasVisible))
                        {
                            bound.Wounds = (int[])wheel.Clone();
                            commits.Add(Arr(bound.Wounds));
                            wheel = new int[bound.Wounds.Length];
                        }
                        else skips++;
                    }
                    else if (kind == "reactivate")
                    {
                        // Parent re-enable WITHOUT a card read (ShowMissionCard
                        // rail). The fixed container has NO OnEnable arming -
                        // so this is deliberately a no-op; the follow-up close
                        // must SKIP (old code re-armed and committed zeros).
                    }
                    else throw new Exception("tracker_bind: unknown op '" + op + "' in " + name);
                }

                Check("tracker[" + name + "].binds", Json.AsStr(exp["binds"]), string.Join(",", reasons));
                Check("tracker[" + name + "].commits", Json.AsStr(exp["commits"]), string.Join("|", commits));
                Check("tracker[" + name + "].skips", (long)Json.AsLong(exp["skips"]), (long)skips);
                var fin = Json.AsDict(exp["final"]);
                foreach (var kv in fin)
                    Check("tracker[" + name + "].final." + kv.Key, Json.AsStr(kv.Value), Arr(byKey[kv.Key].Wounds));
            }
        }
    }
}
