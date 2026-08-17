import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AgentEvent, CodeArtifact, GateResult, Review, RunIteration, RunSnapshot, RunStatus } from './run-models';

type ClientStatus = 'idle' | RunStatus;

@Injectable({ providedIn: 'root' })
export class RunClient {
  private readonly http = inject(HttpClient);
  private stream: EventSource | null = null;

  readonly goal = signal('');
  readonly status = signal<ClientStatus>('idle');
  readonly events = signal<readonly AgentEvent[]>([]);
  readonly code = signal<CodeArtifact | null>(null);
  readonly review = signal<Review | null>(null);
  readonly gates = signal<readonly GateResult[]>([]);
  readonly history = signal<readonly RunIteration[]>([]);
  readonly error = signal<string | null>(null);

  readonly isRunning = computed(() => this.status() === 'running');
  readonly hasStarted = computed(() => this.status() !== 'idle');

  async start(goal: string): Promise<void> {
    this.closeStream();
    this.goal.set(goal);
    this.events.set([]);
    this.code.set(null);
    this.review.set(null);
    this.gates.set([]);
    this.history.set([]);
    this.error.set(null);
    this.status.set('running');

    try {
      const started = await firstValueFrom(
        this.http.post<{ runId: string }>('/api/runs', { goal })
      );
      this.listen(started.runId);
    } catch {
      this.error.set('Could not start the run. Is the API running?');
      this.status.set('failed');
    }
  }

  private listen(runId: string): void {
    const stream = new EventSource(`/api/runs/${runId}/events`);
    this.stream = stream;

    stream.onmessage = (message) => {
      const agentEvent = JSON.parse(message.data) as AgentEvent;
      this.events.update((events) => [...events, agentEvent]);

      const isTerminal =
        agentEvent.agent === 'orchestrator' &&
        (agentEvent.kind === 'completed' || agentEvent.kind === 'failed');

      if (isTerminal) {
        this.closeStream();
        void this.loadSnapshot(runId);
      }
    };

    stream.onerror = () => {
      this.closeStream();
      if (this.status() === 'running') {
        this.error.set('Lost the connection to the agent stream.');
        this.status.set('failed');
      }
    };
  }

  private async loadSnapshot(runId: string): Promise<void> {
    try {
      const snapshot = await firstValueFrom(
        this.http.get<RunSnapshot>(`/api/runs/${runId}`)
      );
      this.code.set(snapshot.code);
      this.review.set(snapshot.review);
      this.gates.set(snapshot.gates);
      this.history.set(snapshot.history);
      this.status.set(snapshot.status);
    } catch {
      this.error.set('The run finished but its result could not be loaded.');
      this.status.set('failed');
    }
  }

  private closeStream(): void {
    this.stream?.close();
    this.stream = null;
  }
}
