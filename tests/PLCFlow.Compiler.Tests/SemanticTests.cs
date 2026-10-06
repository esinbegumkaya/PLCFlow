using PLCFlow.Compiler;
using Xunit;

namespace PLCFlow.Compiler.Tests;

public class SemanticTests
{
    [Fact]
    public void DuplicateDeclaration_IsRejected()
    {
        const string source = "PROGRAM Main VAR Motor : BOOL; Motor : BOOL; END_VAR END_PROGRAM";
        var result = CompilerPipeline.Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Semantics!.Diagnostics, d => d.Code == "PLCF1002");
    }

    [Fact]
    public void InvalidDirectAddressBit_IsRejected()
    {
        const string source = "PROGRAM Main VAR Motor AT %Q0.8 : BOOL; END_VAR END_PROGRAM";
        var result = CompilerPipeline.Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Semantics!.Diagnostics, d => d.Code == "PLCF1010");
    }

    [Fact]
    public void UnknownTonMember_IsRejected()
    {
        const string source = "PROGRAM Main VAR Delay : TON; Motor : BOOL; END_VAR Motor := Delay.Done; END_PROGRAM";
        var result = CompilerPipeline.Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Semantics!.Diagnostics, d => d.Code == "PLCF1018");
    }
}
