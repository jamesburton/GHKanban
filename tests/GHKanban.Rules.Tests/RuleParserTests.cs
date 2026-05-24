using GHKanban.Rules;
using Xunit;

namespace GHKanban.Rules.Tests;

public class RuleParserTests
{
    [Theory]
    [InlineData("has-label(\"bug\")")]
    [InlineData("assignee == \"alice\"")]
    [InlineData("assignee-of-mine")]
    [InlineData("state == \"open\"")]
    [InlineData("state == \"closed\"")]
    [InlineData("age-days > 30")]
    [InlineData("age-days < 7")]
    [InlineData("milestone == \"v1\"")]
    [InlineData("repo == \"owner/repo\"")]
    public void Parses_each_predicate(string input)
    {
        var ast = RuleParser.Parse(input);
        Assert.NotNull(ast);
    }

    [Theory]
    [InlineData("has-label(\"a\") and has-label(\"b\")")]
    [InlineData("has-label(\"a\") or has-label(\"b\")")]
    [InlineData("not has-label(\"a\")")]
    [InlineData("(has-label(\"a\") or has-label(\"b\")) and not state == \"closed\"")]
    public void Parses_boolean_compositions(string input)
    {
        var ast = RuleParser.Parse(input);
        Assert.NotNull(ast);
    }

    [Theory]
    [InlineData("has-label", "expected '('")]
    [InlineData("has-label(\"unterminated", "unterminated string")]
    [InlineData("foo == \"x\"", "unknown identifier")]
    [InlineData("age-days >", "expected integer")]
    [InlineData("and has-label(\"a\")", "unexpected 'and'")]
    public void Rejects_invalid_input_with_diagnostic(string input, string expectedSubstring)
    {
        var ex = Assert.Throws<RuleException>(() => RuleParser.Parse(input));
        Assert.Contains(expectedSubstring, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
