using System.Text.RegularExpressions;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Gates;

public sealed partial class HostAllowlistGate : ICodeGate
{
    public const string GateName = "host-allowlist";

    private static readonly IReadOnlySet<string> AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "collectionapi.metmuseum.org",
        "images.metmuseum.org",
        "metmuseum.github.io",
        "www.metmuseum.org",
    };

    [GeneratedRegex(@"https?://([A-Za-z0-9\.\-]+)")]
    private static partial Regex UrlHost();

    public Task<GateResult> CheckAsync(CodeArtifact code, CancellationToken cancellationToken = default)
    {
        var unknown = UrlHost().Matches(code.Code)
            .Select(m => m.Groups[1].Value)
            .Where(host => !AllowedHosts.Contains(host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(unknown.Count > 0
            ? new GateResult(GateName, false, $"Code references non-Met hosts: {string.Join(", ", unknown)}")
            : new GateResult(GateName, true, "All referenced hosts belong to the Met Museum."));
    }
}
