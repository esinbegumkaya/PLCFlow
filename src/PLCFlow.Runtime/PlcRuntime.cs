using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Runtime;

public sealed record TonState(bool In, bool Q, TimeSpan Et, TimeSpan Pt);

public sealed class PlcRuntime
{
    private readonly ProgramNode _program;
    private readonly Dictionary<string, object> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _outputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TonState> _timers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VariableDeclaration> _variables;

    public IReadOnlyDictionary<string, object> Memory => _memory;
    public IReadOnlyDictionary<string, bool> Inputs => _inputs;
    public IReadOnlyDictionary<string, bool> Outputs => _outputs;
    public IReadOnlyDictionary<string, TonState> Timers => _timers;
    public long CycleCount { get; private set; }
    public TimeSpan DefaultCycleTime { get; set; } = TimeSpan.FromMilliseconds(10);

    public PlcRuntime(ProgramNode program)
    {
        _program = program;
        _variables = program.Variables.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var v in program.Variables)
        {
            switch (v.Type)
            {
                case PlcType.Bool: _memory[v.Name] = false; break;
                case PlcType.Int: _memory[v.Name] = 0; break;
                case PlcType.Ton: _timers[v.Name] = new TonState(false, false, TimeSpan.Zero, TimeSpan.Zero); break;
            }
            if (v.IsInput && v.Address is not null) _inputs[v.Address] = false;
            if (v.IsOutput && v.Address is not null) _outputs[v.Address] = false;
        }
    }

    public void SetInput(string name, object value)
    {
        if (!_variables.TryGetValue(name, out var variable)) throw new ArgumentException($"Unknown variable '{name}'.");
        if (!variable.IsInput) throw new InvalidOperationException($"'{name}' is not mapped to a PLC input address.");
        if (variable.Address is null) throw new InvalidOperationException($"'{name}' has no input address.");
        SetInputAddress(variable.Address, Convert.ToBoolean(value));
    }

    public void SetInputAddress(string address, bool value)
    {
        if (!_inputs.ContainsKey(address)) throw new ArgumentException($"Unknown PLC input address '{address}'.");
        _inputs[address] = value;
    }

    public bool GetOutputAddress(string address)
    {
        if (!_outputs.TryGetValue(address, out var value)) throw new ArgumentException($"Unknown PLC output address '{address}'.");
        return value;
    }

    public void ScanCycle() => ScanCycle(DefaultCycleTime);

    public void ScanCycle(TimeSpan elapsed)
    {
        ReadProcessImageInputs();
        foreach (var statement in _program.Statements) Execute(statement, elapsed);
        WriteProcessImageOutputs();
        CycleCount++;
    }

    private void ReadProcessImageInputs()
    {
        foreach (var variable in _program.Variables.Where(v => v.IsInput && v.Address is not null))
            _memory[variable.Name] = _inputs[variable.Address!];
    }

    private void WriteProcessImageOutputs()
    {
        foreach (var variable in _program.Variables.Where(v => v.IsOutput && v.Address is not null))
            _outputs[variable.Address!] = Convert.ToBoolean(_memory[variable.Name]);
    }

    private void Execute(Statement statement, TimeSpan elapsed)
    {
        switch (statement)
        {
            case AssignmentStatement a: _memory[a.Name] = Evaluate(a.Value); break;
            case IfStatement i:
                var branch = Convert.ToBoolean(Evaluate(i.Condition)) ? i.ThenStatements : i.ElseStatements;
                foreach (var nested in branch) Execute(nested, elapsed);
                break;
            case FunctionBlockCallStatement call: ExecuteFunctionBlock(call, elapsed); break;
        }
    }

    private void ExecuteFunctionBlock(FunctionBlockCallStatement call, TimeSpan elapsed)
    {
        if (!_timers.TryGetValue(call.InstanceName, out var state)) throw new InvalidOperationException($"Unknown TON instance '{call.InstanceName}'.");
        var input = Convert.ToBoolean(Evaluate(call.Arguments["IN"]));
        var pt = call.Arguments["PT"] is TimeLiteralExpression time ? time.Value : throw new InvalidOperationException("TON PT must be TIME literal.");
        var et = input ? state.Et + elapsed : TimeSpan.Zero;
        if (et > pt) et = pt;
        var q = input && et >= pt;
        _timers[call.InstanceName] = new TonState(input, q, et, pt);
    }

    private object Evaluate(Expression expression) => expression switch
    {
        IntegerLiteralExpression n => n.Value,
        BooleanLiteralExpression b => b.Value,
        IdentifierExpression id => _memory[id.Name],
        MemberAccessExpression m => EvaluateMember(m),
        UnaryExpression u => u.Operator switch { "NOT" => !Convert.ToBoolean(Evaluate(u.Operand)), "-" => -Convert.ToInt32(Evaluate(u.Operand)), _ => throw new NotSupportedException(u.Operator) },
        BinaryExpression b => EvaluateBinary(b),
        _ => throw new NotSupportedException(expression.GetType().Name)
    };

    private object EvaluateMember(MemberAccessExpression member)
    {
        if (!_timers.TryGetValue(member.InstanceName, out var timer)) throw new InvalidOperationException($"Unknown timer '{member.InstanceName}'.");
        return member.MemberName.ToUpperInvariant() switch
        {
            "Q" => timer.Q,
            "ET" => (int)timer.Et.TotalMilliseconds,
            _ => throw new NotSupportedException($"TON member '{member.MemberName}'")
        };
    }

    private object EvaluateBinary(BinaryExpression b)
    {
        var l = Evaluate(b.Left); var r = Evaluate(b.Right);
        return b.Operator switch
        {
            "+" => Convert.ToInt32(l) + Convert.ToInt32(r), "-" => Convert.ToInt32(l) - Convert.ToInt32(r),
            "*" => Convert.ToInt32(l) * Convert.ToInt32(r), "/" => Convert.ToInt32(l) / Convert.ToInt32(r),
            "AND" => Convert.ToBoolean(l) && Convert.ToBoolean(r), "OR" => Convert.ToBoolean(l) || Convert.ToBoolean(r),
            "=" => Equals(l, r), "<>" => !Equals(l, r),
            "<" => Convert.ToInt32(l) < Convert.ToInt32(r), "<=" => Convert.ToInt32(l) <= Convert.ToInt32(r),
            ">" => Convert.ToInt32(l) > Convert.ToInt32(r), ">=" => Convert.ToInt32(l) >= Convert.ToInt32(r),
            _ => throw new NotSupportedException(b.Operator)
        };
    }
}
