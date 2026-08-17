using System.Text.Json;
using AgentCodeGen.Api.Abstractions;
using AgentCodeGen.Api.Agents;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace AgentCodeGen.Api.Infrastructure;

public sealed class AnthropicStructuredOutputClient : IStructuredOutputClient
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;
    private readonly ILogger<AnthropicStructuredOutputClient> _logger;

    public AnthropicStructuredOutputClient(
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicStructuredOutputClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<Either<AgentError, string>> RequestAsync(
        StructuredRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var response = await _client.Messages.Create(BuildParams(request), cancellationToken: timeout.Token);

            _logger.LogInformation(
                "Anthropic call for tool {Tool}: stop_reason={StopReason} in={InputTokens} out={OutputTokens}",
                request.ToolName, response.StopReason, response.Usage.InputTokens, response.Usage.OutputTokens);

            if (response.StopReason?.ToString() == "max_tokens")
            {
                return Either<AgentError, string>.Left(
                    new AgentError("The model ran out of output tokens before finishing."));
            }

            foreach (var block in response.Content)
            {
                if (block.TryPickToolUse(out var toolUse) && toolUse!.Name == request.ToolName)
                {
                    return Either<AgentError, string>.Right(JsonSerializer.Serialize(toolUse.Input));
                }
            }

            return Either<AgentError, string>.Left(
                new AgentError($"The model did not call the {request.ToolName} tool (stop_reason: {response.StopReason})."));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Either<AgentError, string>.Left(
                new AgentError($"The model did not respond within {_options.TimeoutSeconds}s."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Anthropic call for tool {Tool} failed", request.ToolName);
            return Either<AgentError, string>.Left(new AgentError("The model call failed. See the API logs."));
        }
    }

    private MessageCreateParams BuildParams(StructuredRequest request)
    {
        // Pass the schema through verbatim — it already carries type/properties/required
        // and the additionalProperties: false that strict tool use demands.
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.ToolInputSchemaJson)!;

        return new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = request.SystemPrompt,
            Messages = [.. request.UserMessages.Select(m => new MessageParam { Role = Role.User, Content = m })],
            Tools =
            [
                new Tool
                {
                    Name = request.ToolName,
                    Description = request.ToolDescription,
                    Strict = true,
                    InputSchema = InputSchema.FromRawUnchecked(schema),
                }
            ],
            ToolChoice = new ToolChoiceTool { Name = request.ToolName },
        };
    }
}
