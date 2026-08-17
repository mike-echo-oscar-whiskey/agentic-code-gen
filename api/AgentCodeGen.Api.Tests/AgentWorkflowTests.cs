using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;
using AgentCodeGen.Api.Infrastructure;
using AwesomeAssertions;
using NSubstitute;

namespace AgentCodeGen.Api.Tests;

public class AgentWorkflowTests
{
    private const string Goal = "summarise a Met artwork";

    private static readonly CodeArtifact Code = new(
        "typescript", "export const x = 1;", [], "why", []);

    private static readonly ReviewResult Review = new(
        "approved", []);

    private readonly InMemoryRunStore _store = new(TimeProvider.System);
    private readonly ICodingAgent _codingAgent = Substitute.For<ICodingAgent>();
    private readonly IReviewAgent _reviewAgent = Substitute.For<IReviewAgent>();

    private readonly List<ICodeGate> _gates = [];

    private AgentWorkflow CreateWorkflow() => new(_store, _codingAgent, _reviewAgent, _gates);

    [Fact]
    public async Task RunAsync_OnTheHappyPath_CompletesWithCodeAndReview()
    {
        StubSuccess();
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var snapshot = Snapshot(run.Id);
        snapshot.Status.Should().Be(RunStatus.Completed);
        snapshot.Code.IsSome.Should().BeTrue();
        snapshot.Review.IsSome.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_OnTheHappyPath_EmitsTheExpectedEventSequence()
    {
        StubSuccess();
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var events = Snapshot(run.Id).Events;
        events.Select(e => (e.Agent, e.Kind)).Should().ContainInOrder(
            (AgentKind.Orchestrator, AgentEventKind.Started),
            (AgentKind.Coding, AgentEventKind.Started),
            (AgentKind.Coding, AgentEventKind.Completed),
            (AgentKind.Review, AgentEventKind.Started),
            (AgentKind.Review, AgentEventKind.Completed),
            (AgentKind.Orchestrator, AgentEventKind.Completed));
    }

    [Fact]
    public async Task RunAsync_WhenTheCodingAgentFails_FailsTheRunAndSkipsReview()
    {
        _codingAgent.GenerateAsync(Goal, Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, CodeArtifact>.Left(new AgentError("model refused")));
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var snapshot = Snapshot(run.Id);
        snapshot.Status.Should().Be(RunStatus.Failed);
        snapshot.Events.Should().Contain(e =>
            e.Agent == AgentKind.Coding && e.Kind == AgentEventKind.Failed && e.Message.Contains("model refused"));
        await _reviewAgent.DidNotReceiveWithAnyArgs().ReviewAsync(default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_WhenTheReviewAgentFails_KeepsTheCodeButFailsTheRun()
    {
        _codingAgent.GenerateAsync(Goal, Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, CodeArtifact>.Right(Code));
        _reviewAgent.ReviewAsync(Goal, Code, Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, ReviewResult>.Left(new AgentError("timeout")));
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var snapshot = Snapshot(run.Id);
        snapshot.Status.Should().Be(RunStatus.Failed);
        snapshot.Code.IsSome.Should().BeTrue();
        snapshot.Review.IsSome.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_RunsTheGatesAndStoresTheirResults()
    {
        StubSuccess();
        var gate = Substitute.For<ICodeGate>();
        gate.CheckAsync(Code, Arg.Any<CancellationToken>())
            .Returns(new GateResult("no-banned-constructs", true, "clean"));
        _gates.Add(gate);
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var snapshot = Snapshot(run.Id);
        snapshot.Gates.Should().ContainSingle(g => g.Name == "no-banned-constructs" && g.Passed);
        snapshot.Status.Should().Be(RunStatus.Completed);
    }

    [Fact]
    public async Task RunAsync_AFailingGateDoesNotFailTheRun()
    {
        StubSuccess();
        var gate = Substitute.For<ICodeGate>();
        gate.CheckAsync(Code, Arg.Any<CancellationToken>())
            .Returns(new GateResult("host-allowlist", false, "unknown host"));
        _gates.Add(gate);
        var run = _store.Create(Goal);

        await CreateWorkflow().RunAsync(run.Id, Goal);

        var snapshot = Snapshot(run.Id);
        snapshot.Status.Should().Be(RunStatus.Completed);
        snapshot.Gates.Should().ContainSingle(g => !g.Passed);
    }

    private void StubSuccess()
    {
        _codingAgent.GenerateAsync(Goal, Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, CodeArtifact>.Right(Code));
        _reviewAgent.ReviewAsync(Goal, Code, Arg.Any<CancellationToken>())
            .Returns(Either<AgentError, ReviewResult>.Right(Review));
    }

    private AgentRun Snapshot(RunId id) =>
        _store.Get(id).Match(r => r, () => throw new InvalidOperationException("run missing"));
}
