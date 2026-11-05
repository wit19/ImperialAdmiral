// M6 Overlord focus layer (concept v4.1 3.3 + 3.5, S4/S5 sharpening).
//
// The overlord plays the SAME legal moves as every other skill level - what
// differs is the QUALITY of public-information optimisation. This module is
// that optimisation for target choice, and it consumes EXACTLY two inputs:
//
//   (1) FeedWorldView - the deterministic projection of the passive public
//       world feed (M5): wounded/defeated flags, focus MARKS (aim tokens).
//       No positions, no hidden state, no dice. A human GM sees the very
//       same facts at the table.
//   (2) HeroCard (M11b, tactical_hero_cards.json) - the PRINTED hero card
//       values of all 21 campaign heroes (healthy + wounded side, weapon
//       dice, items via the app campaign state); HeroThreatProfile stays the
//       role/chip vocabulary and the fallback for rows outside the app
//       roster (concept 3.3.1a). Every W component is vector-ported sim
//       truth (hero_profiles.json, hero_w.json).
//
// FAIRNESS CANON (concept 3.3.1/12, hard constraint): ranking only ever
// PERMUTES the candidate set - it never filters, never invents a target,
// never reads anything outside the two inputs above. Every skill level may
// choose any living hero (identical legal action set); overlord merely picks
// the economically best one and can say WHY in public terms:
//
//   key (lexicographic, all exact arithmetic, no floats, no RNG):
//     1. aim mark   - the human's own public designation (FocusChange). All
//                     groups converge on it: focus fire, table-visible.
//     1b. reaktion  - R-D (concept eventreaktionen v1.2 §2.3, Thomas-Punkt
//                     3a): the hero who shot THIS activating figure inside an
//                     open hero window THIS round (R-A feed attribution
//                     LastShotHero/LastShotRound) leads right behind the aim
//                     mark - the human's designation at the throne stays king
//                     - and BEFORE the counter law. One key, set per
//                     activation and consumed with it (no carry-over: the
//                     shot-round must equal the live round). WACH exception
//                     (ReactionHeroFor): a guard anchor at the mission
//                     objective (Guardhold: the objective text names the
//                     figure, or the group rolled the guard doctrine - R-C
//                     anchor law reused) never sets the key; it values the
//                     normal greatest-threat-to-the-objective head instead.
//                     S1 blind (same disclosed handicap as the R-C order
//                     layer): the key runs from S2 up, exactly like R1.
//     2. counter    - "woundAll": two counter-law slots (wounded-focus fix,
//                     Thomas 03.11): FIRST healthy-beats-wounded (the sim's
//                     leading TargetValue slot - every hit must move the
//                     healthy->wounded counter, a wounded hero is already
//                     counted), then the sim ChipKey verbatim (healer last)
//                     - counter law outranks economics. The wound economy
//                     x2 is OFF entirely under woundAll (finishing a hero
//                     the counter already counts earns nothing).
//     3. economy    - posture-keyed threat x wound-economy multiplier. On
//                     the M11b hero-card basis: expected damage of the real
//                     printed dice pool (weapon + known loadout items)
//                     against the engaging imperial figure's defense, or the
//                     real-tempo runner value under protect/stall. Cost/
//                     benefit instead of the raw score: a wounded hero still
//                     projects his printed threat until he is out, but costs
//                     only about half the engagement to finish - the
//                     danger-per-shot DENSITY doubles. Rational x2, exact.
//                     KILLABLE GATE (wounded-focus fix): on the per-figure
//                     ranking the x2 is earned only by figures whose printed
//                     damage EV reaches the hero's printed wounded-side bar
//                     (the sim ERLEDBAR law) - the x2 is the economy of
//                     FINISHING, and a figure that cannot finish earns
//                     nothing. The round-level projection (no figure EV)
//                     keeps the classic blanket x2 the vectors pin.
//                     M11d STRAIN PRESSURE (light, capped): a hero whose
//                     table strain markers reached his printed reaction
//                     buffer cannot pay his costly reactions for a moment ->
//                     +8 on this key ONLY (StrainPressure.Druck), after the
//                     objective-actor +1, BEFORE the tie-chain (5) - never
//                     a key of its own: marks (1), chip law (2), wounded
//                     (4) and a >8 econ gap all stay untouched. Null strain
//                     source (live app today: markers are not modelled) =
//                     zero influence; the polling seam is documented at the
//                     bridge's candidate builder. M11h sits on the SAME
//                     slot with the FUZZY USAGE PRIOR (rounds vs the
//                     reachable buffer, +4/+8, public round count only)
//                     and combines with the exact rule as MAX, never sum.
//     4. reference  - no curated profile: the S1-S3 public proxy ranking
//                     (wounded, printed attack dice, printed health).
//     5. tiebreak   - squad row (stable, deterministic), then hero id.
//
// Golden vectors: Assets/Tests/Fixtures/tacai/overlord_focus.json (C#-side
// pinned via --record, same precedent as world_feed.json: this layer sits on
// the APP feed, the sim has no feed - its W components stay sim-vectored).
using System;
using System.Collections.Generic;
using System.Linq;

