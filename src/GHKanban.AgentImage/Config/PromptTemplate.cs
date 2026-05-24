using System.Text.RegularExpressions;

namespace GHKanban.AgentImage.Config;

public static class PromptTemplate
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([a-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        return TokenRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return tokens.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}
