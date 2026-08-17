using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;
using AwesomeAssertions;
using NSubstitute;

namespace AgentCodeGen.Api.Tests;

public class CodingAgentTests
{
    private const string Goal = "summarise a Met artwork";

    private readonly IStructuredOutputClient _client = Substitute.For<IStructuredOutputClient>();

    private CodingAgent CreateAgent() => new(_client);

    [Fact]
    public async Task GenerateAsync_SendsTheGoalInAUserTurn_NeverTheSystemPrompt()
    {
        StubResponse(ValidPayload);
        var agent = CreateAgent();

        await agent.GenerateAsync(Goal);

        var request = CapturedRequest();
        request.SystemPrompt.Should().NotContain(Goal);
        request.UserMessages.Should().Contain(m => m.Contains(Goal));
    }

    [Fact]
    public async Task GenerateAsync_ForcesTheEmitCodeToolSchema()
    {
        StubResponse(ValidPayload);
        var agent = CreateAgent();

        await agent.GenerateAsync(Goal);

        var request = CapturedRequest();
        request.ToolName.Should().Be("emit_code");
        request.ToolInputSchemaJson.Should().Contain("\"additionalProperties\": false");
        request.ToolInputSchemaJson.Should().Contain("\"required\"");
    }

    [Fact]
    public async Task GenerateAsync_WithAValidPayload_ReturnsTheCodeArtifact()
    {
        StubResponse(ValidPayload);
        var agent = CreateAgent();

        var result = await agent.GenerateAsync(Goal);

        var artifact = result.Match(_ => null!, code => code);
        artifact.Language.Should().Be("typescript");
        artifact.Code.Should().Contain("searchArtworks");
        artifact.Dependencies.Should().BeEmpty();
        artifact.Assumptions.Should().ContainSingle();
    }

    [Fact]
    public async Task GenerateAsync_WithMalformedJson_ReturnsAnError()
    {
        StubResponse("{ not json");
        var agent = CreateAgent();

        var result = await agent.GenerateAsync(Goal);

        result.IsRight.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithMissingRequiredFields_ReturnsAnError()
    {
        StubResponse("""{ "language": "typescript" }""");
        var agent = CreateAgent();

        var result = await agent.GenerateAsync(Goal);

        result.IsRight.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WhenTheClientFails_PropagatesTheError()
    {
        _client.RequestAsync(Arg.Any<StructuredRequest>(), Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, string>.Left(new AgentError("model unavailable")));
        var agent = CreateAgent();

        var result = await agent.GenerateAsync(Goal);

        result.Match(e => e.Message, _ => "").Should().Be("model unavailable");
    }

    private void StubResponse(string payload) =>
        _client.RequestAsync(Arg.Any<StructuredRequest>(), Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, string>.Right(payload));

    private StructuredRequest CapturedRequest() =>
        (StructuredRequest)_client.ReceivedCalls().Single().GetArguments()[0]!;

    private const string ValidPayload = """
        {
          "language": "typescript",
          "code": "export async function searchArtworks(q: string) { return []; }",
          "dependencies": [],
          "explanation": "Searches the Met collection.",
          "assumptions": ["The API needs no auth."]
        }
        """;
}
