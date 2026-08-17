namespace AgentCodeGen.Api.Contracts;

public sealed record CodeArtifactResponse(
    string Language,
    string Code,
    IReadOnlyList<string> Dependencies,
    string Explanation,
    IReadOnlyList<string> Assumptions);
