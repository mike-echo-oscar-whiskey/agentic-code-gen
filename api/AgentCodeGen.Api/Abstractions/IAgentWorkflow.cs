using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Abstractions;

public interface IAgentWorkflow
{
    Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default);
}
