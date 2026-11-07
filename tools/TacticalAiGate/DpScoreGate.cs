// P33-H4 live gate (Thomas 08.11, CONCEPT_drohungsbudget Sec.2 Stufe 3 +
// Sec.3; card t_31d8b805): the DP-Score law proven LIVE, zero fixtures -
// the fixture vectors (dp_score.json) pin VALUES, this gate pins the LAW
// against any future drift of the Core implementation:
//   1. NULL-LAW EQUIVALENCE (the acceptance criterion): for a deterministic
//      matrix of names x seeds x rounds, ChooseScored WITHOUT any fact
//      (no anchor, no graph, no zones, no own sections, no ledger) lands
//      EXACTLY on the bridge's legacy XOR OrderBy line - the bit-identical
//      fallback, re-run over the whole matrix on every gate pass.
//   2. DYNAMIC ANKER-LAW: a section-only anchor (HasPosition=false) claims
//      the tier of its whole section, NEVER a position (far-apart DPs in
//      the section tie at the anchor stage - coordinates cannot leak), and
//      a positioned anchor in the same section keeps the intra-section
//      precision.
//   3. BATTLE-ZONE stage: a recorded route beats no route (claim over
//      no-claim), nearer beats farther, two claimless candidates stay tied
//      (Loch-Gesetz: absence is never a penalty below other absences).
//   4. LADDER ORDER: zone claim beats leash; leash beats diversity;
//      diversity beats the seed chain - each pair measured on a minimal
//      constellation.
//   5. Determinism: identical inputs, identical pick (replay).
// No UI key, no shipped data, no Unity type - Core law only.
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

