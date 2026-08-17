using System.Text.Json;
using System.Text.Json.Serialization;
using AgentCodeGen.Api.Domain;
using AgentCodeGen.Api.Functional;

namespace AgentCodeGen.Api.Agents;

public static class StructuredPayload
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static Either<AgentError, T> Deserialize<T>(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<T>(json, Options);
            return payload is null
                ? Either<AgentError, T>.Left(new AgentError("The model returned an empty payload."))
                : Either<AgentError, T>.Right(payload);
        }
        catch (JsonException exception)
        {
            return Either<AgentError, T>.Left(
                new AgentError($"The model's payload did not match the schema: {exception.Message}"));
        }
    }
}
