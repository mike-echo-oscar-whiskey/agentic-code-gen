using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Endpoints;
using AgentCodeGen.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
builder.Services.AddSingleton<IAgentWorkflow, StubAgentWorkflow>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapRunEndpoints();

app.Run();

public partial class Program;
