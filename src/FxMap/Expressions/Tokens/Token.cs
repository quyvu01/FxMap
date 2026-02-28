namespace FxMap.Expressions.Tokens;

/// <summary>
/// Represents a single token in the FxMap expression language.
/// </summary>
/// <param name="Type">The type of the token.</param>
/// <param name="Value">The string value of the token.</param>
/// <param name="Position">The position in the original expression string.</param>
public readonly record struct Token(TokenType Type, string Value, int Position)
{
    public override string ToString() => $"{Type}({Value}) at {Position}";
}
