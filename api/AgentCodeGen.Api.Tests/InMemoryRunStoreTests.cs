using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Infrastructure;
using AwesomeAssertions;

namespace AgentCodeGen.Api.Tests;

public class InMemoryRunStoreTests
{
    private static InMemoryRunStore CreateStore() => new(TimeProvider.System);

    [Fact]
    public void Create_ReturnsRunningRunCarryingTheGoal()
    {
        var store = CreateStore();

        var run = store.Create("format a Met artwork summary");

        run.Goal.Should().Be("format a Met artwork summary");
        run.Status.Should().Be(RunStatus.Running);
        run.Events.Should().BeEmpty();
    }

    [Fact]
    public void Get_ForUnknownRunId_ReturnsNone()
    {
        var store = CreateStore();

        store.Get(RunId.New()).IsSome.Should().BeFalse();
    }

    [Fact]
    public void Publish_AssignsIncrementingSequenceNumbers()
    {
        var store = CreateStore();
        var run = store.Create("goal");

        store.Publish(run.Id, AgentKind.Orchestrator, AgentEventKind.Started, "first");
        store.Publish(run.Id, AgentKind.Coding, AgentEventKind.Started, "second");

        var events = store.Get(run.Id).Match(r => r.Events, () => []);
        events.Select(e => e.Sequence).Should().Equal(1, 2);
        events.Select(e => e.Message).Should().Equal("first", "second");
    }

    [Fact]
    public async Task SubscribeAsync_ReplaysEventsPublishedBeforeSubscribing()
    {
        var store = CreateStore();
        var run = store.Create("goal");
        store.Publish(run.Id, AgentKind.Coding, AgentEventKind.Started, "already happened");
        store.Finish(run.Id, RunStatus.Completed);

        var received = new List<AgentEvent>();
        await foreach (var agentEvent in store.SubscribeAsync(run.Id))
        {
            received.Add(agentEvent);
        }

        received.Select(e => e.Message).Should().Equal("already happened");
    }

    [Fact]
    public async Task SubscribeAsync_StreamsEventsPublishedAfterSubscribing()
    {
        var store = CreateStore();
        var run = store.Create("goal");

        await using var subscription = store.SubscribeAsync(run.Id).GetAsyncEnumerator();
        var pending = subscription.MoveNextAsync();

        store.Publish(run.Id, AgentKind.Coding, AgentEventKind.Started, "live event");

        (await pending).Should().BeTrue();
        subscription.Current.Message.Should().Be("live event");
    }

    [Fact]
    public void Finish_MarksTheRunWithTheTerminalStatus()
    {
        var store = CreateStore();
        var run = store.Create("goal");

        store.Finish(run.Id, RunStatus.Failed);

        store.Get(run.Id).Match(r => r.Status, () => RunStatus.Running).Should().Be(RunStatus.Failed);
    }

    [Fact]
    public void SetCodeAndSetReview_AreVisibleOnTheRunSnapshot()
    {
        var store = CreateStore();
        var run = store.Create("goal");
        var code = new CodeArtifact("typescript", "export const x = 1;", [], "why", []);
        var review = new ReviewResult("approved", []);

        store.SetCode(run.Id, code);
        store.SetReview(run.Id, review);

        var snapshot = store.Get(run.Id).Match(r => r, () => throw new InvalidOperationException("run missing"));
        snapshot.Code.Match(c => c.Code, () => "").Should().Be("export const x = 1;");
        snapshot.Review.Match(r => r.Verdict, () => "").Should().Be("approved");
    }
}
