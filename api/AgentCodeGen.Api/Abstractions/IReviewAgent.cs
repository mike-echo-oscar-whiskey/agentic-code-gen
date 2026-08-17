using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

public interface IReviewAgent
{
    Task<Either<AgentError, ReviewResult>> ReviewAsync(string goal, CodeArtifact code, CancellationToken cancellationToken = default);
}
