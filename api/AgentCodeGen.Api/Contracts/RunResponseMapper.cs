using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Contracts;

public static class RunResponseMapper
{
    public static RunSnapshotResponse ToResponse(this AgentRun run) =>
        new(
            run.Id.Value,
            run.Goal,
            Lower(run.Status),
            [.. run.Events.Select(ToResponse)],
            run.Code.Match(ToResponse, () => null),
            run.Review.Match(ToResponse, () => null),
            [.. run.Gates.Select(g => new GateResultResponse(g.Name, g.Passed, g.Detail))]);

    public static AgentEventResponse ToResponse(this AgentEvent agentEvent) =>
        new(
            agentEvent.Sequence,
            agentEvent.At,
            Lower(agentEvent.Agent),
            Lower(agentEvent.Kind),
            agentEvent.Message);

    private static CodeArtifactResponse? ToResponse(CodeArtifact code) =>
        new(code.Language, code.Code, code.Dependencies, code.Explanation, code.Assumptions);

    private static ReviewResponse? ToResponse(ReviewResult review) =>
        new(review.Verdict, [.. review.Findings.Select(f => new ReviewFindingResponse(Lower(f.Severity), f.Issue, f.SuggestedChange))]);

    private static string Lower<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString()!.ToLowerInvariant();
}
