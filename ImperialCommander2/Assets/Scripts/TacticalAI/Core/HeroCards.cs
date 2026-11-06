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
        // M21 (Thomas ruling 06.11., playtest-Nachlese): printed ability
        // EFFECTS curated per hero - language-independent SKILL-ID pins
        // (never card names). Curation is the single source (the app's own
        // skill runtime is not read, same law as the item dice proxies).
        // First carrier: Fenn Signis. His card back prints Havoc Shot
        // ("1C: Use while attacking with a O weapon. This attack gains
        // Blast 1H" - RR keyword Blast/Explosion: figures adjacent to the
        // target suffer the blast value, no defense dice), and his skill
        // card fenn01 ("Taktisches Vorrücken"/"Tactical Movement") raises
        // his speed by one (Thomas ruling: +1 healthy AND wounded).
        public int ExplosionsWert;                                    // printed blast H
        public List<HeroSkillPin> ExplosionsUpgrades = new List<HeroSkillPin>(); // skill -> new WERT (absorbs)
        public List<HeroSkillPin> TempoPins = new List<HeroSkillPin>();          // skill -> tempo DELTA
        // M22 (Thomas ruling 06.11.): the printed SUPPORTER role. Identity
        // pins: Davith Elso (the commander) and MHD-19 always; Diala Passil
        // only while she carries diala02 "Machtadept" (friendly reroll) or
        // diala03 "Kampfmeditation" (friendly Focused). Consumer is the
        // woundAll chip law ONLY (supporter chipped first) - it never moves
        // a threat number: eliminate stays pure dice (findings 06.11. §1).
        public bool SupporterImmer;
        public List<string> SupporterSkills = new List<string>();
        // M24 (Thomas ruling 06.11., Karten-Audit): the printed HEALER kit.
        // Card pins ONLY - no identity healer exists in the live roster, and
        // SELF-heal (gaarkhan01) deliberately does NOT pin (heals others =
        // healer; self-heal = the separate selfHealPossible data field).
        // MHD-19's kit: mhd01 (ally wound), mhd03 (attack-Z ally 2 wounds),
        // mhd05 (attack-Z 1 wound), mhd06 (defeat rescue 3 wounds),
        // mhd07 (Bacta Radiator round aura wound+strain). Consumer is the
        // woundAll chip law ONLY: a hero whose printed cards REPAIR wounds
        // undoes counter progress, so he is chipped BEFORE the supporter
        // class (class 3) - the printed counterpart of the heiler_hammer
        // doctrine line. It never moves a threat number.

        /// <summary>M24: is this hero a card-pinned HEALER with the chosen
        /// skill cards? Printed wound-repair skill ids only; unknown ids
        /// never match - the pin stays inert.</summary>
        public bool HeilerEffektiv(IList<string> skills)
        {
            if (skills == null || HeilerSkills.Count == 0) return false;
            foreach (var s in HeilerSkills)
                if (s != null && skills.Contains(s)) return true;
            return false;
        }

        public List<string> HeilerSkills = new List<string>();
        // M25 (Thomas ruling 06.11., Karten-Audit 2. Prüfung): printed
        // STATIC attack modifiers of the hero row and his cards.
        // waffenGenauigkeit   - the base weapon's printed '+N Accuracy'
        //                       line (CT-1701 +2, Loku +1, Saska +1) - the
        //                       weapon rides in BOTH states.
        // genauigkeitGesund   - the hero card's healthy-side printed
        //                       accuracy ability (Loku "Clear-Minded" +2;
        //                       the wounded back page prints NO ability).
        // verstaerkungGesund  - Loku's printed recon-token synergy: '+1 damage
        //                       whenever YOU OR A FRIEND attack a recon-
        //                       marked figure' - the source sits with him,
        //                       discounted HALF (ExplosionShare precedent,
        //                       Thomas: "je ½"), healthy side only (the
        //                       back page prints no abilities - Print!).
        // genauigkeit_pins    - chosen cards printing static '+N Accuracy'
        //                       (loku08, vinto04, drokkata03) while equipped.
        // angriffs_bonus      - chosen cards printing an EXTRA ATTACK:
        //                       Gideon's gideon07 'Hammer und Amboss' (he +
        //                       an ally attack the same target) and gideon08
        //                       'Meisterleistung' (a second Command free of
        //                       action AND strain). Each counts as HALF of
        //                       one NEUTRAL attack EV (prior 1) - the
        //                       exhaustion-cycle discount mirrors M21;
        //                       Hammer's printed defence-dice removal is
        //                       deliberately NOT counted on top (documented).
        // Consum: the existing keyword seam of Dice.BuildAttackStats
        // ('+N Accuracy' - the M12d pairing law pairs accuracy with damage)
        // and the flat M21 addend seam (real damage, never percentage-bent
        // twice). Attack-accuracy prints are print-honest facts, never a
        // threat-role judgment: NO chip-class, NO economy component.
        public int WaffenGenauigkeit;
        public int GenauigkeitGesund;
        public Rational VerstaerkungGesund = Rational.Zero;
        public List<HeroSkillPin> GenauigkeitPins = new List<HeroSkillPin>();
        public List<HeroSkillFractionPin> AngriffsBonusPins = new List<HeroSkillFractionPin>();

        /// <summary>M25: static accuracy the hero attacks with - printed
        /// weapon line (both states) + healthy-side hero-card ability +
        /// equipped accuracy cards. Unknown ids never match.</summary>
        public int GenauigkeitEffektiv(IList<string> skills, bool wounded)
        {
            int g = WaffenGenauigkeit;
            if (!wounded) g += GenauigkeitGesund;
            if (skills != null)
                foreach (var p in GenauigkeitPins)
                    if (p.Skill != null && skills.Contains(p.Skill)) g += p.Delta;
            return g;
        }

        /// <summary>M25: the printed extra-attack flat addend with the
        /// chosen cards (each card = its fraction of one neutral attack
        /// EV; exhaustion-cycle share, Thomas ruling "je ½").</summary>
        public Rational AngriffsBonusEffektiv(IList<string> skills)
        {
            var s = Rational.Zero;
            if (skills == null || AngriffsBonusPins.Count == 0) return s;
            foreach (var p in AngriffsBonusPins)
                if (p.Skill != null && skills.Contains(p.Skill)) s += p.Wert;
            return s;
        }

        /// <summary>M21: effective printed blast value with the hero's
        /// chosen skill cards (Rebel Elite fenn07 ABSORBS the base value,
        /// it does not add to it - the upgrade replaces Blast 1H with 2H).</summary>
        public int ExplosionsWertEffektiv(IList<string> skills)
        {
            int w = ExplosionsWert;
            if (skills != null)
                foreach (var u in ExplosionsUpgrades)
                    if (u.Skill != null && skills.Contains(u.Skill) && u.Wert > w) w = u.Wert;
            return w;
        }

        /// <summary>M21: sum of the tempo deltas the chosen skill cards
        /// print (applied to the healthy AND the wounded tempo - Thomas
        /// ruling 06.11.: "verwundet ebenfalls +1").</summary>
        public int TempoDelta(IList<string> skills)
        {
            if (skills == null || TempoPins.Count == 0) return 0;
            int d = 0;
            foreach (var p in TempoPins)
                if (p.Skill != null && skills.Contains(p.Skill)) d += p.Delta;
            return d;
        }

        /// <summary>M22: is this hero a SUPPORTER with the chosen skill
        /// cards? Identity pin (always) or one of the printed support skill
        /// ids. Unknown ids never match - the pin stays inert.</summary>
        public bool SupporterEffektiv(IList<string> skills)
        {
            if (SupporterImmer) return true;
            if (skills == null || SupporterSkills.Count == 0) return false;
            foreach (var s in SupporterSkills)
                if (s != null && skills.Contains(s)) return true;
            return false;
        }


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

    /// <summary>M21: one printed skill-card effect pin (asset "effekte").
    /// Skill is the app's language-stable skill id (skills.json); Wert is
    /// an absolute replacement value (blast upgrade), Delta an additive
    /// modifier (tempo). Unknown ids never match - the pin stays inert.</summary>
    public sealed class HeroSkillPin
    {
        public string Skill;
        public int Wert;
        public int Delta;
    }

    /// <summary>M25: one printed skill-card effect pin carrying an exact
    /// fraction (asset "angriffs_bonus" - extra-attack shares).</summary>
    public sealed class HeroSkillFractionPin
    {
        public string Skill;
        public Rational Wert;
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

    /// <summary>Loaded tactical_hero_cards/4 asset (M21: "effekte" skill
    /// pins on hero rows - absent means no effects, null law).</summary>

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
                // M21: the printed ability-effect pins ("effekte"). Absent
                // on every pre-M21 hero -> parsing stays bit-identical
                // (null law).
                var effekte = Json.Get(r, "effekte", null);
                if (effekte != null)
                {
                    var ef = Json.AsDict(effekte);
                    var expl = Json.Get(ef, "explosion", null);
                    if (expl != null)
                    {
                        var ex = Json.AsDict(expl);
                        c.ExplosionsWert = Json.AsInt(Json.Get(ex, "wert", 0));
                        foreach (var rawU in Json.AsList(Json.Get(ex, "upgrades", new List<object>())))
                        {
                            var ud = Json.AsDict(rawU);
                            c.ExplosionsUpgrades.Add(new HeroSkillPin
                            {
                                Skill = Json.AsStr(Json.Get(ud, "skill", null)),
                                Wert = Json.AsInt(Json.Get(ud, "wert", 0)),
                            });
                        }
                    }
                    foreach (var rawP in Json.AsList(Json.Get(ef, "tempo_pins", new List<object>())))
                    {
                        var pd = Json.AsDict(rawP);
                        c.TempoPins.Add(new HeroSkillPin
                        {
                            Skill = Json.AsStr(Json.Get(pd, "skill", null)),
                            Delta = Json.AsInt(Json.Get(pd, "delta", 0)),
                        });
                    }
                    // M22: the printed supporter role (identity or skill ids).
                    var sup = Json.Get(ef, "supporter", null);
                    if (sup != null)
                    {
                        var sd = Json.AsDict(sup);
                        c.SupporterImmer = (bool)Json.Get(sd, "always", false);
                        foreach (var rawS in Json.AsList(Json.Get(sd, "skills", new List<object>())))
                            c.SupporterSkills.Add(Json.AsStr(rawS));
                    }
                    // M24: the printed healer kit (skill ids only - a card
                    // that repairs wounds makes its bearer the hammer
                    // target of the woundAll chip law).
                    var hl = Json.Get(ef, "healer", null);
                    if (hl != null)
                        foreach (var rawH in Json.AsList(Json.Get(Json.AsDict(hl), "skills", new List<object>())))
                            c.HeilerSkills.Add(Json.AsStr(rawH));
                    // M25: the printed static accuracy of CHOSEN cards.
                    var ap = Json.Get(ef, "genauigkeit_pins", null);
                    if (ap != null)
                        foreach (var rawP in Json.AsList(ap))
                        {
                            var pd = Json.AsDict(rawP);
                            c.GenauigkeitPins.Add(new HeroSkillPin
                            {
                                Skill = Json.AsStr(Json.Get(pd, "skill", null)),
                                Delta = Json.AsInt(Json.Get(pd, "delta", 0)),
                            });
                        }
                    // M25: the printed EXTRA-ATTACK cards (exact fractions).
                    var ab = Json.Get(ef, "angriffs_bonus", null);
                    if (ab != null)
                        foreach (var rawB in Json.AsList(ab))
                        {
                            var bd = Json.AsDict(rawB);
                            var wb = Json.AsStr(Json.Get(bd, "wert", null));
                            c.AngriffsBonusPins.Add(new HeroSkillFractionPin
                            {
                                Skill = Json.AsStr(Json.Get(bd, "skill", null)),
                                Wert = string.IsNullOrEmpty(wb) ? Rational.Zero : Rational.Parse(wb),
                            });
                        }
                }
                // M25: the hero-row static prints (weapon accuracy line,
                // healthy-side ability accuracy, recon-token synergy flat).
                c.WaffenGenauigkeit = Json.AsInt(Json.Get(r, "waffenGenauigkeit", 0));
                c.GenauigkeitGesund = Json.AsInt(Json.Get(r, "genauigkeitGesund", 0));
                var vg = Json.AsStr(Json.Get(r, "verstaerkungGesund", null));
                if (!string.IsNullOrEmpty(vg)) c.VerstaerkungGesund = Rational.Parse(vg);
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
        // protect/stall. M20 (Thomas ruling 05.11, melee debate): the 10%
        // RANGE cell is STRICKEN - threat = 100 x (60% normalized weapon
        // DAMAGE + 40% normalized SPEED band). Range was double-counted:
        // the M19 band average already reads every weapon across its whole
        // reach (wide but weak bands dilute, melee pays nothing), so the
        // extra cell only taxed printed melee heroes for carrying no
        // distance they could ever shoot. DamageAnchor stays 8/3 (RR - the
        // strongest printed weapon dice in the item table, two red faces at
        // 4/3 each), BandAnchor stays 3 (fastest mobility band): the 60/40
        // split keeps the law's maximum at 100. Fixed anchors: threat values
        // never wander mid-mission (dynamic anchors would jump whenever the
        // strongest hero falls or a weapon is bought - Thomas: explanation must stay
        // re-computable from the log). The wound effect is the PRINTED
        // tempoVerwundet only (band drop) - NO extra percent malus (ruling).
        // eliminate/attrition stay PURE damage: in a kill mission speed does
        // not make a hero harder to remove (ruling: weights apply to the
        // holding postures protect/stall only). M15's absolute DamageWeight
        // law (24 x EV + band x range) and M16's 55/35/10 split are both
        // superseded - damage keeps the plurality (60 > 40) and a fast scout
        // may legitimately outrank a slow heavy shooter.
        public static readonly Rational DamageShare  = new Rational(3, 5);   // 60 %
        public static readonly Rational SpeedShare   = new Rational(2, 5);   // 40 %
        // M20: the 10% range cell is stricken (see law comment). RangeAnchor
        // and the range operand stay printed in the EXPLAIN row - the M19
        // band average still RUNS over 1..range, it just earns no own points.
        public static readonly Rational RangeShare   = Rational.Zero;         // 0 %
        // M21 (Thomas ruling 06.11.): the printed blast value rides the
        // damage operand at HALF its value (trigger + endurance discount,
        // see the EliminateEv law text). Exact rational, curation-pinned.
        public static readonly Rational ExplosionShare = new Rational(1, 2);

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
            public Rational Explosion;       // M21 flat blast increment
                                             //  inside DamageEv (printed
                                             //  blast x ExplosionShare)
            public Rational Bonus;           // M25 flat extra-attack and
                                             //  Verstaerkung increment
                                             //  inside DamageEv (printed
                                             //  cards x neutral attack EV)
            public int SpeedBand;            // mobility band 1..3 (printed wound tempo!)
            public int RangeCells;           // reach incl. printed ranged-item gates
            public List<string> BestPool;    // dice the DamageEv was computed from
                                             //  (M19: the pool that decided the
                                             //  most distances of the band)
            public List<string> BandBests;   // M19: per-distance winner pool,
                                             //  entry d-1, empty = nothing
                                             //  scores at that distance - the
                                             //  accounting prints the band so
                                             //  the average is hand-re-addable
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
                List<string> pool, band; Rational energyX, explX, bonusX;
                var dmg = EliminateEv(card, table, profile, wounded,
                                      defenderDefense, engage, out pool, out energyX, out band, out explX, out bonusX);
                return RunnerThreat(dmg, RunnerBand(card, wounded, profile),
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
            List<string> pool, band; Rational energy, explosion, flatBonus;
            var dmg = EliminateEv(card, table, profile, wounded,
                                  defenderDefense, engage, out pool, out energy, out band, out explosion, out flatBonus);
            var parts = new ThreatParts
            {
                DamageEv = dmg,
                Energy = energy,
                Explosion = explosion,
                Bonus = flatBonus,
                SpeedBand = runnerRace ? RunnerBand(card, wounded, profile) : 0,
                RangeCells = runnerRace ? RunnerRange(card, table, profile, wounded) : 0,
                BestPool = pool,
                BandBests = band,
            };
            return parts;
        }

        // ------------------------------------------------------- damage race
        static Rational EliminateEv(HeroCard card, HeroCardTable table,
                                    HeroThreatProfile profile, bool wounded,
                                    IList<string> defenderDefense,
                                    EngagementEffects engage = null)
        {
            List<string> unused, unusedB; Rational unusedE, unusedX, unusedF;
            return EliminateEv(card, table, profile, wounded, defenderDefense,
                               engage, out unused, out unusedE, out unusedB, out unusedX, out unusedF);
        }

        static Rational EliminateEv(HeroCard card, HeroCardTable table,
                                    HeroThreatProfile profile, bool wounded,
                                    IList<string> defenderDefense,
                                    EngagementEffects engage,
                                    out List<string> bestPool,
                                    out Rational energy,
                                    out List<string> bandBests,
                                    out Rational explosion,
                                    out Rational flatBonus)
        {
            explosion = ExplosionShare * card.ExplosionsWertEffektiv(
                profile != null ? profile.LoadoutSkills : null);
            // M25 (Thomas ruling 06.11.): the printed EXTRA-ATTACK cards and
            // Loku's printed recon-token synergy ('Set Your Sights' + recon
            // bonus) ride the SAME flat
            // addend seam as the blast - real damage, never percentage-bent
            // twice; each extra attack = its fraction of ONE neutral attack
            // EV (prior 1); the recon synergy is healthy-side print ONLY -
            // wiki_heros_raw models the wounded side for 16 heroes and Loku
            // has NO 'b ability' print, so the whole ability ends when
            // wounded (verified 06.11. against the reference).
            var skillsX = profile != null ? profile.LoadoutSkills : null;
            flatBonus = card.AngriffsBonusEffektiv(skillsX)
                        + (wounded ? Rational.Zero : card.VerstaerkungGesund);
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
            // M26 (Thomas ruling 06.11., Player-29 log "1:Green+Green"):
            // a hero's attack is the weapon SHE CARRIES (rule text: for a
            // hero the attack value "is found on his weapon Item card").
            // A bought ranged weapon OCCUPIES the hero's ranged slot and
            // REPLACES the printed base blaster outright - the standard
            // dice never compete while a {{O}} item is carried ("Jyn hat
            // den DL-44"; she is no melee fighter and does not switch to
            // the weaker printed pistol just because the pairing law
            // scores green dice higher at distance 1). The melee standard
            // of a Nahkaempfer is NOT replaced (fists are the body, not a
            // stored weapon); {{P}} items merely ADD a melee option at
            // their own reach. poolGate[i] = the distances each pool
            // actually covers: base = printed Reichweite, ranged item =
            // its own acc-gate, melee item = 1.
            int baseGate = card.Reichweite < 1 ? 1 : card.Reichweite;
            var poolGate = new List<int> { baseGate };
            var poolIsMeleeItem = new List<bool> { false };
            bool hasRangedItem = false;
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
                {
                    pools.Add(Tuple.Create(new List<string>(e.Wuerfel), ranged, b));
                    poolGate.Add(0);   // ranged: filled from the pool's own
                    poolIsMeleeItem.Add(!ranged);   // acc-gate below; melee = 1
                    if (ranged) hasRangedItem = true;
                }
                if (e.Attribut != null)
                {
                    var attr = card.AttributWuerfel(e.Attribut, wounded);
                    if (attr != null && attr.Count > 0)
                    {
                        pools.Add(Tuple.Create(attr, ranged, b));
                        poolGate.Add(0);
                        poolIsMeleeItem.Add(!ranged);
                        if (ranged) hasRangedItem = true;
                    }
                }
            }
            // ranged slot occupied -> printed base blaster stays in the bag
            bool baseReplaced = hasRangedItem && !card.Nahkaempfer;
            // M25: the hero's OWN printed static accuracy (weapon line +
            // healthy-side ability + accuracy cards) rides the existing
            // keyword seam - the M12d pairing law pairs the acc pips with
            // damage, exactly as for item accuracy lines.
            int heroAcc = card.GenauigkeitEffektiv(skillsX, wounded);
            if (heroAcc > 0) keywords.Add("+" + heroAcc + " Accuracy");
            var defender = Dice.BuildAttackStats(null, null, false, null,
                defenderDefense != null ? new List<string>(defenderDefense) : null);
            // the overlord assumes the hero fights with its best CARRIED
            // weapon SCORER (dice EV + M17 price-energy: one weapon per
            // attack, each pool only inside its own coverage - M26; and the
            // Empire may never under-estimate an equipped hero - same
            // direction as the M10a investment tie-break)
            int engagePierce = engage == null ? 0 : engage.Pierce;
            int engageAcc = engage == null ? 0 : engage.AccDelta;
            // M19 (Thomas ruling 05.11., Player-28 DL-44 fund): BAND-
            // AVERAGED EXCHANGE. The pre-M19 law scored EVERY weapon at
            // distance 1 - but the pairing law spends accuracy for
            // distance, so that reading systematically over-valued green
            // damage dice and hid the accuracy-rich blue dice of the
            // premium ranged weapons (a DL-44 out-scores Jyn's printed GG
            // from d2 on, yet could never win the d=1 score). The honest
            // table projection: the hero fights somewhere inside HIS
            // reach band (printed reach, extended only by verified ranged
            // items - the existing RunnerRange law), and the damage term
            // is     average over d = 1..reach of ( best pool score at d ).
            // Melee pools are distance-inert inside the pairing law by
            // design (no distance spend) - printed melee value simply
            // holds across their band. The per-distance winners ride out
            // in bandBests, so the average stays hand-re-addable (the
            // percent law demands visible arithmetic, not prose).
            //
            // M26 OVERRIDE (Thomas ruling 06.11., Player-29 log): the per-
            // distance switch was too generous. A hero's attack is the
            // weapon she CARRIES (rule: the attack is on the weapon item
            // card) - a bought {{O}} weapon OCCUPIES the ranged slot and
            // replaces the printed standard blaster at EVERY distance
            // ("Jyn hat den DL-44" -> Blue+Yellow auch auf 1, die
            // Standardwaffe rechnet nie mit), and a pool only ever scores
            // inside its OWN coverage poolGate (a Gauntlet cannot "shoot"
            // across the band). Choice stays free between CARRIED weapons
            // and for a Nahkaempfer's innate melee value - her fists are
            // the body, not a stored weapon, so no {{O}} item replaces a
            // printed {{P}} standard.
            int reach = RunnerRange(card, table, profile, wounded);
            if (reach < 1) reach = 1;
            var statsAt = new AttackStats[pools.Count];
            for (int i = 0; i < pools.Count; i++)
            {
                var p = pools[i];
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
                statsAt[i] = stats;
                // M26: the weapon's own coverage. A carried ranged weapon
                // reaches exactly its printed acc-gate (same law that
                // extends RunnerRange); a melee item reaches 1 cell.
                if (i > 0)
                    poolGate[i] = poolIsMeleeItem[i] ? 1
                        : (stats.AccGate < 1 ? 1 : stats.AccGate);
            }
            // the printed winner slot (M15 accounting print): the pool that
            // decided the MOST distances of the band (M26: its display
            // surcharge is the band's actual energy share, see below).
            // A band where nothing scores leaves the base pool + zero
            // energy (M15 print semantics kept).
            bestPool = card.BasisWuerfel;
            energy = Rational.Zero;
            bandBests = new List<string>();
            var sum = Rational.Zero;
            var energySum = Rational.Zero;   // M26: the winner's surcharge PER BAND
            var wins = new int[pools.Count];
            for (int d = 1; d <= reach; d++)
            {
                var best = Rational.Zero;
                int bestIdx = -1;
                for (int i = 0; i < pools.Count; i++)
                {
                    // M26: a replaced standard weapon never competes, and a
                    // weapon only ever scores at a distance IT CAN REACH -
                    // the free per-distance switch across the whole band is
                    // dead (the old law let Verena's gauntlets "shoot" at 4).
                    if (i == 0 && baseReplaced) continue;
                    if (d > poolGate[i]) continue;
                    // M17: the WEAPON SCORE is dice EV + the price-energy
                    // surcharge of THIS pool's weapon - so a bought weapon
                    // may legitimately outrank printed-equal or slightly
                    // stronger dice (E-11 400cr: 13/6 + 2/5 out-scores
                    // printed GG wherever the band reaches it).
                    var ev = Dice.ExpectedDamage(statsAt[i], defender, d,
                                                 pierce + engagePierce)
                             + pools[i].Item3;
                    if (ev > best) { best = ev; bestIdx = i; }  // strict >: ties keep the earlier pool (base weapon first)
                }
                sum += best;
                if (bestIdx >= 0)
                {
                    energySum += pools[bestIdx].Item3;
                    wins[bestIdx]++;
                    bandBests.Add(string.Join("+", pools[bestIdx].Item1));
                }
                else bandBests.Add("");    // this distance trades at zero
            }
            int champ = -1;
            for (int i = 0; i < pools.Count; i++)
                if (champ < 0 || wins[i] > wins[champ]) champ = i;
            if (champ >= 0 && wins[champ] > 0)
            {
                bestPool = pools[champ].Item1;
                // M26: the display energy is the ACTUAL surcharge share of
                // the average (winners' surcharges summed over the band /
                // reach) - a mixed band (Verena: gauntlets at 1, blaster at
                // 2-4) stays hand-re-addable, the operand never lies.
                energy = energySum / reach;
            }
            // M21 (Thomas ruling 06.11., Fenn playtest): the printed blast
            // ability (Fenn's Havoc Shot: "1C: ... this attack gains Blast
            // 1H"; RR keyword Blast / DE rules "Explosion": everything
            // ADJACENT to the hero's target suffers the blast value, no
            // defense dice) rides as a FLAT increment on the damage operand.
            // Ruling: "pauschal" - unconditional, NOT gated on where the
            // imperial figures stand (Thomas chose the simple reading over
            // the adjacency-gated one). The printed value is discounted to
            // HALF (ExplosionShare): Havoc Shot costs 1 endurance per
            // attack (Fenn has 4), triggers only when the primary attack
            // lands damage, and the app campaign drops the friendly fire -
            // the increment models Thomas' "erhöht immer ein wenig seinen
            // Schaden", never the full dice value. fenn07 (Rebel Elite)
            // ABSORBS the value to 2H through the curation pin (community-
            // sourced print upgrade, Thomas ruling 06.11. to encode it).
            // Being wounded DOUBLES the whole operand downstream through
            // the wound economy (OverlordFocus.ThreatEconomic) - the blast
            // rides along consistently. Heroes WITHOUT the printed blast
            // (all others) carry increment 0 - every pre-M21 vector stays
            // bit-identical.
            return sum / reach + explosion + flatBonus;
        }

        // ------------------------------------------------- mobility factors
        // (M16: the mobility FACTORS - band and reach are separate percent
        // terms of the weighted law now; the reach still extends with
        // printed ranged items exactly as before, wound band from the
        // PRINTED wound tempo - Thomas ruling: no hidden extra malus)
        static int RunnerBand(HeroCard card, bool wounded,
                              HeroThreatProfile profile = null)
        {
            // M21 (Thomas ruling 06.11.): printed skill pins raise the band
            // tempo in the HEALTHY AND the wounded state (fenn01 "Taktisches
            // Vorrücken": speed +1 also at 3 -> band 2 while wounded).
            int delta = card.TempoDelta(profile != null ? profile.LoadoutSkills : null);
            return MobilityTier((wounded ? card.TempoVerwundet : card.Tempo) + delta);
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
