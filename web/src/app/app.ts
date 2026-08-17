import { Component, inject } from '@angular/core';
import { RunClient } from './runs/run-client';
import { GoalComposer } from './runs/goal-composer/goal-composer';
import { AgentTimeline } from './runs/agent-timeline/agent-timeline';
import { CodePanel } from './runs/code-panel/code-panel';
import { ReviewPanel } from './runs/review-panel/review-panel';

@Component({
  selector: 'app-root',
  imports: [GoalComposer, AgentTimeline, CodePanel, ReviewPanel],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly run = inject(RunClient);
}
