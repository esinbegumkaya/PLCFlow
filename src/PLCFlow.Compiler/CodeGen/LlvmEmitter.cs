using System.Text;
using PLCFlow.Compiler.Syntax;

namespace PLCFlow.Compiler.CodeGen;

/// <summary>
/// Emits inspectable LLVM IR for the supported Structured Text subset.
/// The generated module is designed to target WebAssembly through clang/LLVM.
/// </summary>
public sealed class LlvmEmitter
{
    private readonly StringBuilder _body = new();
    private readonly Dictionary<string, VariableDeclaration> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private int _temp;
    private int _label;

    public string Emit(ProgramNode program)
    {
        _body.Clear();
        _symbols.Clear();
        _temp = 0;
        _label = 0;

        foreach (var variable in program.Variables)
            _symbols[variable.Name] = variable;

        var output = new StringBuilder();
        output.AppendLine($"; ModuleID = '{Escape(program.Name)}'");
        output.AppendLine($"source_filename = \"{Escape(program.Name)}.st\"");
        output.AppendLine("target triple = \"wasm32-unknown-unknown\"");
        output.AppendLine();
        output.AppendLine("@plc_cycle_ms = global i32 10");

        foreach (var variable in program.Variables)
        {
            if (variable.Type == PlcType.Ton)
            {
                output.AppendLine($"@{Name(variable.Name)}_IN = global i1 0");
                output.AppendLine($"@{Name(variable.Name)}_Q = global i1 0");
                output.AppendLine($"@{Name(variable.Name)}_ET = global i32 0");
                output.AppendLine($"@{Name(variable.Name)}_PT = global i32 0");
            }
            else
            {
                output.AppendLine($"@{Name(variable.Name)} = global {LlvmType(variable.Type)} {DefaultValue(variable.Type)}");
            }
        }

        output.AppendLine();
        output.AppendLine("define void @plc_set_cycle_ms(i32 %value) {");
        output.AppendLine("entry:");
        output.AppendLine("  store i32 %value, ptr @plc_cycle_ms");
        output.AppendLine("  ret void");
        output.AppendLine("}");
        output.AppendLine();
        output.AppendLine("define i32 @plc_get_cycle_ms() {");
        output.AppendLine("entry:");
        output.AppendLine("  %value = load i32, ptr @plc_cycle_ms");
        output.AppendLine("  ret i32 %value");
        output.AppendLine("}");
        output.AppendLine();

        foreach (var variable in program.Variables)
            EmitAccessors(output, variable);

        output.AppendLine("define void @scan_cycle() {");
        output.AppendLine("entry:");
        foreach (var statement in program.Statements)
            EmitStatement(statement);
        _body.AppendLine("  ret void");
        output.Append(_body);
        output.AppendLine("}");
        return output.ToString();
    }

    private void EmitAccessors(StringBuilder output, VariableDeclaration variable)
    {
        var safeName = Name(variable.Name);
        if (variable.Type == PlcType.Ton)
        {
            EmitBoolGetter(output, $"plc_get_{safeName}_Q", $"@{safeName}_Q");
            EmitIntGetter(output, $"plc_get_{safeName}_ET", $"@{safeName}_ET");
            EmitIntGetter(output, $"plc_get_{safeName}_PT", $"@{safeName}_PT");
            return;
        }

        if (variable.IsInput)
        {
            output.AppendLine($"define void @plc_set_{safeName}(i32 %value) {{");
            output.AppendLine("entry:");
            if (variable.Type == PlcType.Bool)
            {
                output.AppendLine("  %normalized = icmp ne i32 %value, 0");
                output.AppendLine($"  store i1 %normalized, ptr @{safeName}");
            }
            else
            {
                output.AppendLine($"  store i32 %value, ptr @{safeName}");
            }
            output.AppendLine("  ret void");
            output.AppendLine("}");
            output.AppendLine();
        }

        if (variable.Type == PlcType.Bool)
            EmitBoolGetter(output, $"plc_get_{safeName}", $"@{safeName}");
        else
            EmitIntGetter(output, $"plc_get_{safeName}", $"@{safeName}");
    }

