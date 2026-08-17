using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Domain;

public sealed record AgentRun(
    RunId Id,
    string Goal,
    RunStatus Status,
    IReadOnlyList<AgentEvent> Events,
    Option<CodeArtifact> Code,
    Option<ReviewResult> Review,
    IReadOnlyList<GateResult> Gates);
