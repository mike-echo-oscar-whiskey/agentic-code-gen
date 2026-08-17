namespace AgentCodeGen.Api.Abstractions;

public interface IPackageRegistry
{
    Task<bool> ExistsAsync(string packageName, CancellationToken cancellationToken = default);
}
