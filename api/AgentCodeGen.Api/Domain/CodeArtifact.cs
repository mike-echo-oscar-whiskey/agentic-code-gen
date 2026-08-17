namespace AgentCodeGen.Api.Domain;

public sealed record CodeArtifact(
    string Language,
    string Code,
    IReadOnlyList<string> Dependencies,
    string Explanation,
    IReadOnlyList<string> Assumptions);
