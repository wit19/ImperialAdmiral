// M13g settings-loader section: the backward-compat route of the removed
// deployment switch. TacticalAISettings (pure C#, Core/) must load old save
// files that still carry a "deployment" field WITHOUT throwing and WITHOUT
// any state effect - the loader reads only named fields via Json.Get. The
// expect blocks are hand-pinned ground truth (same documented route as
// action_tree.json, not --record; there is no Python sim twin). Settings-
// only: planning behaviour and all Canon vectors are untouched. State
// hygiene: every case starts from ResetDefaults and the section unhooks +
// resets when it leaves, so it can never bleed into other sections.
using System;
using System.Collections.Generic;
using TacticalAI;
using TacticalAI.Core;

namespace TacticalAiHarness
{
    public static partial class Program
    {
        static void SecSettingsLoad()
        {
            var v = LoadJson("settings_load.json");
            foreach (var raw in Json.AsList(v["cases"]))
            {
                var c = Json.AsDict(raw);
                var name = Json.AsStr(c["name"]);
                var saveJson = Json.AsStr(c["json"]);
                var exp = Json.AsDict(c["expect"]);

                TacticalAISettings.ResetDefaults();
                bool loadedOk = true;
                try
                {
                    TacticalAISettings.SetSaveHook(_ => saveJson);
                    TacticalAISettings.Load();
                }
                catch (Exception) { loadedOk = false; }
                // hand-pinned ground truth: a stale 'deployment' field (and
                // any unknown key) must NEVER make Load throw.
                Check("settings[" + name + "].loaded_ok", true, loadedOk);
                if (loadedOk)
                {
                    Check("settings[" + name + "].enabled", Json.AsBool(exp["enabled"]), TacticalAISettings.Enabled);
                    Check("settings[" + name + "].skill", Json.AsStr(exp["skill"]), TacticalAISettings.SkillLevel);
                    Check("settings[" + name + "].explain", Json.AsStr(exp["explain"]), TacticalAISettings.ExplainLevel);
                    // M13c: the info-block switch (absent in old saves ->
                    // true; explicit false must round-trip).
                    Check("settings[" + name + "].info", Json.AsBool(exp["info"]), TacticalAISettings.InfoText);
                    Check("settings[" + name + "].mode", Json.AsStr(exp["mode"]), TacticalAISettings.Mode);
                    Check("settings[" + name + "].mode_override", Json.AsBool(exp["mode_override"]), TacticalAISettings.HasModeOverride);
                    // Personas (Thomas 02.11 request): the global default must
                    // survive the Load route byte-exact (absent -> "", stored
                    // value -> verbatim; the settings layer never validates,
                    // PersonaState does - so "any string round-trips" is the
                    // correct pin here, not asset membership).
                    if (exp.ContainsKey("persona"))
                        Check("settings[" + name + "].persona",
                              Json.AsStr(exp["persona"]), TacticalAISettings.PersonaDefault);
                    Check("settings[" + name + "].to_json", Json.AsStr(exp["to_json"]), TacticalAISettings.ToJson());
                    // Write path: assigning the setter must serialise persona
                    // into ToJson (the only thing the save hook will store).
                    // AFTER the to_json pin - the sub-block resets.
                    if (exp.ContainsKey("persona_set"))
                    {
                        TacticalAISettings.PersonaDefault = Json.AsStr(exp["persona_set"]);
                        Check("settings[" + name + "].persona_to_json",
                              Json.AsStr(exp["persona_to_json"]), TacticalAISettings.ToJson());
                        TacticalAISettings.ResetDefaults();
                    }
                }
                // stale state must not survive a reset (same hook still set)
                TacticalAISettings.ResetDefaults();
                Check("settings[" + name + "].reset_clean", Json.AsStr(exp["reset_clean"]), TacticalAISettings.ToJson());
                TacticalAISettings.SetSaveHook(null);
            }
        }
    }
}
