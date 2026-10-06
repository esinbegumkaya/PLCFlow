using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Compiler.Semantics;

public sealed record Diagnostic(string Code, string Message);
public sealed record SemanticResult(IReadOnlyDictionary<string, PlcType> Symbols, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class SemanticAnalyzer
{
    private readonly Dictionary<string, PlcType> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VariableDeclaration> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Diagnostic> _diagnostics = new();

    public SemanticResult Analyze(ProgramNode program)
    {
        _symbols.Clear(); _variables.Clear(); _diagnostics.Clear();
        foreach (var variable in program.Variables)
        {
            if (!_symbols.TryAdd(variable.Name, variable.Type))
                _diagnostics.Add(new("PLCF1002", $"Duplicate declaration '{variable.Name}'."));
            else _variables[variable.Name] = variable;

            if (variable.Address is not null && !IsValidAddress(variable.Address))
                _diagnostics.Add(new("PLCF1010", $"Invalid PLC direct address '{variable.Address}' for '{variable.Name}'."));
            if (variable.Type == PlcType.Ton && variable.Address is not null)
                _diagnostics.Add(new("PLCF1011", $"Function block '{variable.Name}' cannot use a direct I/O address."));
        }
        foreach (var statement in program.Statements) AnalyzeStatement(statement);
        return new SemanticResult(new Dictionary<string, PlcType>(_symbols, StringComparer.OrdinalIgnoreCase), _diagnostics.ToArray());
    }

    private void AnalyzeStatement(Statement statement)
    {
        switch (statement)
        {
            case AssignmentStatement a:
                if (!_symbols.TryGetValue(a.Name, out var targetType)) { _diagnostics.Add(new("PLCF1001", $"Undefined symbol '{a.Name}'.")); return; }
                if (_variables.TryGetValue(a.Name, out var declaration) && declaration.IsInput)
                    _diagnostics.Add(new("PLCF1012", $"Input '{a.Name}' ({declaration.Address}) is read-only inside the PLC program."));
                if (targetType == PlcType.Ton) { _diagnostics.Add(new("PLCF1013", $"Cannot assign directly to TON instance '{a.Name}'.")); return; }
                var valueType = TypeOf(a.Value);
                if (valueType is not null && valueType != targetType)
                    _diagnostics.Add(new("PLCF1003", $"Cannot assign {valueType} to {targetType} variable '{a.Name}'."));
                break;
            case IfStatement i:
                var conditionType = TypeOf(i.Condition);
                if (conditionType is not null && conditionType != PlcType.Bool)
                    _diagnostics.Add(new("PLCF1004", "IF condition must be BOOL."));
                foreach (var s in i.ThenStatements) AnalyzeStatement(s);
                foreach (var s in i.ElseStatements) AnalyzeStatement(s);
                break;
            case FunctionBlockCallStatement call:
                AnalyzeFunctionBlockCall(call);
                break;
        }
    }

    private void AnalyzeFunctionBlockCall(FunctionBlockCallStatement call)
    {
        if (!_symbols.TryGetValue(call.InstanceName, out var type))
        {
            _diagnostics.Add(new("PLCF1001", $"Undefined function block instance '{call.InstanceName}'."));
            return;
        }
        if (type != PlcType.Ton)
        {
            _diagnostics.Add(new("PLCF1014", $"'{call.InstanceName}' is not a TON function block."));
            return;
        }
        if (!call.Arguments.TryGetValue("IN", out var input)) _diagnostics.Add(new("PLCF1015", $"TON '{call.InstanceName}' requires IN argument."));
        else if (TypeOf(input) != PlcType.Bool) _diagnostics.Add(new("PLCF1016", $"TON '{call.InstanceName}' IN must be BOOL."));

        if (!call.Arguments.TryGetValue("PT", out var preset)) _diagnostics.Add(new("PLCF1015", $"TON '{call.InstanceName}' requires PT argument."));
        else if (preset is not TimeLiteralExpression) _diagnostics.Add(new("PLCF1017", $"TON '{call.InstanceName}' PT must be a TIME literal such as T#2s."));
    }

    private PlcType? TypeOf(Expression expression) => expression switch
    {
        IntegerLiteralExpression => PlcType.Int,
        TimeLiteralExpression => null,
        BooleanLiteralExpression => PlcType.Bool,
        IdentifierExpression id => _symbols.TryGetValue(id.Name, out var t) ? t : Undefined(id.Name),
        MemberAccessExpression member => TypeOfMember(member),
        UnaryExpression u => TypeOfUnary(u),
        BinaryExpression b => TypeOfBinary(b),
        _ => null
    };

    private PlcType? TypeOfMember(MemberAccessExpression member)
    {
        if (!_symbols.TryGetValue(member.InstanceName, out var type)) return Undefined(member.InstanceName);
        if (type != PlcType.Ton)
        {
            _diagnostics.Add(new("PLCF1018", $"'{member.InstanceName}' has no member '{member.MemberName}'."));
            return null;
        }
        return member.MemberName.ToUpperInvariant() switch
        {
            "Q" => PlcType.Bool,
            "ET" => PlcType.Int,
            _ => UnknownMember(member)
        };
    }

    private PlcType? UnknownMember(MemberAccessExpression member)
    {
        _diagnostics.Add(new("PLCF1018", $"TON '{member.InstanceName}' has no member '{member.MemberName}'."));
        return null;
    }

    private PlcType? Undefined(string name) { _diagnostics.Add(new("PLCF1001", $"Undefined symbol '{name}'.")); return null; }
    private PlcType? TypeOfUnary(UnaryExpression u)
    {
        var t = TypeOf(u.Operand);
        if (u.Operator == "NOT" && t != PlcType.Bool) _diagnostics.Add(new("PLCF1005", "NOT requires BOOL operand."));
        if (u.Operator == "-" && t != PlcType.Int) _diagnostics.Add(new("PLCF1005", "Unary '-' requires INT operand."));
        return u.Operator == "NOT" ? PlcType.Bool : PlcType.Int;
    }
    private PlcType? TypeOfBinary(BinaryExpression b)
    {
        var left = TypeOf(b.Left); var right = TypeOf(b.Right);
        if (left is null || right is null) return null;
        if (new[] { "+", "-", "*", "/" }.Contains(b.Operator))
        {
            if (left != PlcType.Int || right != PlcType.Int) _diagnostics.Add(new("PLCF1006", $"Operator '{b.Operator}' requires INT operands."));
            return PlcType.Int;
        }
        if (new[] { "AND", "OR" }.Contains(b.Operator))
        {
            if (left != PlcType.Bool || right != PlcType.Bool) _diagnostics.Add(new("PLCF1006", $"Operator '{b.Operator}' requires BOOL operands."));
            return PlcType.Bool;
        }
        if (left != right) _diagnostics.Add(new("PLCF1007", $"Cannot compare {left} and {right}."));
        return PlcType.Bool;
    }

    private static bool IsValidAddress(string address)
    {
        if (!(address.StartsWith("%I", StringComparison.OrdinalIgnoreCase) || address.StartsWith("%Q", StringComparison.OrdinalIgnoreCase))) return false;
        var tail = address[2..];
        var parts = tail.Split('.');
        return parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out var bit) && bit is >= 0 and <= 7;
    }
}
