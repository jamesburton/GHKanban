namespace GHKanban.Rules;

/// <summary>
/// Recursive-descent parser for the rule grammar.
/// Grammar (precedence low to high): or, and, not, primary.
/// </summary>
public static class RuleParser
{
    /// <summary>Parses <paramref name="input"/> into a <see cref="RuleNode"/> AST.</summary>
    /// <param name="input">The rule expression string.</param>
    /// <returns>The root AST node.</returns>
    /// <exception cref="RuleException">Thrown when the input is syntactically invalid.</exception>
    public static RuleNode Parse(string input)
    {
        var tokens = Tokenize(input);
        var p = new Parser(tokens);
        var ast = p.ParseOr();
        p.Expect(TokenType.End);
        return ast;
    }

    private enum TokenType { Identifier, String, Integer, LParen, RParen, EqualsEquals, Greater, Less, And, Or, Not, End }

    private sealed record Token(TokenType Type, string Text, int Position);

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            if (char.IsWhiteSpace(input[i])) { i++; continue; }

            int start = i;

            if (input[i] == '(') { tokens.Add(new(TokenType.LParen, "(", start)); i++; continue; }
            if (input[i] == ')') { tokens.Add(new(TokenType.RParen, ")", start)); i++; continue; }
            if (input[i] == '>') { tokens.Add(new(TokenType.Greater, ">", start)); i++; continue; }
            if (input[i] == '<') { tokens.Add(new(TokenType.Less, "<", start)); i++; continue; }

            if (i + 1 < input.Length && input[i] == '=' && input[i + 1] == '=')
            {
                tokens.Add(new(TokenType.EqualsEquals, "==", start));
                i += 2;
                continue;
            }

            if (input[i] == '"')
            {
                int j = i + 1;
                while (j < input.Length && input[j] != '"') j++;
                if (j == input.Length) throw new RuleException("unterminated string literal", start);
                tokens.Add(new(TokenType.String, input.Substring(i + 1, j - i - 1), start));
                i = j + 1;
                continue;
            }

            if (char.IsDigit(input[i]))
            {
                int j = i;
                while (j < input.Length && char.IsDigit(input[j])) j++;
                tokens.Add(new(TokenType.Integer, input.Substring(i, j - i), start));
                i = j;
                continue;
            }

            if (char.IsLetter(input[i]))
            {
                int j = i;
                while (j < input.Length && (char.IsLetterOrDigit(input[j]) || input[j] == '-')) j++;
                var word = input.Substring(i, j - i);
                var type = word switch
                {
                    "and" => TokenType.And,
                    "or"  => TokenType.Or,
                    "not" => TokenType.Not,
                    _     => TokenType.Identifier
                };
                tokens.Add(new(type, word, start));
                i = j;
                continue;
            }

            throw new RuleException($"unexpected character '{input[i]}'", i);
        }

        tokens.Add(new(TokenType.End, "", input.Length));
        return tokens;
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _pos;

        private Token Peek() => tokens[_pos];
        private Token Consume() => tokens[_pos++];

        public void Expect(TokenType t)
        {
            var tok = Peek();
            if (tok.Type != t) throw new RuleException($"expected {t}, got '{tok.Text}'", tok.Position);
            Consume();
        }

        public RuleNode ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Type == TokenType.Or)
            {
                Consume();
                var right = ParseAnd();
                left = new OrNode(left, right);
            }

            return left;
        }

        public RuleNode ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Type == TokenType.And)
            {
                Consume();
                var right = ParseNot();
                left = new AndNode(left, right);
            }

            return left;
        }

        public RuleNode ParseNot()
        {
            if (Peek().Type == TokenType.Not) { Consume(); return new NotNode(ParseNot()); }
            return ParsePrimary();
        }

        public RuleNode ParsePrimary()
        {
            var tok = Peek();

            if (tok.Type == TokenType.LParen)
            {
                Consume();
                var inner = ParseOr();
                Expect(TokenType.RParen);
                return inner;
            }

            if (tok.Type == TokenType.Identifier)
            {
                return tok.Text switch
                {
                    "has-label"       => ParseHasLabel(),
                    "assignee"        => ParseAssigneeEquals(),
                    "assignee-of-mine" => ParseAssigneeOfMine(),
                    "state"           => ParseStateEquals(),
                    "age-days"        => ParseAgeDays(),
                    "milestone"       => ParseMilestoneEquals(),
                    "repo"            => ParseRepoEquals(),
                    _ => throw new RuleException($"unknown identifier '{tok.Text}'", tok.Position)
                };
            }

            // Covers `and`/`or`/`not` appearing where a primary is expected
            throw new RuleException($"unexpected '{tok.Text}'", tok.Position);
        }

        private HasLabelNode ParseHasLabel()
        {
            Consume();
            var lp = Peek();
            if (lp.Type != TokenType.LParen) throw new RuleException("expected '('", lp.Position);
            Consume();
            var s = Peek();
            if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position);
            Consume();
            Expect(TokenType.RParen);
            return new HasLabelNode(s.Text);
        }

        private AssigneeEqualsNode ParseAssigneeEquals()
        {
            Consume();
            Expect(TokenType.EqualsEquals);
            var s = Peek();
            if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position);
            Consume();
            return new AssigneeEqualsNode(s.Text);
        }

        private AssigneeOfMineNode ParseAssigneeOfMine()
        {
            Consume();
            return new AssigneeOfMineNode();
        }

        private StateEqualsNode ParseStateEquals()
        {
            Consume();
            Expect(TokenType.EqualsEquals);
            var s = Peek();
            if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position);
            Consume();
            return new StateEqualsNode(s.Text);
        }

        private RuleNode ParseAgeDays()
        {
            Consume();
            var op = Peek();
            if (op.Type == TokenType.Greater)
            {
                Consume();
                var n = Peek();
                if (n.Type != TokenType.Integer) throw new RuleException("expected integer", n.Position);
                Consume();
                return new AgeDaysGreaterNode(int.Parse(n.Text));
            }

            if (op.Type == TokenType.Less)
            {
                Consume();
                var n = Peek();
                if (n.Type != TokenType.Integer) throw new RuleException("expected integer", n.Position);
                Consume();
                return new AgeDaysLessNode(int.Parse(n.Text));
            }

            throw new RuleException("expected '>' or '<'", op.Position);
        }

        private MilestoneEqualsNode ParseMilestoneEquals()
        {
            Consume();
            Expect(TokenType.EqualsEquals);
            var s = Peek();
            if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position);
            Consume();
            return new MilestoneEqualsNode(s.Text);
        }

        private RepoEqualsNode ParseRepoEquals()
        {
            Consume();
            Expect(TokenType.EqualsEquals);
            var s = Peek();
            if (s.Type != TokenType.String) throw new RuleException("expected string", s.Position);
            Consume();
            return new RepoEqualsNode(s.Text);
        }
    }
}
