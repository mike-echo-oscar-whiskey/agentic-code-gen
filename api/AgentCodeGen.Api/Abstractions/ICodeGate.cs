using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Abstractions;

/// A deterministic check that runs on generated code before it is shown to the user.
public interface ICodeGate
{
    Task<GateResult> CheckAsync(CodeArtifact code, CancellationToken cancellationToken = default);
}
