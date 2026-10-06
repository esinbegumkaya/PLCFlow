using Xunit;
using PLCFlow.Compiler;
using PLCFlow.Compiler.Lexing;
using PLCFlow.Compiler.Parsing;
using PLCFlow.Compiler.Syntax;
using PLCFlow.Compiler.CodeGen;
using PLCFlow.Runtime;

namespace PLCFlow.Compiler.Tests;

public class CompilerTests
{
    [Fact]
    public void Lexer_RecognizesStructuredTextKeywords()
    {
        var tokens = new Lexer("PROGRAM Main VAR Motor : BOOL; END_VAR END_PROGRAM").Lex();
        Assert.Contains(tokens, t => t.Kind == TokenKind.Program);
        Assert.Contains(tokens, t => t.Kind == TokenKind.Bool);
    }

    [Fact]
    public void Parser_BuildsAssignmentAst()
    {
        const string source = "PROGRAM Main VAR Counter : INT; END_VAR Counter := Counter + 1; END_PROGRAM";
        var program = new Parser(new Lexer(source).Lex()).ParseProgram();
        var assignment = Assert.IsType<AssignmentStatement>(Assert.Single(program.Statements));
        Assert.Equal("Counter", assignment.Name);
    }

    [Fact]
    public void SemanticAnalyzer_RejectsIntAssignedToBool()
    {
        const string source = "PROGRAM Main VAR Motor : BOOL; END_VAR Motor := 42; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.False(result.Success);
        Assert.Contains(result.Semantics!.Diagnostics, d => d.Code == "PLCF1003");
    }

    [Fact]
    public void LlvmEmitter_GeneratesScanCycleAndGlobals()
    {
        const string source = "PROGRAM Main VAR Counter : INT; Enabled : BOOL; END_VAR Counter := Counter + 1; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        var llvm = new LlvmEmitter().Emit(result.Program!);
        Assert.Contains("@Counter = global i32 0", llvm);
        Assert.Contains("define void @scan_cycle()", llvm);
        Assert.Contains("add i32", llvm);
    }

    [Fact]
    public void ValidConveyorProgram_Compiles()
    {
        const string source = "PROGRAM Conveyor VAR StartButton : BOOL; Motor : BOOL; END_VAR IF StartButton THEN Motor := TRUE; END_IF; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
    }

    [Fact]
    public void Parser_RecognizesDirectPlcAddresses()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; END_VAR Motor := Start; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        Assert.Equal("%I0.0", result.Program!.Variables.Single(v => v.Name == "Start").Address);
        Assert.True(result.Program.Variables.Single(v => v.Name == "Motor").IsOutput);
    }

    [Fact]
    public void SemanticAnalyzer_RejectsWriteToInput()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; END_VAR Start := TRUE; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.False(result.Success);
        Assert.Contains(result.Semantics!.Diagnostics, d => d.Code == "PLCF1012");
    }

    [Fact]
    public void Runtime_MapsInputToOutputThroughProcessImage()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; END_VAR Motor := Start; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        var runtime = new PlcRuntime(result.Program!);
        runtime.SetInputAddress("%I0.0", true);
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.GetOutputAddress("%Q0.0"));
    }

    [Fact]
    public void Ton_TimerTurnsOnAfterPresetTime()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; Delay : TON; END_VAR Delay(IN := Start, PT := T#30ms); Motor := Delay.Q; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        var runtime = new PlcRuntime(result.Program!);
        runtime.SetInputAddress("%I0.0", true);
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.GetOutputAddress("%Q0.0"));
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        Assert.False(runtime.GetOutputAddress("%Q0.0"));
        runtime.ScanCycle(TimeSpan.FromMilliseconds(10));
        Assert.True(runtime.GetOutputAddress("%Q0.0"));
        Assert.True(runtime.Timers["Delay"].Q);
    }

    [Fact]
    public void LlvmEmitter_LowersTonForWebAssembly()
    {
        const string source = "PROGRAM Main VAR Start AT %I0.0 : BOOL; Motor AT %Q0.0 : BOOL; Delay : TON; END_VAR Delay(IN := Start, PT := T#30ms); Motor := Delay.Q; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        var llvm = new LlvmEmitter().Emit(result.Program!);
        Assert.Contains("target triple = \"wasm32-unknown-unknown\"", llvm);
        Assert.Contains("@Delay_Q = global i1 0", llvm);
        Assert.Contains("@Delay_ET = global i32 0", llvm);
        Assert.Contains("define void @plc_set_Start(i32 %value)", llvm);
        Assert.Contains("define i32 @plc_get_Motor()", llvm);
        Assert.Contains("store i1", llvm);
    }

    [Fact]
    public void LlvmEmitter_ExportsCycleTimeApi()
    {
        const string source = "PROGRAM Main VAR Counter : INT; END_VAR Counter := Counter + 1; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);
        Assert.True(result.Success);
        var llvm = new LlvmEmitter().Emit(result.Program!);
        Assert.Contains("define void @plc_set_cycle_ms(i32 %value)", llvm);
        Assert.Contains("define i32 @plc_get_cycle_ms()", llvm);
        Assert.Contains("define void @scan_cycle()", llvm);
    }
}
