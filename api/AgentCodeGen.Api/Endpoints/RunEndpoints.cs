using System.Text.Json;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Contracts;
using AgentCodeGen.Api.Domain;

namespace AgentCodeGen.Api.Endpoints;

public static class RunEndpoints
{
    public const int MaxGoalLength = 500;

    private static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/runs").WithTags("Runs");

        group.MapPost("/", StartRun)
            .WithSummary("Starts an agent workflow for a goal")
            .Produces<StartRunResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetRun)
            .WithSummary("Returns the full state of a run")
            .Produces<RunSnapshotResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/events", StreamEvents)
            .WithSummary("Streams agent activity as server-sent events")
            .WithDescription("Replays events already recorded, then streams new ones until the run reaches a terminal status.")
            .Produces<string>(StatusCodes.Status200OK, "text/event-stream")
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult StartRun(
        StartRunRequest request,
        IRunStore store,
        IAgentWorkflow workflow,
        ILogger<Program> logger)
    {
        var goal = request.Goal?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(goal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["goal"] = ["A goal is required."]
            });
        }

        if (goal.Length > MaxGoalLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["goal"] = [$"A goal may be at most {MaxGoalLength} characters."]
            });
        }

        var run = store.Create(goal);

        _ = Task.Run(async () =>
        {
            try
            {
                await workflow.RunAsync(run.Id, goal);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Agent workflow for run {RunId} failed", run.Id);
                store.Publish(run.Id, AgentKind.Orchestrator, AgentEventKind.Failed, "The workflow failed unexpectedly.");
                store.Finish(run.Id, RunStatus.Failed);
            }
        });

        return Results.Accepted($"/api/runs/{run.Id}", new StartRunResponse(run.Id.Value));
    }

    private static IResult GetRun(Guid id, IRunStore store) =>
        store.Get(new RunId(id)).Match(
            run => Results.Ok(run.ToResponse()),
            () => Results.NotFound());

    private static async Task StreamEvents(
        Guid id,
        IRunStore store,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var runId = new RunId(id);

        if (!store.Get(runId).IsSome)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await foreach (var agentEvent in store.SubscribeAsync(runId, cancellationToken))
        {
            var payload = JsonSerializer.Serialize(agentEvent.ToResponse(), EventJson);
            await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}
