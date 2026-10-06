namespace PLCFlow.Compiler.Lexing;

public sealed class Lexer
{
    private readonly string _text;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROGRAM"] = TokenKind.Program, ["END_PROGRAM"] = TokenKind.EndProgram,
        ["VAR"] = TokenKind.Var, ["END_VAR"] = TokenKind.EndVar, ["AT"] = TokenKind.At,
        ["IF"] = TokenKind.If, ["THEN"] = TokenKind.Then, ["ELSE"] = TokenKind.Else, ["END_IF"] = TokenKind.EndIf,
        ["BOOL"] = TokenKind.Bool, ["INT"] = TokenKind.Int, ["TON"] = TokenKind.Ton,
        ["TRUE"] = TokenKind.True, ["FALSE"] = TokenKind.False,
        ["AND"] = TokenKind.And, ["OR"] = TokenKind.Or, ["NOT"] = TokenKind.Not
    };

    public Lexer(string text) => _text = text ?? string.Empty;

    public IReadOnlyList<Token> Lex()
    {
        var tokens = new List<Token>();
        while (_position < _text.Length)
        {
            if (char.IsWhiteSpace(Current)) { ConsumeWhitespace(); continue; }
            if (Current == '(' && Peek(1) == '*') { ConsumeComment(); continue; }

            var start = _position; var line = _line; var col = _column;
            if (Current == '%')
            {
                var address = ReadWhile(c => char.IsLetterOrDigit(c) || c is '%' or '.');
                tokens.Add(new Token(TokenKind.DirectAddress, address, start, line, col));
                continue;
            }
            if ((Current == 'T' || Current == 't') && Peek(1) == '#')
            {
                Advance(2);
                while (_position < _text.Length && (char.IsLetterOrDigit(Current) || Current == '.')) Advance();
                tokens.Add(new Token(TokenKind.TimeLiteral, _text[start.._position], start, line, col));
                continue;
            }
            if (char.IsLetter(Current) || Current == '_')
            {
                var word = ReadWhile(c => char.IsLetterOrDigit(c) || c == '_');
                tokens.Add(new Token(Keywords.GetValueOrDefault(word, TokenKind.Identifier), word, start, line, col));
                continue;
            }
            if (char.IsDigit(Current))
            {
                var number = ReadWhile(char.IsDigit);
                tokens.Add(new Token(TokenKind.Number, number, start, line, col));
                continue;
            }

            TokenKind? kind = Current switch
            {
                ':' when Peek(1) == '=' => TokenKind.Assign,
                '<' when Peek(1) == '>' => TokenKind.NotEqual,
                '<' when Peek(1) == '=' => TokenKind.LessEqual,
                '>' when Peek(1) == '=' => TokenKind.GreaterEqual,
                ':' => TokenKind.Colon, ';' => TokenKind.Semicolon, ',' => TokenKind.Comma, '.' => TokenKind.Dot,
                '+' => TokenKind.Plus, '-' => TokenKind.Minus, '*' => TokenKind.Star, '/' => TokenKind.Slash,
                '=' => TokenKind.Equal, '<' => TokenKind.Less, '>' => TokenKind.Greater,
                '(' => TokenKind.LeftParen, ')' => TokenKind.RightParen,
                _ => null
            };
            if (kind is null) throw new CompilerException($"Unexpected character '{Current}'", line, col);
            var width = (kind is TokenKind.Assign or TokenKind.NotEqual or TokenKind.LessEqual or TokenKind.GreaterEqual) ? 2 : 1;
            var text = _text.Substring(_position, width);
            Advance(width);
            tokens.Add(new Token(kind.Value, text, start, line, col));
        }
        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, _position, _line, _column));
        return tokens;
    }

    private char Current => _text[_position];
    private char Peek(int offset) => _position + offset < _text.Length ? _text[_position + offset] : '\0';
    private void Advance(int count = 1) { for (var i = 0; i < count; i++) { if (_position >= _text.Length) return; if (_text[_position] == '\n') { _line++; _column = 1; } else _column++; _position++; } }
    private void ConsumeWhitespace() { while (_position < _text.Length && char.IsWhiteSpace(Current)) Advance(); }
    private string ReadWhile(Func<char, bool> predicate) { var start = _position; while (_position < _text.Length && predicate(Current)) Advance(); return _text[start.._position]; }
    private void ConsumeComment() { Advance(2); while (_position < _text.Length && !(Current == '*' && Peek(1) == ')')) Advance(); if (_position >= _text.Length) throw new CompilerException("Unterminated comment", _line, _column); Advance(2); }
}

public sealed class CompilerException : Exception
{
    public int Line { get; }
    public int Column { get; }
    public CompilerException(string message, int line, int column) : base($"{message} at {line}:{column}") { Line = line; Column = column; }
}