namespace TacticalAI.Core
{
    /// One living hero as the overlord sees him: public facts only.
    public sealed class FocusCandidate
    {
        public string HeroId;
        public int SquadRow;
        public HeroThreatProfile Profile;   // curated table hit or null
        public bool Wounded;                // feed HeroView (public tracker)
        public int Marks;                   // feed aim tokens (FocusChange)
        public int MarkedRound;
        public int AttackDice;              // printed card values (reference parity)
        public int Health;
        // M10b (Thomas revision 27.10): NO hero cell positions - the app
        // cannot see where a hero stands. The objective attribution is built
        // from public events only: objective progress happened, then a hero
        // finished his activation -> he gets the credit (feed vector).
        public bool ObjectiveActor;
        // M11b: his PRINTED hero card (tactical_hero_cards.json, all 21 app
        // heroes). Null only for rows outside the app roster (sim fixture
        // heroes) - those keep the M2a.4 curated fallback.
        public HeroCard Card;
        // M11d: strain markers the TABLE has placed on him. Null = the
        // source cannot see strain at all (live app today -> ready-to-wire,
        // zero influence). The buffer is NOT caller-supplied: it is computed
        // from his Card (StrainPressure.Budget) so no caller can smuggle a
        // bigger one in (fairness canon).
        public int? StrainMarkers;
        // M11d: the hero's CURRENT unspent campaign XP (CampaignHero.xpAmount
        // via the bridge; optional in fixtures). Gates only KNOWN-cost
        // reactions in StrainPressure.Budget - value-neutral on today's
        // roster (every known-cost row prints strain 0), future-proof if a
        // strain-bearing purchasable ability ever joins the tables.
        public int? AvailableXp;
        // M11h: completed hero activations in THIS mission, public feed
        // state (HeroView.Activations - the activation tracker every player
        // at the table counts, see bridge). Drives the FUZZY strain-usage
        // prior (StrainPressure.Unscharf): a prior on markers, NOT marker
        // knowledge. null = no round source (fixture default for a silent
        // row); 0 = round 1 of the mission - prior level 0, bit-identical
        // to pre-M11h canon. The buffer stays computed from his Card, the
        // caller can only ever supply the PUBLIC round count.
        public int? RoundsCompleted;
        // M12d: the table sees a printed focus-condition card effect on this
        // hero (Stunned/Disarmed/Prone/Slowed/Pinned/Weakened on his figure
        // sheet - public tracker state, the same class of knowledge as the
        // strain markers). Only consulted when the caller passes a
        // cardSurcharge (owned focus_surcharge rows); false default keeps
        // every pre-M12d fixture bit-identical.
        public bool Conditioned;
        // Player-19 (Thomas 03.11): round of the ENGAGEMENT credit - either
        // the terminal objective credit (feed ObjectiveActorRound) or the
        // LAST strike on the figure the objective names (feed StrikeRound).
        // 0 = not engaged (bit-identical pre-Player-19 canon). Where the
        // engaged tier holds more than one hero, the most recent engagement
        // leads - the human GM looks at what just HAPPENED on the table.
        public int EngagedRound;

        public bool Marked { get { return Marks > 0; } }
    }

    // R-D (concept v1.2 §2.3) NOTE: the reaktion key does NOT extend
    // FocusCandidate - like `mark` it is a PER-HERO comparison signal the
    // caller passes once per Rank call (reactionHero parameter, Row.Reaktion
    // below), so every pre-R-D candidate construction stays bit-identical.

    /// The decision, with its explainable public parts (ui.json localises).
    public sealed class FocusPick
    {
        public string HeroId;
        public bool Marked;
        public bool Wounded;
        public Rational? Threat;            // printed threat BEFORE economy
        public Rational? ThreatEconomic;    // after the wound-economy multiplier
        // M15 (Player-26 audit): the threat's operands, printed verbatim by
        // the EXPLAIN cell and the threat accounting. Populated ONLY on card
        // rows under protect/stall (where the threat is NOT just the damage
        // EV - eliminate rows read their damage straight from Threat, every
        // pre-M15 canon vector stays byte-identical, record discipline).
        public Rational? ThreatDamage;      // exchange EV per round (best pool)
        public Rational? ThreatMobility;    // band x range subordination term
        public string BestPool;             // dice the damage EV came from
        public string Basis;                // "mark"|"chip"|"card"|"economy"|"proxy"
        // M11d: the LIVE strain-pressure surcharge actually applied to the
        // econ key this call: 8 (buffer full), 0 (source visible, below the
        // buffer) or null (no strain source / no buffer). The public WHY
        // cell fires on == Druck only.
        public int? StrainDruck;
        // M11h: the fuzzy round-prior stage (0/4/8, StrainPressure.Unscharf)
        // for THIS row: 0 = visible rounds, no stage reached; null = no
        // round source or buffer-less hero (never a signal). The ECON key
        // carries MAX(exact, fuzzy), never the sum - both sources describe
        // the same shortage (combination law pinned in strain_budget /
        // overlord_focus vectors).
        public int? StrainDruckUnscharf;
        // M11h WHY args (only set when the fuzzy stage fires and the exact
        // rule did not): the public round count and the reachable ceiling
        // U it was measured against -> "wahrscheinlich angespannt
        // (Runde r/U)".
        public int? StrainPriorRunden;
        public int? StrainPriorDecke;
        // R-D (concept v1.2 §2.3): true on the row whose hero carries the
        // reaktion key of THIS activation (he shot the activating figure
        // inside an open window this round and no WACH exception suppressed
        // it). The AsDict "reaktion" key appears ONLY where it is true -
        // every pre-R-D canon vector stays byte-identical (record discipline).
        public bool Reaktion;
        // S-D (concept positionierung v1 §13.4, card t_c52dd34b): the two
        // section-space reaction keys of THIS activation (Wachzone = hero
        // factually at the mission-critical object inside the guard's mask;
        // Vergeltung = the mask-sharpened shooter candidate, only a
        // suspicion the fact track left open). AsDict keys appear ONLY
        // where true - every pre-S-D canon row stays byte-identical.
        public bool Wachzone;
        public bool Vergeltung;
        // S-E (concept positionierung v1 Z6/D4, card t_9dd56737): the
        // supporter's mask-trust key of THIS activation - the hero factually
        // anchored in the supporter's trust-gated own section ("Feuer in die
        // richtige Sektion"). AsDict key appears ONLY where true - every
        // pre-S-E canon row stays byte-identical.
        public bool Maskenvertrauen;

