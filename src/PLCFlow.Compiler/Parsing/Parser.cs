using System.Globalization;
using PLCFlow.Compiler.Lexing;
using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Compiler.Parsing;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;
    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public ProgramNode ParseProgram()
    {
        Match(TokenKind.Program);
        var name = Match(TokenKind.Identifier).Text;
        var vars = new List<VariableDeclaration>();
        if (Current.Kind == TokenKind.Var)
        {
            Next();
            while (Current.Kind != TokenKind.EndVar)
            {
                var varName = Match(TokenKind.Identifier).Text;
                string? address = null;
                if (Current.Kind == TokenKind.At)
                {
                    Next();
                    address = Match(TokenKind.DirectAddress).Text;
                }
                Match(TokenKind.Colon);
                var type = Current.Kind switch
                {
                    TokenKind.Bool => PlcType.Bool,
                    TokenKind.Int => PlcType.Int,
                    TokenKind.Ton => PlcType.Ton,
                    _ => throw Error("Expected BOOL, INT or TON")
                };
                Next(); Match(TokenKind.Semicolon);
                vars.Add(new VariableDeclaration(varName, type, address));
            }
            Match(TokenKind.EndVar);
        }
        var statements = ParseStatementsUntil(TokenKind.EndProgram);
        Match(TokenKind.EndProgram);
        Match(TokenKind.EndOfFile);
        return new ProgramNode(name, vars, statements);
    }

    private List<Statement> ParseStatementsUntil(params TokenKind[] terminators)
    {
        var result = new List<Statement>();
        while (!terminators.Contains(Current.Kind) && Current.Kind != TokenKind.EndOfFile)
            result.Add(ParseStatement());
        return result;
    }

    private Statement ParseStatement()
    {
        if (Current.Kind == TokenKind.If) return ParseIf();
        var name = Match(TokenKind.Identifier).Text;
        if (Current.Kind == TokenKind.LeftParen) return ParseFunctionBlockCall(name);
        Match(TokenKind.Assign);
        var value = ParseExpression();
        Match(TokenKind.Semicolon);
        return new AssignmentStatement(name, value);
    }

    private FunctionBlockCallStatement ParseFunctionBlockCall(string instanceName)
    {
        Match(TokenKind.LeftParen);
        var arguments = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);
        while (Current.Kind != TokenKind.RightParen)
        {
            var argumentName = Match(TokenKind.Identifier).Text;
            Match(TokenKind.Assign);
            arguments[argumentName] = ParseExpression();
            if (Current.Kind == TokenKind.Comma) Next();
            else break;
        }
        Match(TokenKind.RightParen);
        Match(TokenKind.Semicolon);
        return new FunctionBlockCallStatement(instanceName, arguments);
    }

    private IfStatement ParseIf()
    {
        Match(TokenKind.If);
        var condition = ParseExpression();
        Match(TokenKind.Then);
        var thenStatements = ParseStatementsUntil(TokenKind.Else, TokenKind.EndIf);
        var elseStatements = new List<Statement>();
        if (Current.Kind == TokenKind.Else) { Next(); elseStatements = ParseStatementsUntil(TokenKind.EndIf); }
        Match(TokenKind.EndIf);
        if (Current.Kind == TokenKind.Semicolon) Next();
        return new IfStatement(condition, thenStatements, elseStatements);
    }

    private Expression ParseExpression(int parentPrecedence = 0)
    {
        Expression left;
        var unaryPrecedence = UnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var op = Next();
            left = new UnaryExpression(op.Text.ToUpperInvariant(), ParseExpression(unaryPrecedence));
        }
        else left = ParsePrimary();

        while (true)
        {
            var precedence = BinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence) break;
            var op = Next();
            var right = ParseExpression(precedence);
            left = new BinaryExpression(left, op.Text.ToUpperInvariant(), right);
        }
        return left;
    }

    private Expression ParsePrimary()
    {
        if (Current.Kind == TokenKind.LeftParen) { Next(); var e = ParseExpression(); Match(TokenKind.RightParen); return e; }
        if (Current.Kind == TokenKind.Number) return new IntegerLiteralExpression(int.Parse(Next().Text, CultureInfo.InvariantCulture));
        if (Current.Kind == TokenKind.TimeLiteral) { var token = Next(); return new TimeLiteralExpression(ParseTime(token.Text), token.Text); }
        if (Current.Kind == TokenKind.True) { Next(); return new BooleanLiteralExpression(true); }
        if (Current.Kind == TokenKind.False) { Next(); return new BooleanLiteralExpression(false); }
        if (Current.Kind == TokenKind.Identifier)
        {
            var name = Next().Text;
            if (Current.Kind == TokenKind.Dot)
            {
                Next();
                var member = Match(TokenKind.Identifier).Text;
                return new MemberAccessExpression(name, member);
            }
            return new IdentifierExpression(name);
        }
        throw Error("Expected expression");
    }

    private static TimeSpan ParseTime(string text)
    {
        var raw = text[2..].ToLowerInvariant();
        if (raw.EndsWith("ms") && double.TryParse(raw[..^2], CultureInfo.InvariantCulture, out var ms)) return TimeSpan.FromMilliseconds(ms);
        if (raw.EndsWith('s') && double.TryParse(raw[..^1], CultureInfo.InvariantCulture, out var s)) return TimeSpan.FromSeconds(s);
        if (raw.EndsWith('m') && double.TryParse(raw[..^1], CultureInfo.InvariantCulture, out var m)) return TimeSpan.FromMinutes(m);
        throw new FormatException($"Unsupported time literal '{text}'. Use T#500ms, T#2s or T#1m.");
    }

    private static int UnaryPrecedence(TokenKind kind) => kind is TokenKind.Not or TokenKind.Minus ? 6 : 0;
    private static int BinaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.Star or TokenKind.Slash => 5,
        TokenKind.Plus or TokenKind.Minus => 4,
        TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual => 3,
        TokenKind.And => 2,
        TokenKind.Or => 1,
        _ => 0
    };

    private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
    private Token Next() => _tokens[_position++];
    private Token Match(TokenKind kind) { if (Current.Kind != kind) throw Error($"Expected {kind}, found {Current.Kind}"); return Next(); }
    private CompilerException Error(string message) => new(message, Current.Line, Current.Column);
}
