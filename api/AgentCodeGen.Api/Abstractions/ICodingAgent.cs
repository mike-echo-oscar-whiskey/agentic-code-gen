using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

public interface ICodingAgent
{
    Task<Either<AgentError, CodeArtifact>> GenerateAsync(string goal, CancellationToken cancellationToken = default);
}