        /// Comparable vector row (harness/NUnit pin), exact string fields.
        /// The strain_druck key appears ONLY on rows where the pressure
        /// fired - the pre-M11d canon of every strain-free case is byte-
        /// identical (record discipline: no unexplained Expect diffs).
        /// M11h: strain_druck_unscharf likewise appears ONLY where the
        /// fuzzy stage fired (> 0), so every pre-M11h row stays identical.
        public Dictionary<string, object> AsDict()
        {
            var d = new Dictionary<string, object>
            {
                { "hero", HeroId },
                { "marked", Marked },
                { "wounded", Wounded },
                { "threat", Threat.HasValue ? Threat.Value.ToString() : null },
                { "threat_econ", ThreatEconomic.HasValue ? ThreatEconomic.Value.ToString() : null },
                { "basis", Basis },
            };
            if (StrainDruck.HasValue && StrainDruck.Value > 0)
                d["strain_druck"] = StrainDruck.Value.ToString();
            // M15: the arithmetic operands - appear ONLY on the protect/stall
            // card rows that carry them (record discipline, see fields).
            if (ThreatDamage.HasValue)
            {
                d["threat_damage"] = ThreatDamage.Value.ToString();
                d["threat_mobility"] = ThreatMobility.HasValue
                    ? ThreatMobility.Value.ToString() : "0";
                if (!string.IsNullOrEmpty(BestPool)) d["best_pool"] = BestPool;
            }
            if (StrainDruckUnscharf.HasValue && StrainDruckUnscharf.Value > 0)
                d["strain_druck_unscharf"] = StrainDruckUnscharf.Value.ToString();
            if (Reaktion) d["reaktion"] = true;
            if (Wachzone) d["wachzone"] = true;
            if (Vergeltung) d["vergeltung"] = true;
            if (Maskenvertrauen) d["maskenvertrauen"] = true;
            return d;
        }
    }

    public static class OverlordFocus
    {
        /// Wound economy: the engagement to FINISH a wounded hero costs about
        /// half (the table tracker says so - public), while his printed threat
        /// keeps projecting until he is out. Danger-per-shot density x2, exact
        /// rational - NEVER a hidden stat bonus (fairness canon).
        public static readonly Rational WoundEconomy = new Rational(2);

        public static Rational? ThreatBase(HeroThreatProfile p, string posture)
        {
            if (p == null) return null;
            if (posture == "protect" || posture == "stall")
                // M15 damage-weighted law (mirrors HeroThreatModelV2.Threat):
                // damage EV x DamageWeight + the mobility/mission-denial term.
                // Curated rows carry the tabled EV (plain Rational - the
                // curation table always has one, NeutralEv on blank reads)
                // and the tabled runner value; the weight law is identical
                // to the card model.
                return HeroThreatModelV2.DamageWeight * p.EvDamagePerRound
                       + new Rational(p.RunnerValue);
            if (posture == "attrition" || posture == "eliminate")
                return p.EvDamagePerRound;                // damage race
            return null;                                  // deny: node rules
        }

        public static Rational? ThreatEconomic(HeroThreatProfile p, string posture, bool wounded)
        {
            var t = ThreatBase(p, posture);
            if (!t.HasValue) return null;
            // Wound economy applies where REMOVING the threat is the goal
            // (damage race): finishing a wounded hero costs about half, so his
            // printed threat carries double finishing efficiency. Under
            // protect/stall the value is the runner's mission-denial threat -
            // being wounded makes him LESS of a denier, not more, so the
            // value stays the printed one (wounded still breaks ties, key 4).
            bool race = posture != "protect" && posture != "stall";
            return wounded && race ? t.Value * WoundEconomy : t;
        }