    private static void EmitBoolGetter(StringBuilder output, string functionName, string globalName)
    {
        output.AppendLine($"define i32 @{functionName}() {{");
        output.AppendLine("entry:");
        output.AppendLine($"  %raw = load i1, ptr {globalName}");
        output.AppendLine("  %value = zext i1 %raw to i32");
        output.AppendLine("  ret i32 %value");
        output.AppendLine("}");
        output.AppendLine();
    }

    private static void EmitIntGetter(StringBuilder output, string functionName, string globalName)
    {
        output.AppendLine($"define i32 @{functionName}() {{");
        output.AppendLine("entry:");
        output.AppendLine($"  %value = load i32, ptr {globalName}");
        output.AppendLine("  ret i32 %value");
        output.AppendLine("}");
        output.AppendLine();
    }

    private void EmitStatement(Statement statement)
    {
        switch (statement)
        {
            case AssignmentStatement assignment:
            {
                var value = EmitExpression(assignment.Value);
                var target = _symbols[assignment.Name];
                _body.AppendLine($"  store {LlvmType(target.Type)} {value.Value}, ptr @{Name(assignment.Name)}");
                break;
            }
            case IfStatement conditional:
            {
                var condition = EmitExpression(conditional.Condition);
                var thenLabel = Label("if_then");
                var elseLabel = Label("if_else");
                var endLabel = Label("if_end");
                _body.AppendLine($"  br i1 {condition.Value}, label %{thenLabel}, label %{elseLabel}");
                _body.AppendLine($"{thenLabel}:");
                foreach (var nested in conditional.ThenStatements) EmitStatement(nested);
                _body.AppendLine($"  br label %{endLabel}");
                _body.AppendLine($"{elseLabel}:");
                foreach (var nested in conditional.ElseStatements) EmitStatement(nested);
                _body.AppendLine($"  br label %{endLabel}");
                _body.AppendLine($"{endLabel}:");
                break;
            }
            case FunctionBlockCallStatement call:
                EmitFunctionBlock(call);
                break;
        }
    }

    private void EmitFunctionBlock(FunctionBlockCallStatement call)
    {
        if (!_symbols.TryGetValue(call.InstanceName, out var declaration) || declaration.Type != PlcType.Ton)
            throw new NotSupportedException($"LLVM backend only supports TON function blocks; '{call.InstanceName}' is not TON.");

        if (!call.Arguments.TryGetValue("IN", out var inputExpression))
            throw new NotSupportedException($"TON '{call.InstanceName}' requires IN.");
        if (!call.Arguments.TryGetValue("PT", out var ptExpression) || ptExpression is not TimeLiteralExpression ptLiteral)
            throw new NotSupportedException($"TON '{call.InstanceName}' PT must be a TIME literal.");

        var timer = Name(call.InstanceName);
        var input = EmitExpression(inputExpression);
        var ptMs = checked((int)ptLiteral.Value.TotalMilliseconds);

        _body.AppendLine($"  store i1 {input.Value}, ptr @{timer}_IN");
        _body.AppendLine($"  store i32 {ptMs}, ptr @{timer}_PT");

        var oldEt = Temp();
        _body.AppendLine($"  {oldEt} = load i32, ptr @{timer}_ET");
        var cycleMs = Temp();
        _body.AppendLine($"  {cycleMs} = load i32, ptr @plc_cycle_ms");
        var sum = Temp();
        _body.AppendLine($"  {sum} = add i32 {oldEt}, {cycleMs}");
        var overPreset = Temp();
        _body.AppendLine($"  {overPreset} = icmp sgt i32 {sum}, {ptMs}");
        var clamped = Temp();
        _body.AppendLine($"  {clamped} = select i1 {overPreset}, i32 {ptMs}, i32 {sum}");
        var newEt = Temp();
        _body.AppendLine($"  {newEt} = select i1 {input.Value}, i32 {clamped}, i32 0");
        _body.AppendLine($"  store i32 {newEt}, ptr @{timer}_ET");
        var done = Temp();
        _body.AppendLine($"  {done} = icmp sge i32 {newEt}, {ptMs}");
        var q = Temp();
        _body.AppendLine($"  {q} = and i1 {input.Value}, {done}");
        _body.AppendLine($"  store i1 {q}, ptr @{timer}_Q");
    }

