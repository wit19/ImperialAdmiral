using System;
using TacticalAI;
class P {
    static void Eq(object a, object b, string m) { if (!Equals(a, b)) { Console.WriteLine("FAIL: " + m + " (" + a + " != " + b + ")"); Environment.ExitCode = 1; } else Console.WriteLine("ok: " + m); }
    static void Main(string[] args) {
        TacticalAISettings.ResetDefaults();
        // M6p (Thomas 02.11): defaults re-clamped ON at S5/Thrawn. Old pin was
        // Eq("reference", Mode, "S1 derives reference"); the derivation law is
        // untouched - only the default rung moved, so the picture inverts.
        Eq(true, TacticalAISettings.Enabled, "M6p default is ON");
        Eq("S5", TacticalAISettings.SkillLevel, "M6p default is S5/Thrawn");
        Eq("overlord", TacticalAISettings.Mode, "S5 derives overlord");
        TacticalAISettings.Enabled = true;
        // dev 21 cycle
        string t = "";
        var seen = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 8; i++) { t = TacticalAISettings.NextMissionToken(t); seen.Add(t); }
        Eq("off,S1,S2,S3,S4,S5,,off", string.Join(",", seen), "cycle AUTO->OFF->S1..S5->AUTO->OFF");
        Eq("legacy", TacticalAISettings.EffectiveMode("off"), "OFF badge plays legacy");
        Eq("reference", TacticalAISettings.EffectiveMode("S2"), "S2 -> reference");
        Eq("overlord", TacticalAISettings.EffectiveMode("S4"), "S4 -> overlord");
        Eq("S2", TacticalAISettings.EffectiveSkill("S2"), "rung pins skill");
        TacticalAISettings.SkillLevel = "S3";
        Eq("S3", TacticalAISettings.EffectiveSkill(""), "AUTO follows global");
        Eq("overlord", TacticalAISettings.EffectiveMode("overlord"), "deprecated overlord playable");
        Eq("reference", TacticalAISettings.EffectiveMode("reference"), "deprecated reference playable");
        Eq("legacy", TacticalAISettings.EffectiveMode("legacy"), "deprecated legacy playable");
        Eq(TacticalAISettings.NextMissionToken("bogus"), "off", "unknown -> OFF");
        WorldFeedGate.Run();
        OverlordGate.Run();
        WoundFocusGate.Run();
        ImperialDeckGate.Run();
        ImperialPurchaseGate.Run();
        ImperialCardEffectsGate.Run();
        AgendaGate.Run();
        OptionalDeployGate.Run();
        PersonasGate.Run();
        TabooLintGate.Run();
        OfficerTextGate.Run();
        OfficerSupporterGate.Run();
        PatternRowLanguageGate.Run();
        MissionCriticalGate.Run();
        SectionTrustGate.Run();
        SectionTextGate.Run();
        UiCompletenessGate.Run();
        DeploymentFilterGate.Run();
        DpScoreGate.Run();
        DutyMineGate.Run();    // P37a (t_1f4d7286): golden vector 7/7/124 + data-exists + Pfad-Doppelpfad, fail-closed
        DutyCoreGate.Run();    // P37c (t_e80246ed): Escort-Quota golden rerun (hoth10/one/six/no_duty/shadow) + Purity + Fairness-Scan, zweiter Compile-Kontext
        DutyScrubGate.Run();   // P37d (t_ffc705af): Briefing-Scrub Zaehlt gescrubte Saetze je Duty-Mission x Sprache gegen Inventar (7x11), QA-BLOCKED-Liste, Corpus-Leak-Scan (andere Missionen byte-identisch), Mission-Liste aus dem dutyAnchors-Artefakt
        NeutralPreyGate.Run(args);   // P38 (t_11ebb843): NEUTRALE-Beute-Zensus - Producer-Mirror + echte Core-Ableitung ueber alle ausgelieferten Missionen, docs/CENSUS_beute_objekte.md fail-closed gegen-gerechnet (Modus 'census': Tabelle drucken)
        MassiveInteriorGate.Run();   // P41b (t_1b28a273): Interior-Gesetz Massive - Census-vs-Datei-Gleichheit der Crosswalk-Zeilentabelle, RR-Vollstaendigkeit 19..36/38/39 fail-closed, Numeric-Overlap-Guard (Hoth), Vier-Zustands-Ableitung auf echter CORE4-Karte; Override runtime==census kommt erst mit H3
        Console.WriteLine(Environment.ExitCode == 0 ? "ALL PASS" : "FAILURES");
    }
}
