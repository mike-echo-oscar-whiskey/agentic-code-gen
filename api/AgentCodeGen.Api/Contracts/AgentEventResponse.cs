namespace AgentCodeGen.Api.Contracts;

public sealed record AgentEventResponse(
    int Sequence,
    DateTimeOffset At,
    string Agent,
    string Kind,
    string Message);