        /// Rank ALL living candidates (permutation only - fairness test pins
        /// that the id-multiset is input-equal for every skill level).
        /// posture/counter come from the public feed derivation; counter
        /// "woundAll" applies the sim ChipKey (profile.py port, vector truth)
        /// AND the healthy-first slot (wounded-focus fix, Thomas 03.11).
        /// M11b: defenderDefense = the defense dice of the IMPERIAL FIGURE
        /// choosing its target (its printed deployment-card defense - public
        /// card data, identical against every candidate in one call; null ->
        /// no figure engaged yet, e.g. the round-level explain projection).
        /// attackerDamageEv (wounded-focus fix): the engaging figure's printed
        /// damage EV (VictoryHelpers.PrintedDamageEvExactColors over its attack
        /// dice). Where present it GATES the wound economy x2 by the sim's
        /// ERLEDBAR law (printed wounded-side bar <= EV) - only a figure that
        /// can actually finish the wounded hero sees him doubled; the rest of
        /// the army ranks by plain printed threat, so the army spreads across
        /// the healthy threats instead of magnetising onto one wounded bait.
        /// null = round-level projection (no engaging figure) - the classic
        /// blanket x2 the golden vectors pin, bit-identical to pre-fix canon.
        /// reactionHero (R-D, concept v1.2 §2.3): the hero carrying the
        /// reaktion key for THIS activation (build it with ReactionHeroFor);
        /// null/empty = key inactive = every pre-R-D vector byte-identical.
        /// wachzoneHero / vergeltungHero (S-D, concept positionierung v1 §10
        /// + §13.4, card t_c52dd34b): the two section-space reaction keys of
        /// THIS activation, resolved by SectionConsumers (Wachzone = the
        /// hero factually working at the mission-critical object this guard
        /// masks; Vergeltung = the mask-sharpened shooter candidate, ONLY a
        /// suspicion the fact track left open). Slot law as pinned on the
        /// card: Interrupt (reaktion) > Wachzone > Vergeltung, both below
        /// the human's aim mark. null/empty = keys inactive = every pre-S-D
        /// vector byte-identical (multiset fairness untouched: keys reorder,
        /// never re-scope the candidate set).
        /// supporterTrustHero (S-E, concept positionierung v1 Z6/D4 + Ziel-
        /// Matrix, card t_9dd56737): the supporter's mask-trust key - the
        /// hero factually anchored in the SUPPORTER group's trust-gated own
        /// section (SectionConsumers.SupporterTargetFor builds the identity;
        /// the channel speaks on the mask-VERFEINERUNG rung only). Slot 1e:
        /// the mask track rides behind the pinned fact-track law
        /// Interrupt > Wachzone > Vergeltung (that trio stays verbatim) and
        /// ahead of the economy - "Feuer in die richtige Sektion statt
        /// Bedrohungs-Schaetzung". null/empty = key inert, every pre-S-E
        /// vector byte-identical.
        public static List<FocusPick> Rank(IEnumerable<FocusCandidate> candidates,
                                               string posture, string counter,
                                               IList<string> defenderDefense = null,
                                               int cardSurcharge = 0,
                                               EngagementEffects engage = null,
                                               Rational? attackerDamageEv = null,
                                               string reactionHero = null,
                                               string wachzoneHero = null,
                                               string vergeltungHero = null,
                                               string supporterTrustHero = null)
        {
            var list = candidates == null ? new List<FocusCandidate>()
                                           : candidates.ToList();
            bool woundAll = counter == "woundAll";
            var table = HeroCards.Load();
            var rows = list.Select(c => new Row
            {
                C = c,
                Threat = ThreatOf(c, posture, defenderDefense, table, engage),
                Econ = EconOf(c, posture, counter, defenderDefense, table,
                              engage, attackerDamageEv),
                Parts = c.Card != null
                    ? HeroThreatModelV2.PartsOf(c.Card, table, c.Profile, c.Wounded,
                                                posture, defenderDefense, engage)
                    : null,
                Chip = woundAll && c.Profile != null ? c.Profile.ChipKey() : null,
            }).ToList();

            // R-D REAKTION key (concept v1.2 §2.3, Thomas-Punkt 3a): mark-style
            // per-hero comparison signal (FocusCandidate stays untouched). The
            // caller resolves it from the R-A feed attribution via
            // ReactionHeroFor; null/empty keeps EVERY row Reaktion=false, the
            // Compare slot never splits and the whole pre-R-D canon runs
            // byte-identical (the old fixture hash is the pin).
            if (!string.IsNullOrEmpty(reactionHero))
                foreach (var r in rows)
                    if (r.C.HeroId == reactionHero) r.Reaktion = true;

            // S-D SECTION-SPACE KEYS (concept positionierung v1 §10 + §13.4):
            // the two reaction keys the section belief resolves for THIS
            // activation (SectionConsumers.WachzoneDecisionFor /
            // VergeltungHeroFor build the identities). Same mark-style
            // per-hero signal as Reaktion - null/empty keeps every row
            // false, both slots inert, the whole pre-S-D canon runs
            // byte-identical.
            if (!string.IsNullOrEmpty(wachzoneHero))
                foreach (var r in rows)
                    if (r.C.HeroId == wachzoneHero) r.Wachzone = true;
            if (!string.IsNullOrEmpty(vergeltungHero))
                foreach (var r in rows)
                    if (r.C.HeroId == vergeltungHero) r.Vergeltung = true;
            // S-E SUPPORTER-MASKEN-VERTRAUEN key (Z6): same mark-style law -
            // null/empty keeps every row false, the slot stays inert, the
            // whole pre-S-E canon runs byte-identical.
            if (!string.IsNullOrEmpty(supporterTrustHero))
                foreach (var r in rows)
                    if (r.C.HeroId == supporterTrustHero) r.Maskenvertrauen = true;

            // M10b OBJECTIVE ACTOR: the hero credited with the last
            // objective progress raises his threat by EXACTLY +1 - "die
            // Bedrohung erhöht sich leicht", exact integer, public-event-
            // derived, never a hidden stat. (M11b: applies on the single
            // hero-card basis directly - the M10b purity guard, which used
            // to gate this to pure curation rounds, is gone.)
            foreach (var r in rows)
                if (r.C.ObjectiveActor && r.Econ.HasValue)
                {
                    r.Threat = r.Threat.Value + Rational.One;
                    r.Econ = r.Econ.Value + Rational.One;
                }

            // M11d STRAIN PRESSURE (Thomas: "erhöht sich leicht" - light and
            // NEVER a primary signal): a hero whose strain markers reached
            // his printed reaction buffer cannot pay his costly reactions
            // (heal, block, power moves) for a short window. +Druck on the
            // econ component ONLY, after the objective-actor +1 and before
            // the deterministic tie-chain (Compare keys 4/5): it flips ties
            // and <Druck gaps, nothing else. Null markers (live app today -
            // the polling seam at TacticalAIBridge.BuildFocusCandidates) or
            // a buffer-less hero (Onar Koma, Tress Hacnua) = zero influence.
            //
            // M11h FUZZY USAGE PRIOR sits EXACTLY beside it on the same
            // slot: an aggressive hero spends ~1 strain reaction per
            // completed activation (public round count r), so after r >= U
            // round-trips his reachable buffer (U = min(buffer, endurance
            // of the ACTIVE sheet side - endurance is the printed hard cap
            // on markers, Learn to Play v17 "Strain and Endurance"; U == B
            // on today's whole roster, pinned value-neutral like the XP
            // gate) he is PROBABLY unable to pay reactions for a moment:
            // +4 at half the ceiling, +8 at it - exactly the two M11d
            // stages, no scaling. The exact marker rule below keeps its
            // full printed form untouched; when both sources can fire they
            // combine as MAX, never a sum (same shortage, one econ key -
            // pinned by the m11h_max_not_sum overlord_focus case).
            foreach (var r in rows)
            {
                int budget = StrainPressure.Budget(r.C.Card, r.C.AvailableXp);
                r.Druck = StrainPressure.Pressure(r.C.StrainMarkers, budget);
                int? endurance = r.C.Card == null ? (int?)null
                    : (r.C.Wounded ? r.C.Card.VerwundeteAusdauer : r.C.Card.GesundeAusdauer);
                r.Unscharf = StrainPressure.Unscharf(r.C.RoundsCompleted, budget, endurance);
                r.Decke = StrainPressure.Ceiling(budget, endurance);
                int surcharge = System.Math.Max(r.Druck ?? 0, r.Unscharf ?? 0);
                if (surcharge > 0 && r.Econ.HasValue)
                    r.Econ = r.Econ.Value + surcharge;
            }

            // M12d CARD SURCHARGE (light, table-driven): the Empire owning
            // printed focus-condition cards (focus_surcharge hook rows)
            // raises EVERY conditioned candidate's threat by EXACTLY +1 -
            // the M10b objective-actor precedent ("die Bedrohung erhoeht
            // sich leicht"): flips ties and <1 gaps only, never outranks
            // strain (+4/+8), chip law or objective credit. The caller
            // passes 1 when an owned table row fires (ImperialCardEffects.
            // CardSurcharge) - the same value applies to the AI's own deck
            // AND the human Imperial player's deck (faction, not controller).
            if (cardSurcharge > 0)
                foreach (var r in rows)
                    if (r.C.Conditioned && r.Econ.HasValue)
                    {
                        r.Threat = r.Threat.Value + cardSurcharge;
                        r.Econ = r.Econ.Value + cardSurcharge;
                    }

            var ranked = StableSort(rows, (x, y) => Compare(x, y, woundAll));

            return ranked.Select(x => new FocusPick
            {
                HeroId = x.C.HeroId,
                Marked = x.C.Marked,
                Wounded = x.C.Wounded,
                Threat = x.Threat,
                ThreatEconomic = x.Econ,
                // M15 operands: ride ONLY on the card rows of a runner race
                // (protect/stall) - there the threat is a SUM and the
                // operands are the honest print. Eliminate card rows already
                // ARE pure damage (Threat itself), curated rows have no card
                // -> both stay key-free, every pre-M15 vector identical.
                ThreatDamage = x.C.Card != null && x.Parts.HasValue
                               && (posture == "protect" || posture == "stall")
                    ? x.Parts.Value.DamageEv : (Rational?)null,
                ThreatMobility = x.C.Card != null && x.Parts.HasValue
                               && (posture == "protect" || posture == "stall")
                    ? x.Parts.Value.MobilityTerm : (Rational?)null,
                BestPool = x.C.Card != null && x.Parts.HasValue
                               && (posture == "protect" || posture == "stall")
                    ? string.Join("+", x.Parts.Value.BestPool ?? new List<string>())
                    : null,
                StrainDruck = x.Druck,
                StrainDruckUnscharf = x.Unscharf,
                // WHY args ride along ONLY when the fuzzy stage actually
                // speaks (fired AND the exact marker rule stayed silent -
                // the exact cell "full strain buffer" is truth, the fuzzy
                // cell "wahrscheinlich" must never double-stamp it).
                StrainPriorRunden = x.Unscharf > 0 && !(x.Druck > 0) ? x.C.RoundsCompleted : (int?)null,
                StrainPriorDecke = x.Unscharf > 0 && !(x.Druck > 0) ? x.Decke : (int?)null,
                Reaktion = x.Reaktion,
                Wachzone = x.Wachzone,
                Vergeltung = x.Vergeltung,
                Maskenvertrauen = x.Maskenvertrauen,
                // R-D: "reaktion" rides behind "mark" (the designation stays
                // king) and pre-empts chip/economy - the lexicographic law the
                // Compare slot enforces; null-key rows never see the token.
                // S-D: "wachzone" then "vergeltung" ride directly behind
                // reaktion - the pinned slot law Interrupt > Wachzone >
                // Vergeltung (§13.4); both pre-empt chip/economy, both
                // null-inert for every pre-S-D vector.
                // S-E: "maskenvertrauen" rides behind the whole pinned trio
                // (the mask track never outranks a fact read, Z6 law) and
                // ahead of chip/economy - the supporter's "Feuer in die
                // richtige Sektion". Null-inert for every pre-S-E vector.
                Basis = x.C.Marked ? "mark"
                        : (x.Reaktion ? "reaktion"
                        : (x.Wachzone ? "wachzone"
                        : (x.Vergeltung ? "vergeltung"
                        : (x.Maskenvertrauen ? "maskenvertrauen"
                        : (x.Chip != null ? "chip"
                        : (x.Econ != null ? (x.C.Card != null ? "card" : "economy")
                                          : "proxy")))))),
            }).ToList();
        }

