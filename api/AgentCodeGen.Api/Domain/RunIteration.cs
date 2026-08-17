namespace AgentCodeGen.Api.Domain;

/// A superseded attempt: the code and the review that sent it back for revision.
public sealed record RunIteration(int Number, CodeArtifact Code, ReviewResult Review);
