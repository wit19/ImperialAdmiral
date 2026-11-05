// M11b hero threat v2 (concept wiki_data/CONCEPT_tactical_ai.md section 2,
// board task M11b). The curated asset tactical_hero_cards.json carries the
// PRINTED hero-card values of all 21 campaign heroes - health/endurance/
// speed/defense/attributes healthy AND wounded (the real card back), the
// printed base weapon die pool, the acc-gate range convention and the
// ability/xp/strain numbers (numbers + keyword slugs only: the asset is
// text-free and language-independent; curation tool + cleanroom policy:
// wiki_data/tools/curate_hero_cards.py, wiki_data/LICENSE_DATA.md).
//
// Threat v2 replaces the M2a.4 curation basis in the ORD focus ranking:
//   * eliminate/attrition: expected damage of the hero's REAL dice pool
//     (base weapon, or an owned weapon/attribute-pool item's pool) plus the
//     items' static printed bonuses, against the DEFENSE POOL OF THE
//     IMPERIAL FIGURE BEING TARGETED, through the frozen pairing/hit
//     formula (Dice.ExpectedDamage - the same hit formula the imperial
//     attack model runs; engagement distance = 1, the worst case for the
//     Empire, identical for every candidate - no positions are read).
//   * protect/stall: runner value = mobility tier x range from the REAL
//     printed tempo/range (wounded heroes run on the real wounded speed).
//   * the wounded penalty is therefore REAL card-back data (tempo -1,
//     shrinking attribute dice for attribute-pool weapons), not a prior.
// Chip class and role vocabulary stay exactly as before (M2a.4 table);
// the ability numbers in the asset are consumed in M11d (strain budget).
using System;
using System.Collections.Generic;
using System.Linq;

namespace TacticalAI.Core
{
    /// <summary>One printed hero card (all numbers are printed card values;
    /// xp costs come from the app's own CampaignData/skills.json).</summary>
    public sealed class HeroCard
    {
        public string Name;
        public string AppHeroId;               // H1..H21 (CardData/heroes.json)
        public int GesUnderhalt;               // printed health (healthy side)
        public int GesundeAusdauer;
        public int VerwundetesGesundheit;
        public int VerwundeteAusdauer;
        public int Tempo;                      // printed speed
        public int TempoVerwundet;
        public string Verteidigung;            // "K" black / "W" white
        public string VerteidigungVerwundet;
        public string Staerke;                 // attribute dice letters (B/R/G/Y)
        public string Einsicht;
        public string Tech;
        public string StaerkeVerwundet;        // card back: dice shrink (M11b
        public string EinsichtVerwundet;       // real wound penalty for
        public string TechVerwundet;           // attribute-pool weapons)
        public string AngriffsTyp;             // "P" melee | "O" ranged
        public string BasisAngriffswuerfel;    // e.g. "BR", "GY+Reach"
        public int Reichweite;                 // acc-gate convention (asset doc)
        public List<HeroAbility> Faehigkeiten = new List<HeroAbility>();

        public bool Nahkaempfer { get { return AngriffsTyp == "P"; } }
        public bool HatReach { get { return BasisAngriffswuerfel != null &&
                BasisAngriffswuerfel.EndsWith("+Reach", StringComparison.Ordinal); } }

        /// <summary>The printed base-weapon die letters (Reach suffix stripped).</summary>
        public List<string> BasisWuerfel
        {
            get { return Letters(BasisAngriffswuerfel); }
        }

        /// <summary>Attribute dice for one attribute slug in the CURRENT
        /// (healthy/wounded) state - null-safe.</summary>
        public List<string> AttributWuerfel(string attribut, bool wounded)
        {
            string v;
            switch (attribut)
            {
                case "staerke": v = wounded ? StaerkeVerwundet : Staerke; break;
                case "einsicht": v = wounded ? EinsichtVerwundet : Einsicht; break;
                case "tech": v = wounded ? TechVerwundet : Tech; break;
                default: return null;
            }
            return Letters(v);
        }

