import { Component, computed, input } from '@angular/core';
import { AgentEvent } from '../run-models';

@Component({
  selector: 'app-agent-timeline',
  imports: [],
  templateUrl: './agent-timeline.html',
  styleUrl: './agent-timeline.css'
})
export class AgentTimeline {
  readonly events = input.required<readonly AgentEvent[]>();
  readonly running = input(false);

  protected readonly startedAt = computed(() => {
    const first = this.events()[0];
    return first ? Date.parse(first.at) : null;
  });

  protected elapsed(agentEvent: AgentEvent): string {
    const start = this.startedAt();
    if (start === null) {
      return '';
    }

    const seconds = (Date.parse(agentEvent.at) - start) / 1000;
    return `+${seconds.toFixed(1)}s`;
  }

  protected isLast(agentEvent: AgentEvent): boolean {
    const events = this.events();
    return events[events.length - 1]?.sequence === agentEvent.sequence;
  }
}
