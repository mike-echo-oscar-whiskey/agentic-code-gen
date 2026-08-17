using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

/// Seam to the LLM vendor: returns the raw JSON the model passed to the forced tool.
public interface IStructuredOutputClient
{
    Task<Either<AgentError, string>> RequestAsync(StructuredRequest request, CancellationToken cancellationToken = default);
}
