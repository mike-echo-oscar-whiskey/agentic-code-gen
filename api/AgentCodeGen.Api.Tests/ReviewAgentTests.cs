using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;
using AwesomeAssertions;
using NSubstitute;

namespace AgentCodeGen.Api.Tests;

public class ReviewAgentTests
{
    private const string Goal = "summarise a Met artwork";

    private static readonly CodeArtifact Code = new(
        "typescript", "export const x = 1;", [], "why", []);

    private readonly IStructuredOutputClient _client = Substitute.For<IStructuredOutputClient>();

    private ReviewAgent CreateAgent() => new(_client);

    [Fact]
    public async Task ReviewAsync_SendsGoalAndCodeInUserTurns()
    {
        StubResponse(ValidPayload);
        var agent = CreateAgent();

        await agent.ReviewAsync(Goal, Code);

        var request = CapturedRequest();
        request.ToolName.Should().Be("emit_review");
        request.UserMessages.Should().Contain(m => m.Contains(Goal));
        request.UserMessages.Should().Contain(m => m.Contains(Code.Code));
    }

    [Fact]
    public async Task ReviewAsync_WithAValidPayload_ReturnsTheReview()
    {
        StubResponse(ValidPayload);
        var agent = CreateAgent();

        var result = await agent.ReviewAsync(Goal, Code);

        var review = result.Match(_ => null!, r => r);
        review.Verdict.Should().Be("changes-requested");
        review.Findings.Should().ContainSingle();
        review.Findings[0].Severity.Should().Be(ReviewSeverity.Major);
    }

    [Fact]
    public async Task ReviewAsync_WithAnUnknownSeverity_ReturnsAnError()
    {
        StubResponse("""
            { "verdict": "approved", "findings": [ { "severity": "catastrophic", "issue": "x", "suggestedChange": "y" } ] }
            """);
        var agent = CreateAgent();

        var result = await agent.ReviewAsync(Goal, Code);

        result.IsRight.Should().BeFalse();
    }

    private void StubResponse(string payload) =>
        _client.RequestAsync(Arg.Any<StructuredRequest>(), Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, string>.Right(payload));

    private StructuredRequest CapturedRequest() =>
        (StructuredRequest)_client.ReceivedCalls().Single().GetArguments()[0]!;

    private const string ValidPayload = """
        {
          "verdict": "changes-requested",
          "findings": [
            {
              "severity": "major",
              "issue": "No error handling for failed fetches.",
              "suggestedChange": "Check response.ok and throw a descriptive error."
            }
          ]
        }
        """;
}
