using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Agents;

public sealed class AgentWorkflow(
    IRunStore store,
    ICodingAgent codingAgent,
    IReviewAgent reviewAgent,
    IEnumerable<ICodeGate> gates) : IAgentWorkflow
{
    public async Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default)
    {
        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Started, $"Received goal: {goal}");

        store.Publish(runId, AgentKind.Coding, AgentEventKind.Started, "Generating code against the Met Museum Collection API");
        var generated = await codingAgent.GenerateAsync(goal, cancellationToken);

        var code = generated.Match(
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
            });

        if (code is null)
        {
            Fail(runId);
            return;
        }

        await RunGatesAsync(runId, code, cancellationToken);

        store.Publish(runId, AgentKind.Review, AgentEventKind.Started, "Reviewing the generated code against the goal");
        var reviewed = await reviewAgent.ReviewAsync(goal, code, cancellationToken);

        var succeeded = reviewed.Match(
            error =>
            {
                store.Publish(runId, AgentKind.Review, AgentEventKind.Failed, error.Message);
                return false;
            },
            review =>
            {
                store.SetReview(runId, review);
                store.Publish(runId, AgentKind.Review, AgentEventKind.Completed,
                    $"Review verdict: {review.Verdict} with {review.Findings.Count} finding(s)");
                return true;
            });

        if (!succeeded)
        {
            Fail(runId);
            return;
        }

        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Completed, "Run completed");
        store.Finish(runId, RunStatus.Completed);
    }

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
