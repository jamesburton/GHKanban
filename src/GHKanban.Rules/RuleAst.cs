namespace GHKanban.Rules;

public abstract record RuleNode;

public sealed record HasLabelNode(string Label) : RuleNode;
public sealed record AssigneeEqualsNode(string Username) : RuleNode;
public sealed record AssigneeOfMineNode : RuleNode;
public sealed record StateEqualsNode(string State) : RuleNode;
public sealed record AgeDaysGreaterNode(int Days) : RuleNode;
public sealed record AgeDaysLessNode(int Days) : RuleNode;
public sealed record MilestoneEqualsNode(string Milestone) : RuleNode;
public sealed record RepoEqualsNode(string Repo) : RuleNode;

public sealed record AndNode(RuleNode Left, RuleNode Right) : RuleNode;
public sealed record OrNode(RuleNode Left, RuleNode Right) : RuleNode;
public sealed record NotNode(RuleNode Inner) : RuleNode;
