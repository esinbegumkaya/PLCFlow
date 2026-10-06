namespace PLCFlow.Compiler.Syntax;

public enum PlcType { Bool, Int, Ton }

public abstract record AstNode;
public sealed record ProgramNode(string Name, IReadOnlyList<VariableDeclaration> Variables, IReadOnlyList<Statement> Statements) : AstNode;
public sealed record VariableDeclaration(string Name, PlcType Type, string? Address = null) : AstNode
{
    public bool IsInput => Address?.StartsWith("%I", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsOutput => Address?.StartsWith("%Q", StringComparison.OrdinalIgnoreCase) == true;
}

public abstract record Statement : AstNode;
public sealed record AssignmentStatement(string Name, Expression Value) : Statement;
public sealed record IfStatement(Expression Condition, IReadOnlyList<Statement> ThenStatements, IReadOnlyList<Statement> ElseStatements) : Statement;
public sealed record FunctionBlockCallStatement(string InstanceName, IReadOnlyDictionary<string, Expression> Arguments) : Statement;

public abstract record Expression : AstNode;
public sealed record IdentifierExpression(string Name) : Expression;
public sealed record MemberAccessExpression(string InstanceName, string MemberName) : Expression;
public sealed record IntegerLiteralExpression(int Value) : Expression;
public sealed record TimeLiteralExpression(TimeSpan Value, string Text) : Expression;
public sealed record BooleanLiteralExpression(bool Value) : Expression;
public sealed record UnaryExpression(string Operator, Expression Operand) : Expression;
public sealed record BinaryExpression(Expression Left, string Operator, Expression Right) : Expression;
