using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Agents;

// Placeholder workflow that proves the end-to-end pipeline before the real agents exist.
public sealed class StubAgentWorkflow(IRunStore store, TimeProvider clock) : IAgentWorkflow
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(600);

    public async Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default)
    {
        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Started, $"Received goal: {goal}");
        await Task.Delay(StepDelay, clock, cancellationToken);

        store.Publish(runId, AgentKind.Coding, AgentEventKind.Started, "Generating code against the Met Museum Collection API");
        await Task.Delay(StepDelay, clock, cancellationToken);

        store.SetCode(runId, PlaceholderCode);
        store.Publish(runId, AgentKind.Coding, AgentEventKind.Completed, "Produced a TypeScript function");
        await Task.Delay(StepDelay, clock, cancellationToken);

        store.Publish(runId, AgentKind.Review, AgentEventKind.Started, "Reviewing the generated code");
        await Task.Delay(StepDelay, clock, cancellationToken);

        store.SetReview(runId, PlaceholderReview);
        store.Publish(runId, AgentKind.Review, AgentEventKind.Completed, "Review finished with 1 finding");

        store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Completed, "Run completed");
        store.Finish(runId, RunStatus.Completed);
    }

    private static CodeArtifact PlaceholderCode => new(
        "typescript",
        """
        export async function searchArtworks(query: string): Promise<Artwork[]> {
          // placeholder produced by the stub workflow
          return [];
        }
        """,
        [],
        "Placeholder output emitted while the real Coding Agent is not wired up yet.",
        ["The stub does not call an LLM."]);

    private static ReviewResult PlaceholderReview => new(
        "changes-requested",
        [
            new ReviewFinding(
                ReviewSeverity.Major,
                "The function returns an empty array and never calls the Met Museum API.",
                "Call /public/collection/v1/search and then fetch each object by id.")
        ]);
}
