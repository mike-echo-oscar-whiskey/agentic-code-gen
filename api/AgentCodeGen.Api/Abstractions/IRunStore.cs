using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

public interface IRunStore
{
    AgentRun Create(string goal);

    Option<AgentRun> Get(RunId id);

    void Publish(RunId id, AgentKind agent, AgentEventKind kind, string message);

    void SetCode(RunId id, CodeArtifact code);

    void SetReview(RunId id, ReviewResult review);

    void SetGates(RunId id, IReadOnlyList<GateResult> gates);

    /// Moves the current code + review into the history before a revision attempt.
    void ArchiveIteration(RunId id);

    void Finish(RunId id, RunStatus status);

    IAsyncEnumerable<AgentEvent> SubscribeAsync(RunId id, CancellationToken cancellationToken = default);
}
