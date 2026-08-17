using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Agents;

public sealed class AgentWorkflow(
    IRunStore store,
    ICodingAgent codingAgent,
    IReviewAgent reviewAgent,
    IEnumerable<ICodeGate> gates) : IAgentWorkflow
{
    /// One revision round: generate → review → (revise → re-review). Bounded so a
    /// reviewer that never approves cannot loop the run (and the tokens) forever.
    public const int MaxRevisions = 1;

    private const string ChangesRequestedVerdict = "changes-requested";

    public async Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default)
    {
        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Started, $"Received goal: {goal}");

        store.Publish(runId, AgentKind.Coding, AgentEventKind.Started, "Generating code against the Met Museum Collection API");
        var code = await AcceptCode(runId, await codingAgent.GenerateAsync(goal, cancellationToken));
        if (code is null)
        {
            Fail(runId);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            await RunGatesAsync(runId, code, cancellationToken);

            store.Publish(runId, AgentKind.Review, AgentEventKind.Started, "Reviewing the generated code against the goal");
            var review = AcceptReview(runId, await reviewAgent.ReviewAsync(goal, code, cancellationToken));
            if (review is null)
            {
                Fail(runId);
                return;
            }

            if (review.Verdict != ChangesRequestedVerdict || attempt > MaxRevisions)
            {
                break;
            }

            store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Progress,
                $"Review requested changes — asking the Coding Agent for a revision (round {attempt + 1})");
            store.ArchiveIteration(runId);

            store.Publish(runId, AgentKind.Coding, AgentEventKind.Started, "Revising the code to address the review findings");
            code = await AcceptCode(runId, await codingAgent.ReviseAsync(goal, code, review, cancellationToken));
            if (code is null)
            {
                Fail(runId);
                return;
            }
        }

        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Completed, "Run completed");
        store.Finish(runId, RunStatus.Completed);
    }

    private Task<CodeArtifact?> AcceptCode(RunId runId, Either<AgentError, CodeArtifact> result) =>
        Task.FromResult(result.Match(
            error =>
            {
                store.Publish(runId, AgentKind.Coding, AgentEventKind.Failed, error.Message);
                return (CodeArtifact?)null;
            },
            artifact =>
            {
                store.SetCode(runId, artifact);
                store.Publish(runId, AgentKind.Coding, AgentEventKind.Completed, $"Produced a {artifact.Language} module");
                return artifact;
            }));

    private ReviewResult? AcceptReview(RunId runId, Either<AgentError, ReviewResult> result) =>
        result.Match(
            error =>
            {
                store.Publish(runId, AgentKind.Review, AgentEventKind.Failed, error.Message);
                return (ReviewResult?)null;
            },
            review =>
            {
                store.SetReview(runId, review);
                store.Publish(runId, AgentKind.Review, AgentEventKind.Completed,
                    $"Review verdict: {review.Verdict} with {review.Findings.Count} finding(s)");
                return review;
            });

    // Deterministic checks on the artifact: banned constructs, secret-shaped
    // literals, host allowlist, and dependency resolution against the real npm
    // registry. Informational — the Review Agent and the user see the results.
    private async Task RunGatesAsync(RunId runId, CodeArtifact code, CancellationToken cancellationToken)
    {
        var gateList = gates.ToList();
        if (gateList.Count == 0)
        {
            return;
        }

        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Progress, "Running validation gates on the generated code");

        var results = new List<GateResult>();
        foreach (var gate in gateList)
        {
            results.Add(await gate.CheckAsync(code, cancellationToken));
        }

        store.SetGates(runId, results);

        var failed = results.Count(r => !r.Passed);
        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Progress,
            failed == 0
                ? $"All {results.Count} validation gates passed"
                : $"{failed} of {results.Count} validation gates failed");
    }

    private void Fail(RunId runId)
    {
        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Failed, "Run failed");
        store.Finish(runId, RunStatus.Failed);
    }
}
