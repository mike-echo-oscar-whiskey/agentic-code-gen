namespace AgentCodeGen.Api.Contracts;

public sealed record ReviewFindingResponse(
    string Severity,
    string Issue,
    string SuggestedChange);