        /// <summary>R-D (concept eventreaktionen v1.2 §2.3, Thomas-Punkt 3a):
        /// the reaktion key for ONE activation - the hero who shot the
        /// activating figure inside an open hero window THIS round (R-A feed
        /// attribution on the group row), or null when the key must not fire.
        /// Pure projection over the feed view + the group's already-rolled
        /// doctrine pattern: no positions, no hidden state, no dice.
        /// Laws (all pinned in overlord_focus.json reaction_* cases):
        ///  - S1 blind: the whole reaction layer starts at S2, the key rides
        ///    R1's floor exactly (honest handicap, disclosed - same law the
        ///    R-C order layer documents). Focus is NOT an order topic, so it
        ///    gets no extra gate of its own (Thomas ruling, card t_41d42c7b).
        ///  - The shot must be from the LIVE round (LastShotRound == view.
        ///    Round): the key is consumed with this activation, no carry-over
        ///    effect into later rounds.
        ///  - WACH exception (Thomas formulation): a guard anchor AT the
        ///    mission objective never takes revenge - the Guardhold descriptor
        ///    (the objective text names the figure, WorldEventVector.TextNames
        ///    - the Player-19 shipped law) or the rolled guard doctrine (the
        ///    R-C guard-anchor leg) suppress the key; the group values the
        ///    normal greatest-threat-to-the-objective head (plain Rank top).
        ///    Existing law building blocks reused, no parallel law invented.
        /// Hero naming: the feed carries the hero's display name (WorldEvents
        /// Unity emits P("hero", name)); FocusCandidate.HeroId is the same
        /// string (bridge BuildFocusCandidates id = h.name ?? h.id) - the
        /// exact-identity match the R-C match law already relies on.</summary>
        public static string ReactionHeroFor(FeedWorldView view, string groupId,
                                             string pattern, int skill)
        {
            if (skill < 2) return null;                 // S1 blind (see doc)
            if (view == null || string.IsNullOrEmpty(groupId)) return null;
            var gv = view.FindGroup(groupId);
            if (gv == null || string.IsNullOrEmpty(gv.LastShotHero)) return null;
            if (gv.LastShotRound != view.Round) return null;   // consumed key
            // WACH exception - the two shipped anchor descriptors:
            bool named = !string.IsNullOrEmpty(view.Objective)
                         && !string.IsNullOrEmpty(gv.DisplayName)
                         && WorldEventVector.TextNames(view.Objective, gv.DisplayName);
            if (named || pattern == "guard") return null;
            return gv.LastShotHero;
        }

