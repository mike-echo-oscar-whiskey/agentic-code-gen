using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Endpoints;
using AgentCodeGen.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();

var apiKey = builder.Configuration["Anthropic:ApiKey"];
if (string.IsNullOrEmpty(apiKey))
{
    // No key configured: fall back to the stub so the app still demos end-to-end.
    builder.Services.AddSingleton<IAgentWorkflow, StubAgentWorkflow>();
}
else
{
    builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));
    builder.Services.AddSingleton<IStructuredOutputClient, AnthropicStructuredOutputClient>();
    builder.Services.AddSingleton<ICodingAgent, CodingAgent>();
    builder.Services.AddSingleton<IReviewAgent, ReviewAgent>();
    builder.Services.AddSingleton<IAgentWorkflow, AgentWorkflow>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapRunEndpoints();

app.Run();

public partial class Program;
