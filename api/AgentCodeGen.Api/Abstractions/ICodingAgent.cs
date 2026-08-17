using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

public interface ICodingAgent
{
    Task<Either<AgentError, CodeArtifact>> GenerateAsync(string goal, CancellationToken cancellationToken = default);

    Task<Either<AgentError, CodeArtifact>> ReviseAsync(
        string goal,
        CodeArtifact previous,
        ReviewResult review,
        CancellationToken cancellationToken = default);
}
