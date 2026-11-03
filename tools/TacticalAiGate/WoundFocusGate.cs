// WOUNDED-FOCUS FIX live gate (Thomas 03.11, Lockvogel-Analyse): the three
// laws that stop the army from magnetising onto one wounded decoy hero:
//   A) DeriveCounter - the mission's own text decides the end-condition law.
//   B) woundAll counter -> healthy-first slot + NO wound economy x2.
//   C) KILLABLE GATE - the per-figure x2 is earned only by figures whose
//      printed damage EV reaches the hero's printed wounded-side bar.
// The null-counter, no-EV canon (blanket x2, finisher key 4) stays pinned
// bit-identical - the golden vectors' law must not move.
using System;
using System.Collections.Generic;
using System.Linq;
using TacticalAI.Core;

public static class WoundFocusGate
{
    static int fails = 0;
    static void Eq(object expected, object actual, string what)
    {
        bool ok = Equals(expected, actual);
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what +
                          " | expected=" + expected + " actual=" + actual);
        if (!ok) { fails++; Environment.ExitCode = 1; }
    }

    // printed EVs this gate reasons with (accuracy-gated EliminateEv, probed
    // against the real model): Green 2/3, Blue 5/6 (a lone unaccurised Red
    // lands at EV 0 - hit-gating law, not a bug).
    static HeroCard Card(string attackLetters, int woundedBar)
    {
        return new HeroCard
        {
            Name = "card", BasisAngriffswuerfel = attackLetters,
            VerwundetesGesundheit = woundedBar, Reichweite = 1,
        };
    }

    static List<FocusPick> Rank(HeroCard baitCard, bool baitWounded,
                                string counter, Rational? attackerEv)
    {
        var cands = new List<FocusCandidate>
        {
            new FocusCandidate { HeroId = "Bait",   SquadRow = 0, Profile = null,
                                 Card = baitCard, Wounded = baitWounded },
            new FocusCandidate { HeroId = "Healthy", SquadRow = 1, Profile = null,
                                 Card = Card("B", 3), Wounded = false },
        };
        // bait = GREEN (threat 2/3), healthy = BLUE (threat 5/6): ungated the
        // bait is the SMALLER threat (2/3 < 5/6); only the x2 can put him
        // first (4/3 > 5/6), so every order flip here is exactly the law
        // under test.
        return OverlordFocus.Rank(cands, "eliminate", counter, null, 0, null, attackerEv);
    }

    public static void Run()
    {
        Console.WriteLine("-- WoundFocusGate --");

        // ------------------------------------------------ A) DeriveCounter
        Eq("woundAll", WorldEventVector.DeriveCounter(
            "Mission complete when all rebel heroes are killed. The mission "
            + "fails if all heroes become wounded or incapacitated."),
            "WC::wounded_or_incapacitated_is_woundall");
        Eq("woundAll", WorldEventVector.DeriveCounter(
            "Niederlage, wenn alle Helden verwundet sind."), "WC::de_woundall");
        Eq("incapacitateAll", WorldEventVector.DeriveCounter(
            "The rebels are defeated if all heroes are incapacitated."),
            "WC::incapacitate_all");
        Eq("incapacitateAll", WorldEventVector.DeriveCounter(
            "Die Rebellen verlieren, wenn alle Helden get\u00f6tet werden."),
            "WC::de_kill_end");
        Eq("woundAll", WorldEventVector.DeriveCounter(
            "Das Imperium gewinnt, falls alle Helden verwundet sind."),
            "WC::imperial_win_word_woundall");
        Eq(null, WorldEventVector.DeriveCounter(
            "The wounded rebels flee toward the hangar. Strike quickly."),
            "WC::flavor_wounded_alone_is_null");
        Eq(null, WorldEventVector.DeriveCounter(
            "Recover the artifact. No hero may carry two weapons."),
            "WC::no_end_sentence_is_null");
        Eq(null, WorldEventVector.DeriveCounter(null), "WC::empty_is_null");

        // ------------------------------------------- C) killable-gated x2
        var bait = Card("G", 4);                       // wounded bar 4, green 2/3
        var twoGreen = new Rational(4, 3);             // 2 * 2/3

        // finisher figure (EV 5 >= printed wounded bar 4): x2 earned.
        var r = Rank(bait, true, null, new Rational(5));
        Eq("Bait", r[0].HeroId, "WC::killable_figure_keeps_finisher_law");
        Eq(twoGreen, r[0].ThreatEconomic, "WC::killable_econ_is_doubled_exact");

        // weak figure (EV 3 < bar 4): NO doubling - the healthy threat leads.
        r = Rank(bait, true, null, new Rational(3));
        Eq("Healthy", r[0].HeroId, "WC::ungated_wounded_is_not_a_magnet");
        var baitRow = r.First(x => x.HeroId == "Bait");
        Eq(new Rational(2, 3), baitRow.ThreatEconomic, "WC::no_killable_econ_is_plain");
        Eq(new Rational(2, 3), baitRow.Threat, "WC::threat_key_never_multiplied");

        // round-level projection (no figure EV): the vectors' blanket x2.
        r = Rank(bait, true, null, null);
        Eq("Bait", r[0].HeroId, "WC::projection_keeps_blanket_canon");
        Eq(twoGreen, r.First(x => x.HeroId == "Bait").ThreatEconomic,
           "WC::projection_econ_bitidentical");

        // healthy hero: EV never matters (no wound state).
        r = Rank(bait, false, null, new Rational(3));
        Eq(new Rational(2, 3), r.First(x => x.HeroId == "Bait").ThreatEconomic,
           "WC::healthy_candidate_unaffected_by_gate");

        // ------------------------------------------------ B) woundAll law
        // healthy-first BEFORE threat economy: the wounded bait cannot outrank
        // a healthy hero even for a finisher figure (x2 would win the econ).
        r = Rank(bait, true, "woundAll", new Rational(5));
        Eq("Healthy", r[0].HeroId, "WC::woundall_healthy_first_beats_econ");
        Eq(new Rational(2, 3), r[1].ThreatEconomic, "WC::woundall_no_wound_economy");
        Eq("card", r[0].Basis, "WC::woundall_label_card_rows");

        // determinism: same input, same order, twice.
        var a = string.Join(";", Rank(bait, true, null, new Rational(5)).Select(x => x.HeroId));
        var b = string.Join(";", Rank(bait, true, null, new Rational(5)).Select(x => x.HeroId));
        Eq(a, b, "WC::determinism_2x");

        Console.WriteLine(fails == 0 ? "== WFOCUS ALL PASS ==" : "== WFOCUS FAILS=" + fails + " ==");
        if (fails != 0) Environment.ExitCode = 1;
    }
}