public static class DpScoreGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    public static void Run()
    {
        NullLawEquivalence();
        DynamicAnchorLaw();
        ZoneStageLaw();
        LadderOrder();
        Console.WriteLine("DpScoreGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL"));
        if (fails > 0) Environment.ExitCode = 1;
    }

    // ---- helpers ---------------------------------------------------------
    static DeployCandidate Cand(string guid, string name, string section, double x = 0, double y = 0)
        => new DeployCandidate { Guid = guid, Name = name, SectionGuid = section, X = x, Y = y };

    static DeployAnchor Anchored(string guid, string section, double x, double y)
        => new DeployAnchor { EntityGuid = guid, SectionGuid = section, X = x, Y = y };

    static DeployAnchor SectionOnly(string section)
        => new DeployAnchor { EntityGuid = section, SectionGuid = section, HasPosition = false };

    // the bridge's legacy XOR line, verbatim (TacticalAIBridge
    // .TryChooseDeploymentPoint): OrdinalOf(name) ^ (seed ^ round*phi),
    // then name Ordinal, then guid Ordinal.
    static string LegacyWinner(IEnumerable<DeployCandidate> cands, ulong seed, int round)
        => cands
            .OrderBy(x => DeploymentAnchors.OrdinalOf(x.Name ?? "") ^
                          (seed ^ (ulong)Math.Max(1, round) * 0x2545F4914F6CDD1DUL))
            .ThenBy(x => x.Name ?? "", StringComparer.Ordinal)
            .ThenBy(x => x.Guid ?? "", StringComparer.Ordinal)
            .First().Guid;

    static string ScoredWinner(List<DeployCandidate> cands, IList<DeployAnchor> anchors,
                               SectionGraph graph, IList<string> zones, ulong seed, int round,
                               IList<string> own = null, IList<string> used = null)
    {
        List<DpRow> rows;
        var pick = DeploymentAnchors.ChooseScored(cands, anchors, graph, zones,
                                                  seed, round, own, used, out rows);
        return pick == null ? null : pick.Candidate.Guid;
    }

    // ---- 1) Null-Law: no fact => the legacy XOR line, bit-identical ------
    static void NullLawEquivalence()
    {
        var nameSets = new[]
        {
            new[] { "Green 1", "Green 2", "Red" },
            new[] { "DP Alpha", "DP Beta", "DP Gamma", "DP Delta" },
            new[] { "Blaue 1", "Rote 2", "Gruene 3", "Gelbe 4", "Violette 5" },
            new[] { "A" },
            new[] { "a", "A", "1", "", " " },
            new[] { "Ugly, Brute.", "Tusken   Raider", "Royal Guard" },
        };
        var seeds = new ulong[] { 0UL, 1UL, 1337UL, 403079UL, ulong.MaxValue, 0x2545F4914F6CDD1DUL };
        int cells = 0, bad = 0;
        foreach (var names in nameSets)
            foreach (var seed in seeds)
                foreach (int round in new[] { 1, 4, 8 })
                {
                    var cands = names.Select((n, i) => Cand("g" + i, n, "sec" + (i % 3))).ToList();
                    var legacy = LegacyWinner(cands, seed, round);
                    var scored = ScoredWinner(cands, new DeployAnchor[0], null, null, seed, round);
                    var wrap = DeploymentAnchors.Choose(cands, null, seed, round);
                    cells++;
                    if (scored != legacy || wrap.Candidate.Guid != legacy) bad++;
                }
        Ok(bad == 0, "Null-Law: scored path == legacy XOR line on " + cells + " cells");
    }

    // ---- 2) dynamic section anchor ----------------------------------------
    static void DynamicAnchorLaw()
    {
        var cands = new List<DeployCandidate>
        {
            Cand("far-in", "Far Inside", "SEC-Z", 1000, 1000),   // coordinates meaningless
            Cand("out-1", "Outside Near", "SEC-A", 0, 0),
        };
        // (a) whole-section tier claim, coordinates never leak:
        var win = ScoredWinner(cands, new[] { SectionOnly("SEC-Z") }, null, null, 12345UL, 1);
        Ok(win == "far-in", "dyn anchor: section tier beats every outside DP despite 1000-unit coords");
        // (b) same-section candidates TIE at the anchor stage (no position
        //     fact): the seed chain decides, not the coordinates - proven by
        //     flipping coordinates only and seeing the winner stay identical.
        var tie = new List<DeployCandidate>
        {
            Cand("t1", "Tie One", "SEC-Z", 0, 0),
            Cand("t2", "Tie Two", "SEC-Z", 9999, 9999),
        };
        var w1 = ScoredWinner(tie, new[] { SectionOnly("SEC-Z") }, null, null, 7UL, 2);
        var flipped = new List<DeployCandidate>
        {
            Cand("t1", "Tie One", "SEC-Z", 9999, 9999),
            Cand("t2", "Tie Two", "SEC-Z", 0, 0),
        };
        var w2 = ScoredWinner(flipped, new[] { SectionOnly("SEC-Z") }, null, null, 7UL, 2);
        Ok(w1 == w2, "dyn anchor: intra-section coords never leak (winner stable under flip)");
        // (c) positioned co-anchor keeps the precision:
        var both = new[] { SectionOnly("SEC-Z"), Anchored("a1", "SEC-Z", 0, 0) };
        var wPos = ScoredWinner(tie, both, null, null, 7UL, 2);
        Ok(wPos == "t1", "dyn anchor: positioned co-anchor still measures (t1 at the anchor)");
    }

    // ---- 3) battle-zone stage ---------------------------------------------
    static void ZoneStageLaw()
    {
        var g = new SectionGraph();
        g.AddSection("SEC-A"); g.AddSection("SEC-B"); g.AddSection("SEC-C");
        g.ConfirmEdge("SEC-A", "SEC-B");
        g.ConfirmEdge("SEC-B", "SEC-C");
        // SEC-ORPH: known but unrouted => claimless.
        g.AddSection("SEC-ORPH");

        // claim beats no-claim
        var c1 = new List<DeployCandidate> { Cand("claim", "Claim", "SEC-C"), Cand("void", "Void", "SEC-ORPH") };
        Ok(ScoredWinner(c1, new DeployAnchor[0], g, new[] { "SEC-A" }, 42UL, 1) == "claim",
           "zone: recorded route beats no recorded route");
        // nearer beats farther
        var c2 = new List<DeployCandidate> { Cand("near", "Near", "SEC-B"), Cand("far", "Far", "SEC-C") };
        Ok(ScoredWinner(c2, new DeployAnchor[0], g, new[] { "SEC-A" }, 42UL, 1) == "near",
           "zone: nearer hop distance wins");
        // two claimless stay tied => seed chain decides, stable under zone-list shuffle? no:
        // prove the TIE by showing the winner equals the legacy seed-chain winner.
        var c3 = new List<DeployCandidate> { Cand("v1", "V1", "SEC-ORPH"), Cand("v2", "V2", "SEC-B") };
        Ok(ScoredWinner(c3, new DeployAnchor[0], g, new string[0], 42UL, 1) == LegacyWinner(c3, 42UL, 1),
           "zone: empty zone list keeps the seed chain (no penalty for the known section)");
        // tier primat over a zone claim
        var c4 = new List<DeployCandidate>
        {
            Cand("in-anchor", "In Anchor", "SEC-ORPH"),
            Cand("zone-zero", "Zone Zero", "SEC-A"),
        };
        Ok(ScoredWinner(c4, new[] { Anchored("a9", "SEC-ORPH", 0, 0) }, g, new[] { "SEC-A" }, 42UL, 1)
           == "in-anchor", "zone: anchor tier keeps Primat over zone=0");
    }

    // ---- 4) ladder order --------------------------------------------------
    static void LadderOrder()
    {
        // zone claim beats leash
        var g = new SectionGraph();
        g.AddSection("SEC-A"); g.AddSection("SEC-B"); g.ConfirmEdge("SEC-A", "SEC-B");
        var c1 = new List<DeployCandidate>
        {
            Cand("closer", "Closer", "SEC-A"),
            Cand("own", "Own", "SEC-B"),
        };
        Ok(ScoredWinner(c1, new DeployAnchor[0], g, new[] { "SEC-A" }, 1UL, 1, new[] { "SEC-B" })
           == "closer", "ladder: zone distance decides BEFORE leash");
        // leash beats diversity
        var c2 = new List<DeployCandidate>
        {
            Cand("own-stale", "Own Stale", "SEC-1"),
            Cand("fresh", "Fresh", "SEC-2"),
        };
        Ok(ScoredWinner(c2, new DeployAnchor[0], null, null, 1UL, 1,
                        new[] { "SEC-1" }, new[] { "own-stale" }) == "own-stale",
           "ladder: leash decides BEFORE diversity");
        // diversity beats the seed chain (seed rigged to prefer the stale one
        // would be unverifiable here - the RELATION is the law: fresh wins
        // whenever the earlier stages tie, whatever the seed says)
        var c3 = new List<DeployCandidate>
        {
            Cand("stale", "Stale", "SEC-1"),
            Cand("fresh", "Fresh", "SEC-2"),
        };
        foreach (var seed in new ulong[] { 0UL, 1337UL, ulong.MaxValue })
            if (ScoredWinner(c3, new DeployAnchor[0], null, null, seed, 3, null, new[] { "stale" })
                != "fresh")
                Ok(false, "ladder: diversity beats seed chain for seed " + seed);
        Ok(true, "ladder: diversity beats the seed chain (all probe seeds)");
        // determinism replay
        var replayA = ScoredWinner(c3, new DeployAnchor[0], null, null, 9UL, 9);
        var replayB = ScoredWinner(c3, new DeployAnchor[0], null, null, 9UL, 9);
        Ok(replayA == replayB, "determinism: identical inputs, identical pick");
    }
}
