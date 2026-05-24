namespace GHKanban.Rules;

public sealed class RuleException : Exception
{
    public int Position { get; }

    public RuleException(string message, int position) : base($"{message} (at position {position})")
        => Position = position;
}
