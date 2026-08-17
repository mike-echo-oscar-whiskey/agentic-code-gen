namespace AgentCodeGen.Api.Contracts;

public sealed record RunSnapshotResponse(
    Guid Id,
    string Goal,
    string Status,
    IReadOnlyList<AgentEventResponse> Events,
    CodeArtifactResponse? Code,
    ReviewResponse? Review,
    IReadOnlyList<GateResultResponse> Gates,
    IReadOnlyList<RunIterationResponse> History);
