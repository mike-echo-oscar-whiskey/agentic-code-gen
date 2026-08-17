using System.Text.RegularExpressions;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Gates;

public sealed partial class BannedConstructsGate : ICodeGate
{
    public const string GateName = "no-banned-constructs";

    [GeneratedRegex(@"\beval\s*\(|new\s+Function\s*\(|child_process|\bexecSync\b|\bspawnSync\b", RegexOptions.IgnoreCase)]
    private static partial Regex Banned();

    public Task<GateResult> CheckAsync(CodeArtifact code, CancellationToken cancellationToken = default)
    {
        var match = Banned().Match(code.Code);
        return Task.FromResult(match.Success
            ? new GateResult(GateName, false, $"Found banned construct: {match.Value.Trim()}")
            : new GateResult(GateName, true, "No eval, Function constructor, or process execution found."));
    }
}
