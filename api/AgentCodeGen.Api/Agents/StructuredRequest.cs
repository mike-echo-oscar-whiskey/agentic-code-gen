namespace AgentCodeGen.Api.Agents;

/// A single LLM call that must answer through a tool schema — never prose.
public sealed record StructuredRequest(
    string SystemPrompt,
    IReadOnlyList<string> UserMessages,
    string ToolName,
    string ToolDescription,
    string ToolInputSchemaJson);
