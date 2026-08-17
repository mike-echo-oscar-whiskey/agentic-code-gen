using AgentCodeGen.Api.Abstractions;

namespace AgentCodeGen.Api.Infrastructure;

public sealed class NpmPackageRegistry(HttpClient httpClient) : IPackageRegistry
{
    public async Task<bool> ExistsAsync(string packageName, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"https://registry.npmjs.org/{Uri.EscapeDataString(packageName)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
