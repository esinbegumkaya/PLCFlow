using PLCFlow.Compiler.Lexing;
using PLCFlow.Compiler.Parsing;
using PLCFlow.Compiler.Semantics;
using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Compiler;

public sealed record CompilationResult(ProgramNode? Program, SemanticResult? Semantics, Exception? Error)
{
    public bool Success => Error is null && Semantics?.Success == true;
}

public static class CompilerPipeline
{
    public static CompilationResult Compile(string source)
    {
        try
        {
            var tokens = new Lexer(source).Lex();
            var program = new Parser(tokens).ParseProgram();
            var semantics = new SemanticAnalyzer().Analyze(program);
            return new CompilationResult(program, semantics, null);
        }
        catch (Exception ex) { return new CompilationResult(null, null, ex); }
    }
}
