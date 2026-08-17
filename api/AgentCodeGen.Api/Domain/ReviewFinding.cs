namespace AgentCodeGen.Api.Domain;

public sealed record ReviewFinding(
    ReviewSeverity Severity,
    string Issue,
    string SuggestedChange);
