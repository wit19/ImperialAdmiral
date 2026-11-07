namespace TacticalAI
{
    /// <summary>P34-FU (Player-35 forensics): every shipped TacticalAI build
    /// stamps itself into the boot log and the producer session line, so a
    /// playtest log answers "which code ran?" without guessing. BUMP THIS
    /// CONSTANT IN THE SAME COMMIT THAT TAGS A dev/RELEASE — the value must
    /// match the git tag it ships with (one-line release law, MECHANICS
    /// release section). No game behavior reads it; log-only.</summary>
    public static class TacticalAiBuildStamp
    {
        public const string Value = "v1.0.1-dev.15";
    }
}
