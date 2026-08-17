namespace AgentCodeGen.Api.Domain;

public sealed record AgentEvent(
    int Sequence,
    DateTimeOffset At,
    AgentKind Agent,
    AgentEventKind Kind,
    string Message);
