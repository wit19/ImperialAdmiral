// P38 (card t_11ebb843, playtest Empire3): the NEUTRAL PREY law — who is a
// neutral bystander object ("Beute-Objekt") and how many units may chase one.
//
// THE PROBLEM this law answers (playtest): the OBJEKT-Ziel (TryObjectTarget)
// ran per unit without any cap — every stormtrooper hunted the SAME neutral
// protester (Health 5, Defense None): cannon fodder, fun collapse. The fix
// mirrors the two P37 laws (Mischung statt Vollzugriff), scoped NARROWLY:
// combat objects, doors, terminals, warships and every other POLAR row keep
// the P36 law bit-identically. No EMPIRE3 special case — the class is
// defined generally and applies to every mission that carries such objects.
//
// APPLICATION SCOPE (card §Scope-Sperre, all three legs, hard):
//   1. the object rides a POLAR prey line in the feed (polaritaet "jagt");
//   2. entityType Token — the app's authored entity-type field (value 4).
//      Doors (Door), terminals (Terminal) and the Warship highlight are
//      locked out by the type leg ALONE (measured: Protester=Token,
//      Northern Door=Door, Terminal1=Terminal, Warship=Highlight);
//   3. the BYSTANDER criterion below (the census decision).
// The class may be EMPTY in a mission (no jagt-Token carries person wiring):
// then the old P36 rule stands unchanged for that mission (Null-Law).
//
// BYSTANDER-KRITERIUM (Zensus-Entscheidung, docs/CENSUS_beute_objekte.md):
// an object is a NEUTRAL BYSTANDER when its public interaction wiring speaks
// PERSON vocabulary: the hero-facing button texts / wired event names carry
// a person stem — claim / rescue / defeat / reveal (EN) or the authored-DE
// stems Anspruch/beanspruchen, retten/befreien, besiegen/vernichten*,
// enthüllen. Printed person semantics back this: persons are claimed,
// rescued, defeated, revealed; THINGS are destroyed — and the shipped
// corpus measured 47/47 exact (9x Protester EMPIRE3, 4x Refugee OTHER11
// DRIN; every device — Barricade/Beacon/Cell/Checkpoint/Data Core/Engine/
// Security Station/Siege Wall/Support/Weak Point/Window — wires a bare
// "Destroy" button and is RAUS). * "vernichten" is the authored-DE stem of
// the defeat verb family; the census pinned it alongside, EN "destroy" is
// NOT in the family and stays a device stem.
// Markedness of the ALTERNATIVES the census examined and rejected:
//   markerType (Neutral on both sides), tokenColor/entityColor (Gray on
//   both sides), the faction named in the attack sentence ("A figure can
//   attack" also on Security Station OTHER37), the printed attack wording
//   (refugees say "can attack", not claim). Only the interaction-vocabulary
//   law reproduces the extensional assignment EXACTLY.
//
// RULETTREUE: attacking bystanders stays a rules-legal path (EMPIRE3: "If a
// protester is defeated, the Empire claims it"). P38 mixes only the NUMBER
// of chasers — no ban, no thematic interference.
//
// Null-Law: no entityType key (hand-pilled pre-P38 rows, older fixtures) =>
// never a bystander => every pre-P38 vector byte-identical.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public static class NeutralPrey
    {
        /// The entityType value this law may ever touch (app enum name,
        /// lower-cased on the feed, see WorldEventsUnity producer).
        public const string TokenType = "token";

        // Person stems (public authoring vocabulary, EN + authored-DE; same
        // two-vocabulary discipline as the polarity ladder P34-A1/A2). They
        // live on the RAW public evidence (buttons/eventNames), never here
        // as a producer verdict.
        static readonly string[] PersonStems =
            { "claim", "besetz", "anspruch",
              "rescue", "rette", "befrei",
              "defeat", "besieg", "vernicht",
              "reveal", "enthüll" };

        /// The person-evidence pool: the RAW public strings (button texts +
        /// wired event names, "; "-joined rows). Deliberately NOT the P1
        /// EvidenceStrings pool: the ladder's verdicts (P1-P4) stay exactly
        /// what they are — the refugee's "Defeat" button must NOT re-polarise
        /// its row; the bystander class is an ORTHOGONAL derived flag.
        public static List<string> PersonEvidence(FeedWorldView.MissionObjectFact o)
        {
            var l = new List<string>();
            if (o == null) return l;
            AddParts(l, o.Buttons);
            AddParts(l, o.EventNames);
            return l;
        }

        static void AddParts(List<string> l, string joined)
        {
            if (string.IsNullOrEmpty(joined)) return;
            foreach (var s in joined.Split(';'))
            {
                var t = (s ?? "").Trim().ToLowerInvariant();
                if (t.Length > 0) l.Add(t);
            }
        }

        /// The BYSTANDER criterion (type leg + person-wiring leg). The jagt
        /// leg lives in the CONSUMER (the class answers "is this a neutral
        /// person?" — "is it a target?" is the ladder's answer). Unknown
        /// entityType ("", pre-P38 row) => false: the Null-Law twin.
        public static bool IsBystander(FeedWorldView.MissionObjectFact o)
        {
            if (o == null) return false;
            if (!string.Equals(o.EntityType, TokenType, StringComparison.Ordinal))
                return false;
            foreach (var s in PersonEvidence(o))
                foreach (var stem in PersonStems)
                    if (s.Contains(stem)) return true;
            return false;
        }

        /// The full application scope (card §Scope-Sperre): POLAR prey line
        /// AND bystander. Combat-object rows never pass (polaritaet != jagt).
        public static bool IsInScope(FeedWorldView.MissionObjectFact o)
        {
            return o != null && o.Polaritaet == "jagt" && IsBystander(o);
        }
    }
}
