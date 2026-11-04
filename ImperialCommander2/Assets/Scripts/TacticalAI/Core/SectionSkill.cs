// S-C (Thomas-Gesetz 04.11., PLAN_sektionsraum Revision IV.1 Kopf): the
// SKILL CURVE for the section space. There are NO feature switches - skill
// is the dial. The law, verbatim from the plan:
//
//   Die EREIGNISGETRIEBENEN AUSNAHMEN (Wachzone, Vergeltung, 3c) greifen
//   auf ALLEN Skill-Stufen - Fakt-Reaktion ist Fairness, kein
//   Intelligenz-Bonus. Skill-gated sind ausschliesslich:
//   Masken-VERFEINERUNG, Posture-Clamps und Support-Navigation.
//
// So this file gates EXACTLY the three intelligence channels and nothing
// else. Low rungs (S1, S2 - the reference ladder) and the absence of a
// profile (reference path, session not up, settings off) all read FALSE:
// the projection then carries only feed FACTS (deploy seed, dilation,
// observed speed, graph edges, alive bookkeeping) and behaves bit-identical
// to the chain-entry state for every consumer that reads it (pinned as a
// byte-identity law in section_trust.json: same event stream, refine OFF
// equals refine ON whenever the stream holds no refinement carriers; the
// S-B canon section itself stays the refined reading at its default).
//
// The exception classes themselves are NOT here on purpose:
// SectionSpace.WachzoneFacts / ThreeCFacts / VictimRingOf never consult
// this file - they fire on positive facts at every skill level (the gate
// proves it with a coarse-mode Wachzone run). Ordering is S-D's; the keys
// that CONSUME trust ride the trust table (SectionTrust), which is itself
// gated through the profile passed in here.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public static class SectionSkill
    {
        // "Mittlere/hohe Skill-Stufen" = the overlord rungs S3, S4, S5.
        static readonly HashSet<string> HighRungs = new HashSet<string>(StringComparer.Ordinal)
        {
            "S3", "S4", "S5",
        };

        static bool High(SkillProfile profile)
        {
            return profile != null && HighRungs.Contains(profile.Level ?? "");
        }

        /// Mask VERFEINERUNG (Eichung re-groundings, wounded group
        /// disjunctive, door transit hypotheses): S3+ only. S1/S2/null: the
        /// belief stays seed-and-dilation, the chain-entry reading.
        public static bool MaskRefinementEnabled(SkillProfile profile) { return High(profile); }

        /// Posture-Clamp-Grundlagen (breakthrough clamp from the posture
        /// classifier's section basis): S3+ only.
        public static bool PostureClampBasisEnabled(SkillProfile profile) { return High(profile); }

        /// Support-Navigation (mask-informed support target choice): S3+
        /// only.
        public static bool SupportNavigationEnabled(SkillProfile profile) { return High(profile); }
    }
}
