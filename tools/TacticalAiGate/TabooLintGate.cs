// M6e RED gate (CONCEPT_personas_v2 Sec.4, Beschluss 6): runs the shipped-tree
// taboo lint (tools/taboo_lint.py) as a build-chain stop. The lint imports the
// Sec.4 word list + the M6a essence/swaps laws from tools/gen_personas_v2.py
// (single source, "keine zweite Liste") and checks the shipped persona asset,
// the Delta-0 gift-kind statics and the persona-owned texts of ALL 11 ui.json.
//
// Fail-closed on purpose: a missing script or a missing python3 interpreter
// is a FAIL, never a silent skip - a lint that cannot run must stop the chain
// exactly like a lint that finds something (the ignored-lane failure class:
// compiled, read-correct, silently dead at runtime).
//
// The corpus is the SHIPPED files only - zero fixtures, zero recorder. The
// lint's own self-test (every RED rule proven able to fire, run on in-memory
// copies) executes inside every gate run and its summary is asserted here.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

public static class TabooLintGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) { fails++; Environment.ExitCode = 1; }
    }

    // Repo root resolved from the gate's working directory first
    // (cwd = tools/TacticalAiGate, so detached QA worktrees lint THEIR OWN
    // tree), the absolute constant is the documented last resort - same
    // convention PersonasGate.App follows.
    static string FindRepoRoot()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..")),
            "/home/open/work/ia-ai/ImperialCommander2",
        };
        foreach (var c in candidates)
            if (File.Exists(Path.Combine(c, "tools", "taboo_lint.py"))) return c;
        return null;
    }

    public static int Run()
    {
        var repo = FindRepoRoot();
        Ok(repo != null, "taboo_lint: tools/taboo_lint.py present in the working tree");
        if (repo == null) return fails;
        var script = Path.Combine(repo, "tools", "taboo_lint.py");

        string output, error; int exit;
        var ran = TryPython(script, out output, out error, out exit);
        Ok(ran, "taboo_lint: interpreter ran the lint (no script, no python3 = gate RED, fail-closed)");
        if (!ran) return fails;

        // lint evidence goes into the gate log verbatim
        foreach (var line in (output ?? "").Split('\n'))
            if (line.Trim().Length > 0) Console.WriteLine("  lint| " + line.TrimEnd());
        if (!string.IsNullOrEmpty((error ?? "").Trim()))
            Console.WriteLine("  lint| stderr: " + error.Trim());

        Ok(exit == 0, "taboo_lint: exit code 0 (every finding => non-zero => build chain stops)");
        Ok((output ?? "").Contains("lint: GRUEN"), "taboo_lint: verdict line 'lint: GRUEN'");
        var m = Regex.Match(output ?? "", @"self-tests: (\d+)/(\d+) ok");
        Ok(m.Success && m.Groups[1].Value == m.Groups[2].Value,
            m.Success
                ? "taboo_lint: self-tests " + m.Groups[1].Value + "/" + m.Groups[2].Value + " ok (every RED rule proven able to fire)"
                : "taboo_lint: self-test summary line missing");

        Console.WriteLine("-- TabooLintGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL") + " --");
        return fails;
    }

    static bool TryPython(string script, out string output, out string error, out int exit)
    {
        output = ""; error = ""; exit = -1;
        foreach (var exe in new[] { Environment.GetEnvironmentVariable("PYTHON3"), "python3", "python" })
        {
            if (string.IsNullOrEmpty(exe)) continue;
            try
            {
                var psi = new ProcessStartInfo(exe, "\"" + script + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using (var p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd();
                    error = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exit = p.ExitCode;
                    return true;
                }
            }
            catch (Exception) { /* next interpreter candidate */ }
        }
        return false;
    }
}
