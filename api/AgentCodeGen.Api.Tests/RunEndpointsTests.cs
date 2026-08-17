using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Domain;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentCodeGen.Api.Tests;

public class RunEndpointsTests
{
    private static WebApplicationFactory<Program> CreateFactory(IAgentWorkflow workflow) =>
        CreateFactory(services => services.AddSingleton(workflow));

    private static WebApplicationFactory<Program> CreateFactory<TWorkflow>()
        where TWorkflow : class, IAgentWorkflow =>
        CreateFactory(services => services.AddSingleton<IAgentWorkflow, TWorkflow>());

    private static WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection> registerWorkflow) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAgentWorkflow>();
                registerWorkflow(services);
            }));

    [Fact]
    public async Task PostRun_WithAGoal_Returns202AndARunId()
    {
        var workflow = new RecordingWorkflow();
        using var factory = CreateFactory(workflow);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/runs", new { goal = "summarise a Met artwork" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("runId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostRun_HandsTheGoalToTheWorkflow()
    {
        var workflow = new RecordingWorkflow();
        using var factory = CreateFactory(workflow);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/runs", new { goal = "summarise a Met artwork" });

        var invocation = await workflow.Invoked.WaitAsync(TimeSpan.FromSeconds(5));
        invocation.Goal.Should().Be("summarise a Met artwork");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostRun_WithABlankGoal_Returns400(string goal)
    {
        using var factory = CreateFactory(new RecordingWorkflow());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/runs", new { goal });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostRun_WithAnOverlongGoal_Returns400()
    {
        using var factory = CreateFactory(new RecordingWorkflow());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/runs", new { goal = new string('a', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRun_ForAnUnknownId_Returns404()
    {
        using var factory = CreateFactory(new RecordingWorkflow());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRun_ReturnsTheGoalAndTerminalStateProducedByTheWorkflow()
    {
        using var factory = CreateFactory<CannedWorkflow>();
        using var client = factory.CreateClient();

        var started = await client.PostAsJsonAsync("/api/runs", new { goal = "summarise a Met artwork" });
        var runId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();

        var snapshot = await PollUntilCompleted(client, runId);

        snapshot.GetProperty("goal").GetString().Should().Be("summarise a Met artwork");
        snapshot.GetProperty("status").GetString().Should().Be("completed");
        snapshot.GetProperty("code").GetProperty("code").GetString().Should().Be("export const x = 1;");
        snapshot.GetProperty("review").GetProperty("verdict").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task GetRunEvents_StreamsAgentActivityAsServerSentEvents()
    {
        using var factory = CreateFactory<CannedWorkflow>();
        using var client = factory.CreateClient();

        var started = await client.PostAsJsonAsync("/api/runs", new { goal = "summarise a Met artwork" });
        var runId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetGuid();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/runs/{runId}/events");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var payloads = await ReadEventStream(response);

        payloads.Should().HaveCount(3);
        payloads[0].GetProperty("agent").GetString().Should().Be("orchestrator");
        payloads[1].GetProperty("agent").GetString().Should().Be("coding");
        payloads[2].GetProperty("kind").GetString().Should().Be("completed");
        payloads.Select(p => p.GetProperty("sequence").GetInt32()).Should().Equal(1, 2, 3);
    }

    private static async Task<JsonElement> PollUntilCompleted(HttpClient client, Guid runId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            var snapshot = await client.GetFromJsonAsync<JsonElement>($"/api/runs/{runId}", timeout.Token);
            if (snapshot.GetProperty("status").GetString() != "running")
            {
                return snapshot;
            }

            await Task.Delay(25, timeout.Token);
        }

        throw new TimeoutException("run did not reach a terminal status");
    }

    private static async Task<List<JsonElement>> ReadEventStream(HttpResponseMessage response)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);

        var payloads = new List<JsonElement>();
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                payloads.Add(JsonDocument.Parse(line[6..]).RootElement.Clone());
            }
        }

        return payloads;
    }

    private sealed class RecordingWorkflow : IAgentWorkflow
    {
        private readonly TaskCompletionSource<(RunId RunId, string Goal)> _invoked =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<(RunId RunId, string Goal)> Invoked => _invoked.Task;

        public Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default)
        {
            _invoked.TrySetResult((runId, goal));
            return Task.CompletedTask;
        }
    }

    private sealed class CannedWorkflow(IRunStore store) : IAgentWorkflow
    {
        public Task RunAsync(RunId runId, string goal, CancellationToken cancellationToken = default)
        {
            store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Started, "run started");
            store.Publish(runId, AgentKind.Coding, AgentEventKind.Completed, "code generated");
            store.SetCode(runId, new CodeArtifact("typescript", "export const x = 1;", [], "why", []));
            store.SetReview(runId, new ReviewResult("approved", []));
            store.Publish(runId, AgentKind.Orchestrator, AgentEventKind.Completed, "run completed");
            store.Finish(runId, RunStatus.Completed);
            return Task.CompletedTask;
        }
    }
}