        /// <summary>M11b threat v2 for one candidate: printed hero card
        /// first (all 21 app heroes), M2a.4 curated profile as fallback for
        /// rows outside the app roster.</summary>
        static Rational? ThreatOf(FocusCandidate c, string posture,
                                  IList<string> defenderDefense, HeroCardTable table,
                                  EngagementEffects engage = null)
        {
            if (c.Card != null)
                return HeroThreatModelV2.Threat(c.Card, table, c.Profile,
                                                c.Wounded, posture, defenderDefense, engage);
            return ThreatBase(c.Profile, posture);
        }

        static Rational? EconOf(FocusCandidate c, string posture, string counter,
                                IList<string> defenderDefense, HeroCardTable table,
                                EngagementEffects engage = null,
                                Rational? attackerDamageEv = null)
        {
            var t = ThreatOf(c, posture, defenderDefense, table, engage);
            if (!t.HasValue) return null;
            // WOUNDED-FOCUS FIX (Thomas playtest reasoning 03.11), part 1 —
            // counter law: a woundAll END (healthy == 0, VictoryCounter.
            // Progress counts wounded-or-worse) is ALREADY satisfied by every
            // wounded hero. Finishing him earns ZERO counter progress, so the
            // wound economy would magnetise the whole army onto the one
            // target where no hit matters - the perfect rebel decoy. Under a
            // woundAll counter there is NO wound economy at all: fresh damage
            // belongs on the healthy (the healthy-first slot at key 2 does
            // the ordering; this kills the x2 that fought against it).
            if (c.Wounded && counter == "woundAll") return t;
            // same wound-economy law as ThreatEconomic: doubling applies in
            // the REMOVING races only; on card rows under protect/stall the
            // wounded penalty is already REAL (card-back tempo, M11b).
            bool race = posture != "protect" && posture != "stall";
            if (!(c.Wounded && race)) return t;
            // Part 2 — KILLABLE GATE: the x2 is the ECONOMY OF FINISHING
            // ("about half the engagement left"), so a figure earns it only
            // where it can plausibly finish the wounded hero: his printed
            // WOUNDED-side bar must fit inside this figure's printed damage
            // EV — the sim's ERLEDBAR law (VictoryHelpers.Killable, M9-F6),
            // not a blanket "someone somewhere could". Weak-damage figures
            // rank the wounded hero at plain printed threat, so the army
            // spreads over the healthy threats instead of converging on the
            // bait it cannot finish. No EV passed (round projection) or no
            // printed bar (off-roster sim rows) = the classic blanket x2.
            if (attackerDamageEv.HasValue && c.Card != null
                && c.Card.VerwundetesGesundheit > 0
                && attackerDamageEv.Value < new Rational(c.Card.VerwundetesGesundheit))
                return t;
            return t * WoundEconomy;
        }

