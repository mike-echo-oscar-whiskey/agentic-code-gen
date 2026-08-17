namespace AgentCodeGen.Api.Infrastructure;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; init; } = "";

    public string Model { get; init; } = "claude-opus-5";

    public int MaxTokens { get; init; } = 8192;

    public int TimeoutSeconds { get; init; } = 120;
}