        static readonly Dictionary<string, string> LetterToColor =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "B", "Blue" }, { "R", "Red" }, { "G", "Green" }, { "Y", "Yellow" },
            };

        /// <summary>Asset die letters (B/R/G/Y) -> Core die colour names
        /// (Dice.AttackDieFaces keys). Unknown letters are dropped.</summary>
        internal static List<string> Letters(string s)
        {
            var outp = new List<string>();
            if (string.IsNullOrEmpty(s)) return outp;
            var core = s;
            var plus = core.IndexOf('+');
            if (plus >= 0) core = core.Substring(0, plus);
            foreach (var c in core)
                if (LetterToColor.TryGetValue(c.ToString(), out var color)) outp.Add(color);
            return outp;
        }
    }

    public sealed class HeroAbility
    {
        public string Bezeichnung;             // pure keyword slug (no prose)
        public int? XpKosten;                  // app skills.json cost
        public int? StrainKosten;              // printed strain on the card
    }

    /// <summary>One campaign item's printed ATTACK-relevant effect
    /// (weapon dice / attribute-pool / static accuracy, damage, pierce).
    /// Surge and activated lines stay out (conservative, cf. Dice.cs).</summary>
    public sealed class ItemEffect
    {
        public string Id;                      // app item id (item101..)
        public string Typ;                     // "O" ranged | "P" melee
        public List<string> Wuerfel = new List<string>();
        public string Attribut;                // "staerke"|"einsicht"|"tech"|null
        public int Genauigkeit;
        public int Schaden;
        public int Durchschlag;

        public bool HatAngriffswirkung
        {
            get { return Wuerfel.Count > 0 || Attribut != null ||
                        Genauigkeit != 0 || Schaden != 0 || Durchschlag != 0; }
        }
    }

    /// <summary>Loaded tactical_hero_cards/1 asset.</summary>
    public sealed class HeroCardTable
    {
        readonly Dictionary<string, HeroCard> _byName =
            new Dictionary<string, HeroCard>(StringComparer.Ordinal);
        readonly Dictionary<string, ItemEffect> _items =
            new Dictionary<string, ItemEffect>(StringComparer.Ordinal);

        public int Count { get { return _byName.Count; } }
        public HeroCard For(string name)
        {
            HeroCard c;
            return name != null && _byName.TryGetValue(name, out c) ? c : null;
        }
        public ItemEffect Item(string id)
        {
            ItemEffect e;
            return id != null && _items.TryGetValue(id, out e) ? e : null;
        }

        public static HeroCardTable FromDoc(object root)
        {
            var t = new HeroCardTable();
            var doc = root as Dictionary<string, object>;
            if (doc == null) return t;
            foreach (var raw in Json.AsList(Json.Get(doc, "heroes", new List<object>())))
            {
                var r = Json.AsDict(raw);
                var c = new HeroCard
                {
                    Name = Json.AsStr(r["name"]),
                    AppHeroId = Json.AsStr(Json.Get(r, "appHeroId", null)),
                    GesUnderhalt = Json.AsInt(Json.Get(r, "gesUnderhalt", 0)),
                    GesundeAusdauer = Json.AsInt(Json.Get(r, "gesundeAusdauer", 0)),
                    VerwundetesGesundheit = Json.AsInt(Json.Get(r, "verwundetesGesundheit", 0)),
                    VerwundeteAusdauer = Json.AsInt(Json.Get(r, "verwundeteAusdauer", 0)),
                    Tempo = Json.AsInt(Json.Get(r, "tempo", 0)),
                    TempoVerwundet = Json.AsInt(Json.Get(r, "tempoVerwundet", 0)),
                    Verteidigung = Json.AsStr(Json.Get(r, "verteidigung", null)),
                    VerteidigungVerwundet = Json.AsStr(Json.Get(r, "verteidigungVerwundet", null)),
                    AngriffsTyp = Json.AsStr(Json.Get(r, "angriffsTyp", "O")),
                    BasisAngriffswuerfel = Json.AsStr(Json.Get(r, "basisAngriffswuerfel", "")),
                    Reichweite = Json.AsInt(Json.Get(r, "reichweite", 0)),
                };
                var att = Json.Get(r, "attribute", null);
                if (att != null)
                {
                    var a = Json.AsDict(att);
                    c.Staerke = Json.AsStr(Json.Get(a, "staerke", ""));
                    c.Einsicht = Json.AsStr(Json.Get(a, "einsicht", ""));
                    c.Tech = Json.AsStr(Json.Get(a, "tech", ""));
                }
                var attv = Json.Get(r, "attributeVerwundet", null);
                if (attv != null)
                {
                    var a = Json.AsDict(attv);
                    c.StaerkeVerwundet = Json.AsStr(Json.Get(a, "staerke", ""));
                    c.EinsichtVerwundet = Json.AsStr(Json.Get(a, "einsicht", ""));
                    c.TechVerwundet = Json.AsStr(Json.Get(a, "tech", ""));
                }
                foreach (var rawA in Json.AsList(Json.Get(r, "faehigkeiten", new List<object>())))
                {
                    var ad = Json.AsDict(rawA);
                    c.Faehigkeiten.Add(new HeroAbility
                    {
                        Bezeichnung = Json.AsStr(Json.Get(ad, "bezeichnung", "")),
                        XpKosten = ad.ContainsKey("xp_kosten") && ad["xp_kosten"] != null
                            ? (int?)Json.AsInt(ad["xp_kosten"]) : null,
                        StrainKosten = ad.ContainsKey("strain_kosten") && ad["strain_kosten"] != null
                            ? (int?)Json.AsInt(ad["strain_kosten"]) : null,
                    });
                }
                if (c.Faehigkeiten.Count > 0)
                    c.Faehigkeiten = c.Faehigkeiten
                        .OrderBy(f => f.Bezeichnung, StringComparer.Ordinal).ToList();
                if (!string.IsNullOrEmpty(c.Name)) t._byName[c.Name] = c;
            }
            foreach (var raw in Json.AsList(Json.Get(doc, "items", new List<object>())))
            {
                var r = Json.AsDict(raw);
                var e = new ItemEffect
                {
                    Id = Json.AsStr(Json.Get(r, "id", null)),
                    Typ = Json.AsStr(Json.Get(r, "typ", "O")),
                    Attribut = Json.AsStr(Json.Get(r, "attribut", null)),
                    Genauigkeit = Json.AsInt(Json.Get(r, "genauigkeit", 0)),
                    Schaden = Json.AsInt(Json.Get(r, "schaden", 0)),
                    Durchschlag = Json.AsInt(Json.Get(r, "durchschlag", 0)),
                };
                var w = Json.AsStr(Json.Get(r, "wuerfel", ""));
                e.Wuerfel = HeroCard.Letters(w);
                if (!string.IsNullOrEmpty(e.Id) && e.HatAngriffswirkung) t._items[e.Id] = e;
            }
            return t;
        }
    }

    /// <summary>Doc cache with the same seam as HeroProfiles: the harness and
    /// the live gate PRIME from file; the app loader (TacticalAIAssets)
    /// primes from Resources - whoever runs first wins deterministically.</summary>
    public static class HeroCards
    {
        static Dictionary<string, object> _docCache;
        static HeroCardTable _table;

        public static void PrimeDoc(Dictionary<string, object> doc)
        {
            _docCache = doc;
            _table = null;
        }

        public static Dictionary<string, object> DocOrNull() => _docCache;

        public static HeroCardTable Load()
        {
            if (_table == null) _table = HeroCardTable.FromDoc(_docCache);
            return _table;
        }

        public static HeroCard For(string name) => Load().For(name);
    }

    /// <summary>Hero threat v2 (see file header). Pure function - no state,
    /// no positions, exact rationals.</summary>
    public static class HeroThreatModelV2
    {
        static readonly List<string> NoItems = new List<string>();
        static readonly System.Collections.Generic.Dictionary<string, Rational>
            EmptyBonus = new System.Collections.Generic.Dictionary<string, Rational>();


        // M16 (Thomas design ruling 05.11): PERCENT-WEIGHTED THREAT LAW for
        // protect/stall. threat = 100 x (55% normalized weapon DAMAGE
        //        + 35% normalized SPEED band + 10% normalized weapon RANGE).
        // Percent weights only mean something over NORMALIZED factors, so
        // every factor rides the largest value the GAME itself can print:
        // DamageAnchor = 8/3 (RR - the strongest printed weapon dice in the
        // item table, two red faces at 4/3 each), BandAnchor = 3 (fastest
        // mobility band), RangeAnchor = 7 (printed max range extended by the
        // reach-giving items). Fixed anchors: threat values never wander
        // mid-mission (dynamic anchors would jump whenever the strongest
        // hero falls or a weapon is bought - Thomas: explanation must stay
        // re-computable from the log). The wound effect is the PRINTED
        // tempoVerwundet only (band drop) - NO extra percent malus (ruling).
        // eliminate/attrition stay PURE damage: in a kill mission speed does
        // not make a hero harder to remove (ruling: weights apply to the
        // holding postures protect/stall only). M15's absolute DamageWeight
        // law (24 x EV + band x range) is superseded by this softer law -
        // damage keeps the plurality (55 > 35+10) but a fast scout may now
        // legitimately outrank a slow heavy shooter.
        public static readonly Rational DamageShare  = new Rational(11, 20); // 55 %
        public static readonly Rational SpeedShare   = new Rational(7, 20);  // 35 %
        public static readonly Rational RangeShare   = new Rational(1, 10);  // 10 %
        public static readonly Rational DamageAnchor = new Rational(8, 3);
        public const int BandAnchor  = 3;
        public const int RangeAnchor = 7;

        /// M16: round half up to the whole threat value (exact rationals all
        /// the way, quantized once at the end). The printed and ranked number
        /// is the rounded one; the EXPLAIN cell shows the exact fractions so
        /// the sum can be re-added by hand.
        public static Rational Quantize(Rational v)
        {
            return new Rational((v.Num * 2 + v.Den) / (2 * v.Den));   // floor(x + 1/2)
        }

        /// M16 law, uniform for card rows AND curation rows (Thomas ruling:
        /// the percent law is a protect/stall law - never applied to the
        /// damage races here, callers route eliminate/attrition to the EV).
        public static Rational RunnerThreat(Rational damageEv, int band, int range)
        {
            return Quantize(new Rational(100) * (
                DamageShare * damageEv / DamageAnchor
                + SpeedShare * band / BandAnchor
                + RangeShare * range / RangeAnchor));
        }

        /// <summary>M16: the threat broken into its printable operands.
        /// The EXPLAIN text and the threat-accounting log must show the REAL
        /// arithmetic with these values (Thomas: "die Rechnung mit Kommentar,
        /// welcher Wert wofuer steht" - never prose about how it works).</summary>
        public struct ThreatParts
        {
            public Rational DamageEv;        // exchange EV per round (weapon
                                             //  score: printed dice + M17
                                             //  price-energy of the winner)
            public Rational Energy;          // M17 surcharge inside DamageEv
            public int SpeedBand;            // mobility band 1..3 (printed wound tempo!)
            public int RangeCells;           // reach incl. printed ranged-item gates
            public List<string> BestPool;    // dice the DamageEv was computed from
        }

        /// <summary>Printed-threat value of one hero in one race, or null
        /// when the posture is not a threat race. card null -> caller falls
        /// back to the M2a.4 curation table (Han/Wookiee sim rows).
        /// M15: every threat posture now carries the damage exchange;
        /// eliminate/attrition stay PURE damage, protect/stall =
        /// DamageWeight x damage + mobility factor (see DamageWeight law).
        /// M12d: engage = the IMPERIAL SIDE's card effects on the hero
        /// exchange (owned engage-hook cards, ImperialCardEffects). null or
        /// neutral -> bit-identical pre-M12d threat (canon law): the acc pip
        /// floor moves the hero's accuracy EV (his attack exchange against
        /// the owning side: negative delta = printed defensive prints make
        /// his attack worse), the printed Pierce of the owning side rides
        /// the EXISTING pierce seam (M11b item pierce).</summary>
        public static Rational? Threat(HeroCard card, HeroCardTable table,
                                       HeroThreatProfile profile, bool wounded,
                                       string posture, IList<string> defenderDefense,
                                       EngagementEffects engage = null)
        {
            if (card == null) return null;
            if (posture == "eliminate" || posture == "attrition")
                return EliminateEv(card, table, profile, wounded, defenderDefense, engage);
            if (posture == "protect" || posture == "stall")
            {
                // M16 percent law (see DamageShare law): 55% damage, 35%
                // speed, 10% range over the game's own printed anchors. The
                // engage hook rides the exchange exactly as in the damage
                // races (M12d seam).
                List<string> pool; Rational energyX;
                var dmg = EliminateEv(card, table, profile, wounded,
                                      defenderDefense, engage, out pool, out energyX);
                return RunnerThreat(dmg, RunnerBand(card, wounded),
                                    RunnerRange(card, table, profile, wounded));
            }
            return null;
        }

        /// <summary>M16: the SAME threat as Threat(), plus its operands -
        /// damage EV, mobility band, reach and the exact dice pool the
        /// damage was computed from (LogThreatAccounting + the EXPLAIN cell
        /// print the arithmetic with these, never prose).</summary>
        public static ThreatParts? PartsOf(HeroCard card, HeroCardTable table,
                                           HeroThreatProfile profile, bool wounded,
                                           string posture, IList<string> defenderDefense,
                                           EngagementEffects engage = null)
        {
            if (card == null) return null;
            bool damageRace = posture == "eliminate" || posture == "attrition";
            bool runnerRace = posture == "protect" || posture == "stall";
            if (!damageRace && !runnerRace) return null;
            List<string> pool; Rational energy;
            var dmg = EliminateEv(card, table, profile, wounded,
                                  defenderDefense, engage, out pool, out energy);
            var parts = new ThreatParts
            {
                DamageEv = dmg,
                Energy = energy,
                SpeedBand = runnerRace ? RunnerBand(card, wounded) : 0,
                RangeCells = runnerRace ? RunnerRange(card, table, profile, wounded) : 0,
                BestPool = pool,
            };
            return parts;
        }

        // ------------------------------------------------------- damage race
        static Rational EliminateEv(HeroCard card, HeroCardTable table,
                                    HeroThreatProfile profile, bool wounded,
                                    IList<string> defenderDefense,
                                    EngagementEffects engage = null)
        {
            List<string> unused; Rational unusedE;
            return EliminateEv(card, table, profile, wounded, defenderDefense,
                               engage, out unused, out unusedE);
        }

        static Rational EliminateEv(HeroCard card, HeroCardTable table,
                                    HeroThreatProfile profile, bool wounded,
                                    IList<string> defenderDefense,
                                    EngagementEffects engage,
                                    out List<string> bestPool,
                                    out Rational energy)
        {
            var owned = profile != null && profile.LoadoutItems != null
                ? profile.LoadoutItems : NoItems;
            int pierce = 0;
            var keywords = new List<string>();
            // M17 (Thomas 05.11): each candidate pool carries its weapon's
            // PRICE-ENERGY surcharge (curated ev_model: weapon_ev_per_1000_cost
            // x tier x cost, fed per item through HeroThreatProfile.
            // WeaponEvBonus). The printed dice understate bought weapons -
            // energy abilities and keywords are priced into the cost but not
            // printed in the dice; price is 100% public (shop + purchases).
            // The base weapon costs nothing -> surcharge 0.
            var bonus = profile != null && profile.WeaponEvBonus != null
                ? profile.WeaponEvBonus : EmptyBonus;
            var pools = new List<Tuple<List<string>, bool, Rational>>
            {
                Tuple.Create(card.BasisWuerfel, !card.Nahkaempfer, Rational.Zero),
            };
            foreach (var id in owned)
            {
                var e = table != null ? table.Item(id) : null;
                if (e == null) continue;
                pierce += e.Durchschlag;
                if (e.Genauigkeit > 0) keywords.Add("+" + e.Genauigkeit + " Accuracy");
                if (e.Schaden > 0) keywords.Add("+" + e.Schaden + " {H}");
                bool ranged = e.Typ == "O";
                Rational b; bonus.TryGetValue(id, out b);   // default 0
                if (e.Wuerfel.Count > 0)
                    pools.Add(Tuple.Create(new List<string>(e.Wuerfel), ranged, b));
                if (e.Attribut != null)
                {
                    var attr = card.AttributWuerfel(e.Attribut, wounded);
                    if (attr != null && attr.Count > 0)
                        pools.Add(Tuple.Create(attr, ranged, b));
                }
            }
            var defender = Dice.BuildAttackStats(null, null, false, null,
                defenderDefense != null ? new List<string>(defenderDefense) : null);
            // the overlord assumes the hero fights with its best known
            // weapon SCORER (dice EV + M17 price-energy: one weapon per
            // attack, and the Empire may never under-estimate an equipped
            // hero - same direction as the M10a investment tie-break)
            int engagePierce = engage == null ? 0 : engage.Pierce;
            int engageAcc = engage == null ? 0 : engage.AccDelta;
            var best = Rational.Zero;
            // the pool whose EV produced `best` (M15 print: the accounting
            // shows WHICH printed pool the damage was read from; stays the
            // base pool when every pool trades at zero)
            bestPool = card.BasisWuerfel;
            energy = Rational.Zero;
            Rational bestEnergy = Rational.Zero;
            foreach (var p in pools)
            {
                var stats = Dice.BuildAttackStats(p.Item1, keywords, p.Item2, null,
                    defenderDefense != null ? new List<string>(defenderDefense) : null);
                // M12d engagement hook: the acc pips ride the AccEv/GATE of
                // this exact pool (printed-accuracy law: pips pair damage,
                // the gate follows); pierce uses the existing pierce seam.
                if (engageAcc != 0)
                {
                    stats.AccEv = Rational.Max(Rational.Zero, stats.AccEv + engageAcc);
                    var gate = stats.AccEv + Rational.One / 2;
                    stats.AccGate = (int)(gate.Num / gate.Den);
                }
                // M17: the WEAPON SCORE is dice EV + the price-energy
                // surcharge of THIS pool's weapon - so a bought weapon may
                // legitimately outrank printed-equal or slightly stronger
                // dice (E-11 400cr: 13/6 + 2/5 > Jyn's printed GG 7/3).
                var ev = Dice.ExpectedDamage(stats, defender, 1, pierce + engagePierce)
                         + p.Item3;
                if (ev > best) { best = ev; bestPool = p.Item1; bestEnergy = p.Item3; }
            }
            energy = bestEnergy;
            return best;
        }

        // ------------------------------------------------- mobility factors
        // (M16: the mobility FACTORS - band and reach are separate percent
        // terms of the weighted law now; the reach still extends with
        // printed ranged items exactly as before, wound band from the
        // PRINTED wound tempo - Thomas ruling: no hidden extra malus)
        static int RunnerBand(HeroCard card, bool wounded)
        {
            return MobilityTier(wounded ? card.TempoVerwundet : card.Tempo);
        }

        static int RunnerRange(HeroCard card, HeroCardTable table,
                               HeroThreatProfile profile, bool wounded)
        {
            int range = card.Reichweite;
            var owned = profile != null && profile.LoadoutItems != null
                ? profile.LoadoutItems : NoItems;
            foreach (var id in owned)
            {
                var e = table != null ? table.Item(id) : null;
                if (e == null) continue;
                var chars = e.Wuerfel.Count > 0 ? e.Wuerfel
                          : e.Attribut != null ? card.AttributWuerfel(e.Attribut, wounded)
                          : null;
                if (chars == null || chars.Count == 0) continue;
                if (e.Typ != "O") continue;   // melee items never extend the
                var stats = Dice.BuildAttackStats(chars,   // runner's reach
                    e.Genauigkeit > 0 ? new List<string> { "+" + e.Genauigkeit + " Accuracy" } : null,
                    true, null, null);
                if (stats.AccGate > range) range = stats.AccGate;
            }
            return range;
        }

        /// <summary>Mobility bands exactly as tactical_hero_profiles/1
        /// band_semantics: 1 = speed <= 3, 2 = speed 4, 3 = speed >= 5.</summary>
        public static int MobilityTier(int tempo)
        {
            return tempo <= 3 ? 1 : tempo == 4 ? 2 : 3;
        }
    }
}
