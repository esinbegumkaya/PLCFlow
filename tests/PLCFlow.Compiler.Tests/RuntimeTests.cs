using PLCFlow.Compiler;
using PLCFlow.Runtime;
using Xunit;

namespace PLCFlow.Compiler.Tests;

public class RuntimeTests
{
    [Fact]
    public void Ton_ResetsWhenInputTurnsFalse()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; Delay : TON; END_VAR Delay(IN := Start, PT := T#20ms); Motor := Delay.Q; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);

        var runtime = new PlcRuntime(result.Program!);
        runtime.SetInputAddress("%I0.0", true);
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.GetOutputAddress("%Q0.0"));

        runtime.SetInputAddress("%I0.0", false);
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));

        Assert.False(runtime.GetOutputAddress("%Q0.0"));
        Assert.False(runtime.Timers["Delay"].Q);
        Assert.Equal(TimeSpan.Zero, runtime.Timers["Delay"].Et);
    }

    [Fact]
    public void IfElse_ExecutesExpectedBranch()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; END_VAR IF Start THEN Motor := TRUE; ELSE Motor := FALSE; END_IF; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);

        var runtime = new PlcRuntime(result.Program!);
        runtime.SetInputAddress("%I0.0", false);
        runtime.ScanCycle();
        Assert.False(runtime.GetOutputAddress("%Q0.0"));

        runtime.SetInputAddress("%I0.0", true);
        runtime.ScanCycle();
        Assert.True(runtime.GetOutputAddress("%Q0.0"));
    }

    [Fact]
    public void Memory_PersistsAcrossScanCycles()
    {
        const string source = "PROGRAM Main VAR Counter : INT; END_VAR Counter := Counter + 1; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);

        var runtime = new PlcRuntime(result.Program!);
        runtime.ScanCycle();
        runtime.ScanCycle();
        runtime.ScanCycle();

        Assert.Equal(3, Assert.IsType<int>(runtime.Memory["Counter"]));
        Assert.Equal(3L, runtime.CycleCount);
    }

    [Fact]
    public void UnknownInputAddress_IsRejectedByRuntime()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; END_VAR END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);

        var runtime = new PlcRuntime(result.Program!);
        Assert.Throws<ArgumentException>(() => runtime.SetInputAddress("%I9.9", true));
    }
}
