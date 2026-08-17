using System.Text.RegularExpressions;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Gates;

public sealed partial class SecretScanGate : ICodeGate
{
    public const string GateName = "no-secret-literals";

    [GeneratedRegex(@"sk-[A-Za-z0-9\-_]{16,}|AKIA[0-9A-Z]{16}|Bearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+|ghp_[A-Za-z0-9]{20,}")]
    private static partial Regex SecretShaped();

    public Task<GateResult> CheckAsync(CodeArtifact code, CancellationToken cancellationToken = default)
    {
        var match = SecretShaped().Match(code.Code);
        return Task.FromResult(match.Success
            ? new GateResult(GateName, false, "Found a secret-shaped literal in the generated code.")
            : new GateResult(GateName, true, "No API-key or token-shaped literals found."));
    }
}