        sealed class Row
        {
            public FocusCandidate C;
            public Rational? Threat;
            public Rational? Econ;
            // M15: threat operands (card rows only - PartsOf is card-based).
            public HeroThreatModelV2.ThreatParts? Parts;
            public List<object> Chip;
            public int? Druck;              // M11d strain surcharge (see Rank)
            public int? Unscharf;           // M11h fuzzy round-prior stage (see Rank)
            public int? Decke;              // M11h reachable ceiling U behind it
            public bool Reaktion;           // R-D reaktion key (see Rank)
            // S-D section-space reaction keys (concept positionierung v1
            // §10+§13.4, card t_c52dd34b): per-hero comparison signals on
            // the reactionHero pattern (FocusCandidate stays untouched).
            public bool Wachzone;
            public bool Vergeltung;
            // S-E supporter mask-trust key (Z6, card t_9dd56737): same
            // pattern - the hero in the supporter's trusted own section.
            public bool Maskenvertrauen;
        }

        /// Lexicographic compare of two rows, DESC (a before b => -1).
        /// woundAll = the counter-law flag (wounded-focus fix): healthy-first.
        static int Compare(Row a, Row b, bool woundAll)
        {
            // 1. aim mark (public designation): marked first, then the most
            //    recently marked (the CURRENT designation, not an old one).
            if (a.C.Marked != b.C.Marked) return a.C.Marked ? -1 : 1;
            if (a.C.Marked)
            {
                int mr = b.C.MarkedRound.CompareTo(a.C.MarkedRound);
                if (mr != 0) return mr;
            }
            // 1b. R-D REAKTION (concept v1.2 §2.3, Thomas-Punkt 3a): the hero
            //     who shot THIS activating figure inside an open window THIS
            //     round leads right behind the aim mark (the human's
            //     designation at the throne stays king) and BEFORE the
            //     counter law. No reactionHero passed -> both rows false,
            //     slot inert, pre-R-D canon byte-identical.
            if (a.Reaktion != b.Reaktion) return a.Reaktion ? -1 : 1;
            // 1c. S-D WACHZONE (concept positionierung v1 §10: "Wachzonen-
            //     Schluessel ... Ausnahme-Aktivierung laeuft auf der
            //     Reihenfolge-Ebene, das Ziel auf der Rank-Ebene"; §13.4
            //     slot law: Interrupt > Wachzone > Vergeltung): the hero
            //     factually working at the mission-critical object inside
            //     the activating guard's mask leads behind the fact-track
            //     reaktion, ahead of the counter law. Null key => inert.
            if (a.Wachzone != b.Wachzone) return a.Wachzone ? -1 : 1;
            // 1d. S-D VERGELTUNG, mask track (concept §10 "C(Schuetze)":
            //     candidate from argmax(mask ∩ victim ring); the FACT track
            //     (window attribution, slot 1b) already beat it by standing
            //     above - and the resolver only ever names ONE unambiguous
            //     candidate, else the key stays off. Suspicion ranks behind
            //     fact, never alone above it. Null key => inert.
            if (a.Vergeltung != b.Vergeltung) return a.Vergeltung ? -1 : 1;
            // 1e. S-E SUPPORTER-MASKEN-VERTRAUEN (Z6, card t_9dd56737): the
            //     supporter fires into his trust-gated own section - the hero
            //     factually anchored there leads the mask track. It rides
            //     behind the whole pinned trio (reaktion > wachzone >
            //     vergeltung stays verbatim, this key never enters it) and
            //     ahead of the Bedrohungs-Schaetzung (chip/economy), exactly
            //     the concept's "Feuer in die richtige Sektion statt
            //     Bedrohungs-Schaetzung". Null key => inert.
            if (a.Maskenvertrauen != b.Maskenvertrauen) return a.Maskenvertrauen ? -1 : 1;
            // 2. counter law (woundAll): sim ChipKey verbatim, higher first.
            //    M10b: an objective actor OUTRANKS the static role doctrine
            //    below - a hero who just did real mission damage is the live
            //    threat, ahead of any printed-role preference (only the
            //    human's aim mark, key 1, still overrides him).
            if (a.C.ObjectiveActor != b.C.ObjectiveActor)
                return a.C.ObjectiveActor ? -1 : 1;
            // Player-19 (Thomas 03.11): engaged-vs-engaged - the most recent
            // engagement leads (the door-crusher from two rounds ago yields
            // to the hero who just struck the mission figure). 0/0 rows (all
            // pre-Player-19 canon) keep the old fall-through order verbatim.
            if (a.C.ObjectiveActor && b.C.ObjectiveActor)
            {
                int er = b.C.EngagedRound.CompareTo(a.C.EngagedRound);
                if (er != 0) return er;
            }
            // 2a. WOUNDED-FOCUS FIX (Thomas 03.11): under a woundAll counter
            //     the sim's LEADING counter slot applies - "healthy BEATS
            //     wounded, every hit counts and counter progress is
            //     irreversible" (VictoryCounter.TargetValue woundAll law).
            //     Before the chip: the chip orders ROLES for FRESH damage and
            //     must never pull the army back onto a wounded hero. Without
            //     the woundAll counter this slot does not exist - the null-
            //     counter canon (finisher doctrine, key 4) stays bit-identical.
            if (woundAll && a.C.Wounded != b.C.Wounded)
                return a.C.Wounded ? 1 : -1;
            if (a.Chip != null || b.Chip != null)
            {
                int ck = CompareChip(a.Chip, b.Chip);
                if (ck != 0) return ck;
            }
            // 3. threat economy: posture-keyed printed value x wound economy,
            //    exact rationals. No-profile rows carry null -> they rank
            //    BELOW profiled rows on this key (unknown is not better).
            //    M10b OBJECTIVE ACTOR (pure curated rounds): the +1 above
            //    already moved him up this key; the tie-break fired at key 2.
            if (a.Econ != null || b.Econ != null)
            {
                int e = CompareEcon(a.Econ, b.Econ);
                if (e != 0) return e;
            }
            // 4. reference parity (no curated profile): wounded first, then
            //    M10a CAMPAIGN INVESTMENT (Thomas playtest 27.10): four
            //    expansion heroes share the neutral prior 10, so keys 1-3
            //    tie and the old chain fell straight to squad row - a
            //    predictable top-to-bottom kill queue. Items+skills from the
            //    campaign table are PUBLIC board data the Imperial player
            //    reads off the hero sheets: the better-equipped hero is
            //    objectively the bigger threat, so EquipmentIndex (weighted
            //    tier x cost) then AbilityIndex (xp sum) break the tie BEFORE
            //    any row order. Wounded keeps priority (finisher doctrine).
            if (a.C.Wounded != b.C.Wounded) return a.C.Wounded ? -1 : 1;
            var eqA = a.C.Profile != null ? a.C.Profile.EquipmentIndex : Rational.Zero;
            var eqB = b.C.Profile != null ? b.C.Profile.EquipmentIndex : Rational.Zero;
            int eq = eqB.CompareTo(eqA);                                   // desc
            if (eq != 0) return eq;
            int ab = (b.C.Profile != null ? b.C.Profile.AbilityIndex : 0)
                   .CompareTo(a.C.Profile != null ? a.C.Profile.AbilityIndex : 0);
            if (ab != 0) return ab;                                       // desc
            int ad = b.C.AttackDice.CompareTo(a.C.AttackDice);
            if (ad != 0) return ad;
            int hp = b.C.Health.CompareTo(a.C.Health);
            if (hp != 0) return hp;
            // 5. stable tie-chain: squad row left-to-right, then id.
            int row = a.C.SquadRow.CompareTo(b.C.SquadRow);
            if (row != 0) return row;
            return string.CompareOrdinal(a.C.HeroId ?? "", b.C.HeroId ?? "");
        }

