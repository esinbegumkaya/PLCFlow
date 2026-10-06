using PLCFlow.Compiler;
using PLCFlow.Compiler.CodeGen;
using PLCFlow.Runtime;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run --project src/PLCFlow.Cli -- <file.st> [--run] [--cycles N] [--cycle-ms N] [--input %I0.0=true] [--emit-llvm [output.ll]] [--emit-wasm [output.wasm]] [--run-wasm]");
    return;
}

var sourcePath = args[0];
var source = File.ReadAllText(sourcePath);
var result = CompilerPipeline.Compile(source);
if (result.Error is not null)
{
    Console.Error.WriteLine(result.Error.Message);
    Environment.ExitCode = 1;
    return;
}
foreach (var diagnostic in result.Semantics!.Diagnostics)
    Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
if (!result.Success)
{
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"✓ Parsed PROGRAM {result.Program!.Name}");
Console.WriteLine($"✓ Semantic analysis passed ({result.Semantics.Symbols.Count} symbols)");

var inputValues = ReadInputs(args);
if (inputValues is null)
{
    Environment.ExitCode = 1;
    return;
}

var cycleMs = ReadIntOption(args, "--cycle-ms", 10);
var cycles = ReadIntOption(args, "--cycles", 1);

var llvmIndex = Array.IndexOf(args, "--emit-llvm");
if (llvmIndex >= 0)
{
    try
    {
        var llvm = new LlvmEmitter().Emit(result.Program);
        var outputPath = ReadOptionalOutputPath(args, llvmIndex, Path.ChangeExtension(sourcePath, ".ll"));
        File.WriteAllText(outputPath, llvm);
        Console.WriteLine($"✓ LLVM IR written to {outputPath}");
    }
    catch (NotSupportedException ex)
    {
        Console.Error.WriteLine($"LLVM backend: {ex.Message}");
        Environment.ExitCode = 2;
        return;
    }
}

var wasmIndex = Array.IndexOf(args, "--emit-wasm");
var runWasm = args.Contains("--run-wasm");
if (wasmIndex >= 0 || runWasm)
{
    try
    {
        var defaultWasm = Path.ChangeExtension(sourcePath, ".wasm");
        var wasmPath = wasmIndex >= 0
            ? ReadOptionalOutputPath(args, wasmIndex, defaultWasm)
            : defaultWasm;
        var toolchain = new WasmToolchain();
        toolchain.EmitWasm(result.Program, wasmPath);
        Console.WriteLine($"✓ WebAssembly module written to {wasmPath}");

        if (runWasm)
            toolchain.RunWasm(result.Program, wasmPath, inputValues, cycles, cycleMs);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"WebAssembly backend: {ex.Message}");
        Console.Error.WriteLine("Install LLVM/Clang and Node.js, then make sure both 'clang' and 'node' are available on PATH.");
        Environment.ExitCode = 3;
        return;
    }
}

if (args.Contains("--run"))
{
    var runtime = new PlcRuntime(result.Program);
    runtime.DefaultCycleTime = TimeSpan.FromMilliseconds(cycleMs);

    foreach (var input in inputValues)
        runtime.SetInputAddress(input.Key, input.Value);

    for (var i = 0; i < cycles; i++) runtime.ScanCycle();

    Console.WriteLine($"✓ PLC scan cycles: {runtime.CycleCount} × {cycleMs} ms");
    if (runtime.Inputs.Count > 0)
    {
        Console.WriteLine("  INPUT PROCESS IMAGE");
        foreach (var item in runtime.Inputs) Console.WriteLine($"    {item.Key} = {item.Value}");
    }
    if (runtime.Outputs.Count > 0)
    {
        Console.WriteLine("  OUTPUT PROCESS IMAGE");
        foreach (var item in runtime.Outputs) Console.WriteLine($"    {item.Key} = {item.Value}");
    }
    if (runtime.Timers.Count > 0)
    {
        Console.WriteLine("  TIMERS");
        foreach (var item in runtime.Timers)
            Console.WriteLine($"    {item.Key}: IN={item.Value.In}, Q={item.Value.Q}, ET={item.Value.Et.TotalMilliseconds:0}ms, PT={item.Value.Pt.TotalMilliseconds:0}ms");
    }
    Console.WriteLine("  MEMORY");
    foreach (var item in runtime.Memory) Console.WriteLine($"    {item.Key} = {item.Value}");
}

static string ReadOptionalOutputPath(string[] args, int optionIndex, string fallback)
{
    return optionIndex + 1 < args.Length && !args[optionIndex + 1].StartsWith("--", StringComparison.Ordinal)
        ? args[optionIndex + 1]
        : fallback;
}

static int ReadIntOption(string[] args, string option, int defaultValue)
{
    var index = Array.IndexOf(args, option);
    return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value)
        ? value
        : defaultValue;
}

static Dictionary<string, bool>? ReadInputs(string[] args)
{
    var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    foreach (var input in ReadMultiOption(args, "--input"))
    {
        var parts = input.Split('=', 2);
        if (parts.Length != 2 || !bool.TryParse(parts[1], out var value))
        {
            Console.Error.WriteLine($"Invalid --input '{input}'. Example: --input %I0.0=true");
            return null;
        }
        values[parts[0]] = value;
    }
    return values;
}

static IEnumerable<string> ReadMultiOption(string[] args, string option)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(option, StringComparison.OrdinalIgnoreCase))
            yield return args[i + 1];
}
