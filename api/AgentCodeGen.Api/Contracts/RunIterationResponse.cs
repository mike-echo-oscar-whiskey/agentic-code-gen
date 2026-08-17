namespace AgentCodeGen.Api.Contracts;

public sealed record RunIterationResponse(
    int Number,
    CodeArtifactResponse Code,
    ReviewResponse Review);
