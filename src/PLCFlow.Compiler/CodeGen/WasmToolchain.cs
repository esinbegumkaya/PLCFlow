using System.Diagnostics;
using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Compiler.CodeGen;

public sealed class WasmToolchain
{
    public string ClangExecutable { get; init; } = "clang";
    public string NodeExecutable { get; init; } = "node";

    public void EmitWasm(ProgramNode program, string outputPath)
    {
        var llvm = new LlvmEmitter().Emit(program);
        var outputFullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);
        var tempLl = Path.Combine(Path.GetTempPath(), $"plcflow-{Guid.NewGuid():N}.ll");
        File.WriteAllText(tempLl, llvm);
        try
        {
            RunProcess(
                ClangExecutable,
                [
                    "--target=wasm32",
                    "-nostdlib",
                    "-Wl,--no-entry",
                    "-Wl,--export-all",
                    "-Wl,--strip-all",
                    tempLl,
                    "-o",
                    outputFullPath
                ],
                "LLVM/Clang WebAssembly compilation failed");
        }
        finally
        {
            if (File.Exists(tempLl)) File.Delete(tempLl);
        }
    }

    public void RunWasm(
        ProgramNode program,
        string wasmPath,
        IReadOnlyDictionary<string, bool> inputAddresses,
        int cycles,
        int cycleMs)
    {
        var inputAssignments = new List<(string Variable, bool Value)>();
        foreach (var input in inputAddresses)
        {
            var variable = program.Variables.FirstOrDefault(v =>
                v.IsInput && string.Equals(v.Address, input.Key, StringComparison.OrdinalIgnoreCase));
            if (variable is null)
                throw new InvalidOperationException($"Unknown PLC input address '{input.Key}' for WebAssembly execution.");
            inputAssignments.Add((variable.Name, input.Value));
        }

        var outputs = program.Variables.Where(v => v.IsOutput).ToArray();
        var timers = program.Variables.Where(v => v.Type == PlcType.Ton).ToArray();
        var runner = BuildNodeRunner(inputAssignments, outputs, timers, cycles, cycleMs);
        var tempRunner = Path.Combine(Path.GetTempPath(), $"plcflow-wasm-{Guid.NewGuid():N}.mjs");
        File.WriteAllText(tempRunner, runner);
        try
        {
            RunProcess(NodeExecutable, [tempRunner, Path.GetFullPath(wasmPath)], "WebAssembly runtime failed", inheritOutput: true);
        }
        finally
        {
            if (File.Exists(tempRunner)) File.Delete(tempRunner);
        }
    }

    private static string BuildNodeRunner(
        IReadOnlyList<(string Variable, bool Value)> inputs,
        IReadOnlyList<VariableDeclaration> outputs,
        IReadOnlyList<VariableDeclaration> timers,
        int cycles,
        int cycleMs)
    {
        static string JsName(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
        var lines = new List<string>
        {
            "import fs from 'node:fs';",
            "const wasmPath = process.argv[2];",
            "const bytes = fs.readFileSync(wasmPath);",
            "const { instance } = await WebAssembly.instantiate(bytes, {});",
            "const e = instance.exports;",
            $"e.plc_set_cycle_ms({cycleMs});"
        };

        foreach (var input in inputs)
            lines.Add($"e['plc_set_{JsName(input.Variable)}']({(input.Value ? 1 : 0)});");

        lines.Add($"for (let i = 0; i < {Math.Max(0, cycles)}; i++) e.scan_cycle();");
        lines.Add($"console.log('✓ WebAssembly scan cycles: {Math.Max(0, cycles)} × {cycleMs} ms');");

        if (outputs.Count > 0)
        {
            lines.Add("console.log('  OUTPUT PROCESS IMAGE');");
            foreach (var output in outputs)
                lines.Add($"console.log('    {JsName(output.Address ?? output.Name)} = ' + Boolean(e['plc_get_{JsName(output.Name)}']()));");
        }

        if (timers.Count > 0)
        {
            lines.Add("console.log('  TIMERS');");
            foreach (var timer in timers)
            {
                var name = JsName(timer.Name);
                lines.Add($"console.log('    {name}: Q=' + Boolean(e['plc_get_{name}_Q']()) + ', ET=' + e['plc_get_{name}_ET']() + 'ms, PT=' + e['plc_get_{name}_PT']() + 'ms');");
            }
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void RunProcess(string executable, IReadOnlyList<string> arguments, string errorPrefix, bool inheritOutput = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = !inheritOutput,
            RedirectStandardError = !inheritOutput,
            UseShellExecute = false
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start '{executable}'. Make sure it is installed and available on PATH. {ex.Message}", ex);
        }

        if (process is null) throw new InvalidOperationException($"Could not start '{executable}'.");
        using (process)
        {
            var stdout = inheritOutput ? string.Empty : process.StandardOutput.ReadToEnd();
            var stderr = inheritOutput ? string.Empty : process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"{errorPrefix} (exit {process.ExitCode}).\n{stdout}\n{stderr}".Trim());
        }
    }
}
