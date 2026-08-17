namespace AgentCodeGen.Api.Contracts;

public sealed record ReviewResponse(
    string Verdict,
    IReadOnlyList<ReviewFindingResponse> Findings);
