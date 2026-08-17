using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Abstractions;

/// Supplies a real, trimmed Met API response so the Coding Agent matches the
/// actual response shape instead of hallucinating one. None on failure — the
/// workflow degrades to ungrounded generation rather than failing the run.
public interface IGroundingProvider
{
    Task<Option<string>> GetMetSampleAsync(CancellationToken cancellationToken = default);
}
