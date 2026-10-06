using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PLCFlow.Compiler;
using PLCFlow.Compiler.Lexing;
using PLCFlow.Compiler.Syntax;

namespace PLCFlow.LanguageServer;

internal static class Program
{
    private static readonly Dictionary<string, string> Documents = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Stream Input = Console.OpenStandardInput();
    private static readonly Stream Output = Console.OpenStandardOutput();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task Main()
    {
        while (true)
        {
            var message = await ReadMessageAsync();
            if (message is null) break;
            var root = JsonNode.Parse(message)?.AsObject();
            if (root is null) continue;
            var method = root["method"]?.GetValue<string>();
            var id = root["id"];
            var parameters = root["params"] as JsonObject;

            switch (method)
            {
                case "initialize":
                    await RespondAsync(id, new
                    {
                        capabilities = new
                        {
                            textDocumentSync = 1,
                            hoverProvider = true,
                            definitionProvider = true,
                            completionProvider = new { resolveProvider = false, triggerCharacters = new[] { "." } }
                        },
                        serverInfo = new { name = "PLCFlow Language Server", version = "0.5.0" }
                    });
                    break;
                case "initialized":
                    break;
                case "textDocument/didOpen":
                {
                    var doc = parameters?["textDocument"]?.AsObject();
                    var uri = doc?["uri"]?.GetValue<string>();
                    var text = doc?["text"]?.GetValue<string>();
                    if (uri is not null && text is not null)
                    {
                        Documents[uri] = text;
                        await PublishDiagnosticsAsync(uri, text);
                    }
                    break;
                }
                case "textDocument/didChange":
                {
                    var uri = parameters?["textDocument"]?["uri"]?.GetValue<string>();
                    var changes = parameters?["contentChanges"]?.AsArray();
                    var text = changes?.LastOrDefault()?["text"]?.GetValue<string>();
                    if (uri is not null && text is not null)
                    {
                        Documents[uri] = text;
                        await PublishDiagnosticsAsync(uri, text);
                    }
                    break;
                }
                case "textDocument/didClose":
                {
                    var uri = parameters?["textDocument"]?["uri"]?.GetValue<string>();
                    if (uri is not null)
                    {
                        Documents.Remove(uri);
                        await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics = Array.Empty<object>() });
                    }
                    break;
                }
                case "textDocument/hover":
                    await RespondAsync(id, HandleHover(parameters));
                    break;
                case "textDocument/completion":
                    await RespondAsync(id, HandleCompletion(parameters));
                    break;
                case "textDocument/definition":
                    await RespondAsync(id, HandleDefinition(parameters));
                    break;
                case "shutdown":
                    await RespondAsync(id, null);
                    break;
                case "exit":
                    return;
                default:
                    if (id is not null) await RespondAsync(id, null);
                    break;
            }
        }
    }

    private static object? HandleHover(JsonObject? parameters)
    {
        if (!TryContext(parameters, out var uri, out var text, out var line, out var character)) return null;
        var word = WordAt(text, line, character);
        if (string.IsNullOrWhiteSpace(word)) return null;
        var result = CompilerPipeline.Compile(text);
        if (result.Program is null) return null;

        var declaration = result.Program.Variables.FirstOrDefault(v => v.Name.Equals(word, StringComparison.OrdinalIgnoreCase));
        if (declaration is not null)
        {
            var address = declaration.Address is null ? string.Empty : $" at `{declaration.Address}`";
            var role = declaration.IsInput ? "Input" : declaration.IsOutput ? "Output" : declaration.Type == PlcType.Ton ? "Function block" : "Variable";
            return new { contents = new { kind = "markdown", value = $"**{declaration.Name}** : `{TypeName(declaration.Type)}`{address}\n\n{role} in PROGRAM `{result.Program.Name}`" } };
        }

        var member = MemberAt(text, line, character);
        if (member is not null)
        {
            var fb = result.Program.Variables.FirstOrDefault(v => v.Name.Equals(member.Value.instance, StringComparison.OrdinalIgnoreCase) && v.Type == PlcType.Ton);
            if (fb is not null && member.Value.member.Equals("Q", StringComparison.OrdinalIgnoreCase))
                return new { contents = new { kind = "markdown", value = $"**{fb.Name}.Q** : `BOOL`\n\nTON done output." } };
            if (fb is not null && member.Value.member.Equals("ET", StringComparison.OrdinalIgnoreCase))
                return new { contents = new { kind = "markdown", value = $"**{fb.Name}.ET** : `INT`\n\nTON elapsed time in milliseconds." } };
        }

        return null;
    }

    private static object HandleCompletion(JsonObject? parameters)
    {
        if (!TryContext(parameters, out _, out var text, out var line, out var character))
            return Array.Empty<object>();

        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (line < 0 || line >= lines.Length) return Array.Empty<object>();
        var currentLine = lines[line];
        var safeCharacter = Math.Clamp(character, 0, currentLine.Length);
        var beforeCursor = currentLine[..safeCharacter];

        var memberMatch = Regex.Match(beforeCursor, @"([A-Za-z_][A-Za-z0-9_]*)\.\s*$");
        if (memberMatch.Success)
        {
            var variableName = memberMatch.Groups[1].Value;
            var tonDeclarationPattern = $@"(?im)^\s*{Regex.Escape(variableName)}\s*:\s*TON\s*;";
            if (Regex.IsMatch(text, tonDeclarationPattern))
            {
                return new object[]
                {
                    new { label = "Q", kind = 5, detail = "BOOL — Timer output", insertText = "Q" },
                    new { label = "ET", kind = 5, detail = "TIME — Elapsed time", insertText = "ET" }
                };
            }
        }

        return new object[]
        {
            new { label = "PROGRAM", kind = 14, detail = "Structured Text keyword" },
            new { label = "VAR", kind = 14, detail = "Variable declaration block" },
            new { label = "END_VAR", kind = 14, detail = "End variable declaration block" },
            new { label = "IF", kind = 14, detail = "Conditional statement" },
            new { label = "THEN", kind = 14, detail = "Conditional keyword" },
            new { label = "ELSE", kind = 14, detail = "Conditional keyword" },
            new { label = "END_IF", kind = 14, detail = "End conditional statement" },
            new { label = "BOOL", kind = 14, detail = "Boolean data type" },
            new { label = "INT", kind = 14, detail = "Integer data type" },
            new { label = "TON", kind = 14, detail = "On-delay timer function block" }
        };
    }

    private static object? HandleDefinition(JsonObject? parameters)
    {
        if (!TryContext(parameters, out var uri, out var text, out var line, out var character))
            return null;

        var word = WordAt(text, line, character);
        if (string.IsNullOrWhiteSpace(word)) return null;

        var pattern =
            $@"(?im)^\s*{Regex.Escape(word)}\s*" +
            $@"(?:AT\s+%[IQ]\d+\.\d+\s*)?" +
            $@":\s*(?:BOOL|INT|TON)\s*;";

        var match = Regex.Match(text, pattern);
        if (!match.Success) return null;

        var relativeIndex = match.Value.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        if (relativeIndex < 0) return null;
        var absoluteIndex = match.Index + relativeIndex;
        var (declLine, declChar) = OffsetToPosition(text, absoluteIndex);

        return new
        {
            uri,
            range = new
            {
                start = new { line = declLine, character = declChar },
                end = new { line = declLine, character = declChar + word.Length }
            }
        };
    }

    private static async Task PublishDiagnosticsAsync(string uri, string text)
    {
        var diagnostics = new List<object>();
        var result = CompilerPipeline.Compile(text);
        if (result.Error is CompilerException compilerError)
        {
            diagnostics.Add(new
            {
                range = Range(Math.Max(0, compilerError.Line - 1), Math.Max(0, compilerError.Column - 1), Math.Max(0, compilerError.Line - 1), Math.Max(1, compilerError.Column)),
                severity = 1,
                code = "PLCF0001",
                source = "PLCFlow",
                message = compilerError.Message
            });
        }
        else if (result.Error is not null)
        {
            diagnostics.Add(new { range = Range(0, 0, 0, 1), severity = 1, code = "PLCF0001", source = "PLCFlow", message = result.Error.Message });
        }
        else if (result.Semantics is not null)
        {
            foreach (var diagnostic in result.Semantics.Diagnostics)
            {
                var symbol = ExtractQuotedSymbol(diagnostic.Message);
                var location = symbol is null ? (0, 0, 1) : FindSymbol(text, symbol);
                diagnostics.Add(new
                {
                    range = Range(location.Item1, location.Item2, location.Item1, location.Item2 + Math.Max(1, location.Item3)),
                    severity = 1,
                    code = diagnostic.Code,
                    source = "PLCFlow",
                    message = diagnostic.Message
                });
            }
        }
        await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics });
    }

    private static string? ExtractQuotedSymbol(string message)
    {
        var m = Regex.Match(message, "'([^']+)'");
        return m.Success ? m.Groups[1].Value.Split('.')[0] : null;
    }

    private static (int, int, int) FindSymbol(string text, string symbol)
    {
        var m = Regex.Match(text, $@"\b{Regex.Escape(symbol)}\b", RegexOptions.IgnoreCase);
        if (!m.Success) return (0, 0, 1);
        var p = OffsetToPosition(text, m.Index);
        return (p.line, p.character, symbol.Length);
    }

    private static bool TryContext(JsonObject? p, out string uri, out string text, out int line, out int character)
    {
        uri = p?["textDocument"]?["uri"]?.GetValue<string>() ?? string.Empty;
        line = p?["position"]?["line"]?.GetValue<int>() ?? 0;
        character = p?["position"]?["character"]?.GetValue<int>() ?? 0;
        if (!Documents.TryGetValue(uri, out var source)) { text = string.Empty; return false; }
        text = source;
        return true;
    }

    private static string WordAt(string text, int line, int character)
    {
        var value = GetLine(text, line);
        if (value.Length == 0) return string.Empty;
        var i = Math.Clamp(character, 0, value.Length);
        var start = i;
        while (start > 0 && (char.IsLetterOrDigit(value[start - 1]) || value[start - 1] == '_')) start--;
        var end = i;
        while (end < value.Length && (char.IsLetterOrDigit(value[end]) || value[end] == '_')) end++;
        return value[start..end];
    }

    private static (string instance, string member)? MemberAt(string text, int line, int character)
    {
        var value = GetLine(text, line);
        foreach (Match m in Regex.Matches(value, @"([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)"))
            if (character >= m.Index && character <= m.Index + m.Length) return (m.Groups[1].Value, m.Groups[2].Value);
        return null;
    }

    private static string LinePrefix(string text, int line, int character)
    {
        var value = GetLine(text, line);
        return value[..Math.Clamp(character, 0, value.Length)];
    }

    private static string GetLine(string text, int line)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return line >= 0 && line < lines.Length ? lines[line] : string.Empty;
    }

    private static (int line, int character) OffsetToPosition(string text, int offset)
    {
        var line = 0; var character = 0;
        for (var i = 0; i < Math.Min(offset, text.Length); i++)
        {
            if (text[i] == '\n') { line++; character = 0; }
            else if (text[i] != '\r') character++;
        }
        return (line, character);
    }

    private static object Range(int sl, int sc, int el, int ec) => new
    {
        start = new { line = sl, character = sc },
        end = new { line = el, character = ec }
    };

    private static string TypeName(PlcType type) => type switch { PlcType.Bool => "BOOL", PlcType.Int => "INT", PlcType.Ton => "TON", _ => type.ToString().ToUpperInvariant() };

    private static async Task<string?> ReadMessageAsync()
    {
        var header = new StringBuilder();
        var state = 0;
        while (true)
        {
            var b = Input.ReadByte();
            if (b < 0) return null;
            var c = (char)b;
            header.Append(c);
            state = (state, c) switch
            {
                (0, '\r') => 1,
                (1, '\n') => 2,
                (2, '\r') => 3,
                (3, '\n') => 4,
                (_, '\r') => 1,
                _ => 0
            };
            if (state == 4) break;
        }
        var match = Regex.Match(header.ToString(), @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var length = int.Parse(match.Groups[1].Value);
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await Input.ReadAsync(buffer.AsMemory(read, length - read));
            if (n == 0) return null;
            read += n;
        }
        return Encoding.UTF8.GetString(buffer);
    }

    private static Task RespondAsync(JsonNode? id, object? result) => WriteAsync(new { jsonrpc = "2.0", id, result });
    private static Task NotifyAsync(string method, object @params) => WriteAsync(new { jsonrpc = "2.0", method, @params });

    private static async Task WriteAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await Output.WriteAsync(header);
        await Output.WriteAsync(bytes);
        await Output.FlushAsync();
    }
}
