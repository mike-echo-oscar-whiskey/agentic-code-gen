using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Gates;

/// Resolves every declared dependency against the real npm registry.
/// Models hallucinate package names, and attackers pre-register the popular
/// hallucinations — an unresolvable dependency is a red flag, not a typo.
public sealed class DependencyGate(IPackageRegistry registry) : ICodeGate
{
    public const string GateName = "dependencies-resolve";

    public async Task<GateResult> CheckAsync(CodeArtifact code, CancellationToken cancellationToken = default)
    {
        if (code.Dependencies.Count == 0)
        {
            return new GateResult(GateName, true, "No external dependencies declared.");
        }

        var missing = new List<string>();
        foreach (var dependency in code.Dependencies)
        {
            if (!await registry.ExistsAsync(dependency, cancellationToken))
            {
                missing.Add(dependency);
            }
        }

        return missing.Count > 0
            ? new GateResult(GateName, false, $"Not found on the npm registry (possible hallucination): {string.Join(", ", missing)}")
            : new GateResult(GateName, true, $"All {code.Dependencies.Count} declared dependencies exist on npm.");
    }
}
