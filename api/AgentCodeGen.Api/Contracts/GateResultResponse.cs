namespace AgentCodeGen.Api.Contracts;

public sealed record GateResultResponse(string Name, bool Passed, string Detail);
