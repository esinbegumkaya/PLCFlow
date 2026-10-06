namespace PLCFlow.Compiler.Lexing;

public enum TokenKind
{
    EndOfFile, Identifier, Number, TimeLiteral, DirectAddress, True, False,
    Program, EndProgram, Var, EndVar, If, Then, Else, EndIf, At,
    Bool, Int, Ton, And, Or, Not,
    Colon, Semicolon, Comma, Dot, Assign, Plus, Minus, Star, Slash,
    Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual,
    LeftParen, RightParen
}

public sealed record Token(TokenKind Kind, string Text, int Position, int Line, int Column);