    private IrValue EmitExpression(Expression expression)
    {
        switch (expression)
        {
            case IntegerLiteralExpression number:
                return new IrValue(PlcType.Int, number.Value.ToString());
            case BooleanLiteralExpression boolean:
                return new IrValue(PlcType.Bool, boolean.Value ? "1" : "0");
            case MemberAccessExpression member:
                return EmitMember(member);
            case IdentifierExpression identifier:
            {
                var declaration = _symbols[identifier.Name];
                if (declaration.Type == PlcType.Ton)
                    throw new NotSupportedException($"TON instance '{identifier.Name}' cannot be used directly as a value.");
                var temp = Temp();
                _body.AppendLine($"  {temp} = load {LlvmType(declaration.Type)}, ptr @{Name(identifier.Name)}");
                return new IrValue(declaration.Type, temp);
            }
            case UnaryExpression unary:
            {
                var operand = EmitExpression(unary.Operand);
                var temp = Temp();
                if (unary.Operator == "NOT")
                    _body.AppendLine($"  {temp} = xor i1 {operand.Value}, true");
                else
                    _body.AppendLine($"  {temp} = sub i32 0, {operand.Value}");
                return new IrValue(unary.Operator == "NOT" ? PlcType.Bool : PlcType.Int, temp);
            }
            case BinaryExpression binary:
                return EmitBinary(binary);
            default:
                throw new NotSupportedException(expression.GetType().Name);
        }
    }

    private IrValue EmitMember(MemberAccessExpression member)
    {
        if (!_symbols.TryGetValue(member.InstanceName, out var declaration) || declaration.Type != PlcType.Ton)
            throw new NotSupportedException($"Member access '{member.InstanceName}.{member.MemberName}' is only supported for TON.");

        var timer = Name(member.InstanceName);
        if (member.MemberName.Equals("Q", StringComparison.OrdinalIgnoreCase))
        {
            var temp = Temp();
            _body.AppendLine($"  {temp} = load i1, ptr @{timer}_Q");
            return new IrValue(PlcType.Bool, temp);
        }
        if (member.MemberName.Equals("ET", StringComparison.OrdinalIgnoreCase))
        {
            var temp = Temp();
            _body.AppendLine($"  {temp} = load i32, ptr @{timer}_ET");
            return new IrValue(PlcType.Int, temp);
        }

        throw new NotSupportedException($"TON member '{member.MemberName}' is not supported by the LLVM backend.");
    }

    private IrValue EmitBinary(BinaryExpression binary)
    {
        var left = EmitExpression(binary.Left);
        var right = EmitExpression(binary.Right);
        var temp = Temp();
        var instruction = binary.Operator switch
        {
            "+" => "add i32",
            "-" => "sub i32",
            "*" => "mul i32",
            "/" => "sdiv i32",
            "AND" => "and i1",
            "OR" => "or i1",
            "=" => $"icmp eq {LlvmType(left.Type)}",
            "<>" => $"icmp ne {LlvmType(left.Type)}",
            "<" => "icmp slt i32",
            "<=" => "icmp sle i32",
            ">" => "icmp sgt i32",
            ">=" => "icmp sge i32",
            _ => throw new NotSupportedException(binary.Operator)
        };
        _body.AppendLine($"  {temp} = {instruction} {left.Value}, {right.Value}");
        var resultType = new[] { "+", "-", "*", "/" }.Contains(binary.Operator) ? PlcType.Int : PlcType.Bool;
        return new IrValue(resultType, temp);
    }

    private string Temp() => $"%t{++_temp}";
    private string Label(string prefix) => $"{prefix}_{++_label}";
    private static string LlvmType(PlcType type) => type == PlcType.Bool ? "i1" : "i32";
    private static string DefaultValue(PlcType type) => "0";
    private static string Name(string name) => name.Replace("-", "_");
    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private sealed record IrValue(PlcType Type, string Value);
}
