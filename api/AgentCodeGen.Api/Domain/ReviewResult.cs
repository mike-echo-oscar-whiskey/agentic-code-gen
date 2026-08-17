namespace AgentCodeGen.Api.Domain;

public sealed record ReviewResult(
    string Verdict,
    IReadOnlyList<ReviewFinding> Findings);
