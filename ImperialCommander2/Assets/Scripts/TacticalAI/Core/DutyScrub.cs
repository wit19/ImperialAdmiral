// P37d (card t_ffc705af, concept docs/CONCEPT_missions_duty.md 9.3 + 7
// Abhaengigkeits-Gesetz): the duty briefing scrub law. In tactical mode the
// Duty-Ast decides WHO serves the mission objective per round (seeded quota
// dice, 1b) - so no briefing text may pre-attribute that duty to named legacy
// figure classes ("Imperial Officers and Stormtroopers will usher the
// prisoners towards the cell" - the HOTH10 example from 1a/9.3, the app's
// legacy instruction-override splitting). The MedPac precedent: a mechanic
// the tactical layer replaces must not be promised by mission prose either.
// Scope law (9.3): ONLY the duty missions (mission list = the dutyAnchors
// artifact, decided by the caller behind DutyAnchors.ForMission) are ever
// touched; the table below is keyed BY MISSION FILE, so a sentence of one
// mission can never scrub another mission's text. All other missions stay
// byte-identical (DutyScrubGate proves pattern-specificity over the corpus).
// A legacy sentence is a multi-family conjunction, every family in its own
// language-stem list (MedPac's loanword trick, 4 families so generic
// "stormtroopers capture the prisoners" flavor NEVER matches - the global
// leak scan in DutyScrubGate pins zero hits outside the inventory).
// Pure function: no IO, no Unity, no culture-sensitive casing (invariant).
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// <summary>One mission's scrub law: a sentence is legacy duty splitting
    /// only when EVERY family has at least one stem inside it (lowercased,
    /// invariant). Families are language-stem lists covering the shipped
    /// translations (Br De En Es Fr Hu It Nl No Pl Ru - languages without a
    /// mission injection read the EN canonical, whose stems are listed too).</summary>
    public sealed class DutyScrubLaw
    {
        public readonly string[][] Families;

        public DutyScrubLaw(string[][] families) { Families = families; }

        /// <summary>Oracle predicate shared by Scrub and by the gate scan -
        /// one law, one implementation (no mirror drift).</summary>
        public bool IsLegacy(string lowerSentence)
        {
            if (string.IsNullOrEmpty(lowerSentence)) return false;
            for (int f = 0; f < Families.Length; f++)
            {
                var fam = Families[f];
                bool hit = false;
                for (int i = 0; i < fam.Length; i++)
                    if (lowerSentence.IndexOf(fam[i], StringComparison.Ordinal) >= 0)
                    { hit = true; break; }
                if (!hit) return false;
            }
            return true;
        }
    }

    public static class DutyScrub
    {
        // Figure-class families - BOTH imperial classes must be named (the
        // legacy override texts always pair Officer AND Stormtrooper; a lone
        // "stormtroopers capture the rebels" story line is flavor, not duty).
        static readonly string[] Officer =
            { "offic", "offiz", "oficer", "oficia", "офиц" };
        static readonly string[] Trooper =
            { "stormtroop", "sturmtrupp", "szturmow", "штурмовик", "soldado de asalto",
              "soldados de asalto" };
        // HOTH10: duty object + goal word (the ushering TARGET distinguishes
        // the duty sentence from every generic prison/capture flavor line).
        static readonly string[] Prisoner =
            { "prisoner", "gefange", "prisonnier", "prisioner", "gevangene", "więźni", "заключ" };
        static readonly string[] Cell =
            { "cell", "zell", "celda", "cellu", "gevangenis", "celi", "камер" };
        // OTHER9: duty object + "when instructed" (the sentence is the legacy
        // instruction-override itself; "stormtroopers round up the wookiees"
        // story flavor has neither the officer class nor the instruction).
        static readonly string[] Wookiee =
            { "wookie", "вуки" };
        static readonly string[] Instructed =
            { "instructed", "anweisung", "instrucc", "instruction", "opgedragen",
              "instrukcj", "инструкц", "indica" };

        // Inventory 08.11. (card t_ffc705af INVENTAR, measured over ALL 138
        // canonical mission files + every shipped mission injection, briefing
        // channels): exactly two duty missions carry a legacy duty sentence;
        // CORE23/CORE27/CORE28/HOTH12/OTHER31 briefings are already
        // REF-clean (their unit-class sentences are printed mission rules -
        // the CORE23 claim law, the CORE27 courier deployment - or story
        // flavor, never an override splitting). Missions absent from this
        // table are NEVER scrubbed, even when duty-classed.
        static readonly Dictionary<string, DutyScrubLaw> ByMission =
            new Dictionary<string, DutyScrubLaw>(StringComparer.OrdinalIgnoreCase)
        {
            { "HOTH10.json", new DutyScrubLaw(new[] { Officer, Trooper, Prisoner, Cell }) },
            { "OTHER9.json", new DutyScrubLaw(new[] { Officer, Trooper, Wookiee, Instructed }) },
        };

        public static bool HasPatterns(string missionFileName)
            => missionFileName != null && ByMission.ContainsKey(missionFileName);

        static DutyScrubLaw LawFor(string missionFileName)
        {
            DutyScrubLaw law;
            return missionFileName != null && ByMission.TryGetValue(missionFileName, out law) ? law : null;
        }

        /// <summary>Split ONE line into sentence spans: a span runs to the
        /// first terminator [.!?] (inclusive); a tail without terminator is
        /// its own span. Bullet glyphs ({-}) ride inside the span that
        /// follows them, so a full bullet line is one span and vanishes whole
        /// (the MedPac line law).</summary>
        public static List<string> SentenceSpans(string line)
        {
            var outp = new List<string>();
            if (line == null) return outp;
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '.' || c == '!' || c == '?')
                {
                    outp.Add(line.Substring(start, i - start + 1));
                    start = i + 1;
                }
            }
            if (start < line.Length) outp.Add(line.Substring(start));
            return outp;
        }

        /// <summary>Gate oracle: every legacy sentence of this text under this
        /// mission's law (trimmed, in document order). Counts and content are
        /// compared against the pinned inventory - fail-closed drift detector.</summary>
        public static List<string> LegacySentences(string missionFileName, string text)
        {
            var outp = new List<string>();
            var law = LawFor(missionFileName);
            if (law == null || string.IsNullOrEmpty(text)) return outp;
            foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
                foreach (var span in SentenceSpans(line))
                    if (law.IsLegacy(span.ToLowerInvariant()) && span.Trim().Length > 0)
                        outp.Add(span.Trim());
            return outp;
        }

        /// <summary>The scrub: remove every legacy sentence (span) under the
        /// mission's law, drop lines the scrub emptied, collapse the orphaned
        /// paragraph gaps. Fail-open like the MedPac law: text that would go
        /// fully blank is returned untouched (never an empty briefing box).
        /// removed = number of sentence spans deleted (the inventory currency).</summary>
        public static string Scrub(string missionFileName, string text, out int removed)
        {
            removed = 0;
            var law = LawFor(missionFileName);
            if (law == null || string.IsNullOrEmpty(text)) return text;
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var kept = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                var spans = SentenceSpans(line);
                var sb = new System.Text.StringBuilder();
                bool touched = false;
                foreach (var span in spans)
                {
                    if (law.IsLegacy(span.ToLowerInvariant()))
                    { removed++; touched = true; continue; }
                    sb.Append(span);
                }
                // an untouched line ships verbatim, even if it looks empty
                kept.Add(touched ? sb.ToString() : line);
            }
            if (removed == 0) return text;
            var joined = new System.Text.StringBuilder();
            for (int i = 0; i < kept.Count; i++)
            {
                if (kept[i].Trim().Length == 0 && lines[i].Trim().Length > 0)
                    continue;   // line existed before and is empty only because we scrubbed it
                joined.Append(kept[i]);
                if (i < kept.Count - 1) joined.Append('\n');
            }
            var result = System.Text.RegularExpressions.Regex
                .Replace(joined.ToString().TrimEnd('\n'), @"\n{3,}", "\n\n");
            if (result.Trim().Length == 0) return text;   // blank-box law (MedPac)
            return result;
        }
    }
}