        /// ChipKey = [int class, int runner, Rational ev] (HeroProfiles.cs
        /// port of the sim chip_key). Higher leads; missing chip loses.
        static int CompareChip(List<object> a, List<object> b)
        {
            if (a == null) return 1;
            if (b == null) return -1;
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                object x = a[i], y = b[i];
                int c;
                if (x is Rational && y is Rational)
                    c = ((Rational)y).CompareTo((Rational)x);          // desc
                else if (x is Rational)
                    c = -1;                                            // number leads
                else if (y is Rational)
                    c = 1;
                else
                    c = Convert.ToInt32(y, System.Globalization.CultureInfo.InvariantCulture)
                      .CompareTo(Convert.ToInt32(x, System.Globalization.CultureInfo.InvariantCulture));
                if (c != 0) return c;
            }
            return b.Count.CompareTo(a.Count);
        }

        static int CompareEcon(Rational? a, Rational? b)
        {
            if (!a.HasValue) return 1;
            if (!b.HasValue) return -1;
            return b.Value.CompareTo(a.Value);                         // desc
        }

        /// Stable insertion sort (candidate sets are hero-sized; avoids the
        /// List.Sort instability question entirely - fairness demands the
        /// tie-chain in Compare to decide, nothing else).
        static List<T> StableSort<T>(List<T> input, Func<T, T, int> cmp)
        {
            var outp = new List<T>();
            foreach (var item in input)
            {
                int lo = 0, hi = outp.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (cmp(outp[mid], item) <= 0) lo = mid + 1;
                    else hi = mid;
                }
                outp.Insert(lo, item);
            }
            return outp;
        }
    }
}
