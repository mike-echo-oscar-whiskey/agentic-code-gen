export type AgentName = 'orchestrator' | 'coding' | 'review';

export type AgentEventKind = 'started' | 'progress' | 'completed' | 'failed';

export type RunStatus = 'running' | 'completed' | 'failed';

export interface AgentEvent {
  readonly sequence: number;
  readonly at: string;
  readonly agent: AgentName;
  readonly kind: AgentEventKind;
  readonly message: string;
}

export interface CodeArtifact {
  readonly language: string;
  readonly code: string;
  readonly dependencies: readonly string[];
  readonly explanation: string;
  readonly assumptions: readonly string[];
}

export type ReviewSeverity = 'info' | 'minor' | 'major' | 'blocking';

export interface ReviewFinding {
  readonly severity: ReviewSeverity;
  readonly issue: string;
  readonly suggestedChange: string;
}

export interface Review {
  readonly verdict: string;
  readonly findings: readonly ReviewFinding[];
}

export interface GateResult {
  readonly name: string;
  readonly passed: boolean;
  readonly detail: string;
}

export interface RunIteration {
  readonly number: number;
  readonly code: CodeArtifact;
  readonly review: Review;
}

export interface RunSnapshot {
  readonly id: string;
  readonly goal: string;
  readonly status: RunStatus;
  readonly events: readonly AgentEvent[];
  readonly code: CodeArtifact | null;
  readonly review: Review | null;
  readonly gates: readonly GateResult[];
  readonly history: readonly RunIteration[];
}
